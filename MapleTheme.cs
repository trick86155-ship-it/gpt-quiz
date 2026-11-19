using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class MapleTheme {
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);

    private static readonly PrivateFontCollection Fonts = new PrivateFontCollection();

    public static void Load(string path) {
        if (AddFontResourceEx(path, 0x10, IntPtr.Zero) == 0)
            return;
        Fonts.AddFontFile(path);
    }

    public static Font Font(float size) {
        var families = Fonts.Families;
        return families.Length == 0
            ? new Font("Malgun Gothic", size, FontStyle.Bold)
            : new Font(families[0], size, FontStyle.Bold);
    }

    internal static GraphicsPath Round(RectangleF rect, float radius) {
        var path = new GraphicsPath();
        float d = 2 * radius;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class MapleButton : Button {
    public bool Primary { get; set; }
    public bool UpdateAvailable { get; set; }
    private bool hot;
    private bool down;

    public MapleButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Opaque, false);
        FlatStyle = FlatStyle.Flat;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); down = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); down = false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { down = true; Invalidate(); } }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); down = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(Parent.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var rect = new RectangleF(2, 2, Width - 5, Height - 5);
        Color top, bottom, ink;
        if (!Enabled) {
            top = Color.FromArgb(210, 216, 219); bottom = Color.FromArgb(190, 199, 203);
            ink = Color.FromArgb(63, 73, 79);
        } else if (UpdateAvailable) {
            top = Color.FromArgb(201, 236, 112); bottom = Color.FromArgb(137, 185, 44);
            ink = Color.FromArgb(29, 48, 12);
        } else if (Primary) {
            top = Color.FromArgb(113, 235, 239); bottom = Color.FromArgb(26, 170, 193);
            ink = Color.FromArgb(7, 43, 55);
        } else {
            top = Color.FromArgb(87, 102, 117); bottom = Color.FromArgb(53, 66, 82);
            ink = Color.White;
        }
        if (hot && Enabled) top = ControlPaint.Light(top, .28f);
        if (down && Enabled) { top = ControlPaint.Dark(top, .12f); bottom = ControlPaint.Dark(bottom, .12f); }
        using (var shape = MapleTheme.Round(rect, 6))
        using (var fill = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
        using (var edge = new Pen(Color.FromArgb(24, 40, 49)))
        using (var shine = new Pen(Color.FromArgb(100, Color.White))) {
            g.FillPath(fill, shape);
            g.DrawPath(edge, shape);
            var inner = RectangleF.Inflate(rect, -1, -1);
            using (var inside = MapleTheme.Round(inner, 5)) g.DrawPath(shine, inside);
        }
        if (Focused) {
            using (var focus = new Pen(Primary ? Color.FromArgb(11, 76, 91) : Color.FromArgb(150, 244, 255), 2))
            using (var ring = MapleTheme.Round(RectangleF.Inflate(rect, -3, -3), 3))
                g.DrawPath(focus, ring);
        }
        using (var brush = new SolidBrush(ink))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString(Text, Font, brush, rect, format);
    }
}

public sealed class MapleCheckBox : CheckBox {
    public MapleCheckBox() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(Parent.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float scale = Math.Max(g.DpiX / 96f, Height / 30f);
        float side = 15 * scale;
        var box = new RectangleF(2 * scale, (Height - side) / 2, side, side);
        Color fill = Checked ? Color.FromArgb(26, 170, 193) : Color.FromArgb(247, 250, 252);
        if (!Enabled) fill = Checked ? Color.FromArgb(117, 155, 164) : Color.FromArgb(206, 216, 223);
        using (var shape = MapleTheme.Round(box, 3 * scale))
        using (var background = new SolidBrush(fill))
        using (var outline = new Pen(Color.FromArgb(66, 91, 105), scale)) {
            g.FillPath(background, shape);
            g.DrawPath(outline, shape);
        }
        if (Checked) {
            using (var tick = new Pen(Color.FromArgb(11, 48, 58), 1.8f * scale)) {
                tick.StartCap = tick.EndCap = LineCap.Round;
                tick.LineJoin = LineJoin.Round;
                g.DrawLines(tick, new[] {
                    new PointF(box.X + side * .22f, box.Y + side * .52f),
                    new PointF(box.X + side * .43f, box.Y + side * .73f),
                    new PointF(box.X + side * .79f, box.Y + side * .31f)
                });
            }
        }
        float left = box.Right + 6 * scale;
        var label = new RectangleF(left, 0, Math.Max(0, Width - left), Height);
        using (var ink = new SolidBrush(ForeColor))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(Text, Font, ink, label, format);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -2), ForeColor, Parent.BackColor);
    }
}

