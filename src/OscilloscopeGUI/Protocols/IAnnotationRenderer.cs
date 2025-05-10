using ScottPlot;
using OscilloscopeCLI.Protocols;
using ScottPlot.Plottables;

namespace OscilloscopeGUI {
    public interface IAnnotationRenderer {
         void Render(IProtocolAnalyzer analyzer, Plot plot, ByteDisplayFormat format, List<Text> byteLabels, List<IPlottable> byteStartLines);
    }
}