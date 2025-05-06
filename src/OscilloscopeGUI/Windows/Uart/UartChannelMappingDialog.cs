using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OscilloscopeGUI {
    public partial class UartChannelMappingDialog : Window {
        // Vysledne prirazeni kanal => role
        public Dictionary<string, string> ChannelRenames { get; private set; } = new();

        // Pomocna trida pro kazdy radek
        private class ChannelRole {
            public string ChannelName { get; set; } = "";
            public string SelectedRole { get; set; } = "Žádná";
            public ComboBox ComboBox { get; set; } = null!; 
        }

        private readonly List<ChannelRole> roles = new();

        public UartChannelMappingDialog(List<string> availableChannels) {
            InitializeComponent();
            foreach (var ch in availableChannels) {
                var role = new ChannelRole { ChannelName = ch };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,4,0,4) };
                row.Children.Add(new TextBlock { Text = ch + ":", Width = 80, VerticalAlignment = VerticalAlignment.Center });
                var combo = new ComboBox { Width = 200, Height = 25, ItemsSource = new List<string> { "Žádná","RX","TX" }, SelectedItem = "Žádná" };
                row.Children.Add(combo);
                FormPanel.Children.Add(row);
                role.ComboBox = combo;
                roles.Add(role);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            // Nacteni vybrane role z ComboBoxu
            foreach (var r in roles)
                r.SelectedRole = r.ComboBox.SelectedItem?.ToString() ?? "Žádná";

            // Seskupeni podle vybrane role (vynechame "Zadna")
            var grouped = roles
                .Where(r => r.SelectedRole != "Žádná")
                .GroupBy(r => r.SelectedRole)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ChannelName).ToList());

            // Kontrola duplicitnich roli
            var dup = grouped.Where(g => g.Value.Count > 1).Select(g => g.Key).ToList();
            if (dup.Any()) {
                MessageBox.Show(
                    $"Každé roli může být přiřazen pouze jeden kanál.{Environment.NewLine}Duplicitní role: {string.Join(", ", dup)}",
                    "Chyba mapování", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pokud je jen jeden kanal, vyzadujeme alespon jednu roli
            if (roles.Count == 1) {
                if (grouped.Count == 0) {
                    MessageBox.Show(
                        "Chybi prirazeni signalu: vyberte RX nebo TX.",
                        "Neúplné mapovani", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else {
                // Pro 2 a vice kanalu: pro UART vyzadujeme RX i TX
                var required = new[] { "RX", "TX" };
                var missing = required.Where(r => !grouped.ContainsKey(r)).ToList();
                if (missing.Any()) {
                    MessageBox.Show(
                        $"Chybi prirazeni povinnych signalu: {string.Join(", ", missing)}",
                        "Neúplné mapovani", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Sestaveni vysledneho slovniku
            ChannelRenames = roles
                .Where(r => r.SelectedRole != "Žádná")
                .ToDictionary(r => r.ChannelName, r => r.SelectedRole);

            DialogResult = true; 
            Close();
        }
    }
}