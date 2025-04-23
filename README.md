# OscilloscopeAnalyzer

## Ovládání grafu (vizualizace signálu)

- **Kolečko myši** – přiblížení / oddálení (alternativa klavesy **W** / **S**)
- **Klik kolečkem** - resetuje zobrazení na výchozí stav
- **Držení levého tlačítka myši + tažení** – horizontální posun grafu (alternativa klavesy **A** / **D**)

## Spustitelná verze
Spustitelný soubor je dostupný ve formě ZIP archivu:

```
Spustitelny_soubor.zip
```
Po rozbalení stačí spustit soubor:

```
OscilloscopeGUI.exe
```

## Sestavení a spuštění
```bash
dotnet clean
dotnet build

spusteni programu 
dotnet run --project src/OscilloscopeCLI
dotnet run --project src/OscilloscopeGUI

zabaleni projektu
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

# Struktura projektu
```bash
OscilloscopeAnalyzer/
│
├── src/
│   ├── OscilloscopeCLI/              # Backend – zpracování a analýza signálů
│   │   ├── Signal/                   # Načítání a základní analýza signálů
│   │   ├── Protocols/                # Implementace detekce a dekódování protokolů 
│   │   ├── ProtocolSettings/         # Nastavení pro jednotlivé protokoly
│   │   └── OscilloscopeCLI.csproj
│   │
│   └── OscilloscopeGUI/              # WPF GUI – vizualizace a ovládací prvky
│       ├── Plotting/                 # Vykreslování signálů
│       ├── Services/                 # Služby (např. zpracování CSV, analýza UART)
│       ├── App.xaml + MainWindow.xaml(.cs)
│       └── OscilloscopeGUI.csproj
│
└── README.md                         # Dokumentace k projektu 
```