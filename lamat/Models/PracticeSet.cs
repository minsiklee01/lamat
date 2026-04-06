using System;
using System.Collections.Generic;
using System.Text;

// Model that groups practice items. Used to read the whole JSON and to manage set name with the list.

namespace lamat.Models
{
    public class PracticeSet<T>
    {
        public string Title { get; set; } = "";
        public List<T> Items { get; set; } = new();
    }
}
