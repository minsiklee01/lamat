using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Markup;

// Single step of a key sequence

namespace lamat.Models
{
    public class KeyStep
    {
        // Identifier of the key to be pressed
        public string KeyId { get; set; } = "";
        // Label shown in UI
        public string? DisplayLabel { get; set; }
    }
}
