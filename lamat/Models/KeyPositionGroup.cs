using System.Collections.Generic;

namespace lamat.Models
{
    public sealed record KeyPositionGroup(string Name, string[] KeyIds);

    public static class KeyPositionGroups
    {
        public static readonly IReadOnlyList<KeyPositionGroup> All = new KeyPositionGroup[]
        {
            new("Basic",        new[] { "A", "S", "D", "F", "J", "K", "L", "OemSemicolon" }),
            new("Left Top",     new[] { "Q", "W", "E", "R" }),
            new("Left Bottom",  new[] { "Z", "X", "C", "V" }),
            new("Right Top",    new[] { "U", "I", "O", "P" }),
            new("Right Bottom", new[] { "M", "OemComma", "OemPeriod", "OemQuestion" }),
            new("Middle",       new[] { "T", "Y", "G", "H", "B", "N" }),
            new("Number",       new[] { "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D0", "OemMinus", "OemPlus" }),
        };
    }
}
