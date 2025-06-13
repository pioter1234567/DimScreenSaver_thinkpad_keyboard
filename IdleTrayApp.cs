
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;




namespace DimScreenSaver
{


    public class IdleTrayApp : ApplicationContext
    {

              
        // 1. 🔄 Jasność / poziomy podświetlenia
        public int dimBrightnessPercent = 0;
        public int lastKnownBrightness = -1;
        public int lastPolledBrightness = -1;
        private int lastBacklightLevel = -1;
        private bool keyboardAutoEnabled = true;
        public static bool PreparingToDim = false;
        public List<(int min, int max, int level)> brightnessToLevelMap = new List<(int min, int max, int level)>();
        public static System.Windows.Forms.Timer idleCheckTimerPublic;
        public static System.Windows.Forms.Timer idleCheckTimer;
        private ToolStripMenuItem brightnessLevelMenu;
        private ToolStripMenuItem keyboardAutoToggleItem;
        public KeyboardController keyboard;

        // 2. 💤 Bezczynność, wykrywanie aktywności
        public static int idleSeconds = 0;
        public static int lastIdleTime = -1;
        public static DateTime? lastIdleTickTime = null;
        public DateTime? dimFormClosedAt = null;
        public static DateTime? lastSkippedDimNotificationTime = null;
        private System.Threading.Timer idleWatchdogTimer;
        public static bool WaitForUserActivity = false;
        private bool wakeOnAudio = true;
        private System.Threading.Timer wakeupTimer;
        private int wakeupIntervalMinutes = -1;
        public static bool dimFormActive = false;
        private static bool dimFormIsOpen = false;
        private bool isTemporarilyDisabled = false;
        private int idleThresholdRuntime = 120;
        private int screenOffAfterSecondsRuntime = -1;
        private int idleThresholdConfig = 120;
        private int screenOffAfterSecondsConfig = -1;
        private System.Threading.Timer javaWatchdogTimer;
        public static bool GlobalScreenOff = false;


        // 3. 📺 Formy
        public static FormVideoPlayer CurrentFormVideoPlayer = null;
        public static FormWakeup CurrentFormWakeup = null;
        private FormOptions formOptions;
        public static bool FormWasClosed = false;

        // 4. 🧠 Synchronizacja, kontrolery, obserwatorzy
        public static SynchronizationContext UISyncContext;
        private JavaDialogWatcher javaWatcher;
        private System.Threading.Timer javaFollowUpTimer;
        private PowerBroadcastWatcher powerWatcher;
        public bool monitorJavaDialog = true;
        private bool isWatchdogRestarting = false;
        private bool isTickRunning = false;
        private static Queue<DateTime> recentTicks = new Queue<DateTime>();
        private static object tickLock = new object();
        private static bool isPopupResetInProgress = false;

        // 5. 📛 Ikony i menu trayowe
        private NotifyIcon trayIcon;
        private Icon iconEnabled;
        private Icon iconDisabled;
        private Icon iconOnlyOff;
        private ToolStripMenuItem screenOffMenu;
        private ToolStripMenuItem wakeupMenu;
        private ToolStripMenuItem audioWakeItem;
        private ToolStripMenuItem javaMonitorMenuItem;
        private ToolStripMenuItem timeoutMenu;
        private ToolStripMenuItem disableItem;
        private ToolStripMenuItem exitItem;
        private ToolStripMenuItem paneloMenu;
        private ToolStripMenuItem paneloErrorNotifyItem;
        public bool paneloErrorNotifyEnabled = true;

        // 6. 🐭 Mouse event API
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        // 7. 🛠️ Pozostałe / narzędziowe
        private Control guiInvoker = new Control();
        private static string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");
        private string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DimScreenSaver", "settings.cfg");
        public static IdleTrayApp Instance { get; private set; }



          //********************************//
         // LOGI    
        //********************************//

        private static void LogIdle(string message)
        {
            string logFile = logPath;
            string logEntry = $"[IdleTray] {DateTime.Now:HH:mm:ss} {message}";

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



          //********************************//
         // KONSTRUKTOR I INICJALIZACJA    
        //********************************//

        public IdleTrayApp() //konstruktor
        {
            Instance = this;
            powerWatcher = new PowerBroadcastWatcher();
            LogIdle("Program uruchomiony");
            guiInvoker.CreateControl();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));      

            // 🎨 Ikony
            iconEnabled = LoadEmbeddedIcon("DimScreenSaver.dim.ico");
            iconDisabled = LoadEmbeddedIcon("DimScreenSaver.off.ico");
            iconOnlyOff = LoadEmbeddedIcon("DimScreenSaver.onlyoff.ico");

            // 📋 Menu (tworzy m.in. disableItem, paneloErrorNotifyItem itd.)
            ContextMenuStrip menu = BuildContextMenu();

            // 🛎️ NotifyIcon musi być zainicjalizowany PRZED LoadConfig
            trayIcon = new NotifyIcon
            {
                Icon = iconEnabled,
                ContextMenuStrip = menu,
                Visible = true
            };

            // 📂 Wczytanie ustawień (nie przed NotifyIcon!)
            LoadConfig();

            // ✅ Konfiguracja stanu zgodnie z ustawieniem
            CheckBrightnessItem(dimBrightnessPercent);
            CheckTimeoutItem(idleThresholdConfig);
            CheckScreenOffItem(screenOffAfterSecondsConfig);
            UpdateWakeupTimer();
            UpdateWakeupMenuText();
            Task.Run(() => InitKeyboardAfterStartup());
            UpdateTrayIcon();

            // 🖱️ Kliknięcie ikonki – LPM otwiera brightnessform
            trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    int b = IdleTrayApp.Instance?.lastPolledBrightness ?? -1;
                    int current = b >= 0 && b <= 100 ? b : 75;

