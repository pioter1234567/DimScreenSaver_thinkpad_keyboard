using System;
using System.Diagnostics;
using System.IO;

public static class BatterySaverChecker
{
    public static bool IsBatterySaverActive()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "batterysaver.check"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Trim().ToLower().Contains("true");
            }
        }
        catch
        {
            return false; // w razie błędu traktujemy jak nieaktywny
        }
    }
}
