using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace DimScreenSaver
{
    public class DimForm : Form
    {
        private Timer dimFormMonitor;
        private Timer screenOffTimer;
        private bool screenTurnedOff = false;
        private readonly string logPath;
        private readonly string brightnessPath;
        private bool allowClose = false;
        private DateTime started;
        private int previousBrightness = 75;
        private bool closeImmediately = false;
        public static Action OnGlobalReset;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);
        public bool WasClosedByUserInteraction { get; private set; } = false;
        private Point globalCursorAtStart = Point.Empty;
        private bool isClosingIntended = false;
        private DateTime? screenOffTimerStartedAt = null;
        private int screenOffDelaySeconds = 0;
        private static void Log(string msg) => _ = AppLogger.LogAsync("DimForm", msg);
        private static readonly string _configPath
     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");

        private static async Task<bool> GetWakeOnAudioFlagAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    
                    var lines = await Task.Run(() => File.ReadAllLines(_configPath))
                                          .ConfigureAwait(false);

                    if (lines.Length >= 2)
                    {
                        string raw = lines[1]
                            .Split(new[] { "//" }, StringSplitOptions.None)[0]
                            .Trim();

                        return raw == "1";
                    }
                }
            }
            catch (Exception ex)
            {
                
                Log($"[GetWakeOnAudioFlag] Błąd: {ex.Message}");
            }

            
            return true;
        }

        private void StopScreenOffTimer(string reason)
        {
            if (screenOffTimer != null && screenOffTimer.Enabled)
            {
                screenOffTimer.Stop();
                Log($"📛 screenOffTimer zatrzymany ({reason})");
            }
        }

        public DimForm(int screenOffAfterSeconds, int idleAlready, int dimBrightness)
        {
            logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");
            brightnessPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DimScreenSaver", "brightness.txt");
            started = DateTime.Now;
            int dimLevel = dimBrightness;
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.WorkingArea;
            this.TopMost = true;
            this.Name = "DimForm";
            this.KeyPreview = true;
            this.BackColor = Color.Black;       
            this.Visible = false;
            this.Opacity = 0.0;

            try { Log("Form uruchomiona"); } catch { }

            this.MouseMove += (s, e) =>
            {
                if (!GetCursorPos(out Point current))
                {
                    Log("MouseMove → nie udało się pobrać pozycji globalnej");
                    return;
                }

                int dx = current.X - globalCursorAtStart.X;
                int dy = current.Y - globalCursorAtStart.Y;

                if (dx == 0 && dy == 0)
                {
                   return; //ignorowanie ruchu o 0px
                }

                Log($"EVENT: MouseMove → Δx: {dx}, Δy: {dy} (from {globalCursorAtStart.X},{globalCursorAtStart.Y} to {current.X},{current.Y})");

                CheckAndClose(s, e);
            };


            this.KeyDown += (s, e) =>
            {
                Log("EVENT: KeyDown");
                CheckAndClose(s, e);
            };
            this.MouseClick += (s, e) =>
            {
                Log("EVENT: MouseClick");
                CheckAndClose(s, e);
            };


            allowClose = false;

            var allowCloseTimer = new Timer {Interval = 500};
            allowCloseTimer.Tick += (s, e) =>
            {
                allowClose = true;
                allowCloseTimer.Stop();
                Log($"Upłynęło {(allowCloseTimer.Interval / 1000.0):0.##} sek. – allowClose = true");
            };
            allowCloseTimer.Start();


            if (screenOffAfterSeconds > 0)
            {
                screenOffTimer = new Timer();
                int remaining = screenOffAfterSeconds - idleAlready;


                if (remaining > 0)
                {
                    screenOffDelaySeconds = remaining; // 💾 zapisz cały czas trwania
                    screenOffTimerStartedAt = DateTime.Now; // 🕒 zapisz czas startu

                    screenOffTimer.Interval = remaining * 1000;
                    screenOffTimer.Tick += (s, e) =>
                    {
                        screenOffTimer.Stop(); 

                        if (!IdleTrayApp.GlobalScreenOff)
                        {
                            Log("screenOffTimer.Tick → wyłączam ekran");
                            DisplayControl.TurnOff();
                            screenTurnedOff = true;
                            IdleTrayApp.GlobalScreenOff = true;
                        }
                        else
                        {
                            Log("screenOffTimer.Tick → pomijam TurnOff (już wyłączony)");
                        }

                        Log("screenOffTimer.Tick → zamykanie DimForm będzie obsługiwane przez MonitorStateWatcher");
                    };
                    screenOffTimer.Start();
                }
                else
                {
                    Log("screenOffTimer: natychmiastowe wyłączenie");
                    DisplayControl.TurnOff();
                    screenTurnedOff = true;
                    IdleTrayApp.GlobalScreenOff = true;
                    closeImmediately = true;
                }
            }


            this.Load += async (s, e) =>
            {
                GetCursorPos(out globalCursorAtStart);
                Log($"Pozycja kursora przy wejściu: {globalCursorAtStart.X},{globalCursorAtStart.Y}");
                try
                {
                    if (File.Exists(brightnessPath) && int.TryParse(File.ReadAllText(brightnessPath), out int saved))
                    {
                        previousBrightness = saved;
                        Log($"[LOAD] Odczytano jasność z pliku: {saved}%");
                    }
                    else
                    {
                        previousBrightness = 75;
                        Log("[LOAD] Nie znaleziono brightness.txt – fallback do 75%");
                    }
                }
                catch
                {
                    previousBrightness = 75;
                    Log("[LOAD] Błąd odczytu brightness.txt – fallback do 75%");
                }

                // 🔒 Przygaszanie tylko jeśli nie closeImmediately
                if (!closeImmediately)
                {
                    await IdleTrayApp.SetBrightnessAsync(dimLevel);
                    Log($"Ustawiam jasność (WMI): {dimLevel}%");
                    try
                    {
                        IdleTrayApp.Instance?.keyboard?.Set(0);
                        Log("🎹 Wyłączono podświetlenie klawiatury");
                    }
                    catch (Exception ex)
                    {
                        Log($"Błąd wyłączania klawiatury: {ex.Message}");
                    }

                }
                else
                {
                    Log("Pominięto przygaszanie – closeImmediately aktywne");
                }




                await Task.Delay(250);
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.Opacity = 0.02;
                    this.Visible = true;
                    this.Activate();
                    this.Focus();
                }

                if (closeImmediately)
                {
                    Log("closeImmediately → zamknięcie formy od razu");
                    screenTurnedOff = true;
                    this.Close();
                    return;
                }

                dimFormMonitor = new Timer {Interval = 7000};
                dimFormMonitor.Tick += async (snd, evt) =>
                {
                    // Odczyt flagi z pliku asynchronicznie
                    bool wakeOnAudio = await GetWakeOnAudioFlagAsync().ConfigureAwait(true);
                    bool playing = AudioWatcher.IsAudioPlaying();

                    Log($"[dimFormMonitor] Tick → WakeOnAudio: {wakeOnAudio}, IsAudioPlaying: {playing}");

                    if (screenOffTimer != null && screenOffTimer.Enabled && screenOffTimerStartedAt.HasValue)
                    {
                        var elapsed = (DateTime.Now - screenOffTimerStartedAt.Value).TotalSeconds;
                        var remaining = screenOffDelaySeconds - elapsed;
                        remaining = Math.Max(0, remaining);

                        Log($"screenOffTimer.Tick → upłynęło {elapsed:F0}s z {screenOffDelaySeconds}s (pozostało {remaining:F0}s)");
                    }


                    if (wakeOnAudio && playing)
                    {
                        Log("🎵 Wykryto dźwięk – zamykam formę (dimFormMonitor)");
                        CheckAndClose(snd, evt);
                    }
                };
                dimFormMonitor.Start();
            };



            this.FormClosed += async (s, e) =>
            {
                try
                {
                    if (BatterySaverChecker.IsBatterySaverActive())
                    {
                        if (screenTurnedOff)
                        {
                            await IdleTrayApp.Instance.RestoreBrightnessWithBatterySaverCompensation(previousBrightness);
                            Log($"[FormClosed] Jasność przywrócona (Battery Saver, ekran WYŁ.): {previousBrightness}%");
                        }
                        else
                        {
                            // TODO: Zamień w przyszłości na wersję z retry
                            await IdleTrayApp.Instance.RestoreBrightnessWithBatterySaverCompensation(previousBrightness);
                            Log($"[FormClosed] Jasność przywrócona (Battery Saver): {previousBrightness}%");
                        }
                    }
                    else
                    {
                        if (screenTurnedOff)
                        {
                            await IdleTrayApp.SetBrightnessAsync(previousBrightness);
                            Log($"[FormClosed] Jasność przywrócona bez Battery Saver (ekran WYŁ.): {previousBrightness}%");
                        }
                        else
                        {
                            await IdleTrayApp.SetBrightnessWithRetry(previousBrightness);
                            Log($"[FormClosed] Jasność przywrócona bez Battery Saver: {previousBrightness}%");
                        }
                    }
                }
                catch (Exception ex)
                {
                    try { Log($"[FormClosed] Błąd przywracania jasności: {ex.Message}"); } catch { }
                }
                
                if (screenTurnedOff && !BatterySaverChecker.IsBatterySaverActive())
                {
                    IdleTrayApp.WaitForUserActivity = true;
                    IdleTrayApp.idleCheckTimerPublic?.Stop();
                    IdleTrayApp.idleCheckTimerPublic = IdleTrayApp.idleCheckTimer;
                    Log($"[FormClosed] screenTurnedOff → WaitForUserActivity = {IdleTrayApp.WaitForUserActivity}");
                    Log("🎹 Pominięto przywracanie klawiatury – ekran został wyłączony");
                }
                else
                {
                    try
                    {
                        IdleTrayApp.Instance?.SetKeyboardBacklightBasedOnBrightnessForce(previousBrightness, "DimForm.FormClosed");
                    }
                    catch (Exception ex)
                    {
                        Log($"[FormClosed] Błąd przywracania klawiatury: {ex.Message}");
                    }
                }

                // 🔐 Reset flag, zatrzymanie – PRZYWRÓCONE
                IdleTrayApp.PreparingToDim = false;
                IdleTrayApp.Instance.dimFormClosedAt = DateTime.Now;
                OnGlobalReset = null;
                dimFormMonitor?.Stop();
                dimFormMonitor?.Dispose();
                StopScreenOffTimer("FormClosed");
                Log("[FormClosed] FormClosed zakończony – form zamknięty");
            };



            OnGlobalReset = () =>
            {
                StopScreenOffTimer("OnGlobalReset z poziomu ResetIdle");

                if (!this.IsDisposed && this.Visible)
                {
                    Log("📛 DimForm zamknięta z OnGlobalReset z poziomu ResetIdle()");
                    Log($"[OnGlobalReset] GlobalScreenOff = {IdleTrayApp.GlobalScreenOff}");
                    isClosingIntended = true;
                    this.Close();

                }
            };


        }

        public void CloseFromScreenOff()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((Action)CloseFromScreenOff);
                return;
            }

            screenTurnedOff = true;
            Log("Zamknięcie przez MonitorStateWatcher (ekran wyłączony)");
            isClosingIntended = true;
            this.Close();
        }

        public void CheckAndClose(object sender, EventArgs e)
        {
            if (!allowClose)
            {
                Log("CheckAndClose → interakcja zablokowana (allowClose = false)");
                return;
            }

            Log("CheckAndClose → interakcja użytkownika zaakceptowana (allowClose = true)");
            WasClosedByUserInteraction = true;

            StopScreenOffTimer("CheckAndClose");
            isClosingIntended = true;
            this.Close();
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log($"[OnFormClosing] Powód zamknięcia: {e.CloseReason}");
            if (!isClosingIntended && e.CloseReason == CloseReason.UserClosing)
                Log("[OnFormClosing]⚠️ Forma zamykana przez użytkownika bez isClosingIntended (Alt+F4? Task Manager?).");
            if (!isClosingIntended)
            {
                Log("[OnFormClosing]🚫 Zamknięcie DimForm not intended.");

                //e.Cancel = true; //najpierw tylko logujemy, potem sprobujemy zablokowac
                // return;         //najpierw tylko logujemy, potem sprobujemy zablokowac
            }
            else
            {
                Log("[OnFormClosing]✅ Zamknięcie DimForm intended.");
            }

                base.OnFormClosing(e);
        }
    }
}
