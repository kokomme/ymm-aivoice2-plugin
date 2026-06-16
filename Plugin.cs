using System.ComponentModel.Composition;
#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif

namespace YmmAivoice2Plugin;

/// <summary>
/// YMM4プラグインのエントリポイント。
///
/// [API要変更]
/// YMM4のIPluginインターフェースの正確なシグネチャは ymmapi.pages.dev を参照してください。
/// 特に:
///   - Details プロパティの型名（YmmPluginDetails / PluginDetails 等）
///   - CreateSettingsControl() の戻り値の型
///   - MEFエクスポート属性のインターフェース型
/// </summary>
[Export(typeof(IPlugin))]
public sealed class Aivoice2HelperPlugin : IPlugin
{
    readonly PluginSettings _settings = new();

    // [API要変更] Details プロパティの型を正しい型名に変更してください
    public object Details => new
    {
        AuthorName = "kokomme",
        ContentId = "ymm-aivoice2-helper-v1",
        DisplayName = "AIVOICE2 Helper",
        Description = "AIVOICE2の書き出しWAVを連番順に整理し、末尾無音をカットします。",
        Version = new Version(1, 0, 0)
    };

    /// <summary>
    /// [API要変更] YMM4のプラグインパネルに表示するWPF UserControlを返す。
    /// 戻り値の型が UIElement / FrameworkElement / UserControl 等、APIに合わせて変更してください。
    /// </summary>
    public object? CreateSettingsControl()
    {
        var panel = new SettingsPanel();
        panel.OnExecute += (_, project) =>
        {
            _settings.SilenceThresholdDb = panel.SilenceThresholdDb;
            _settings.TailMarginSec = panel.TailMarginSec;
            int count = ProcessCommand.Execute(project, _settings);
            panel.ShowResult($"{count} 件のアイテムを整理しました。");
        };
        return panel;
    }
}
