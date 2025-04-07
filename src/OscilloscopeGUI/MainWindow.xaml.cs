using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.ProtocolSettings;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy

        public MainWindow() {
            InitializeComponent();
            this.KeyDown += MainWindow_KeyDown; // Pripojeni obsluhy klavesnice
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Nacist CSV"
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    // Nacteni dat ze souboru CSV
                    await Task.Run(() => loader.LoadCsvFile(openFileDialog.FileName));

                    if (loader.SignalData.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Vykresleni signalu na pozadi
                    await PlotSignalGraphAsync();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Vykresli signaly do grafu s vertikalnim posunutim
        /// </summary>
        private async Task PlotSignalGraphAsync() {
            await Task.Run(() => {
                Dispatcher.Invoke(() => {
                    plot.Plot.Clear();

                    double offset = 0; // Pocatecni posun na ose Y
                    double spacing = 0.2; // Velikost mezery mezi kanaly

                    // Pro kazdy kanal zjistime jeho rozsah
                    foreach (var channel in loader.SignalData) { // Vykreslime prvni kanal nahore, dalsi budou pod nim
                        string channelName = channel.Key;
                        double[] times = channel.Value.Select(v => v.Item1).ToArray();
                        double[] voltages = channel.Value.Select(v => v.Item2).ToArray();

                        // Zjisteni minima a maxima aktualniho kanalu
                        double minValue = voltages.Min();
                        double maxValue = voltages.Max();

                        // Posun signalu dolu, aby se kanaly neprekryvaly
                        double[] adjustedVoltages = voltages.Select(v => v + offset - minValue).ToArray();

                        // Vykresleni signalu
                        var signal = plot.Plot.Add.Signal(adjustedVoltages);
                        signal.LegendText = channelName;

                        // Pridani horni a dolni ohranicujici cary
                        double lowerBound = offset - spacing;  // Dolni hranice kanalu
                        double upperBound = offset + (maxValue - minValue) + spacing;  // Horni hranice kanalu
                        
                        var lowerLine = plot.Plot.Add.HorizontalLine(lowerBound);
                        lowerLine.Color = new ScottPlot.Color(128, 128, 128, 128); // Sediva barva

                        var upperLine = plot.Plot.Add.HorizontalLine(upperBound);
                        upperLine.Color = new ScottPlot.Color(128, 128, 128, 128);

                        // Posuneme osu Y dolu pro dalsi kanal (max hodnota + mezera)
                        offset -= (maxValue - minValue) + 2 * spacing;
                    }

                    plot.Plot.Axes.AutoScale();
                    plot.Plot.ShowLegend();
                    plot.Refresh();
                });
            });
        }

        /// <summary>
        /// Zobrazi minimalni a maximalni hodnotu signalu pro kazdy kanal
        /// </summary>
        private void DisplayMinMaxValues() {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Není načten žádný signál.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string result = "Min/Max hodnoty kanálu:\n";

            foreach (var channel in loader.SignalData) {
                var analyzer = new SignalAnalyzer(channel.Value);
                var (min, max) = analyzer.GetMinMaxValues();
                result += $"{channel.Key}: Min = {min} V, Max = {max} V\n";
            }

            MessageBox.Show(result, "Min/Max hodnoty", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Min/Max hodnoty"
        /// </summary>
        private void MinMaxButton_Click(object sender, RoutedEventArgs e) {
            DisplayMinMaxValues();
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Detekce pulzu"
        /// </summary>
        private void PulseDetectionButton_Click(object sender, RoutedEventArgs e) {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Není načten žádný signál.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double threshold = 0.5; // Mezni hodnota pro detekci pulzu
            string result = "Detekované pulzy:\n";

            foreach (var channel in loader.SignalData) {
                var analyzer = new SignalAnalyzer(channel.Value);
                var pulses = analyzer.DetectPulses(threshold);
                double avgWidth = analyzer.CalculateAveragePulseWidth(pulses);

                result += $"{channel.Key}: {pulses.Count} pulzů, průměrná šířka {avgWidth:F6} s\n";
            }

            MessageBox.Show(result, "Detekované pulzy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Obsluha klavesovych vstupu pro ovladani zoomu
        /// </summary>
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            var xAxis = plot.Plot.Axes.Bottom; // Spodni osa X
            var yAxis = plot.Plot.Axes.Left;   // Leva osa Y

            double zoomFactor = 0.1; // Mira zmeny pri zoomovani
            double rangeX = xAxis.Max - xAxis.Min;
            double shiftX = rangeX * zoomFactor;

            // Ulozime aktualni rozsah osy Y, aby se nemenil
            double minY = yAxis.Min;
            double maxY = yAxis.Max;

            // roztazeni osy pomoci W
            if (e.Key == System.Windows.Input.Key.W) { 
                xAxis.Min += shiftX;
                xAxis.Max -= shiftX;
            }
            // opacny zoom S
            else if (e.Key == System.Windows.Input.Key.S) {
                xAxis.Min -= shiftX;
                xAxis.Max += shiftX;
            }

            // Vratime osu Y zpet na puvodni rozsah
            yAxis.Min = minY;
            yAxis.Max = maxY;

            plot.Refresh(); // Aktualizace grafu
        }

    /// <summary>
    /// Handler pro kliknuti na tlacitko "Analyzovat"
    /// </summary>
    private void AnalyzeButton_Click(object sender, RoutedEventArgs e) {
        if (loader.SignalData.Count == 0) {
            MessageBox.Show("Neni nacten zadny signal.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        bool isManual = ManualRadio.IsChecked == true;

        switch (selectedProtocol) {
            case "UART":
                if (isManual) {
                    // Zatim zadavame hodnoty natvrdo – pozdeji se nahradi GUI dialogem
                    var settings = new UartSettings {
                        BaudRate = 9600,
                        DataBits = 8,
                        ParityEnabled = false,
                        ParityEven = true,
                        StopBits = 1,
                        IdleHigh = false
                    };
                    RunUartAnalysisWithSettings(settings);
                } else {
                    RunUartAutoDetection();
                }
                break;

            case "SPI":
            case "I2C":
                MessageBox.Show($"{selectedProtocol} zatim neni implementovano.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                break;

            default:
                MessageBox.Show("Neni vybran platny protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
        }
    }

    /// <summary>
    /// Spusti automatickou detekci UART signalu a analyzu
    /// </summary>
    private void RunUartAutoDetection() {
        var channel = loader.SignalData.First();
        var analyzer = new DigitalSignalAnalyzer(loader.SignalData, channel.Key);
        var samples = analyzer.GetSamples();

        var (_, _, avgInterval, baudRate) = analyzer.AnalyzeTiming();
        bool isUart = ProtocolDetector.DetectUARTProtocol(samples, baudRate);

        if (!isUart) {
            MessageBox.Show("Signal neodpovida UART komunikaci.", "Detekce UART", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var uart = new UartProtocolAnalyzer();
        uart.Analyze(samples, baudRate);

        // TODO export vysledku
    }

    /// <summary>
    /// Spusti analyzu UART se zadanym nastavenim
    /// </summary>
    private void RunUartAnalysisWithSettings(UartSettings settings) {
        var channel = loader.SignalData.First();
        var analyzer = new DigitalSignalAnalyzer(loader.SignalData, channel.Key);
        var samples = analyzer.GetSamples();

        var uart = new UartProtocolAnalyzer();
        uart.Analyze(samples, settings.BaudRate); // Zatim pouzivame jen baudrate – ostatni se doplni pozdeji

        // TODO export vysledku
}



    }
}
