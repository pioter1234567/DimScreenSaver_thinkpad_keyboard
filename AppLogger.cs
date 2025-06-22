using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DimScreenSaver
{
    public static class AppLogger
    {
        private static readonly object logLock = new object();
        private static int logCounter = 0;
        private const int TrimFrequency = 10; // Przycinaj częściej, żeby lepiej kontrolować długość
        private const int MaxLines = 3000;
        private static readonly string logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");

        public static void Log(string prefix, string message)
        {
            string entry = $"[{prefix}] {DateTime.Now:HH:mm:ss} {message}";
            try
            {
                lock (logLock)
                {
                    using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(entry);
                        writer.Flush();
                        stream.Flush(true); // <- TO GWARANTUJE zapis na dysk, nie tylko do bufora systemowego
                    }


                    logCounter++;

                    if (logCounter % TrimFrequency == 0)
                        TrimLogFile();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] Błąd logowania: {ex.Message}");
            }
        }

        private static void TrimLogFile()
        {
            try
            {
                if (!File.Exists(logPath)) return;

                string[] lines = File.ReadAllLines(logPath);
                if (lines.Length > MaxLines)
                {
                    string[] trimmed = lines.Skip(lines.Length - MaxLines).ToArray();
                    using (var fs = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(fs))
                        foreach (var line in trimmed)
                            sw.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] Błąd przycinania loga: {ex.Message}");
            }
        }
    }
}
