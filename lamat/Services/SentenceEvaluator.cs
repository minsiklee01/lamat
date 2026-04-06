using System;
using System.Collections.Generic;
using System.Text;

// Sentence practice evaluation engine
// Break sentence into words. Compare input word with target word. Evaluate upon space or enter. Decide if a sentence is complete.

namespace lamat.Services
{
    public class SentenceEvaluator
    {
        public bool IsWordMatch(string inputWord, string targetWord)
        {
            return Normalize(inputWord) == Normalize(targetWord);
        }

        private string Normalize(string text)
        {
            return text.Normalize(NormalizationForm.FormC);
        }
    }
}
