using System;
using System.IO;
using System.Globalization;

namespace OscilloscopeCLI.Signal {
    public class OscilloscopeConfig {
        public string Model { get; private set; } = "";
        public double SamplingRate { get; private set; } = 0;
        public double TimeScale { get; private set; } = 0;

        public void LoadTxtFile(string filePath) {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"TXT soubor {filePath} nebyl nalezen.");

            using (var reader = new StreamReader(filePath)) {
                while (!reader.EndOfStream) {
                    string? line = reader.ReadLine();

                    // Pokud je radek null, pokracuj na dalsi iteraci cyklu
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Zpracovani modelu osciloskopu
                    if (line != null && line.StartsWith("Model:")) {
                        var parts = line.Split(':', 2);
                        Model = parts.Length > 1 ? parts[1].Trim() : "Nezname";
                    }

                    // Zpracovani vzorkovaci frekvence
                    if (line != null && line.StartsWith("Sampling Rate:")) {
                        SamplingRate = ParseValue(line, "Sa/s");
                    }

                    // Zpracovani casoveho rozsahu
                    if (line != null && line.StartsWith("Time Scale:")) {
                        TimeScale = ParseValue(line, "s");
                    }
                }
            }
        }

        private double ParseValue(string line, string unitToRemove) {
            string valuePart = line.Split(':')[1].Trim().Replace(unitToRemove, "").Trim();
            return double.TryParse(valuePart, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0;
        }
    }
}