public sealed class MapleSectionBar : Label {
    protected override void OnPaint(PaintEventArgs e) {
        Rectangle area = ClientRectangle;
        using (var fill = new LinearGradientBrush(area, Color.FromArgb(246, 248, 249), Color.FromArgb(187, 199, 208), LinearGradientMode.Vertical))
        using (var edge = new Pen(Color.FromArgb(134, 150, 162)))
        using (var capEdge = new Pen(Color.FromArgb(160, 174, 184)))
        using (var cap = new SolidBrush(Color.FromArgb(224, 234, 239)))
        using (var ink = new SolidBrush(ForeColor))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center }) {
            e.Graphics.FillRectangle(fill, area);
            int capWidth = (int)(116 * e.Graphics.DpiX / 96);
            var points = new[] { new Point(0, 0), new Point(capWidth, 0),
                new Point(capWidth - 12, area.Height - 1), new Point(0, area.Height - 1) };
            e.Graphics.FillPolygon(cap, points);
            e.Graphics.DrawLine(capEdge, points[1], points[2]);
            e.Graphics.DrawLine(edge, 0, area.Height - 1, area.Width, area.Height - 1);
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            e.Graphics.DrawString(Text, Font, ink, new RectangleF(12, 0, capWidth - 12, area.Height), format);
        }
    }
}

public class CaptureOutline : Form {
    // A layered, click-through window keeps the selected contents interactive.
    private static readonly Color Key = Color.Magenta;
    public static readonly Color AnchorColor = Color.FromArgb(199, 110, 255);
    public static readonly Color FullColor = Color.FromArgb(16, 222, 222);
    private const int LineWidth = 3;
    private readonly Color stroke;

    public CaptureOutline(Color stroke) {
        this.stroke = stroke;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = Size.Empty;
        BackColor = Key;
        TransparencyKey = Key;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams {
        get {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080;
            return parameters;
        }
    }

    protected override void WndProc(ref Message message) {
        const int HitTest = 0x0084;
        if (message.Msg == HitTest) {
            message.Result = new IntPtr(-1); // HTTRANSPARENT
            return;
        }
        base.WndProc(ref message);
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmFlush();

    public virtual bool IntersectsStroke(Rectangle area) {
        var inner = Rectangle.Inflate(Bounds, -LineWidth, -LineWidth);
        return Visible && Bounds.IntersectsWith(area) && !inner.Contains(area);
    }

    public void Place(Rectangle area) {
        area.Inflate(LineWidth, LineWidth);
        Bounds = area;
        Show();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        using (var fill = new SolidBrush(stroke)) {
            e.Graphics.FillRectangle(fill, 0, 0, Width, LineWidth);
            e.Graphics.FillRectangle(fill, 0, Height - LineWidth, Width, LineWidth);
            e.Graphics.FillRectangle(fill, 0, LineWidth, LineWidth, Height - 2 * LineWidth);
            e.Graphics.FillRectangle(fill, Width - LineWidth, LineWidth, LineWidth, Height - 2 * LineWidth);
        }
    }
}

public sealed class QuizActionHud : CaptureOutline {
    private Point[] starts, ends;
    private bool drag;

    public QuizActionHud() : base(Color.Gold) {}

    public void Preview(Rectangle area, Point[] from, Point[] to, bool showDrag) {
        drag = showDrag;
        area.Inflate(18, 18);
        starts = Array.ConvertAll(from, point => new Point(point.X - area.Left, point.Y - area.Top));
        ends = Array.ConvertAll(to, point => new Point(point.X - area.Left, point.Y - area.Top));
        Bounds = area;
        Show();
        Invalidate();
    }

    public override bool IntersectsStroke(Rectangle area) {
        return Visible && Bounds.IntersectsWith(area);
    }

    protected override void OnPaint(PaintEventArgs e) {
        if (starts == null) return;
        if (drag) {
            using (var shadow = new Pen(Color.Black, 8))
            using (var line = new Pen(Color.Gold, 4)) {
                line.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                for (int i = 0; i < starts.Length; i++) e.Graphics.DrawLine(shadow, starts[i], ends[i]);
                for (int i = 0; i < starts.Length; i++) e.Graphics.DrawLine(line, starts[i], ends[i]);
            }
        }
        foreach (Point start in starts) DrawMarker(e.Graphics, start, Color.Gold);
        if (drag) foreach (Point end in ends) DrawMarker(e.Graphics, end, Color.Lime);
    }

    private static void DrawMarker(Graphics graphics, Point point, Color color) {
        var ring = new Rectangle(point.X - 12, point.Y - 12, 24, 24);
        using (var shadow = new Pen(Color.Black, 7))
        using (var ink = new Pen(color, 3))
        using (var fill = new SolidBrush(color)) {
            graphics.DrawEllipse(shadow, ring);
            graphics.DrawEllipse(ink, ring);
            graphics.FillEllipse(fill, point.X - 2, point.Y - 2, 5, 5);
        }
    }
}

public sealed class MapleLabel : Label {
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (var ink = new SolidBrush(ForeColor))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord }) {
            format.Alignment = TextAlign == ContentAlignment.MiddleRight ? StringAlignment.Far : StringAlignment.Near;
            e.Graphics.DrawString(Text, Font, ink, ClientRectangle, format);
        }
    }
}

