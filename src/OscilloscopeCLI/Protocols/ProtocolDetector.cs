using System;
using System.Collections.Generic;
using System.Linq;
using OscilloscopeCLI.Signal;

public static class ProtocolDetector {
    /// <summary>
    /// Pokusi se odhadnout zda vzorek odpovida UART komunikaci
    /// </summary>
    /// <param name="samples">Vzorky signalu (cas + stav)</param>
    /// <param name="baudRate">Odhadovana baud rate (bitova rychlost)</param>
    /// <param name="minValidFrames">Minimalni pocet platnych ramcu pro potvrzeni</param>
    /// <returns>True pokud signal pravdepodobne odpovida UART</returns>
    public static bool DetectUARTProtocol(List<DigitalSignalAnalyzer.SignalSample> samples, double baudRate, int minValidFrames = 3) {
        if (samples == null || samples.Count < 10 || baudRate <= 0)
            return false;

        double bitDuration = 1.0 / baudRate;
        int validFrameCount = 0;

        for (int i = 1; i < samples.Count - 10; i++) {
            // Hledani start bitu (prechod z HIGH na LOW)
            if (samples[i - 1].State == true && samples[i].State == false) {
                double startTime = samples[i].Timestamp;

                // Overeni stop bitu na spravne pozici
                double stopTime = startTime + 9 * bitDuration;
                bool stopBit = GetBitAtTime(samples, stopTime);

                if (stopBit) {
                    validFrameCount++;
                    i = FindIndexAfterTime(samples, stopTime); // preskocime tento ramec
                }

                if (validFrameCount >= minValidFrames)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vrati hodnotu bitu v danem case (vyhleda nejblizsi vzorek)
    /// </summary>
    private static bool GetBitAtTime(List<DigitalSignalAnalyzer.SignalSample> samples, double time) {
        for (int i = 1; i < samples.Count; i++) {
            if (samples[i].Timestamp >= time)
                return samples[i - 1].State;
        }
        return samples[^1].State;
    }

    /// <summary>
    /// Najde index prvniho vzorku za zadanym casem
    /// </summary>
    private static int FindIndexAfterTime(List<DigitalSignalAnalyzer.SignalSample> samples, double time) {
        for (int i = 0; i < samples.Count; i++) {
            if (samples[i].Timestamp > time)
                return i;
        }
        return samples.Count - 1;
    }
}
