using System.IO;
using System.Security.Cryptography;
using System.Text;
using Daryva.Services.Platform;

namespace Daryva.Services.Platform
{
    /// <summary>
    /// Cross-platform secure store implementation.
    /// Windows: Uses DPAPI (ProtectedData)
    /// macOS/Linux: Uses AES encryption with a user-specific key file (TODO: implement proper Keychain on macOS)
    /// </summary>
    public class SecureStore : ISecureStore
    {
        private readonly string _storePath;
        private readonly Dictionary<string, string> _cache = new();
        private bool _cacheLoaded = false;

        public SecureStore(IAppPaths appPaths)
        {
            _storePath = Path.Combine(appPaths.AppData, "secure_store.dat");
            LoadCache();
        }

        public void Store(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            _cache[key] = value;
            SaveCache();
        }

        public string? Retrieve(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (!_cacheLoaded)
            {
                LoadCache();
            }

            return _cache.TryGetValue(key, out var value) ? value : null;
        }

        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (_cache.Remove(key))
            {
                SaveCache();
            }
        }

        private void LoadCache()
        {
            _cache.Clear();
            _cacheLoaded = true;

            if (!File.Exists(_storePath))
                return;

            try
            {
                var encryptedData = File.ReadAllBytes(_storePath);
                var decryptedJson = Decrypt(encryptedData);
                
                if (string.IsNullOrWhiteSpace(decryptedJson))
                    return;

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(decryptedJson));
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // If decryption fails, start with empty cache
                _cache.Clear();
            }
        }

        private void SaveCache()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_cache);
                var encryptedData = Encrypt(json);
                File.WriteAllBytes(_storePath, encryptedData);
            }
            catch
            {
                // Silently fail - secure store is optional
            }
        }

        private byte[] Encrypt(string plainText)
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: Use DPAPI
                var data = Encoding.UTF8.GetBytes(plainText);
                return System.Security.Cryptography.ProtectedData.Protect(
                    data, 
                    null, 
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
            }
            else
            {
                // macOS/Linux: Use AES with a user-specific key
                // TODO: On macOS, integrate with Keychain for better security
                var key = GetOrCreateUserKey();
                using var aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var ms = new MemoryStream();
                ms.Write(aes.IV, 0, aes.IV.Length);
                
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return ms.ToArray();
            }
        }

        private string Decrypt(byte[] cipherData)
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: Use DPAPI
                var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(
                    cipherData,
                    null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            else
            {
                // macOS/Linux: Use AES
                var key = GetOrCreateUserKey();
                using var aes = Aes.Create();
                aes.Key = key;

                using var ms = new MemoryStream(cipherData);
                var iv = new byte[aes.IV.Length];
                ms.Read(iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
        }

        private byte[] GetOrCreateUserKey()
        {
            // Generate a user-specific key file
            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Daryva",
                ".secure_key");

            if (File.Exists(keyPath))
            {
                return File.ReadAllBytes(keyPath);
            }

            // Generate a new 256-bit key
            using var aes = Aes.Create();
            aes.GenerateKey();
            var key = aes.Key;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(keyPath, key);
            
            // Set restrictive permissions (Unix only)
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    // chmod 600 (owner read/write only)
                    File.SetUnixFileMode(keyPath, 
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch
                {
                    // Ignore if not supported
                }
            }

            return key;
        }
    }
}
