
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;




namespace DimScreenSaver
{


    public class IdleTrayApp : ApplicationContext
    {

              
        // 1. 🔄 Jasność / poziomy podświetlenia
        public int dimBrightnessPercent = 1;
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
        private static System.Windows.Forms.Timer brightnessRetryTimer;
        private static bool isBrightnessCheckRunning = false;
        private static int retryBrightnessTarget = -1;

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
        private BrightnessForm brightnessForm;

        // 4. 🧠 Synchronizacja, kontrolery, obserwatorzy
        public static SynchronizationContext UISyncContext;
        private JavaDialogWatcher javaWatcher;
        private System.Threading.Timer javaFollowUpTimer;
        private PowerBroadcastWatcher powerWatcher;
        public bool monitorJavaDialog = true;
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
        private static readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DimScreenSaver", "settings.cfg");
        private static readonly string BrightnessPath = Path.Combine(Path.GetTempPath(), "brightness.txt");

        public static IdleTrayApp Instance { get; private set; }
        private static void Log(string msg) => AppLogger.Log("IdleTray", msg);



      



        
  


        


        //********************************//
        // LOGI    
        //********************************//

        private static readonly object logLock = new object();
        private static int logCounter = 0;
        private const int TrimFrequency = 45; // co 45 wpisów przytnij plik
        private const int MaxLines = 5000;
        private static string logFile = Path.Combine(Path.GetTempPath(), "scrlog.txt");

        private static void LogIdle(string message)
        {
            try
            {
                string entry = $"[IdleTray] {DateTime.Now:HH:mm:ss} {message}";
                lock (logLock)
                {
                    File.AppendAllText(logFile, entry + Environment.NewLine);
                    logCounter++;

                    if (logCounter % TrimFrequency == 0)
                        TrimLogFile();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogIdle] Błąd logowania: {ex.Message}");
            }
        }

        private static void TrimLogFile()
        {
            try
            {
                if (!File.Exists(logFile)) return;

                var lines = File.ReadAllLines(logFile);
                if (lines.Length <= MaxLines) return;

                var trimmed = lines.Skip(lines.Length - MaxLines).ToArray();
                File.WriteAllLines(logFile, trimmed);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogIdle] Błąd przycinania loga: {ex.Message}");
            }
        }




        //********************************//
        // KONSTRUKTOR I INICJALIZACJA    
        //********************************//

