namespace YmmAivoice2Plugin;

public static class AutoWatcher
{
    static FileSystemWatcher? _watcher;
    static System.Timers.Timer? _debounce;
    static volatile bool _suppressed;

    public static bool IsRunning => _watcher?.EnableRaisingEvents == true;

    public static void Start(string ymmpPath, Action onChanged)
    {
        Stop();
        var dir  = Path.GetDirectoryName(ymmpPath);
        var file = Path.GetFileName(ymmpPath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file)) return;

        _watcher = new FileSystemWatcher(dir)
        {
            Filter            = file,
            NotifyFilter      = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) =>
        {
            if (_suppressed) return;
            _debounce?.Dispose();
            _debounce = new System.Timers.Timer(1000) { AutoReset = false };
            _debounce.Elapsed += (_, _) => onChanged();
            _debounce.Start();
        };
    }

    public static void Stop()
    {
        _debounce?.Dispose();
        _debounce = null;
        _watcher?.Dispose();
        _watcher = null;
    }

    // プラグイン自身がYMMPを書き換える間はイベントを抑制する
    public static void Suppress(Action action)
    {
        _suppressed = true;
        try   { action(); }
        finally { _suppressed = false; }
    }
}
