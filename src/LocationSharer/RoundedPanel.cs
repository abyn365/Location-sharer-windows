using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocationSharer;

public sealed class RoundedPanel : Panel
{
    public int BorderRadius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.FromArgb(63, 63, 70); // Zinc-700 (brighter for glass)
    public int BorderThickness { get; set; } = 1;
    public Color GlowColor { get; set; } = Color.FromArgb(40, 255, 255, 255); // Subtle white glow

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(200, 9, 9, 11); // Semi-transparent Zinc-950
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            using var path = GetRoundedRectanglePath(ClientRectangle, BorderRadius);
            Region = new Region(path);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = ClientRectangle;

        using var path = GetRoundedRectanglePath(rect, BorderRadius);

        // Fill background (semi-transparent to let backdrop show through)
        using (var brush = new SolidBrush(BackColor))
        {
            e.Graphics.FillPath(brush, path);
        }

        // Glass inner glow - a subtle lighter band around the edge
        using (var glowPen = new Pen(GlowColor, 2))
        {
            var innerRect = Rectangle.Inflate(rect, -1, -1);
            using var glowPath = GetRoundedRectanglePath(innerRect, Math.Max(0, BorderRadius - 1));
            e.Graphics.DrawPath(glowPen, glowPath);
        }

        // Draw border
        if (BorderThickness > 0)
        {
            using var pen = new Pen(BorderColor, BorderThickness);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;

        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        // Top-left
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        // Top-right
        path.AddArc(rect.Right - diameter - 1, rect.Y, diameter, diameter, 270, 90);
        // Bottom-right
        path.AddArc(rect.Right - diameter - 1, rect.Bottom - diameter - 1, diameter, diameter, 0, 90);
        // Bottom-left
        path.AddArc(rect.X, rect.Bottom - diameter - 1, diameter, diameter, 90, 90);

        path.CloseFigure();
        return path;
    }
}