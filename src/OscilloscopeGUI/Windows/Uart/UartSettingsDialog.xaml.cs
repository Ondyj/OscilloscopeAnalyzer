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
            Settings = new UartSettings {
                BaudRate = double.TryParse(BaudRateBox.Text, out var br) ? br : 9600,
                DataBits = int.TryParse(DataBitsBox.Text, out var db) ? db : 8,
                ParityEnabled = ParityEnabledBox.IsChecked == true,
                ParityEven = (ParityTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Sudá",
                StopBits = int.TryParse((StopBitsBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var sb) ? sb : 1,
                IdleHigh = IdleHighBox.IsChecked == true
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
