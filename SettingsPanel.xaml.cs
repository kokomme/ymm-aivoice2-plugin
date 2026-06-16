using System.Windows.Controls;
#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif

namespace YmmAivoice2Plugin;

public partial class SettingsPanel : UserControl
{
    /// <summary>
    /// 「整理を実行」ボタン押下時に発火するイベント。
    /// Plugin.cs でサブスクライブし、ProcessCommand.Execute() を呼ぶ。
    /// </summary>
    public event EventHandler<IProject>? OnExecute;

    // [API要変更] IProject を取得する方法はYMM4のAPIに依存する。
    // 以下のいずれかの方法で取得してください:
    //   1. Plugin.cs から渡す (コンストラクタ引数 / プロパティ)
    //   2. YMM4が提供するシングルトン or サービスロケータ
    IProject? _project;

    public double SilenceThresholdDb => ThresholdSlider.Value;
    public double TailMarginSec => MarginSlider.Value / 1000.0;

    public SettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// YMM4からプロジェクト参照を受け取る。
    /// Plugin.cs の CreateSettingsControl() で呼ぶか、
    /// YMM4 APIの初期化コールバックでセットしてください。
    /// </summary>
    public void SetProject(IProject project) => _project = project;

    public void ShowResult(string message) => ResultLabel.Text = message;

    void ThresholdSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        => ThresholdLabel.Text = $"{(int)e.NewValue} dB";

    void MarginSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        => MarginLabel.Text = $"{(int)e.NewValue} ms";

    void ExecuteButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_project is null)
        {
            ResultLabel.Text = "プロジェクトが開かれていません。";
            return;
        }
        ResultLabel.Text = "処理中...";
        ExecuteButton.IsEnabled = false;
        try
        {
            OnExecute?.Invoke(this, _project);
        }
        finally
        {
            ExecuteButton.IsEnabled = true;
        }
    }
}
