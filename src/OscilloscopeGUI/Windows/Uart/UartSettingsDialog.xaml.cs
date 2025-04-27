using System.Windows;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class UartSettingsDialog : Window {
        public UartSettings Settings { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public UartSettingsDialog() {
            InitializeComponent();
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        private void OkButton_Click(object sender, RoutedEventArgs e) {
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
