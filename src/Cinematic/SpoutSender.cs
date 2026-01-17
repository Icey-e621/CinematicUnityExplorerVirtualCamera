using MelonLoader;
using System.Runtime.InteropServices;

namespace CinematicUnityExplorer.Cinematic
{
    public class SpoutSender
    {
        private const string SPOUT_DLL = "SpoutLibrary.dll";

        // The only exported C function - returns pointer to Spout object
        [DllImport(SPOUT_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetSpout();

        // Delegate types for virtual table functions
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate void SetSenderNameDelegate(IntPtr thisPtr, string sendername);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SetSenderFormatDelegate(IntPtr thisPtr, uint dwFormat);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void ReleaseSenderDelegate(IntPtr thisPtr, uint dwMsec);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate bool SendTextureDelegate(IntPtr thisPtr, uint TextureID, uint TextureTarget, uint width, uint height, bool bInvert, uint HostFBO);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate bool IsInitializedDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void ReleaseDelegate(IntPtr thisPtr);

        private IntPtr spoutPtr;
        private string senderName;
        private uint width;
        private uint height;

        // Virtual table function indices (based on the header file order)
        private const int VTABLE_SetSenderName = 0;
        private const int VTABLE_SetSenderFormat = 1;
        private const int VTABLE_ReleaseSender = 2;
        private const int VTABLE_SendFbo = 3;
        private const int VTABLE_SendTexture = 4;
        private const int VTABLE_SendImage = 5;
        private const int VTABLE_IsInitialized = 6;
        // ... Release is at the very end of the vtable

        private T GetVTableFunction<T>(int index) where T : class
        {
            try
            {
                // Read the vtable pointer from the object
                IntPtr vTable = Marshal.ReadIntPtr(spoutPtr);
                // Read the function pointer from the vtable
                IntPtr funcPtr = Marshal.ReadIntPtr(vTable, index * IntPtr.Size);
                // Convert to delegate
                return Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T)) as T;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to get vtable function at index {index}: {ex}");
                return null;
            }
        }

        public bool Initialize(string name, int w, int h)
        {
            try
            {
                senderName = name;
                width = (uint)w;
                height = (uint)h;

                // Get Spout instance
                spoutPtr = GetSpout();
                if (spoutPtr == IntPtr.Zero)
                {
                    MelonLogger.Error("Failed to get Spout instance");
                    return false;
                }

                // Set sender name
                var setSenderName = GetVTableFunction<SetSenderNameDelegate>(VTABLE_SetSenderName);
                if (setSenderName == null)
                {
                    MelonLogger.Error("Failed to get SetSenderName function");
                    return false;
                }
                setSenderName(spoutPtr, senderName);

                // Set format to DXGI_FORMAT_R8G8B8A8_UNORM (28)
                var setSenderFormat = GetVTableFunction<SetSenderFormatDelegate>(VTABLE_SetSenderFormat);
                if (setSenderFormat != null)
                {
                    setSenderFormat(spoutPtr, 28);
                }

                MelonLogger.Msg($"Spout sender initialized: {senderName} ({width}x{height})");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Exception during Spout initialization: {ex}");
                if (spoutPtr != IntPtr.Zero)
                {
                    try { Dispose(); } catch { }
                }
                return false;
            }
        }

        public void SendFrame(RenderTexture texture)
        {
            if (spoutPtr == IntPtr.Zero || texture == null)
                return;

            try
            {
                uint textureID = (uint)texture.GetNativeTextureID();
                const uint GL_TEXTURE_2D = 0x0DE1;

                var sendTexture = GetVTableFunction<SendTextureDelegate>(VTABLE_SendTexture);
                if (sendTexture != null)
                {
                    bool success = sendTexture(spoutPtr, textureID, GL_TEXTURE_2D, width, height, false, 0);

                    // Only log errors occasionally to avoid spam
                    if (!success && Time.frameCount % 300 == 0)
                    {
                        MelonLogger.Warning("Failed to send texture to Spout");
                    }
                }
            }
            catch (Exception ex)
            {
                // Only log occasionally to avoid spam
                if (Time.frameCount % 300 == 0)
                {
                    MelonLogger.Error($"Exception sending frame: {ex}");
                }
            }
        }

        public bool GetInitialized()
        {
            if (spoutPtr == IntPtr.Zero)
                return false;

            try
            {
                var isInitialized = GetVTableFunction<IsInitializedDelegate>(VTABLE_IsInitialized);
                if (isInitialized != null)
                {
                    return isInitialized(spoutPtr);
                }
            }
            catch { }

            return false;
        }

        public void Dispose()
        {
            if (spoutPtr == IntPtr.Zero)
                return;

            try
            {
                // ReleaseSender with 0 milliseconds (immediate)
                var releaseSender = GetVTableFunction<ReleaseSenderDelegate>(VTABLE_ReleaseSender);
                if (releaseSender != null)
                {
                    releaseSender(spoutPtr, 0);
                }

                // The Release function is the last virtual function
                // We need to count all functions in the interface to get the right index
                // For now, we'll just set the pointer to zero
                // The Spout object will be cleaned up when the DLL unloads

                MelonLogger.Msg($"Spout sender released: {senderName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Exception during Spout disposal: {ex}");
            }
            finally
            {
                spoutPtr = IntPtr.Zero;
            }
        }
    }
}