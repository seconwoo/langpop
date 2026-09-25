using System;
using System.Drawing;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LanguageIndicator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\LanguageIndicator_5b1e0c7a", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var app = new TrayApp())
                    Application.Run();
                GC.KeepAlive(mutex);
            }
        }
    }

    internal sealed class TrayApp : IDisposable
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "LanguageIndicator";

        private readonly InputStateMonitor _monitor = new InputStateMonitor();
        private readonly OsdWindow _osd = new OsdWindow();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private readonly ToolStripMenuItem _autostart;
        private Icon _icon;

        public TrayApp()
        {
            var menu = new ContextMenuStrip();
            _autostart = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleAutostart(); });
            _autostart.Checked = IsAutostart();
            menu.Items.Add(new ToolStripMenuItem("Show current", null, delegate { ShowCurrent(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(BuildThemeMenu());
            menu.Items.Add(BuildOpacityMenu());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autostart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { Application.ExitThread(); }));

            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowCurrent(); };

            _monitor.StateChanged += delegate(InputState state, IntPtr fg)
            {
                bool isCaret;
                Rectangle anchor = CaretLocator.Locate(fg, out isCaret);
                _osd.ShowState(state, anchor, isCaret);
                UpdateTray(state);
            };
            _monitor.Start();
            UpdateTray(_monitor.Current);
            _tray.Visible = true;
        }

        private ToolStripMenuItem BuildThemeMenu()
        {
            var root = new ToolStripMenuItem("Theme");
            string current = Settings.ThemeName;
            foreach (string name in Theme.Names)
            {
                string n = name;
                var item = new ToolStripMenuItem(n == "Auto" ? "Auto (follow Windows)" : n);
                item.Checked = n == current;
                item.Click += delegate
                {
                    Settings.ThemeName = n;
                    foreach (ToolStripMenuItem i in root.DropDownItems) i.Checked = i == item;
                    ShowCurrent();
                };
                root.DropDownItems.Add(item);
            }
            return root;
        }

        private ToolStripMenuItem BuildOpacityMenu()
        {
            var root = new ToolStripMenuItem("Background opacity");
            int current = Settings.Opacity;
            foreach (int level in Theme.OpacityLevels)
            {
                int l = level;
                string label = l + "%" + (l == Theme.DefaultOpacity ? "  (default)" : "");
                var item = new ToolStripMenuItem(label);
                item.Checked = l == current;
                item.Click += delegate
                {
                    Settings.Opacity = l;
                    foreach (ToolStripMenuItem i in root.DropDownItems) i.Checked = i == item;
                    ShowCurrent();
                };
                root.DropDownItems.Add(item);
            }
            return root;
        }

        private void ShowCurrent()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            _osd.ShowState(_monitor.Current, new Rectangle(p.X, p.Y, 0, 0), false);
        }

        private void UpdateTray(InputState state)
        {
            string glyph = state.Glyph;
            _tray.Text = "Language Indicator — " + state.Caption;

            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font(glyph.Length > 0 && glyph[0] > 0x2E80 ? "Microsoft YaHei UI" : "Segoe UI Semibold",
                                       glyph.Length > 1 ? 15f : 20f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var bg = new SolidBrush(state.IsAccent ? Color.FromArgb(255, 0, 103, 192) : Color.FromArgb(255, 60, 60, 60)))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                g.FillEllipse(bg, 1, 1, 30, 30);
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, font, Brushes.White, new RectangleF(0, 1, 32, 32), fmt);

                Icon old = _icon;
                IntPtr h = bmp.GetHicon();
                _icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                _tray.Icon = _icon;
                if (old != null) old.Dispose();
            }
        }

        private static bool IsAutostart()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunValue) != null;
        }

        private void ToggleAutostart()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (IsAutostart()) k.DeleteValue(RunValue, false);
                else k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
            }
            _autostart.Checked = IsAutostart();
        }

        public void Dispose()
        {
            _tray.Visible = false;
            _tray.Dispose();
            _monitor.Dispose();
            _osd.Dispose();
            if (_icon != null) _icon.Dispose();
        }
    }
}
