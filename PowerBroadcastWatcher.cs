using DimScreenSaver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class PowerBroadcastWatcher : NativeWindow, IDisposable
{
    // GUID do monitorowania zdarzeń zasilania (nieużywany w tej klasie, ale pozostawiony z oryginału)
    private static readonly Guid GUID_MONITOR_POWER_ON = new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
    // Ścieżka do pliku logu (nieużywana bezpośrednio tutaj)
    private static readonly string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");

    // Logger do konsoli lub pliku
    private static void Log(string msg) => AppLogger.Log("PowerBroadcastWatcher", msg);

    // Singleton
    public static PowerBroadcastWatcher Instance { get; private set; }

    // Kolejka ostatnich komunikatów i czas ostatniego wykrycia otwarcia ekranu
    private DateTime lastWakeDetect = DateTime.MinValue;
    private readonly Queue<int> msgSequence = new Queue<int>(3);

    // Ukryta forma do przechwytywania komunikatów
    private HiddenForm form;
    private bool disposed;

    private class HiddenForm : Form
    {
        protected override void SetVisibleCore(bool value)
            => base.SetVisibleCore(false);
    }

    public PowerBroadcastWatcher()
    {
        Instance = this;
        form = new HiddenForm();
        form.CreateControl();
        AssignHandle(form.Handle);
        Log("🔔 PowerBroadcastWatcher aktywny – nasłuchuję WM_POWERBROADCAST");
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;

        // 📜 Loguj każdy komunikat
        Log($"🎯 NativeWindow: Msg=0x{m.Msg:X} WParam=0x{m.WParam.ToInt64():X} LParam=0x{m.LParam.ToInt64():X}");

        // 🔁 Rejestrujemy ostatnie 3 komunikaty
        msgSequence.Enqueue(m.Msg);
        if (msgSequence.Count > 3)
            msgSequence.Dequeue();

        // ✅ Detekcja sekwencji po otwarciu ekranu (bez suspenda)
        if ((DateTime.Now - lastWakeDetect).TotalSeconds > 5 && msgSequence.Count == 3)
        {
            var msgArray = msgSequence.ToArray();
            var expected = new[] { 0x7E, 0x46, 0x24 };

            if (!expected.Except(msgArray).Any())
            {
                lastWakeDetect = DateTime.Now;

                if (msgArray[0] == 0x7E && msgArray[1] == 0x46 && msgArray[2] == 0x24)
                    Log("🟢 Wykryto sekwencję [GETTEXT, WINDOWPOSCHANGING, GETMINMAXINFO] → podniesiono klapę");
                else
                    Log("🔀 Wykryto te same 3 eventy [GETTEXT, WINDOWPOSCHANGING, GETMINMAXINFO], w innej kolejności → podniesiono klapę?");

                IdleTrayApp.Instance?.NotifyPowerEvent();
                IdleTrayApp.ClearWakeState();
            }
        }

        // 💤 Resume ze sleepa
        if (m.Msg == WM_POWERBROADCAST)
        {
            int wparam = m.WParam.ToInt32();
            if (wparam == PBT_APMRESUMESUSPEND || wparam == PBT_APMRESUMEAUTOMATIC)
            {
                Log($"🟢 System wznowiony z uśpienia (PBT: 0x{wparam:X})");
                IdleTrayApp.Instance?.NotifyPowerEvent();
                IdleTrayApp.ClearWakeState();
            }
        }

        base.WndProc(ref m);
    }

    // Dispose() zwalnia uchwyt i niszczy formę
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ReleaseHandle();
        form?.Dispose();
        form = null;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public int DataLength;
        public byte Data;
    }
}
