using DimScreenSaver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public class JavaDialogWatcher
{

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindow(IntPtr hWnd);

    private readonly string logPath;
    private System.Windows.Forms.Timer loopTimer;

    public void StartLoopingMonitor()
    {
        if (loopTimer != null)
        {
            loopTimer.Stop();
            loopTimer.Dispose();
            loopTimer = null;
        }

        loopTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        loopTimer.Tick += (s, e) =>
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
        };

        loopTimer.Start();
        LastTickTime = DateTime.Now;
        Log("🔍 Rozpoczęto cykliczne wyszukiwanie okna Java.");
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




    public JavaDialogWatcher() { logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt"); }
    private static void Log(string msg) => AppLogger.Log("JavaWatcher", msg);

    private void LogJava(string message)
    {

        string logEntry = $"[JavaWatcher] {DateTime.Now:HH:mm:ss} {message}";
        try
        {

            const int maxLines = 5000;
            List<string> lines = new List<string>();

            if (File.Exists(logPath))
            {
                lines = File.ReadAllLines(logPath).ToList();
                if (lines.Count >= maxLines)
                    lines = lines.Skip(lines.Count - (maxLines - 1)).ToList();
            }

            lines.Add(logEntry);
            File.WriteAllLines(logPath, lines);
        }
        catch { }
    }


    private System.Windows.Forms.Timer disappearanceWatcher;

    private bool wasPreviouslyVisible = false;

    public void StartMonitoringDisappearance(IntPtr hwnd)
    {

        Log("▶ StartMonitor – zaczynam obserwację okna.");
        disappearanceWatcher = new System.Windows.Forms.Timer { Interval = 10_000 };
        disappearanceWatcher.Tick += (s, e) =>
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
                StartLoopingMonitor();
                return;
            }
            if (!IsWindow(hwnd))
            {
                Log("🟥 Okno już nie istnieje (zamknięte) – przełączam z powrotem na tryb wyszukiwania.");
                disappearanceWatcher.Stop();
                targetWindow = IntPtr.Zero;
                StartLoopingMonitor();
                return;
            }
            LastTickTime = DateTime.Now;
            bool visibleNow = IsWindowVisible(hwnd); // ← TU!


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


        };






        wasPreviouslyVisible = IsWindowVisible(hwnd);
        disappearanceWatcher.Start();
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
                Log($"🎯 Wykryłem okno Java → szerokość: {width}px, wysokość: {height}px");
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
}
