using Fusion.Photon.Realtime;
using GameJam.Multiplayer;

namespace GameJam.Multiplayer
{
    public static class PhotonLocalSettings
    {
        public static string LastDiagnostic { get; private set; } = "Sin comprobar";
        public static string LastVoiceDiagnostic { get; private set; } = "Sin comprobar";

        public static string ReadVoiceAppId()
        {
            string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "GameJamPlus26", "Photon", "voice-appid.dpapi");
            if (!System.IO.File.Exists(path))
            {
                var directory = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath));
                for (int i = 0; i < 6 && directory != null; i++, directory = directory.Parent)
                {
                    var candidate = System.IO.Path.Combine(directory.FullName, ".local", "Photon", "voice-appid.dpapi");
                    if (System.IO.File.Exists(candidate)) { path = candidate; break; }
                }
            }
            if (!System.IO.File.Exists(path)) { LastVoiceDiagnostic = "Falta configurar el App ID local de Voice."; return null; }
            byte[] bytes = null;
            try
            {
                bytes = Unprotect(System.IO.File.ReadAllBytes(path));
                string id = System.Text.Encoding.UTF8.GetString(bytes).Trim();
                if (!System.Guid.TryParse(id, out _)) { LastVoiceDiagnostic = "El App ID cifrado de Voice no tiene formato válido."; return null; }
                LastVoiceDiagnostic = "Voice: configuración cifrada cargada.";
                return id;
            }
            catch (System.Exception exception) when (exception is System.IO.IOException || exception is System.UnauthorizedAccessException || exception is System.ComponentModel.Win32Exception || exception is System.DllNotFoundException)
            {
                LastVoiceDiagnostic = "No se pudo leer la configuración cifrada de Voice en este usuario de Windows.";
                return null;
            }
            finally { if (bytes != null) System.Array.Clear(bytes, 0, bytes.Length); }
        }
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Read();
            UnityEngine.Debug.Log("[Photon local config] " + LastDiagnostic);
        }

        public static FusionAppSettings Read()
        {
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor &&
                UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsPlayer)
            {
                LastDiagnostic = "La configuración DPAPI requiere Windows.";
                return null;
            }
            string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "GameJamPlus26", "Photon", "appid.dpapi");
            if (!System.IO.File.Exists(path))
            {
                // Virtual players run from Library/VP/<clone>; the ignored project-local copy is a fallback.
                var directory = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath));
                for (int i = 0; i < 6 && directory != null; i++, directory = directory.Parent)
                {
                    var candidate = System.IO.Path.Combine(directory.FullName, ".local", "Photon", "appid.dpapi");
                    if (System.IO.File.Exists(candidate)) { path = candidate; break; }
                }
            }
            if (!System.IO.File.Exists(path))
            {
                LastDiagnostic = "No se encontró el archivo cifrado local de Photon.";
                return null;
            }
            try
            {
                byte[] bytes = Unprotect(System.IO.File.ReadAllBytes(path));
                try
                {
                    var result = new FusionAppSettings();
                    // Use independent defaults: reading a global Editor asset can initialize Editor windows in a clone.
                    result.AppIdFusion = System.Text.Encoding.UTF8.GetString(bytes);
                    result.AppVersion = "MarsCoop-v1";
                    // Fusion 2.1.1 SetupAppSettings calls FixedRegion.ToLower() without a null check.
                    // Empty means automatic region selection; a freshly constructed settings object leaves it null.
                    result.FixedRegion = string.Empty;
                    result.ClientLogging = Photon.Client.LogLevel.Error;
                    if (!System.Guid.TryParse(result.AppIdFusion, out _))
                    {
                        LastDiagnostic = "El archivo cifrado no contiene un App ID válido.";
                        return null;
                    }
                    LastDiagnostic = "Configuración cifrada cargada.";
                    UnityEngine.Debug.Log("[Photon local config] stage=validated source=" +
                        (path.Contains(".local") ? "project-fallback" : "windows-profile") +
                        " appIdPresent=true appIdFormatValid=true region=auto pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
                    return result;
                }
                finally { System.Array.Clear(bytes, 0, bytes.Length); }
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                LastDiagnostic = "Windows no pudo descifrar la configuración (código " + exception.NativeErrorCode + ").";
            }
            catch (System.IO.IOException)
            {
                LastDiagnostic = "No se pudo leer el archivo cifrado; vuelve a intentarlo.";
            }
            catch (System.UnauthorizedAccessException)
            {
                LastDiagnostic = "Windows denegó el acceso al archivo cifrado.";
            }
            return null;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Blob { public int Size; public System.IntPtr Data; }
        [System.Runtime.InteropServices.DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(ref Blob input, System.IntPtr description, System.IntPtr entropy,
            System.IntPtr reserved, System.IntPtr prompt, int flags, out Blob output);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern System.IntPtr LocalFree(System.IntPtr pointer);

        private static byte[] Unprotect(byte[] ciphertext)
        {
            var input = new Blob { Size = ciphertext.Length, Data = System.Runtime.InteropServices.Marshal.AllocHGlobal(ciphertext.Length) };
            var output = new Blob();
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(ciphertext, 0, input.Data, ciphertext.Length);
                if (!CryptUnprotectData(ref input, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero, 1, out output))
                    throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                var result = new byte[output.Size];
                System.Runtime.InteropServices.Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                if (output.Data != System.IntPtr.Zero)
                {
                    for (int i = 0; i < output.Size; i++) System.Runtime.InteropServices.Marshal.WriteByte(output.Data, i, 0);
                    LocalFree(output.Data);
                }
                System.Runtime.InteropServices.Marshal.FreeHGlobal(input.Data);
            }
        }
    }
}
