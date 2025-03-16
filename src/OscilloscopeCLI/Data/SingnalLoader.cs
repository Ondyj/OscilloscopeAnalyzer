using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace OscilloscopeCLI.Data {
    public class SignalLoader {
        // Slovnik pro ukladani signalu, klice jsou nazvy kanalu
        public Dictionary<string, List<Tuple<double, double>>> SignalData { get; private set; } = new();

        /// <summary>
        /// Nacte signalni data z CSV souboru v ruznych formatech (osciloskop, logicky analyzator, obecny CSV).
        /// </summary>
        /// <param name="filePath">Cesta k souboru.</param>
        public void LoadCsvFile(string filePath) {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Soubor {filePath} nebyl nalezen.");

            SignalData.Clear();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 3) throw new Exception("Soubor nema dostatek radku pro nacteni dat.");

            // Detekce formatu na zaklade prvniho radku
            string[] firstRow = lines[0].Split(',');
            bool isOscilloscopeFormat = firstRow.Contains("X") && firstRow.Any(col => col.StartsWith("CH")) &&
                                        firstRow.Contains("Start") && firstRow.Contains("Increment");

            if (isOscilloscopeFormat) {
                LoadOscilloscopeData(lines);
            } else {
                LoadLogicAnalyzerData(lines);
            }

            // Odstraneni prazdnych kanalu
            RemoveEmptyChannels();
        }

        /// <summary>
        /// Nacte osciloskopova data, ktera obsahuji X, CH kanaly, Start a Increment.
        /// </summary>
        private void LoadOscilloscopeData(string[] lines) {
            Console.WriteLine("Detekován osciloskopový formát.");

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
                var parts = lines[i].Split(',');

                if (parts.Length < headers.Length - 2) continue;

                double time = startTime + (i - 2) * increment;
                for (int j = 1; j < headers.Length - 2; j++) {
                    if (double.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) {
                        string channel = channelIndexes[j];
                        SignalData[channel].Add(new Tuple<double, double>(time, value));
                    }
                }
            }
        }

        /// <summary>
        /// Nacte data z logickeho analyzatoru (např. DSLogic, Saleae).
        /// </summary>
        private void LoadLogicAnalyzerData(string[] lines) {
            Console.WriteLine("Detekován formát logického analyzátoru.");

            int headerIndex = Array.FindIndex(lines, line => line.StartsWith("Time("));
            if (headerIndex == -1 || headerIndex + 1 >= lines.Length)
                throw new Exception("Soubor neobsahuje platnou hlavicku s casovymi udaji.");

            var headers = lines[headerIndex].Split(',');
            for (int i = 1; i < headers.Length; i++) {
                string channelName = $"CH{headers[i].Trim()}";
                SignalData[channelName] = new List<Tuple<double, double>>();
            }

            for (int i = headerIndex + 1; i < lines.Length; i++) {
                var parts = lines[i].Split(',');
                if (parts.Length != headers.Length) continue;

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
                    continue;

                for (int j = 1; j < parts.Length; j++) {
                    if (int.TryParse(parts[j], out int value)) {
                        string channel = $"CH{headers[j].Trim()}";
                        SignalData[channel].Add(new Tuple<double, double>(time, value));
                    }
                }
            }
        }

        /// <summary>
        /// Odstrani kanaly, ktere obsahuji pouze nuly.
        /// </summary>
        private void RemoveEmptyChannels() {
            var emptyChannels = SignalData
                .Where(kv => kv.Value.All(v => v.Item2 == 0)) // Zkontroluje, zda jsou vsechny hodnoty 0
                .Select(kv => kv.Key)
                .ToList();

            foreach (var channel in emptyChannels) {
                Console.WriteLine($"Odstraněn prázdný kanál: {channel}");
                SignalData.Remove(channel);
            }
        }
    }
}
