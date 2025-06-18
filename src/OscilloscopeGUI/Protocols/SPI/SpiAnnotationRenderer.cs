using OscilloscopeCLI.Protocols;
using ScottPlot;
using ScottPlot.Plottables;

namespace OscilloscopeGUI;

public class SpiAnnotationRenderer : IAnnotationRenderer {

    /// <summary>
    /// Vykresli anotace bajtu (texty a svisle cary) do grafu podle SPI analyzy.
    /// </summary>
    /// <param name="analyzer">Analyzator SPI protokolu</param>
    /// <param name="plot">Graf ScottPlot, do ktereho se vykresluje</param>
    /// <param name="format">Format zobrazeni bajtu (HEX, DEC, ASCII)</param>
    /// <param name="byteLabels">Seznam textovych popisku, ktery se doplni</param>
    /// <param name="byteStartLines">Seznam car zacatku bajtu, ktery se doplni</param>
    /// <param name="channelOffsets">Svisle ofsety jednotlivych kanalu</param>
    public void Render(
        IProtocolAnalyzer analyzer,
        Plot plot,
        ByteDisplayFormat format,
        List<Text> byteLabels,
        List<IPlottable> byteStartLines,
        IReadOnlyDictionary<string, double> channelOffsets)
    {
        if (analyzer is not SpiProtocolAnalyzer spi)
            return;

        var limits = plot.Axes.GetLimits();
        double xMin = limits.Left, xMax = limits.Right;

        var bytes = spi.DecodedBytes;
        for (int i = 0; i < bytes.Count; i++) {
            var b = bytes[i];
            double centerX = (b.StartTime + b.EndTime) / 2;
            if (centerX < xMin || centerX > xMax)
                continue;

            bool hasError = !string.IsNullOrEmpty(b.Error);
            var color = hasError ? Colors.Red : Colors.Black;

            double yMosi = channelOffsets.TryGetValue("MOSI", out var mo) ? mo : 0;
            var textMosi = plot.Add.Text(FormatByte(b.ValueMOSI, format), centerX, yMosi + 1.3);
            textMosi.LabelStyle.FontSize = 16;
            textMosi.LabelStyle.Bold = true;
            textMosi.LabelFontColor = color;
            byteLabels.Add(textMosi);

            if (b.HasMISO) {
                double yMiso = channelOffsets.TryGetValue("MISO", out var mi) ? mi : 0;
                var textMiso = plot.Add.Text(FormatByte(b.ValueMISO, format), centerX, yMiso + 1.3);
                textMiso.LabelStyle.FontSize = 16;
                textMiso.LabelStyle.Bold = true;
                textMiso.LabelFontColor = color;
                byteLabels.Add(textMiso);
            }

            var lineStart = plot.Add.VerticalLine(b.StartTime);
            lineStart.Color = color;
            lineStart.LineWidth = 1;
            lineStart.LinePattern = LinePattern.Dashed;
            byteStartLines.Add(lineStart);

            var lineEnd = plot.Add.VerticalLine(b.EndTime);
            lineEnd.Color = color;
            lineEnd.LineWidth = 1;
            lineEnd.LinePattern = LinePattern.Dashed;
            byteStartLines.Add(lineEnd);
        }
    }

    /// <summary>
    /// Prevede bajt na retezec podle zvoleneho formatu zobrazeni.
    /// </summary>
    /// <param name="b">Dekodovana hodnota bajtu</param>
    /// <param name="format">Zvoleny format (HEX, DEC, ASCII)</param>
    /// <returns>Textova reprezentace bajtu</returns>
    private string FormatByte(byte b, ByteDisplayFormat format) => format switch {
        ByteDisplayFormat.Hex => $"0x{b:X2}",
        ByteDisplayFormat.Dec => b.ToString(),
        ByteDisplayFormat.Ascii => char.IsControl((char)b) ? "." : ((char)b).ToString(),
        _ => $"0x{b:X2}"
    };
}
