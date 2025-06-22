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
    public readonly CustomSlider slider;
    public readonly Label valueLabel;
    private readonly bool isAdjusting = false;
    private bool isUpdatingFromPolling = false;
    private DateTime brightnessSetAt = DateTime.MinValue;
    private DateTime mouseDownAt = DateTime.MinValue;
    public bool scrollInProgress = false;
    private bool valueChangedSinceLastApply = false;
    private DateTime scrollStartedAt = DateTime.MinValue;
    private bool isBatterySaverActive = false;
    private readonly PictureBox brightnessIcon;
    private readonly PictureBox batterySaverIcon;
    public bool userInitiatedChange = false;
    private int idleSecondsBrightnessForm = 0;
    private DateTime lastUserInteraction;
    private int userSetBrightness = -1;
    private System.Windows.Forms.Timer postUserChangeTimer;

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    private static void Log(string msg) => AppLogger.Log("BrightnessForm", msg);
    private int lastSentValue = -1;
    private System.Windows.Forms.Timer dragTimer;





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

            dragTimer?.Stop();
            dragTimer?.Dispose();

            brightnessPollTimer?.Stop();
            brightnessPollTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
    
    public BrightnessForm(int currentBrightness)
    {

       /* Task.Run(() =>  // ZASTĄPIONE HOOKIEM WMI
        {
            isBatterySaverActive = BatterySaverChecker.IsBatterySaverActive();
            if (isBatterySaverActive)
                Log("🔋 Battery Saver aktywny – polling zablokowany.");
        });
       */

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
            Value = IdleTrayApp.Instance?.lastKnownBrightness is int b && b >= 0 && b <= 100 ? b : currentBrightness,
            Height = 30,
            Width = 226, // ustalona szerokość
            Location = new Point(10, 10), // odległość od lewej i od góry (X, Y)
            Anchor = AnchorStyles.Top | AnchorStyles.Left, // ewentualnie dodaj Right jeśli chcesz rozciąganie
            Margin = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(228, 228, 228)
        };





        slider.MouseDown += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {
                Log("🚫 Blokuję interakcję – DimForm aktywny (MouseDown)");
                return;
            }
            else if (e.Button == MouseButtons.Left)
            {
                userInitiatedChange = true;
                mouseDownAt = DateTime.Now;
                dragTimer.Start(); // ⬅️ startujemy timer
            }
        };



        slider.MouseUp += async (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                dragTimer.Stop(); // zatrzymujemy timer

                if (userInitiatedChange && slider.Value != lastSentValue)
                {
                    int value = slider.Value;
                    lastSentValue = value;

                    await IdleTrayApp.SetBrightnessAsync(value);

                    var app = IdleTrayApp.Instance;
                    if (app != null)
                    {
                        app.SetKeyboardBacklightBasedOnBrightnessForce(value, "BrightnessForm.slider.MouseUp += async (s, e) =>");
                        app.lastKnownBrightness = value;
                        app.SetLastKnownBrightness(value);
                    }

                    brightnessSetAt = DateTime.Now;
                    valueChangedSinceLastApply = false;
                }


                _ = Task.Delay(500).ContinueWith(_ =>
                {
                    userInitiatedChange = false;
                    //ScheduleBrightnessDropCheck();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        };







        dragTimer = new System.Windows.Forms.Timer { Interval = 100 }; // co 150ms sprawdzanie
        dragTimer.Tick += async (s, e) =>
        {
            if (IdleTrayApp.dimFormActive || !userInitiatedChange)
                return;

            int current = slider.Value;
            if (current != lastSentValue)
            {
                lastSentValue = current;
                await IdleTrayApp.SetBrightnessAsync(current);
                var app = IdleTrayApp.Instance;
                if (app != null)
                {
                    app.SetKeyboardBacklightBasedOnBrightnessForce(current, "BrightnessForm.dragTimer.Tick += async (s, e) =>");
                    app.lastKnownBrightness = current;
                }

                brightnessSetAt = DateTime.Now;
                IdleTrayApp.Instance?.SetLastKnownBrightness(current);
            }
        };

        scrollTimer = new System.Windows.Forms.Timer { Interval = 600 };
        scrollTimer.Tick += async (s, e) =>
        {
            scrollTimer.Stop();
            if (scrollInProgress && valueChangedSinceLastApply)
            {
                scrollInProgress = false;
                valueChangedSinceLastApply = false;
                _ = Task.Delay(500).ContinueWith(_ =>
                {
                    userInitiatedChange = false;
                    //ScheduleBrightnessDropCheck();
                }, TaskScheduler.FromCurrentSynchronizationContext());
                int value = slider.Value;
                await IdleTrayApp.SetBrightnessAsync(value);

                var app = IdleTrayApp.Instance;
                if (app != null)
                {
                    app.SetKeyboardBacklightBasedOnBrightnessForce(value, "BrightnessForm.scrollTimer.Tick += async (s, e) =>");
                    app.lastKnownBrightness = value;
                }
            
                brightnessSetAt = DateTime.Now;
                IdleTrayApp.Instance?.SetLastKnownBrightness(value);
            }
        };



        slider.MouseWheel += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {
                Log("🚫 Blokuję interakcję – DimForm aktywny (MouseWheel)");

                if (e is HandledMouseEventArgs handledEvent)
                    handledEvent.Handled = true;

                return;
            }


            userInitiatedChange = true;
            scrollInProgress = true;
            scrollStartedAt = DateTime.Now;
            scrollTimer.Stop();
            scrollTimer.Start();
        };


        slider.ValueChanged += (s, e) =>
        {
            if (IdleTrayApp.dimFormActive)
            {
                Log("⛔ Ignoruję ValueChanged – DimForm aktywny");
                return;
            }

            if (slider.InteractionLocked)
            {
                Log("⛔ Ignoruję ValueChanged – suwak zablokowany");
                return;
            }

            if (!userInitiatedChange)
            {
                // tylko update UI (labela, ikony itp.)
                valueLabel.Text = slider.Value.ToString();
                brightnessIcon.Image = GetBrightnessIcon(slider.Value);
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
                    Log("🐔🐔🐔pseudonim KURKA 🐔🐔🐔🐔🐔🐔🐔🐔🐔");
                    slider.Value = corrected;

                return;
            }
            valueLabel.Text = val.ToString();
            brightnessIcon.Image = GetBrightnessIcon(val);
            idleSecondsBrightnessForm = 0;
            lastUserInteraction = DateTime.Now;



            if (!dragTimer.Enabled)
            {                  
            valueChangedSinceLastApply = true; 
            scrollInProgress = true;
            scrollStartedAt = DateTime.Now;
            scrollTimer.Stop();
            scrollTimer.Start();
            }
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

        valueLabel = new Label()
        {
            Text = currentBrightness.ToString(),
            ForeColor = System.Drawing.Color.Black,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Height = 22,
            AutoSize = true,
            Visible = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 18f, FontStyle.Regular),
            Margin = new Padding(0, 7, 10, 0),
            Padding = new Padding(0, 0, 0, 0)
        };

        //ikonka
        batterySaverIcon = new PictureBox
        {
            Size = new Size(36, 36),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(this.Width - 54, 10), // X: przy prawej krawędzi, Y: wysokość tytułu
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false,
            BackColor = Color.Transparent,
            Image = GetBatterySaverIcon("BS.png")
        };



        /* // Kontener nakładający oba na siebie
         var valueContainer = new Panel
         {
             Size = new Size(50, 60), // lub AutoSize = true jeśli preferujesz
             Margin = new Padding(0, 7, 0, 0),
             Padding = Padding.Empty
         };*/

        // Dodaj do kontenera
        //valueContainer.Controls.Add(batterySaverIcon);
        //valueContainer.Controls.Add(valueLabel);
        valueLabel.BringToFront();


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

        this.Controls.Add(batterySaverIcon);
        batterySaverIcon.BringToFront();


        batterySaverIcon.Visible = BatterySaverChecker.IsBatterySaverActive();

        /*
        if (BatterySaverChecker.IsBatterySaverActive())
        {
            valueLabel.Text = " ";
            batterySaverIcon.Visible = true;
            valueLabel.Visible = false;
            Log("🔋 Battery Saver aktywny – nie ustawiam slidera, pokazuję puste");
        }
        else
        {*/
        int knownBrighthtness = IdleTrayApp.Instance?.lastKnownBrightness ?? -1;
            int dimLevel = IdleTrayApp.Instance?.dimBrightnessPercent ?? -1;

            if (knownBrighthtness == dimLevel)
            {
                Log($"⚠️ knownBrightness = {knownBrighthtness} równa dimBrightnessPercent {dimLevel} – nie ustawiam slidera");
            }
            else if (knownBrighthtness >= 0 && knownBrighthtness <= 100)
            {
            Log("🐸🐸🐸pseudonim ŻABKA🐸🐸🐸🐸🐸🐸🐸 ");
            slider.Value = knownBrighthtness;
                valueLabel.Text = knownBrighthtness.ToString();
                Log($"🎚️ Ustawiam slider na start: {slider.Value} (lastKnownBrightness = {knownBrighthtness})");
            }
            else
            {
            Log("🐷🐷🐷pseudonim ŚWINKA🐷🐷🐷🐷🐷🐷🐷🐷 ");
            slider.Value = currentBrightness;
                valueLabel.Text = currentBrightness.ToString();
                Log($"🎚️ Ustawiam slider na fallback: {currentBrightness} (brak sensownej wartości lastKnownBrightness )");
            }
       // }





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
    

        autoCloseTimer.Tick += (s, e) =>
        {
            if (userInitiatedChange)
            {
                idleSecondsBrightnessForm = 0; // 👈 resetuj licznik – user nadal przesuwa
                return;          // 👈 nie zamykaj formy
            }

            Point cursor = Cursor.Position;

            if (Bounds.Contains(cursor))
            {
                idleSecondsBrightnessForm = 0; // reset jak myszka jest na formie
            }
            else
            {
                idleSecondsBrightnessForm++;
            }

            if (idleSecondsBrightnessForm >= 4)
            {
                Log("Auto close: myszka poza formą przez 4s – zamykam");
                autoCloseTimer.Stop();
                FadeOutThenClose();
                return;
            }

            if ((DateTime.Now - lastUserInteraction).TotalSeconds >= 40)
            {
                Log("Auto close: 40s minęło – zamykam z myszką na formie");
                autoCloseTimer.Stop();
                FadeOutThenClose();
            }
        };
        autoCloseTimer.Start();
        lastUserInteraction = DateTime.Now;
        /* ZASTĄPIONE PRZEZ HOOK WMI
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
                Log("⛔ PreparingToDim aktywne – pomijam polling");
                return;
            }

            if (IdleTrayApp.dimFormActive)
            {
                Log("⛔ DimForm aktywny – pomijam polling jasności");
                return;
            }

            if (IdleTrayApp.Instance?.dimFormClosedAt is DateTime dt &&
                (DateTime.Now - dt).TotalSeconds < 1.0)
            {
                Log("⏳ Odczekuję 1s po zamknięciu DimForm – pomijam polling");
                return;
            }

            if (isAdjusting) return;

            if ((DateTime.Now - brightnessSetAt < TimeSpan.FromSeconds(1)) ||
                (DateTime.Now - mouseDownAt < TimeSpan.FromSeconds(1)) ||
                (DateTime.Now - scrollStartedAt < TimeSpan.FromSeconds(1)))
                return;

            isUpdatingFromPolling = true;

            Log($"🔄 Polluje jasność");
            int current = await IdleTrayApp.GetCurrentBrightnessAsync();
            IdleTrayApp.Instance?.SetLastKnownBrightness(current);

            if (slider.Value != current)
                slider.Value = current;

            valueLabel.Text = current.ToString();

            isUpdatingFromPolling = false;
        };

        brightnessPollTimer.Start();
        */

        // ⛔ Zamknij formę po kliknięciu poza nią + obsługa MouseUp
        IdleTrayApp.MouseHook.Start((pt, btn, isDown) =>
        {
            if (!Bounds.Contains(pt))
            {
                FadeOutThenClose();
            }

            if (!isDown && btn == MouseButtons.Left && userInitiatedChange)
            {
                Log("🖱️ Global MouseUp – resetuję userInitiatedChange i zatrzymuję dragTimer");
                _ = Task.Delay(500).ContinueWith(_ =>
                {
                    userInitiatedChange = false;
                    //ScheduleBrightnessDropCheck();
                }, TaskScheduler.FromCurrentSynchronizationContext());
                dragTimer.Stop();
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

        lastSentValue = slider.Value; // startowa wartość


    }
    private async void FlashBatterySaverIcon(int blinkCount = 3, int delayMs = 200)
    {
        if (batterySaverIcon == null) return;

        for (int i = 0; i < blinkCount; i++)
        {
            batterySaverIcon.Visible = false;
            await Task.Delay(delayMs);
            batterySaverIcon.Visible = true;
            await Task.Delay(delayMs);
        }
    }

    public async Task AnimateSliderTo(int targetValue, int stepDelay = 1)
    {
        int current = slider.Value;
        Log("jade jade");
        if (current == targetValue)
            return;

        int direction = targetValue > current ? 1 : -5;

        while (slider.Value != targetValue)
        {
            int next = slider.Value + direction;

            if (direction > 0)
                slider.Value = Math.Min(next, targetValue);
            else
                slider.Value = Math.Max(next, targetValue);

            // ręczny update labelki i ikonki (bo ValueChanged ignoruje przy !userInitiatedChange)
            //valueLabel.Text = slider.Value.ToString();
            //brightnessIcon.Image = GetBrightnessIcon(slider.Value);

            await Task.Delay(stepDelay);
        }
    }

    public async void PulseBatterySaverIcon(int pulseCount = 3, int delayMs = 30, float scaleUp = 1.2f, float scaleDown = 0.8f)
    {
        if (batterySaverIcon == null) return;

        Size originalSize = batterySaverIcon.Size;
        Point originalLocation = batterySaverIcon.Location;

        for (int i = 0; i < pulseCount; i++)
        {
            batterySaverIcon.Visible = false;
            await Task.Delay(100);
            batterySaverIcon.Visible = true;

            // Pomniejsz
            ResizeBatteryIcon(scaleDown, originalSize, originalLocation);
            await Task.Delay(delayMs);

            // Powrót
            batterySaverIcon.Size = originalSize;
            batterySaverIcon.Location = originalLocation;
            await Task.Delay(delayMs);



            // Powiększ
            ResizeBatteryIcon(scaleUp, originalSize, originalLocation);
            await Task.Delay(delayMs);

            // Powrót
            batterySaverIcon.Size = originalSize;
            batterySaverIcon.Location = originalLocation;
            await Task.Delay(delayMs);

        }
    }

    private void ResizeBatteryIcon(float scale, Size baseSize, Point baseLocation)
    {
        int newWidth = (int)(baseSize.Width * scale);
        int newHeight = (int)(baseSize.Height * scale);
        batterySaverIcon.Size = new Size(newWidth, newHeight);

        batterySaverIcon.Location = new Point(
            baseLocation.X - (newWidth - baseSize.Width) / 2,
            baseLocation.Y - (newHeight - baseSize.Height) / 2
        );
    }



    private void ScheduleBrightnessDropCheck()
    {
        int valueBefore = slider.Value;

        _ = Task.Delay(1000).ContinueWith(_ =>
        {
            if (!userInitiatedChange && slider.Value < valueBefore)
            {
                Log("🔻 Jasność spadła po zmianie użytkownika – battery saver?");
                PulseBatterySaverIcon();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
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


    private Image GetBatterySaverIcon(string name)
    {
        var asm = typeof(BrightnessForm).Assembly;
        var resourceName = "DimScreenSaver.Resources.BrightnessIcons." + name;
        var stream = asm.GetManifestResourceStream(resourceName);

        if (stream != null)
        {
            try
            {
                return Image.FromStream(stream);
            }
            finally
            {
                stream.Dispose();
            }
        }

        return null;
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
        {
            try
            {
                return Image.FromStream(stream);
            }
            finally
            {
                stream.Dispose();
            }
        }

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
