// Add this class to handle DLL extraction
using MelonLoader;
using System.Runtime.InteropServices;


namespace CinematicUnityExplorer.Cinematic
{
    public static class SpoutLoader
    {
        public static bool isLoaded = false;

        public static bool LoadSpout()
        {
            if (isLoaded) return true;

            try
            {
                // Load the DLL
                IntPtr handle = LoadLibrary(@"SpoutLibrary.dll");
                if (handle == IntPtr.Zero)
                {
                    MelonLogger.Error($"Failed to load SpoutLibrary.dll. Error code: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                isLoaded = true;
                MelonLogger.Msg("SpoutLibrary.dll loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load Spout: {ex}");
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}