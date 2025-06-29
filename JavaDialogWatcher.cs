using DimScreenSaver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public class JavaDialogWatcher : IDisposable
{

    private bool disposed = false;
    private IntPtr monitoredWindow = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private bool isErrorSoundPlaying = false;
    private System.Media.SoundPlayer errorSoundPlayer;

    int errorBalloonCounter = 0;

    public bool VisibleNow => wasPreviouslyVisible;
    public Action OnJavaDialogVisible { get; set; } = null;
    public DateTime LastTickTime { get; private set; } = DateTime.MinValue;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private IntPtr targetWindow = IntPtr.Zero;
    public bool ShouldRun { get; set; } = true;
    public IntPtr TargetWindow => targetWindow;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // importy do przywracania/ustawiania okna na wierzchu
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    const int SW_RESTORE = 9;


    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindow(IntPtr hWnd);

    private readonly string logPath;
    private System.Windows.Forms.Timer loopTimer;

    private bool simulateInvisible = false;

    public void StartLoopingMonitor()
    {
        if (loopTimer != null)
        {
            loopTimer.Stop();
            loopTimer.Dispose();
            loopTimer = null;
        }

        loopTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        // 1) przypisujemy handler do zdarzenia
        loopTimer.Tick += LoopingMonitor_Tick;

        loopTimer.Start();
        LastTickTime = DateTime.Now;
        Log("🔍 Rozpoczęto cykliczne wyszukiwanie okna Java.");

        // 2) i od razu po uruchomieniu robimy pierwsze sprawdzenie
        LoopingMonitor_Tick(this, EventArgs.Empty);
    }



    // Wydzielona metoda z całą logiką Tick-a
    private void LoopingMonitor_Tick(object sender, EventArgs e)
    {
        if (!ShouldRun)
        {
            Log("⛔ Monitoring wyłączony – pętla pominięta.");
            return;
        }

        LastTickTime = DateTime.Now;
        FindJavaDialog(); // próba znalezienia

        if (targetWindow != IntPtr.Zero)
        {
            Log("🎯 Okno Java znalezione – uruchamiam monitorowanie.");
            loopTimer.Stop();
            StartMonitoringDisappearance(targetWindow);
        }
        else
        {
            Log($"🔄 Brak okna Java – próbuję ponownie za {loopTimer.Interval / 1000} sekund...");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }


    /// <summary>
    /// Jeżeli true, to przy następnym ticku symulujemy, że okno nie jest widoczne.
    /// </summary>
    public void SetSimulateInvisible(bool value)
    {
        simulateInvisible = value;
    }

    public JavaDialogWatcher() { logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt"); }
    private static void Log(string msg) => _ = AppLogger.LogAsync("JavaWatcher", msg);




    private System.Windows.Forms.Timer disappearanceWatcher;

    private bool wasPreviouslyVisible = false;

    public void StartMonitoringDisappearance(IntPtr hwnd)
    {
        monitoredWindow = hwnd;
        Log("▶ StartMonitor – zaczynam obserwację okna.");
        disappearanceWatcher = new System.Windows.Forms.Timer { Interval = 5_000 };
        disappearanceWatcher.Tick += DisappearanceWatcher_Tick;
        wasPreviouslyVisible = IsWindowVisible(hwnd);
        disappearanceWatcher.Start();
    }


    private void DisappearanceWatcher_Tick(object sender, EventArgs e)
    {
        if (Process.GetProcessesByName("javaw").Length == 0)
        {
            Log("🟥 Proces javaw już nie istnieje – zatrzymuję obserwację.");
            disappearanceWatcher.Stop();
            isErrorSoundPlaying = false;
            errorSoundPlayer?.Stop();
            errorSoundPlayer = null;
            errorBalloonCounter = 0;
            targetWindow = IntPtr.Zero;
            IdleTrayApp.javaFollowUpActive = false;
            StartLoopingMonitor();
            return;
        }
        if (!IsWindow(monitoredWindow))
        {
            Log("🟥 Okno już nie istnieje (zamknięte) – przełączam z powrotem na tryb wyszukiwania.");
            disappearanceWatcher.Stop();
            targetWindow = IntPtr.Zero;
            IdleTrayApp.javaFollowUpActive = false;
            StartLoopingMonitor();
            return;
        }
        LastTickTime = DateTime.Now;
        bool visibleNow = simulateInvisible
         // jeśli symulacja włączona, to zawsze false
         ? false
         // w przeciwnym razie odczyt z WinAPI
         : IsWindowVisible(monitoredWindow);


        if (visibleNow && !wasPreviouslyVisible)
        {
            Log("🟢 Okno Panelo wróciło – zgłaszam do systemu.");
            OnJavaDialogVisible?.Invoke();
        }



        if (wasPreviouslyVisible && !visibleNow)
        {
            if (!ShouldRun)
            {
                Log("⛔ Monitoring wyłączony – ignoruję zniknięcie okna.");
                return;
            }

            if (IdleTrayApp.Instance != null && IdleTrayApp.Instance.monitorJavaDialog)
            {
                Log("🕵️ Okno stało się niewidoczne – budzik aktywny, wybudzam i gram dźwięk!");
                DisplayControl.TurnOn();
                string pathToMp4 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert.mp4");

                if (IdleTrayApp.UISyncContext != null)
                {
                    IdleTrayApp.UISyncContext.Post(_ =>
                    {
                        IdleTrayApp.ResetByPopup();
                        try
                        {
                            if (IdleTrayApp.CurrentFormVideoPlayer == null || IdleTrayApp.CurrentFormVideoPlayer.IsDisposed)
                            {
                                IdleTrayApp.CurrentFormVideoPlayer = new FormVideoPlayer(pathToMp4);
                                Log("🆕 Tworzę nową instancję FormVideoPlayer");
                            }

                            if (!IdleTrayApp.CurrentFormVideoPlayer.Visible)
                            {
                                IdleTrayApp.CurrentFormVideoPlayer.Show();
                                Log("▶ Pokazuję FormVideoPlayer");

                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"❌ Błąd podczas pokazywania FormVideoPlayer: {ex.Message}");
                        }

                    }, null);
                }
                else
                {
                    Log("Brak SynchronizationContext – nie udało się pokazać formularza video.");
                }
            }
            else
            {
                Log("🕵️ Okno zniknęło, ale budzik jest wyłączony – nie odtwarzam alertu.");
            }
        }

        wasPreviouslyVisible = visibleNow;

        bool errorWindowVisible = IsErrorDialogPresent();

        if (errorWindowVisible && IdleTrayApp.Instance?.paneloErrorNotifyEnabled == true)
        {
            errorBalloonCounter++;

            if (!isErrorSoundPlaying)
            {
                try
                {
                    string wavPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.wav");
                    errorSoundPlayer = new System.Media.SoundPlayer(wavPath);
                    errorSoundPlayer.PlayLooping();
                    isErrorSoundPlaying = true;
                    Log("🔊 Rozpoczęto odtwarzanie dźwięku błędu.");
                    IdleTrayApp.ResetByPopup();
                    BalloonForm.ShowBalloon("Błąd Panelo", "Zerwane połączenie", 20000, showIcons: false);
                }
                catch (Exception ex)
                {
                    Log($"❌ Błąd podczas odtwarzania dźwięku błędu: {ex.Message}");
                }
            }
            else if (errorBalloonCounter % 6 == 0)
            {
                BalloonForm.ShowBalloon("Błąd Panelo", "Zerwane połączenie", 20000, showIcons: false);
                Log("🔁 Odświeżono bąbelek błędu Panelo.");
            }
        }

        else if (!errorWindowVisible && isErrorSoundPlaying)
        {
            // reset wszystkiego
            try
            {
                errorSoundPlayer?.Stop();
                errorSoundPlayer = null;
                isErrorSoundPlaying = false;
                errorBalloonCounter = 0;
                Log("🔇 Zatrzymano odtwarzanie dźwięku błędu.");
            }
            catch { }
        }


    }

    public void ForceStopPaneloAlarm()
    {
        try
        {
            errorSoundPlayer?.Stop();
            errorSoundPlayer = null;
            isErrorSoundPlaying = false;
            errorBalloonCounter = 0;
            Log("🔇 ForceStopPaneloAlarm() → zatrzymano dźwięk i wyzerowano licznik.");
        }
        catch (Exception ex)
        {
            Log($"❌ Błąd przy ForceStopPaneloAlarm: {ex.Message}");
        }
    }


    private bool IsErrorDialogPresent()
    {
        bool found = false;

        EnumWindows((hWnd, lParam) =>
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            if (className.ToString() == "SunAwtDialog")
            {
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hWnd, windowText, windowText.Capacity);

                if (windowText.ToString().Trim() == "Error" && IsWindowVisible(hWnd))
                {
                    found = true;
                    return false; // stop
                }
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }



    private void BringPaneloToFront()
    {
        EnumWindows((hWnd, lParam) =>
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (title.StartsWith("Panelo v", StringComparison.OrdinalIgnoreCase))
            {
                // przywróć, jeśli zminimalizowane
                ShowWindow(hWnd, SW_RESTORE);
                // ustaw na wierzchu
                SetForegroundWindow(hWnd);
                return false; // przerwij dalsze enum
            }
            return true; // szukaj dalej
        }, IntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    private bool IsWaitDialogBySize(IntPtr hWnd)
    {
        if (GetWindowRect(hWnd, out RECT rect))
        {
            int width = rect.Width;
            int height = rect.Height;

            if (width >= 400 && width <= 500 && height >= 300 && height <= 400)
            {
                Log($"🎯 Wykryłem okno Java → szerokość: {width}px, wysokość: {height}px; Visible = {IsWindowVisible(hWnd)}");

                // 🔁 Nowy dodatek:
                if (IsWindowVisible(hWnd))
                {
                    Log("👁️ Okno jest widoczne – wywołuję OnJavaDialogVisible()");
                    OnJavaDialogVisible?.Invoke();
                }

                return true;
            }
        }
        return false;
    }





    public void FindJavaDialog()
    {

        Process[] javaProcesses = Process.GetProcessesByName("javaw");

        foreach (var proc in javaProcesses)
        {
            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);

                if (className.ToString() == "SunAwtDialog")
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid == proc.Id)
                    {
                        if (IsWaitDialogBySize(hWnd))
                        {
                            GetWindowRect(hWnd, out RECT rect);
                            targetWindow = hWnd;
                            bool visible = IsWindowVisible(hWnd);
                            //MessageBox.Show($"Znaleziono okno o wymiarach: \n\nHeight: {rect.Height} \nWidth: {rect.Width} \n\nHWND: {hWnd}\nPID: {pid}\nVisible: {visible}");
                            return false; // stop searching
                        }
                    }
                }

                return true;
            }, IntPtr.Zero);
        }



    }


    /// <summary>
    /// Zatrzymuje wszystkie timery i zamyka ewentualne okno Java.
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        // 1. Zatrzymaj pętlę główną
        ShouldRun = false;

        // 2. Zatrzymaj i usuń loopTimer
        if (loopTimer != null)
        {
            loopTimer.Stop();
            loopTimer.Tick -= LoopingMonitor_Tick;   
            loopTimer.Dispose();
            loopTimer = null;
        }

        // 3. Zatrzymaj i usuń disappearanceWatcher
        if (disappearanceWatcher != null)
        {
            disappearanceWatcher.Stop();
            disappearanceWatcher.Tick -= DisappearanceWatcher_Tick;
            disappearanceWatcher.Dispose();
            disappearanceWatcher = null;
        }

        // 4. Zamknij okno video, jeśli jest otwarte
        IdleTrayApp.CurrentFormVideoPlayer?.BeginInvoke((Action)(() =>
        {
            IdleTrayApp.CurrentFormVideoPlayer.Close();
            IdleTrayApp.CurrentFormVideoPlayer.Dispose();
        }));
        IdleTrayApp.CurrentFormVideoPlayer = null;
    }

}
