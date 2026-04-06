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
    }
}
