using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using lamat.Models;

namespace lamat.Services
{
    public class PracticeDataLoader
    {
        public PracticeSet<KeySequencePracticeItem> LoadKeySequencePracticeSet(string path)
        {
            if (!File.Exists(path))
            {
                return new PracticeSet<KeySequencePracticeItem>();
            }

            string json = File.ReadAllText(path);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<PracticeSet<KeySequencePracticeItem>>(json, options);

            return result ?? new PracticeSet<KeySequencePracticeItem>();
        }

        public PracticeSet<SentencePracticeItem> LoadSentencePracticeSet(string path)
        {

            var set = new PracticeSet<SentencePracticeItem>();

            if (!File.Exists(path))
            {
                return set;
            }

            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    set.Items.Add(new SentencePracticeItem
                    {
                        DisplayText = trimmed
                    });
                }
            }

            return set;
        }

        // Reads Jarai words from a text file; splits each line by spaces so multi-word lines
        // each become individual practice items. Skips tokens with unmappable characters.
        public PracticeSet<KeySequencePracticeItem> LoadWordPracticeFromTextFile(
            string path, JaraiLayoutService layout)
        {
            var set = new PracticeSet<KeySequencePracticeItem>();
            if (!File.Exists(path)) return set;

            foreach (var line in File.ReadAllLines(path))
            {
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    var word = token.Trim();
                    if (string.IsNullOrWhiteSpace(word)) continue;

                    var steps = layout.DeriveKeySteps(word);
                    if (steps == null) continue; // skip unmappable tokens

                    set.Items.Add(new KeySequencePracticeItem
                    {
                        DisplayText = word,
                        Steps = steps
                    });
                }
            }

            return set;
        }

        // Reads one sentence per line from a plain-text file.
        public PracticeSet<SentencePracticeItem> LoadSentencePracticeFromTextFile(string path)
            => LoadSentencePracticeSet(path); // same format — reuse existing logic
    }
}
