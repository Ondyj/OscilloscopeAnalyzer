using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class SpiChannelMappingDialog : Window {
        public SpiChannelMapping Mapping { get; private set; } = new();
        public Dictionary<string, int> RoleToIndex { get; private set; } = new();

        // Trida reprezentujici jeden radek (kanal + zvolena role + combo)
        public class ChannelRole {
            public string ChannelName { get; set; } = "";
            public string SelectedRole { get; set; } = "Žádná";
            public ComboBox? ComboBox { get; set; }
        }

        private List<ChannelRole> roles = new();

        public SpiChannelMappingDialog(List<string> availableChannels) {
            InitializeComponent();

            foreach (string ch in availableChannels) {
                var role = new ChannelRole { ChannelName = ch };

                // Radek s nazvem kanalu a comboboxem
                var row = new StackPanel {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 4)
                };

                row.Children.Add(new TextBlock {
                    Text = ch + ":",
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var combo = new ComboBox {
                    Width = 200,
                    Height = 25,
                    ItemsSource = new List<string> { "Žádná", "CS", "SCLK", "MOSI", "MISO" },
                    SelectedItem = "Žádná"
                };

                row.Children.Add(combo);
                FormPanel.Children.Add(row);

                role.ComboBox = combo;
                roles.Add(role);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            // Ziska z comboboxu vybrane hodnoty
            foreach (var role in roles)
                role.SelectedRole = role.ComboBox?.SelectedItem?.ToString() ?? "Žádná";

            RoleToIndex = roles
                .Select((r, i) => new { r.SelectedRole, Index = i })
                .Where(x => x.SelectedRole != "Žádná")
                .ToDictionary(x => x.SelectedRole, x => x.Index);

            var grouped = roles
                .Where(r => r.SelectedRole != "Žádná")
                .GroupBy(r => r.SelectedRole)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ChannelName).ToList());

            // Validace: max 1x kazda role
            var duplicateRoles = grouped
                .Where(g => g.Value.Count > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateRoles.Any()) {
                MessageBox.Show($"Každé roli může být přiřazen pouze jeden kanál.\nDuplicitní role: {string.Join(", ", duplicateRoles)}",
                                "Chyba mapování",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Povinne signaly
            List<string> requiredRoles = new() { "SCLK", "MOSI" };
            var missing = requiredRoles.Where(r => !grouped.ContainsKey(r)).ToList();
            if (missing.Any()) {
                MessageBox.Show($"Chybí přiřazení povinných signálů: {string.Join(", ", missing)}",
                    "Neúplné mapování",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Zadny kanal nesmi zustat neprirazen
            var unassignedChannels = roles
                .Where(r => r.SelectedRole == "Žádná")
                .Select(r => r.ChannelName)
                .Where(ch => !string.IsNullOrWhiteSpace(ch))
                .ToList();

            if (unassignedChannels.Any()) {
                MessageBox.Show($"Následující kanály nemají přiřazenou roli: {string.Join(", ", unassignedChannels)}",
                                "Nepřiřazené kanály",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Vytvoreni vysledne mapy
            Mapping = new SpiChannelMapping {
                ChipSelect = grouped.TryGetValue("CS", out var cs) ? cs.FirstOrDefault() ?? "" : "",
                Clock = grouped.TryGetValue("SCLK", out var clk) ? clk.FirstOrDefault() ?? "" : "",
                Mosi = grouped.TryGetValue("MOSI", out var mosi) ? mosi.FirstOrDefault() ?? "" : "",
                Miso = grouped.TryGetValue("MISO", out var miso) ? miso.FirstOrDefault() ?? "" : ""
            };

            DialogResult = true;
            Close();
        }
    }
}