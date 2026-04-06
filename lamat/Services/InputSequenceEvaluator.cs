using lamat.Models;
using System;
using System.Collections.Generic;
using System.Text;

// Position/word practice evaluation engine

namespace lamat.Services
{
    public enum KeyInputResult
    {
        CorrectStep,
        WrongStep,
        ItemCompleted
    }
    public class InputSequenceEvaluator
    {
        public KeyInputResult Evaluate(KeySequencePracticeItem item, int currentStepIndex, string actualKeyId)
        {
            if (item.Steps.Count == 0 || currentStepIndex >= item.Steps.Count)
            {
                return KeyInputResult.WrongStep;
            }

            string expectedKeyID = item.Steps[currentStepIndex].KeyId;

            if (!string.Equals(expectedKeyID, actualKeyId, System.StringComparison.OrdinalIgnoreCase))
            {
                return KeyInputResult.WrongStep;
            }

            bool isLastStep = currentStepIndex == item.Steps.Count - 1;

            if (isLastStep)
            {
                return KeyInputResult.ItemCompleted;
            }

            return KeyInputResult.CorrectStep;
        }
    }
}
