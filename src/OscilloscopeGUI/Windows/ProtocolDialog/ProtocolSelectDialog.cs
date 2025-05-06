using System.Windows;
using System.Windows.Controls;

namespace OscilloscopeGUI {
    /// <summary>
    /// Okno pro vyber protokolu pred nactenim CSV.
    /// </summary>
    public partial class ProtocolSelectDialog : Window {
        public string SelectedProtocol => ((ProtocolCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? string.Empty;

        public ProtocolSelectDialog(int channelCount) {
            InitializeComponent();

            // Nejprve vycistime vsechny polozky
            ProtocolCombo.Items.Clear();

            // 1 kanal  -> jen UART
            // 2 kanaly -> UART i SPI
            // 3+ kanaly-> jen SPI
            if (channelCount == 1) {
                ProtocolCombo.Items.Add(new ComboBoxItem { Content = "UART" });
            }
            else if (channelCount == 2) {
                ProtocolCombo.Items.Add(new ComboBoxItem { Content = "SPI" });
                ProtocolCombo.Items.Add(new ComboBoxItem { Content = "UART" });
            }
            else {
                ProtocolCombo.Items.Add(new ComboBoxItem { Content = "SPI" });
            }

            ProtocolCombo.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}
