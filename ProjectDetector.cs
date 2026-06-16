namespace YmmAivoice2Plugin;

// JetCutPlugin (https://github.com/Rindai0123-Artifact/ymm4-jetcut-plugin) を参考に実装
static class ProjectDetector
{
    static object? _cachedMainModel;
    static Type?   _cachedMainModelType;

    public static string? GetCurrentProjectPath()
    {
        var path = GetPropertyValue<string>("ProjectFilePath");
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;

        path = GetProjectPathFromTitle();
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;

        return FindLatestYmmpFile();
    }

    static T? GetPropertyValue<T>(string name) where T : class
    {
        var raw = GetPropertyValueRaw(name);
        if (raw is T t) return t;
        try
        {
            var v = raw?.GetType().GetProperty("Value");
            return v?.GetValue(raw) as T;
        }
        catch { return default; }
    }

    static object? GetPropertyValueRaw(string name)
    {
        try
        {
            var (model, modelType) = GetMainModel();
            if (model == null || modelType == null) return null;
            var prop = modelType.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(model);
        }
        catch { return null; }
    }

    static (object? Model, Type? ModelType) GetMainModel()
    {
        if (_cachedMainModel != null && _cachedMainModelType != null)
            return (_cachedMainModel, _cachedMainModelType);
        try
        {
            var appType = Type.GetType("System.Windows.Application, PresentationFramework");
            var app = appType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (app == null) return (null, null);

            var mw = appType!.GetProperty("MainWindow", BindingFlags.Public | BindingFlags.Instance)?.GetValue(app);
            if (mw == null) return (null, null);

            var dc = mw.GetType().GetProperty("DataContext", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mw);
            if (dc == null) return (null, null);

            _cachedMainModel = dc;
            _cachedMainModelType = dc.GetType();
            return (_cachedMainModel, _cachedMainModelType);
        }
        catch { return (null, null); }
    }

    static string? GetProjectPathFromTitle()
    {
        try
        {
            var appType = Type.GetType("System.Windows.Application, PresentationFramework");
            var app = appType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var mw  = appType?.GetProperty("MainWindow")?.GetValue(app);
            var title = mw?.GetType().GetProperty("Title")?.GetValue(mw) as string;
            if (string.IsNullOrEmpty(title)) return null;

            var dash = title.LastIndexOf(" - ");
            if (dash <= 0) return null;
            var name = title[..dash].Trim();

            if (name.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase) && File.Exists(name))
                return name;

            var dir = Path.Combine(GetYmm4Dir() ?? "", "user", "project");
            if (!Directory.Exists(dir)) return null;
            foreach (var f in Directory.GetFiles(dir, "*.ymmp", SearchOption.AllDirectories))
                if (Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return f;
        }
        catch { }
        return null;
    }

    static string? FindLatestYmmpFile()
    {
        try
        {
            var dir = Path.Combine(GetYmm4Dir() ?? "", "user", "project");
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, "*.ymmp", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    static string? GetYmm4Dir()
    {
        try { return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName); }
        catch { return null; }
    }
}
