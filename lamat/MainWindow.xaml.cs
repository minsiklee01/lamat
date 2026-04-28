using lamat.Models;
using lamat.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace lamat
{
    public partial class MainWindow : Window
    {
        private readonly PracticeDataLoader _loader = new();

        private readonly KeySequenceSessionService _keySessionService = new();
        private readonly InputSequenceEvaluator _keyEvaluator = new();
        private readonly KeyboardHintService _keyboardHintService = new();
        private readonly JaraiLayoutService _jaraiLayoutService = new();

        private readonly SentenceSessionService _sentenceSessionService = new();
        private readonly SentenceEvaluator _sentenceEvaluator = new();

        private PracticeModeType _currentMode = PracticeModeType.WordPractice;
        private bool _isAdvancing = false;

        private string? _heldModifier = null;
        private readonly List<string> _displayHistory = new();
        private bool _pendingDisplayUpdate = false;

        private string _sentenceBuffer = "";
        private string _typedWordsDisplay = "";
        private readonly List<string> _submittedWords = new();
        private List<bool?> _wordResults = new();
        private bool _sentenceFailed = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadAllData();
            JaraiKeyboard.Initialize(_jaraiLayoutService);
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

        private void ShowHome()
        {
            HomePanel.Visibility = Visibility.Visible;
            PracticePanel.Visibility = Visibility.Collapsed;
            _isAdvancing = false;
            _heldModifier = null;
            _pendingDisplayUpdate = false;
            _displayHistory.Clear();
            _sentenceBuffer = "";
            _typedWordsDisplay = "";
            _submittedWords.Clear();
            _wordResults.Clear();
            _sentenceFailed = false;
        }

        private void SwitchMode(PracticeModeType mode)
        {
            _currentMode = mode;
            _isAdvancing = false;
            _heldModifier = null;
            _pendingDisplayUpdate = false;
            _displayHistory.Clear();
            ActualKeyText.Text = "";
            ActualKeyText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            StatusText.Text = "";
            JaraiKeyboard.SetHighlights([]);

            HomePanel.Visibility = Visibility.Collapsed;
            PracticePanel.Visibility = Visibility.Visible;

            bool isSentence = mode == PracticeModeType.SentencePractice;
            KeySequencePanel.Visibility = isSentence ? Visibility.Collapsed : Visibility.Visible;
            SentencePanel.Visibility = isSentence ? Visibility.Visible : Visibility.Collapsed;

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

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_currentMode == PracticeModeType.SentencePractice)
                RefreshSentenceUI();
            else
                RefreshKeySequenceUI();
        }

        private void RefreshKeySequenceUI()
        {
            var currentItem = _keySessionService.GetCurrentItem();
            var currentStep = _keySessionService.GetCurrentStep();

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
            ExpectedKeyText.Text = _keyboardHintService.GetHintText(currentStep, _heldModifier != null);
            StatusText.Text = "";
            JaraiKeyboard.SetHighlights(_keyboardHintService.GetKeysToHighlight(currentStep, _heldModifier));
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice)
            {
                Key k = e.Key switch
                {
                    Key.System      => e.SystemKey,
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
            if (_isAdvancing) return;

            var currentItem = _keySessionService.GetCurrentItem();
            var currentStep = _keySessionService.GetCurrentStep();
            if (currentItem == null || currentStep == null) return;

            string actualKeyId = ConvertKeyEventToKeyId(e);
            if (string.IsNullOrEmpty(actualKeyId)) return;

            var result = _keyEvaluator.Evaluate(currentItem, _keySessionService.CurrentStepIndex, actualKeyId);

            if (result == KeyInputResult.WrongStep)
            {
                ActualKeyText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                StatusText.Text = $"Expected: {currentStep.KeyId}  ·  pressed: {actualKeyId}";
                e.Handled = true;
                return;
            }

            StatusText.Text = "";
            ActualKeyText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");

            if (ModifierKeyIds.IsModifier(actualKeyId))
            {
                _heldModifier = actualKeyId;
            }
            else
            {
                _heldModifier = null;
                _pendingDisplayUpdate = true;
            }

            if (result == KeyInputResult.CorrectStep)
            {
                _keySessionService.AdvanceStep();
                RefreshUI();
            }
            else if (result == KeyInputResult.ItemCompleted)
            {
                _heldModifier = null;
                _pendingDisplayUpdate = false;
                _isAdvancing = true;
                _displayHistory.Clear();
                ActualKeyText.Text = "";
                ActualKeyText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _keySessionService.AdvanceItem();
                    _isAdvancing = false;
                    RefreshUI();
                }), DispatcherPriority.Background);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice) return;
            if (_heldModifier == null) return;

            string released = ConvertKeyUpToKeyId(e);
            if (released == _heldModifier)
            {
                _keySessionService.RevertStep();
                _heldModifier = null;
                RefreshKeySequenceUI();
                StatusText.Text = "Hold Shift while pressing the next key";
            }
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice)
            {
                if (e.Text == " " || e.Text == "\r" || e.Text == "\n")
                    e.Handled = true;
                return;
            }
            if (!_pendingDisplayUpdate) return;

            _pendingDisplayUpdate = false;

            if (!IsJaraiCharacter(e.Text))
            {
                _keySessionService.RevertStep();
                ActualKeyText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
                RefreshKeySequenceUI();
                StatusText.Text = "Switch to Jarai keyboard (Keyman) to continue";
                return;
            }

            _displayHistory.Add(e.Text);
            ActualKeyText.Text = string.Join("", _displayHistory);
        }

        private void SentenceInputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_sentenceFailed)
                SentenceInputDisplay.Text = _typedWordsDisplay + SentenceInputBox.Text;
        }

        private static string RemoveLastTextElement(string text)
        {
            var info = new System.Globalization.StringInfo(text);
            int len = info.LengthInTextElements;
            return len <= 1 ? "" : info.SubstringByTextElements(0, len - 1);
        }

        private static bool IsJaraiCharacter(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            char c = text[0];
            return c >= 'ក' && c <= '៿';
        }

        private void AdvanceSentenceWord()
        {
            // After a failed attempt, the next Space resets for retry
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
                ? string.Join(" ", _submittedWords) + " "
                : "";

            SentenceInputBox.Text = lastWord;
            SentenceInputBox.CaretIndex = lastWord.Length;

            RefreshSentenceUI();
        }

        private string ConvertKeyEventToKeyId(KeyEventArgs e)
        {
            Key key = e.Key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key
            };
            if (key == Key.ImeProcessed || key == Key.None) return "";
            return key.ToString();
        }

        private string ConvertKeyUpToKeyId(KeyEventArgs e)
        {
            Key key = e.Key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key
            };
            return key.ToString();
        }

        private void HomeBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowHome();
        }

        private void WordPracticeBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadWordPractice();
            SwitchMode(PracticeModeType.WordPractice);
        }

        private void SentencePracticeBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchMode(PracticeModeType.SentencePractice);
        }
    }
}
