namespace YmmAivoice2Plugin;

public sealed class PluginSettings
{
    // ピーク振幅に対する相対閾値 (dB)
    public double SilenceThresholdDb { get; set; } = -40.0;
    public double TailMarginSec      { get; set; } = 0.50;
}
