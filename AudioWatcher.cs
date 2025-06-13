using NAudio.CoreAudioApi;

namespace DimScreenSaver
{
    public static class AudioWatcher
    {
        private static readonly MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

        public static bool IsAudioPlaying()
        {
            try
            {
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.AudioMeterInformation.MasterPeakValue > 0.01;
            }
            catch
            {
                return false;
            }
        }
    }
}
