public class SignalReader {
    private readonly List<(double Timestamp, bool State)> samples;
    private int currentIndex = 0;

    public SignalReader(List<(double Timestamp, bool State)> samples) {
        this.samples = samples;
    }

    public bool GetStateAt(double time) {
        while (currentIndex + 1 < samples.Count && samples[currentIndex + 1].Timestamp <= time) {
            currentIndex++;
        }
        return samples[currentIndex].State;
    }

    public void Reset() {
        currentIndex = 0;
    }
}