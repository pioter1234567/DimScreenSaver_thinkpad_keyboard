using AxWMPLib;
using DimScreenSaver;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;





public class FormWakeup : Form
{


    private Point globalCursorAtStart;
    private readonly InnerWakeupForm inner;
    private bool innerAlreadyShown = false;
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);
    private static IntPtr hookID = IntPtr.Zero;
    private static LowLevelKeyboardProc proc;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    private bool alreadyClosing = false;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private Point initialCursor;
    private System.Windows.Forms.Timer movementCheckTimer;

    public FormWakeup(string videoPath)
    {


        GetCursorPos(out initialCursor);
        if (IdleTrayApp.GlobalScreenOff)
        {
            DisplayControl.TurnOn();
            Log("Ekran był wyłączony – wybudzam przez DisplayControl.TurnOn()");
        }
        Log("FormWakeup start – sprawdzam GlobalScreenOff i DimForm");
        if (Application.OpenForms["DimForm"] is Form dim)
        {
            Log("Zamykam istniejący DimForm przy starcie FormWakeup");
            try { dim.Close(); } catch { }
        }



        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Size = new Size(1, 1);
        this.Opacity = 0.0;
        this.BackColor = Color.Black;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.FormClosed += (_, __) =>
        {
            IdleTrayApp.CurrentFormWakeup = null;
            Log("🧹 FormWakeup zamknięty – referencja wyczyszczona");
        };

        // 🖱️ Zapisz pozycję kursora przy starcie
        GetCursorPos(out initialCursor);

        // 🔄 Timer do sprawdzania globalnego ruchu myszy
        movementCheckTimer = new System.Windows.Forms.Timer {Interval = 100};
        movementCheckTimer.Tick += (s, e) =>
        {
            try
            {
                if (!GetCursorPos(out Point current))
                    return;

                int dx = Math.Abs(current.X - initialCursor.X);
                int dy = Math.Abs(current.Y - initialCursor.Y);

                if (dx > 2 || dy > 2)
                {
                    Log($"🖱️ Ruch myszy wykryty (Δx={dx}, Δy={dy}) – zamykam obie formy");
                    ZamknijObieFormy();
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd w movementCheckTimer: {ex.Message}");
            }
        };
        movementCheckTimer.Start();


        // pokaż filmik w osobnej formie
        inner = new InnerWakeupForm(videoPath);
        this.Load += async (_, __) =>
        {


            await Task.Delay(5000);

            if (alreadyClosing || this.IsDisposed || inner == null || inner.IsDisposed)
            {
                Log("▶ Nie pokazuję InnerWakeupForm – forma już zamknięta");
                return;
            }

            try
            {
                Log("▶ Minęło 5 sekund – pokazuję InnerWakeupForm z filmem");
                if (!innerAlreadyShown)
                {
                    innerAlreadyShown = true;
                    inner.Show();
                    Log("▶ InnerWakeupForm  pokazany po raz pierwszy");
                }
                else
                {
                    Log("❗ InnerWakeupForm już był pokazany – ignoruję kolejne wywołanie");
                }
                await Task.Delay(50); // daj mu chwilę na odpalenie

                this.TopMost = true;
                this.BringToFront();
                this.Activate();

                Log("▶ Ustawiam FormWakeup z powrotem na TopMost i Focus po InnerForm");


            }
            catch (Exception ex)
            {
                Log($"❌ Błąd przy inner.Show(): {ex.Message}");
            }
        };







        // łapanie poruszenia myszką
        this.MouseMove += CheckMouseDelta;
        this.KeyDown += (_, __) => ZamknijObieFormy();



        proc = HookCallback;
        hookID = SetHook(proc);

        this.FormClosing += (s, e) =>
        {
            try
            {
                if (hookID != IntPtr.Zero)
                {
                    Log("🧹 FormClosing → zwalniam hook klawiatury");
                    UnhookWindowsHookEx(hookID);
                    hookID = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd przy zwalnianiu hooka: {ex.Message}");
            }

            if (!alreadyClosing)
            {
                Log("🧹 FormClosing → przekierowuję do ForceStopAndClose");
            }
        };





    }
    public void SpróbujZamknąć(string źródło)
    {
        if (alreadyClosing)
        {
            Log($"🚫 Próba zamknięcia z \"{źródło}\" zignorowana – alreadyClosing = true");
            return;
        }

        alreadyClosing = true;
        Log($"✅ SpróbujZamknąć() wywołana z \"{źródło}\" – wykonuję ZamknijObieFormy()");
        ZamknijObieFormy();
    }
    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Log($"⌨️ Naciśnięto klawisz globalnie: {vkCode} – próbuje zamknąć");
            SpróbujZamknąć($"klawisz {vkCode}");
        }

        return CallNextHookEx(hookID, nCode, wParam, lParam);
    }

    private static void Log(string msg) => AppLogger.Log("FormWakeup", msg);
    public static void LogWakeup(string message)
    {
        string logFile = Path.Combine(Path.GetTempPath(), "scrlog.txt");
        string logEntry = $"[FormWakeup] {DateTime.Now:HH:mm:ss} {message}";

        try
        {
            const int maxLines = 5000;

            List<string> lines = new List<string>();
            if (File.Exists(logFile))
            {
                lines = File.ReadAllLines(logFile).ToList();

                if (lines.Count >= maxLines)
                    lines = lines.Skip(lines.Count - (maxLines - 1)).ToList();
            }

            lines.Add(logEntry);
            File.WriteAllLines(logFile, lines);
        }
        catch { }
    }



    private void CheckMouseDelta(object sender, MouseEventArgs e)
    {
        if (!GetCursorPos(out Point current))
        {
            Log("Nie udało się pobrać pozycji kursora");
            return;
        }

        int dx = current.X - globalCursorAtStart.X;
        int dy = current.Y - globalCursorAtStart.Y;

        Log($"MouseMove → Δx: {dx}, Δy: {dy} (from {globalCursorAtStart.X},{globalCursorAtStart.Y} to {current.X},{current.Y})");

        if ((dx == 0 && dy == 0) || (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2))
        {
            Log("Ruch systemowy (Δx ≤ 2, Δy ≤ 2) – ignoruję");
            return;
        }

        Log("Ruch wykryty – próbuje zamknąć");
        SpróbujZamknąć($"ruch myszy Δx={dx}, Δy={dy}");
    }

    private void ZamknijObieFormy()
    {
        try
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((MethodInvoker)(() => ZamknijObieFormy()));
                return;
            }

            if (alreadyClosing)
            {
                Log("🔁 ZamknijObieFormy() wywołana z alreadyClosing – kontynuuję zamykanie");
            }
            else
            {
                Log("🧹 ZamknijObieFormy() bez ustawionego alreadyClosing – wywołane myszką");
            }

            alreadyClosing = true;

            Log("🧹 ZamknijObieFormy → rozpoczynam zamykanie formy i czyszczenie");

            // zatrzymaj nasłuchiwanie ruchu myszy
            movementCheckTimer?.Stop();
            movementCheckTimer?.Dispose();
            movementCheckTimer = null;

            // zatrzymaj InnerForm (z dźwiękiem)
            if (inner != null && !inner.IsDisposed)
            {
                inner.ForceStopAndClose();
            }

            // zwolnij hook klawiatury
            try
            {
                if (hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookID);
                    hookID = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Błąd przy zwalnianiu hooka w ZamknijObieFormy: {ex.Message}");
            }

            try
            {
                this.Close();
            }
            finally
            {

            }

        }
        catch (Exception ex)
        {
            Log($"❌ Błąd w ZamknijObieFormy: {ex.Message}");
        }
    }




}

