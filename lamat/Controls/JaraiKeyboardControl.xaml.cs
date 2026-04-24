using lamat.Services;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace lamat.Controls
{
    public partial class JaraiKeyboardControl : UserControl
    {
        // All four main rows. Each row starts at x=0; stagger comes from special key widths.
        private static readonly string[][] Rows =
        [
            ["OemTilde", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D0", "OemMinus", "OemPlus", "Back"],
            ["Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "OemOpenBrackets", "OemCloseBrackets", "OemPipe"],
            ["CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", "OemSemicolon", "OemQuotes", "Return"],
            ["LeftShift", "Z", "X", "C", "V", "B", "N", "M", "OemComma", "OemPeriod", "OemQuestion", "RightShift"]
        ];

        // Standard key: 42 wide, 3 margin each side → 48px per slot.
        // All rows sum to 720px by sizing special keys to fill the remainder:
        //   Row 0: 13×48 + (90+6)  = 720
        //   Row 1: (66+6) + 12×48 + (66+6) = 720
        //   Row 2: (76+6) + 11×48 + (104+6) = 720
        //   Row 3: (100+6) + 10×48 + (128+6) = 720
        private const double Kw = 42;
        private const double Kh = 42;
        private const double Km = 3;

        private static readonly HashSet<string> SpecialKeys =
            ["Back", "Tab", "CapsLock", "Return", "LeftShift", "RightShift"];

        private readonly Dictionary<string, Border>    _keyBorders = new();
        private readonly Dictionary<string, TextBlock> _keyLabels  = new();
        private readonly HashSet<string>               _specials   = new();

        public JaraiKeyboardControl() => InitializeComponent();

        public void Initialize(JaraiLayoutService layout)
        {
            _keyBorders.Clear();
            _keyLabels.Clear();
            _specials.Clear();
            KeyboardHost.Children.Clear();

            bool first = true;
            foreach (var row in Rows)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin      = new Thickness(0, first ? 0 : 4, 0, 0)
                };
                first = false;

                foreach (var keyId in row)
                {
                    bool isSpecial = SpecialKeys.Contains(keyId);
                    if (isSpecial) _specials.Add(keyId);

                    var (border, mainLabel) = isSpecial
                        ? BuildSpecialKey(keyId)
                        : BuildJaraiKey(keyId, layout);

                    _keyBorders[keyId] = border;
                    _keyLabels[keyId]  = mainLabel;
                    rowPanel.Children.Add(border);
                }

                KeyboardHost.Children.Add(rowPanel);
            }
        }

        private (Border, TextBlock) BuildSpecialKey(string keyId)
        {
            var label = new TextBlock
            {
                Text                = SpecialLabel(keyId),
                FontSize            = 11,
                Foreground          = (Brush)FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            return (new Border
            {
                Width           = KeyWidth(keyId),
                Height          = Kh,
                Margin          = new Thickness(Km),
                CornerRadius    = new CornerRadius(6),
                Background      = (Brush)FindResource("SurfaceBrush"),
                BorderBrush     = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1.5),
                Child           = label
            }, label);
        }

        private (Border, TextBlock) BuildJaraiKey(string keyId, JaraiLayoutService layout)
        {
            var mainLabel = new TextBlock
            {
                Text                = layout.GetNormalLabel(keyId),
                FontSize            = 14,
                Foreground          = (Brush)FindResource("TextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            var engLabel = new TextBlock
            {
                Text                = EnglishLabel(keyId),
                FontSize            = 9,
                Foreground          = (Brush)FindResource("MutedBrush"),
                Margin              = new Thickness(3, 2, 0, 0),
                VerticalAlignment   = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var grid = new Grid();
            grid.Children.Add(mainLabel);
            grid.Children.Add(engLabel);

            return (new Border
            {
                Width           = KeyWidth(keyId),
                Height          = Kh,
                Margin          = new Thickness(Km),
                CornerRadius    = new CornerRadius(6),
                Background      = (Brush)FindResource("Surface2Brush"),
                BorderBrush     = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1.5),
                Child           = grid
            }, mainLabel);
        }

        public void SetHighlights(string[] keyIds)
        {
            var on = new HashSet<string>(keyIds);
            foreach (var (keyId, border) in _keyBorders)
            {
                bool lit     = on.Contains(keyId);
                bool special = _specials.Contains(keyId);

                border.Background = lit
                    ? (Brush)FindResource("AccentBrush")
                    : special
                        ? (Brush)FindResource("SurfaceBrush")
                        : (Brush)FindResource("Surface2Brush");

                border.BorderBrush    = lit ? (Brush)FindResource("AccentHoverBrush") : (Brush)FindResource("BorderBrush");
                border.BorderThickness = new Thickness(lit ? 2 : 1.5);

                _keyLabels[keyId].Foreground = lit
                    ? Brushes.White
                    : special
                        ? (Brush)FindResource("MutedBrush")
                        : (Brush)FindResource("TextBrush");
            }
        }

        private static double KeyWidth(string keyId) => keyId switch
        {
            "Back"       => 90,
            "Tab"        => 66,
            "OemPipe"    => 66,
            "CapsLock"   => 76,
            "Return"     => 104,
            "LeftShift"  => 100,
            "RightShift" => 128,
            _            => Kw
        };

        private static string SpecialLabel(string keyId) => keyId switch
        {
            "Back"     => "Bksp",
            "Tab"      => "Tab",
            "CapsLock" => "Caps",
            "Return"   => "Enter",
            _          => "Shift"
        };

        private static string EnglishLabel(string keyId) => keyId switch
        {
            "D1" => "1", "D2" => "2", "D3" => "3", "D4" => "4", "D5" => "5",
            "D6" => "6", "D7" => "7", "D8" => "8", "D9" => "9", "D0" => "0",
            "OemTilde"         => "`",
            "OemMinus"         => "-",
            "OemPlus"          => "=",
            "OemOpenBrackets"  => "[",
            "OemCloseBrackets" => "]",
            "OemPipe"          => "\\",
            "OemSemicolon"     => ";",
            "OemQuotes"        => "'",
            "OemComma"         => ",",
            "OemPeriod"        => ".",
            "OemQuestion"      => "/",
            _ when keyId.Length == 1 => keyId.ToLower(),
            _ => ""
        };
    }
}
