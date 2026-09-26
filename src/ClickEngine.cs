using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace McClicker
{
    // Silnik: globalne hooki (klawisz wl/wyl + trzymanie przycisku) i watek klikajacy przez SendInput.
    internal sealed class ClickEngine : IDisposable
    {
        // Znacznik w dwExtraInfo, po ktorym hook rozpoznaje nasze wlasne klikniecia.
        static readonly IntPtr Signature = new IntPtr(0x4D43434B);
        static readonly int InputSize = Marshal.SizeOf(typeof(Native.INPUT));

        // Ustawienia zmieniane z GUI.
        public volatile int CpsMin = 10;
        public volatile int CpsMax = 14;
        public volatile bool HoldMode;        // false = klika caly czas, true = tylko gdy trzymasz przycisk
        public volatile bool RightButton;     // false = LPM, true = PPM
        public volatile bool OnlyMinecraft = true;
        public volatile bool SkipMenus = true;
        public volatile int HotkeyVk = 0x75;  // F6

        // Wywolywane z watku hooka - GUI musi uzyc BeginInvoke.
        public event Action<bool> EnabledChanged;
        public event Action<int> HotkeyBound;

        public volatile string HookError;

        volatile bool enabled;
        volatile bool binding;
        volatile bool running;
        volatile bool held;         // fizyczny przycisk trzymany (tryb trzymania)
        volatile bool blockedDown;  // zablokowalismy fizyczne wcisniecie, wiec blokujemy tez puszczenie
        volatile bool fgIsTarget;
        volatile bool fgIsMinecraft;
        volatile string fgTitle = "";
        volatile string fgExe = "";

        IntPtr fgHwnd;
        uint fgPid;
        readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;
        readonly IntPtr[] menuCursors;
        readonly bool[] keyDown = new bool[256];

        Thread hookThread, clickThread;
        uint hookThreadId;
        IntPtr mouseHook, keyHook;
        Native.HookProc mouseProc, keyProc; // trzymamy referencje, zeby GC nie usunal delegatow
        IntPtr timer;                       // precyzyjny timer (Windows 10 1803+), inaczej Thread.Sleep
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Queue<long> clickTimes = new Queue<long>();

        public ClickEngine()
        {
            menuCursors = new IntPtr[]
            {
                Native.LoadCursor(IntPtr.Zero, new IntPtr(Native.IDC_ARROW)),
                Native.LoadCursor(IntPtr.Zero, new IntPtr(Native.IDC_IBEAM)),
                Native.LoadCursor(IntPtr.Zero, new IntPtr(Native.IDC_HAND)),
            };
        }

        public bool Enabled { get { return enabled; } }
        public bool IsBinding { get { return binding; } }
        public string ForegroundTitle { get { return fgTitle; } }
        public string ForegroundExe { get { return fgExe; } }
        public bool ForegroundIsMinecraft { get { return fgIsMinecraft; } }

        public void Start()
        {
            running = true;
            hookThread = new Thread(HookLoop);
            hookThread.IsBackground = true;
            hookThread.Priority = ThreadPriority.Highest;
            hookThread.Start();

            clickThread = new Thread(ClickLoop);
            clickThread.IsBackground = true;
            clickThread.Priority = ThreadPriority.AboveNormal;
            clickThread.Start();
        }

        public void SetEnabled(bool on)
        {
            if (enabled == on) return;
            enabled = on;
            if (!on) held = false;
            wake.Set();
            Action<bool> handler = EnabledChanged;
            if (handler != null) handler(on);
        }

        public void Toggle() { SetEnabled(!enabled); }

        public void BeginBinding() { binding = true; }

        public int MeasuredCps()
        {
            long now = clock.ElapsedMilliseconds;
            lock (clickTimes)
            {
                while (clickTimes.Count > 0 && now - clickTimes.Peek() > 1000) clickTimes.Dequeue();
                return clickTimes.Count;
            }
        }

        // ---------------- Hooki ----------------

        void HookLoop()
        {
            hookThreadId = Native.GetCurrentThreadId();
            mouseProc = MouseHook;
            keyProc = KeyHook;
            IntPtr module = Native.GetModuleHandle(null);
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, module, 0);
            keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, module, 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero)
                HookError = "Nie udalo sie zalozyc hooka (blad " + Marshal.GetLastWin32Error() + ")";

            Native.MSG msg;
            while (running && Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }

            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
        }

        IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                if (info.dwExtraInfo != Signature)
                {
                    int msg = wParam.ToInt32();
                    int downMsg = RightButton ? Native.WM_RBUTTONDOWN : Native.WM_LBUTTONDOWN;
                    int upMsg = RightButton ? Native.WM_RBUTTONUP : Native.WM_LBUTTONUP;

                    if (msg == Native.WM_MBUTTONDOWN)
                    {
                        OnKey(Native.VK_MBUTTON);
                    }
                    else if (msg == Native.WM_XBUTTONDOWN)
                    {
                        OnKey((info.mouseData >> 16) == 1 ? Native.VK_XBUTTON1 : Native.VK_XBUTTON2);
                    }
                    else if (msg == downMsg)
                    {
                        // Tryb trzymania: prawdziwe wcisniecie zastepujemy seria naszych klikniec.
                        if (enabled && HoldMode && TargetReady())
                        {
                            blockedDown = true;
                            held = true;
                            wake.Set();
                            return new IntPtr(1);
                        }
                    }
                    else if (msg == upMsg)
                    {
                        held = false;
                        if (blockedDown)
                        {
                            blockedDown = false;
                            return new IntPtr(1);
                        }
                    }
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam) & 0xFF; // vkCode to pierwsze pole KBDLLHOOKSTRUCT
                if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN)
                {
                    if (!keyDown[vk]) // ignoruj autopowtarzanie przytrzymanego klawisza
                    {
                        keyDown[vk] = true;
                        OnKey(vk);
                    }
                }
                else if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP)
                {
                    keyDown[vk] = false;
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        void OnKey(int vk)
        {
            if (binding)
            {
                binding = false;
                if (vk != Native.VK_ESCAPE) HotkeyVk = vk;
                Action<int> handler = HotkeyBound;
                if (handler != null) handler(HotkeyVk);
                return;
            }
            if (vk == HotkeyVk) Toggle();
        }

        // ---------------- Klikanie ----------------

        void ClickLoop()
        {
            DisablePowerThrottling();
            Native.timeBeginPeriod(1);
            timer = Native.CreateWaitableTimerEx(IntPtr.Zero, null,
                Native.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, Native.TIMER_ALL_ACCESS);
            Random rnd = new Random();
            double next = 0; // zaplanowany czas nastepnego klikniecia (0 = kliknij od razu)
            try
            {
                while (running)
                {
                    UpdateForeground();
                    if (!ShouldClick())
                    {
                        next = 0;
                        wake.WaitOne(10);
                        continue;
                    }
                    if (next > Now())
                    {
                        WaitUntil(next, true);
                        continue; // sprawdz warunki jeszcze raz
                    }

                    int lo = CpsMin;
                    int hi = Math.Max(lo, CpsMax);
                    double cps = lo + rnd.NextDouble() * (hi - lo);
                    double interval = 1000.0 / Math.Max(1.0, cps);
                    double holdMs = Math.Min(80.0, Math.Max(5.0, interval * (0.30 + rnd.NextDouble() * 0.20)));
                    bool right = RightButton;

                    // Liczymy od zaplanowanego czasu, a nie od "teraz", zeby male opoznienia sie nie sumowaly.
                    double now = Now();
                    double start = (next > 0 && now - next < 50.0) ? next : now;

                    Send(right ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_LEFTDOWN);
                    RecordClick();
                    WaitUntil(start + holdMs, false);
                    Send(right ? Native.MOUSEEVENTF_RIGHTUP : Native.MOUSEEVENTF_LEFTUP); // puszczenie zawsze wysylamy
                    next = start + interval;
                    WaitUntil(next, true);
                }
            }
            finally
            {
                if (timer != IntPtr.Zero) Native.CloseHandle(timer);
                Native.timeEndPeriod(1);
            }
        }

        static void DisablePowerThrottling()
        {
            try
            {
                if (!SetThrottling(Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED |
                                   Native.PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION))
                    SetThrottling(Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED); // starszy Windows 10
            }
            catch (EntryPointNotFoundException) { } // Windows 7 - brak tej funkcji
        }

        static bool SetThrottling(uint controlMask)
        {
            Native.PROCESS_POWER_THROTTLING_STATE state = new Native.PROCESS_POWER_THROTTLING_STATE();
            state.Version = 1;
            state.ControlMask = controlMask;
            state.StateMask = 0; // 0 = wylacz dlawienie dla bitow z ControlMask
            return Native.SetProcessInformation(Native.GetCurrentProcess(), Native.ProcessPowerThrottling,
                ref state, Marshal.SizeOf(typeof(Native.PROCESS_POWER_THROTTLING_STATE)));
        }

        bool ShouldClick()
        {
            if (!enabled) return false;
            if (HoldMode && !held) return false;
            return TargetReady();
        }

        bool TargetReady()
        {
            if (!fgIsTarget) return false;
            if (SkipMenus && fgIsMinecraft && CursorInMenu()) return false;
            return true;
        }

        void WaitUntil(double targetMs, bool abortable)
        {
            while (running)
            {
                double left = targetMs - Now();
                if (left <= 0) return;
                if (abortable && !ShouldClick()) return;
                // Spimy krotkimi kawalkami, zeby szybko zareagowac na puszczenie przycisku.
                if (left > 1.0) SleepMs(Math.Min(left - 0.5, 4.0));
                else Thread.Sleep(0);
            }
        }

        void SleepMs(double ms)
        {
            if (timer != IntPtr.Zero)
            {
                long due = -(long)(ms * 10000.0); // jednostki 100 ns, wartosc ujemna = czas wzgledny
                if (Native.SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    Native.WaitForSingleObject(timer, 100);
                    return;
                }
            }
            Thread.Sleep(1);
        }

        double Now() { return clock.ElapsedTicks * 1000.0 / Stopwatch.Frequency; }

        void Send(uint flags)
        {
            Native.INPUT[] input = new Native.INPUT[1];
            input[0].type = Native.INPUT_MOUSE;
            input[0].mi.dwFlags = flags;
            input[0].mi.dwExtraInfo = Signature;
            Native.SendInput(1, input, InputSize);
        }

        void RecordClick()
        {
            long now = clock.ElapsedMilliseconds;
            lock (clickTimes)
            {
                clickTimes.Enqueue(now);
                while (clickTimes.Count > 0 && now - clickTimes.Peek() > 1000) clickTimes.Dequeue();
            }
        }

        // W grze Minecraft chowa kursor (LWJGL2 ustawia pusty kursor, LWJGL3/GLFW ustawia NULL).
        // Zwykla strzalka systemowa = otwarte menu / ekwipunek / czat.
        bool CursorInMenu()
        {
            Native.CURSORINFO ci = new Native.CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(Native.CURSORINFO));
            if (!Native.GetCursorInfo(ref ci)) return false;
            if ((ci.flags & Native.CURSOR_SHOWING) == 0 || ci.hCursor == IntPtr.Zero) return false;
            for (int i = 0; i < menuCursors.Length; i++)
                if (ci.hCursor == menuCursors[i]) return true;
            return false;
        }

        void UpdateForeground()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            StringBuilder sb = new StringBuilder(256);
            Native.GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (hwnd != fgHwnd || title != fgTitle)
            {
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                string exe = GetProcessName(pid);
                fgHwnd = hwnd;
                fgPid = pid;
                fgExe = exe;
                fgTitle = title;
                fgIsMinecraft = LooksLikeMinecraft(exe, title);
            }
            fgIsTarget = hwnd != IntPtr.Zero && fgPid != ownPid && (!OnlyMinecraft || fgIsMinecraft);
        }

        static bool LooksLikeMinecraft(string exe, string title)
        {
            // Lunar, BlazingPack i wiekszosc klientow uruchamia gre przez java/javaw.
            if (exe.Contains("java") || exe.Contains("minecraft") || exe.Contains("blazing")) return true;
            string[] browsers = { "chrome", "msedge", "firefox", "opera", "brave", "explorer", "discord" };
            foreach (string b in browsers)
                if (exe.Contains(b)) return false;
            string t = title.ToLowerInvariant();
            return t.StartsWith("minecraft") || t.StartsWith("lunar client 1") || t.StartsWith("blazingpack");
        }

        static string GetProcessName(uint pid)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (!Native.QueryFullProcessImageName(h, 0, sb, ref size)) return "";
                return Path.GetFileNameWithoutExtension(sb.ToString()).ToLowerInvariant();
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        public void Dispose()
        {
            enabled = false;
            running = false;
            wake.Set();
            if (hookThreadId != 0) Native.PostThreadMessage(hookThreadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            if (clickThread != null) clickThread.Join(500);
            if (hookThread != null) hookThread.Join(500);
        }
    }
}
