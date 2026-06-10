using lamat.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace lamat.Services
{
    public class JaraiLayoutService
    {
        private Dictionary<string, JaraiKeyEntry> _map = new();
        // char(s) → (keyId, isShifted); 2-char entries must be checked before 1-char
        private Dictionary<string, (string KeyId, bool IsShifted)> _reverseMap = new();

        public void Load(string path)
        {
            if (!File.Exists(path)) return;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<Dictionary<string, JaraiKeyEntry>>(
                File.ReadAllText(path), options);
            if (result != null)
            {
                _map = result;
                BuildReverseMap();
            }
        }

        public string GetNormalLabel(string keyId) =>
            _map.TryGetValue(keyId, out var e) ? e.Normal : keyId;

        public string GetShiftedLabel(string keyId) =>
            _map.TryGetValue(keyId, out var e) ? e.Shifted : "";

        // Returns null if a character in the text has no mapping.
        public List<KeyStep>? DeriveKeySteps(string text)
        {
            var steps = new List<KeyStep>();
            int i = 0;
            while (i < text.Length)
            {
                // Try 2-char cluster first
                if (i + 1 < text.Length && _reverseMap.TryGetValue(text.Substring(i, 2), out var m2))
                {
                    if (m2.IsShifted) steps.Add(new KeyStep { KeyId = "LeftShift" });
                    steps.Add(new KeyStep { KeyId = m2.KeyId });
                    i += 2;
                }
                else if (_reverseMap.TryGetValue(text.Substring(i, 1), out var m1))
                {
                    if (m1.IsShifted) steps.Add(new KeyStep { KeyId = "LeftShift" });
                    steps.Add(new KeyStep { KeyId = m1.KeyId });
                    i += 1;
                }
                else
                {
                    return null; // unmappable character
                }
            }
            return steps;
        }

        public bool TryGetKeyForChar(string chars, out string keyId, out bool shifted)
        {
            if (_reverseMap.TryGetValue(chars, out var m))
            {
                keyId = m.KeyId; shifted = m.IsShifted; return true;
            }
            keyId = ""; shifted = false; return false;
        }

        private void BuildReverseMap()
        {
            _reverseMap.Clear();
            foreach (var (keyId, entry) in _map)
            {
                if (!string.IsNullOrEmpty(entry.Normal))
                    _reverseMap.TryAdd(entry.Normal, (keyId, false));
                if (!string.IsNullOrEmpty(entry.Shifted))
                    _reverseMap.TryAdd(entry.Shifted, (keyId, true));
            }
        }
    }
}
