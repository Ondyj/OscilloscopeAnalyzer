# OscilloscopeAnalyzer


dotnet clean
dotnet build

spusteni programu 
dotnet run --project src/OscilloscopeCLI
dotnet run --project src/OscilloscopeGUI


# Struktura projektu
OscilloscopeAnalyzer/            # Hlavní složka projektu
│── src/                         # Zdrojové kódy aplikace
│   │── OscilloscopeCLI/         # CLI část projektu (backend pro analýzu dat)
│   │   │── Program.cs           # Hlavní vstupní bod CLI aplikace
│   │   │── OscilloscopeCLI.csproj # Projektový soubor pro .NET
│   │   │── Data/
│   │   │   │── SignalLoader.cs  # Načítání a zpracování dat (CSV, WFM)
│   │   │── Protocols/           # Implementace dekódování protokolů (UART, SPI, I2C, CAN)
│   │   │── Utils/               # Pomocné nástroje (výpočty, zpracování souborů)
│   │── OscilloscopeGUI/         # GUI část projektu (vizualizace signálů)
│   │   │── App.xaml             # Hlavní soubor WPF aplikace
│   │   │── MainWindow.xaml      # XAML rozhraní hlavního okna
│   │   │── MainWindow.xaml.cs   # Logika hlavního okna
│   │   │── OscilloscopeGUI.csproj # Projektový soubor pro WPF aplikaci
│── data/                        # Testovací soubory a vzorkovaná data
│   │── rs115200.csv             # Ukázkový CSV soubor
│   │── rs115200.wfm             # Binární soubor osciloskopu
│   │── rs115200_txt.txt         # Textový výpis metadat
│── README.md                    # Dokumentace k projektu
│── OscilloscopeAnalyzer.sln      # Solution soubor pro správu projektů