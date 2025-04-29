using System.Windows;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    /// <summary>
    /// Dialog pro nastaveni UART parametru.
    /// </summary>
    public partial class UartSettingsDialog : Window {
        public UartSettings Settings { get; private set; } = new UartSettings(); // Aktualni nastaveni UART protokolu

        /// <summary>
        /// Inicializuje komponenty dialogu.
        /// </summary>
        public UartSettingsDialog() {
            InitializeComponent();
        }

        /// <summary>
        /// Obsluzna metoda pro kliknuti na tlacitko OK.
        /// Validuje vstupni hodnoty a uklada je do Settings.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e) {
            // Nacteni a validace hodnoty Baud Rate
            if (!int.TryParse(BaudRateBox.Text.Trim(), out int baudRate) || baudRate <= 0) {
                MessageBox.Show("Zadejte platnou hodnotu pro rychlost přenosu (Baud Rate).", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Nacteni poctu datovych bitu
            if (DataBitsBox.SelectedItem is not ComboBoxItem dataBitsItem ||
                !int.TryParse(dataBitsItem.Content?.ToString(), out int dataBits)) {
                MessageBox.Show("Vyberte počet datových bitů.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Nacteni typu parity
            string parityText = (ParityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Žádná";
            Parity parity = parityText switch {
                "Žádná" => Parity.None,
                "Sudá" => Parity.Even,
                "Lichá" => Parity.Odd,
                _ => Parity.None
            };

            // Nacteni poctu stop bitu
            string stopBitsText = (StopBitsBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1";
            int stopBits = stopBitsText switch {
                "1" => 1,
                "2" => 2,
                _ => 1
            };

            // Idle uroven linky (true = High)
            bool idleLevelHigh = (IdleLevelBox.SelectedIndex == 0);

            // Nastaveni hodnot
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

        /// <summary>
        /// Obsluzna metoda pro kliknuti na tlacitko Zrusit.
        /// Zavira dialog bez ulozeni.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
