using System.Collections.Generic;
using System.Windows;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    /// <summary>
    /// Okno pro nastaveni mapovani SPI kanalu (CS, SCLK, MOSI, MISO).
    /// </summary>
    public partial class SpiChannelMappingDialog : Window {
        public SpiChannelMapping Mapping { get; private set; } = new();

        public SpiChannelMappingDialog(List<string> availableChannels) {
            InitializeComponent();

            // Nastavi zdroje dat pro vyberove seznamy
            CsCombo.ItemsSource = availableChannels;
            SclkCombo.ItemsSource = availableChannels;
            MosiCombo.ItemsSource = availableChannels;
            MisoCombo.ItemsSource = availableChannels;

            // Predvybere prvni 4 kanaly (pokud jsou)
            CsCombo.SelectedIndex = 0;
            SclkCombo.SelectedIndex = 1;
            MosiCombo.SelectedIndex = 2;
            MisoCombo.SelectedIndex = 3;

            if (availableChannels.Count < 4) {
                MisoRow.Visibility = Visibility.Collapsed;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            string cs = CsCombo.SelectedItem?.ToString() ?? "";
            string sclk = SclkCombo.SelectedItem?.ToString() ?? "";
            string mosi = MosiCombo.SelectedItem?.ToString() ?? "";
            string miso = MisoCombo.SelectedItem?.ToString() ?? "";

            var selected = new List<string> { cs, sclk, mosi, miso };
            var duplicates = selected
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any()) {
                MessageBox.Show(
                    $"Každý signál (CS, SCLK, MOSI, MISO) musí mít unikátní kanál.\nDuplicitní: {string.Join(", ", duplicates)}",
                    "Chyba mapování",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Mapping = new SpiChannelMapping {
                ChipSelect = cs,
                Clock = sclk,
                Mosi = mosi,
                Miso = miso
            };

            DialogResult = true;
            Close();
        }
    }
}
