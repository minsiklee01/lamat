using lamat.Models;
using lamat.Services;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace lamat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PracticeDataLoader _loader = new();
        private readonly KeySequenceSessionService _sessionService = new();
        private readonly InputSequenceEvaluator _evaluator = new();
        private readonly KeyboardHintService _KeyboardHintService = new();

        private bool _isAdvancing = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadWordPractice();
            RefreshUI();

        }

        private void LoadWordPractice()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(basePath, "Data", "word-practice.json");

            PracticeSet<KeySequencePracticeItem> set = _loader.LoadKeySequencePracticeSet(path);
            _sessionService.LoadItems(set.Items);

        }

        private void RefreshUI()
        {
            var currentItem = _sessionService.GetCurrentItem();
            var currentStep = _sessionService.GetCurrentStep();

            if (currentItem == null)
            {
                TargetText.Text = "Practice complete!";
                ProgressText.Text = "";
                ExpectedKeyText.Text = "";
                KeyboardHintText.Text = "";
                StatusText.Text = "All items finished.";
                return;
            }

            TargetText.Text = currentItem.DisplayText;
            ProgressText.Text = $"{_sessionService.CurrentItemIndex + 1} / {_sessionService.TotalItemCount}";

            string keyId = currentStep?.KeyId ?? "";
            ExpectedKeyText.Text = keyId;
            KeyboardHintText.Text = _KeyboardHintService.GetHighlightKeyId(currentStep);

            StatusText.Text = "";
        }

        private void Window_KeyDown(Object sender, KeyEventArgs e)
        {
            if (_isAdvancing == true) return;

            var currentItem = _sessionService.GetCurrentItem();
            var currentStep = _sessionService.GetCurrentStep();

            if (currentItem == null || currentStep == null) return;

            string actualKeyId = ConvertKeyEventToKeyId(e);

            var result = _evaluator.Evaluate(currentItem, _sessionService.CurrentStepIndex, actualKeyId);

            if (result == KeyInputResult.WrongStep)
            {
                StatusText.Text = "Wrong key";
                TargetText.Foreground = System.Windows.Media.Brushes.Red;
                e.Handled = true;
                return;
            }

            TargetText.Foreground = System.Windows.Media.Brushes.Black;

            if (result == KeyInputResult.CorrectStep)
            {
                _sessionService.AdvanceStep();
                StatusText.Text = "Correct";
            }

            else if (result == KeyInputResult.ItemCompleted)
            {
                _isAdvancing = true;
                StatusText.Text = "Completed";

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _sessionService.AdvanceItem();
                    _isAdvancing = false;
                }), DispatcherPriority.Background);
            }

            RefreshUI();
            e.Handled = true;
        }

        private string ConvertKeyEventToKeyId(KeyEventArgs e)
        {
            if (e.Key == Key.System)
            {
                return e.SystemKey.ToString();
            }

            return e.Key.ToString();
        }
    }
}