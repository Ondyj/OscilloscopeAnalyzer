using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeCLI.Signal {
    /// <summary>
    /// Trida pro nacitani signalovych dat z CSV souboru ruznych formatu (osciloskop, logicky analyzator).
    /// Ulozena data jsou pristupna jako slovnik kanalu s casovou a hodnotovou slozkou.
    /// </summary>
    public class SignalLoader {
        // Slovnik pro ukladani signalu, klice jsou nazvy kanalu
        public Dictionary<string, List<Tuple<double, double>>> SignalData { get; private set; } = new();

        /// <summary>
        /// Nacte signalova data z CSV souboru ve formatu osciloskopu nebo logickeho analyzatoru.
        /// Detekce formatu probiha automaticky podle hlavicky.
        /// </summary>
        /// <param name="filePath">Cesta k CSV souboru.</param>
        /// <param name="progress">Volitelny reporter prubehu nacitani (0–100 %).</param>
        /// <param name="cancellationToken">Volitelny token pro predcasne preruseni nacitani.</param>
        public void LoadCsvFile(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default) {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Soubor {filePath} nebyl nalezen.");

            SignalData.Clear();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 3) throw new Exception("Soubor nemá dostatek řádku pro načtení dat.");

            // Detekce formatu na zaklade prvniho radku
            string[] firstRow = lines[0].Split(',');
            bool isOscilloscopeFormat = firstRow.Contains("X") && firstRow.Any(col => col.StartsWith("CH")) &&
                                        firstRow.Contains("Start") && firstRow.Contains("Increment");

            if (isOscilloscopeFormat) {
                LoadOscilloscopeData(lines, progress, cancellationToken);
            } else {
                LoadLogicAnalyzerData(lines, progress, cancellationToken);
            }

            // Odstraneni prazdnych kanalu
            RemoveEmptyChannels();
        }

        /// <summary>
        /// Nacte data z osciloskopoveho CSV souboru, ktery obsahuje sloupce X, CH kanaly, Start a Increment.
        /// Cas je odvozen na zaklade indexu a hodnoty inkrementu.
        /// </summary>
        /// <param name="lines">Pole radku ze souboru.</param>
        /// <param name="progress">Volitelny reporter prubehu nacitani (0–100 %).</param>
        /// <param name="cancellationToken">Volitelny token pro preruseni.</param>
        private void LoadOscilloscopeData(string[] lines, IProgress<int>? progress = null, CancellationToken cancellationToken = default) {

            var headers = lines[0].Split(',');
            var metadata = lines[1].Split(',');

            if (!double.TryParse(metadata[metadata.Length - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime) ||
                !double.TryParse(metadata[metadata.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double increment)) {
                throw new Exception("Neplatné hodnoty Start nebo Increment v metadatech.");
            }

            Dictionary<int, string> channelIndexes = new();
            for (int i = 1; i < headers.Length - 2; i++) {
                string channelName = headers[i].Trim();
                if (!SignalData.ContainsKey(channelName)) {
                    SignalData[channelName] = new List<Tuple<double, double>>();
                }
                channelIndexes[i] = channelName;
            }

            for (int i = 2; i < lines.Length; i++) {

                cancellationToken.ThrowIfCancellationRequested(); // kontrola ruseni

                var parts = lines[i].Split(',');

                 if (i % 100 == 0) {
                    int percent = (int)(((i - 2) / (double)(lines.Length - 2)) * 100);
                    progress?.Report(percent);
                }

                if (parts.Length < headers.Length - 2) continue;

                double time = startTime + (i - 2) * increment;
                for (int j = 1; j < headers.Length - 2; j++) {
                    if (double.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) {
                        string channel = channelIndexes[j];
                        SignalData[channel].Add(new Tuple<double, double>(time, value));
                    }
                }
            }
            progress?.Report(100);
        }

        /// <summary>
        /// Nacte data z CSV souboru vygenerovaneho logickym analyzatorem.
        /// Hlavicka zacina retezcem "Time(...)" a urcuje nazvy kanalu.
        /// </summary>
        /// <param name="lines">Pole radku ze souboru.</param>
        /// <param name="progress">Volitelny reporter prubehu nacitani (0–100 %).</param>
        /// <param name="cancellationToken">Volitelny token pro preruseni.</param>
        private void LoadLogicAnalyzerData(string[] lines, IProgress<int>? progress = null, CancellationToken cancellationToken = default) {

            int headerIndex = Array.FindIndex(lines, line => line.StartsWith("Time("));
            if (headerIndex == -1 || headerIndex + 1 >= lines.Length)
                throw new Exception("Soubor neobsahuje platnou hlavičku s casovými údaji.");

            var headers = lines[headerIndex].Split(',');
            for (int i = 1; i < headers.Length; i++) {
                string channelName = $"CH{headers[i].Trim()}";
                SignalData[channelName] = new List<Tuple<double, double>>();
            }

            int dataLines = lines.Length - headerIndex - 1;

            for (int i = headerIndex + 1; i < lines.Length; i++) {

                cancellationToken.ThrowIfCancellationRequested(); // kontrola ruseni

                var parts = lines[i].Split(',');
                if (parts.Length != headers.Length) continue;

                if ((i - headerIndex - 1) % 100 == 0) {
                    int percent = (int)(((i - headerIndex - 1) / (double)dataLines) * 100);
                    progress?.Report(percent);
                }

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
                    continue;

                for (int j = 1; j < parts.Length; j++) {
                    if (int.TryParse(parts[j], out int value)) {
                        string channel = $"CH{headers[j].Trim()}";
                        SignalData[channel].Add(new Tuple<double, double>(time, value));
                    }
                }
            }
            progress?.Report(100);
        }

        /// <summary>
        /// Odstrani z vyslednych dat vsechny kanaly, ktere obsahuji pouze nulove hodnoty.
        /// Tim se zmensi pametova stopa a zvysi prehlednost vysledku.
        /// </summary>
        private void RemoveEmptyChannels() {
            var emptyChannels = SignalData
                .Where(kv => kv.Value.All(v => v.Item2 == 0)) // Zkontroluje, zda jsou vsechny hodnoty 0
                .Select(kv => kv.Key)
                .ToList();

            foreach (var channel in emptyChannels) {
                SignalData.Remove(channel);
            }
        }
    }
}
