using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocationSharer;

public sealed class RoundedPanel : Panel
{
    public int BorderRadius { get; set; } = 12;
    public Color BorderColor { get; set; } = Color.FromArgb(39, 39, 42); // Zinc-800
    public int BorderThickness { get; set; } = 1;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(9, 9, 11); // Zinc-950
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (Width <= 0 || Height <= 0) return;

        using var path = GetRoundedRectanglePath(ClientRectangle, BorderRadius);
        
        // Fill background
        using (var brush = new SolidBrush(BackColor))
        {
            e.Graphics.FillPath(brush, path);
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
