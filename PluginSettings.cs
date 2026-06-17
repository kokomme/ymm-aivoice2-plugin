namespace YmmAivoice2Plugin;

public sealed class PluginSettings
{
    // WAV末尾から一律に引く秒数 (0 = カットなし)
    public double TailCutSec { get; set; } = 0.3;
}