                    Application.OpenForms.OfType<Form>().FirstOrDefault(f => f is BrightnessForm)?.Close();
                    new BrightnessForm(current).Show();
                }
            };


            // 🕒 Timer główny – tickuje co 7 sekund
            idleCheckTimer = new System.Windows.Forms.Timer { Interval = 7000 };
            idleCheckTimer.Tick += IdleCheckTimer_Tick;
            idleCheckTimer.Start();
            idleCheckTimerPublic = idleCheckTimer;

            // Wyłączenie Timera jeśli tymczasowo zablokowany
            if (isTemporarilyDisabled)
            {
                LogIdle("🔕 Start: tryb tymczasowego wyłączenia aktywny – zatrzymuję idleCheckTimer.");
                idleCheckTimer.Stop();
            }


            // 🐶 Watchdog – sprawdza, czy tick działa
            if (idleWatchdogTimer == null)
            {
                idleWatchdogTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        if (isTemporarilyDisabled || dimFormActive || GlobalScreenOff)
                        {
                            var powody = new List<string>();
                            if (isTemporarilyDisabled) powody.Add("isTemporarilyDisabled=true");
                            if (dimFormActive) powody.Add("dimFormActive=true");
                            if (GlobalScreenOff) powody.Add("GlobalScreenOff=true");

                            LogIdle("🐶 Watchdog Idle: Pomijam sprawdzanie ticka przez: " + string.Join(", ", powody));
                            return;
                        }

                        var last = lastIdleTickTime;
                        if (last == null)
                        {
                            LogIdle("🐶 Watchdog Idle: brak danych o ticku – nie robię nic.");
                            return;
                        }

                        var diff = DateTime.Now - last.Value;
                        if (diff.TotalMinutes > 1)
                        {
                            if (isWatchdogRestarting)
                            {
                                LogIdle("⛔ Watchdog już restartuje timer – pomijam");
                                return;
                            }

                            isWatchdogRestarting = true;

                            LogIdle("🐶 Watchdog Idle: brak ticka – restartuję monitorowanie.");

                            guiInvoker?.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    idleCheckTimerPublic?.Stop();
                                    if (idleCheckTimerPublic != null) idleCheckTimerPublic.Tick -= IdleCheckTimer_Tick;
                                    idleCheckTimerPublic?.Dispose();

                                    idleCheckTimerPublic = new System.Windows.Forms.Timer { Interval = 7000 };
                                    idleCheckTimerPublic.Tick += IdleCheckTimer_Tick;
                                    idleCheckTimerPublic.Start();

                                    idleCheckTimer = idleCheckTimerPublic;
                                    LogIdle("♻️ Watchdog Idle: Timer odtworzony na GUI wątku.");
                                }
                                catch (Exception ex)
                                {
                                    LogIdle($"❌ Watchdog Idle: wyjątek w BeginInvoke – {ex.Message}");
                                }
                                finally
                                {
                                    isWatchdogRestarting = false;
                                }
                            }));
                        }
                        else
                        {
                            LogIdle("🐶 Watchdog Idle: tick aktualny.");
                           // UpdateJavaWatcherState();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogIdle($"❌ Watchdog Idle: błąd – {ex.Message}");
                    }
                }, null, 0, 2 * 60 * 1000);
            }
            

            // 🔁 Ponowne ustawienie ikonki po wszystkim
            UpdateTrayIcon();

            // 👀 Watcher Java – wykrywa powrót Panelo
            javaWatcher = new JavaDialogWatcher
            {
                OnJavaDialogVisible = () =>
                {
                    LogIdle("✅ JavaWatcher wykrył powrót Panelo – anuluję follow-up.");
                    javaFollowUpTimer?.Dispose();

                    if (CurrentFormVideoPlayer != null && !CurrentFormVideoPlayer.IsDisposed)
                    {
                        try
                        {
                            LogIdle("FormVideoPlayer aktywny, zamykam przez SpróbujZamknąć - powrót Panelo");
                            CurrentFormVideoPlayer.Invoke(new MethodInvoker(() =>
                            {
                                CurrentFormVideoPlayer?.SpróbujZamknąć("powrót Panelo");
                            }));
                        }
                        catch (Exception ex)
                        {
                            LogIdle($"❌ Błąd przy zamykaniu FormVideoPlayer po powrocie Panelo: {ex.Message}");
                        }
                    }
                }
            };

            UpdateJavaWatcherState();

            MonitorStateWatcher.Start();

            MonitorStateWatcher.OnMonitorTurnedOn += () =>
            {
                if (GlobalScreenOff || WaitForUserActivity)
                {
                    LogIdle("🟢 [MonitorStateWatcher] Ekran fizycznie się WŁĄCZYŁ – resetuję GlobalScreenOff i przywracam ticka.");
                    
                    ClearWakeState();




                    guiInvoker.BeginInvoke(new Action(() =>
                    {
                        // 💥 Ubij stary timer
                        if (idleCheckTimerPublic != null)
                        {
                            idleCheckTimerPublic.Stop();
                            idleCheckTimerPublic.Tick -= IdleCheckTimer_Tick;
                            idleCheckTimerPublic.Dispose();
                            idleCheckTimerPublic = null;
                        }

                        // 🆕 Stwórz nowy
                        idleCheckTimerPublic = new System.Windows.Forms.Timer { Interval = 7000 };
                        idleCheckTimerPublic.Tick += IdleCheckTimer_Tick;
                        idleCheckTimerPublic.Start();
                        idleCheckTimer = idleCheckTimerPublic;
                        UpdateTrayIcon();
                    }));
                }

            };

            MonitorStateWatcher.OnMonitorTurnedOff += () =>
            {
                LogIdle("🔴 [MonitorStateWatcher] Ekran fizycznie się WYŁĄCZYŁ");

                if (dimFormActive)
                {
                    LogIdle("🧼 [MonitorStateWatcher] DimForm aktywny – zamykam go przez CloseFromScreenOff");
                    
                    Application.OpenForms
                        .OfType<DimForm>()
                        .FirstOrDefault()
                        ?.CloseFromScreenOff();
                }
                else
                {
                    LogIdle("🔴 [MonitorStateWatcher] DimForm nieaktywny – nie zamykam");
                }
            };

            _hookID = SetHook(_proc);
            LogIdle("🧲 Globalny hook klawiatury aktywowany");

            Application.ApplicationExit += (s, e) =>
            {
                try
                {
                    UnhookWindowsHookEx(_hookID);
                    trayIcon.Visible = false;
                    idleCheckTimer.Stop();
                    LogIdle("🧹 Cleanup – odłączono globalny hook klawiatury, zamknięto ikonkę, zatrzymano timer");
                }
                catch { }
            };


        }


        private async void InitKeyboardAfterStartup()
        {

            try
            {
                LoadBrightnessMapFromSettings();
                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keyboard_Core.dll");
                keyboard = new KeyboardController(dllPath);
                int brightness = await GetCurrentBrightnessAsync();
                SetKeyboardBacklightBasedOnBrightness(brightness);
                lastKnownBrightness = brightness;
            }
            catch (Exception ex)
            {
                LogIdle($"❌ Błąd inicjalizacji KeyboardController: {ex.Message}");
                keyboard = null;
            }
        }


        private void LoadConfig()
        {
            string CleanValue(string line) =>
                line.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();

            if (!File.Exists(configPath)) return;

            var lines = File.ReadAllLines(configPath);

            // 1. Czas bezczynności do przygaszenia
            if (lines.Length >= 1 && int.TryParse(CleanValue(lines[0]), out int savedTimeout))
                idleThresholdConfig = savedTimeout;

            // 2. Czas po którym wyłączyć ekran
            if (lines.Length >= 2 && int.TryParse(CleanValue(lines[1]), out int screenDelay))
                screenOffAfterSecondsConfig = screenDelay;

            // 3. Jasność przygaszenia
            if (lines.Length >= 3 && int.TryParse(CleanValue(lines[2]), out int savedBrightness))
                dimBrightnessPercent = savedBrightness;

            // 4. Nasłuch audio
            if (lines.Length >= 4)
                wakeOnAudio = CleanValue(lines[3]) == "1";
            else
                wakeOnAudio = true;

            // 5. Tymczasowa blokada
            bool fromFile = (lines.Length >= 5 && CleanValue(lines[4]) == "1");
            isTemporarilyDisabled = fromFile;
            if (disableItem != null)
                disableItem.Checked = fromFile;

            // 6. Panelo – monitorowanie okna
            if (lines.Length >= 6)
                monitorJavaDialog = CleanValue(lines[5]) == "1";
            else
                monitorJavaDialog = true;

            // timer i limity
            if (isTemporarilyDisabled)
            {

                idleThresholdRuntime = int.MaxValue;
                screenOffAfterSecondsRuntime = int.MaxValue;
            }
            else
            {
                idleThresholdRuntime = idleThresholdConfig;
                screenOffAfterSecondsRuntime = screenOffAfterSecondsConfig;
                idleCheckTimer?.Start();
            }

            //7. Blad panelu
            if (lines.Length >= 7)
                paneloErrorNotifyEnabled = CleanValue(lines[6]) == "1";
            else
                paneloErrorNotifyEnabled = true;

            if (paneloErrorNotifyItem != null)
                paneloErrorNotifyItem.Checked = paneloErrorNotifyEnabled;

            //8. Budzik
            if (lines.Length >= 8 && int.TryParse(CleanValue(lines[7]), out int wakeupMins))
                wakeupIntervalMinutes = wakeupMins;
            else
                wakeupIntervalMinutes = -1;

            // 9. Automatyczne sterowanie klawiaturą
            if (lines.Length >= 9)
                keyboardAutoEnabled = CleanValue(lines[8]) == "1";
            else
                keyboardAutoEnabled = true;

            if (keyboardAutoToggleItem != null)
                keyboardAutoToggleItem.Checked = keyboardAutoEnabled;





            // odświeżenie ptaszków
            if (audioWakeItem != null)
                audioWakeItem.Checked = wakeOnAudio;

            if (javaMonitorMenuItem != null)
                javaMonitorMenuItem.Checked = monitorJavaDialog;

            if (disableItem != null)
                disableItem.Checked = isTemporarilyDisabled;

            // logi
            LogIdle($"Wczytano config: {idleThresholdConfig}|{screenOffAfterSecondsConfig}|{dimBrightnessPercent}|{(wakeOnAudio ? 1 : 0)}|{(isTemporarilyDisabled ? 1 : 0)}|{(monitorJavaDialog ? 1 : 0)}|{(paneloErrorNotifyEnabled ? 1 : 0)}");

            LogIdle($"[CONFIG APPLIED] tempIdleThreshold: {idleThresholdRuntime}, tempScreenOffAfterSeconds: {screenOffAfterSecondsRuntime}");
        }


        private void SaveConfig()
        {
            var lines = new List<string>
            {
              $"{idleThresholdConfig} // czas bezczynności do przygaszenia jasności [s]",
              $"{screenOffAfterSecondsConfig} // opóźnienie wyłączenia ekranu po ściemnieniu [s]",
              $"{dimBrightnessPercent} // poziom ściemniania jasności [%]",
              $"{(wakeOnAudio ? "1" : "0")} // 0-nie nasłuchuj audio, 1-nasłuchuj audio",
              $"{(disableItem?.Checked == true ? "1" : "0")} // 0-normalnie, 1-tymczasowo zablokowane",
              $"{(monitorJavaDialog ? "1" : "0")} // 0-Panelo nie jest śledzone - brak budzika, 1-Zniknięcie okienka oczekiwania na wiadomość w Panelo powoduje włączenie budzika",
              $"{(paneloErrorNotifyEnabled ? "1" : "0")} // 0-nie pokazuj błędów Panelo, 1-pokaż bąbel+dźwięk",
              $"{wakeupIntervalMinutes} // interwał budzika cyklicznego w minutach (-1 = wyłączony)",
              $"{(keyboardAutoEnabled ? "1" : "0")} // 0-wyłączone auto klawiatura, 1-włączone",

            };

            try
            {
                File.WriteAllLines(configPath, lines);
                LogIdle($"Zapisano config: {idleThresholdConfig}|{screenOffAfterSecondsConfig}|{dimBrightnessPercent}|{(wakeOnAudio ? 1 : 0)}|{(disableItem?.Checked == true ? 1 : 0)}|{(monitorJavaDialog ? 1 : 0)}|{(paneloErrorNotifyEnabled ? 1 : 0)}");



            }
            catch (Exception ex)
            {
                LogIdle($"Błąd zapisu configu: {ex.Message}");
            }

            UpdateTrayIcon();
        }


        private void LoadBrightnessMapFromSettings()
        {
            var saved = Properties.Settings.Default.KeyboardLevelMap;
            if (!string.IsNullOrWhiteSpace(saved))
            {
                try
                {
                    var array = saved.Split(',').Select(x => int.Parse(x)).ToArray();

                    var map = new List<(int min, int max, int level)>();
                    int start = 0;
                    int current = array[0];

                    for (int i = 1; i <= 10; i++)
                    {
                        if (i == 10 || array[i] != current)
                        {
                            int min = start * 10;
                            int max = i * 10 - 1;
                            if (i == 10) max = 100;

                            map.Add((min, max, current));
                            if (i < 10)
                            {
                                current = array[i];
                                start = i;
                            }
                        }
                    }

                    brightnessToLevelMap = map;
                    LogIdle($"📥 Wczytano mapę z ustawień: {saved}");
                }
                catch (Exception ex)
                {
                    LogIdle($"❌ Błąd parsowania mapy z ustawień: {ex.Message}");
                    brightnessToLevelMap = new List<(int, int, int)>();
                }
            }
            else
            {
                LogIdle("⚠️ Brak zapisanej mapy – ustawiam domyślną");
                brightnessToLevelMap = new List<(int, int, int)>
        {
            (0, 30, 1),
            (31, 50, 2),
            (51, 100, 0)
        };
            }
        }


        private Icon LoadEmbeddedIcon(string resourceName)
        {
            using (var stream = typeof(IdleTrayApp).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    MessageBox.Show($"Brak zasobu: {resourceName}", "Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return SystemIcons.Application;
                }

                // kopiujemy dane do bufora w pamięci
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    return new Icon(ms);
                }
            }
        }



       
          //**************************************//
         // MENU, TRAY + STYLIZACJA I AKTUALIZACJA 
        //*************************************//

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new ModernRenderer(),
                Font = new Font("Segoe UI", 9.75f, FontStyle.Regular),
                ShowImageMargin = true,
                Padding = new Padding(4),
            };

            // 📄 TESTY (zakomentowane)
            /*
            var simulateJavaItem = new ToolStripMenuItem("\uD83D\uDD01 Test: zasymuluj zniknięcie okna Java");
            StyleMenuItem(simulateJavaItem);
            simulateJavaItem.Click += (s, e) =>
            {
                LogIdle("\uD83D\uDD01 Testowy przycisk → symuluję zniknięcie okna Java");
                ResetByPopup();
                string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert.mp4");
                var videoForm = new FormVideoPlayer(videoPath);
                videoForm.Show();
            };
            menu.Items.Insert(0, simulateJavaItem);
            /*

            // 🧪 Test czkawki ticka
            var simulateCzkawkaItem = new ToolStripMenuItem("🧪 Testuj czkawkę Ticka (10x recreate timer)");
            StyleMenuItem(simulateCzkawkaItem);
            simulateCzkawkaItem.Click += (s, e) =>
            {
                LogIdle("🧪 Ręczny test: próbuję odtworzyć timer 10x z rzędu");

                for (int i = 0; i < 500; i++)
                {
                    idleCheckTimerPublic = new System.Windows.Forms.Timer { Interval = 7000 };
                    idleCheckTimerPublic.Tick += IdleCheckTimer_Tick;
                    idleCheckTimerPublic.Start();
                    idleCheckTimer = idleCheckTimerPublic;
                    LogIdle($"♻️ [{i + 1}/10] Próba odtworzenia Timer NA GUI wątku.");
                }
            };
            menu.Items.Add(simulateCzkawkaItem);
            */



            // 🔧 Przyciemnianie po bezczynności
            timeoutMenu = new ToolStripMenuItem("Przy bezczynności - PRZYGAŚ ekran po");
            StyleMenuItem(timeoutMenu);
            AddTimeoutOption("30 s.", 30);
            AddTimeoutOption("1 min.", 60);
            AddTimeoutOption("2 min.", 120);
            AddTimeoutOption("5 min.", 300);
            AddTimeoutOption("10 min.", 600);
            AddTimeoutOption("30 min.", 1800);
            foreach (ToolStripMenuItem item in timeoutMenu.DropDownItems)
                StyleSubMenuItem(item);
            menu.Items.Add(timeoutMenu);

            // 🔧 Wyłączanie ekranu po bezczynności
            screenOffMenu = new ToolStripMenuItem("Przy bezczynności - WYŁĄCZ ekran po");
            StyleMenuItem(screenOffMenu);
            AddScreenOffOption("3 min.", 180);
            AddScreenOffOption("7 min.", 420);
            AddScreenOffOption("15 min.", 900);
            AddScreenOffOption("40 min.", 2400);
            AddScreenOffOption("Nigdy", -1);
            foreach (ToolStripMenuItem item in screenOffMenu.DropDownItems)
                StyleSubMenuItem(item);
            menu.Items.Add(screenOffMenu);

            menu.Items.Add(new ToolStripSeparator());

            // 🔆 Jasność przygaszonego ekranu
            brightnessLevelMenu = new ToolStripMenuItem("Jasność przygaszonego ekranu");
            StyleMenuItem(brightnessLevelMenu);
            AddBrightnessOption("0%", 0);
            AddBrightnessOption("5%", 5);
            AddBrightnessOption("10%", 10);
            AddBrightnessOption("15%", 15);
            AddBrightnessOption("20%", 20);
            AddBrightnessOption("25%", 25);
            AddBrightnessOption("30%", 30);
            foreach (ToolStripMenuItem item in brightnessLevelMenu.DropDownItems)
                StyleSubMenuItem(item);
            menu.Items.Add(brightnessLevelMenu);

            // 🔈 Dźwięk jako aktywność
            audioWakeItem = new ToolStripMenuItem("Wykrywaj dźwięk (np. NetFlix, YouTube)")
            {
                Checked = wakeOnAudio,
                CheckOnClick = true
            };
            StyleMenuItem(audioWakeItem);
            audioWakeItem.CheckedChanged += (s, e) =>
            {
                wakeOnAudio = audioWakeItem.Checked;
                SaveConfig();
            };
            menu.Items.Add(audioWakeItem);

            menu.Items.Add(new ToolStripSeparator());

            // 🎹 Auto sterowanie klawiaturą
            keyboardAutoToggleItem = new ToolStripMenuItem("Automatyczne sterowanie klawiaturą")
            {
                Checked = true,
                CheckOnClick = true
            };
            StyleMenuItem(keyboardAutoToggleItem);
            keyboardAutoToggleItem.CheckedChanged += async (s, e) =>
            {
                keyboardAutoEnabled = keyboardAutoToggleItem.Checked;
                SaveConfig();

                if (keyboardAutoEnabled)
                {
                    try
                    {
                        LoadBrightnessMapFromSettings();
                        int brightness = await GetCurrentBrightnessAsync();
                        LogIdle($"🎹 Auto-klawiatura włączona → ustawiam podświetlenie");
                        SetKeyboardBacklightBasedOnBrightness(brightness);

                    }
                    catch (Exception ex)
                    {
                        LogIdle($"❌ Błąd przy włączaniu auto-klawiatury: {ex.Message}");
                    }
                }
            };

            menu.Items.Add(keyboardAutoToggleItem);

            // ⚙️ Opcje klawiatury
            var optionsItem = new ToolStripMenuItem("Opcje klawiatury...");
            StyleMenuItem(optionsItem);
            optionsItem.Click += (s, e) =>
            {
                if (formOptions == null || formOptions.IsDisposed)
                {
                    formOptions = new FormOptions();
                    formOptions.FormClosed += (_, __) => formOptions = null;
                    formOptions.Show();
                }
                else
                {
                    formOptions.WindowState = FormWindowState.Normal;
                    formOptions.TopMost = true;
                    formOptions.TopMost = false;
                    formOptions.BringToFront();
                    FlashWindowHelper.Flash(formOptions);
                }
            };
            menu.Items.Add(optionsItem);

            menu.Items.Add(new ToolStripSeparator());

            // 📦 Panelo – podmenu
            paneloMenu = new ToolStripMenuItem("Panelo");
            StyleMenuItem(paneloMenu);

            javaMonitorMenuItem = new ToolStripMenuItem("Budzik przy nowej wiadomości")
            {
                Checked = monitorJavaDialog,
                CheckOnClick = true
            };
            StyleSubMenuItem(javaMonitorMenuItem);
            javaMonitorMenuItem.CheckedChanged += (s, e) =>
            {
                monitorJavaDialog = javaMonitorMenuItem.Checked;
                UpdateJavaWatcherState();
                SaveConfig();
            };
            paneloMenu.DropDownItems.Add(javaMonitorMenuItem);

            paneloErrorNotifyItem = new ToolStripMenuItem("Informuj o zerwanym połączeniu")
            {
                Checked = paneloErrorNotifyEnabled,
                CheckOnClick = true
            };
            StyleSubMenuItem(paneloErrorNotifyItem);
            paneloErrorNotifyItem.CheckedChanged += (s, e) =>
            {
                paneloErrorNotifyEnabled = paneloErrorNotifyItem.Checked;
                SaveConfig();
                UpdateJavaWatcherState();

                if (!paneloErrorNotifyEnabled && Instance?.javaWatcher != null)
                {
                    Instance.javaWatcher.ForceStopPaneloAlarm();
                    LogIdle("\uD83D\uDEA9 PaneloErrorNotify → opcja odznaczona – zatrzymuję dźwięk i resetuję licznik błędu");
                }
            };
            paneloMenu.DropDownItems.Add(paneloErrorNotifyItem);

            wakeupMenu = new ToolStripMenuItem("Budzik cykliczny");
            AddWakeupOption("10 min", 10);
            AddWakeupOption("15 min", 15);
            AddWakeupOption("20 min", 20);
            AddWakeupOption("30 min", 30);
            AddWakeupOption("Wyłączony", -1);
            StyleSubMenuItem(wakeupMenu);


            void AddWakeupOption(string label, int minutes)
            {
                var item = new ToolStripMenuItem(label)
                {
                    Tag = minutes,
                    Checked = (wakeupIntervalMinutes == minutes)
                };
                StyleSubMenuItem(item);

                item.Click += (s, e) =>
                {
                    wakeupIntervalMinutes = (int)((ToolStripMenuItem)s).Tag;

                    foreach (ToolStripMenuItem i in wakeupMenu.DropDownItems)
                        i.Checked = false;
                    item.Checked = true;

                    SaveConfig();
                    UpdateWakeupTimer();
                    UpdateWakeupMenuText();
                    UpdateTrayIcon();
                };

                wakeupMenu.DropDownItems.Add(item);
            }




            paneloMenu.DropDownItems.Add(wakeupMenu);

            menu.Items.Add(paneloMenu);

            menu.Items.Add(new ToolStripSeparator());

            // 🛑 Tryb tymczasowy
            disableItem = new ToolStripMenuItem("Tymczasowo zablokuj wygaszanie")
            {
                Checked = false
            };
            StyleMenuItem(disableItem);
            disableItem.Click += (s, e) =>
            {
                disableItem.Checked = !disableItem.Checked;
                isTemporarilyDisabled = disableItem.Checked;

                LogIdle($"KLIK! Checked: {disableItem.Checked}, isTemporarilyDisabled: {isTemporarilyDisabled}");

                if (isTemporarilyDisabled)
                {
                    idleThresholdRuntime = int.MaxValue;
                    screenOffAfterSecondsRuntime = int.MaxValue;
                    idleCheckTimer.Stop();
                }
                else
                {
                    idleThresholdRuntime = idleThresholdConfig;
                    screenOffAfterSecondsRuntime = screenOffAfterSecondsConfig;
                    idleCheckTimer.Start();
                    CheckTimeoutItem(idleThresholdConfig);
                }

                SaveConfig();
                disableItem.Invalidate();
            };
            menu.Items.Add(disableItem);



            // ❌ Zamknij program
            exitItem = new ToolStripMenuItem("Zamknij program");
            StyleMenuItem(exitItem);
            exitItem.Click += (s, e) =>
            {
                Application.Exit();
            };
            menu.Items.Add(exitItem);

            return menu;
        }


        private void StyleMenuItem(ToolStripMenuItem item)
        {
            item.Padding = new Padding(0, 8, 0, 8);
            item.Margin = new Padding(0);
            item.ImageScaling = ToolStripItemImageScaling.None;

            // Stylizuj również wszystkie jego podmenu (jeśli istnieją)
            foreach (ToolStripItem sub in item.DropDownItems)
            {
                if (sub is ToolStripMenuItem subItem)
                {
                    StyleSubMenuItem(subItem);
                }
            }


            item.Paint += (s, e) =>
            {

                if (s.GetType() == typeof(ToolStripSeparator))
                    return;

                var i = (ToolStripMenuItem)s;
                var g = e.Graphics;
                Rectangle bounds = e.ClipRectangle;

                Rectangle textArea = new Rectangle(bounds.Left + 28, bounds.Top, bounds.Width - 28, bounds.Height);


                Color background = i.Selected ? Color.White : Color.FromArgb(238, 238, 238);
                using (Brush b = new SolidBrush(background))
                    g.FillRectangle(b, textArea);


                var text = i.Text;
                var font = i.Font;
                Size textSize = TextRenderer.MeasureText(text, font);
                int iconOffset = 32;

                Rectangle textRect = new Rectangle(
                         bounds.Left + iconOffset,
                         bounds.Top + (bounds.Height - textSize.Height) / 2,
                         bounds.Width - iconOffset - 4,
                         textSize.Height
                     );
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                TextRenderer.DrawText(

                         g,
                         text,
                         font,
                         textRect,
                         Color.Black,
                         TextFormatFlags.Left | TextFormatFlags.NoPadding
                     );

                // Strzałka dla rozwijanych pozycji – tylko jeśli ma DropDownItems
                if (i.HasDropDownItems)
                {


                    using (Pen pen = new Pen(Color.FromArgb(15, 15, 60), 1))
                    {
                        int cx = bounds.Right - 12;
                        int cy = bounds.Top + bounds.Height / 2;

                        Point p1 = new Point(cx - 4, cy - 6); // było -3, -4 → teraz szersza i wyższa
                        Point p2 = new Point(cx + 3, cy);     // bez zmian
                        Point p3 = new Point(cx - 4, cy + 6); // było -3, +4 → teraz szersza i wyższa

                        g.DrawLines(pen, new[] { p1, p2, p3 });
                    }


                }

                // Ptaszek NIE jest tu rysowany – robi to `OnRenderItemCheck` z ModernRenderer
            };


        }


        private void StyleSubMenuItem(ToolStripMenuItem item)
        {
            item.Padding = new Padding(0, 7, 0, 7);
            item.Margin = new Padding(0);
            item.ImageScaling = ToolStripItemImageScaling.None;

            item.Paint += (s, e) =>
            {
                var i = (ToolStripMenuItem)s;
                var g = e.Graphics;
                Rectangle bounds = e.ClipRectangle;
                Rectangle textArea = new Rectangle(bounds.Left + 28, bounds.Top, bounds.Width - 28, bounds.Height);

                // Rysowanie tła tylko w obszarze tekstu
                Color background = i.Selected ? Color.White : Color.FromArgb(238, 238, 238);
                using (Brush b = new SolidBrush(background))
                    g.FillRectangle(b, textArea);

                // Rysowanie tekstu ręcznie
                var text = i.Text;
                var font = i.Font;
                Size textSize = TextRenderer.MeasureText(text, font);
                int iconOffset = 32;

                Rectangle textRect = new Rectangle(
                    bounds.Left + iconOffset,
                    bounds.Top + (bounds.Height - textSize.Height) / 2,
                    bounds.Width - iconOffset - 4,
                    textSize.Height
                );

                TextRenderer.DrawText(
                    g,
                    text,
                    font,
                    textRect,
                    Color.Black,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding
                );

                // Strzałka dla rozwijanych pozycji – tylko jeśli ma DropDownItems
                if (i.HasDropDownItems)
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (Pen pen = new Pen(Color.FromArgb(15, 15, 60), 1))
                    {
                        int cx = bounds.Right - 12;
                        int cy = bounds.Top + bounds.Height / 2;

                        Point p1 = new Point(cx - 4, cy - 6);
                        Point p2 = new Point(cx + 3, cy);
                        Point p3 = new Point(cx - 4, cy + 6);

                        g.DrawLines(pen, new[] { p1, p2, p3 });
                    }
                }
            };
        }


        private void AddTimeoutOption(string label, int seconds)
        {
            var item = new ToolStripMenuItem(label) { Tag = seconds };
            item.Click += (s, e) =>
            {
                idleThresholdConfig = (int)((ToolStripMenuItem)s).Tag;
                screenOffAfterSecondsRuntime = screenOffAfterSecondsConfig;
                idleThresholdRuntime = idleThresholdConfig;

                trayIcon.Text = $"Przygaś po: {FormatThresholdText(idleThresholdConfig)}";

                foreach (ToolStripMenuItem i in timeoutMenu.DropDownItems)
                    i.Checked = false;
                ((ToolStripMenuItem)s).Checked = true;
                disableItem.Checked = false;
                isTemporarilyDisabled = false;
                idleCheckTimer.Start();

                SaveConfig();
                
            };
            timeoutMenu.DropDownItems.Add(item);

        }

        private void AddScreenOffOption(string label, int seconds)
        {
            var item = new ToolStripMenuItem(label)
            {
                Tag = seconds,
                Checked = (screenOffAfterSecondsConfig == seconds)
            };

            item.Click += (s, e) =>
            {
                screenOffAfterSecondsConfig = (int)((ToolStripMenuItem)s).Tag;
                screenOffAfterSecondsRuntime = screenOffAfterSecondsConfig;
                idleThresholdRuntime = idleThresholdConfig;


                foreach (ToolStripMenuItem i in screenOffMenu.DropDownItems)
                    i.Checked = false;
                item.Checked = true;
                disableItem.Checked = false;
                isTemporarilyDisabled = false;
                idleCheckTimer.Start();


                SaveConfig();
            };

            screenOffMenu.DropDownItems.Add(item);
            
        }


        private void AddBrightnessOption(string label, int percent)
        {
            var item = new ToolStripMenuItem(label)
            {
                Tag = percent,
                Checked = (dimBrightnessPercent == percent)
            };

            item.Click += (s, e) =>
            {
                dimBrightnessPercent = (int)((ToolStripMenuItem)s).Tag;

                foreach (ToolStripMenuItem i in brightnessLevelMenu.DropDownItems)
                    i.Checked = false;
                item.Checked = true;

                SaveConfig();

            };

            brightnessLevelMenu.DropDownItems.Add(item);
        }
                        

        private void SetIconForTimeout(int seconds)
        {
            if (trayIcon == null)
            {
                MessageBox.Show("Nie mogę ustawić ikony – trayIcon to null!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string resourceName;
            switch (seconds)
            {
                case 10: resourceName = "DimScreenSaver.10s.ico"; break;
                case 30: resourceName = "DimScreenSaver.30s.ico"; break;
                case 60: resourceName = "DimScreenSaver.1m.ico"; break;
                case 120: resourceName = "DimScreenSaver.2m.ico"; break;
                case 300: resourceName = "DimScreenSaver.5m.ico"; break;
                case 600: resourceName = "DimScreenSaver.10m.ico"; break;
                case 1800: resourceName = "DimScreenSaver.30m.ico"; break;
                case int.MaxValue: resourceName = "DimScreenSaver.off.ico"; break;
                default: resourceName = "DimScreenSaver.dim.ico"; break;
            }

            try
            {
                var assembly = typeof(IdleTrayApp).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new Exception($"Brak zasobu: {resourceName}");
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        trayIcon.Icon = new Icon(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania ikony: {ex.Message}", "Tray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                trayIcon.Icon = iconEnabled ?? SystemIcons.Application; // fallback
            }


        }


        private string FormatThresholdText(int seconds)
        {
            if (seconds == int.MaxValue || seconds < 0)
                return "Nigdy";

            if (seconds < 60)
                return $"{seconds} sek";

            int minutes = seconds / 60;
            return $"{minutes} min";
        }


        private void UpdateTrayIcon()
        {
            if (trayIcon == null)
                return;

            if (isTemporarilyDisabled)
            {
                SetIconForTimeout(int.MaxValue); // ← załaduje DimScreenSaver.off.ico
                trayIcon.Text = "Tymczasowo zablokowany";
                return;
            }


            bool wygaszaczNiesensowny = screenOffAfterSecondsConfig > 0 && screenOffAfterSecondsConfig < idleThresholdConfig;

            if (wygaszaczNiesensowny)
            {
                trayIcon.Icon = iconOnlyOff ?? SystemIcons.Application;
            }
            else
            {
                SetIconForTimeout(idleThresholdRuntime);
            }




            trayIcon.Text =
                $"Przygaś po: {(wygaszaczNiesensowny ? "N/A" : FormatThresholdText(idleThresholdConfig))}\n" +
                $"Wyłącz po: {FormatThresholdText(screenOffAfterSecondsConfig)}";
            if (disableItem != null)
                disableItem.Checked = isTemporarilyDisabled;



            if (wakeupMenu != null)
            {
                foreach (ToolStripMenuItem item in wakeupMenu.DropDownItems)
                {
                    if (item.Tag is int value)
                        item.Checked = (value == wakeupIntervalMinutes);
                }
            }

            wakeupMenu.Checked = (wakeupIntervalMinutes != -1);

        }


        private void UpdateWakeupMenuText()
        {
            if (wakeupMenu == null)
                return;

            string suffix;
            if (wakeupIntervalMinutes == -1)
                suffix = "Wyłączony";
            else
                suffix = $"{wakeupIntervalMinutes} min";



            wakeupMenu.Text = $"Budzik cykliczny - {suffix}";
        }


        private void UpdateWakeupTimer()
        {
            wakeupTimer?.Dispose();

            if (wakeupIntervalMinutes <= 0)
            {
                LogIdle("⏰ Budzik cykliczny wyłączony");
                return;
                
            }

            LogIdle($"⏰ Ustawiam budzik cykliczny co {wakeupIntervalMinutes} minut.");

            wakeupTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    LogIdle("⏰ Budzik cykliczny – odpalam budzik.mp4");
                    UISyncContext?.Post(__ =>
                    {
                        string label = wakeupIntervalMinutes == -1 ? "Wyłączony" : $"{wakeupIntervalMinutes} min";
                        BalloonForm.ShowBalloon("Dzwoni budzik", $"Obecnie ustawiony co: {label}", 10000);
                        ResetByPopup();
                        string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "budzik.mp4");
                        if (CurrentFormWakeup != null && !CurrentFormWakeup.IsDisposed)
                        {
                            LogIdle("🚫 FormWakeup już aktywny – nie tworzę nowego");
                            return;
                        }

                        CurrentFormWakeup = new FormWakeup(videoPath);
                        CurrentFormWakeup.Show();
                    }, null);
                }
                catch (Exception ex)
                {
                    LogIdle($"❌ Błąd w budziku cyklicznym: {ex.Message}");
                }
            }, null, TimeSpan.FromMinutes(wakeupIntervalMinutes), TimeSpan.FromMinutes(wakeupIntervalMinutes));
        }


        private void CheckTimeoutItem(int seconds)
        {
            foreach (ToolStripMenuItem item in timeoutMenu.DropDownItems)
                item.Checked = false;

            foreach (ToolStripMenuItem item in timeoutMenu.DropDownItems)
            {
                if ((int)item.Tag == seconds)
                {
                    item.Checked = true;
                    break;
                }
            }
        }


        private void CheckBrightnessItem(int percent)
        {
            foreach (ToolStripMenuItem item in brightnessLevelMenu.DropDownItems)
            {
                item.Checked = ((int)item.Tag == percent);
            }
        }


        private void CheckScreenOffItem(int seconds)
        {
            foreach (ToolStripMenuItem item in screenOffMenu.DropDownItems)
            {
                item.Checked = ((int)item.Tag == seconds);
            }
        }




          //**********************************//
         // PODŚWIETLENIE KLAWIATURY I JASNOŚĆ
        //**********************************//

        public static async Task<int> GetCurrentBrightnessAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness"))
                    {
                        foreach (ManagementObject obj in searcher.Get().OfType<ManagementObject>())
                        {
                            return Convert.ToInt32(obj["CurrentBrightness"]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogIdle($"[WMI] Błąd odczytu jasności: {ex.Message}");
                }
                return 75;
            });
        }


        public static async Task SetBrightnessAsync(int brightness)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var mclass = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null))
                    {
                        foreach (ManagementObject instance in mclass.GetInstances().OfType<ManagementObject>())
                        {
                            instance.InvokeMethod("WmiSetBrightness", new object[] { 1, brightness });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogIdle($"[WMI] Błąd ustawiania jasności: {ex.Message}");
                }
            });
        }


        public void SetLastPolledBrightness(int value)
        {
            if (value >= 0 && value <= 100)
                lastPolledBrightness = value;
            LogIdle("Ustawiam lastPolledBrightness na przekazaną z BrightnessForm");
        }


        private int GetBacklightLevelForBrightness(int brightness)
        {
            foreach (var (min, max, level) in brightnessToLevelMap)
            {
                if (brightness >= min && brightness <= max)
                    return level;
            }

            return 0; // fallback
        }


        // Sterowanie podswietleniem klawiatury wykorzystywane przez funkcje automatyczne
        // takie jak wygaszenie ekranu lub zmiana jasnosci ekranu. (Force change)
        public void SetKeyboardBacklightBasedOnBrightness(int brightness)
        {
            if (!keyboardAutoEnabled || keyboard == null)
                return;

            int level = GetBacklightLevelForBrightness(brightness);

            keyboard.Set(level);
            LogIdle($"🎹 Klawiatura: Force change: Jasność {brightness}% → Poziom {level}");
        }

        // Sterowanie podswietleniem klawiatury wykorzystywane tylko w ticku
        // 🔄 Automatyczne podświetlenie klawiatury – sprytny feature, nie bug:
        // Tick ustawia poziom podświetlenia tylko przy zmianie jasności ekranu.
        // Jeśli użytkownik ręcznie wyłączy podświetlenie (np. Fn + Space),
        // to nie zostanie ono nadpisane – dopóki jasność się nie zmieni
        // lub nie nastąpi wygaszenie/wybudzenie ekranu (DimForm / DisplayControl).
        // Dzięki temu użytkownik ma pełną kontrolę – a automat trzyma się z boku.
        // Po zmianie reczej intensywnosci podswietlenia klawiatury level zostaje
        // nadal rowny lastlevel bo jasnosc ekranu sie nie zmienila. (Tick)
        public void SetKeyboardBacklightBasedOnBrightnessTick(int brightness)
        {
            if (!keyboardAutoEnabled || keyboard == null || (dimFormIsOpen))
                return;

            int level = GetBacklightLevelForBrightness(brightness);

            if (level != lastBacklightLevel)
            {
                try
                {
                    keyboard.Set(level);
                    LogIdle($"🎹 Klawiatura: Tick: Jasność {brightness}% → Poziom {level}");
                    lastBacklightLevel = level;
                }
                catch (Exception ex)
                {
                    LogIdle($"❌ Błąd ustawiania klawiatury: {ex.Message}");
                }
            }
        }




        //**********************************//
        // TICK, IDLE, DIMFORM, LOGIKA, INNE
        //**********************************//

      

        private void IdleCheckTimer_Tick(object sender, EventArgs e)
        {
            lock (tickLock)
            {
                DateTime now = DateTime.Now;
                recentTicks.Enqueue(now);

                // usuń stare ticki (starsze niż 500 ms)
                while (recentTicks.Count > 0 && (now - recentTicks.Peek()).TotalMilliseconds > 6900)
                    recentTicks.Dequeue();

                if (recentTicks.Count > 1)
                {
                    LogIdle("💣 Wykryto nadmiarowe ticki. Restartuje aplikacje.");
                    trayIcon.Visible = false;
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
            if (isTickRunning)
            {
                LogIdle("Tick is running. wychodze.");
                return;
            }
            isTickRunning = true;

            try
            {
                if (WaitForUserActivity)
            {
                int idleNow = GetIdleTime() / 1000;
                bool audioActive = wakeOnAudio && AudioWatcher.IsAudioPlaying();

                LogIdle($"[MAIN TICK] AudioActive = {audioActive} (wakeOnAudio = {wakeOnAudio})");

                if (idleNow == 0 || idleNow < lastIdleTime || audioActive)
                {
                    LogIdle($"[MAIN TICK] Kończę Oczekiwanie na aktywność – idleNow={idleNow}, lastIdleTime={lastIdleTime}");
                    WaitForUserActivity = false;
                    GlobalScreenOff = false;
                    idleCheckTimerPublic?.Start();
                    UpdateTrayIcon();
                    lastIdleTime = -1;
                    return;
                }

                lastIdleTime = idleNow;
                LogIdle($"[MAIN TICK] Oczekiwanie na aktywność... idle: {idleNow}s");
                return;
            }

            if (GlobalScreenOff)
            {
                LogIdle("[MAIN TICK] ⛔ Tick pominięty – ekran wyłączony (GlobalScreenOff)");
                return;
            }

            lastIdleTickTime = DateTime.Now;

            _ = Task.Run(async () =>
            {
                try
                {
                    int brightness = await GetCurrentBrightnessAsync();
                    if (brightness >= 0 && brightness <= 100)
                    {
                        lastPolledBrightness = brightness;

                        UISyncContext?.Post(_ =>
                        {
                            SetKeyboardBacklightBasedOnBrightnessTick(brightness);
                            lastKnownBrightness = brightness;
                        }, null);
                    }
                    else
                    {
                        LogIdle($"[MAIN TICK] ❌ Jasność poza zakresem: {brightness}");
                    }
                }
                catch (Exception ex)
                {
                    LogIdle($"[MAIN TICK] ⚠️ Błąd podczas pobierania jasności: {ex.Message}");
                }
            });

            int idle = GetIdleTime() / 1000;
            bool audioActiveNow = wakeOnAudio && AudioWatcher.IsAudioPlaying();

                if (wakeOnAudio && audioActiveNow)
                {
                    LogIdle($"⏱️ [MAIN TICK] IDLE: prog: {idleSeconds}s, sys: {idle}s, THRESHOLD: dim:{idleThresholdRuntime}s off:{screenOffAfterSecondsRuntime}s | 🔊");
                }
                else
                {
                    LogIdle($"⏱️ [MAIN TICK] IDLE: prog: {idleSeconds}s, sys: {idle}s, THRESHOLD: dim:{idleThresholdRuntime}s off:{screenOffAfterSecondsRuntime}s");
                }

                if (idle == 0 || audioActiveNow)
            {
                ResetIdle();
                idleSeconds = 0;
                GlobalScreenOff = false;
                WaitForUserActivity = false;
            }
            else
            {
                idleSeconds = idle;
            }

            if (idleSeconds >= idleThresholdRuntime)
            {
                idleCheckTimer.Stop();

                if (!GlobalScreenOff)
                {
                    trayIcon.Text = "[MAIN TICK] Uruchamiam przygaszanie...";
                    ShowDimForm();
                }
                else
                {
                    trayIcon.Text = "[MAIN TICK] Przygaszanie pominięte";
                }
            }

            if (!disableItem.Checked &&
                screenOffAfterSecondsRuntime > 0 &&
                idleSeconds >= screenOffAfterSecondsRuntime &&
                !dimFormActive &&
                !GlobalScreenOff)
            {
                LogIdle($"[MAIN TICK] 🌒 Ekran OFF przez IdleTrayApp | idle: {idleSeconds}");

                DisplayControl.TurnOff();
                ResetIdle();
                GlobalScreenOff = true;
                WaitForUserActivity = true;
                idleCheckTimer.Stop();
            }
            }
            finally
            {
                isTickRunning = false;
            }
        }


        private async void ShowDimForm()
        {
            if (dimFormIsOpen)
            {
                LogIdle("❌ [ShowDimForm] Forma już otwarta – pomijam");
                return;
            }

            dimFormIsOpen = true;



            LogIdle("🟢 [ShowDimForm] START → przygotowuję przygaszenie");

            int freshIdle = GetIdleTime() / 1000;
            if (freshIdle < idleThresholdRuntime)
            {
                LogIdle($"❌ Odrzucono przygaszanie – nowy GetIdleTime = {freshIdle}s, poniżej progu.");
                return;
            }



            DimForm form = null;

            try
            {
                string brightnessPath = Path.Combine(Path.GetTempPath(), "brightness.txt");
                int currentBrightness = await GetCurrentBrightnessAsync();
                lastKnownBrightness = currentBrightness;

                if (currentBrightness <= dimBrightnessPercent)
                {
                    LogIdle($"⛔ Przygaszanie anulowane – obecna jasność ({currentBrightness}%) ≤ docelowa dim ({dimBrightnessPercent}%)");

                    var now = DateTime.Now;
                    var last = IdleTrayApp.lastSkippedDimNotificationTime;

                    if (last == null || (now - last.Value).TotalMinutes > 30)
                    {
                        IdleTrayApp.lastSkippedDimNotificationTime = now;
                        BalloonForm.ShowBalloon("Przygaszanie pominięte", $"Obecna jasność ({currentBrightness}%) mniejsza niż zadana ({dimBrightnessPercent}%).", 10000, BalloonForm.BalloonStyle.NO_ICONS);
                    }
                    

                    return;
                }

                if (Application.OpenForms.OfType<BrightnessForm>().Any())
                {
                    LogIdle("ShowDimForm()try GetCurrentBrightness ❌ Odrzucono przygaszanie – BrightnessForm jest aktywny");
                    return;
                }

                if (currentBrightness != dimBrightnessPercent)
                {
                    File.WriteAllText(brightnessPath, currentBrightness.ToString());
                    LogIdle($"[PRELOAD] Jasność zapisana do pliku: {currentBrightness}%");
                }
                else
                {
                    if (File.Exists(brightnessPath) && int.TryParse(File.ReadAllText(brightnessPath), out int saved))
                    {
                        LogIdle($"[PRELOAD] Pominięto zapis ({currentBrightness}%) – równa dimLevel, wczytano poprzednią: {saved}%");
                    }
                    else
                    {
                        LogIdle($"[PRELOAD] Pominięto zapis ({currentBrightness}%) – równa dimLevel, brak pliku – fallback będzie 75%");
                    }
                }

                int idleNow = GetIdleTime() / 1000;
                if (idleNow < idleThresholdRuntime)
                {
                    LogIdle($"❌ Przygaszanie anulowane – GetIdleTime tuż przed DimForm.ShowDialog() = {idleNow}s");
                    return;
                }
                if (Application.OpenForms.OfType<BrightnessForm>().Any())
                {
                    LogIdle("ShowDimForm()przed samym pokazaniem showdialog ❌ Odrzucono przygaszanie – BrightnessForm jest aktywny");
                    return;
                }


                // 🌘 Przygaszanie
                IdleTrayApp.PreparingToDim = true;
                form = new DimForm(screenOffAfterSecondsRuntime, idleSeconds, dimBrightnessPercent);
                LogIdle("🌑 DimForm.ShowDialog() → uruchamiam form.ShowDialog()");
                dimFormActive = true;
                form.ShowDialog();
                form.Dispose();
                LogIdle("🔙 DimForm.ShowDialog() → form zamknięta, wracam");
                UpdateTrayIcon();
            }
            catch (ObjectDisposedException)
            {
                LogIdle("⚠️ DimForm.ShowDialog() form już została zamknięta (ObjectDisposedException)");
                GlobalScreenOff = false;
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                LogIdle($"❌ DimForm.ShowDialog() WYJĄTEK: {ex}");
            }
            finally
            {

                await Task.Delay(20); // 💡 pozwól FormClosed się wykonać

                if (form?.WasClosedByUserInteraction == true)
                {
                    LogIdle($"✅ DimForm.ShowDialog()finally{{}}  Zamknięty przez użytkownika – idleCheckTimer enabled = {idleCheckTimer.Enabled}, Restartuję, Interval = {idleCheckTimer.Interval}");
                    idleCheckTimer.Start();

                }
                else if (!WaitForUserActivity)
                {
                    LogIdle("▶️ DimForm.ShowDialog()finally{} WaitForUserActivity = FALSE - Restartuję idleCheckTimer ");
                    idleCheckTimer.Start();

                }
                else
                {
                    LogIdle("🛑 DimForm.ShowDialog()finally{} WaitForUserActivity = TRUE - Restartuję idleCheckTimer (finally)");
                }

                dimFormActive = false;
                dimFormIsOpen = false;
            }
        }


        public static void ClearWakeState()
        {
            
            idleSeconds = 0;
            lastIdleTime = -1;
            GlobalScreenOff = false;
            WaitForUserActivity = false;
            ResetIdle();
            try
            {
                Instance?.SetKeyboardBacklightBasedOnBrightness(Instance?.lastKnownBrightness ?? 0);
            }
            catch (Exception ex)
            {
                LogIdle($"🎹 Błąd przywracania klawiatury: {ex.Message}");
            }
        }


        private int GetIdleTime()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            GetLastInputInfo(ref lii);
            return Environment.TickCount - (int)lii.dwTime;
        }


        public static void ResetIdle()
        {
            if (GlobalScreenOff && !isPopupResetInProgress)
            {
                LogIdle("🛑 Pomijam ResetIdle → ekran i tak się wyłączy zaraz (GlobalScreenOff = true)");
                return;
            }

            mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
            LogIdle("ResetIdle() → zasymulowano MOUSEEVENTF_MOVE (Δx=0, Δy=0)");
            DimForm.OnGlobalReset?.Invoke();
        }


        public static void ResetByPopup()
        {
            LogIdle("Reset przez popup");
            isPopupResetInProgress = true;
            try
            {
                ClearWakeState();

            }
            finally
            {
                isPopupResetInProgress = false;
            }

            idleSeconds = 0;
            GlobalScreenOff = false;
            WaitForUserActivity = false;
            
        }


        public void StartJavaFollowUpSequence()
        {
            const int FollowUpStartDelayMinutes = 5;
            const int FollowUpCheckIntervalSeconds = 90;
            const int InactivityThreshold = 420;

            LogIdle("📡 Rozpoczynam sekwencję monitorowania po zamknięciu FormVideoPlayer.");

            // 🧹 Ubij poprzedni follow-up timer
            javaFollowUpTimer?.Dispose();

            javaFollowUpTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (Process.GetProcessesByName("javaw").Length == 0)
                    {
                        LogIdle("🟥 Java już nie istnieje – przerywam sekwencję follow-up.");
                        javaFollowUpTimer?.Dispose();
                        return;
                    }

                    if (javaWatcher == null || !monitorJavaDialog)
                        return;

                    if (javaWatcher.VisibleNow)
                    {
                        LogIdle("✅ Okno Java 'Oczekiwanie na wiadomość' wróciło – przerywam sekwencję follow-up.");
                        javaFollowUpTimer?.Dispose();
                        return;
                    }

                    int idleSeconds = GetIdleTime() / 1000;

                    if (idleSeconds > InactivityThreshold)
                    {
                        LogIdle($"🔔[Z_z_z]😴🛌 Wiadomość wisi, brak aktywności od {idleSeconds / 60} min – uruchamiam alert.mp4 + notif.mp3");

                        DisplayControl.TurnOn();
                        string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert.mp4");

                        UISyncContext?.Post(__ =>
                        {
                            ResetByPopup();
                            var videoForm = new FormVideoPlayer(videoPath);
                            videoForm.Show();
                        }, null);

                        javaFollowUpTimer?.Dispose();
                        return;
                    }

                    LogIdle($"🔔👀🙋 Wiadomość wisi, ale użytkownik aktywny ({idleSeconds}s temu) – gram pending.wav");
                    BalloonForm.ShowBalloon("Panelo czeka!", "Nowa wiadomość oczekuje na odpisanie", 10000);
                    PlayCustomSound("pending.wav");
                }
                catch (Exception ex)
                {
                    LogIdle($"❌ Błąd w Java follow-up: {ex.Message}");
                    javaFollowUpTimer?.Dispose();
                }
            },
            null,
            TimeSpan.FromMinutes(FollowUpStartDelayMinutes),
            TimeSpan.FromSeconds(FollowUpCheckIntervalSeconds));
        }


        private void UpdateJavaWatcherState()
        {
            if (javaWatcher == null)
                return;

            // JavaWatcher działa, jeśli którakolwiek z opcji jest zaznaczona
            bool shouldRun = monitorJavaDialog || paneloErrorNotifyEnabled;

            javaWatcher.ShouldRun = shouldRun;

            if (shouldRun)
            {
                LogIdle("▶️ JavaWatcher: start monitorowania");
                javaWatcher.StartLoopingMonitor();

                if (javaWatchdogTimer == null)
                {
                    javaWatchdogTimer = new System.Threading.Timer(_ =>
                    {
                        try
                        {
                            if (!(monitorJavaDialog || paneloErrorNotifyEnabled) || javaWatcher == null)
                                return;

                            var diff = DateTime.Now - javaWatcher.LastTickTime;

                            if (diff.TotalMinutes > 2)
                            {
                                LogIdle("🐶 Watchdog Java: brak ticka – restartuję monitorowanie.");
                                javaWatcher.StartLoopingMonitor();
                            }
                            else
                            {
                                LogIdle("🐶 Watchdog Java: tick aktualny.");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogIdle($"❌ Watchdog Java: błąd – {ex.Message}");
                        }
                    }, null, 0, 2 * 60 * 1000);
                }
            }
            else
            {
                LogIdle("⛔ JavaWatcher: zatrzymuję monitorowanie i kasuję watchdog");
                javaWatchdogTimer?.Dispose();
                javaWatchdogTimer = null;
            }
        }



        private void PlayCustomSound(string fileName)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                if (!File.Exists(path))
                {
                    LogIdle($"❌ Brak pliku dźwiękowego: {fileName}");
                    return;
                }

                using (var audioFile = new NAudio.Wave.AudioFileReader(path))
                using (var outputDevice = new NAudio.Wave.WaveOutEvent())
                {
                    outputDevice.Init(audioFile);
                    outputDevice.Volume = 1.0f; // 🔊 ZAWSZE na 100% przed startem
                    outputDevice.Play();

                    // mały delay, żeby dźwięk zdążył się odegrać (bo using inaczej zamknie za wcześnie)
                    while (outputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                        System.Threading.Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                LogIdle($"❌ Błąd przy odtwarzaniu dźwięku {fileName}: {ex.Message}");
            }
        }


        public Bitmap GetTrayIconBitmapSafe()
        {
            try { return trayIcon?.Icon?.ToBitmap(); }
            catch { return null; }
        }




          //**********************************//
         // INTERTOP, STRUCTS, DLLIMPORTY
        //**********************************//



        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        public static class FlashWindowHelper
        {
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

            public const uint FLASHW_ALL = 3;
            // public const uint FLASHW_TIMERNOFG = 12;

            public static void Flash(System.Windows.Forms.Form form)
            {
                FLASHWINFO fw = new FLASHWINFO();

                fw.cbSize = Convert.ToUInt32(Marshal.SizeOf(fw));
                fw.hwnd = form.Handle;
                fw.dwFlags = FLASHW_ALL;//| FLASHW_TIMERNOFG;
                fw.uCount = 3; // 3 razy
                fw.dwTimeout = 0;

                FlashWindowEx(ref fw);
            }
        }




          //**********************************//
         // HOOKI
        //**********************************//


        #region interop GlobalKeyboardHook

        private static IntPtr _hookID = IntPtr.Zero;
        private readonly static LowLevelKeyboardProc _proc = HookCallback;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }

        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {

                if (dimFormActive)
                {
                    LogIdle($"🧲 Hook: Wciśnięto klawisz przy aktywnym DimForm");
                    // sprawdź, czy DimForm jest otwarty
                    foreach (Form f in Application.OpenForms)
                    {
                        if (f is DimForm dim && f.Visible)
                        {
                            dim.Invoke(new Action(() =>
                            {
                                dim.CheckAndClose(null, EventArgs.Empty);
                            }));
                            break;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #endregion


        #region Globalny hook myszy do zamykania BrightnessForm
        public static class MouseHook
        {
            private static IntPtr _hookID = IntPtr.Zero;
            private static LowLevelMouseProc _proc;
            private static Action<Point, MouseButtons> _onClick;

            public static void Start(Action<Point, MouseButtons> onClick)
            {
                Stop();
                _onClick = onClick;
                _proc = HookCallback;
                _hookID = SetHook(_proc);
            }

            public static void Stop()
            {
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }
            }

            private static IntPtr SetHook(LowLevelMouseProc proc)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_MOUSE_LL, proc,
                        GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0 &&
                    (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    _onClick?.Invoke(new Point(hookStruct.pt.x, hookStruct.pt.y),
                        wParam == (IntPtr)WM_LBUTTONDOWN ? MouseButtons.Left : MouseButtons.Right);
                }

                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            private const int WH_MOUSE_LL = 14;
            private const int WM_LBUTTONDOWN = 0x0201;
            private const int WM_RBUTTONDOWN = 0x0204;

            [StructLayout(LayoutKind.Sequential)]
            private struct POINT { public int x; public int y; }

            [StructLayout(LayoutKind.Sequential)]
            private struct MSLLHOOKSTRUCT
            {
                public POINT pt;
                public int mouseData, flags, time;
                public IntPtr dwExtraInfo;
            }

            [DllImport("user32.dll")]
            private static extern IntPtr SetWindowsHookEx(int idHook,
                LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll")]
            private static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll")]
            private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);
        }
        #endregion




    }
}
