namespace YmmAivoice2Plugin;

public sealed class PluginSettings
{
    /// <summary>無音と判断するdB閾値（負値）。デフォルト -40dB。</summary>
    public double SilenceThresholdDb { get; set; } = -40.0;

    /// <summary>無音カット後に残すバッファ秒数。デフォルト 50ms。</summary>
    public double TailMarginSec { get; set; } = 0.05;
}