        public IdleTrayApp() //konstruktor
        {
            Instance = this;
            powerWatcher = new PowerBroadcastWatcher();
            Log("Program uruchomiony");
            guiInvoker.CreateControl();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));



          
           


          StartWmiBrightnessHook();

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
                    int b = IdleTrayApp.Instance?.lastKnownBrightness ?? -1;
                    int current = b >= 0 && b <= 100 ? b : 75;

                    IdleTrayApp.Instance.brightnessForm?.Close();
                    IdleTrayApp.Instance.brightnessForm = new BrightnessForm(current);
                    IdleTrayApp.Instance.brightnessForm.Show();
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
                Log("🔕 Start: tryb tymczasowego wyłączenia aktywny – zatrzymuję idleCheckTimer.");
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

                            Log("🐶 Watchdog Idle: Pomijam sprawdzanie ticka przez: " + string.Join(", ", powody));
                            return;
                        }

                        var last = lastIdleTickTime;
                        if (last == null)
                        {
                            Log("🐶 Watchdog Idle: brak danych o ticku – nie robię nic.");
                            return;
                        }

                        var diff = DateTime.Now - last.Value;
                        if (diff.TotalMinutes > 1)
                        {
                            System.Threading.Thread.Sleep(100);
                            Log("💣 Watchdog Idle: brak ticka – restartuję aplikacje.");
                            trayIcon.Visible = false;
                            Application.Restart();
                            Environment.Exit(0);
                        }
                        else
                        {
                            Log("🐶 Watchdog Idle: tick aktualny.");
                        
                            // UpdateJavaWatcherState();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Watchdog Idle: błąd – {ex.Message}");
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
                    Log("✅ JavaWatcher wykrył powrót Panelo – anuluję follow-up.");
                    javaFollowUpTimer?.Dispose();

                    if (CurrentFormVideoPlayer != null && !CurrentFormVideoPlayer.IsDisposed)
                    {
                        try
                        {
                            Log("FormVideoPlayer aktywny, zamykam przez SpróbujZamknąć - powrót Panelo");
                            CurrentFormVideoPlayer.Invoke(new MethodInvoker(() =>
                            {
                                CurrentFormVideoPlayer?.SpróbujZamknąć("powrót Panelo");
                            }));
                        }
                        catch (Exception ex)
                        {
                            Log($"❌ Błąd przy zamykaniu FormVideoPlayer po powrocie Panelo: {ex.Message}");
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
                    
                    
                    ClearWakeState();
                    



                    /*
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
                    }));*/
                }

            };

            MonitorStateWatcher.OnMonitorTurnedOff += () =>
            {
                

                if (dimFormActive)
                {
                    Log("🧼 MonitorStateWatcher.OnMonitorTurnedOff - DimForm aktywny – zamykam go przez CloseFromScreenOff");
                    
                    Application.OpenForms
                        .OfType<DimForm>()
                        .FirstOrDefault()
                        ?.CloseFromScreenOff();
                }
                else
                {
                    Log("🔴 MonitorStateWatcher.OnMonitorTurnedOff - DimForm nieaktywny – nie zamykam");
                }
            };

            _hookID = SetHook(_proc);
            Log("🧲 Globalny hook klawiatury aktywowany");

            Application.ApplicationExit += (s, e) =>
            {
                try
                {
                    UnhookWindowsHookEx(_hookID);
                    trayIcon.Visible = false;
                    idleCheckTimer.Stop();
                    wmiWatcher?.Stop();
                    wmiWatcher?.Dispose();
                    Log("🧹 Cleanup – odłączono globalny hook klawiatury i WMI, zamknięto ikonkę, zatrzymano timer");
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
                SetKeyboardBacklightBasedOnBrightnessForce(brightness);
                lastKnownBrightness = brightness;
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd inicjalizacji KeyboardController: {ex.Message}");
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
            Log($"Wczytano config: {idleThresholdConfig}|{screenOffAfterSecondsConfig}|{dimBrightnessPercent}|{(wakeOnAudio ? 1 : 0)}|{(isTemporarilyDisabled ? 1 : 0)}|{(monitorJavaDialog ? 1 : 0)}|{(paneloErrorNotifyEnabled ? 1 : 0)}");

            Log($"[CONFIG APPLIED] tempIdleThreshold: {idleThresholdRuntime}, tempScreenOffAfterSeconds: {screenOffAfterSecondsRuntime}");
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
                Log($"Zapisano config: {idleThresholdConfig}|{screenOffAfterSecondsConfig}|{dimBrightnessPercent}|{(wakeOnAudio ? 1 : 0)}|{(disableItem?.Checked == true ? 1 : 0)}|{(monitorJavaDialog ? 1 : 0)}|{(paneloErrorNotifyEnabled ? 1 : 0)}");



            }
            catch (Exception ex)
            {
                Log($"Błąd zapisu configu: {ex.Message}");
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
                    Log($"📥 Wczytano mapę z ustawień: {saved}");
                }
                catch (Exception ex)
                {
                    Log($"❌ Błąd parsowania mapy z ustawień: {ex.Message}");
                    brightnessToLevelMap = new List<(int, int, int)>();
                }
            }
            else
            {
                Log("⚠️ Brak zapisanej mapy – ustawiam domyślną");
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
                Log("\uD83D\uDD01 Testowy przycisk → symuluję zniknięcie okna Java");
                ResetByPopup();
                string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert.mp4");
                var videoForm = new FormVideoPlayer(videoPath);
                videoForm.Show();
            };
            menu.Items.Insert(0, simulateJavaItem);*/

            /*
            var stopIdleTickItem = new ToolStripMenuItem("⏸️ Test: zatrzymaj idleCheckTimer");
            StyleMenuItem(stopIdleTickItem);
            stopIdleTickItem.Click += (s, e) =>
            {
                Log("⏸️ Testowy przycisk → zatrzymuję idleCheckTimer");

                if (idleCheckTimerPublic != null)
                {
                    idleCheckTimerPublic.Stop();
                    idleCheckTimerPublic.Tick -= IdleCheckTimer_Tick;
                    idleCheckTimerPublic.Dispose();
                    idleCheckTimerPublic = null;
                }

                idleCheckTimer = null;
            };
            menu.Items.Add(stopIdleTickItem);
            */


            /*
            var zmienjasnosc = new ToolStripMenuItem("💡 Test: ustaw jasność na 50%");
            StyleMenuItem(zmienjasnosc);
            zmienjasnosc.Click += async (s, e) =>
            {
                Log("💡 Testowy przycisk → ustawiam jasność na 50%");
                await SetBrightnessAsync(50);
            };
            menu.Items.Add(zmienjasnosc);
            */


            /*

            // 🧪 Test czkawki ticka
            var simulateCzkawkaItem = new ToolStripMenuItem("🧪 Testuj czkawkę Ticka (10x recreate timer)");
            StyleMenuItem(simulateCzkawkaItem);
            simulateCzkawkaItem.Click += (s, e) =>
            {
                Log("🧪 Ręczny test: próbuję odtworzyć timer 10x z rzędu");

                for (int i = 0; i < 500; i++)
                {
                    idleCheckTimerPublic = new System.Windows.Forms.Timer { Interval = 7000 };
                    idleCheckTimerPublic.Tick += IdleCheckTimer_Tick;
                    idleCheckTimerPublic.Start();
                    idleCheckTimer = idleCheckTimerPublic;
                    Log($"♻️ [{i + 1}/10] Próba odtworzenia Timer NA GUI wątku.");
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
            AddBrightnessOption("~0%", 1);
            AddBrightnessOption("~5%", 6);
            AddBrightnessOption("~10%", 11);
            AddBrightnessOption("~15%", 16);
            AddBrightnessOption("~20%", 21);
            AddBrightnessOption("~25%", 26);
            AddBrightnessOption("~30%", 31);
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
                        Log($"🎹 Auto-klawiatura włączona → ustawiam podświetlenie");
                        SetKeyboardBacklightBasedOnBrightnessForce(brightness);

                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Błąd przy włączaniu auto-klawiatury: {ex.Message}");
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
                    Log("\uD83D\uDEA9 PaneloErrorNotify → opcja odznaczona – zatrzymuję dźwięk i resetuję licznik błędu");
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

                Log($"KLIK! Checked: {disableItem.Checked}, isTemporarilyDisabled: {isTemporarilyDisabled}");

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
                Log("⏰ Budzik cykliczny wyłączony");
                return;
                
            }

            Log($"⏰ Ustawiam budzik cykliczny co {wakeupIntervalMinutes} minut.");

            wakeupTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    Log("⏰ Budzik cykliczny – odpalam budzik.mp4");
                    UISyncContext?.Post(__ =>
                    {
                        string label = wakeupIntervalMinutes == -1 ? "Wyłączony" : $"{wakeupIntervalMinutes} min";
                        BalloonForm.ShowBalloon("Dzwoni budzik", $"Obecnie ustawiony co: {label}", 10000);
                        ResetByPopup();
                        string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "budzik.mp4");
                        if (CurrentFormWakeup != null && !CurrentFormWakeup.IsDisposed)
                        {
                            Log("🚫 FormWakeup już aktywny – nie tworzę nowego");
                            return;
                        }

                        CurrentFormWakeup = new FormWakeup(videoPath);
                        CurrentFormWakeup.Show();
                    }, null);
                }
                catch (Exception ex)
                {
                    Log($"❌ Błąd w budziku cyklicznym: {ex.Message}");
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


        public static async Task<int> GetCurrentBrightnessAsync(int timeoutMs = 1000)
        {
            Stopwatch sw = null;
            try
            {
                Log($"🕵️ Start GetCurrentBrightnessAsync (ustawiony timeout {timeoutMs} ms)");

                sw = Stopwatch.StartNew();
                var brightnessTask = Task.Run(() =>
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
                        Log($"❌ Wyjątek w Task.Run GetCurrentBrightness: {ex.Message}");
                    }

                    return -1; // <-- oznacza błąd, obsłużymy niżej
                });

                if (await Task.WhenAny(brightnessTask, Task.Delay(timeoutMs)) == brightnessTask)
                {
                    int result = await brightnessTask;
                    sw.Stop();
                    if (result >= 0 && result != Instance?.dimBrightnessPercent)
                    {
                        Log($"← Zakończono pobieranie jasności: {result}%, {sw.ElapsedMilliseconds} ms");
                        return result;
                    }
                    else
                    {
                        Log($"⚠️ Jasność z taska = {result}% → fallback dim{dimFormIsOpen}");
                        return await LoadBrightnessFallback(sw.ElapsedMilliseconds);
                    }
                }
                else
                {
                    sw.Stop();
                    Log($"⏱ Ustawiony timeout ({timeoutMs}) ms (rzeczywiste: {sw.ElapsedMilliseconds}) – fallback");
                    return await LoadBrightnessFallback(sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                sw?.Stop();
                Log($"❌ Błąd GetCurrentBrightnessAsync: {ex.Message}, {sw?.ElapsedMilliseconds ?? -1} ms");
                return await LoadBrightnessFallback(sw?.ElapsedMilliseconds ?? -1); // <-- TEŻ MUSI BYĆ await
            }
        }


        public async Task RestoreBrightnessWithBatterySaverCompensation(int desiredBrightness)
        {

            if (!BatterySaverChecker.IsBatterySaverActive())
            {
                Log($"🔋 Battery saver NIEaktywny – ustawiam jasność {desiredBrightness}% bez korekty");
                await SetBrightnessAsync(desiredBrightness);
                return;
            }

            int compensated = Math.Min(100, (int)Math.Ceiling(desiredBrightness / 0.7));

            Log($"🔋 Battery saver aktywny – ustawiam {compensated}% (kompensacja 30%)");
            int compensatedb4 = compensated;
            await SetBrightnessAsync(compensated);
            BalloonForm.ShowBalloon("Kompensuję jasność...", $"Oczekiwana:\u00A0{desiredBrightness}%,\u00A0ustawiam:\u00A0{compensated}%",4000, showIcons: false, "Sys. Oszczędzanie baterii wł. - kompensacja 🔆");
            await Task.Delay(3000);

            int current = await GetCurrentBrightnessAsync(500);
            double actualDrop = current / compensated;
            if (current == desiredBrightness)
            {
                Log($"✅ Kompensacja zadziałała – Windows obniżył jasność do {current}%");
                Log($"📉 Współczynnik spadku jasności (runtime): {actualDrop:F2}");
                BalloonForm.ShowBalloon($"Sukces! Kompensacja jasności udana!", $"Oczekiwano:\u00A0{desiredBrightness}%,\u00A0aktualnie:\u00A0{current}%", 12000, showIcons: false, "Sys. Oszczędzanie baterii wł. - kompensacja 🔆");
            }
            else if (current == compensated)
            {
                Log($"⚠️ Windows NIE obniżył jasności – ustawiam ręcznie {desiredBrightness}%");
                Log($"📉 Współczynnik spadku jasności (runtime): {actualDrop:F2}");
                BalloonForm.ShowBalloon("Windows nie obniżył jasności", $"Przywracam\u00A0oczekiwane:\u00A0{desiredBrightness}%", 12000, showIcons: false, "Sys. Oszczędzanie baterii wł. - kompensacja 🔆");
               
            
            await SetBrightnessAsync(desiredBrightness);
            }
            else
            {
               
                Log($"📉 Współczynnik spadku jasności (runtime): {actualDrop:F2}");
                Log($"❓ Jasność po kompensacji to {current}%, oczekiwano {desiredBrightness}% – nic nie robię");
                BalloonForm.ShowBalloon("Kompensacja nieudana - błąd współczynnika", $"Jasność:\u00A0{current}%,\u00A0oczekiwano\u00A0{desiredBrightness}%\u00A0–\u00A0ignoruję", 5000, showIcons: true, "Sys. Oszczędzanie baterii wł. - kompensacja 🔆");
            }
        }




        private static async Task TrySetBrightness(int value)
        {
            try
            {
                if (Instance != null)
                    Instance.lastKnownBrightness = value;
                WaitForUserActivity = false;
                GlobalScreenOff = false;
                idleCheckTimerPublic?.Start();
                lastIdleTime = -1;

                await SetBrightnessAsync(value);

                await Task.Delay(500);
                int current = await GetCurrentBrightnessAsync(300);
                if (current != value)
                {
                    Log($"⚠️ Jasność po ustawieniu to {current}%, oczekiwano {value}% – ponawiam próbę");
                    await SetBrightnessAsync(value);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ TrySetBrightness – wyjątek: {ex.Message}");
            }
        }


        private static async Task<int> LoadBrightnessFallback(long elapsedMs)
        {
            // 1. lastKnownBrightness
            int last = Instance?.lastKnownBrightness ?? -1;
            if (last >= 0 && last != Instance?.dimBrightnessPercent)
            {
                Log($"📥 Fallback: używam lastKnownBrightness = {last}% i ustawiam jasność");
                await TrySetBrightness(last);

                return last;
            }

            // 2. z pliku
            try
            {
                if (File.Exists(BrightnessPath) && int.TryParse(File.ReadAllText(BrightnessPath), out int fromFile))
                {
                    if (fromFile != Instance?.dimBrightnessPercent)
                    {
                        Log($"📁 Fallback: używam z pliku brightness.txt: {fromFile}%");
                        await TrySetBrightness(fromFile);
                        return fromFile;
                    }
                    else
                    {
                        Log($"📁 Fallback: odczytano {fromFile}% z pliku, ale równe dimBrightnessPercent → pomijam");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"📁 Błąd odczytu z pliku brightness.txt: {ex.Message}");
            }

            // 3. domyślnie
            Log("🕳️ Fallback: brak danych – ustawiam domyślne 70%");
            await TrySetBrightness(70);
           
            return 70;
        }






        public static async Task SetBrightnessAsync(int brightness)
        {
            await Task.Run((Action)(() =>
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
                    IdleTrayApp.Log($"[WMI] Błąd ustawiania jasności: {ex.Message}");
                }
            }));
        }


        public void SetLastKnownBrightness(int value)
        {
            if (value >= 0 && value <= 100)
                lastKnownBrightness = value;
            Log($"Ustawiam lastKnownBrightness na {value}");
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
        public void SetKeyboardBacklightBasedOnBrightnessForce(int brightness)
        {
            if (!keyboardAutoEnabled || keyboard == null)
                return;

            int level = GetBacklightLevelForBrightness(brightness);
            
            keyboard.Set(level);
            Log($"🎹 SetKeyboard..Force(): Jasność {brightness}% → Poziom {level}");
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
            bool skip = false;
            var reasons = new List<string>();

            if (!keyboardAutoEnabled) reasons.Add("keyboardAutoDisabled");
            if (keyboard == null) reasons.Add("keyboard=null");
            if (dimFormIsOpen) reasons.Add("dimFormIsOpen");
            if (GlobalScreenOff) reasons.Add("GlobalScreenOff=true");

            if (reasons.Any())
            {
                Log($"🎹 SetKeyboard..Tick(): Pomijam podświetlenie → {string.Join(", ", reasons)}");
                return;
            }

            int level = GetBacklightLevelForBrightness(brightness);

            if (level != lastBacklightLevel)
            {
                try
                {
                    keyboard.Set(level);
                    Log($"🎹 SetKeyboard..Tick():  Jasność {brightness}% → Poziom {level}");
                    lastBacklightLevel = level;
                }
                catch (Exception ex)
                {
                    Log($"❌ Błąd ustawiania klawiatury: {ex.Message}");
                }
            }
        }




        //**********************************//
        // TICK, IDLE, DIMFORM, LOGIKA, INNE
        //**********************************//

        private static int _tickCounter = 0;

        private void SafeStartIdleCheckTimer()
        {
            if (idleCheckTimer != null && !idleCheckTimer.Enabled)
            {
                try
                {
                    Log("Startuje Timer z SafeStartIdleCheckTimer()");
                    idleCheckTimer.Start();
                }
                catch (ObjectDisposedException)
                {
                    Log("⚠️ Timer był już disposed – nie można go uruchomić.");
                    // TODO: Odtwórz timer jeśli to potrzebne
                }
            }
        }


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
                    Log("💣 Wykryto nadmiarowe ticki. Restartuje aplikacje.");
                    trayIcon.Visible = false;
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
            if (isTickRunning)
            {
                Log("Tick is running. wychodze.");
                return;
            }
            isTickRunning = true;

            try
            {
                if (WaitForUserActivity)
            {
                int idleNow = GetIdleTime() / 1000;
                bool audioActive = wakeOnAudio && AudioWatcher.IsAudioPlaying();

                    Log($"[MAIN TICK] AudioActive = {audioActive} (wakeOnAudio = {wakeOnAudio})");

                if (idleNow == 0 || idleNow < lastIdleTime || audioActive)
                {
                        Log($"[MAIN TICK] Kończę Oczekiwanie na aktywność – idleNow={idleNow}, lastIdleTime={lastIdleTime}");
                    WaitForUserActivity = false;
                    GlobalScreenOff = false;
                    idleCheckTimerPublic?.Start();
                    UpdateTrayIcon();
                    lastIdleTime = -1;
                    return;
                }

                lastIdleTime = idleNow;
                    Log($"[MAIN TICK] Oczekiwanie na aktywność... idle: {idleNow}s");
                return;
            }

            if (GlobalScreenOff)
            {
                    Log("[MAIN TICK] ⛔ Tick pominięty – ekran wyłączony (GlobalScreenOff)");
                return;
            }

            lastIdleTickTime = DateTime.Now;

                /*   //ZASTĄPIONE HOOKIEM WMI
                if (!isBrightnessCheckRunning)
                {
                    isBrightnessCheckRunning = true;

                    _ = Task.Run((Func<Task>)(async () =>
                    {
                        try
                        {
                            int tickId = Interlocked.Increment(ref _tickCounter);
                            IdleTrayApp.Log($"🔎 [tick {tickId}] DEBUG PRZED GetCurrentBrightnessAsync ");
                            int brightness = await GetCurrentBrightnessAsync();
                            IdleTrayApp.Log($"🔎 [tick {tickId}] DEBUG PO GetCurrentBrightnessAsync");

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
                                IdleTrayApp.Log($"[MAIN TICK] ❌ Jasność poza zakresem: {brightness}");
                            }
                        }
                        catch (Exception ex)
                        {
                            IdleTrayApp.Log($"[MAIN TICK] ⚠️ Błąd podczas pobierania jasności: {ex.Message}");
                        }
                        finally
                        {
                            isBrightnessCheckRunning = false;
                        }
                    }));
                }
                else
                {
                    Log("⚠️ Pomiar jasności nadal trwa – pomijam kolejny GetCurrentBrightnessAsync.");
                } */ 

                int idle = GetIdleTime() / 1000;
            bool audioActiveNow = wakeOnAudio && AudioWatcher.IsAudioPlaying();

                if (wakeOnAudio && audioActiveNow)
                {
                    Log($"⏱️ [MAIN TICK] IDLE: prog: {idleSeconds}s, sys: {idle}s, THRESHOLD: dim:{idleThresholdRuntime}s off:{screenOffAfterSecondsRuntime}s | 🔊");
                }
                else
                {
                    Log($"⏱️ [MAIN TICK] IDLE: prog: {idleSeconds}s, sys: {idle}s, THRESHOLD: dim:{idleThresholdRuntime}s off:{screenOffAfterSecondsRuntime}s");
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
                    Log($"[MAIN TICK] 🌒 Ekran OFF przez IdleTrayApp | idle: {idleSeconds}");

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
                Log("❌ [ShowDimForm] Forma już otwarta – pomijam");

                
                return;
            }

            dimFormIsOpen = true;



            Log("🟢 [ShowDimForm] START → przygotowuję przygaszenie");
            

            int freshIdle = GetIdleTime() / 1000;
            if (freshIdle < idleThresholdRuntime)
            {
                Log($"❌ Odrzucono przygaszanie – nowy GetIdleTime = {freshIdle}s, poniżej progu.");

                SafeStartIdleCheckTimer();
                return;
            }



            DimForm form = null;

            try
            {
               
                int currentBrightness = await GetCurrentBrightnessAsync();
                lastKnownBrightness = currentBrightness;

                if (currentBrightness <= dimBrightnessPercent)
                {
                    Log($"⛔ Przygaszanie anulowane – obecna jasność ({currentBrightness}%) ≤ docelowa dim ({dimBrightnessPercent}%)");

                    var now = DateTime.Now;
                    var last = IdleTrayApp.lastSkippedDimNotificationTime;

                    if (last == null || (now - last.Value).TotalMinutes > 30)
                    {
                        IdleTrayApp.lastSkippedDimNotificationTime = now;
                        BalloonForm.ShowBalloon("Przygaszanie pominięte", $"Obecna jasność ({currentBrightness}%) mniejsza niż zadana ({dimBrightnessPercent}%).", 10000, showIcons: false);
                    }

                    SafeStartIdleCheckTimer();
                    return;
                }

                if (Application.OpenForms.OfType<BrightnessForm>().Any())
                {
                    Log("ShowDimForm()try GetCurrentBrightness ❌ Odrzucono przygaszanie – BrightnessForm jest aktywny");
                    SafeStartIdleCheckTimer();
                    return;
                }

                if (currentBrightness != dimBrightnessPercent)
                {
                    File.WriteAllText(BrightnessPath, currentBrightness.ToString());
                    Log($"[PRELOAD] Jasność zapisana do pliku: {currentBrightness}%");
                }
                else
                {
                    if (File.Exists(BrightnessPath) && int.TryParse(File.ReadAllText(BrightnessPath), out int saved))
                    {
                        Log($"[PRELOAD] Pominięto zapis ({currentBrightness}%) – równa dimLevel, wczytano poprzednią: {saved}%");
                    }
                    else
                    {
                        Log($"[PRELOAD] Pominięto zapis ({currentBrightness}%) – równa dimLevel, brak pliku – fallback będzie 75%");
                    }
                }

                int idleNow = GetIdleTime() / 1000;
                if (idleNow < idleThresholdRuntime)
                {
                    Log($"❌ Przygaszanie anulowane – GetIdleTime tuż przed DimForm.ShowDialog() = {idleNow}s");
                    SafeStartIdleCheckTimer();
                    return;
                }
                if (Application.OpenForms.OfType<BrightnessForm>().Any())
                {
                    Log("ShowDimForm()przed samym pokazaniem showdialog ❌ Odrzucono przygaszanie – BrightnessForm jest aktywny");
                    SafeStartIdleCheckTimer();
                    return;
                }


                // 🌘 Przygaszanie
                IdleTrayApp.PreparingToDim = true;
                form = new DimForm(screenOffAfterSecondsRuntime, idleSeconds, dimBrightnessPercent);
                Log("🌑 DimForm.ShowDialog() → uruchamiam form.ShowDialog()");
                dimFormActive = true;
                form.ShowDialog();
                form.Dispose();
                Log("🔙 DimForm.ShowDialog() → form zamknięta, wracam");
                UpdateTrayIcon();

               
            }
            catch (ObjectDisposedException)
            {
                Log("⚠️ DimForm.ShowDialog() form już została zamknięta (ObjectDisposedException)");
                GlobalScreenOff = false;
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                Log($"❌ DimForm.ShowDialog() WYJĄTEK: {ex}");
            }
            finally
            {

                await Task.Delay(20); // 💡 pozwól FormClosed się wykonać

                if (form?.WasClosedByUserInteraction == true)
                {
                    Log($"✅ DimForm.ShowDialog()finally{{}}  Zamknięty przez użytkownika – idleCheckTimer enabled = {idleCheckTimer.Enabled}, Restartuję, Interval = {idleCheckTimer.Interval}");
                    SafeStartIdleCheckTimer();

                }
                else if (!WaitForUserActivity)
                {
                    Log("▶️ DimForm.ShowDialog()finally{} WaitForUserActivity = FALSE - Restartuję idleCheckTimer ");
                    SafeStartIdleCheckTimer();

                }
                else
                {
                    Log("⏸️ DimForm.ShowDialog()finally{} WaitForUserActivity = TRUE");
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
            Instance?.SafeStartIdleCheckTimer();


            try
            {
                Instance?.SetKeyboardBacklightBasedOnBrightnessForce(Instance?.lastKnownBrightness ?? 0);

            }
            catch (Exception ex)
            {
                Log($"🎹 Błąd przywracania klawiatury: {ex.Message}");
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
                Log("🛑 Pomijam ResetIdle → ekran i tak się wyłączy zaraz (GlobalScreenOff = true)");
                return;
            }

            mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
            Log("ResetIdle() → zasymulowano MOUSEEVENTF_MOVE (Δx=0, Δy=0)");
            DimForm.OnGlobalReset?.Invoke();
        }


        public static void ResetByPopup()
        {
            Log("Reset przez popup");
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

            Log("📡 Rozpoczynam sekwencję monitorowania po zamknięciu FormVideoPlayer.");

            // 🧹 Ubij poprzedni follow-up timer
            javaFollowUpTimer?.Dispose();

            javaFollowUpTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (Process.GetProcessesByName("javaw").Length == 0)
                    {
                        Log("🟥 Java już nie istnieje – przerywam sekwencję follow-up.");
                        javaFollowUpTimer?.Dispose();
                        return;
                    }

                    if (javaWatcher == null || !monitorJavaDialog)
                        return;

                    if (javaWatcher.VisibleNow)
                    {
                        Log("✅ Okno Java 'Oczekiwanie na wiadomość' wróciło – przerywam sekwencję follow-up.");
                        javaFollowUpTimer?.Dispose();
                        return;
                    }

                    int idleSeconds = GetIdleTime() / 1000;

                    if (idleSeconds > InactivityThreshold)
                    {
                        Log($"🔔[Z_z_z]😴🛌 Wiadomość wisi, brak aktywności od {idleSeconds / 60} min – uruchamiam alert.mp4 + notif.mp3");

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

                    Log($"🔔👀🙋 Wiadomość wisi, ale użytkownik aktywny ({idleSeconds}s temu) – gram pending.wav");
                    BalloonForm.ShowBalloon("Panelo czeka!", "Nowa wiadomość oczekuje na odpisanie", 10000);
                    PlayCustomSound("pending.wav");
                }
                catch (Exception ex)
                {
                    Log($"❌ Błąd w Java follow-up: {ex.Message}");
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
                Log("▶️ JavaWatcher: start monitorowania");
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
                                Log("🐶 Watchdog Java: brak ticka – restartuję monitorowanie.");
                                javaWatcher.StartLoopingMonitor();
                            }
                            else
                            {
                                Log("🐶 Watchdog Java: tick aktualny.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"❌ Watchdog Java: błąd – {ex.Message}");
                        }
                    }, null, 0, 2 * 60 * 1000);
                }
            }
            else
            {
                Log("⛔ JavaWatcher: zatrzymuję monitorowanie i kasuję watchdog");
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
                    Log($"❌ Brak pliku dźwiękowego: {fileName}");
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
                Log($"❌ Błąd przy odtwarzaniu dźwięku {fileName}: {ex.Message}");
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


            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
              

                // Obsługa zamykania DimForm
                if (wParam == (IntPtr)WM_KEYDOWN && dimFormActive)
                {
                    Log("🧲 Hook: Wciśnięto klawisz przy aktywnym DimForm");
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


        #region HookWMI

        private ManagementEventWatcher wmiWatcher;
        private int referenceBrightness = -1;
     


        private void StartWmiBrightnessHook()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery(
                    "__InstanceModificationEvent",
                    new TimeSpan(0, 0, 1),
                    "TargetInstance ISA 'WmiMonitorBrightness'"
                );

                wmiWatcher = new ManagementEventWatcher(new ManagementScope("root\\wmi"), query);
                wmiWatcher.EventArrived += (s, e) =>
                {
                    var newInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                    int brightness = (byte)newInstance["CurrentBrightness"];

                    bool isBatterySaver = BatterySaverChecker.IsBatterySaverActive();
                    UISyncContext?.Post(_ =>
                    {
                        if (brightnessForm != null && brightnessForm.Visible)
                        {
                            if (brightnessForm.userInitiatedChange || brightnessForm.scrollInProgress)
                            {
                                Log("⛔ Pomijam aktualizację suwaka – użytkownik właśnie zmienia jasność.");

                                if (brightnessRetryTimer == null)
                                {
                                    brightnessRetryTimer = new System.Windows.Forms.Timer
                                    {
                                        Interval = 100
                                    };

                                    brightnessRetryTimer.Tick += (sd, ea) =>
                                    {
                                        if (brightnessForm != null && brightnessForm.Visible && !brightnessForm.userInitiatedChange)
                                        {
                                            Log("🔁 Ponawiam aktualizację suwaka – użytkownik już skończył zmieniać.");

                                            _ = Task.Run(async () =>
                                            {
                                                int currentBrightness = await IdleTrayApp.GetCurrentBrightnessAsync();

                                                UISyncContext?.Post(__ =>
                                                {
                                                    if (brightnessForm == null || !brightnessForm.Visible)
                                                        return;

                                                    if (BatterySaverChecker.IsBatterySaverActive())
                                                    {
                                                        _ = brightnessForm.AnimateSliderTo(currentBrightness);
                                                        brightnessForm.PulseBatterySaverIcon();
                                                    }
                                                    else
                                                    {
                                                        Log("🐱🐱🐱pseudonim KOTEK🐱🐱🐱🐱🐱🐱 ");
                                                        brightnessForm.slider.Value = currentBrightness;
                                                        brightnessForm.valueLabel.Text = currentBrightness.ToString();
                                                    }
                                                }, null);
                                            });

                                            brightnessRetryTimer.Stop();
                                        }
                                    };
                                }


                                brightnessRetryTimer.Stop(); // reset od nowa
                                brightnessRetryTimer.Start();
                            }

                            else
                            {
                                _ = Task.Run(async () =>
                                {
                                    int currentBrightness = await IdleTrayApp.GetCurrentBrightnessAsync();

                                    UISyncContext?.Post(__ =>
                                    {
                                        if (brightnessForm == null || !brightnessForm.Visible)
                                            return;

                                        if (isBatterySaver)
                                        {
                                            _ = brightnessForm.AnimateSliderTo(currentBrightness);
                                            brightnessForm.PulseBatterySaverIcon();
                                        }
                                        else
                                        {
                                            Log("🐭🐭🐭pseudonim MYSZKA🐭🐭🐭🐭🐭🐭🐭 ");
                                            //_ = brightnessForm.AnimateSliderTo(currentBrightness);
                                            brightnessForm.slider.Value = currentBrightness;
                                            brightnessForm.valueLabel.Text = currentBrightness.ToString();
                                        }

                                    }, null);
                                });
                            }

                        }

                        if (formOptions?.labelCurrentBrightness != null)
                        {
                            formOptions.labelCurrentBrightness.Text = $"{brightness}%";
                        }

                        // ❗️NIE może być pominięte – nawet jeśli pominięto slider
                        SetKeyboardBacklightBasedOnBrightnessTick(brightness);
                        lastKnownBrightness = brightness;

                    }, null);

                };

                wmiWatcher.Start();
                Log("✅ WMI brightness hook aktywny.");
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd WMI hooka: {ex.Message}");
            }
        }


        #endregion


        #region Globalny hook myszy do BrightnessForm
        public static class MouseHook
        {
            private static IntPtr _hookID = IntPtr.Zero;
            private static LowLevelMouseProc _proc;
            private static Action<Point, MouseButtons, bool> _onMouseEvent;

            public static void Start(Action<Point, MouseButtons, bool> onMouseEvent)
            {
                Stop();
                _onMouseEvent = onMouseEvent;
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
                if (nCode >= 0)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    Point pt = new Point(hookStruct.pt.x, hookStruct.pt.y);
                    MouseButtons btn = MouseButtons.None;
                    bool isDown = false;

                    switch ((int)wParam)
                    {
                        case WM_LBUTTONDOWN:
                            btn = MouseButtons.Left;
                            isDown = true;
                            break;
                        case WM_LBUTTONUP:
                            btn = MouseButtons.Left;
                            isDown = false;
                            break;
                        case WM_RBUTTONDOWN:
                            btn = MouseButtons.Right;
                            isDown = true;
                            break;
                        case WM_RBUTTONUP:
                            btn = MouseButtons.Right;
                            isDown = false;
                            break;
                    }

                    if (btn != MouseButtons.None)
                    {
                        _onMouseEvent?.Invoke(pt, btn, isDown);
                    }
                }

                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            private const int WH_MOUSE_LL = 14;
            private const int WM_LBUTTONDOWN = 0x0201;
            private const int WM_LBUTTONUP = 0x0202;
            private const int WM_RBUTTONDOWN = 0x0204;
            private const int WM_RBUTTONUP = 0x0205;

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
