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

        // Converts arbitrary Jarai text (may contain literal spaces between words) into a
        // per-physical-key sequence for character-by-character comparison, mirroring
        // DeriveKeySteps but returning the matched chars alongside each key/shift pair.
        // Space bar has no layout entry (it isn't remapped by Keyman) — it always maps to " ".
        // Returns null if a character in the text has no mapping.
        public List<(string Chars, string KeyId, bool IsShifted)>? DeriveCharKeySeq(string text)
        {
            var result = new List<(string, string, bool)>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    result.Add((" ", "Space", false));
                    i++;
                }
                else if (i + 1 < text.Length && TryGetKeyForChar(text.Substring(i, 2), out var keyId2, out var shifted2))
                {
                    result.Add((text.Substring(i, 2), keyId2, shifted2));
                    i += 2;
                }
                else if (TryGetKeyForChar(text.Substring(i, 1), out var keyId1, out var shifted1))
                {
                    result.Add((text.Substring(i, 1), keyId1, shifted1));
                    i += 1;
                }
                else
                {
                    return null; // unmappable character
                }
            }
            return result;
        }

        // Khmer/Jarai coeng sign (្) — always pulls in the *following* consonant, forming one
        // visually-joined subscript stack (e.g. "ស្រ" = S + coeng + R renders as one shape).
        public const char Coeng = '្';

        public static bool IsCombiningMark(char c)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            return cat == System.Globalization.UnicodeCategory.NonSpacingMark
                || cat == System.Globalization.UnicodeCategory.SpacingCombiningMark;
        }

        // Removes characters that have no key mapping (space is always kept) instead of
        // rejecting the whole string, so a line with a few stray/unsupported symbols can
        // still be used for practice rather than being discarded entirely. When the dropped
        // character is the *base* of a cluster (combining marks, or a coeng + subscript
        // consonant, immediately follow it), those dependents are dropped too — left in
        // place with no base to attach to, they'd render as an orphaned dotted-circle mark.
        public string StripUnmappableChars(string text, out int removedCount)
        {
            removedCount = 0;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    sb.Append(' ');
                    i++;
                }
                else if (i + 1 < text.Length && TryGetKeyForChar(text.Substring(i, 2), out _, out _))
                {
                    sb.Append(text, i, 2);
                    i += 2;
                }
                else if (TryGetKeyForChar(text.Substring(i, 1), out _, out _))
                {
                    sb.Append(text[i]);
                    i += 1;
                }
                else
                {
                    removedCount++;
                    i++;
                    while (i < text.Length)
                    {
                        if (IsCombiningMark(text[i]))
                        {
                            removedCount++;
                            i++;
                        }
                        else if (text[i] == Coeng && i + 1 < text.Length)
                        {
                            removedCount += 2;
                            i += 2;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return sb.ToString();
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
