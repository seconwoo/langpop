using System;
using System.Drawing;
using Microsoft.Win32;

namespace LanguageIndicator
{
    /// <summary>Visual style of the popup. Fill alpha is controlled separately by the opacity setting.</summary>
    internal sealed class Theme
    {
        public string Name;
        public Color Fill;          // background tint (alpha ignored; opacity setting applies)
        public Color Text;
        public Color Sub;           // caption
        public Color Border;
        public Color Bar;           // accent bar for native/non-Latin state; Empty = Windows accent
        public Color Halo;          // glow behind text, keeps it legible when the fill is very transparent
        public int MinHalo;         // halo floor for themes whose text needs contrast even when fairly opaque
        public int Highlight;       // alpha of the glassy top highlight
        public int Shadow;          // shadow strength (0..20)
        public string LatinFont;    // null = Segoe UI Semibold

        private static Color A(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }

        public static readonly string[] Names =
            { "Auto", "Dark", "Light", "Glass", "Frost", "Midnight", "Accent", "Sakura", "Terminal" };

        public const int DefaultOpacity = 45;
        public static readonly int[] OpacityLevels = { 15, 30, 45, 60, 80 };

        /// <summary>Resolves a theme by name; "Auto" follows the Windows light/dark app setting.</summary>
        public static Theme Get(string name)
        {
            switch (name)
            {
                case "Light":
                    return new Theme { Name = name, Fill = A(255, 249, 249, 249), Text = A(255, 20, 20, 20),
                        Sub = A(170, 20, 20, 20), Border = A(40, 0, 0, 0), Halo = A(255, 255, 255, 255),
                        Highlight = 50, Shadow = 6 };
                case "Glass":
                    return new Theme { Name = name, Fill = A(255, 255, 255, 255), Text = A(255, 255, 255, 255),
                        Sub = A(235, 255, 255, 255), Border = A(90, 255, 255, 255), Halo = A(255, 0, 0, 0), MinHalo = 110,
                        Highlight = 70, Shadow = 5 };
                case "Frost":
                    return new Theme { Name = name, Fill = A(255, 200, 225, 255), Text = A(255, 10, 30, 60),
                        Sub = A(190, 10, 30, 60), Border = A(110, 255, 255, 255), Halo = A(255, 235, 245, 255),
                        Bar = A(255, 40, 120, 230), Highlight = 90, Shadow = 5 };
                case "Midnight":
                    return new Theme { Name = name, Fill = A(255, 14, 20, 44), Text = A(255, 235, 242, 255),
                        Sub = A(180, 160, 200, 255), Border = A(70, 90, 170, 255), Halo = A(255, 0, 0, 20),
                        Bar = A(255, 0, 210, 255), Highlight = 20, Shadow = 10 };
                case "Accent":
                    return new Theme { Name = name, Fill = SystemAccent(), Text = A(255, 255, 255, 255),
                        Sub = A(215, 255, 255, 255), Border = A(60, 255, 255, 255), Halo = A(255, 0, 0, 0),
                        Bar = A(255, 255, 255, 255), Highlight = 35, Shadow = 9 };
                case "Sakura":
                    return new Theme { Name = name, Fill = A(255, 255, 222, 232), Text = A(255, 120, 30, 70),
                        Sub = A(190, 150, 50, 90), Border = A(90, 255, 160, 190), Halo = A(255, 255, 240, 245),
                        Bar = A(255, 240, 90, 140), Highlight = 60, Shadow = 5 };
                case "Terminal":
                    return new Theme { Name = name, Fill = A(255, 6, 12, 8), Text = A(255, 90, 255, 140),
                        Sub = A(170, 90, 255, 140), Border = A(80, 90, 255, 140), Halo = A(255, 0, 0, 0),
                        Bar = A(255, 90, 255, 140), Highlight = 0, Shadow = 10, LatinFont = "Cascadia Mono" };
                case "Dark":
                    return Dark(name);
                default:
                    Theme t = IsLightMode() ? Get("Light") : Dark("Dark");
                    t.Name = "Auto";
                    return t;
            }
        }

        private static Theme Dark(string name)
        {
            return new Theme { Name = name, Fill = A(255, 28, 28, 30), Text = A(255, 245, 245, 245),
                Sub = A(180, 245, 245, 245), Border = A(45, 255, 255, 255), Halo = A(255, 0, 0, 0),
                Highlight = 20, Shadow = 10 };
        }

        public static bool IsLightMode()
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 0);
                return v is int && (int)v != 0;
            }
            catch { return false; }
        }

        public static Color SystemAccent()
        {
            uint c; bool opaque;
            if (Native.DwmGetColorizationColor(out c, out opaque) == 0)
                return Color.FromArgb(255, (int)((c >> 16) & 0xFF), (int)((c >> 8) & 0xFF), (int)(c & 0xFF));
            return Color.FromArgb(255, 0, 120, 212);
        }
    }

    /// <summary>User preferences persisted under HKCU\Software\LanguageIndicator.</summary>
    internal static class Settings
    {
        private const string Key = @"Software\LanguageIndicator";

        public static string ThemeName
        {
            get { return Read("Theme", "Auto"); }
            set { Write("Theme", value); }
        }

        public static int Opacity
        {
            get
            {
                int v;
                return int.TryParse(Read("Opacity", Theme.DefaultOpacity.ToString()), out v)
                    ? Math.Max(5, Math.Min(100, v)) : Theme.DefaultOpacity;
            }
            set { Write("Opacity", value.ToString()); }
        }

        /// <summary>Glyph-only badge instead of the full popup with caption.</summary>
        public static bool Compact
        {
            get { return Read("Compact", "0") == "1"; }
            set { Write("Compact", value ? "1" : "0"); }
        }

        private static string Read(string name, string fallback)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    object v = k != null ? k.GetValue(name) : null;
                    return v != null ? v.ToString() : fallback;
                }
            }
            catch { return fallback; }
        }

        private static void Write(string name, string value)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue(name, value);
        }
    }
}
