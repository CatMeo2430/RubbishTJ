using System;
using System.IO;
using Newtonsoft.Json;
using Taiji.Core.Models;

namespace Taiji.Core.Utils
{
    /// <summary>%APPDATA%\MeowCSharp\TaiJi 登录凭据读写。</summary>
    public static class CredentialStore
    {
        public static string DirectoryPath
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "MeowCSharp", "TaiJi");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "credentials.json"); }
        }

        public static StoredCredentials Load()
        {
            try
            {
                var path = FilePath;
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonConvert.DeserializeObject<StoredCredentials>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void Save(StoredCredentials credentials)
        {
            if (credentials == null)
            {
                Clear();
                return;
            }

            Directory.CreateDirectory(DirectoryPath);
            var json = JsonConvert.SerializeObject(credentials, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch
            {
            }
        }
    }
}