public sealed class MaplePresetList : ComboBox {
    public MaplePresetList() {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 24;
    }

    protected override void OnDrawItem(DrawItemEventArgs e) {
        e.DrawBackground();
        if (e.Index < 0) return;
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (var ink = new SolidBrush(e.ForeColor))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center }) {
            var text = new RectangleF(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height);
            e.Graphics.DrawString(GetItemText(Items[e.Index]), Font, ink, text, format);
        }
        e.DrawFocusRectangle();
    }
}

public sealed class MapleAnswerBox : TextBox {
    private IntPtr displayFont;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class LogFont {
        public int Height, Width, Escapement, Orientation, Weight;
        public byte Italic, Underline, StrikeOut, CharacterSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FaceName;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontIndirect([In] LogFont font);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private void ApplyDisplayFont() {
        if (!IsHandleCreated) return;
        var settings = new LogFont();
        using (var graphics = CreateGraphics()) Font.ToLogFont(settings, graphics);
        settings.Quality = 4; // ANTIALIASED_QUALITY, without colored ClearType fringes.
        IntPtr replacement = CreateFontIndirect(settings);
        if (replacement == IntPtr.Zero) return;
        SendMessage(Handle, 0x0030, replacement, new IntPtr(1)); // WM_SETFONT
        if (displayFont != IntPtr.Zero) DeleteObject(displayFont);
        displayFont = replacement;
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        ApplyDisplayFont();
    }

    protected override void OnFontChanged(EventArgs e) {
        base.OnFontChanged(e);
        ApplyDisplayFont();
    }

    protected override void OnHandleDestroyed(EventArgs e) {
        base.OnHandleDestroyed(e);
        if (displayFont != IntPtr.Zero) {
            DeleteObject(displayFont);
            displayFont = IntPtr.Zero;
        }
    }
}

public static class QuizPresence {
    private static bool Marker(Color c) {
        return c.R >= 105 && c.R <= 180 && c.G >= 165 && c.G <= 220 &&
            c.G - c.R >= 24 && c.B <= 88;
    }

    private static bool Pale(Color c) {
        return c.R >= 224 && c.G >= 224 && c.B >= 224 &&
            Math.Abs(c.R - c.G) <= 15 && Math.Abs(c.G - c.B) <= 15;
    }

    private static Bitmap Resize(Bitmap source, int width, int height) {
        var result = new Bitmap(width, height);
        using (var g = Graphics.FromImage(result)) {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(source, 0, 0, width, height);
        }
        return result;
    }

