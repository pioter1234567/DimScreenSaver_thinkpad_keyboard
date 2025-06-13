using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DimScreenSaver
{

    public partial class FormOptions : Form
    {
        private KeyboardController keyboard;
        private Timer statusUpdateTimer;
        private bool suppressApplySettings = true;
        private int lastForceLevel = -1;
        private int[] lastCustomMap = null;
        private bool isProgrammaticForceLevelUpdate = false;


        public FormOptions()
        {
            InitializeComponent();
            this.AutoScaleMode = AutoScaleMode.None;
            if (!IsInDesignMode())
            {
                InitRuntimeLogic();

            }
        }

        private async Task UpdateStatusLabels()
        {
            try
            {
                // Jasność ekranu
                int brightness = await IdleTrayApp.GetCurrentBrightnessAsync();
                lblCurrentBrightness.Text = $"{brightness}%";
            }
            catch
            {
                lblCurrentBrightness.Text = $"(błąd)";
            }

            try
            {
                // Jasność podświetlenia klawiatury
                var (result, level) = keyboard?.Get() ?? (1u, -1);
                if (result == 0 && level >= 0 && level <= 2)
                    lblKeyboardBacklight.Text = $"{level}";
                else
                    lblKeyboardBacklight.Text = $"(błąd)";
            }
            catch
            {
                lblKeyboardBacklight.Text = $"(błąd)";
            }
        }



        private void AddButtonPressEffect(Button btn)
        {
            Point? originalLocation = null;

            btn.MouseDown += (s, e) =>
            {
                originalLocation = btn.Location;
                btn.Location = new Point(btn.Location.X + 1, btn.Location.Y + 1);
            };

            btn.MouseUp += (s, e) =>
            {
                if (originalLocation != null)
                    btn.Location = originalLocation.Value;
            };

            btn.LostFocus += (s, e) =>
            {
                if (originalLocation != null)
                    btn.Location = originalLocation.Value;
            };
        }

        private void BtnSet0_Click(object sender, EventArgs e)
        {
            IdleTrayApp.Instance?.keyboard?.Set(0);
        }

        private void BtnSet1_Click(object sender, EventArgs e)
        {
            IdleTrayApp.Instance?.keyboard?.Set(1);
        }

        private void BtnSet2_Click(object sender, EventArgs e)
        {
            IdleTrayApp.Instance?.keyboard?.Set(2);
        }
        private async void FormOptions_Load(object sender, EventArgs e)
        {
            btnSet0.Image = LoadEmbeddedImage("level0.png");
            btnSet1.Image = LoadEmbeddedImage("level1.png");
            btnSet2.Image = LoadEmbeddedImage("level2.png");





            if (!IsInDesignMode())
                await UpdateStatusLabels();



        }
        private Image LoadEmbeddedImage(string filename)
        {
            var asm = typeof(FormOptions).Assembly;
            var stream = asm.GetManifestResourceStream($"DimScreenSaver.Resources.{filename}");
            return stream != null ? Image.FromStream(stream) : null;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Zapis ustawień trybu sterowania
            Properties.Settings.Default.ForceLevelModeEnabled = rbForceLevel.Checked;
            Properties.Settings.Default.ForcedKeyboardLevel = cmbForceLevel.SelectedIndex;

            // Zapis mapy bloków z brightnessEditor
            var map = brightnessEditor.GetCurrentLevelMap();
            Properties.Settings.Default.KeyboardLevelMap = string.Join(",", map);

            // Przekazanie do głównego programu i ustawienie podswietlenia zgodnie z mapą
            IdleTrayApp.Instance.brightnessToLevelMap = GetMappedRanges();
            IdleTrayApp.Instance.SetKeyboardBacklightBasedOnBrightness(IdleTrayApp.Instance.lastKnownBrightness);

            // Finalizacja
            Properties.Settings.Default.Save();
            BalloonForm.ShowBalloon("Zapisano", "Ustawienia klawiatury zostały zapisane", 4000, BalloonForm.BalloonStyle.NO_ICONS);
            this.Close();
        }

        private void ApplySettings()
        {
            // Blokowanie ApplySettings przy uruchamianiu
            if (suppressApplySettings)
            {
                Debug.WriteLine("🛑 ApplySettings zablokowane przy inicjalizacji");
                return;
            }

            // Zapis ustawień trybu sterowania
            Properties.Settings.Default.ForceLevelModeEnabled = rbForceLevel.Checked;
            Properties.Settings.Default.ForcedKeyboardLevel = cmbForceLevel.SelectedIndex;

            // Zapis mapy bloków z brightnessEditor
            var map = brightnessEditor.GetCurrentLevelMap();
            Properties.Settings.Default.KeyboardLevelMap = string.Join(",", map);

            // Przekazanie do głównego programu i ustawienie podswietlenia zgodnie z mapą
            IdleTrayApp.Instance.brightnessToLevelMap = GetMappedRanges();
            IdleTrayApp.Instance.SetKeyboardBacklightBasedOnBrightness(IdleTrayApp.Instance.lastKnownBrightness);

            // Finalizacja
            Properties.Settings.Default.Save();


        }

        private List<(int min, int max, int level)> GetMappedRanges()
        {
            var result = new List<(int min, int max, int level)>();
            var values = brightnessEditor.GetCurrentLevelMap();

            int start = 0;
            int current = values[0];

            for (int i = 1; i <= 10; i++)
            {
                if (i == 10 || values[i] != current)
                {
                    int min = start * 10;
                    int max = i * 10 - 1;
                    if (i == 10) max = 100;

                    result.Add((min, max, current));
                    if (i < 10)
                    {
                        current = values[i];
                        start = i;
                    }
                }
            }

            return result;
        }


        public static bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }
        private void InitRuntimeLogic()
        {
            InitInactivityTimeout();

            // Obsługa kliknięcia w edytor jasności
            brightnessEditor.LevelClicked += (s, e) =>
            {
                if (rbForceLevel.Checked)
                {
                    // Zapisanie mapy przed przeskoczeniem na tryb Use Map
                    lastCustomMap = brightnessEditor.GetCurrentLevelMap().ToArray();

                    rbUseBrightnessMap.Checked = true;
                }

                ApplySettings();
            };

            // Timer do statusów
            statusUpdateTimer = new Timer
            {
                Interval = 750
            };
            statusUpdateTimer.Tick += async (s, e) => await UpdateStatusLabels();
            statusUpdateTimer.Start();

            // Podpięcie przycisków z efektem
            AddButtonPressEffect(btnSet0);
            AddButtonPressEffect(btnSet1);
            AddButtonPressEffect(btnSet2);

            // Inicjalizacja kontrolera klawiatury
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keyboard_core.dll");
            keyboard = new KeyboardController(dllPath);

            // Eventy – odpięcie, żeby nie odpalały ApplySettings() podczas ładowania
            rbForceLevel.CheckedChanged -= RbForceLevel_CheckedChanged;
            cmbForceLevel.SelectedIndexChanged -= CmbForceLevel_SelectedIndexChanged;

            // Wybrany tryb (force/map)
            bool force = Properties.Settings.Default.ForceLevelModeEnabled;
            rbForceLevel.Checked = force;
            rbUseBrightnessMap.Checked = !force;

            // Indeks comboboxa
            int savedIndex = Properties.Settings.Default.ForcedKeyboardLevel;
            cmbForceLevel.SelectedIndex = savedIndex >= 0 && savedIndex <= 2 ? savedIndex : 2;

            // Poziomy bloków
            string raw = Properties.Settings.Default.KeyboardLevelMap;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var split = raw.Split(',');
                for (int i = 0; i < 10 && i < split.Length; i++)
                {
                    if (int.TryParse(split[i], out int val))
                        brightnessEditor.SetLevel(i, Math.Max(0, Math.Min(2, val)));
                }
            }

            // Eventy - zapięcie
            cmbForceLevel.SelectedIndexChanged += CmbForceLevel_SelectedIndexChanged;
            rbForceLevel.CheckedChanged += RbForceLevel_CheckedChanged;


            cmbForceLevel.Visible = rbForceLevel.Checked;
            UpdateBrightnessEditorForForcedLevel();
            lastForceLevel = cmbForceLevel.SelectedIndex;
            suppressApplySettings = false;
        }


        private Timer inactivityTimer;
        private void CmbForceLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            isProgrammaticForceLevelUpdate = true;
            UpdateBrightnessEditorForForcedLevel();
            isProgrammaticForceLevelUpdate = false;
            ApplySettings();
        }
        private void ResetInactivityTimer()
        {
            inactivityTimer?.Stop();
            inactivityTimer?.Start();
        }

        private void InitInactivityTimeout()
        {
            inactivityTimer = new Timer {Interval = 3 * 60 * 1000}; // 3 minuty
            inactivityTimer.Tick += (s, e) =>
            {
                {
                    if (chkAutoClose.Checked)
                    {
                        inactivityTimer.Stop();
                        Close();
                    }
                }
                ;
            };
            inactivityTimer.Start();

            this.MouseMove += (_, __) => ResetInactivityTimer();
            this.KeyDown += (_, __) => ResetInactivityTimer();
            chkAutoClose.CheckedChanged += (s, e) => ResetInactivityTimer();
        }

        private void GroupBoxTest_Enter(object sender, EventArgs e)
        {

        }
        private void RbForceLevel_CheckedChanged(object sender, EventArgs e)
        {
            cmbForceLevel.Visible = rbForceLevel.Checked;
            UpdateBrightnessEditorForForcedLevel();
            ApplySettings();
        }
        private void RbUseBrightnessMap_CheckedChanged(object sender, EventArgs e)
        {
            if (rbUseBrightnessMap.Checked && lastCustomMap != null)
            {
                for (int i = 0; i < lastCustomMap.Length; i++)
                    brightnessEditor.SetLevel(i, lastCustomMap[i]);

                lastCustomMap = null; // reset
            }

            ApplySettings();
        }

        private void Button1_Click(object sender, EventArgs e)
        {

            this.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ApplySettings();
                statusUpdateTimer?.Stop();
                statusUpdateTimer?.Dispose();
                Debug.WriteLine("🧹 Dispose() został wywołany.");
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateBrightnessEditorForForcedLevel()
        {
            if (!rbForceLevel.Checked) return;

            // Zapisz tylko jeśli nie przyszło z comboboxa
            if (!isProgrammaticForceLevelUpdate)
            {
                lastCustomMap = brightnessEditor.GetCurrentLevelMap().ToArray();
            }

            int selected = cmbForceLevel.SelectedIndex;
            if (selected < 0 || selected > 2) selected = 2;

            for (int i = 0; i < 10; i++)
                brightnessEditor.SetLevel(i, selected);
        }

        private void CenterControlInContainer(Control control, Control container, bool ch = true, bool cv = false)
        {
            if (ch)
                control.Left = (container.ClientSize.Width - control.Width) / 2;

            if (cv)
                control.Top = (container.ClientSize.Height - control.Height) / 2;
        }
        private void DistributeControlsHorizontally(Control[] controls, Control container, int margin = 10, bool centerVertically = false)
        {
            if (controls == null || controls.Length == 0) return;

            int totalWidth = controls.Sum(c => c.Width);
            int availableWidth = container.ClientSize.Width - 2 * margin;

            if (controls.Length > 1)
            {
                int spacing = (availableWidth - totalWidth) / (controls.Length - 1);
                int currentLeft = margin;

                foreach (var ctrl in controls)
                {
                    ctrl.Left = currentLeft;

                    if (centerVertically)
                        ctrl.Top = (container.ClientSize.Height - ctrl.Height) / 2;

                    currentLeft += ctrl.Width + spacing;
                }
            }
            else
            {
                // tylko jeden element – wyśrodkuj
                controls[0].Left = (container.ClientSize.Width - controls[0].Width) / 2;

                if (centerVertically)
                    controls[0].Top = (container.ClientSize.Height - controls[0].Height) / 2;
            }
        }


        private void BtnResetDefaults_Click(object sender, EventArgs e)
        {
            rbUseBrightnessMap.Checked = true;


            int[] def = new int[] { 1, 1, 1, 2, 2, 2, 0, 0, 0, 0 };
            for (int i = 0; i < def.Length; i++)
            {
                brightnessEditor.SetLevel(i, def[i]);
            }
            ApplySettings();
        }




      

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://coff.ee/dimscreensaver");
        }

        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);



        protected async override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var screen = Screen.FromHandle(this.Handle);
            var mon = MonitorFromPoint(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2);
            GetDpiForMonitor(mon, 0, out uint dpiX, out _);

            float widthScale;
            float heightScale;

            if (dpiX >= 190) // ~200% DPI
            {
                widthScale = 1.4f;
                heightScale = 1.2f;
            }
            else if (dpiX >= 165) // ~175% DPI
            {
                widthScale = 1.3f;
                heightScale = 1.2f;
            }
            else if (dpiX >= 120) // ~125% DPI
            {
                widthScale = 1.1f;
                heightScale = 1.05f;
            }
            else
            {
                widthScale = 1.0f;
                heightScale = 1.0f;
            }

            if (Math.Abs(widthScale - 1f) > 0.01f || Math.Abs(heightScale - 1f) > 0.01f)
            {
                Debug.WriteLine($"🖥️ DPI = {dpiX} → skaluję tylko rozmiary: width x{widthScale:0.00}, height x{heightScale:0.00}");

                // Skalowanie tylko rozmiaru formy (bez zmiany pozycji)
                this.ClientSize = new Size((int)(400 * widthScale), (int)(315 * heightScale));

                // Skalowanie tylko Wymiarów (bez zmiany pozycji)
                groupBoxCurrentBrightness.Size = new Size(
                    (int)(groupBoxCurrentBrightness.Width * widthScale),
                    (int)(groupBoxCurrentBrightness.Height * heightScale));

                groupBoxBacklight.Size = new Size(
                    (int)(groupBoxBacklight.Width * widthScale),
                    (int)(groupBoxBacklight.Height * heightScale));

                groupBoxTest.Size = new Size(
                    (int)(groupBoxTest.Width * widthScale),
                    (int)(groupBoxTest.Height * heightScale));

                groupBoxMode.Size = new Size(
                    (int)(groupBoxMode.Width * widthScale),
                    (int)(groupBoxMode.Height * heightScale));

                panel1.Size = new Size(
                    (int)(panel1.Width * widthScale),
                    (int)(panel1.Height * heightScale));

                // Reszta – np. fonty, marginesy
                this.Scale(new SizeF(widthScale, heightScale));
            }


            await UpdateStatusLabels();

            CenterControlInContainer(lblCurrentBrightness, groupBoxCurrentBrightness);
            CenterControlInContainer(lblKeyboardBacklight, groupBoxBacklight);
            CenterControlInContainer(panel_z_regulacja, groupBoxMode);
            CenterControlInContainer(panel1, this);
            CenterControlInContainer(chkAutoClose, panel1);
            DistributeControlsHorizontally(new[] { btnSet0, btnSet1, btnSet2 }, groupBoxTest, 10, centerVertically: true);
            DistributeControlsHorizontally(
            new[] { groupBoxCurrentBrightness, groupBoxBacklight, groupBoxTest },
            this,
            10,
            centerVertically: false
            );


        }


    }

    public class TransparentOverlayPanel : Panel
    {

        public TransparentOverlayPanel()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Brush b = new SolidBrush(Color.FromArgb(100, SystemColors.Control)))
            {
                e.Graphics.FillRectangle(b, this.ClientRectangle);
            }
            base.OnPaint(e);
        }
    }




}
