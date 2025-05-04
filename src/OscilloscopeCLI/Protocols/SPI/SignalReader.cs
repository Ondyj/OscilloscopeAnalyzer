public class SignalReader {
    private readonly List<(double Timestamp, bool State)> samples; // Seznam vzorku se zaznamenanym casem a logickym stavem
    private int currentIndex = 0; // Aktualni index ve vzorcich

    public SignalReader(List<(double Timestamp, bool State)> samples) {
        this.samples = samples;
    }

    // Vrati logicky stav v danem case
    public bool GetStateAt(double time) {
        while (currentIndex + 1 < samples.Count && samples[currentIndex + 1].Timestamp <= time) {
            currentIndex++;
        }
        return samples[currentIndex].State;
    }

    // Resetuje index na zacatek
    public void Reset() {
        currentIndex = 0;
    }
}