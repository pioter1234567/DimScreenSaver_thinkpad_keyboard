using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;


namespace DimScreenSaver
{
    internal static class Program
    {
       
        [STAThread]
        static void Main(string[] args)
        {
            

            using (var mutex = new Mutex(true, "DimScreenSaverMutex", out bool createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("DimScreenSaver już działa.", "Uwaga", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (args.Length > 0)
                {
                    string mode = args[0].ToLower();

                    if (mode == "/s")
                    {
                        StartScreensaver();
                    }
                    else if (mode == "/c")
                    {
                        MessageBox.Show("Brak ustawień do skonfigurowania.", "DimScreenSaver");
                    }
                    else if (mode == "/p")
                    {
                        // /p <HWND> – podgląd miniatury – zignorowane
                        return;
                    }
                    else
                    {
                        Application.Exit();
                    }
                }
                else
                {
                    // ręczne uruchomienie bez argumentów = test
                    StartScreensaver();
                }
            }
        }










        static void StartScreensaver()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            try
            {
                var app = new IdleTrayApp();



                Application.Idle += (s, e) =>
                {
                    if (IdleTrayApp.UISyncContext == null)
                        IdleTrayApp.UISyncContext = SynchronizationContext.Current;
                };

                Application.Run(app);
            }
            catch (Exception ex)
            {
                try
                {
                    string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DimScreenSaver");
                    Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, "crashlog.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex}\n");
                }
                catch
                {
                    // Jeżeli nawet logowanie padnie, to nie blokujemy MessageBoxa
                }

                MessageBox.Show("Program nie mógł się uruchomić. Szczegóły zapisano w AppData\\DimScreenSaver\\crashlog.txt", "Błąd krytyczny");
            }
        }


    }
}
