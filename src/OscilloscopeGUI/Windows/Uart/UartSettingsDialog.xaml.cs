using System.Windows;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class UartSettingsDialog : Window {
        public UartSettings Settings { get; private set; } = new UartSettings();

        public UartSettingsDialog() {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            if (!int.TryParse(BaudRateBox.Text.Trim(), out int baudRate) || baudRate <= 0) {
                MessageBox.Show("Zadejte platnou hodnotu pro Baud Rate.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (DataBitsBox.SelectedItem is not ComboBoxItem dataBitsItem ||
                !int.TryParse(dataBitsItem.Content?.ToString(), out int dataBits)) {
                MessageBox.Show("Vyberte počet datových bitů.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string parityText = (ParityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Žádná";
            Parity parity = parityText switch {
                "Žádná" => Parity.None,
                "Sudá" => Parity.Even,
                "Lichá" => Parity.Odd,
                _ => Parity.None
            };

            int stopBits;
            string stopBitsText = (StopBitsBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1";

            if (stopBitsText == "1")
                stopBits = 1;
            else if (stopBitsText == "1.5")
                stopBits = 2; // 1.5 zatim jako 2 TODO
            else
                stopBits = 2;

            bool idleLevelHigh = (IdleLevelBox.SelectedIndex == 0); // 0 = High, 1 = Low

            Settings = new UartSettings {
                BaudRate = baudRate,
                DataBits = dataBits,
                Parity = parity,
                StopBits = stopBits,
                IdleLevelHigh = idleLevelHigh
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
