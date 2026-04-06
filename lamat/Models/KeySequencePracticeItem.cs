using System;
using System.Collections.Generic;
using System.Text;

// Key model that defines text to type in with its sequences.

namespace lamat.Models
{
    public class KeySequencePracticeItem
    {
        public string DisplayText { get; set; } = "";
        public List<KeyStep> Steps { get; set; } = new();
    }
}
