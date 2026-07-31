using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        // Reads a raw prose "story" file. Blank lines mark paragraph breaks; single newlines
        // within a paragraph are soft wraps and are joined back into one line with a space.
        // Hyphens ('-') are stripped — they're a formatting artifact in this content, not
        // meaningful punctuation. ASCII digit runs (verse/chapter markers — Jarai's own
        // numerals are the Khmer digit glyphs, mapped separately) are also stripped. The
        // result is then split into individual lines on Jarai/Latin sentence-ending
        // punctuation ('។' is the Jarai/Khmer period), so the caller doesn't need to
        // pre-format one sentence per line. Characters the keyboard layout can't produce
        // (stray symbols from the source document) are stripped out rather than discarding
        // the whole line; strippedCharCount reports how many were removed.
        public PracticeSet<SentencePracticeItem> LoadParagraphPracticeFromTextFile(
            string path, JaraiLayoutService layout, out int strippedCharCount)
        {
            var set = new PracticeSet<SentencePracticeItem>();
            strippedCharCount = 0;
            if (!File.Exists(path)) return set;

            string text = File.ReadAllText(path);
            var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n");

            foreach (var para in paragraphs)
            {
                string joined = string.Join(" ", para.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                                       .Select(l => l.Trim())
                                                       .Where(l => l.Length > 0));
                if (joined.Length == 0) continue;

                string stripped = joined.Replace("-", "");
                stripped = Regex.Replace(stripped, "[0-9]+", "");
                stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
                if (stripped.Length == 0) continue;

                foreach (var sentence in Regex.Split(stripped, @"(?<=[។.!?])\s+"))
                {
                    string trimmed = sentence.Trim();
                    if (trimmed.Length == 0) continue;

                    string clean = layout.StripUnmappableChars(trimmed, out int removed);
                    strippedCharCount += removed;
                    clean = Regex.Replace(clean, @"\s+", " ").Trim();
                    if (clean.Length == 0) continue;

                    set.Items.Add(new SentencePracticeItem { DisplayText = clean });
                }
            }

            return set;
        }
    }
}
