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

        // Key/Word practice
        private readonly KeySequenceSessionService _keySessionService = new();
        private readonly InputSequenceEvaluator _keyEvaluator = new();
        private readonly KeyboardHintService _keyboardHintService = new();

        // Sentence practice
        private readonly SentenceSessionService _sentenceSessionService = new();
        private readonly SentenceEvaluator _sentenceEvaluator = new();

        private PracticeModeType _currentMode = PracticeModeType.WordPractice;
        private bool _isAdvancing = false;

        // Tracks the modifier key accepted in the previous step (e.g. "LeftShift")
        private string? _heldModifier = null;

        // Accumulates display characters for the current word
        private readonly List<string> _displayHistory = new();

        // Set when a non-modifier key is accepted in PreviewKeyDown, consumed in PreviewTextInput
        private bool _pendingDisplayUpdate = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadAllData();
            SwitchMode(PracticeModeType.WordPractice);
        }

        private void LoadAllData()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            var wordSet = _loader.LoadKeySequencePracticeSet(Path.Combine(basePath, "Data", "word-practice.json"));
            _keySessionService.LoadItems(wordSet.Items);

            var sentenceSet = _loader.LoadSentencePracticeSet(Path.Combine(basePath, "Data", "sentence-practice.json"));
            _sentenceSessionService.LoadItem(sentenceSet.Items);
        }

        private void LoadWordPractice()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            var set = _loader.LoadKeySequencePracticeSet(Path.Combine(basePath, "Data", "word-practice.json"));
            _keySessionService.LoadItems(set.Items);
        }

        private void SwitchMode(PracticeModeType mode)
        {
            _currentMode = mode;
            _isAdvancing = false;
            _heldModifier = null;
            _pendingDisplayUpdate = false;
            _displayHistory.Clear();

            bool isSentence = mode == PracticeModeType.SentencePRactice;
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
            if (_currentMode == PracticeModeType.SentencePRactice)
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
                KeyboardHintText.Text = "";
                ActualKeyText.Text = "";
                StatusText.Text = "All items finished.";
                return;
            }

            TargetText.Text = currentItem.DisplayText;
            ProgressText.Text = $"{_keySessionService.CurrentItemIndex + 1} / {_keySessionService.TotalItemCount}";
            ExpectedKeyText.Text = _keyboardHintService.GetHintText(currentStep, _heldModifier != null);
            KeyboardHintText.Text = _keyboardHintService.GetHintText(currentStep, _heldModifier != null);
            StatusText.Text = "";
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

        // Key/Word practice input
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePRactice) return;
            if (_isAdvancing) return;

            var currentItem = _keySessionService.GetCurrentItem();
            var currentStep = _keySessionService.GetCurrentStep();
            if (currentItem == null || currentStep == null) return;

            string actualKeyId = ConvertKeyEventToKeyId(e);
            if (string.IsNullOrEmpty(actualKeyId)) return;

            var result = _keyEvaluator.Evaluate(currentItem, _keySessionService.CurrentStepIndex, actualKeyId);

            if (result == KeyInputResult.WrongStep)
            {
                ActualKeyText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Text = "Wrong key";
                e.Handled = true;
                return;
            }

            StatusText.Text = "";
            ActualKeyText.Foreground = System.Windows.Media.Brushes.Green;

            if (ModifierKeyIds.IsModifier(actualKeyId))
            {
                _heldModifier = actualKeyId;
                // Don't add to display — the combined character from the next key will represent this
            }
            else
            {
                _heldModifier = null;
                // Wait for PreviewTextInput to get the Jarai character
                _pendingDisplayUpdate = true;
            }

            if (result == KeyInputResult.CorrectStep)
            {
                _keySessionService.AdvanceStep();
                RefreshUI();
                // Don't mark handled — let PreviewTextInput fire to get the Jarai character
            }
            else if (result == KeyInputResult.ItemCompleted)
            {
                _heldModifier = null;
                _pendingDisplayUpdate = false; // skip PreviewTextInput — word is done
                _isAdvancing = true;
                _displayHistory.Clear();
                ActualKeyText.Text = "";
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _keySessionService.AdvanceItem();
                    _isAdvancing = false;
                    RefreshUI();
                }), DispatcherPriority.Background);
            }
        }

        // If the user releases a modifier before pressing the next key, revert to that modifier step
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePRactice) return;
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

        // Captures the Keyman-converted Jarai character and appends it to the display
        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (_currentMode == PracticeModeType.SentencePRactice) return;
            if (!_pendingDisplayUpdate) return;

            _pendingDisplayUpdate = false;
            _displayHistory.Add(e.Text);
            ActualKeyText.Text = string.Join("", _displayHistory);
        }

        // Sentence practice input
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

        // Mode button handlers
        private void WordPracticeBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadWordPractice();
            SwitchMode(PracticeModeType.WordPractice);
        }

        private void SentencePracticeBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchMode(PracticeModeType.SentencePRactice);
        }
    }
}
