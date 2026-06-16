#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif
using System.Windows;

namespace YmmAivoice2Plugin;

[PluginDetails(
    AuthorName  = "kokomme",
    ContentId   = "ymm-aivoice2-helper-v1",
    DisplayName = "AIVOICE2 Helper")]
public sealed class Aivoice2HelperPlugin : IToolPlugin
{
    readonly SettingsPanel _panel;

    public Aivoice2HelperPlugin()
    {
        _panel = new SettingsPanel();
        _panel.OnExecute += OnExecute;
    }

    // --- IPlugin ---

    public PluginDetailsAttribute Details =>
        (PluginDetailsAttribute)Attribute.GetCustomAttribute(GetType(), typeof(PluginDetailsAttribute))!;

    public PluginType PluginType => PluginType.Tool;

    public event EventHandler? CreateNewToolViewRequested;

    public void Initialize() { }

    public string[] GetUserPluginFiles() => Array.Empty<string>();

    // --- IToolPlugin ---

    public UIElement[] GetControls() => new UIElement[] { _panel };

    public object[] GetToolBarGroups() => Array.Empty<object>();

    // --- 実行ロジック ---

    void OnExecute(object? sender, IProject project)
    {
        var settings = new PluginSettings
        {
            SilenceThresholdDb = _panel.SilenceThresholdDb,
            TailMarginSec      = _panel.TailMarginSec
        };
        int count = ProcessCommand.Execute(project, settings);
        _panel.ShowResult($"{count} 件のアイテムを整理しました。");
    }
}
