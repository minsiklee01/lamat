using lamat.Models;
using System;
using System.Collections.Generic;
using System.Text;

// manage sentence practice progress status

namespace lamat.Services
{
    public class SentenceSessionService
    {
        private List<SentencePracticeItem> _items = new();
        private string[] _currentWords = Array.Empty<string>();

        public int CurrentSentenceIndex { get; private set; } = 0;
        public int CurrentWordIndex { get; private set; } = 0;

        public void LoadItem(List<SentencePracticeItem> items)
        {
            _items = items;
            CurrentSentenceIndex = 0;
            LoadCurrentSentenceWords();
        }

        public SentencePracticeItem? GetCurrentSentence()
        {
            if (_items.Count == 0 || CurrentSentenceIndex >= _items.Count)
            {
                return null;
            }

            return _items[CurrentSentenceIndex];
        }

        public string? GetCurrentTargetWord()
        {
            if (_currentWords.Length == 0 || CurrentWordIndex >= _currentWords.Length)
            {
                return null;
            }

            return _currentWords[CurrentWordIndex];
        }

        public bool IsCurrentSentenceCompleted()
        {
            return CurrentWordIndex >= _currentWords.Length;
        }
        public void AdvanceSentence()
        {
            CurrentSentenceIndex++;
            LoadCurrentSentenceWords();
        }
        public bool HasMoreSentences()
        {
            return CurrentSentenceIndex < _items.Count;
        }

        public int TotalSentenceCount => _items.Count;

        private void LoadCurrentSentenceWords()
        {
            CurrentWordIndex = 0;

            var sentence = GetCurrentSentence();

            if (sentence == null)
            {
                _currentWords = Array.Empty<string>();
                return;
            }

            _currentWords = sentence.DisplayText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