    // Shape of the green Q at the start of the quiz heading.
    private static readonly string[] QuestionMarker = {
        "....####....", "..########..", ".####..####.", ".##.....###.",
        "###......##.", "###......###", "##.......###", "##.......###",
        "###......##.", "###...#####.", ".###..#####.", "..########..",
        "...#######..", ".........##."
    };

    public static Rectangle FindQuestion(Bitmap source) {
        int width = Math.Min(source.Width, 900);
        double scale = (double)width / source.Width;
        int height = Math.Max(1, (int)(source.Height * scale));
        using (var image = Resize(source, width, height)) {
            var pixels = new byte[width * height];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    var c = image.GetPixel(x, y);
                    pixels[y * width + x] = Marker(c) ? (byte)1 :
                        Pale(c) ? (byte)2 :
                        (c.R < 130 && c.G < 130 && c.B < 130) ? (byte)3 : (byte)0;
                }
            }

            Rectangle best = Rectangle.Empty;
            int bestScore = 37;
            for (int y = 2; y < height - 13; y++) {
                for (int x = 3; x < width - 46; x++) {
                    int at = y * width + x;
                    if (pixels[at] != 1 || pixels[at - width] != 2) continue;
                    for (int h = 9; h <= 30 && y + h < height; h++) {
                        int w = (int)Math.Round(h * 12.0 / 14);
                        int left = x - (int)Math.Round(w / 3.0);
                        if (left < 1 || left + 3 * w + 16 >= width) continue;
                        if (pixels[y * width + left - 1] != 2) continue;
                        int score = 0;
                        for (int row = 0; row < QuestionMarker.Length && score < bestScore; row++) {
                            int from = (y + row * h / 14) * width + left;
                            for (int col = 0; col < 12; col++) {
                                if ((pixels[from + col * w / 12] == 1) != (QuestionMarker[row][col] == '#')) score++;
                            }
                        }
                        if (score >= bestScore) continue;

                        int textStart = left + w + w / 2;
                        int rightLimit = Math.Min(width, left + 29 * w);
                        int firstText = Math.Min(rightLimit, textStart + 5 * w);
                        int ink = 0, pale = 0;
                        for (int row = y; row < y + h; row++) {
                            for (int col = textStart; col < firstText; col++) {
                                byte color = pixels[row * width + col];
                                if (color == 3) ink++;
                                if (color == 2) pale++;
                            }
                        }
                        if (ink < h || pale < (firstText - textStart) * h / 2) continue;
                        bestScore = score;
                        int bottom = Math.Min(height, y + h * 3);
                        best = Rectangle.FromLTRB((int)(left / scale), (int)(y / scale),
                            Math.Min(source.Width, (int)Math.Ceiling(rightLimit / scale)),
                            Math.Min(source.Height, (int)Math.Ceiling(bottom / scale)));
                    }
                }
            }
            return best;
        }
    }

    private static bool LooksLikeChoiceRow(Bitmap image, int x, int y) {
        if (!Pale(image.GetPixel(x + 65, y)) || !Pale(image.GetPixel(x + 90, y))) return false;
        int textPixels = 0, palePixels = 0;
        for (int row = y - 3; row <= y + 3; row++) {
            for (int column = x + 6; column < x + 39; column++) {
                Color pixel = image.GetPixel(column, row);
                if (Pale(pixel)) palePixels++;
                if (pixel.R < 155 && pixel.G < 155 && pixel.B < 155) textPixels++;
            }
        }
        return palePixels >= 135 && textPixels >= 3;
    }

    public static bool HasChoices(Bitmap source) {
        using (var image = Resize(source, 240, 180)) {
            // Detect a numbered option row without depending on the number of choices.
            for (int x = 5; x < 65; x++) {
                int start = -1;
                for (int y = 4; y < image.Height - 3; y++) {
                    bool badge = y < image.Height - 4 && Marker(image.GetPixel(x, y)) &&
                        Pale(image.GetPixel(x - 5, y)) && Pale(image.GetPixel(x + 5, y));
                    if (badge) {
                        if (start < 0) start = y;
                    } else if (start >= 0) {
                        int length = y - start;
                        if (length >= 3 && length <= 15 && LooksLikeChoiceRow(image, x, start + length / 2))
                            return true;
                        start = -1;
                    }
                }
            }
        }
        return false;
    }
}
public sealed class LunaDpiForm : Form {
    private sealed class LayoutItem {
        public Control Control;
        public Rectangle Bounds;
        public float Points;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);

