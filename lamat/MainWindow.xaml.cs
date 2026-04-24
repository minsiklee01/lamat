using lamat.Models;
using lamat.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
                SentenceInput.Clear();
                SentenceInput.Focus();
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

            string targetWord = _sentenceSessionService.GetCurrentTargetWord() ?? "";
            TargetText.Text = sentence.DisplayText;
            ProgressText.Text = $"Sentence {_sentenceSessionService.CurrentSentenceIndex + 1} / {_sentenceSessionService.TotalSentenceCount}  |  Word: {targetWord}";
            StatusText.Text = "";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePractice) return;
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
            if (_currentMode == PracticeModeType.SentencePractice) return;
            if (!_pendingDisplayUpdate) return;

            _pendingDisplayUpdate = false;
            _displayHistory.Add(e.Text);
            ActualKeyText.Text = string.Join("", _displayHistory);
        }

        private void SentenceInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space && e.Key != Key.Return) return;

            string input = SentenceInput.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            string targetWord = _sentenceSessionService.GetCurrentTargetWord() ?? "";

            if (_sentenceEvaluator.IsWordMatch(input, targetWord))
            {
                SentenceInput.Clear();
                _sentenceSessionService.AdvanceWord();

                if (_sentenceSessionService.IsCurrentSentenceCompleted())
                    _sentenceSessionService.AdvanceSentence();

                RefreshSentenceUI();
                StatusText.Text = "";
            }
            else
            {
                StatusText.Text = $"Wrong — expected: {targetWord}";
            }

            e.Handled = true;
        }

        private string ConvertKeyEventToKeyId(KeyEventArgs e)
        {
            Key key = e.Key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key
            };
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
