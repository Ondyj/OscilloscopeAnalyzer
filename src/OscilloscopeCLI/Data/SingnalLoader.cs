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
        /// Nacte signalni data z CSV souboru v osciloskopovem formatu.
        /// </summary>
        /// <param name="filePath">Cesta k souboru.</param>
        public void LoadCsvFile(string filePath) {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Soubor {filePath} nebyl nalezen.");

            SignalData.Clear(); // Vycistime predchozi data

            var lines = File.ReadAllLines(filePath);

            if (lines.Length < 3)
                throw new Exception("Soubor nema dostatek radku pro nacteni dat.");

            // Debug!!!!!
            Console.WriteLine($"Nacitani souboru: {filePath}");
            Console.WriteLine($"Hlavicka: {lines[0]}");
            Console.WriteLine($"Metadata: {lines[1]}");

            // Prvni radek - hlavicka (napr. X,CH1,CH2,Start,Increment)
            var headers = lines[0].Split(',');

            // Druhy radek - metadata (napr. Sequence,Volt,Volt,-2.38e-04,1.00e-06)
            var metadata = lines[1].Split(',');

            // Kontrola, zda metadata maji dostatek hodnot
            if (metadata.Length < 2)
                throw new Exception("Metadata v CSV souboru neobsahuji dostatek hodnot.");

            // Najdeme indexy, kde jsou napeti (CHx)
            int startIndex = 1; // X je vzdy prvni sloupec
            // Pouziti invariantni kultury pro prevod cisla
            if (!double.TryParse(metadata[metadata.Length - 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double startTime) ||
                !double.TryParse(metadata[metadata.Length - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double increment)) {
                throw new Exception("Neplatne hodnoty Start nebo Increment v metadatech souboru.");
            }

            // Debug!!!!!
            Console.WriteLine($"Startovni cas: {startTime}s");
            Console.WriteLine($"Casovy prirustek: {increment}s");

            // Vytvorime seznam kanalu
            Dictionary<int, string> channelIndexes = new(); // Index sloupce -> nazev kanalu
            for (int i = startIndex; i < headers.Length - 2; i++) {
                string channelName = headers[i].Trim();

                 // Ignorujeme "Start" a "Increment"
                if (channelName.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                    channelName.Equals("Increment", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SignalData.ContainsKey(channelName)) {
                    SignalData[channelName] = new List<Tuple<double, double>>();
                }
                channelIndexes[i] = channelName;
                Console.WriteLine($"Nalezen kanal: {channelName}");
            }

            Console.WriteLine("Zpracovavani dat...");

            // Zpracujeme signální data (od 3. radku dal)
            for (int i = 2; i < lines.Length; i++) {
                var parts = lines[i].Split(',');

                if (parts.Length < metadata.Length - 2) {
                    Console.WriteLine($"Varovani: Radek {i + 1} ma nedostatek hodnot, preskakuji...");
                    continue;
                }

                double time = startTime + (i - 2) * increment; // Vypocitame cas podle indexu
                //Console.Write($"{time}s | ");

                for (int j = startIndex; j < headers.Length - 2; j++) {
                    if (j < parts.Length && double.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) {
                        string channel = channelIndexes[j];
                        SignalData[channel].Add(new Tuple<double, double>(time, value));
                        //Console.Write($" {channel}: {value}V |");
                    }
                    else {
                        //Console.Write($"{headers[j]}: CHYBA |");
                    }
                }
                //Console.WriteLine();
            }
        }
    }
}
