namespace YmmAivoice2Plugin;

public sealed class PluginSettings
{
    public bool   TrimSilence       { get; set; } = false;
    public double SilenceThresholdDb { get; set; } = -40.0;
    public double TailMarginSec      { get; set; } = 0.05;
}