public class InnerWakeupForm : Form
{
    private readonly AxWindowsMediaPlayer _wmp;
    private bool _isClosing = false;
    private static void Log(string msg) => AppLogger.Log("InnerWakeupForm", msg);

    public void ForceStopAndClose()
    {
        if (_isClosing) return;
        _isClosing = true;

        if (this.InvokeRequired)
        {
            this.BeginInvoke((MethodInvoker)(() => ForceStopAndClose()));
            return;
        }

        try
        {
            Log("⛔ ForceStopAndClose → rozpoczynam zatrzymywanie...");

            try
            {
                if (_wmp != null)
                {
                    // Bezpieczne sprawdzenie: komponent musi być gotowy
                    bool isReady = _wmp.Created && _wmp.IsHandleCreated;

                    if (isReady && _wmp.playState == WMPLib.WMPPlayState.wmppsPlaying)
                    {
                        Log("⏹ MediaPlayer gra – zatrzymuję...");
                        _wmp.Ctlcontrols.stop();
                        Thread.Sleep(100);
                    }

                    if (isReady)
                    {
                        _wmp.close();
                        Log("✅ MediaPlayer zutylizowany");
                    }
                    else
                    {
                        Log("⚠️ MediaPlayer nie był gotowy do zatrzymania (jeszcze nie wystartował)");
                    }
                }
            }
            catch (Exception ex)
            {
               Log($"❌ Błąd podczas zatrzymywania: {ex.Message}");
            }

            this.Close();
            Log("❌ Forma zamknięta...");
        }
        catch (Exception ex)
        {
           Log($"❌ Błąd główny w ForceStopAndClose: {ex.Message}");
        }


    }


    public InnerWakeupForm(string videoPath)
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Size = new Size(640, 360);
        this.TopMost = true;
        this.BackColor = Color.Black;
        this.ShowInTaskbar = false;

        var screen = Screen.PrimaryScreen.WorkingArea;
        this.Left = (screen.Width - this.Width) / 2;
        this.Top = (screen.Height - this.Height) / 2;

        _wmp = new AxWindowsMediaPlayer {Dock = DockStyle.Fill};
        _wmp.HandleCreated += (s, e) =>
        {
            _wmp.uiMode = "none";
            _wmp.settings.setMode("loop", true);
            _wmp.settings.autoStart = true;
            _wmp.URL = videoPath;
        };

        this.Controls.Add(_wmp);

        this.Load += (s, e) =>
        {
            Log("▶ Próba odpalenia WMP...");
        };


    }
}
