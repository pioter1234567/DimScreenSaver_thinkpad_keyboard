using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimScreenSaver
{
    public static class AppLogger
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static int _logCounter = 0;
        private const int TrimFrequency = 1000; // co 1000 wpisów przycinaj log
        private const int MaxLines = 3000;
        private static readonly string _logPath = Path.Combine(Path.GetTempPath(), "scrlog.txt");




        /// <summary>
        /// Zamienia cyfry na odpowiadające im indeksy dolne Unicode.
        /// </summary>
        private static string ToSubscript(string digits)
        {
            return digits
                .Replace('0', '₀')
                .Replace('1', '₁')
                .Replace('2', '₂')
                .Replace('3', '₃')
                .Replace('4', '₄')
                .Replace('5', '₅')
                .Replace('6', '₆')
                .Replace('7', '₇')
                .Replace('8', '₈')
                .Replace('9', '₉');
        }


        /// <summary>
        /// Zapisuje wpis do loga asynchronicznie.
        /// </summary>
        public static async Task LogAsync(string prefix, string message)
        {

            var now = DateTime.Now;
            string time = now.ToString("HH:mm:ss.");
            string msSub = ToSubscript(now.ToString("fff"));
            string entry = $"[{prefix}] {time}{msSub} {message}";

            
            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);
                // append entry
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                using (var stream = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, leaveOpen: false))
                {
                    await writer.WriteLineAsync(entry).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                _logCounter++;
                if (_logCounter % TrimFrequency == 0)
                {
                    // nie czekamy na trimming w wątku wywołującym
                    _ = TrimLogFileAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] Błąd logowania: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Przycina plik loga do ostatnich MaxLines linii (asynchronicznie).
        /// </summary>
        private static async Task TrimLogFileAsync()
        {
            try
            {
                if (!File.Exists(_logPath)) return;

                string[] allLines;
                using (var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, false))
                {
                    var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                    // usuwamy puste linie od razu przy dzieleniu
                    allLines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                }

                if (allLines.Length <= MaxLines) return;

                var trimmed = allLines.Skip(allLines.Length - MaxLines);

                using (var stream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, false))
                {
                    foreach (var line in trimmed)
                    {
                        // dodatkowo filtr bezpieczeństwa
                        if (!string.IsNullOrWhiteSpace(line))
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                    }
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] Błąd przycinania loga: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
