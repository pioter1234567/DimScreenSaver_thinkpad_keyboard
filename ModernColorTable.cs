using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

public class ModernColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(200, 200, 200); // ramka
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Color.White; // hover
    public override Color MenuItemSelectedGradientBegin => Color.White;
    public override Color MenuItemSelectedGradientEnd => Color.White;
    public override Color ToolStripDropDownBackground => Color.FromArgb(238, 238, 238);
    public override Color ImageMarginGradientBegin => Color.FromArgb(238, 238, 238);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(238, 238, 238);
    public override Color ImageMarginGradientEnd => Color.FromArgb(238, 238, 238);
}

public class ModernRenderer : ToolStripProfessionalRenderer
{
    public ModernRenderer() : base(new ModernColorTable()) { }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        Rectangle r = e.ImageRectangle;

        // Fallback jeśli ToolStrip nie przydzielił miejsca
        if (r.Width == 0 || r.Height == 0)
        {
            r = new Rectangle(8, e.Item.Bounds.Top + (e.Item.Bounds.Height / 2) - 6, 16, 16);
        }

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        Color bgColor = e.Item.Selected ? Color.White : Color.FromArgb(238, 238, 238);
        using (Brush b = new SolidBrush(bgColor))
            g.FillRectangle(b, r);

        using (Pen pen = new Pen(Color.FromArgb(15, 15, 60), 1))
        {
            Point p1 = new Point(r.Left + 4, r.Top + r.Height / 2);
            Point p2 = new Point(r.Left + r.Width / 2, r.Bottom - 4);
            Point p3 = new Point(r.Right, r.Top + 4);
            g.DrawLines(pen, new[] { p1, p2, p3 });
        }
    }



    

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is ToolStripSeparator)
        {
            Debug.WriteLine(">>> Background skip for separator");
            return;
        }

        if (e.Item.Selected)
            e.Graphics.FillRectangle(new SolidBrush(Color.White), e.Item.ContentRectangle);
        else
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(238, 238, 238)), e.Item.ContentRectangle);
    }

}
