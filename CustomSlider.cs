using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class CustomSlider : UserControl
{
    public event EventHandler ValueChanged;

    private int _value = 5;
    private int _minimum = 0;
    private int _maximum = 10;
    private Rectangle thumbRect;
    private bool _isDragging = false;
    private bool _isHovering = false;
    private bool _hoverDisabledUntilLeave = false;
    public bool InteractionLocked { get; set; } = false;
    private const int WM_SETCURSOR = 0x0020;

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
            if (_value != clamped)
            {
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }


    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; Invalidate(); }
    }

    public CustomSlider()
    {
        this.Cursor = Cursors.Default;
        this.DoubleBuffered = true;
        this.Height = 30;
        this.Width = 150;
        this.SetStyle(ControlStyles.UserMouse, true);
        this.MouseWheel += (s, e) => Focus();
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint |
                      ControlStyles.ResizeRedraw |
                      ControlStyles.OptimizedDoubleBuffer, true);
        this.Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;

        int trackHeight = 2;
        int thumbWidth = 8;
        int thumbHeight = 24;
        int offsetY = 0;
        int marginY = ((this.Height - thumbHeight) / 2) - offsetY;
        //int availableWidth = this.Width - thumbWidth;

        float percent = (float)(_value - _minimum) / (_maximum - _minimum);
        int thumbX = (int)(percent * (this.Width - thumbWidth - 1));
        thumbRect = new Rectangle(thumbX, marginY, thumbWidth, thumbHeight);

        // 🔹 WYŁĄCZ AA DLA TRACKA
        g.SmoothingMode = SmoothingMode.None;

        // Tło suwaka (po prawej)
        using (Brush bgBrush = new SolidBrush(Color.FromArgb(137, 137, 137)))
            g.FillRectangle(bgBrush, 0, this.Height / 2 - trackHeight / 2, this.Width, trackHeight);

        // Pasek aktywny (po lewej)
        using (Brush activeBrush = new SolidBrush(Color.FromArgb(0, 183, 195)))
            g.FillRectangle(activeBrush, 0, this.Height / 2 - trackHeight / 2, thumbX + thumbWidth / 2, trackHeight);

        // 🔹 WŁĄCZ AA DLA CHWYTAKA
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color thumbColor;

        if (_isDragging)
            thumbColor = Color.Black;
        else if (_isHovering && !_hoverDisabledUntilLeave)
            thumbColor = Color.Black;
        else
            thumbColor = Color.FromArgb(0, 183, 195);

        using (Brush thumbBrush = new SolidBrush(thumbColor))
        {
            g.FillRoundedRectangle(thumbBrush, thumbRect, 4);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovering = true;
        Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovering = false;
        _hoverDisabledUntilLeave = false; // reset po wyjściu
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (InteractionLocked) return;

        base.OnMouseWheel(e);

        int delta = e.Delta;
        int step = 2;

        if (delta > 0)
            Value = Math.Min(Value + step, Maximum);
        else if (delta < 0)
            Value = Math.Max(Value - step, Minimum);
    }



    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (InteractionLocked) return;

        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            if (thumbRect.Contains(e.Location))
            {
                _isDragging = true;
            }
            else
            {
                float percent = (float)(e.X - thumbRect.Width / 2) / (Width - thumbRect.Width);
                percent = Math.Max(0, Math.Min(1, percent));
                Value = Minimum + (int)(percent * (Maximum - Minimum));
                _isDragging = true;
            }

            this.Capture = true;
            Invalidate();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SETCURSOR)
        {
            Cursor.Current = Cursors.Default;
            m.Result = (IntPtr)1; // zablokuj domyślną obsługę kursora
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (InteractionLocked) return;

        base.OnMouseMove(e);

        if (_isDragging)
        {
            float percent = (float)(e.X - thumbRect.Width / 2) / (Width - thumbRect.Width);
            percent = Math.Max(0, Math.Min(1, percent));
            Value = Minimum + (int)(percent * (Maximum - Minimum));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (InteractionLocked) return;

        base.OnMouseUp(e);
        _isDragging = false;
        this.Capture = false;
        _hoverDisabledUntilLeave = true;
        Invalidate();
    }

    private void UpdateValueFromPosition(int x)
    {
        int thumbWidth = 8;
        int availableWidth = this.Width - thumbWidth;
        float percent = (float)(x - thumbWidth / 2) / availableWidth;
        Value = _minimum + (int)(percent * (_maximum - _minimum));
    }
}
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
