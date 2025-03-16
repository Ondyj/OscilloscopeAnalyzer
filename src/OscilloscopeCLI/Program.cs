using System;
using OscilloscopeCLI.Data;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        string filePathCSV = "data/pwm50z.csv"; // pwm50z.csv , rs115200.csv , rs115200_scr.csv
        //string filePathTXT = "data/rs115200_scr_csv.txt";

        try
        {
            // Načtení signálu
            SignalLoader loader = new SignalLoader();
            loader.LoadCsvFile(filePathCSV);

            // Načtení konfigurace osciloskopu
            //OscilloscopeConfig config = new OscilloscopeConfig();
            //config.LoadTxtFile(filePathTXT);

            //Console.WriteLine($"Model osciloskopu: {config.Model}");
            //Console.WriteLine($"Vzorkovaci frekvence: {config.SamplingRate} Sa/s");
            //Console.WriteLine($"Casovy rozsah: {config.TimeScale}s");

            // VYPSÁNÍ NAČTENÝCH DAT
            Console.WriteLine("\nPŘEHLED NAČTENÝCH DAT:");
            if (loader.SignalData.Count > 0)
            {
                foreach (var channel in loader.SignalData)
                {
                    Console.WriteLine($"Kanál: {channel.Key}, Počet vzorků: {channel.Value.Count}");
                    
                    // Výpis prvních 10 vzorků pro kontrolu
                    foreach (var sample in channel.Value.Take(10))
                    {
                        Console.WriteLine($" {sample.Item1}s -> {sample.Item2}V");
                    }
                    Console.WriteLine("...");
                }
            }
            else
            {
                Console.WriteLine("Žádná signální data nebyla načtena.");
            }

            // VYBEREME PRVNÍ KANÁL ZE SIGNALDATA
            if (loader.SignalData.Count > 0)
            {
                var firstChannel = loader.SignalData.First();
                string channelName = firstChannel.Key;
                List<double> signalValues = firstChannel.Value.Select(t => t.Item2).ToList();

                Console.WriteLine($"Analyzujeme kanál: {channelName}");

                // ANALÝZA SIGNÁLU
                //AnalyzeSignal analyzer = new AnalyzeSignal(signalValues);
                //var signalType = analyzer.DetectSignalType();
                //Console.WriteLine($"Detekovaný typ signálu: {signalType}");
            }
            else
            {
                Console.WriteLine("Žádná signální data nebyla načtena.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chyba: {ex.Message}");
        }
    }
}
