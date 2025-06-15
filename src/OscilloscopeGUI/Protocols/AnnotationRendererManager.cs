using System;
using System.Collections.Generic;
using ScottPlot;
using OscilloscopeCLI.Protocols;
using ScottPlot.Plottables;

namespace OscilloscopeGUI {
    public class AnnotationRendererManager {
        private readonly Dictionary<Type, IAnnotationRenderer> renderers = new() {
            { typeof(UartProtocolAnalyzer), new UartAnnotationRenderer() },
            { typeof(SpiProtocolAnalyzer), new SpiAnnotationRenderer() }
        };

        public void Render(
            IProtocolAnalyzer analyzer,
            Plot plot,
            ByteDisplayFormat format,
            List<Text> byteLabels,
            List<IPlottable> byteStartLines, IReadOnlyDictionary<string, double> channelOffsets) {
            if (renderers.TryGetValue(analyzer.GetType(), out var renderer))
                 renderer.Render(analyzer, plot, format, byteLabels, byteStartLines, channelOffsets);
        }

        public IAnnotationRenderer? GetRendererFor(IProtocolAnalyzer analyzer) {
            return renderers.TryGetValue(analyzer.GetType(), out var renderer) ? renderer : null;
        }
    }
}