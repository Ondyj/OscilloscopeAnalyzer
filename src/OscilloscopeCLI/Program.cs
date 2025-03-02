using System;
using OscilloscopeCLI.Data;

class Program
{
    static void Main() {
        string filePathCSV = "data/rs115200.csv";
        string filePathTXT = "data/rs115200_csv.txt";

        try {
            SignalLoader loader = new SignalLoader();
            loader.LoadCsvFile(filePathCSV);

            OscilloscopeConfig config = new OscilloscopeConfig();
            config.LoadTxtFile(filePathTXT);

            Console.WriteLine($"Model osciloskopu: {config.Model}");
            Console.WriteLine($"Vzorkovaci frekvence: {config.SamplingRate} Sa/s");
            Console.WriteLine($"Casovy rozsah: {config.TimeScale}s");

        } catch (Exception ex) {
            Console.WriteLine($"Chyba: {ex.Message}");
        }
    }
}