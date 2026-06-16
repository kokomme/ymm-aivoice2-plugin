namespace YmmAivoice2Plugin;

public class Aivoice2HelperViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    double _silenceThresholdDb = -40.0;
    public double SilenceThresholdDb
    {
        get => _silenceThresholdDb;
        set { _silenceThresholdDb = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThresholdText)); }
    }
    public string ThresholdText => $"{(int)SilenceThresholdDb} dB";

    double _tailMarginMs = 50.0;
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
                SilenceThresholdDb = SilenceThresholdDb,
                TailMarginSec = TailMarginMs / 1000.0
            };
            int count = ProcessCommand.Execute(settings);
            ResultText = count switch
            {
                < 0 => "プロジェクトファイルが見つかりませんでした。\nYMM4でプロジェクトを開いてください。",
                0   => "対象アイテムが見つかりませんでした。\nファイル名が 000_キャラ名_セリフ.wav 形式か確認してください。",
                _   => $"{count} 件を整理しました。\nYMM4でプロジェクトを再読み込みしてください。"
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
