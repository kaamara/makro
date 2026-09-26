using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace McClicker
{
    internal sealed class MainForm : Form
    {
        const int W = 400;
        const int H = 612;

        readonly ClickEngine engine;
        readonly Settings cfg;
        readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        bool dwmRounded;

        Button btnToggle, btnModeAlways, btnModeHold, btnLeft, btnRight, btnKey, btnMin, btnClose;
        FlatSlider sliderMin, sliderMax;
        Label lblCps, lblWindow, lblLive;
        CheckBox chkOnlyMc, chkSkipMenus, chkSound, chkTop;

        public MainForm(ClickEngine engine, Settings cfg)
        {
            this.engine = engine;
            this.cfg = cfg;

            Text = "AutoClicker MC";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, H);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Normal;
            DoubleBuffered = true;
            ShowInTaskbar = true;

            BuildUi();
            LoadFromSettings();

            engine.EnabledChanged += OnEngineEnabled;
            engine.HotkeyBound += OnHotkeyBound;

            uiTimer.Interval = 200;
            uiTimer.Tick += delegate { RefreshStatus(); };
            uiTimer.Start();
        }

        // Pozwala minimalizowac okno bez ramki klikajac ikone na pasku zadan.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                return cp;
            }
        }

        void BuildUi()
        {
            // Pasek tytulu
            Label title = Ui.MakeLabel(this, "AutoClicker MC", 16, 12, Theme.Title, Theme.Text);
            Label sub = Ui.MakeLabel(this, "Lunar / BlazingPack", 16 + title.PreferredWidth + 6, 15, Theme.Small, Theme.Muted);
            title.MouseDown += DragWindow;
            sub.MouseDown += DragWindow;
            MouseDown += DragWindow;

            btnMin = Ui.MakeButton(this, "–", W - 84, 8, 34, 28);
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.BackColor = Theme.Bg;
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            btnClose = Ui.MakeButton(this, "×", W - 46, 8, 34, 28);
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = Theme.Bg;
            btnClose.FlatAppearance.MouseOverBackColor = Theme.Off;
            btnClose.FlatAppearance.MouseDownBackColor = Theme.Off;
            btnClose.Click += delegate { Close(); };

            // Duzy przycisk wl/wyl
            btnToggle = Ui.MakeButton(this, "", 16, 58, W - 32, 56);
            btnToggle.Font = Theme.Big;
            btnToggle.Click += delegate { engine.Toggle(); };

            // Tryb
            Ui.MakeLabel(this, "TRYB", 16, 128, Theme.Section, Theme.Muted);
            btnModeAlways = Ui.MakeButton(this, "Klikaj cały czas", 16, 148, 180, 36);
            btnModeHold = Ui.MakeButton(this, "Tylko gdy trzymam", 204, 148, 180, 36);
            btnModeAlways.Click += delegate { engine.HoldMode = false; UpdateSelections(); };
            btnModeHold.Click += delegate { engine.HoldMode = true; UpdateSelections(); };

            // Przycisk myszy
            Ui.MakeLabel(this, "PRZYCISK MYSZY", 16, 196, Theme.Section, Theme.Muted);
            btnLeft = Ui.MakeButton(this, "Lewy (LPM)", 16, 216, 180, 36);
            btnRight = Ui.MakeButton(this, "Prawy (PPM)", 204, 216, 180, 36);
            btnLeft.Click += delegate { engine.RightButton = false; UpdateSelections(); };
            btnRight.Click += delegate { engine.RightButton = true; UpdateSelections(); };

            // CPS
            Ui.MakeLabel(this, "CPS", 16, 266, Theme.Section, Theme.Muted);
            lblCps = new Label();
            lblCps.SetBounds(204, 262, 180, 22);
            lblCps.TextAlign = ContentAlignment.MiddleRight;
            lblCps.Font = Theme.Title;
            lblCps.ForeColor = Theme.Text;
            lblCps.BackColor = Color.Transparent;
            Controls.Add(lblCps);

            Ui.MakeLabel(this, "min", 16, 292, Theme.Normal, Theme.Muted);
            sliderMin = new FlatSlider();
            sliderMin.SetBounds(52, 290, W - 68, 24);
            Controls.Add(sliderMin);

            Ui.MakeLabel(this, "max", 16, 322, Theme.Normal, Theme.Muted);
            sliderMax = new FlatSlider();
            sliderMax.SetBounds(52, 320, W - 68, 24);
            Controls.Add(sliderMax);

            Ui.MakeLabel(this, "CPS losowane z zakresu min–max. Ustaw równe = stałe CPS.", 16, 350, Theme.Small, Theme.Muted);

            sliderMin.ValueChanged += delegate
            {
                if (sliderMax.Value < sliderMin.Value) sliderMax.Value = sliderMin.Value;
                engine.CpsMin = sliderMin.Value;
                UpdateCpsLabel();
            };
            sliderMax.ValueChanged += delegate
            {
                if (sliderMin.Value > sliderMax.Value) sliderMin.Value = sliderMax.Value;
                engine.CpsMax = sliderMax.Value;
                UpdateCpsLabel();
            };

            // Klawisz
            Ui.MakeLabel(this, "KLAWISZ WŁĄCZ / WYŁĄCZ", 16, 378, Theme.Section, Theme.Muted);
            btnKey = Ui.MakeButton(this, "", 16, 398, W - 32, 34);
            btnKey.Click += delegate
            {
                engine.BeginBinding();
                btnKey.Text = "Naciśnij klawisz lub boczny przycisk myszy...  (Esc = anuluj)";
                Ui.SetSelected(btnKey, true);
                ActiveControl = null;
            };

            // Opcje
            chkOnlyMc = Ui.MakeCheck(this, "Klikaj tylko w oknie Minecrafta", 16, 446, true);
            chkSkipMenus = Ui.MakeCheck(this, "Nie klikaj w ekwipunku / menu / czacie", 16, 472, true);
            chkSound = Ui.MakeCheck(this, "Dźwięk przy włączaniu / wyłączaniu", 16, 498, true);
            chkTop = Ui.MakeCheck(this, "Zawsze na wierzchu", 16, 524, false);
            chkOnlyMc.CheckedChanged += delegate { engine.OnlyMinecraft = chkOnlyMc.Checked; };
            chkSkipMenus.CheckedChanged += delegate { engine.SkipMenus = chkSkipMenus.Checked; };
            chkTop.CheckedChanged += delegate { TopMost = chkTop.Checked; };

            // Status
            lblWindow = new Label();
            lblWindow.SetBounds(16, 566, W - 32, 18);
            lblWindow.AutoEllipsis = true;
            lblWindow.Font = Theme.Small;
            lblWindow.ForeColor = Theme.Muted;
            lblWindow.BackColor = Color.Transparent;
            Controls.Add(lblWindow);

            lblLive = Ui.MakeLabel(this, "", 16, 586, Theme.Small, Theme.Muted);
        }

        void LoadFromSettings()
        {
            sliderMin.Value = cfg.CpsMin;
            sliderMax.Value = cfg.CpsMax;
            engine.CpsMin = sliderMin.Value;
            engine.CpsMax = sliderMax.Value;
            engine.HoldMode = cfg.HoldMode;
            engine.RightButton = cfg.RightButton;
            engine.HotkeyVk = cfg.HotkeyVk;
            engine.OnlyMinecraft = cfg.OnlyMinecraft;
            engine.SkipMenus = cfg.SkipMenus;
            chkOnlyMc.Checked = cfg.OnlyMinecraft;
            chkSkipMenus.Checked = cfg.SkipMenus;
            chkSound.Checked = cfg.Sound;
            chkTop.Checked = cfg.TopMost;
            TopMost = cfg.TopMost;

            UpdateSelections();
            UpdateCpsLabel();
            UpdateKeyTexts();
            RefreshStatus();
        }

        void SaveToSettings()
        {
            cfg.CpsMin = engine.CpsMin;
            cfg.CpsMax = engine.CpsMax;
            cfg.HoldMode = engine.HoldMode;
            cfg.RightButton = engine.RightButton;
            cfg.HotkeyVk = engine.HotkeyVk;
            cfg.OnlyMinecraft = chkOnlyMc.Checked;
            cfg.SkipMenus = chkSkipMenus.Checked;
            cfg.Sound = chkSound.Checked;
            cfg.TopMost = chkTop.Checked;
            cfg.Save();
        }

        void UpdateSelections()
        {
            Ui.SetSelected(btnModeAlways, !engine.HoldMode);
            Ui.SetSelected(btnModeHold, engine.HoldMode);
            Ui.SetSelected(btnLeft, !engine.RightButton);
            Ui.SetSelected(btnRight, engine.RightButton);
        }

        void UpdateCpsLabel()
        {
            lblCps.Text = sliderMin.Value == sliderMax.Value
                ? sliderMin.Value + " CPS"
                : sliderMin.Value + " – " + sliderMax.Value + " CPS";
        }

        void UpdateKeyTexts()
        {
            btnKey.Text = KeyName(engine.HotkeyVk) + "      (kliknij, aby zmienić)";
            Ui.SetSelected(btnKey, false);
            UpdateToggle();
        }

        void UpdateToggle()
        {
            bool on = engine.Enabled;
            btnToggle.Text = (on ? "WŁĄCZONY" : "WYŁĄCZONY") + "   ·   " + KeyName(engine.HotkeyVk);
            if (on) Ui.Paint(btnToggle, Theme.On, Theme.OnHover, Theme.On, Color.White);
            else Ui.Paint(btnToggle, Theme.Off, Theme.OffHover, Theme.Off, Color.White);
        }

        void RefreshStatus()
        {
            string title = engine.ForegroundTitle;
            string exe = engine.ForegroundExe;
            bool mc = engine.ForegroundIsMinecraft;
            lblWindow.Text = "Aktywne okno: " + (title.Length > 0 ? title : "(brak)") +
                             (exe.Length > 0 ? "  [" + exe + "]" : "") +
                             (mc ? "  – wykryto Minecrafta" : "");
            lblWindow.ForeColor = mc ? Theme.On : Theme.Muted;

            string err = engine.HookError;
            if (err != null)
            {
                lblLive.Text = err;
                lblLive.ForeColor = Theme.Off;
                return;
            }
            int cps = engine.MeasuredCps();
            lblLive.Text = "Kliknięcia na sekundę: " + cps;
            lblLive.ForeColor = cps > 0 ? Theme.Text : Theme.Muted;
        }

        // Zdarzenia z watku hooka - przerzucamy na watek GUI.
        void OnEngineEnabled(bool on)
        {
            RunOnUi(delegate
            {
                UpdateToggle();
                if (chkSound.Checked) Beep(on);
            });
        }

        void OnHotkeyBound(int vk)
        {
            RunOnUi(UpdateKeyTexts);
        }

        void RunOnUi(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }

        static void Beep(bool on)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Console.Beep(on ? 1320 : 660, 70); }
                catch { }
            });
        }

        static string KeyName(int vk)
        {
            if (vk == Native.VK_MBUTTON) return "Środkowy przycisk myszy";
            if (vk == Native.VK_XBUTTON1) return "Mysz 4 (boczny)";
            if (vk == Native.VK_XBUTTON2) return "Mysz 5 (boczny)";
            Keys key = (Keys)vk;
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (vk - (int)Keys.D0))).ToString();
            return key.ToString();
        }

        void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sender == this && e.Y > 44) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, new IntPtr(Native.HTCAPTION), IntPtr.Zero);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // DWMWCP_ROUND - zaokraglone rogi na Windows 11
                dwmRounded = Native.DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)) == 0;
            }
            catch { dwmRounded = false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Border))
            {
                if (!dwmRounded) e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                e.Graphics.DrawLine(pen, 0, 44, ClientSize.Width, 44);
                e.Graphics.DrawLine(pen, 16, 556, ClientSize.Width - 16, 556);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            uiTimer.Stop();
            engine.EnabledChanged -= OnEngineEnabled;
            engine.HotkeyBound -= OnHotkeyBound;
            SaveToSettings();
            base.OnFormClosing(e);
        }
    }
}
