#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif
using System.Windows;

namespace YmmAivoice2Plugin;

[PluginDetails(
    AuthorName = "kokomme",
    ContentId  = "ymm-aivoice2-helper-v1")]
public sealed class Aivoice2HelperPlugin : IToolPlugin
{
    readonly SettingsPanel _panel;

    public Aivoice2HelperPlugin()
    {
        _panel = new SettingsPanel();
        _panel.OnExecute += OnExecute;
    }

    // --- IPlugin ---

    public string Name => "AIVOICE2 Helper";

    public PluginDetailsAttribute Details =>
        (PluginDetailsAttribute)Attribute.GetCustomAttribute(GetType(), typeof(PluginDetailsAttribute))!;

#if !YMM4_SDK
    public PluginType PluginType => PluginType.Tool;
#endif

    public event EventHandler? CreateNewToolViewRequested;

    public void Initialize() { }

    public string[] GetUserPluginFiles() => Array.Empty<string>();

    // --- IToolPlugin ---

    public Type ViewModelType => typeof(SettingsPanel);

    public Type ViewType => typeof(SettingsPanel);

    public UIElement[] GetControls() => new UIElement[] { _panel };

    public object[] GetToolBarGroups() => Array.Empty<object>();

    // --- 実行ロジック ---

    void OnExecute(object? sender, EventArgs e)
    {
        var settings = new PluginSettings
        {
            SilenceThresholdDb = _panel.SilenceThresholdDb,
            TailMarginSec      = _panel.TailMarginSec
        };
        int count = ProcessCommand.Execute(settings);
        _panel.ShowResult($"{count} 件のアイテムを整理しました。");
    }
}
