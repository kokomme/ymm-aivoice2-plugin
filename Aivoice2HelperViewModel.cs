namespace YmmAivoice2Plugin;

public class Aivoice2HelperViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    bool _trimSilence = false;
    public bool TrimSilence
    {
        get => _trimSilence;
        set { _trimSilence = value; OnPropertyChanged(); }
    }

    double _silenceThresholdDb = -40.0;
    public double SilenceThresholdDb
    {
        get => _silenceThresholdDb;
        set { _silenceThresholdDb = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThresholdText)); }
    }
    public string ThresholdText => $"{(int)SilenceThresholdDb} dB";

    double _tailMarginMs = 300.0;
    public double TailMarginMs
    {
        get => _tailMarginMs;
        set { _tailMarginMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginText)); }
    }
    public string MarginText => $"{(int)TailMarginMs} ms";

    string _resultText = "";
    public string ResultText
    {
        get => _resultText;
        set { _resultText = value; OnPropertyChanged(); }
    }

    public ICommand ExecuteCommand => new RelayCommand(_ => Execute());

    void Execute()
    {
        ResultText = "処理中...";
        try
        {
            var settings = new PluginSettings
            {
                TrimSilence        = TrimSilence,
                SilenceThresholdDb = SilenceThresholdDb,
                TailMarginSec      = TailMarginMs / 1000.0
            };
            int count = ProcessCommand.Execute(settings);
            var diag = ProcessCommand.LastDiagLog;
            var reloaded = ProcessCommand.AutoReloaded;
            var diagBlock = string.IsNullOrEmpty(diag) ? "" : $"\n[診断]\n{diag}";
            ResultText = count switch
            {
                < 0 => $"プロジェクトファイルが見つかりませんでした。\nYMM4でプロジェクトを開いて保存してください。{diagBlock}",
                0   => $"対象アイテムが見つかりませんでした。\n・プロジェクトを保存しましたか？\n・タイムラインにWAVは配置されていますか？{diagBlock}",
                _   => (reloaded
                    ? $"{count} 件を整理しました。（YMM4に自動再読み込み済み）"
                    : $"{count} 件を整理しました。\nYMM4でプロジェクトを再読み込みしてください。") + diagBlock
            };
        }
        catch (Exception ex)
        {
            ResultText = $"エラー: {ex.Message}";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
}
