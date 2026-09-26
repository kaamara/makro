using System;
using System.Runtime.InteropServices;
using System.Text;

namespace McClicker
{
    // Deklaracje WinAPI uzywane przez clicker i GUI.
    internal static class Native
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const uint WM_QUIT = 0x0012;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 2;

        public const uint INPUT_MOUSE = 0;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        public const int VK_MBUTTON = 0x04;
        public const int VK_XBUTTON1 = 0x05;
        public const int VK_XBUTTON2 = 0x06;
        public const int VK_ESCAPE = 0x1B;

        public const int CURSOR_SHOWING = 0x0001;
        public const int IDC_ARROW = 32512;
        public const int IDC_IBEAM = 32513;
        public const int IDC_HAND = 32649;

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // MOUSEINPUT jest najwiekszym czlonem unii, wiec rozmiar INPUT sie zgadza (x86 i x64).
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(ref CURSORINFO info);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr cursorName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint ms);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint ms);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // Windows 11 obniza dokladnosc timerow i zwalnia procesy, ktorych okno jest zasloniete
        // (a w grze okno makra zawsze jest zasloniete). Ponizsze pozwala z tego zrezygnowac.
        public const int ProcessPowerThrottling = 4;
        public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
        public const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;
        public const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x2;
        public const uint TIMER_ALL_ACCESS = 0x1F0003;

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        public static extern bool SetProcessInformation(IntPtr hProcess, int infoClass,
            ref PROCESS_POWER_THROTTLING_STATE info, int size);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint access);

        [DllImport("kernel32.dll")]
        public static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period,
            IntPtr completionRoutine, IntPtr arg, bool resume);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    }
}
