using System;
using System.Windows;

namespace OscilloscopeGUI {
    public partial class ProgressDialog : Window {
        public Action? OnOkClicked { get; set; }
        public Action? OnCanceled { get; set; }

        public ProgressDialog() {
            InitializeComponent();
        }

        public void ReportProgress(int value) {
            ProgressBar.Value = value;
            StatusText.Text = $"Načítání: {value}%";
        }

        public void Finish(string message = "Načítání dokončeno.", bool autoClose = false) {
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
    }
}
