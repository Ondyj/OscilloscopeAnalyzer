using System.Windows;
using System.Windows.Controls;

namespace OscilloscopeGUI {
    /// <summary>
    /// Okno pro vyber protokolu pred nacitanim CSV.
    /// </summary>
    public partial class ProtocolSelectDialog : Window {
        public string SelectedProtocol => ((ProtocolCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? string.Empty;

        public ProtocolSelectDialog() {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}
