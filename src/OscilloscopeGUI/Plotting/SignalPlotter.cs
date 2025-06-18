using ScottPlot.WPF;

namespace OscilloscopeGUI.Plotting {
    /// <summary>
    /// Trida zodpovedna za vykresleni signalu do ScottPlot grafu
    /// </summary>
    public class SignalPlotter {
        private readonly WpfPlot plot;
        public double EarliestTime { get; private set; } = 0;
        private Dictionary<string, double> channelOffsets = new();

        /// <summary>
        /// Konstruktor prijimajici ovladaci prvek WpfPlot
        /// </summary>
        /// <param name="plotControl">Instance grafu, do ktereho se bude vykreslovat</param>
        public SignalPlotter(WpfPlot plotControl) {
            plot = plotControl;
        }

        /// <summary>
        /// Asynchronne vykresli vsechny signaly v datasetu do grafu po davkach
        /// </summary>
        /// <param name="signalData">Data se signaly rozdelena podle nazvu kanalu</param>
        public async Task PlotSignalsAsync(
             Dictionary<string, List<(double Time, double Value)>> signalData, Dictionary<string, string>? channelRenames = null,
             IProgress<int>? progress = null,
             int chunkSize = 100_000,
             CancellationToken cancellationToken = default) {
            await Task.Run(async () => {
                plot.Dispatcher.Invoke(() => plot.Plot.Clear());

                double offset = 0;
                double spacing = 0.2;

                var palette = new ScottPlot.Palettes.Category10();

                EarliestTime = signalData
                    .SelectMany(kvp => kvp.Value.Select(p => p.Time))
                    .DefaultIfEmpty(0)
                    .Min();

                int totalChannels = signalData.Count;
                int currentChannel = 0;

                foreach (var channel in signalData) {
                    cancellationToken.ThrowIfCancellationRequested(); // cancel

                    string channelName = channel.Key;
                    if (channelRenames != null && channelRenames.TryGetValue(channel.Key, out var newName)) {
                        channelOffsets[newName] = offset;
                    } else {
                        channelOffsets[channel.Key] = offset;
                    }
                    double[] rawTimes = channel.Value.Select(v => v.Time).ToArray();
                    double[] rawVoltages = channel.Value.Select(v => v.Value).ToArray();

                    double minValue = rawVoltages.Min();
                    double maxValue = rawVoltages.Max();
                    double[] adjustedVoltages = rawVoltages.Select(v => v + offset - minValue).ToArray();

                    ToStepPoints(rawTimes.Zip(adjustedVoltages).ToList(), out double[] times, out double[] values);
                    var simplified = SimplifyToEdges(times, values);

                    var channelColor = palette.GetColor(currentChannel);
                    int totalChunks = (int)Math.Ceiling((double)simplified.times.Length / chunkSize);
                    int processedChunks = 0;

                    for (int i = 0; i < simplified.times.Length; i += chunkSize) {
                        cancellationToken.ThrowIfCancellationRequested(); // cancel

                        int count = Math.Min(chunkSize, simplified.times.Length - i);
                        double[] chunkTimes = simplified.times.Skip(i).Take(count).ToArray();
                        double[] chunkValues = simplified.values.Skip(i).Take(count).ToArray();

                        int chunkIndex = i;

                        plot.Dispatcher.Invoke(() =>
                        {
                            var signal = plot.Plot.Add.SignalXY(chunkTimes, chunkValues);
                            signal.MarkerSize = 0;
                            signal.Color = channelColor;
                            signal.LegendText = chunkIndex == 0 ? channelName : string.Empty;
                        });

                        processedChunks++;
                        int overallProgress = (int)(((currentChannel + processedChunks / (double)totalChunks) / totalChannels) * 100);
                        progress?.Report(overallProgress);

                        await Task.Delay(1);
                    }

                    plot.Dispatcher.Invoke(() => {
                        var lowerLine = plot.Plot.Add.HorizontalLine(offset - spacing);
                        lowerLine.Color = new ScottPlot.Color(128, 128, 128, 128);

                        var upperLine = plot.Plot.Add.HorizontalLine(offset + (maxValue - minValue) + spacing);
                        upperLine.Color = new ScottPlot.Color(128, 128, 128, 128);

                        offset -= (maxValue - minValue) + 2 * spacing;
                    });

                    currentChannel++;
                    progress?.Report((int)((currentChannel / (double)totalChannels) * 100));
                }

                plot.Dispatcher.Invoke(() => {
                    plot.Plot.ShowLegend();
                    plot.Refresh();
                });
            }, cancellationToken);
        }

        /// <summary>
        /// Prevede signal na krokovy tvar (step plot)
        /// </summary>
        private static void ToStepPoints(List<(double Time, double Value)> input, out double[] steppedX, out double[] steppedY) {
            var listX = new List<double>();
            var listY = new List<double>();

            for (int i = 0; i < input.Count - 1; i++) {
                var (t1, v1) = input[i];
                var (t2, _) = input[i + 1];

                listX.Add(t1);
                listY.Add(v1);

                listX.Add(t2);
                listY.Add(v1);
            }

            steppedX = listX.ToArray();
            steppedY = listY.ToArray();
        }

        /// <summary>
        /// Zjednodusi signal tak, aby obsahoval pouze body na hranach (zmenach hodnoty)
        /// Slouzi ke snizeni poctu bodu u digitalnich signalu bez ztraty tvaru
        /// </summary>
        private static (double[] times, double[] values) SimplifyToEdges(double[] times, double[] values) {
            if (times.Length != values.Length || times.Length == 0)
                return (Array.Empty<double>(), Array.Empty<double>());

            var simplifiedTimes = new List<double>();
            var simplifiedValues = new List<double>();

            simplifiedTimes.Add(times[0]);
            simplifiedValues.Add(values[0]);

            for (int i = 1; i < times.Length; i++) {
                if (values[i] != values[i - 1]) {
                    simplifiedTimes.Add(times[i]);
                    simplifiedValues.Add(values[i - 1]);

                    simplifiedTimes.Add(times[i]);
                    simplifiedValues.Add(values[i]);
                }
            }

            simplifiedTimes.Add(times[^1]);
            simplifiedValues.Add(values[^1]);

            return (simplifiedTimes.ToArray(), simplifiedValues.ToArray());
        }

        /// <summary>
        /// Aktualizuje popisky legendy podle noveho pojmenovani kanalu
        /// </summary>
        public void RenameChannels(Dictionary<string, string> channelRenames) {
            plot.Dispatcher.Invoke(() => {
                foreach (var plottable in plot.Plot.GetPlottables()) {
                    if (plottable is ScottPlot.Plottables.SignalXY signal &&
                        signal.LegendText is string legend && // null-check
                        channelRenames.TryGetValue(legend, out string? newName))
                    {
                        signal.LegendText = newName;
                    }
                }
                plot.Refresh();
            });
        }

        public IReadOnlyDictionary<string, double> ChannelOffsets => channelOffsets;
    } 
}
