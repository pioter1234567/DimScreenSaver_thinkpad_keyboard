using DimScreenSaver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;





public class BrightnessForm : Form
{
    private readonly System.Windows.Forms.Timer fadeInTimer;
    private readonly System.Windows.Forms.Timer autoCloseTimer;
    private readonly System.Windows.Forms.Timer brightnessPollTimer;
    private readonly System.Windows.Forms.Timer scrollTimer;
    private static readonly string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");
    private readonly CustomSlider slider;
    private readonly Label valueLabel;
    private readonly bool isAdjusting = false;
    private bool isUpdatingFromPolling = false;
    private DateTime brightnessSetAt = DateTime.MinValue;
    private DateTime mouseDownAt = DateTime.MinValue;
    private bool scrollInProgress = false;
    private bool valueChangedSinceLastApply = false;
    private DateTime scrollStartedAt = DateTime.MinValue;
    private bool isBatterySaverActive = false;
    private readonly PictureBox brightnessIcon;
    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);





    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        IdleTrayApp.MouseHook.Stop();
        base.OnFormClosed(e);
        brightnessPollTimer?.Stop();
        brightnessPollTimer?.Dispose();
    }

    private static void LogBrightness(string message)
    {
        string logFile = logPath;
        string logEntry = $"[BrightnessForm] {DateTime.Now:HH:mm:ss} {message}";

        try
        {
            const int maxLines = 5000;

            // odczytaj istniejące linie (jeśli plik istnieje)
            List<string> lines = new List<string>();
            if (File.Exists(logFile))
            {
                lines = File.ReadAllLines(logFile).ToList();

                // ogranicz do ostatnich maxLines - 1, zostaw miejsce na nowy wpis
                if (lines.Count >= maxLines)
                    lines = lines.Skip(lines.Count - (maxLines - 1)).ToList();
            }

            // dodaj nową linię
            lines.Add(logEntry);

            // zapisz z powrotem
            File.WriteAllLines(logFile, lines);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LogIdle] Błąd logowania: {ex.Message}");
        }
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fadeInTimer?.Stop();
            fadeInTimer?.Dispose();

            autoCloseTimer?.Stop();
            autoCloseTimer?.Dispose();

            scrollTimer?.Stop();
            scrollTimer?.Dispose();

            brightnessPollTimer?.Stop();
            brightnessPollTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
    public BrightnessForm(int currentBrightness)
    {

        Task.Run(() =>
        {
            isBatterySaverActive = BatterySaverChecker.IsBatterySaverActive();
            if (isBatterySaverActive)
                LogBrightness("🔋 Battery Saver aktywny – polling zablokowany.");
        });


        this.Deactivate += (_, __) =>
        {
            FadeOutThenClose();// 🚪 klik poza formą = bye
        };

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = System.Drawing.Color.FromArgb(228, 228, 228);

        DoubleBuffered = true;
        Size = new Size(360, 100);
        Opacity = 0;

        var label = new Label()
        {
            Text = $"Jasność ekranu",
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            ForeColor = System.Drawing.Color.FromArgb(20, 20, 20),
            Dock = DockStyle.Top,
            Height = 31,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 12, 4, 0)
        };

        slider = new CustomSlider()
        {
            Minimum = 0,
            Maximum = 100,
            Value = IdleTrayApp.Instance?.lastPolledBrightness is int b && b >= 0 && b <= 100 ? b : currentBrightness,
            Height = 30,
            Width = 226, // ustalona szerokość
            Location = new Point(10, 10), // odległość od lewej i od góry (X, Y)
            Anchor = AnchorStyles.Top | AnchorStyles.Left, // ewentualnie dodaj Right jeśli chcesz rozciąganie
            Margin = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(228, 228, 228)
        };


        int polled = IdleTrayApp.Instance?.lastPolledBrightness ?? -1;
        int dimLevel = IdleTrayApp.Instance?.dimBrightnessPercent ?? -1;

        if (polled == dimLevel)
        {
            LogBrightness($"⚠️ Pollowana jasność = {polled} równa dimBrightnessPercent – nie ustawiam slidera");
        }
        else if (polled >= 0 && polled <= 100)
        {
            slider.Value = polled;
            LogBrightness($"🎚️ Ustawiam slider na start: {slider.Value} (lastPolledBrightness = {polled})");
        }
        else
        {
            slider.Value = currentBrightness;
            LogBrightness($"🎚️ Ustawiam slider na fallback: {currentBrightness} (brak sensownej wartości z pollingu)");
        }

        //LogBrightness($"🎚️ Ustawiam slider na start: {slider.Value} (lastPolledBrightness = {IdleTrayApp.Instance?.lastPolledBrightness})");



        valueLabel = new Label()
        {
            Text = currentBrightness.ToString(),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = System.Drawing.Color.Black,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 10, 0)
        };




        slider.MouseDown += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {
                LogBrightness("🚫 Blokuję interakcję – DimForm aktywny (MouseDown)");
                return;
            }
            else if (e.Button == MouseButtons.Left)
            {
                mouseDownAt = DateTime.Now;
            }
        };


        /*
        slider.MouseUp += async (s, e) =>
        {
            if (e.Button == MouseButtons.Left && !isAdjusting)
            {
                isAdjusting = true;
                Cursor = Cursors.WaitCursor;

                int value = slider.Value;
                await IdleTrayApp.SetBrightnessAsync(value);

                var app = IdleTrayApp.Instance;
                if (app != null)
                {
                    app.SetKeyboardBacklightBasedOnBrightness(value);
                    app.lastKnownBrightness = value;
                }

                brightnessSetAt = DateTime.Now; // 🕒 Zablokuj polling na chwilę
                Cursor = Cursors.Default;
                isAdjusting = false;
            }

        };*/

        slider.MouseUp += async (s, e) =>
        {
            if (e.Button == MouseButtons.Left && valueChangedSinceLastApply)
            {
                valueChangedSinceLastApply = false;
                int value = slider.Value;
                await IdleTrayApp.SetBrightnessAsync(value);

                var app = IdleTrayApp.Instance;
                if (app != null)
                {
                    app.SetKeyboardBacklightBasedOnBrightness(value);
                    app.lastKnownBrightness = value;
                }

                brightnessSetAt = DateTime.Now;
                IdleTrayApp.Instance?.SetLastPolledBrightness(value);
            }
        };



        scrollTimer = new System.Windows.Forms.Timer { Interval = 400 };
        scrollTimer.Tick += async (s, e) =>
        {
            scrollTimer.Stop();
            if (scrollInProgress && valueChangedSinceLastApply)
            {
                scrollInProgress = false;
                valueChangedSinceLastApply = false;

                int value = slider.Value;
                await IdleTrayApp.SetBrightnessAsync(value);

                var app = IdleTrayApp.Instance;
                if (app != null)
                {
                    app.SetKeyboardBacklightBasedOnBrightness(value);
                    app.lastKnownBrightness = value;
                }

                brightnessSetAt = DateTime.Now;
                IdleTrayApp.Instance?.SetLastPolledBrightness(value);
            }
        };



        slider.MouseWheel += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {
                LogBrightness("🚫 Blokuję interakcję – DimForm aktywny (MouseWheel)");

                if (e is HandledMouseEventArgs handledEvent)
                    handledEvent.Handled = true;

                return;
            }

            scrollInProgress = true;
            scrollStartedAt = DateTime.Now;
            scrollTimer.Stop();
            scrollTimer.Start();
        };


        slider.ValueChanged += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {

                LogBrightness("⛔ Ignoruję ValueChanged – DimForm aktywny");
                return;
            }
            if (slider.InteractionLocked)
            {
                LogBrightness("⛔ Ignoruję ValueChanged – suwak zablokowany");
                return;
            }

            int val = slider.Value;
            int blocked = IdleTrayApp.Instance?.dimBrightnessPercent ?? -1;

            if (val == blocked)
            {
                int corrected = val;

                // Szukamy w górę
                int up = val + 1;
                while (up <= slider.Maximum && up == blocked) up++;

                // Szukamy w dół
                int down = val - 1;
                while (down >= slider.Minimum && down == blocked) down--;

                bool upValid = up <= slider.Maximum;
                bool downValid = down >= slider.Minimum;

                if (upValid && downValid)
                    corrected = Math.Abs(val - down) <= Math.Abs(val - up) ? down : up;
                else if (upValid)
                    corrected = up;
                else if (downValid)
                    corrected = down;
                else
                    return; // nie ma gdzie zeskoczyć – zostaw

                // zabezpieczenie przed nieprawidłową wartością
                if (corrected >= slider.Minimum && corrected <= slider.Maximum)
                    slider.Value = corrected;

                return;
            }

            if (!isUpdatingFromPolling)
                valueLabel.Text = val.ToString();

            valueChangedSinceLastApply = true;
            brightnessIcon.Image = GetBrightnessIcon(val);
            scrollInProgress = true;
            scrollStartedAt = DateTime.Now;
            scrollTimer.Stop();
            scrollTimer.Start();
        };




        slider.Enabled = true;
        var sliderPanel = new Panel
        {
            Height = 42,
            Dock = DockStyle.Top,
            Padding = new Padding(10, 12, 4, 0),
            BackColor = System.Drawing.Color.FromArgb(228, 228, 228),
            Margin = Padding.Empty
        };

        brightnessIcon = new PictureBox
        {
            Size = new Size(28, 28),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Left,
            Margin = new Padding(10, 0, 0, 0),
            Image = GetBrightnessIcon(currentBrightness)
        };


        this.Padding = new Padding(1); // miejsce na ramkę

        var mainPanel = new Panel
        {
            Dock = DockStyle.Top,

            BackColor = System.Drawing.Color.Transparent
        };
        mainPanel.Controls.Add(sliderPanel);
        mainPanel.Controls.Add(label);
        Controls.Clear();
        Controls.Add(mainPanel);





        //brightnessIcon.BorderStyle = BorderStyle.FixedSingle;//debug
        //brightnessIcon.BackColor = System.Drawing.Color.Red;//debug
        // 1) Tworzymy TableLayoutPanel na ikonę, suwak i label
        var table = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 4, 0, 0), // padingi ramki sloneczka z suwakiem - razem sie przesuwaja
            Margin = Padding.Empty,
            BackColor = System.Drawing.Color.Transparent
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // ikonka
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));     // suwak
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // label z procentem
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));            // wysokość 36px

        // 2) Ikona
        brightnessIcon.Size = new Size(24, 24);
        brightnessIcon.Margin = new Padding(0, 10, 6, 6); // paddingi ikony
        brightnessIcon.SizeMode = PictureBoxSizeMode.Zoom;

        // 3) Suwak w kontenerze
        var sliderContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 0),

            Margin = Padding.Empty

        };

        slider.Margin = Padding.Empty;
        //slider.Height = 2; // dostosuj, jeśli potrzeba
        sliderContainer.Controls.Add(slider);

        // 4) Label z procentem
        valueLabel.AutoSize = true;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Font = new Font("Segoe UI", 18f, FontStyle.Regular);
        valueLabel.Margin = new Padding(0, 7, 0, 0); // lekko w dół, odstęp od suwaka

        // 5) Dodajemy wszystko do tabeli
        table.Controls.Add(brightnessIcon, 0, 0);
        table.Controls.Add(sliderContainer, 1, 0);
        table.Controls.Add(valueLabel, 2, 0);

        // 6) Dodajemy do sliderPanel
        sliderPanel.Controls.Clear();
        sliderPanel.Controls.Add(table);
        sliderPanel.Size = new Size(280, 55);
        sliderPanel.Margin = Padding.Empty;

        //// 7) Dodajemy wszystkie kontrolki do formy
        //Controls.Add(sliderPanel);
        //Controls.Add(label); // label z tytułem np. "Jasność"

        // 8) Pozycjonujemy okno
        var workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width - 0, workingArea.Bottom - Height - 0);




        fadeInTimer = new System.Windows.Forms.Timer { Interval = 10 };
        fadeInTimer.Tick += (s, e) =>
        {

            if (this.IsDisposed || !this.IsHandleCreated)
            {
                fadeInTimer.Stop();
                return;
            }
            if (Opacity < 1)
                Opacity += 0.05;
            else
                fadeInTimer.Stop();
        };




        fadeInTimer.Start();
        // 👀 Timer sprawdzający brak aktywności myszy
        autoCloseTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        int idleSeconds = 0;

        autoCloseTimer.Tick += (s, e) =>
        {
            idleSeconds++;
            Point cursor = Cursor.Position;

            if (idleSeconds >= 40)
            {
                LogBrightness("Auto close: 40s minęło – zamykam z myszką na formie");
                autoCloseTimer.Stop();
                FadeOutThenClose();
                return;
            }

            if (!Bounds.Contains(cursor) && idleSeconds >= 4)
            {
                LogBrightness("Auto close: myszka poza formą przez 4s – zamykam");
                autoCloseTimer.Stop();
                FadeOutThenClose();
            }
        };
        autoCloseTimer.Start();

        brightnessPollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        brightnessPollTimer.Tick += async (s, e) =>
        {
            slider.InteractionLocked = IdleTrayApp.dimFormActive;
            if (isBatterySaverActive)
            {
                
                return;
            }
            if (IdleTrayApp.PreparingToDim)
            {
                LogBrightness("⛔ PreparingToDim aktywne – pomijam polling");
                return;
            }

            if (IdleTrayApp.dimFormActive)
            {
                LogBrightness("⛔ DimForm aktywny – pomijam polling jasności");
                return;
            }

            if (IdleTrayApp.Instance?.dimFormClosedAt is DateTime dt &&
                (DateTime.Now - dt).TotalSeconds < 1.0)
            {
                LogBrightness("⏳ Odczekuję 1s po zamknięciu DimForm – pomijam polling");
                return;
            }

            if (isAdjusting) return;

            if ((DateTime.Now - brightnessSetAt < TimeSpan.FromSeconds(1)) ||
                (DateTime.Now - mouseDownAt < TimeSpan.FromSeconds(1)) ||
                (DateTime.Now - scrollStartedAt < TimeSpan.FromSeconds(1)))
                return;

            isUpdatingFromPolling = true;

            LogBrightness($"🔄 Polluje jasność");
            int current = await IdleTrayApp.GetCurrentBrightnessAsync();
            IdleTrayApp.Instance?.SetLastPolledBrightness(current);

            if (slider.Value != current)
                slider.Value = current;

            valueLabel.Text = current.ToString();

            isUpdatingFromPolling = false;
        };

        brightnessPollTimer.Start();

        // ⛔ Zamknij formę po kliknięciu poza nią
        IdleTrayApp.MouseHook.Start((pt, btn) =>
        {
            if (!Bounds.Contains(pt))
            {
                FadeOutThenClose();
            }
        });
        slider.TabStop = false;
        this.ActiveControl = null;
        var mon = MonitorFromWindow(this.Handle, 2);
        if (GetDpiForMonitor(mon, 0, out uint dpiX, out uint dpiY) == 0)
        {
            float scale = dpiX / 96f;
            Debug.WriteLine($"🔆 BrightnessForm: DPI = {dpiX} → scale x{scale:0.00}");

            this.Scale(new SizeF(scale, scale));
        }
        var screen = Screen.FromHandle(this.Handle).WorkingArea;

        // forma miała być przyklejona do prawego dolnego rogu
        this.Left = screen.Right - this.Width;
        this.Top = screen.Bottom - this.Height;


    }

    private void FadeOutThenClose()
    {
        var fadeOut = new System.Windows.Forms.Timer { Interval = 10 };
        fadeOut.Tick += (s, e) =>
        {
            if (Opacity > 0)
            {
                Opacity -= 0.05;
            }
            else
            {
                fadeOut.Stop();
                fadeOut.Dispose();
                Close();
            }
        };
        fadeOut.Start();
    }
    public class FlatTrackBar : TrackBar
    {
        public FlatTrackBar()
        {
            SetStyle(ControlStyles.Selectable, false);
        }


        private const int WM_SETFOCUS = 0x0007;

        protected override bool ShowFocusCues => false;



        protected override void OnGotFocus(EventArgs e)
        {
            // Nie rób nic
        }

        protected override void OnEnter(EventArgs e)
        {
            // Nie rób nic
        }

        protected override void OnLeave(EventArgs e)
        {
            // Nie rób nic
        }


        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETFOCUS)
            {
                // Zamiast ustawiać fokus na TrackBar, przekaż go komuś innemu lub olej
                if (Parent != null && Parent.ContainsFocus)
                    Parent.SelectNextControl(this, true, true, true, true);

                return; // zablokuj
            }

            base.WndProc(ref m);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        // Nie rób tu nic innego, ale to wymusza poprawne narysowanie tła
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 200, 200), 1))
        {
            // Górna krawędź
            e.Graphics.DrawLine(pen, 0, 0, this.ClientSize.Width, 0);
            // Lewa krawędź
            e.Graphics.DrawLine(pen, 0, 0, 0, this.ClientSize.Height);
        }
    }



    private Image GetBrightnessIcon(int brightness)
    {
        string name;

        if (brightness <= 10)
            name = "0-10.png";
        else if (brightness <= 20)
            name = "11-20.png";
        else if (brightness <= 30)
            name = "21-30.png";
        else if (brightness <= 40)
            name = "31-40.png";
        else if (brightness <= 50)
            name = "41-50.png";
        else if (brightness <= 60)
            name = "51-60.png";
        else if (brightness <= 70)
            name = "61-70.png";
        else if (brightness <= 80)
            name = "71-80.png";
        else if (brightness <= 90)
            name = "81-90.png";
        else
            name = "91-100.png";

        var asm = typeof(BrightnessForm).Assembly;
        var resourceName = "DimScreenSaver.Resources.BrightnessIcons." + name;
        var stream = asm.GetManifestResourceStream(resourceName);

        if (stream != null)
            return Image.FromStream(stream);

        return null;
    }


    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST = 0x00000008;

            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }


}
