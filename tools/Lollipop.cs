using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary>Draws the LangPop logo: a rainbow pinwheel lollipop. Designed on a 256px grid.</summary>
public static class Lollipop
{
    static readonly string[] Swirl = { "FF5C8A", "FFB443", "FFE14D", "5FD38D", "4DB5FF", "9B7BFF" };

    public static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(size / 256f, size / 256f);

            const float cx = 128, cy = 96, R = 84;

            // Stick
            var stick = new RectangleF(cx - 9, cy + R - 20, 18, 250 - (cy + R - 20) - 4);
            using (var p = RoundRect(stick, 9))
            {
                using (var b = new LinearGradientBrush(stick, C("FFFFFF"), C("D9D4E3"), 0f)) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(70, 60, 40, 90), 3f)) g.DrawPath(pen, p);
            }

            // Candy: soft shadow, curved rainbow wedges, rim, gloss
            var candy = new GraphicsPath();
            candy.AddEllipse(cx - R, cy - R, 2 * R, 2 * R);
            using (var sh = new SolidBrush(Color.FromArgb(40, 40, 20, 60)))
                g.FillEllipse(sh, cx - R + 4, cy - R + 8, 2 * R, 2 * R);
            using (var b = new SolidBrush(Color.White)) g.FillPath(b, candy);

            g.SetClip(candy);
            const int wedges = 10;
            const float twist = 2.2f;   // radians the wedge edges curl by the rim
            for (int i = 0; i < wedges; i++)
            {
                float a0 = (float)(2 * Math.PI * i / wedges), a1 = (float)(2 * Math.PI * (i + 1) / wedges);
                var pts = new List<PointF>();
                for (int s = 0; s <= 24; s++) { float t = s / 24f; pts.Add(P(cx, cy, t * R * 1.05f, a0 + t * twist)); }
                for (int s = 24; s >= 0; s--) { float t = s / 24f; pts.Add(P(cx, cy, t * R * 1.05f, a1 + t * twist)); }
                using (var b = new SolidBrush(C(Swirl[i % Swirl.Length]))) g.FillPolygon(b, pts.ToArray());
            }
            g.ResetClip();

            using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 4f)) g.DrawPath(pen, candy);
            using (var gloss = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                var arc = new GraphicsPath();
                arc.AddArc(cx - R + 16, cy - R + 16, 2 * R - 32, 2 * R - 32, 200, 70);
                using (var pen = new Pen(gloss, 13f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawPath(pen, arc);
                g.FillEllipse(gloss, cx - R * 0.08f, cy - R + 14, 11, 11);
            }
        }
        return bmp;
    }

    static Color C(string hex)
    {
        int v = Convert.ToInt32(hex, 16);
        return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
    }

    static PointF P(float cx, float cy, float r, float ang)
    {
        return new PointF(cx + r * (float)Math.Cos(ang), cy + r * (float)Math.Sin(ang));
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); float d = 2 * radius;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}
