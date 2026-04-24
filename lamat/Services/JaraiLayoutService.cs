using lamat.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace lamat.Services
{
    public class JaraiLayoutService
    {
        private Dictionary<string, JaraiKeyEntry> _map = new();

        public void Load(string path)
        {
            if (!File.Exists(path)) return;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<Dictionary<string, JaraiKeyEntry>>(
                File.ReadAllText(path), options);
            if (result != null) _map = result;
        }

        public string GetNormalLabel(string keyId) =>
            _map.TryGetValue(keyId, out var e) ? e.Normal : keyId;

        public string GetShiftedLabel(string keyId) =>
            _map.TryGetValue(keyId, out var e) ? e.Shifted : "";
    }
}
