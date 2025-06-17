using DimScreenSaver;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;


public class BalloonForm : Form
{
    private Timer lifeTimer;
    private bool mouseMoved = false;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);
    public enum BalloonStyle
    {
        WITH_ICONS,
        NO_ICONS
    }
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    private Timer fadeInTimer;
    private Timer fadeOutTimer;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public BalloonForm(string title, string message, int durationMs = 5000, BalloonStyle style = BalloonStyle.WITH_ICONS, string appName = "DimScreenSaver")


    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(228, 228, 228); // jaśniejszy szary
        Size = new Size(364, 111);
        Opacity = 1;

        var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (icon != null)
            this.Icon = icon;

        var appNameLabel = new Label()
        {
            Text = appName,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(20, 20, 20),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(37, 10, 4, 0)
        };

        var titleLabel = new Label()
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 34,
            Padding = (style == BalloonStyle.WITH_ICONS) ? new Padding(82, 9, 12, 0) : new Padding(13, 9, 12, 0),

            TextAlign = ContentAlignment.MiddleLeft
        };

        


        var messageLabel = new Label()
        {
            Text = message,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            ForeColor = Color.FromArgb(91, 91, 91),
            Dock = DockStyle.Fill,
            Height = 28,
            Padding = new Padding(13, 0, 0, 0),
            TextAlign = ContentAlignment.TopLeft
        };

        Controls.Add(messageLabel);
        Controls.Add(titleLabel);



        if (style == BalloonStyle.WITH_ICONS)
        {
            var emojiBox = new PictureBox()
            {
                Image = Image.FromStream(
                    typeof(BalloonForm).Assembly.GetManifestResourceStream("DimScreenSaver.pending.png")),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size = new Size(63, 18),
                Location = new Point(16, appNameLabel.Bottom + 8),
                BackColor = Color.Transparent
            };
            Controls.Add(emojiBox);
            emojiBox.BringToFront();
        }


        if (DimScreenSaver.IdleTrayApp.Instance != null)
        {
            var trayIconImage = DimScreenSaver.IdleTrayApp.Instance
                .GetTrayIconBitmapSafe();

            if (trayIconImage != null)
            {
                var trayIconBox = new PictureBox()
                {
                    Image = trayIconImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(16, 16),
                    Location = new Point(15, 19),
                    BackColor = Color.Transparent
                };
                Controls.Add(trayIconBox);
                trayIconBox.BringToFront();
            }
        }


        Controls.Add(appNameLabel);

        var workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width - 10, workingArea.Bottom - Height - 10);

        lifeTimer = new Timer();
        lifeTimer.Interval = durationMs;
        lifeTimer.Tick += (s, e) =>
        {
            fadeOutTimer = new Timer();
            fadeOutTimer.Interval = 10;
            fadeOutTimer.Tick += (_, __) =>
            {
                if (Opacity > 0)
                    Opacity -= 0.05;
                else
                {
                    fadeOutTimer.Stop();
                    Close();
                }
            };
            fadeOutTimer.Start();
        };
        lifeTimer.Start();

        Opacity = 0; // Startujemy od przezroczystości

        fadeInTimer = new Timer();
        fadeInTimer.Interval = 10; // szybkość animacji (ms)
        fadeInTimer.Tick += (s, e) =>
        {
            if (Opacity < 1)
                Opacity += 0.05;
            else
                fadeInTimer.Stop();
        };
        fadeInTimer.Start();

        HookMouseMoveRecursive(this);

        float scale = 1f;
        var mon = MonitorFromWindow(this.Handle, 2);
        if (GetDpiForMonitor(mon, 0, out uint dpiX, out uint dpiY) == 0)
        {
            scale = dpiX / 96f;
            Debug.WriteLine($"📌 BalloonForm: DPI = {dpiX} → scale x{scale:0.00}");
            this.Scale(new SizeF(scale, scale));
        }

        if (scale > 1.4f)
        {
            titleLabel.Height += 2;
            var pad = titleLabel.Padding;
            titleLabel.Padding = new Padding(pad.Left, pad.Top + 2, pad.Right, pad.Bottom);
        }

        var screen = Screen.FromHandle(this.Handle).WorkingArea;
        this.Left = screen.Right - this.Width - 5;
        this.Top = screen.Bottom - this.Height - 5;


    }




    public BalloonForm(int currentBrightness)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(228, 228, 228);
        Size = new Size(360, 86);
        Opacity = 0;

        var label = new Label()
        {
            Text = $"Jasność ekranu",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(20, 20, 20),
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 8, 4, 0)
        };

        var slider = new TrackBar()
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            SmallChange = 10,
            LargeChange = 10,
            Value = currentBrightness,
            TickStyle = TickStyle.None,
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(228, 228, 228),
            Margin = new Padding(10)
        };

        var valueLabel = new Label()
        {
            Text = currentBrightness.ToString(),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.Black,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 10, 0)
        };

        slider.ValueChanged += async (s, e) =>
        {
            valueLabel.Text = slider.Value.ToString();
            await IdleTrayApp.SetBrightnessAsync(slider.Value);
            var app = IdleTrayApp.Instance;
            if (app != null)
            {
                app.SetKeyboardBacklightBasedOnBrightnessForce(slider.Value);
                app.lastKnownBrightness = slider.Value;
            }
        };

        Controls.Add(valueLabel);
        Controls.Add(slider);
        Controls.Add(label);

        var workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width - 10, workingArea.Bottom - Height - 10);

        // fadeIn
        fadeInTimer = new Timer();
        fadeInTimer.Interval = 10;
        fadeInTimer.Tick += (s, e) =>
        {
            if (Opacity < 1)
                Opacity += 0.05;
            else
                fadeInTimer.Stop();
        };
        fadeInTimer.Start();

        HookMouseMoveRecursive(this);
    }


    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST = 0x00000008;

            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }
    private void HookMouseMoveRecursive(Control control)
    {
        control.MouseMove += (_, __) => TriggerClose();
        foreach (Control child in control.Controls)
        {
            HookMouseMoveRecursive(child);
        }
    }

    private void TriggerClose()
    {
        if (!mouseMoved)
        {
            mouseMoved = true;

            fadeInTimer?.Stop();    // 💥 To zatrzymuje fade-in
            fadeOutTimer?.Stop();   // <- tylko jeśli już był wcześniej

            var localFade = new Timer();
            localFade.Interval = 10;
            localFade.Tick += (_, __) =>
            {
                if (Opacity > 0)
                    Opacity -= 0.05;
                else
                {
                    localFade.Stop();
                    Close();
                }
            };
            localFade.Start();
        }
    }

    public static void ShowBalloon(string title, string message, int durationMs = 5000, bool showIcons = true, string appName = "DimScreenSaver")

  

    {
        
        Task.Run(() =>
        {
            var style = showIcons ? BalloonStyle.WITH_ICONS : BalloonStyle.NO_ICONS;
            Application.Run(new BalloonForm(title, message, durationMs, style, appName));
        });

    }
}
