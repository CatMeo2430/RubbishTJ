using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Taiji.Engine.Latex
{
    /// <summary>从 Taiji.Engine 嵌入资源解压并加载 ratex_ffi.dll。</summary>
    internal static class RatexNativeLoader
    {
        private const string ResourceName = "ratex_ffi.dll";
        private const string DllFileName = "ratex_ffi.dll";
        private static readonly object Sync = new object();
        private static bool _loaded;
        private static string _dllPath;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        internal static bool TryEnsureLoaded(out string path)
        {
            lock (Sync)
            {
                if (_loaded)
                {
                    path = _dllPath;
                    return true;
                }

                var sideBySide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllFileName);
                if (File.Exists(sideBySide))
                {
                    if (TryLoad(sideBySide))
                    {
                        path = _dllPath;
                        return true;
                    }
                }

                var embedded = ReadEmbeddedDll();
                if (embedded == null || embedded.Length == 0)
                {
                    path = null;
                    return false;
                }

                var extractDir = Path.Combine(Path.GetTempPath(), "MeowCSharp", "TaiJi");
                Directory.CreateDirectory(extractDir);
                var extractPath = Path.Combine(extractDir, DllFileName);

                if (!File.Exists(extractPath) || !HashEquals(embedded, File.ReadAllBytes(extractPath)))
                    File.WriteAllBytes(extractPath, embedded);

                if (!TryLoad(extractPath))
                {
                    path = null;
                    return false;
                }

                path = _dllPath;
                return true;
            }
        }

        private static byte[] ReadEmbeddedDll()
        {
            var assembly = typeof(RatexNativeLoader).Assembly;
            using (var stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    return null;

                var bytes = new byte[stream.Length];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                        break;
                    offset += read;
                }

                if (offset != bytes.Length)
                    return null;

                return bytes;
            }
        }

        private static bool HashEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
                return false;

            using (var sha = SHA256.Create())
            {
                var leftHash = sha.ComputeHash(left);
                var rightHash = sha.ComputeHash(right);
                if (leftHash.Length != rightHash.Length)
                    return false;

                for (var i = 0; i < leftHash.Length; i++)
                {
                    if (leftHash[i] != rightHash[i])
                        return false;
                }

                return true;
            }
        }

        private static bool TryLoad(string fullPath)
        {
            if (LoadLibrary(fullPath) == IntPtr.Zero)
                return false;

            _dllPath = fullPath;
            _loaded = true;
            return true;
        }
    }
}
