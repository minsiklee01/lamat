using lamat.Models;
using System;
using System.Collections.Generic;
using System.Text;

// Used to decide which key to highlight.

namespace lamat.Services
{
    public class KeyboardHintService
    {
        public string GetHighlightKeyId(KeyStep? step)
        {
            if (step == null)
            {
                return "";
            }
            return step.KeyId;
        }
    }
}
