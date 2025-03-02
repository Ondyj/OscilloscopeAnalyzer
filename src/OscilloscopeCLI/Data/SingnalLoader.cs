using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OscilloscopeCLI.Data {
    public class SignalLoader {
        public List<Tuple<double, double>> SignalData { get; private set; }
        public double StartTime { get; private set; }
        public double TimeIncrement { get; private set; }

        public SignalLoader() {
            SignalData = new List<Tuple<double, double>>();
        }

        public void LoadCsvFile(string filePath) {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Soubor {filePath} nebyl nalezen.");

            using (var reader = new StreamReader(filePath)) {
                string? headerLine = null;
                string? paramLine = null;

                // Najit hlavicku tabulky (X, CH1, Start, Increment)
                while (!reader.EndOfStream) {
                    headerLine = reader.ReadLine();
                    if (headerLine != null && headerLine.StartsWith("X,CH1,Start,Increment")) {
                        break;
                    }
                }

                // Najit radek s parametry (Sequence, Volt, Start Time, Time Increment)
                while (!reader.EndOfStream) {
                    paramLine = reader.ReadLine();
                    if (paramLine != null && paramLine.StartsWith("Sequence,Volt")) {
                        break;
                    }
                }

                if (headerLine == null || paramLine == null)
                    throw new Exception("Neplatny CSV soubor – nelze najit hlavicku nebo parametry.");

                // Rozdeleni hodnot druheho radku a odstraneni prazdnych hodnot
                var paramValues = paramLine.Split(',')
                                           .Select(x => x.Trim())
                                           .Where(x => !string.IsNullOrEmpty(x))
                                           .ToArray();

                if (paramValues.Length < 3)
                    throw new Exception($"Neplatne parametry CSV – nalezeno pouze {paramValues.Length} hodnot, ocekavany alespon 3 (Start, Increment).");

                // Nacteni Start Time
                if (!double.TryParse(paramValues[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime))
                    throw new Exception("Chyba pri cteni Start Time.");

                double timeIncrement = 1e-9; // Vychozi hodnota pro inkrement
                if (paramValues.Length >= 4) {
                    if (!double.TryParse(paramValues[3], NumberStyles.Float, CultureInfo.InvariantCulture, out timeIncrement)) {
                        timeIncrement = 1e-9;
                    }
                }

                // Prirazeni hodnot
                StartTime = startTime;
                TimeIncrement = timeIncrement;

                int index = 0;
                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',')
                                     .Select(x => x.Trim())
                                     .Where(x => !string.IsNullOrEmpty(x))
                                     .ToArray();

                    if (values.Length < 2) continue;

                    if (double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage)) {
                        double time = StartTime + index * TimeIncrement;
                        SignalData.Add(new Tuple<double, double>(time, voltage));
                        index++;
                    }
                }
            }
        }

        public void PrintSignalData(int maxSamples = 10) {
            Console.WriteLine("\nUkazka prvnich " + maxSamples + " vzorku signalu:");
            for (int i = 0; i < Math.Min(SignalData.Count, maxSamples); i++) {
                Console.WriteLine($"t = {SignalData[i].Item1:F9}s, V = {SignalData[i].Item2}V");
            }
        }
    }
}
