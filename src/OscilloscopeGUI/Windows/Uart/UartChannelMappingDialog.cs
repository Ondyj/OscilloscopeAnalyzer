using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OscilloscopeGUI {
    public partial class UartChannelMappingDialog : Window {
        public Dictionary<string, string> ChannelRenames { get; private set; } = new();

        private List<string> originalChannels = new();

        public UartChannelMappingDialog(List<string> availableChannels) {
            InitializeComponent();
            originalChannels = availableChannels;
            BuildForm();
        }

        private void BuildForm() {
            foreach (var channel in originalChannels) {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };

                var label = new TextBlock {
                    Text = channel + ":",
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var textbox = new TextBox {
                    Name = "Box_" + channel,
                    Width = 200,
                    Tag = channel,
                    Text = channel
                };

                panel.Children.Add(label);
                panel.Children.Add(textbox);
                FormPanel.Children.Add(panel);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            ChannelRenames.Clear();

            foreach (var child in FormPanel.Children) {
                if (child is StackPanel panel && panel.Children.Count == 2 && panel.Children[1] is TextBox box) {
                    string original = box.Tag as string ?? "";
                    string renamed = box.Text.Trim();

                    if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(renamed)) {
                        ChannelRenames[original] = renamed;
                    }
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
