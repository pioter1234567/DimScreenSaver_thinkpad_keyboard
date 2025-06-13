using DimScreenSaver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class PowerBroadcastWatcher : NativeWindow
{
    private static readonly Guid GUID_MONITOR_POWER_ON = new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
    private DateTime lastWakeDetect = DateTime.MinValue;
    private readonly Queue<int> msgSequence = new Queue<int>(3);
    private static readonly string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");


    private class HiddenForm : Form
    {
        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);
    }
    private static void LogPower(string message)
    {
        string logFile = logPath;
        string logEntry = $"[PowerBroadcastWarcher] {DateTime.Now:HH:mm:ss} {message}";

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
    public PowerBroadcastWatcher()
    {
        var form = new HiddenForm();  // ukryta forma tylko do złapania handle
        form.CreateControl();         // wymusza utworzenie uchwytu
        AssignHandle(form.Handle);

        LogPower("🔔 PowerBroadcastWatcher aktywny – nasłuchuję WM_POWERBROADCAST");
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;

        // 📜 Loguj każdy komunikat
        LogPower($"🎯 NativeWindow: Msg=0x{m.Msg:X} WParam=0x{m.WParam.ToInt64():X} LParam=0x{m.LParam.ToInt64():X}");

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
                    LogPower("🟢 Wykryto sekwencję [GETTEXT, WINDOWPOSCHANGING, GETMINMAXINFO] → podniesiono klapę");
                else
                    LogPower("🔀 Wykryto te same 3 eventy [GETTEXT, WINDOWPOSCHANGING, GETMINMAXINFO], w innej kolejności → podniesiono klapę?");
                
                IdleTrayApp.ClearWakeState();

            }
        }


        // 💤 Resume ze sleepa
        if (m.Msg == WM_POWERBROADCAST)
        {
            int wparam = m.WParam.ToInt32();

            if (wparam == PBT_APMRESUMESUSPEND || wparam == PBT_APMRESUMEAUTOMATIC)
            {
                LogPower($"🟢 System wznowiony z uśpienia (PBT: 0x{wparam:X})");

                IdleTrayApp.ClearWakeState();
            }
        }

        base.WndProc(ref m);
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public int DataLength;
        public byte Data;
    }
}
