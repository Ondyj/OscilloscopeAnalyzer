using System.Windows;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    
    public partial class SpiSettingsDialog : Window {
        /// <summary>
        /// Dialog pro nastaveni SPI parametru.
        /// </summary>
        public SpiSettings Settings { get; private set; } = new SpiSettings(); // Aktualni nastaveni SPI protokolu

        /// <summary>
        /// Inicializuje komponenty dialogu.
        /// </summary>
        public SpiSettingsDialog() {
            InitializeComponent();
        }

        /// <summary>
        /// Obsluzna metoda pro kliknuti na tlacitko OK.
        /// Validuje vstupni hodnoty a uklada je do Settings.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e) {
            int bitsPerWord = 8;
            if (!int.TryParse(BitsPerWordBox.Text.Trim(), out bitsPerWord) || bitsPerWord <= 0) {
                bitsPerWord = 8; 
            }

            bool cpol = CpolBox.SelectedIndex == 1; // 0 = neinvertovane, 1 = invertovane
            bool cpha = CphaBox.SelectedIndex == 1; // 0 = 1. hrana, 1 = 2. hrana

            // Nastaveni hodnot
            Settings = new SpiSettings {
                BitsPerWord = bitsPerWord,
                Cpol = cpol,
                Cpha = cpha
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
