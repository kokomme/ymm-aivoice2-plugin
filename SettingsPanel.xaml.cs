using System.Windows.Controls;

namespace YmmAivoice2Plugin;

public partial class SettingsPanel : UserControl
{
    public event EventHandler? OnExecute;

    public double SilenceThresholdDb => ThresholdSlider.Value;
    public double TailMarginSec => MarginSlider.Value / 1000.0;

    public SettingsPanel()
    {
        InitializeComponent();
    }

    public void ShowResult(string message) => ResultLabel.Text = message;

    void ThresholdSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        => ThresholdLabel.Text = $"{(int)e.NewValue} dB";

    void MarginSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        => MarginLabel.Text = $"{(int)e.NewValue} ms";

    void ExecuteButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ResultLabel.Text = "処理中...";
        ExecuteButton.IsEnabled = false;
        try
        {
            OnExecute?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            ExecuteButton.IsEnabled = true;
        }
    }
}
