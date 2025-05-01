using System;
using System.Windows;
using System.Windows.Media;

namespace OscilloscopeGUI {
    public partial class ProgressDialog : Window {
        public Action? OnOkClicked { get; set; }
        public Action? OnCanceled { get; set; }

        private string phasePrefix = "Načítání";

        public ProgressDialog() {
            InitializeComponent();
        }

        public void ReportMessage(string message) {
            StatusText.Text = message;
        }

        public void ReportProgress(int value) {
            ProgressBar.Value = value;
            StatusText.Text = $"{phasePrefix}: {value}%";
        }

        public void Finish(string message = "Hotovo.", bool autoClose = false) {
            StatusText.Text = message;
            CancelButton.Visibility = Visibility.Collapsed;

            if (autoClose) {
                Close();
            } else {
                OkButton.Visibility = Visibility.Visible;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            OnOkClicked?.Invoke();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            OnCanceled?.Invoke();
            Close();
        }

        public void SetPhase(string phase) {
            phasePrefix = phase;
        }

        public void SetTitle(string title) {
            Dispatcher.Invoke(() => this.Title = title);
        }

        public void SetErrorState() {
            ProgressBar.Foreground = Brushes.Red; 
        }
    }
}
