using System.Windows;
using System.Windows.Controls;

namespace OscilloscopeGUI {
    /// <summary>
    /// Okno pro vyber protokolu pred nacitanim CSV.
    /// </summary>
    public partial class ProtocolSelectDialog : Window {
        public string SelectedProtocol => ((ProtocolCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? string.Empty;

        public ProtocolSelectDialog(int channelCount) {
            InitializeComponent();

            if (channelCount < 3) {
                // Odebrani SPI z ComboBoxu
                foreach (var item in ProtocolCombo.Items.Cast<ComboBoxItem>().ToList()) {
                    if (item.Content?.ToString() == "SPI") {
                        ProtocolCombo.Items.Remove(item);
                        break;
                    }
                }

                ProtocolCombo.SelectedIndex = 0;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}
