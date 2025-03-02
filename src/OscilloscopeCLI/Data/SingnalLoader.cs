using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OscilloscopeCLI.Data {
    public class SignalLoader {
        // Ulozeni signalu pro vice kanalu (napr. CH1, CH2) jako seznam casovych a napetovych hodnot
        public Dictionary<string, List<Tuple<double, double>>> SignalData { get; private set; } = new();

        // Cas pocatku mereni
        public double StartTime { get; private set; }
        
        // Casovy krok mezi vzorky
        public double TimeIncrement { get; private set; }

        public void LoadCsvFile(string filePath) {
            // Overeni, zda soubor existuje
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Soubor {filePath} nebyl nalezen.");

            using (var reader = new StreamReader(filePath)) {
                string? headerLine = null;
                string? paramLine = null;

                // Hledani hlavicky souboru obsahujici nazvy kanalu (napr. "X, CH1, CH2, Start, Increment")
                while (!reader.EndOfStream) {
                    headerLine = reader.ReadLine();
                    if (headerLine != null && headerLine.Contains("CH")) {
                        break;
                    }
                }

                // Hledani radku s parametry (napr. "Sequence, Volt, Volt, -2.380000e-04, 1.000000e-06")
                while (!reader.EndOfStream) {
                    paramLine = reader.ReadLine();
                    if (paramLine != null && paramLine.StartsWith("Sequence")) {
                        break;
                    }
                }

                // Pokud nebyla nalezena hlavicka nebo parametry, soubor je neplatny
                if (headerLine == null || paramLine == null)
                    throw new Exception("Neplatny CSV soubor – nelze najit hlavicku nebo parametry.");

                // Rozdeleni hlavicky na jednotlive sloupce
                var headers = headerLine.Split(',').Select(x => x.Trim()).ToArray();
                var paramValues = paramLine.Split(',').Select(x => x.Trim()).ToArray();

                // Nalezeni indexu sloupcu Start a Increment
                int startIndex = Array.IndexOf(headers, "Start");
                int incrementIndex = Array.IndexOf(headers, "Increment");

                if (startIndex == -1 || incrementIndex == -1)
                    throw new Exception("Neplatny format CSV – chybi sloupce Start a Increment.");

                // Parsovani pocatecniho casu mereni
                if (!double.TryParse(paramValues[startIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime))
                    throw new Exception("Chyba pri cteni Start Time.");

                // Parsovani casoveho kroku mezi vzorky (pokud neni platne cislo, pouzije se vychozi hodnota)
                if (!double.TryParse(paramValues[incrementIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double timeIncrement))
                    timeIncrement = 1e-9;

                // Ulozeni hodnot StartTime a TimeIncrement
                StartTime = startTime;
                TimeIncrement = timeIncrement;

                // Identifikace vsech dostupnych signalu (napr. CH1, CH2)
                foreach (var header in headers) {
                    if (header.StartsWith("CH")) {
                        SignalData[header] = new List<Tuple<double, double>>();
                    }
                }

                int sampleIndex = 0;
                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Rozdeleni radku na jednotlive hodnoty
                    var values = line.Split(',').Select(x => x.Trim()).ToArray();
                    if (values.Length < 2) continue;

                    // Vypocet aktualniho casu vzorku
                    double time = StartTime + sampleIndex * TimeIncrement;

                    // Zpracovani kazdeho kanalu (CH1, CH2 atd.)
                    foreach (var header in SignalData.Keys) {
                        int index = Array.IndexOf(headers, header);
                        if (index != -1 && double.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage)) {
                            SignalData[header].Add(new Tuple<double, double>(time, voltage));
                        }
                    }

                    sampleIndex++;
                }
            }
        }
    }
}
