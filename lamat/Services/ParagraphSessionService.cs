using lamat.Models;
using System.Collections.Generic;

// Tracks which line of the current story is active. Character-level progress within
// a line is tracked in MainWindow (mirrors how word practice tracks _wordCharIdx).

namespace lamat.Services
{
    public class ParagraphSessionService
    {
        private List<SentencePracticeItem> _lines = new();

        public int CurrentLineIndex { get; private set; }
        public int TotalLineCount => _lines.Count;

        public void LoadLines(List<SentencePracticeItem> lines)
        {
            _lines = lines;
            CurrentLineIndex = 0;
        }

        public SentencePracticeItem? GetCurrentLine() => PeekLine(0);

        // offset 0 = current line, negative = already-typed lines, positive = upcoming lines.
        public SentencePracticeItem? PeekLine(int offset)
        {
            int idx = CurrentLineIndex + offset;
            return idx >= 0 && idx < _lines.Count ? _lines[idx] : null;
        }

        public void AdvanceLine() => CurrentLineIndex++;
    }
}
