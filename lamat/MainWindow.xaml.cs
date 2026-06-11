using lamat.Controls;
using lamat.Models;
using lamat.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace lamat
{
    public partial class MainWindow : Window
    {
        private readonly PracticeDataLoader _loader = new();
        private readonly KeySequenceSessionService _keySessionService = new();
        private readonly JaraiLayoutService _jaraiLayoutService = new();
        private readonly SentenceSessionService _sentenceSessionService = new();
        private readonly SentenceEvaluator _sentenceEvaluator = new();

        private PracticeModeType _currentMode = PracticeModeType.WordPractice;
        private PracticeModeType _fileSelectMode = PracticeModeType.WordPractice;
        private bool _isAdvancing = false;
        private bool _shiftHeld = false;

        // Word practice — character-based sequence derived from steps at load time
        private readonly List<string> _displayHistory = new();
        private List<(string Chars, string KeyId, bool IsShifted)> _wordCharSeq = new();
        private int _wordCharIdx = 0;
        private bool _wordHasError = false;

        // Sentence practice
        private string _typedWordsDisplay = "";
        private readonly List<string> _submittedWords = new();
        private List<bool?> _wordResults = new();
        private bool _sentenceFailed = false;

        // Position practice
        private string[] _positionGroupKeys = [];
        private string _positionGroupName = "";
        private string _positionTargetKey = "";
        private bool _positionTargetShifted = false;
        private string _positionTargetChar = "";
        private string? _positionErrorKey = null;
        private int _positionCorrect = 0;
        private readonly Random _rng = new();

        // Raw Win32 VK fallback: when Keyman reports Key.ImeProcessed + ImeProcessedKey=None,
        // recover the physical key from the most recent WM_KEYDOWN wParam.
        private int _lastRawVirtualKey;

        public MainWindow()
        {
            InitializeComponent();
            LoadAllData();
            JaraiKeyboard.Initialize(_jaraiLayoutService);
            Loaded += (_, _) =>
            {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)
                          ?.AddHook(WndProc);
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_SYSKEYDOWN = 0x0104;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                _lastRawVirtualKey = (int)wParam;
            return IntPtr.Zero;
        }

        private void LoadAllData()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            var wordSet = _loader.LoadKeySequencePracticeSet(Path.Combine(basePath, "Data", "word-practice.json"));
            _keySessionService.LoadItems(wordSet.Items);
            var sentenceSet = _loader.LoadSentencePracticeSet(Path.Combine(basePath, "Data", "sentence-practice.json"));
            _sentenceSessionService.LoadItem(sentenceSet.Items);
            _jaraiLayoutService.Load(Path.Combine(basePath, "Data", "jarai-keyboard-layout.json"));
        }

        private void LoadWordPractice()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            var set = _loader.LoadKeySequencePracticeSet(Path.Combine(basePath, "Data", "word-practice.json"));
            _keySessionService.LoadItems(set.Items);
        }

        // Converts the steps array of a word practice item into a flat character sequence.
        // Shift steps are consumed here; each remaining entry is (expectedChars, keyId, isShifted).
        private List<(string Chars, string KeyId, bool IsShifted)> ComputeWordCharSeq(KeySequencePracticeItem item)
        {
            var result = new List<(string, string, bool)>();
            bool nextShifted = false;
            foreach (var step in item.Steps)
            {
                if (ModifierKeyIds.IsModifier(step.KeyId))
                {
                    nextShifted = step.KeyId is "LeftShift" or "RightShift";
                }
                else
                {
                    string chars = nextShifted
                        ? _jaraiLayoutService.GetShiftedLabel(step.KeyId)
                        : _jaraiLayoutService.GetNormalLabel(step.KeyId);
                    if (!string.IsNullOrEmpty(chars))
                        result.Add((chars, step.KeyId, nextShifted));
                    nextShifted = false;
                }
            }
            return result;
        }

        private void ShowHome()
        {
            HomePanel.Visibility = Visibility.Visible;
            FileSelectPanel.Visibility = Visibility.Collapsed;
            PracticePanel.Visibility = Visibility.Collapsed;
            _isAdvancing = false;
            _shiftHeld = false;
            _displayHistory.Clear();
            _wordCharSeq = new();
            _wordCharIdx = 0;
            _wordHasError = false;
            _typedWordsDisplay = "";
            _submittedWords.Clear();
            _wordResults.Clear();
            _sentenceFailed = false;
            _positionTargetKey = "";
            _positionTargetShifted = false;
            _positionTargetChar = "";
            _positionErrorKey = null;
        }

        private void SwitchMode(PracticeModeType mode)
        {
            _currentMode = mode;
            _isAdvancing = false;
            _shiftHeld = false;
            _wordHasError = false;
            _displayHistory.Clear();
            ActualKeyText.Text = "";
            ActualKeyText.Foreground = (Brush)FindResource("MutedBrush");
            StatusText.Text = "";
            JaraiKeyboard.SetHighlights([]);

            HomePanel.Visibility = Visibility.Collapsed;
            FileSelectPanel.Visibility = Visibility.Collapsed;
            PracticePanel.Visibility = Visibility.Visible;

            bool isSentence = mode == PracticeModeType.SentencePractice;
            bool isPosition = mode == PracticeModeType.PositionPractice;
            KeySequencePanel.Visibility = (!isSentence && !isPosition) ? Visibility.Visible : Visibility.Collapsed;
            SentencePanel.Visibility    = isSentence ? Visibility.Visible : Visibility.Collapsed;
            PositionPanel.Visibility    = isPosition ? Visibility.Visible : Visibility.Collapsed;
            JaraiKeyboard.Visibility    = !isSentence ? Visibility.Visible : Visibility.Collapsed;

            if (isSentence)
            {
                _typedWordsDisplay = "";
                _submittedWords.Clear();
                _sentenceFailed = false;
                SentenceInputDisplay.Text = "";
                InitWordResults();
                SentenceInputBox.Clear();
                Dispatcher.BeginInvoke(new Action(() => SentenceInputBox.Focus()), DispatcherPriority.Input);
            }
            else if (isPosition)
            {
                _positionCorrect = 0;
                _positionErrorKey = null;
                PickNextPositionTarget();
                PositionInputBox.Clear();
                Dispatcher.BeginInvoke(new Action(() => PositionInputBox.Focus()), DispatcherPriority.Input);
            }
            else
            {
                var item = _keySessionService.GetCurrentItem();
                _wordCharSeq = item != null ? ComputeWordCharSeq(item) : new();
                _wordCharIdx = 0;
                WordPracticeInputBox.Clear();
                Dispatcher.BeginInvoke(new Action(() => WordPracticeInputBox.Focus()), DispatcherPriority.Input);
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_currentMode == PracticeModeType.SentencePractice)
                RefreshSentenceUI();
            else if (_currentMode == PracticeModeType.PositionPractice)
                RefreshPositionUI();
            else
                RefreshKeySequenceUI();
        }

        private void RefreshKeySequenceUI()
        {
            var currentItem = _keySessionService.GetCurrentItem();
            if (currentItem == null)
            {
                TargetText.Text = "Practice complete!";
                ProgressText.Text = "";
                ExpectedKeyText.Text = "";
                ActualKeyText.Text = "";
                StatusText.Text = "All items finished.";
                JaraiKeyboard.SetHighlights([]);
                return;
            }

            TargetText.Text = currentItem.DisplayText;
            ProgressText.Text = $"{_keySessionService.CurrentItemIndex + 1} / {_keySessionService.TotalItemCount}";

            if (_wordCharIdx < _wordCharSeq.Count)
            {
                var (_, keyId, isShifted) = _wordCharSeq[_wordCharIdx];
                ExpectedKeyText.Text = (isShifted ? "Shift + " : "") +
                                       JaraiKeyboardControl.EnglishLabel(keyId).ToUpperInvariant();
                string? shiftKey = isShifted ? "LeftShift" : null;
                JaraiKeyboard.SetHighlights([keyId], null, shiftKey, _shiftHeld);

                if (!_wordHasError)
                {
                    StatusText.Text = isShifted
                        ? (_shiftHeld ? "Shift held — now press the highlighted key"
                                      : "Hold Shift, then press the highlighted key")
                        : "";
                }
            }
            else
            {
                ExpectedKeyText.Text = "";
                JaraiKeyboard.SetHighlights([]);
                if (!_wordHasError) StatusText.Text = "";
            }
        }

        private void RefreshPositionUI()
        {
            TargetText.Text = _positionTargetChar;
            ProgressText.Text = $"{_positionGroupName}  ·  {_positionCorrect} correct";
            string? shiftKey = _positionTargetShifted ? "LeftShift" : null;
            JaraiKeyboard.SetHighlights([_positionTargetKey], _positionErrorKey, shiftKey, _shiftHeld);

            if (_positionErrorKey != null)
                StatusText.Text = "Wrong key — try again";
            else if (_positionTargetShifted && !_shiftHeld)
                StatusText.Text = "Hold Shift, then press the highlighted key";
            else if (_positionTargetShifted && _shiftHeld)
                StatusText.Text = "Shift held — now press the highlighted key";
            else
                StatusText.Text = "";
        }

        // Picks a random (key, normal/shifted) target from the current position group,
        // avoiding repeating the exact same key+shift combo as last time.
        // Only includes shifted chars that are Jarai (Khmer range) to avoid ASCII targets.
        private void PickNextPositionTarget()
        {
            if (_positionGroupKeys.Length == 0) return;

            var candidates = new List<(string Key, bool Shifted, string Chars)>();
            foreach (var key in _positionGroupKeys)
            {
                string norm = _jaraiLayoutService.GetNormalLabel(key);
                if (!string.IsNullOrEmpty(norm))
                    candidates.Add((key, false, norm));
                string shift = _jaraiLayoutService.GetShiftedLabel(key);
                if (IsJaraiChar(shift))
                    candidates.Add((key, true, shift));
            }

            var others = new List<(string Key, bool Shifted, string Chars)>();
            foreach (var c in candidates)
                if (!(c.Key == _positionTargetKey && c.Shifted == _positionTargetShifted))
                    others.Add(c);
            if (others.Count == 0) others = candidates;

            var pick = others[_rng.Next(others.Count)];
            _positionTargetKey = pick.Key;
            _positionTargetShifted = pick.Shifted;
            _positionTargetChar = pick.Chars;
            _positionErrorKey = null;
            _shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }

        private static bool IsJaraiChar(string? text) =>
            !string.IsNullOrEmpty(text) && text[0] >= 'ក' && text[0] <= '៿';

        private void RefreshSentenceUI()
        {
            var sentence = _sentenceSessionService.GetCurrentSentence();
            if (sentence == null)
            {
                TargetText.Text = "Practice complete!";
                ProgressText.Text = "";
                StatusText.Text = "All sentences finished.";
                return;
            }

            var words = sentence.DisplayText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int currentIdx = _sentenceSessionService.CurrentWordIndex;
            TargetText.Inlines.Clear();
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) TargetText.Inlines.Add(new Run(" "));
                Brush fg;
                if (i < _wordResults.Count && _wordResults[i] == true)
                    fg = (Brush)FindResource("SuccessBrush");
                else if (i < _wordResults.Count && _wordResults[i] == false)
                    fg = (Brush)FindResource("ErrorBrush");
                else if (i == currentIdx)
                    fg = (Brush)FindResource("AccentBrush");
                else
                    fg = (Brush)FindResource("TextBrush");
                TargetText.Inlines.Add(new Run(words[i]) { Foreground = fg });
            }
            ProgressText.Text = $"Sentence {_sentenceSessionService.CurrentSentenceIndex + 1} / {_sentenceSessionService.TotalSentenceCount}";
            StatusText.Text = "";
        }

        private void InitWordResults()
        {
            var sentence = _sentenceSessionService.GetCurrentSentence();
            int count = sentence?.DisplayText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
            _wordResults = new List<bool?>(new bool?[count]);
        }

        // ── Input handlers ────────────────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice)
            {
                Key k = e.Key switch
                {
                    Key.System       => e.SystemKey,
                    Key.ImeProcessed => e.ImeProcessedKey,
                    _ => e.Key
                };
                if (k == Key.Space || k == Key.Return)
                {
                    AdvanceSentenceWord();
                    e.Handled = true;
                }
                else if (k == Key.Back && !_sentenceFailed
                         && SentenceInputBox.Text.Length == 0
                         && _submittedWords.Count > 0)
                {
                    RevertLastWord();
                    e.Handled = true;
                }
                return;
            }

            if (_currentMode == PracticeModeType.WordPractice ||
                _currentMode == PracticeModeType.PositionPractice)
            {
                Key k = e.Key switch
                {
                    Key.System       => e.SystemKey,
                    Key.ImeProcessed => e.ImeProcessedKey,
                    _ => e.Key
                };
                if (k == Key.None && _lastRawVirtualKey != 0)
                    k = KeyInterop.KeyFromVirtualKey(_lastRawVirtualKey);
                if (k == Key.LeftShift || k == Key.RightShift)
                {
                    _shiftHeld = true;
                    RefreshUI();
                    // Do NOT set e.Handled — Shift must reach the TextBox so Keyman can compose shifted chars.
                    return;
                }

                if (k == Key.None) return;
                string pressedId = KeyToKeyId(k);
                if (ModifierKeyIds.IsModifier(pressedId)) return;
                e.Handled = true;

                // Position practice: check key is in the group and matches the target.
                if (_currentMode == PracticeModeType.PositionPractice)
                {
                    if (!_positionGroupKeys.Any(pk => string.Equals(pk, pressedId, StringComparison.OrdinalIgnoreCase))) return;
                    if (string.Equals(pressedId, _positionTargetKey, StringComparison.OrdinalIgnoreCase)
                        && _shiftHeld == _positionTargetShifted)
                    {
                        _positionCorrect++;
                        PickNextPositionTarget();
                    }
                    else
                    {
                        _positionErrorKey = pressedId;
                    }
                    RefreshPositionUI();
                    return;
                }

                // Word practice: compare physical key ID against the expected step.
                if (_currentMode == PracticeModeType.WordPractice && !_isAdvancing)
                {
                    if (_wordCharIdx >= _wordCharSeq.Count) return;
                    var (expectedChars, expectedKeyId, isShifted) = _wordCharSeq[_wordCharIdx];
                    if (string.Equals(pressedId, expectedKeyId, StringComparison.OrdinalIgnoreCase)
                        && _shiftHeld == isShifted)
                    {
                        _wordHasError = false;
                        _displayHistory.Add(expectedChars);
                        ActualKeyText.Text = string.Join("", _displayHistory);
                        ActualKeyText.Foreground = (Brush)FindResource("SuccessBrush");
                        _wordCharIdx++;
                        if (_wordCharIdx >= _wordCharSeq.Count)
                        {
                            _isAdvancing = true;
                            _shiftHeld = false;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _keySessionService.AdvanceItem();
                                _isAdvancing = false;
                                _wordCharIdx = 0;
                                _wordHasError = false;
                                _displayHistory.Clear();
                                ActualKeyText.Text = "";
                                ActualKeyText.Foreground = (Brush)FindResource("MutedBrush");
                                var item = _keySessionService.GetCurrentItem();
                                _wordCharSeq = item != null ? ComputeWordCharSeq(item) : new();
                                RefreshUI();
                            }), DispatcherPriority.Background);
                        }
                        else
                        {
                            RefreshKeySequenceUI();
                        }
                    }
                    else
                    {
                        _wordHasError = true;
                        StatusText.Text = $"Wrong — expected {expectedChars}";
                        RefreshKeySequenceUI();
                    }
                }
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice) return;
            if (_currentMode != PracticeModeType.WordPractice &&
                _currentMode != PracticeModeType.PositionPractice) return;

            Key k = e.Key switch
            {
                Key.System       => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key
            };
            if (k == Key.LeftShift || k == Key.RightShift)
            {
                _shiftHeld = false;
                RefreshUI();
            }
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            // Sentence practice: block space/enter from landing in SentenceInputBox.
            if (_currentMode == PracticeModeType.SentencePractice)
            {
                if (e.Text == " " || e.Text == "\r" || e.Text == "\n")
                    e.Handled = true;
            }
        }

        // ── Sentence practice logic ───────────────────────────────────────────────

        private void SentenceInputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_sentenceFailed)
                SentenceInputDisplay.Text = _typedWordsDisplay + SentenceInputBox.Text;
        }

        private void AdvanceSentenceWord()
        {
            if (_sentenceFailed)
            {
                _sentenceFailed = false;
                _sentenceSessionService.ResetWordIndex();
                _typedWordsDisplay = "";
                _submittedWords.Clear();
                SentenceInputBox.Clear();
                SentenceInputDisplay.Text = "";
                InitWordResults();
                RefreshSentenceUI();
                return;
            }

            string input = SentenceInputBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            int wordIndex = _sentenceSessionService.CurrentWordIndex;
            string targetWord = _sentenceSessionService.GetCurrentTargetWord() ?? "";
            bool correct = _sentenceEvaluator.IsWordMatch(input, targetWord);

            _wordResults[wordIndex] = correct;
            _submittedWords.Add(input);
            _typedWordsDisplay += input + " ";
            SentenceInputBox.Clear();
            _sentenceSessionService.AdvanceWord();

            if (_sentenceSessionService.IsCurrentSentenceCompleted())
            {
                bool allCorrect = _wordResults.TrueForAll(r => r == true);
                if (allCorrect)
                {
                    _sentenceSessionService.AdvanceSentence();
                    _typedWordsDisplay = "";
                    _submittedWords.Clear();
                    SentenceInputBox.Clear();
                    SentenceInputDisplay.Text = "";
                    InitWordResults();
                    RefreshSentenceUI();
                }
                else
                {
                    _sentenceFailed = true;
                    RefreshSentenceUI();
                    StatusText.Text = "Some words incorrect — press Space to try again";
                }
            }
            else
            {
                RefreshSentenceUI();
            }
        }

        private void RevertLastWord()
        {
            string lastWord = _submittedWords[^1];
            _submittedWords.RemoveAt(_submittedWords.Count - 1);
            _sentenceSessionService.RevertWord();
            _wordResults[_sentenceSessionService.CurrentWordIndex] = null;
            _typedWordsDisplay = _submittedWords.Count > 0
                ? string.Join(" ", _submittedWords) + " " : "";
            SentenceInputBox.Text = lastWord;
            SentenceInputBox.CaretIndex = lastWord.Length;
            RefreshSentenceUI();
        }

        // Maps WPF Key enum to the canonical key ID string used in layout data.
        // Key.Oem1–Oem7 are aliases for OemSemicolon–OemQuotes; .ToString() returns the
        // alias name ("Oem1") in .NET 10, which doesn't match our stored key IDs.
        private static string KeyToKeyId(Key key) => key switch
        {
            Key.Oem1 => "OemSemicolon",
            Key.Oem2 => "OemQuestion",
            Key.Oem3 => "OemTilde",
            Key.Oem4 => "OemOpenBrackets",
            Key.Oem5 => "OemPipe",
            Key.Oem6 => "OemCloseBrackets",
            Key.Oem7 => "OemQuotes",
            _        => key.ToString()
        };

        // ── Navigation ────────────────────────────────────────────────────────────

        private void HomeBtn_Click(object sender, RoutedEventArgs e) => ShowHome();

        private void PositionPracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.PositionPractice);

        private void WordPracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.WordPractice);

        private void SentencePracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.SentencePractice);

        private void ShowFileSelector(PracticeModeType mode)
        {
            _fileSelectMode = mode;
            HomePanel.Visibility = Visibility.Collapsed;
            FileSelectPanel.Visibility = Visibility.Visible;

            FileSelectTitle.Text = mode switch
            {
                PracticeModeType.WordPractice     => "Word Practice",
                PracticeModeType.SentencePractice => "Sentence Practice",
                _                                 => "Position Practice",
            };

            FileSelectCustomSection.Visibility = mode == PracticeModeType.PositionPractice
                ? Visibility.Collapsed : Visibility.Visible;

            FileListPanel.Children.Clear();
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            if (mode == PracticeModeType.PositionPractice)
            {
                foreach (var group in KeyPositionGroups.All)
                {
                    var g = group;
                    AddFileButton(g.Name, () =>
                    {
                        _positionGroupKeys = g.KeyIds;
                        _positionGroupName = g.Name;
                        SwitchMode(PracticeModeType.PositionPractice);
                    });
                }
            }
            else if (mode == PracticeModeType.WordPractice)
            {
                AddFileButton("Practice 1", () =>
                {
                    LoadWordPractice();
                    SwitchMode(PracticeModeType.WordPractice);
                });
                AddFileButton("Practice 2", () =>
                {
                    var set = _loader.LoadWordPracticeFromTextFile(
                        Path.Combine(basePath, "Data", "word-practice-sample.txt"),
                        _jaraiLayoutService);
                    _keySessionService.LoadItems(set.Items);
                    SwitchMode(PracticeModeType.WordPractice);
                });
            }
            else
            {
                AddFileButton("Practice 1", () =>
                {
                    var set = _loader.LoadSentencePracticeSet(
                        Path.Combine(basePath, "Data", "sentence-practice.json"));
                    _sentenceSessionService.LoadItem(set.Items);
                    SwitchMode(PracticeModeType.SentencePractice);
                });
                AddFileButton("Practice 2", () =>
                {
                    var set = _loader.LoadSentencePracticeFromTextFile(
                        Path.Combine(basePath, "Data", "sentence-practice-sample.txt"));
                    _sentenceSessionService.LoadItem(set.Items);
                    SwitchMode(PracticeModeType.SentencePractice);
                });
            }
        }

        private void AddFileButton(string name, Action onClick)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = name,
                Height = 48,
                FontSize = 15,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            btn.Click += (_, _) => onClick();
            FileListPanel.Children.Add(btn);
        }

        private void FileSelectBackBtn_Click(object sender, RoutedEventArgs e)
        {
            FileSelectPanel.Visibility = Visibility.Collapsed;
            HomePanel.Visibility = Visibility.Visible;
        }

        private void FileSelectCustomBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Load Practice File",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            if (_fileSelectMode == PracticeModeType.WordPractice)
            {
                var set = _loader.LoadWordPracticeFromTextFile(dlg.FileName, _jaraiLayoutService);
                if (set.Items.Count == 0)
                {
                    MessageBox.Show("No valid words found in the file.\n\nMake sure the file contains Jarai words.",
                        "Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _keySessionService.LoadItems(set.Items);
                SwitchMode(PracticeModeType.WordPractice);
            }
            else
            {
                var set = _loader.LoadSentencePracticeFromTextFile(dlg.FileName);
                if (set.Items.Count == 0)
                {
                    MessageBox.Show("No sentences found in the file.\n\nMake sure the file contains one sentence per line.",
                        "Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _sentenceSessionService.LoadItem(set.Items);
                SwitchMode(PracticeModeType.SentencePractice);
            }
        }
    }
}
