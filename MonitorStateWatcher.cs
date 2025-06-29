﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DimScreenSaver
{
    public static class MonitorStateWatcher
    {
        public static bool IsMonitorOn { get; private set; } = true;
        public static event Action OnMonitorTurnedOn;
        public static event Action OnMonitorTurnedOff;
        private static readonly string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");
        private static Guid GUID_MONITOR_POWER_ON = new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
        private static IntPtr hMonitorNotify = IntPtr.Zero;
        private static MessageWindow messageWindow;

        public static void Start()
        {
            if (messageWindow != null)
                return;

            messageWindow = new MessageWindow();
            hMonitorNotify = RegisterPowerSettingNotification(messageWindow.Handle, ref GUID_MONITOR_POWER_ON, DEVICE_NOTIFY_WINDOW_HANDLE);
        }

        public static void Stop()
        {
            if (hMonitorNotify != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(hMonitorNotify);
                hMonitorNotify = IntPtr.Zero;
            }

            messageWindow?.Dispose();
            messageWindow = null;
            OnMonitorTurnedOn = null;
            OnMonitorTurnedOff = null;

        }

        // Logi
        private static void Log(string msg) => _ = AppLogger.LogAsync("MonitorStateWatcher", msg);
       
        private class MessageWindow : NativeWindow, IDisposable
        {
            private const int WM_POWERBROADCAST = 0x0218;
            private const int PBT_POWERSETTINGCHANGE = 0x8013;

            [StructLayout(LayoutKind.Sequential)]
            private struct POWERBROADCAST_SETTING
            {
                public Guid PowerSetting;
                public int DataLength;
                public byte Data;
            }

            public MessageWindow()
            {
                CreateParams cp = new CreateParams();
                this.CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_POWERBROADCAST)
                {


                    if (m.WParam.ToInt32() == PBT_POWERSETTINGCHANGE)
                    {
                        var ps = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(POWERBROADCAST_SETTING));


                        if (ps.PowerSetting == GUID_MONITOR_POWER_ON)
                        {
                            bool isOn = (ps.Data != 0);
                            IsMonitorOn = isOn;

                            if (isOn)
                            {
                                OnMonitorTurnedOn?.Invoke();
                                Log("🟢 Ekran fizycznie się WŁĄCZYŁ");
                                //Task.Run(() => IdleTrayApp.Instance?.NotifyPowerEvent());
                            }
                            else
                            {
                                OnMonitorTurnedOff?.Invoke();
                                Log("🔴 [MonitorStateWatcher] Ekran fizycznie się WYŁĄCZYŁ");
                                //Task.Run(() => IdleTrayApp.Instance?.NotifyPowerEvent());
                            }
                        }
                    }
                }

                base.WndProc(ref m);
            }



            public void Dispose()
            {
                this.DestroyHandle();
            }
        }

        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    }
}