    private readonly System.Collections.Generic.List<LayoutItem> layout =
        new System.Collections.Generic.List<LayoutItem>();
    private readonly System.Collections.Generic.List<Font> activeFonts =
        new System.Collections.Generic.List<Font>();
    private Size baseline;
    private bool ready;
    private float scale;

    public LunaDpiForm() { AutoScaleMode = AutoScaleMode.None; }

    public void SealLayout() {
        baseline = ClientSize;
        Remember(Controls);
        ready = true;
    }

    private void Remember(Control.ControlCollection controls) {
        foreach (Control control in controls) {
            layout.Add(new LayoutItem { Control = control, Bounds = control.Bounds, Points = control.Font.SizeInPoints });
            Remember(control.Controls);
        }
    }

    protected override void OnShown(EventArgs e) {
        var work = Screen.FromHandle(Handle).WorkingArea;
        ApplyDpi((int)GetDpiForWindow(Handle), work);
        FitLocation(Location, work);
        base.OnShown(e);
    }

    public void ApplyDpi(int dpi, Rectangle workArea) {
        if (!ready) return;
        float target = Math.Min(Math.Max(dpi, 96) / 96f,
            Math.Min((workArea.Width - 32f) / baseline.Width, (workArea.Height - 88f) / baseline.Height));
        if (target <= 0) return;
        if (Math.Abs(target - scale) < .005f) {
            var desired = new Size(Px(baseline.Width), Px(baseline.Height));
            if (ClientSize != desired) ClientSize = desired;
            return;
        }
        scale = target;
        var previousFonts = activeFonts.ToArray();
        activeFonts.Clear();
        SuspendLayout();
        foreach (LayoutItem item in layout) item.Control.SuspendLayout();
        try {
            ClientSize = new Size(Px(baseline.Width), Px(baseline.Height));
            foreach (LayoutItem item in layout) {
                var control = item.Control;
                // Pixel units prevent the drawing APIs from applying the monitor scale twice.
                var font = new Font(control.Font.FontFamily, item.Points * 96f / 72f * scale, control.Font.Style, GraphicsUnit.Pixel);
                activeFonts.Add(font);
                control.Font = font;
                var picker = control as MaplePresetList;
                if (picker != null) picker.ItemHeight = Px(24);
                var r = item.Bounds;
                control.SetBounds(Px(r.X), Px(r.Y), Px(r.Width), Px(r.Height));
            }
        } finally {
            for (int i = layout.Count - 1; i >= 0; i--) layout[i].Control.ResumeLayout(true);
            ResumeLayout(true);
            foreach (Font font in previousFonts) font.Dispose();
        }
    }

    private void FitLocation(Point origin, Rectangle work) {
        Location = new Point(Math.Max(work.Left, Math.Min(origin.X, work.Right - Width)),
            Math.Max(work.Top, Math.Min(origin.Y, work.Bottom - Height)));
    }

    private int Px(int value) { return (int)Math.Round(value * scale); }

    protected override void WndProc(ref Message message) {
        if (message.Msg == 0x02E0 && ready && message.LParam != IntPtr.Zero) {
            var next = (NativeRect)Marshal.PtrToStructure(message.LParam, typeof(NativeRect));
            var work = Screen.FromPoint(new Point((next.Left + next.Right) / 2, (next.Top + next.Bottom) / 2)).WorkingArea;
            ApplyDpi((int)(message.WParam.ToInt64() & 0xffff), work);
            FitLocation(new Point(next.Left, next.Top), work);
            BeginInvoke((MethodInvoker)delegate {
                if (IsDisposed) return;
                var settled = Screen.FromHandle(Handle).WorkingArea;
                ApplyDpi((int)GetDpiForWindow(Handle), settled);
                FitLocation(Location, settled);
            });
            return;
        }
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            foreach (Font font in activeFonts) font.Dispose();
            activeFonts.Clear();
        }
    }
}
