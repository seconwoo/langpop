using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LangPop
{
    /// <summary>
    /// Click-through, non-activating, per-pixel-alpha popup that fades/slides in near the caret.
    /// </summary>
    internal sealed class OsdWindow : NativeWindow, IDisposable
    {
        private const double FadeInMs = 160, HoldMs = 900, FadeOutMs = 280;
        private enum Phase { Hidden, In, Hold, Out }

        private readonly Timer _timer = new Timer { Interval = 15 };
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private Phase _phase = Phase.Hidden;
        private double _v;              // 0..1 visibility progress (linear)
        private double _lastTick;
        private double _holdUntil;

        private IntPtr _memDc, _hBitmap, _oldBitmap;
        private Size _size;
        private Point _base;            // top-left at full visibility
        private float _scale = 1f;

        public OsdWindow()
        {
            var cp = new CreateParams
            {
                Caption = "LangPopOSD",
                Style = Native.WS_POPUP,
                ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                          | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST,
            };
            CreateHandle(cp);
            _timer.Tick += delegate { Animate(); };
        }

        public void ShowState(InputState state, Rectangle anchor, bool isCaret)
        {
            var anchorPt = new Native.POINT(anchor.X, anchor.Y);
            _scale = DpiScaleAt(anchorPt);

            using (Bitmap bmp = Render(state, _scale, Theme.Get(Settings.ThemeName), Settings.Opacity, Settings.Compact))
            {
                SetBitmap(bmp);
            }

            int margin = ShadowMargin(_scale);
            int x, y;
            if (isCaret)
            {
                x = anchor.Left - margin;
                y = anchor.Bottom + (int)(8 * _scale) - margin;
            }
            else
            {
                x = anchor.X + (int)(16 * _scale) - margin;
                y = anchor.Y + (int)(22 * _scale) - margin;
            }

            Rectangle bounds = Screen.FromPoint(new Point(anchor.X, anchor.Y)).Bounds;
            if (y + _size.Height > bounds.Bottom)
                y = anchor.Top - _size.Height - (int)(6 * _scale) + margin;
            x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - _size.Width));
            y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - _size.Height));
            _base = new Point(x, y);

            double now = _clock.Elapsed.TotalMilliseconds;
            if (_phase == Phase.Hidden)
            {
                _v = 0;
                Apply();
                Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            }
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

            _phase = Phase.In;
            _holdUntil = double.MaxValue;
            _lastTick = now;
            Apply();
            _timer.Start();
        }

        private void Animate()
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            double dt = now - _lastTick;
            _lastTick = now;

            switch (_phase)
            {
                case Phase.In:
                    _v = Math.Min(1, _v + dt / FadeInMs);
                    if (_v >= 1) { _phase = Phase.Hold; _holdUntil = now + HoldMs; }
                    break;
                case Phase.Hold:
                    if (now >= _holdUntil) _phase = Phase.Out;
                    return;
                case Phase.Out:
                    _v = Math.Max(0, _v - dt / FadeOutMs);
                    if (_v <= 0)
                    {
                        _phase = Phase.Hidden;
                        _timer.Stop();
                        Native.ShowWindow(Handle, Native.SW_HIDE);
                        return;
                    }
                    break;
                default:
                    _timer.Stop();
                    return;
            }
            Apply();
        }

        /// <summary>Pushes the cached bitmap with the current alpha/offset.</summary>
        private void Apply()
        {
            if (_memDc == IntPtr.Zero) return;
            double e = _phase == Phase.Out ? _v * _v * (3 - 2 * _v)        // smoothstep out
                                           : 1 - Math.Pow(1 - _v, 3);       // cubic ease-out in
            var dst = new Native.POINT(_base.X, _base.Y + (int)Math.Round((1 - e) * 8 * _scale));
            var size = new Native.SIZE(_size.Width, _size.Height);
            var src = new Native.POINT(0, 0);
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                SourceConstantAlpha = (byte)Math.Round(255 * e),
                AlphaFormat = Native.AC_SRC_ALPHA,
            };
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, _memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }

        private void SetBitmap(Bitmap bmp)
        {
            ReleaseBitmap();
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            _memDc = Native.CreateCompatibleDC(screenDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
            _hBitmap = bmp.GetHbitmap(Color.FromArgb(0));   // premultiplied 32bpp DIB
            _oldBitmap = Native.SelectObject(_memDc, _hBitmap);
            _size = bmp.Size;
        }

        private void ReleaseBitmap()
        {
            if (_memDc == IntPtr.Zero) return;
            Native.SelectObject(_memDc, _oldBitmap);
            Native.DeleteObject(_hBitmap);
            Native.DeleteDC(_memDc);
            _memDc = _hBitmap = _oldBitmap = IntPtr.Zero;
        }

        // ------------------------------------------------------------------ rendering

        private static int ShadowMargin(float scale) { return (int)Math.Ceiling(14 * scale); }

        private static Bitmap Render(InputState state, float s, Theme th, int opacity, bool compact)
        {
            float op = opacity / 100f;
            Color fill = Color.FromArgb((int)Math.Round(255 * op), th.Fill);
            Color accent = th.Bar.IsEmpty ? Theme.SystemAccent() : th.Bar;
            // The more see-through the background, the stronger the halo that keeps text readable.
            int halo = Math.Max(th.MinHalo, (int)Math.Round(90 * (1 - op)));

            string glyph = state.Glyph;
            bool cjkGlyph = glyph.Length > 0 && glyph[0] > 0x2E80;
            float glyphPx = compact ? (cjkGlyph ? 16f : 15f) : (cjkGlyph ? 22f : 21f);

            using (var glyphFont = MakeFont(cjkGlyph ? "Microsoft YaHei UI" : (th.LatinFont ?? "Segoe UI Semibold"),
                                            glyphPx * s, cjkGlyph ? FontStyle.Bold : FontStyle.Regular))
            using (var measureBmp = new Bitmap(1, 1))
            using (var mg = Graphics.FromImage(measureBmp))
            {
                var fmt = StringFormat.GenericTypographic;
                float glyphW = mg.MeasureString(glyph, glyphFont, 1000, fmt).Width;
                int m = ShadowMargin(s);
                var layout = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };

                if (compact)
                {
                    // Glyph-only badge; a circle for single CJK characters, a short pill otherwise.
                    const string capsMark = "⇪";
                    using (var capsFont = MakeFont("Segoe UI Symbol", 15f * s, FontStyle.Regular))
                    {
                        float capsW = state.Caps ? mg.MeasureString(capsMark, capsFont, 1000, fmt).Width + 3 * s : 0;
                        float h = 30 * s;
                        float w = Math.Max(h, glyphW + capsW + 18 * s);
                        Bitmap bmp;
                        using (var g = BeginCanvas(w, h, m, out bmp))
                        {
                            var pill = new RectangleF(m, m, w, h);
                            DrawPill(g, pill, h / 2, th, fill, op, m, s);

                            float gx = m + (w - glyphW - capsW) / 2;
                            DrawText(g, glyph, glyphFont, th.Text, th.Halo, halo,
                                     new RectangleF(gx, m - 1 * s, glyphW, h), layout, s * 0.6f);
                            if (state.Caps)
                                DrawText(g, capsMark, capsFont, th.Text, th.Halo, halo,
                                         new RectangleF(gx + glyphW + 3 * s, m - 1 * s, capsW, h), layout, s * 0.5f);

                            if (state.IsAccent)
                            {
                                float lw = Math.Min(14 * s, glyphW * 0.8f);
                                var line = new RectangleF(gx + (glyphW - lw) / 2, m + h - 6 * s, lw, 2 * s);
                                using (var p = RoundRect(line, 1 * s))
                                using (var b = new SolidBrush(accent))
                                    g.FillPath(b, p);
                            }
                        }
                        layout.Dispose();
                        return bmp;
                    }
                }

                string caption = state.Caption;
                using (var capFont = MakeFont(IsAscii(caption) ? (th.LatinFont ?? "Segoe UI") : "Microsoft YaHei UI",
                                              12.5f * s, FontStyle.Regular))
                {
                    float capW = mg.MeasureString(caption, capFont, 1000, fmt).Width;
                    float padL = 14 * s, barW = 3 * s, gap = 11 * s, padR = 18 * s, h = 50 * s;
                    float glyphBox = Math.Max(glyphW, 26 * s);
                    float w = padL + barW + gap + glyphBox + gap + capW + padR;

                    Bitmap bmp;
                    using (var g = BeginCanvas(w, h, m, out bmp))
                    {
                        DrawPill(g, new RectangleF(m, m, w, h), 12 * s, th, fill, op, m, s);

                        // Accent bar
                        float barH = 22 * s;
                        var barRect = new RectangleF(m + padL, m + (h - barH) / 2, barW, barH);
                        using (var p = RoundRect(barRect, barW / 2))
                        using (var b = new SolidBrush(state.IsAccent ? accent : Color.FromArgb(100, th.Text)))
                            g.FillPath(b, p);

                        float gx = m + padL + barW + gap;
                        DrawText(g, glyph, glyphFont, th.Text, th.Halo, halo, new RectangleF(gx, m, glyphBox, h), layout, s);

                        layout.Alignment = StringAlignment.Near;
                        float cx = gx + glyphBox + gap;
                        DrawText(g, caption, capFont, th.Sub, th.Halo, halo, new RectangleF(cx, m + 1 * s, capW + 4 * s, h), layout, s);
                    }
                    layout.Dispose();
                    return bmp;
                }
            }
        }

        private static Graphics BeginCanvas(float w, float h, int m, out Bitmap bmp)
        {
            bmp = new Bitmap((int)Math.Ceiling(w) + 2 * m, (int)Math.Ceiling(h) + 2 * m, PixelFormat.Format32bppArgb);
            var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            return g;
        }

        /// <summary>Background: soft shadow, translucent fill, glassy highlight and hairline border.</summary>
        private static void DrawPill(Graphics g, RectangleF pill, float radius, Theme th, Color fill, float op, int m, float s)
        {
            using (var pillPath = RoundRect(pill, radius))
            {
                // Shadow outside the pill only (so the glass stays clean), fainter when transparent.
                using (var excl = new Region(pillPath))
                {
                    g.SetClip(excl, CombineMode.Exclude);
                    float strength = th.Shadow * (0.35f + 0.65f * op);
                    for (int i = m; i >= 1; i--)
                    {
                        float t = 1f - (float)i / (m + 1);
                        int a = (int)(strength * t * t);
                        if (a < 1) continue;
                        var r = pill;
                        r.Inflate(i * 0.9f, i * 0.9f);
                        r.Offset(0, 2 * s);
                        using (var p = RoundRect(r, radius + i * 0.9f))
                        using (var b = new SolidBrush(Color.FromArgb(a, 0, 0, 0)))
                            g.FillPath(b, p);
                    }
                    g.ResetClip();
                }

                using (var b = new SolidBrush(fill)) g.FillPath(b, pillPath);
                if (th.Highlight > 0)
                    using (var hl = new LinearGradientBrush(pill, Color.FromArgb(th.Highlight, 255, 255, 255),
                               Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                        g.FillPath(hl, pillPath);
                using (var pen = new Pen(th.Border, Math.Max(1f, s))) g.DrawPath(pen, pillPath);
            }
        }

        /// <summary>Draws text with an optional soft halo behind it.</summary>
        private static void DrawText(Graphics g, string text, Font font, Color color, Color haloColor, int haloAlpha,
                                     RectangleF rect, StringFormat fmt, float s)
        {
            if (haloAlpha > 2)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, rect, fmt);
                    float[] widths = { 7f, 4.5f, 2.5f };
                    foreach (float wd in widths)
                        using (var pen = new Pen(Color.FromArgb(haloAlpha / 3, haloColor), wd * s) { LineJoin = LineJoin.Round })
                            g.DrawPath(pen, path);
                }
            }
            using (var b = new SolidBrush(color)) g.DrawString(text, font, b, rect, fmt);
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Font MakeFont(string family, float px, FontStyle style)
        {
            try
            {
                var f = new Font(family, px, style, GraphicsUnit.Pixel);
                if (f.Name == family) return f;
                f.Dispose();
            }
            catch { }
            return new Font("Segoe UI", px, style, GraphicsUnit.Pixel);
        }

        private static bool IsAscii(string s)
        {
            foreach (char c in s) if (c > 0x7F && c != '·') return false;
            return true;
        }

        private static float DpiScaleAt(Native.POINT pt)
        {
            try
            {
                IntPtr mon = Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (Native.GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx > 0) return dx / 96f;
            }
            catch { }
            return 1f;
        }

        public void Dispose()
        {
            _timer.Dispose();
            ReleaseBitmap();
            DestroyHandle();
        }
    }
}
