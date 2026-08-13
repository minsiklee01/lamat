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
        private readonly SoundService _soundService = new();
        private readonly ParagraphSessionService _paragraphSessionService = new();

        private PracticeModeType _currentMode = PracticeModeType.WordPractice;
        private PracticeModeType _fileSelectMode = PracticeModeType.WordPractice;
        private bool _isAdvancing = false;
        private bool _shiftHeld = false;
        private bool _altGrHeld = false;

        // Word practice — character-based sequence derived from steps at load time.
        // Modifier is the KeyId that must be held ("LeftShift"/"RightAlt"), or null for none.
        private readonly List<string> _displayHistory = new();
        private List<(string Chars, string KeyId, string? Modifier)> _wordCharSeq = new();
        private int _wordCharIdx = 0;
        private bool _wordHasError = false;

        // Sentence practice
        private string _typedWordsDisplay = "";
        private string _sentenceCurrentWord = "";
        private readonly List<string> _submittedWords = new();
        private List<bool?> _wordResults = new();
        private bool _sentenceFailed = false;

        // Position practice
        private string[] _positionGroupKeys = [];
        private string _positionGroupName = "";
        private readonly List<(string Key, string? Modifier, string Chars)> _positionHistory = new();
        private readonly List<(string Key, string? Modifier, string Chars)> _positionUpcoming = new();
        private string? _positionErrorKey = null;
        private int _positionCorrect = 0;
        private readonly Random _rng = new();

        // Paragraph practice — character-based sequence for the current line, same shape as word practice
        private List<(string Chars, string KeyId, string? Modifier)> _paragraphCharSeq = new();
        private int _paragraphCharIdx = 0;
        private bool _paragraphHasError = false;
        private bool _paragraphLineComplete = false;

        // Raw Win32 VK fallback: when Keyman reports Key.ImeProcessed + ImeProcessedKey=None,
        // recover the physical key from the most recent WM_KEYDOWN wParam.
        private int _lastRawVirtualKey;

        public MainWindow()
        {
            InitializeComponent();
            LoadAllData();
            JaraiKeyboard.Initialize(_jaraiLayoutService);
            _soundService.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            _soundService.StartMusic();
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
        // Modifier steps (Shift/AltGr) are consumed here; each remaining entry is
        // (expectedChars, keyId, modifier), where modifier is the KeyId to hold or null.
        private List<(string Chars, string KeyId, string? Modifier)> ComputeWordCharSeq(KeySequencePracticeItem item)
        {
            var result = new List<(string, string, string?)>();
            string? nextModifier = null;
            foreach (var step in item.Steps)
            {
                if (ModifierKeyIds.IsModifier(step.KeyId))
                {
                    nextModifier = step.KeyId switch
                    {
                        "LeftShift" or "RightShift" => "LeftShift",
                        "LeftAlt" or "RightAlt"      => "RightAlt",
                        _                            => null
                    };
                }
                else
                {
                    string chars = nextModifier switch
                    {
                        "LeftShift" => _jaraiLayoutService.GetShiftedLabel(step.KeyId),
                        "RightAlt"  => _jaraiLayoutService.GetAltGrLabel(step.KeyId),
                        _           => _jaraiLayoutService.GetNormalLabel(step.KeyId)
                    };
                    if (!string.IsNullOrEmpty(chars))
                        result.Add((chars, step.KeyId, nextModifier));
                    nextModifier = null;
                }
            }
            return result;
        }

        private static string ModifierDisplayName(string? modifier) => modifier switch
        {
            "LeftShift" or "RightShift" => "Shift",
            "LeftAlt" or "RightAlt"     => "Alt",
            _                           => ""
        };

        private void ShowHome()
        {
            HomePanel.Visibility = Visibility.Visible;
            FileSelectPanel.Visibility = Visibility.Collapsed;
            PracticePanel.Visibility = Visibility.Collapsed;
            _isAdvancing = false;
            _shiftHeld = false;
            _altGrHeld = false;
            _displayHistory.Clear();
            _wordCharSeq = new();
            _wordCharIdx = 0;
            _wordHasError = false;
            _typedWordsDisplay = "";
            _sentenceCurrentWord = "";
            _submittedWords.Clear();
            _wordResults.Clear();
            _sentenceFailed = false;
            _positionHistory.Clear();
            _positionUpcoming.Clear();
            _positionErrorKey = null;
            _paragraphCharSeq = new();
            _paragraphCharIdx = 0;
            _paragraphHasError = false;
            _paragraphLineComplete = false;
            TargetText.Visibility = Visibility.Visible;
        }

        private void SwitchMode(PracticeModeType mode)
        {
            _currentMode = mode;
            _isAdvancing = false;
            _shiftHeld = false;
            _altGrHeld = false;
            _wordHasError = false;
            _displayHistory.Clear();
            ActualKeyText.Text = "";
            ActualKeyText.Foreground = (Brush)FindResource("MutedBrush");
            StatusText.Text = "";
            JaraiKeyboard.SetHighlights([]);

            HomePanel.Visibility = Visibility.Collapsed;
            FileSelectPanel.Visibility = Visibility.Collapsed;
            PracticePanel.Visibility = Visibility.Visible;

            bool isSentence  = mode == PracticeModeType.SentencePractice;
            bool isPosition  = mode == PracticeModeType.PositionPractice;
            bool isParagraph = mode == PracticeModeType.ParagraphPractice;
            TargetText.Visibility = (isPosition || isParagraph) ? Visibility.Collapsed : Visibility.Visible;
            KeySequencePanel.Visibility = (!isSentence && !isPosition && !isParagraph) ? Visibility.Visible : Visibility.Collapsed;
            SentencePanel.Visibility    = isSentence ? Visibility.Visible : Visibility.Collapsed;
            PositionPanel.Visibility    = isPosition ? Visibility.Visible : Visibility.Collapsed;
            ParagraphPanel.Visibility   = isParagraph ? Visibility.Visible : Visibility.Collapsed;
            JaraiKeyboard.Visibility    = !isSentence ? Visibility.Visible : Visibility.Collapsed;

            if (isSentence)
            {
                _typedWordsDisplay = "";
                _sentenceCurrentWord = "";
                _submittedWords.Clear();
                _sentenceFailed = false;
                SentenceInputDisplay.Text = "";
                InitWordResults();
                Dispatcher.BeginInvoke(new Action(() => SentenceInputBox.Focus()), DispatcherPriority.Input);
            }
            else if (isPosition)
            {
                _positionCorrect = 0;
                _positionErrorKey = null;
                _positionHistory.Clear();
                _positionUpcoming.Clear();
                for (int i = 0; i < 4; i++)
                    EnqueueNextPositionTarget();
                PositionInputBox.Clear();
                Dispatcher.BeginInvoke(new Action(() => PositionInputBox.Focus()), DispatcherPriority.Input);
            }
            else if (isParagraph)
            {
                _paragraphHasError = false;
                _paragraphLineComplete = false;
                LoadParagraphCharSeq();
                Dispatcher.BeginInvoke(new Action(() => ParagraphInputBox.Focus()), DispatcherPriority.Input);
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

        private void LoadParagraphCharSeq()
        {
            var line = _paragraphSessionService.GetCurrentLine();
            _paragraphCharSeq = line != null
                ? (_jaraiLayoutService.DeriveCharKeySeq(line.DisplayText) ?? new())
                : new();
            _paragraphCharIdx = 0;
            _paragraphHasError = false;
            _paragraphLineComplete = false;
        }

        private void RefreshUI()
        {
            if (_currentMode == PracticeModeType.SentencePractice)
                RefreshSentenceUI();
            else if (_currentMode == PracticeModeType.PositionPractice)
                RefreshPositionUI();
            else if (_currentMode == PracticeModeType.ParagraphPractice)
                RefreshParagraphUI();
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
                var (_, keyId, modifier) = _wordCharSeq[_wordCharIdx];
                string modName = ModifierDisplayName(modifier);
                ExpectedKeyText.Text = (modifier != null ? modName + " + " : "") +
                                       JaraiKeyboardControl.EnglishLabel(keyId).ToUpperInvariant();
                bool modifierHeld = modifier == "LeftShift" ? _shiftHeld : modifier == "RightAlt" && _altGrHeld;
                JaraiKeyboard.SetHighlights([keyId], null, modifier, modifierHeld);

                if (!_wordHasError)
                {
                    StatusText.Text = modifier != null
                        ? (modifierHeld ? $"{modName} held — now press the highlighted key"
                                        : $"Hold {modName}, then press the highlighted key")
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
            var current = _positionUpcoming.Count > 0 ? _positionUpcoming[0] : default;
            PositionCurrentKey.Text = current.Chars;
            PastKey1.Text = _positionHistory.Count > 0 ? _positionHistory[0].Chars : "";
            PastKey2.Text = _positionHistory.Count > 1 ? _positionHistory[1].Chars : "";
            PastKey3.Text = _positionHistory.Count > 2 ? _positionHistory[2].Chars : "";
            NextKey1.Text = _positionUpcoming.Count > 1 ? _positionUpcoming[1].Chars : "";
            NextKey2.Text = _positionUpcoming.Count > 2 ? _positionUpcoming[2].Chars : "";
            NextKey3.Text = _positionUpcoming.Count > 3 ? _positionUpcoming[3].Chars : "";

            ProgressText.Text = $"{_positionGroupName}  ·  {_positionCorrect} correct";
            string modName = ModifierDisplayName(current.Modifier);
            bool modifierHeld = current.Modifier == "LeftShift" ? _shiftHeld : current.Modifier == "RightAlt" && _altGrHeld;
            JaraiKeyboard.SetHighlights([current.Key ?? ""], _positionErrorKey, current.Modifier, modifierHeld);

            if (_positionErrorKey != null)
                StatusText.Text = "Wrong key — try again";
            else if (current.Modifier != null && !modifierHeld)
                StatusText.Text = $"Hold {modName}, then press the highlighted key";
            else if (current.Modifier != null && modifierHeld)
                StatusText.Text = $"{modName} held — now press the highlighted key";
            else
                StatusText.Text = "";
        }

        // Appends one random (key, layer) target to the upcoming queue, avoiding repeating
        // the same key+modifier combo as the last item already queued. Only includes
        // shifted/altGr chars that are Jarai (Khmer range) to avoid ASCII/punctuation targets.
        private void EnqueueNextPositionTarget()
        {
            if (_positionGroupKeys.Length == 0) return;

            var candidates = new List<(string Key, string? Modifier, string Chars)>();
            foreach (var key in _positionGroupKeys)
            {
                string norm = _jaraiLayoutService.GetNormalLabel(key);
                if (!string.IsNullOrEmpty(norm))
                    candidates.Add((key, null, norm));
                string shift = _jaraiLayoutService.GetShiftedLabel(key);
                if (IsJaraiChar(shift))
                    candidates.Add((key, "LeftShift", shift));
                string altGr = _jaraiLayoutService.GetAltGrLabel(key);
                if (IsJaraiChar(altGr))
                    candidates.Add((key, "RightAlt", altGr));
            }

            var last = _positionUpcoming.Count > 0 ? _positionUpcoming[^1] : default;
            var others = new List<(string Key, string? Modifier, string Chars)>();
            foreach (var c in candidates)
                if (!(c.Key == last.Key && c.Modifier == last.Modifier))
                    others.Add(c);
            if (others.Count == 0) others = candidates;

            _positionUpcoming.Add(others[_rng.Next(others.Count)]);
        }

        private static bool IsJaraiChar(string? text) =>
            !string.IsNullOrEmpty(text) && text[0] >= 'ក' && text[0] <= '៿';

        // Groups _paragraphCharSeq entries (one per physical key) into full grapheme clusters
        // (a base character plus any combining marks, e.g. "ឝ៉ះ" spans 3 keys but is one
        // visual character) so the UI can color/render each cluster as a single unbroken Run.
        // Unicode's generic grapheme-cluster rules (StringInfo) only attach combining marks to
        // the preceding base — they don't know that Khmer/Jarai's coeng sign always pulls in
        // the *following* consonant too, forming one visually-joined subscript stack (e.g.
        // "ស្រ" = S + coeng + R renders as one shape). Without this, splitting a Run right
        // after the coeng orphans the subscript consonant and it renders as a dotted circle.
        private static HashSet<int> GetKhmerClusterStarts(string text)
        {
            var starts = new HashSet<int> { 0 };
            for (int i = 1; i < text.Length; i++)
            {
                char prev = text[i - 1];
                char cur = text[i];
                bool curContinuesCluster = JaraiLayoutService.IsCombiningMark(cur)
                    || cur == JaraiLayoutService.Coeng || prev == JaraiLayoutService.Coeng;
                if (!curContinuesCluster)
                    starts.Add(i);
            }
            return starts;
        }

        private List<(string Text, int FirstIdx, int LastIdx)> BuildParagraphClusters()
        {
            var clusters = new List<(string, int, int)>();
            if (_paragraphCharSeq.Count == 0) return clusters;

            var fullTextBuilder = new System.Text.StringBuilder();
            foreach (var step in _paragraphCharSeq) fullTextBuilder.Append(step.Chars);
            string fullText = fullTextBuilder.ToString();

            var clusterStarts = GetKhmerClusterStarts(fullText);

            int offset = 0;
            string currentText = "";
            int firstIdx = 0;
            for (int i = 0; i < _paragraphCharSeq.Count; i++)
            {
                if (currentText.Length == 0 || clusterStarts.Contains(offset))
                {
                    if (currentText.Length > 0)
                        clusters.Add((currentText, firstIdx, i - 1));
                    currentText = _paragraphCharSeq[i].Chars;
                    firstIdx = i;
                }
                else
                {
                    currentText += _paragraphCharSeq[i].Chars;
                }
                offset += _paragraphCharSeq[i].Chars.Length;
            }
            if (currentText.Length > 0)
                clusters.Add((currentText, firstIdx, _paragraphCharSeq.Count - 1));

            return clusters;
        }

        private void RefreshParagraphUI()
        {
            var current = _paragraphSessionService.GetCurrentLine();
            if (current == null)
            {
                ParagraphPrevLine.Text = "";
                ParagraphCurrentLine.Inlines.Clear();
                ParagraphNext1.Text = "";
                ParagraphNext2.Text = "";
                ParagraphNext3.Text = "";
                ProgressText.Text = "";
                StatusText.Text = "Story complete!";
                JaraiKeyboard.SetHighlights([]);
                return;
            }

            ParagraphPrevLine.Text = _paragraphSessionService.PeekLine(-1)?.DisplayText ?? "";
            ParagraphNext1.Text = _paragraphSessionService.PeekLine(1)?.DisplayText ?? "";
            ParagraphNext2.Text = _paragraphSessionService.PeekLine(2)?.DisplayText ?? "";
            ParagraphNext3.Text = _paragraphSessionService.PeekLine(3)?.DisplayText ?? "";

            ParagraphCurrentLine.Inlines.Clear();
            foreach (var (text, firstIdx, lastIdx) in BuildParagraphClusters())
            {
                // Color the whole grapheme cluster as one unit — splitting a base character
                // from its combining marks across separate Runs breaks Khmer/Jarai shaping
                // (the mark renders as an isolated dotted-circle placeholder).
                Brush fg = lastIdx < _paragraphCharIdx
                    ? (Brush)FindResource("SuccessBrush")
                    : firstIdx <= _paragraphCharIdx && _paragraphCharIdx <= lastIdx
                        ? (Brush)FindResource(_paragraphHasError ? "ErrorBrush" : "AccentBrush")
                        : (Brush)FindResource("TextBrush");
                ParagraphCurrentLine.Inlines.Add(new Run(text) { Foreground = fg });
            }

            ProgressText.Text = $"Line {_paragraphSessionService.CurrentLineIndex + 1} / {_paragraphSessionService.TotalLineCount}";

            if (_paragraphLineComplete)
            {
                StatusText.Text = "Press any key to continue";
                JaraiKeyboard.SetHighlights([]);
            }
            else if (_paragraphCharIdx < _paragraphCharSeq.Count)
            {
                var (_, keyId, modifier) = _paragraphCharSeq[_paragraphCharIdx];
                string modName = ModifierDisplayName(modifier);
                bool modifierHeld = modifier == "LeftShift" ? _shiftHeld : modifier == "RightAlt" && _altGrHeld;
                JaraiKeyboard.SetHighlights([keyId], null, modifier, modifierHeld);
                StatusText.Text = _paragraphHasError
                    ? "Wrong key — try again"
                    : modifier != null
                        ? (modifierHeld ? $"{modName} held — now press the highlighted key"
                                        : $"Hold {modName}, then press the highlighted key")
                        : keyId == "Space"
                            ? "Press SPACE"
                            : "";
            }
        }

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
                if (k == Key.None && _lastRawVirtualKey != 0)
                    k = KeyInterop.KeyFromVirtualKey(_lastRawVirtualKey);

                if (k == Key.LeftShift || k == Key.RightShift)
                {
                    _shiftHeld = true;
                    return;
                }
                if (k == Key.RightAlt)
                {
                    _altGrHeld = true;
                    return;
                }

                _soundService.PlayClick();

                if (k == Key.Space || k == Key.Return)
                {
                    AdvanceSentenceWord();
                    e.Handled = true;
                    return;
                }

                if (!_sentenceFailed && k == Key.Back)
                {
                    e.Handled = true;
                    if (_sentenceCurrentWord.Length > 0)
                    {
                        var info = new System.Globalization.StringInfo(_sentenceCurrentWord);
                        int len = info.LengthInTextElements;
                        _sentenceCurrentWord = len <= 1 ? ""
                            : info.SubstringByTextElements(0, len - 1);
                        SentenceInputDisplay.Text = _typedWordsDisplay + _sentenceCurrentWord;
                    }
                    else if (_submittedWords.Count > 0)
                    {
                        RevertLastWord();
                    }
                    return;
                }

                // Regular key: look up Jarai character from the layout and append to current word.
                if (!_sentenceFailed && k != Key.None)
                {
                    string keyId = KeyToKeyId(k);
                    if (!ModifierKeyIds.IsModifier(keyId))
                    {
                        e.Handled = true;
                        string ch = _altGrHeld
                            ? _jaraiLayoutService.GetAltGrLabel(keyId)
                            : _shiftHeld
                                ? _jaraiLayoutService.GetShiftedLabel(keyId)
                                : _jaraiLayoutService.GetNormalLabel(keyId);
                        if (IsJaraiChar(ch))
                        {
                            _sentenceCurrentWord += ch;
                            SentenceInputDisplay.Text = _typedWordsDisplay + _sentenceCurrentWord;
                        }
                    }
                }
                return;
            }

            if (_currentMode == PracticeModeType.WordPractice ||
                _currentMode == PracticeModeType.PositionPractice ||
                _currentMode == PracticeModeType.ParagraphPractice)
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
                if (k == Key.RightAlt)
                {
                    _altGrHeld = true;
                    RefreshUI();
                    // Do NOT set e.Handled — AltGr must reach the TextBox so Keyman can compose the character.
                    return;
                }

                if (k == Key.None) return;
                string pressedId = KeyToKeyId(k);
                if (ModifierKeyIds.IsModifier(pressedId)) return;
                e.Handled = true;
                _soundService.PlayClick();

                // Position practice: check key is in the group and matches the current target.
                if (_currentMode == PracticeModeType.PositionPractice)
                {
                    if (!_positionGroupKeys.Any(pk => string.Equals(pk, pressedId, StringComparison.OrdinalIgnoreCase))) return;
                    var current = _positionUpcoming.Count > 0 ? _positionUpcoming[0] : default;
                    string? heldModifier = _shiftHeld ? "LeftShift" : _altGrHeld ? "RightAlt" : null;
                    if (current.Key != null
                        && string.Equals(pressedId, current.Key, StringComparison.OrdinalIgnoreCase)
                        && heldModifier == current.Modifier)
                    {
                        _positionCorrect++;
                        _positionHistory.Insert(0, _positionUpcoming[0]);
                        if (_positionHistory.Count > 3) _positionHistory.RemoveAt(3);
                        _positionUpcoming.RemoveAt(0);
                        EnqueueNextPositionTarget();
                        _positionErrorKey = null;
                        _shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                        _altGrHeld = Keyboard.IsKeyDown(Key.RightAlt);
                    }
                    else
                    {
                        _positionErrorKey = pressedId;
                    }
                    RefreshPositionUI();
                    return;
                }

                // Paragraph practice: if the line was just completed, this keystroke is the
                // "press any key to continue" trigger — advance without evaluating it.
                if (_currentMode == PracticeModeType.ParagraphPractice)
                {
                    if (_paragraphLineComplete)
                    {
                        _paragraphLineComplete = false;
                        _paragraphSessionService.AdvanceLine();
                        LoadParagraphCharSeq();
                        RefreshParagraphUI();
                        return;
                    }

                    if (_paragraphCharIdx >= _paragraphCharSeq.Count) return;
                    var (_, expectedParaKeyId, expectedParaModifier) = _paragraphCharSeq[_paragraphCharIdx];
                    string? paraHeldModifier = _shiftHeld ? "LeftShift" : _altGrHeld ? "RightAlt" : null;
                    if (string.Equals(pressedId, expectedParaKeyId, StringComparison.OrdinalIgnoreCase)
                        && paraHeldModifier == expectedParaModifier)
                    {
                        _paragraphHasError = false;
                        _paragraphCharIdx++;
                        if (_paragraphCharIdx >= _paragraphCharSeq.Count)
                        {
                            _paragraphLineComplete = true;
                            _shiftHeld = false;
                            _altGrHeld = false;
                        }
                    }
                    else
                    {
                        _paragraphHasError = true;
                    }
                    RefreshParagraphUI();
                    return;
                }

                // Word practice: compare physical key ID against the expected step.
                if (_currentMode == PracticeModeType.WordPractice && !_isAdvancing)
                {
                    if (_wordCharIdx >= _wordCharSeq.Count) return;
                    var (expectedChars, expectedKeyId, expectedModifier) = _wordCharSeq[_wordCharIdx];
                    string? wordHeldModifier = _shiftHeld ? "LeftShift" : _altGrHeld ? "RightAlt" : null;
                    if (string.Equals(pressedId, expectedKeyId, StringComparison.OrdinalIgnoreCase)
                        && wordHeldModifier == expectedModifier)
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
                            _altGrHeld = false;
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
            Key k = e.Key switch
            {
                Key.System       => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key
            };
            bool changed = false;
            if (k == Key.LeftShift || k == Key.RightShift) { _shiftHeld = false; changed = true; }
            else if (k == Key.RightAlt) { _altGrHeld = false; changed = true; }
            if (!changed) return;

            if (_currentMode == PracticeModeType.WordPractice ||
                _currentMode == PracticeModeType.PositionPractice ||
                _currentMode == PracticeModeType.ParagraphPractice)
                RefreshUI();
        }

        // ── Sentence practice logic ───────────────────────────────────────────────

        private void AdvanceSentenceWord()
        {
            if (_sentenceFailed)
            {
                _sentenceFailed = false;
                _sentenceSessionService.ResetWordIndex();
                _typedWordsDisplay = "";
                _sentenceCurrentWord = "";
                _submittedWords.Clear();
                SentenceInputDisplay.Text = "";
                InitWordResults();
                RefreshSentenceUI();
                return;
            }

            string input = _sentenceCurrentWord.Trim();
            if (string.IsNullOrEmpty(input)) return;

            int wordIndex = _sentenceSessionService.CurrentWordIndex;
            string targetWord = _sentenceSessionService.GetCurrentTargetWord() ?? "";
            bool correct = _sentenceEvaluator.IsWordMatch(input, targetWord);

            _wordResults[wordIndex] = correct;
            _submittedWords.Add(input);
            _typedWordsDisplay += input + " ";
            _sentenceCurrentWord = "";
            _sentenceSessionService.AdvanceWord();

            if (_sentenceSessionService.IsCurrentSentenceCompleted())
            {
                bool allCorrect = _wordResults.TrueForAll(r => r == true);
                if (allCorrect)
                {
                    _sentenceSessionService.AdvanceSentence();
                    _typedWordsDisplay = "";
                    _sentenceCurrentWord = "";
                    _submittedWords.Clear();
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
            _sentenceCurrentWord = lastWord;
            SentenceInputDisplay.Text = _typedWordsDisplay + _sentenceCurrentWord;
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

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            _soundService.ToggleMute();
            MuteButton.Content = _soundService.IsMuted ? "🔇" : "🔊";
        }

        private void PositionPracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.PositionPractice);

        private void WordPracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.WordPractice);

        private void SentencePracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.SentencePractice);

        private void ParagraphPracticeBtn_Click(object sender, RoutedEventArgs e) =>
            ShowFileSelector(PracticeModeType.ParagraphPractice);

        private void ShowFileSelector(PracticeModeType mode)
        {
            _fileSelectMode = mode;
            HomePanel.Visibility = Visibility.Collapsed;
            FileSelectPanel.Visibility = Visibility.Visible;

            FileSelectTitle.Text = mode switch
            {
                PracticeModeType.WordPractice      => "Word Practice",
                PracticeModeType.SentencePractice  => "Sentence Practice",
                PracticeModeType.ParagraphPractice => "Paragraph Practice",
                _                                  => "Position Practice",
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
                AddFileButton("Practice 2", () =>
                {
                    var set = _loader.LoadWordPracticeFromTextFile(
                        Path.Combine(basePath, "Data", "word-practice-sample.txt"),
                        _jaraiLayoutService);
                    _keySessionService.LoadItems(set.Items);
                    SwitchMode(PracticeModeType.WordPractice);
                });
            }
            else if (mode == PracticeModeType.ParagraphPractice)
            {
                AddFileButton("Story 1", () =>
                {
                    var set = _loader.LoadParagraphPracticeFromTextFile(
                        Path.Combine(basePath, "Data", "paragraph-practice-sample.txt"),
                        _jaraiLayoutService, out int stripped);
                    _paragraphSessionService.LoadLines(set.Items);
                    SwitchMode(PracticeModeType.ParagraphPractice);
                    if (stripped > 0)
                        MessageBox.Show($"{stripped} character(s) not on the Jarai keyboard were skipped.",
                            "Some Characters Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
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
            else if (_fileSelectMode == PracticeModeType.ParagraphPractice)
            {
                var set = _loader.LoadParagraphPracticeFromTextFile(dlg.FileName, _jaraiLayoutService, out int stripped);
                if (set.Items.Count == 0)
                {
                    MessageBox.Show("No usable text found in the file.\n\nMake sure the file contains Jarai paragraphs made of characters on the Jarai keyboard.",
                        "Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _paragraphSessionService.LoadLines(set.Items);
                SwitchMode(PracticeModeType.ParagraphPractice);
                if (stripped > 0)
                    MessageBox.Show($"{stripped} character(s) not on the Jarai keyboard were skipped.",
                        "Some Characters Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
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
