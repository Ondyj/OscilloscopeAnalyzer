using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OscilloscopeGUI.Services;
using ScottPlot;
using ScottPlot.WPF;

namespace OscilloscopeGUI.Plotting {
    /// <summary>
    /// Trida zodpovedna za vykresleni signalu do ScottPlot grafu
    /// </summary>
    public class SignalPlotter {
        private readonly WpfPlot plot;
        private readonly PlotNavigationService navService;

        /// <summary>
        /// Konstruktor prijimajici ovladaci prvek WpfPlot
        /// </summary>
        /// <param name="plotControl">Instance grafu, do ktereho se bude vykreslovat</param>
        public SignalPlotter(WpfPlot plotControl) {
            plot = plotControl;
            navService = new PlotNavigationService(plotControl);
        }

        /// <summary>
        /// Asynchronne vykresli vsechny signaly v datasetu do grafu
        /// </summary>
        /// <param name="signalData">Data se signaly rozdelena podle nazvu kanalu</param>
       public async Task PlotSignalsAsync(Dictionary<string, List<Tuple<double, double>>> signalData) {
            await Task.Run(() => {
                plot.Dispatcher.Invoke(() => {
                    plot.Plot.Clear(); // Vymazani puvodniho obsahu grafu

                    double offset = 0; // Posun Y pro oddeleni kanalu
                    double spacing = 0.2; // Mezery mezi kanaly

                    foreach (var channel in signalData) {
                        string channelName = channel.Key;
                        double[] times = channel.Value.Select(v => v.Item1).ToArray(); // Casove hodnoty
                        double[] voltages = channel.Value.Select(v => v.Item2).ToArray(); // Napetove hodnoty

                        double minValue = voltages.Min();
                        double maxValue = voltages.Max();

                        // Posuneme napeti podle offsetu a min hodnoty, aby se neprekryvaly
                        double[] adjustedVoltages = voltages.Select(v => v + offset - minValue).ToArray();

                        // Vykresleni signalu do grafu
                        //var signal = plot.Plot.Add.Signal(adjustedVoltages);
                        var signal = plot.Plot.Add.Scatter(times, adjustedVoltages);
                          signal.MarkerSize = 0;
                        signal.LegendText = channelName;

                        // Vykresleni ohranicujicich car pro lepsi oddeleni
                        var lowerLine = plot.Plot.Add.HorizontalLine(offset - spacing);
                        lowerLine.Color = new ScottPlot.Color(128, 128, 128, 128); // Prusvitna seda

                        var upperLine = plot.Plot.Add.HorizontalLine(offset + (maxValue - minValue) + spacing);
                        upperLine.Color = new ScottPlot.Color(128, 128, 128, 128);

                        // Posuneme offset pro dalsi kanal
                        offset -= (maxValue - minValue) + 2 * spacing;
                    }

                    plot.Plot.ShowLegend();     // Zobrazeni legendy
                    
                });
            });
        }
    }
}
