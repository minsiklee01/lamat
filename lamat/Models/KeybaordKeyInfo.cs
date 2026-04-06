using System;
using System.Collections.Generic;
using System.Text;

// Used to define highlight position of a key on keyboard image

namespace lamat.Models
{
    internal class KeybaordKeyInfo
    {
        public string KeyId { get; set; } = "";
        public double x { get; set; }
        public double y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

    }
}
