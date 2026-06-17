namespace YmmAivoice2Plugin;

public class Aivoice2HelperViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    bool _autoWatch;
    public bool AutoWatch
    {
        get => _autoWatch;
        set
        {
            _autoWatch = value;
            OnPropertyChanged();
            if (value) StartWatcher();
            else       AutoWatcher.Stop();
        }
    }

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
            int count    = ProcessCommand.Execute();
            var diag     = ProcessCommand.LastDiagLog;
            var reloaded = ProcessCommand.AutoReloaded;
            var diagBlock = string.IsNullOrEmpty(diag) ? "" : $"\n[診断]\n{diag}";
            ResultText = count switch
            {
                < 0 => $"プロジェクトファイルが見つかりませんでした。\nYMM4でプロジェクトを開いて保存してください。{diagBlock}",
                0   => $"対象アイテムが見つかりませんでした。\n・プロジェクトを保存しましたか？\n・タイムラインにWAVは配置されていますか？{diagBlock}",
                _   => (reloaded
                    ? $"{count} 件を整理しました。（自動再読み込み済み）"
                    : $"{count} 件を整理しました。\nYMM4でプロジェクトを再読み込みしてください。") + diagBlock
            };

            // 手動実行後もウォッチャーを最新プロジェクトパスで更新
            if (_autoWatch) StartWatcher();
        }
        catch (Exception ex) { ResultText = $"エラー: {ex.Message}"; }
    }

    void AutoExecute()
    {
        // FileSystemWatcherのスレッドから呼ばれる → UIスレッドで実行
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                int count = ProcessCommand.Execute();
                if (count > 0)
                    ResultText = $"自動整理: {count} 件";
            }
            catch { }
        });
    }

    void StartWatcher()
    {
        var path = ProjectDetector.GetCurrentProjectPath();
        if (path == null)
        {
            ResultText = "プロジェクトが見つかりません。先に手動で「今すぐ整理」を実行してください。";
            AutoWatcher.Stop();
            return;
        }
        AutoWatcher.Start(path, AutoExecute);
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
