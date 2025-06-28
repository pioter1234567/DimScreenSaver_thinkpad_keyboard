using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using static DimScreenSaver.IdleTrayApp;

namespace DimScreenSaver
{
    internal static class Program
    {
        public const string RestartEventName = "DimScreenSaver_HotRestartEvent";

        private static void Log(string msg) => _ = AppLogger.LogAsync("Program", msg);

        [STAThread]
        static void Main(string[] args)
        {
            // Sprawdź, czy to jest start po hot-restartcie
            bool isRestart = args.Length > 0 && args[0].Equals("/restart", StringComparison.OrdinalIgnoreCase);

            // Utworzenie lub otwarcie eventu do hot-restartu
            bool createdNewEvent;
            var restartEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                RestartEventName,
                out createdNewEvent
            );

            // Tylko w normalnym uruchomieniu: nasłuchiwanie sygnału restartu
            if (!isRestart)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    // Oczekiwanie na sygnał
                    restartEvent.WaitOne();

                    // Sprzątanie zasobów przed zabiciem procesu
                    try
                    {
                        // Ukrycie i zwolnienie ikonki tray
                        if (IdleTrayApp.TrayIcon != null)
                        {
                            IdleTrayApp.TrayIcon.Visible = false;
                            IdleTrayApp.TrayIcon.Dispose();
                        }

                        // Odłączenie globalnego hooka klawiatury i WMI
                        IdleTrayApp.Instance?.CleanupHooks();

                        // Zatrzymanie timerów
                        IdleTrayApp.Instance?.StopTimers();
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Błąd podczas sprzątania zasobów: {ex.Message}");
                    }

                    // Zabij proces
                    Process.GetCurrentProcess().Kill();
                });
            }

            // Obsługa mutexa i trybu uruchomienia
            using (var mutex = new Mutex(true, "DimScreenSaverMutex", out bool createdNewMutex))
            {
                // Jeśli już działa i to nie restart, wyjście
                if (!createdNewMutex && !isRestart)
                {
                    MessageBox.Show("DimScreenSaver już działa.", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // W trybie restart: od razu start screensavera
                if (isRestart)
                {
                    StartScreensaver();
                    return;
                }
                    
              
                else
                {
                    // brak argumentów: normalny start
                    StartScreensaver();
                }
            }
        }




        public static void HotRestart()
        {
            // 1. Zapis stanu aplikacji
            try
            {
                var state = new AppState { JavaFollowUpActive = IdleTrayApp.javaFollowUpActive };
                Directory.CreateDirectory(Path.GetDirectoryName(StateStorage.StateFilePath));
                File.WriteAllText(
                    StateStorage.StateFilePath,
                    JsonConvert.SerializeObject(state, Formatting.Indented)
                );
                Log("💾 Zapisano stan HotRestart");
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd zapisu HotRestart: {ex.GetType().Name}: {ex.Message}");
            }

            // 2. Uruchom nową instancję **pierwsze**
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "/restart",
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                Process.Start(psi);
                Log("🚀 Uruchomiono nową instancję");
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd Process.Start: {ex.Message}");
                return;
            }

            // 3. Wyślij sygnał do starej, żeby sama się zakończyła
            try
            {
                using (var ev = EventWaitHandle.OpenExisting(RestartEventName))
                {
                    ev.Set();
                    Log("🔔 Wysłano sygnał do starej instancji");
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Log("⚠️ Nie znaleziono eventu restartu");
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd sygnału: {ex.Message}");
            }

            // 4. Kończymy starą instancję
            Application.ExitThread();
            Application.Exit();
        }



        static void StartScreensaver()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var app = new IdleTrayApp();

                Application.Idle += (s, e) =>
                {
                    if (IdleTrayApp.UISyncContext == null)
                        IdleTrayApp.UISyncContext = SynchronizationContext.Current;
                };

                // Wczytanie stanu po restarcie
                app.LoadHotRestartState();

                Application.Run(app);
            }
            catch (Exception ex)
            {
                try
                {
                    string logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DimScreenSaver"
                    );
                    Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, "crashlog.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex}\n");
                }
                catch
                {
                    // Ignoruj błędy logowania
                }

                MessageBox.Show(
                    "Program nie mógł się uruchomić. Szczegóły zapisano w AppData\\DimScreenSaver\\crashlog.txt",
                    "Błąd krytyczny"
                );
            }
        }
    }

    internal static class StateStorage
    {
        public static readonly string StateFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DimScreenSaver",
                "state.json"
            );
    }
}
