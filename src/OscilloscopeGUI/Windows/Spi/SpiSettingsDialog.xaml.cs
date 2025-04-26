using System.Windows;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class SpiSettingsDialog : Window {
        public SpiSettings Settings { get; private set; } = new SpiSettings();

        public SpiSettingsDialog() {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            // Zpracovani hodnot
            int.TryParse(BitsPerWordBox.Text, out var bits);
            int.TryParse(CpolBox.Text, out var cpol);
            int.TryParse(CphaBox.Text, out var cpha);

            // Nastaveni hodnot
            Settings.BitsPerWord = bits > 0 ? bits : 8;
            Settings.Cpol = cpol == 1;
            Settings.Cpha = cpha == 1;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
