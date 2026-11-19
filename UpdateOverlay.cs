using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

public sealed class QuizUpdateOverlay : Control {
    private Bitmap background;
    private readonly Timer animation = new Timer { Interval = 40 };
    private float angle;
    public int Progress { get; set; }

    public QuizUpdateOverlay(Bitmap source) {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        background = new Bitmap(Math.Max(1, source.Width / 9), Math.Max(1, source.Height / 9));
        using (var g = Graphics.FromImage(background)) {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(source, 0, 0, background.Width, background.Height);
        }
        Dock = DockStyle.Fill;
        TabStop = true;
        AccessibleName = "업데이트 진행률";
        animation.Tick += (s, e) => { angle = (angle + 9) % 360; Invalidate(); };
        animation.Start();
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(background, ClientRectangle);
        using (var dim = new SolidBrush(Color.FromArgb(200, 24, 34, 45)))
            g.FillRectangle(dim, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        float scale = g.DpiX / 96f;
        float side = Math.Min(62 * scale, Height * .28f);
        var ring = new RectangleF((Width - side) / 2, Height / 2f - side, side, side);
        using (var track = new Pen(Color.FromArgb(90, 139, 155), 4 * scale))
        using (var arc = new Pen(Color.FromArgb(174, 232, 91), 4 * scale) {
            StartCap = LineCap.Round, EndCap = LineCap.Round }) {
            g.DrawEllipse(track, ring);
            g.DrawArc(arc, ring, angle, 95 + Progress * 1.8f);
        }
        using (var text = new SolidBrush(Color.FromArgb(240, 247, 250)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {
            g.DrawString("업데이트 중 " + Progress + "%", Font, text,
                new RectangleF(12, Height / 2f + 16 * scale, Width - 24, 42 * scale), format);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) { animation.Dispose(); if (background != null) background.Dispose(); }
        base.Dispose(disposing);
    }
}
