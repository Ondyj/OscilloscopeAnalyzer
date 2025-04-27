using System.Windows;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class SpiSettingsDialog : Window {
        public SpiSettings Settings { get; private set; } = new SpiSettings();

        public SpiSettingsDialog() {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            int bitsPerWord = 8;
            if (!int.TryParse(BitsPerWordBox.Text.Trim(), out bitsPerWord) || bitsPerWord <= 0) {
                bitsPerWord = 8; 
            }

            bool cpol = CpolBox.SelectedIndex == 1; // 0 = neinvertovane, 1 = invertovane
            bool cpha = CphaBox.SelectedIndex == 1; // 0 = 1. hrana, 1 = 2. hrana

            Settings = new SpiSettings {
                BitsPerWord = bitsPerWord,
                Cpol = cpol,
                Cpha = cpha
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
