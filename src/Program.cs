using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace McClicker
{
    // Ustawienia zapisywane w AutoClickerMC.ini obok pliku .exe.
    internal sealed class Settings
    {
        public int CpsMin = 10;
        public int CpsMax = 14;
        public int HotkeyVk = 0x75; // F6
        public bool HoldMode;
        public bool RightButton;
        public bool OnlyMinecraft = true;
        public bool SkipMenus = true;
        public bool Sound = true;
        public bool TopMost;

        static string FilePath
        {
            get { return Path.Combine(Application.StartupPath, "AutoClickerMC.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                Dictionary<string, string> v = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) v[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                s.CpsMin = GetInt(v, "CpsMin", s.CpsMin);
                s.CpsMax = GetInt(v, "CpsMax", s.CpsMax);
                s.HotkeyVk = GetInt(v, "HotkeyVk", s.HotkeyVk);
                s.HoldMode = GetBool(v, "HoldMode", s.HoldMode);
                s.RightButton = GetBool(v, "RightButton", s.RightButton);
                s.OnlyMinecraft = GetBool(v, "OnlyMinecraft", s.OnlyMinecraft);
                s.SkipMenus = GetBool(v, "SkipMenus", s.SkipMenus);
                s.Sound = GetBool(v, "Sound", s.Sound);
                s.TopMost = GetBool(v, "TopMost", s.TopMost);
            }
            catch { }

            s.CpsMin = Math.Max(1, Math.Min(50, s.CpsMin));
            s.CpsMax = Math.Max(s.CpsMin, Math.Min(50, s.CpsMax));
            if (s.HotkeyVk <= 0 || s.HotkeyVk > 255) s.HotkeyVk = 0x75;
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, new string[]
                {
                    "CpsMin=" + CpsMin,
                    "CpsMax=" + CpsMax,
                    "HotkeyVk=" + HotkeyVk,
                    "HoldMode=" + (HoldMode ? 1 : 0),
                    "RightButton=" + (RightButton ? 1 : 0),
                    "OnlyMinecraft=" + (OnlyMinecraft ? 1 : 0),
                    "SkipMenus=" + (SkipMenus ? 1 : 0),
                    "Sound=" + (Sound ? 1 : 0),
                    "TopMost=" + (TopMost ? 1 : 0),
                });
            }
            catch { }
        }

        static int GetInt(Dictionary<string, string> v, string key, int fallback)
        {
            string s;
            int result;
            return v.TryGetValue(key, out s) && int.TryParse(s, out result) ? result : fallback;
        }

        static bool GetBool(Dictionary<string, string> v, string key, bool fallback)
        {
            string s;
            return v.TryGetValue(key, out s) ? s == "1" : fallback;
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex single = new Mutex(true, "AutoClickerMC_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("AutoClicker MC jest już uruchomiony.", "AutoClicker MC",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Settings cfg = Settings.Load();
                using (ClickEngine engine = new ClickEngine())
                {
                    engine.Start();
                    Application.Run(new MainForm(engine, cfg));
                }
            }
        }
    }
}
