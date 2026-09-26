using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace McClicker
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(17, 17, 23);
        public static readonly Color Panel = Color.FromArgb(28, 28, 38);
        public static readonly Color PanelHover = Color.FromArgb(40, 40, 54);
        public static readonly Color Border = Color.FromArgb(52, 52, 70);
        public static readonly Color Text = Color.FromArgb(236, 236, 245);
        public static readonly Color Muted = Color.FromArgb(135, 135, 158);
        public static readonly Color Accent = Color.FromArgb(124, 92, 255);
        public static readonly Color AccentHover = Color.FromArgb(142, 114, 255);
        public static readonly Color On = Color.FromArgb(38, 186, 104);
        public static readonly Color OnHover = Color.FromArgb(48, 204, 118);
        public static readonly Color Off = Color.FromArgb(214, 64, 69);
        public static readonly Color OffHover = Color.FromArgb(230, 80, 84);

        public static readonly Font Normal = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Section = new Font("Segoe UI Semibold", 8.5f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 11f);
        public static readonly Font Big = new Font("Segoe UI Semibold", 14f);
    }

    // Plaski suwak rysowany recznie (standardowy TrackBar nie da sie ladnie ostylowac).
    internal sealed class FlatSlider : Control
    {
        const int Pad = 9;
        int minimum = 1, maximum = 50, current = 10;
        bool dragging;

        public event EventHandler ValueChanged;

        public FlatSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Height = 24;
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
        }

        public int Minimum { get { return minimum; } set { minimum = value; Invalidate(); } }
        public int Maximum { get { return maximum; } set { maximum = value; Invalidate(); } }

        public int Value
        {
            get { return current; }
            set
            {
                int v = Math.Max(minimum, Math.Min(maximum, value));
                if (v == current) return;
                current = v;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            float frac = maximum > minimum ? (float)(current - minimum) / (maximum - minimum) : 0f;
            float x = Pad + frac * (Width - 2 * Pad);

            using (Pen track = new Pen(Theme.Border, 4f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                g.DrawLine(track, Pad, cy, Width - Pad, cy);
            }
            using (Pen fill = new Pen(Theme.Accent, 4f))
            {
                fill.StartCap = LineCap.Round;
                fill.EndCap = LineCap.Round;
                g.DrawLine(fill, Pad, cy, x, cy);
            }
            using (Brush thumb = new SolidBrush(Theme.Text))
                g.FillEllipse(thumb, x - 7, cy - 7, 14, 14);
            using (Pen ring = new Pen(Focused ? Theme.AccentHover : Theme.Accent, 2.5f))
                g.DrawEllipse(ring, x - 7, cy - 7, 14, 14);
        }

        void SetFromX(int x)
        {
            float frac = (float)(x - Pad) / Math.Max(1, Width - 2 * Pad);
            Value = minimum + (int)Math.Round(frac * (maximum - minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                Focus();
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Value += e.Delta > 0 ? 1 : -1;
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value--;
            else if (e.KeyCode == Keys.Right) Value++;
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    internal static class Ui
    {
        public static Button MakeButton(Control parent, string text, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.Font = Theme.Normal;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.UseVisualStyleBackColor = false;
            SetSelected(b, false);
            parent.Controls.Add(b);
            return b;
        }

        public static void SetSelected(Button b, bool selected)
        {
            Paint(b, selected ? Theme.Accent : Theme.Panel, selected ? Theme.AccentHover : Theme.PanelHover,
                  selected ? Theme.Accent : Theme.Border, selected ? Color.White : Theme.Text);
        }

        public static void Paint(Button b, Color back, Color hover, Color border, Color fore)
        {
            b.BackColor = back;
            b.ForeColor = fore;
            b.FlatAppearance.BorderColor = border;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.FlatAppearance.MouseDownBackColor = hover;
        }

        public static Label MakeLabel(Control parent, string text, int x, int y, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            parent.Controls.Add(l);
            return l;
        }

        public static CheckBox MakeCheck(Control parent, string text, int x, int y, bool isChecked)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Location = new Point(x, y);
            c.Font = Theme.Normal;
            c.ForeColor = Theme.Text;
            c.BackColor = Theme.Bg;
            c.FlatStyle = FlatStyle.Flat;
            c.FlatAppearance.BorderColor = Theme.Border;
            c.FlatAppearance.CheckedBackColor = Theme.Accent;
            c.FlatAppearance.MouseOverBackColor = Theme.PanelHover;
            c.Cursor = Cursors.Hand;
            c.TabStop = false;
            c.Checked = isChecked;
            parent.Controls.Add(c);
            return c;
        }
    }
}
