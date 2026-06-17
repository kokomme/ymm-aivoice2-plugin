namespace YmmAivoice2Plugin;

public static class ProcessCommand
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string LastDiagLog  { get; private set; } = "";
    public static bool   AutoReloaded { get; private set; } = false;

    public static int Execute(PluginSettings settings)
    {
        var log = new System.Text.StringBuilder();

        var ymmpPath = ProjectDetector.GetCurrentProjectPath();
        if (ymmpPath == null) { LastDiagLog = "プロジェクト未検出"; return -1; }
        log.AppendLine($"プロジェクト: {Path.GetFileName(ymmpPath)}");

        var json = File.ReadAllText(ymmpPath);
        var doc  = JsonNode.Parse(json);
        if (doc == null) { LastDiagLog = "JSONパース失敗"; return -1; }

        double fps = doc["VideoInfo"]?["FPS"]?.GetValue<double>() ?? 30.0;
        log.AppendLine($"FPS: {fps}");

        var timelines = doc["Timelines"]?.AsArray();
        if (timelines == null) { LastDiagLog = log + "Timelinesキーなし"; return 0; }

        // (item, 連番index, フレーム数)
        var entries = new List<(JsonObject Item, int Index, int LengthFrames)>();
        int totalItems = 0, wavItems = 0;

        foreach (var timeline in timelines)
        {
            var items = timeline?["Items"]?.AsArray();
            if (items == null) continue;

            foreach (var item in items)
            {
                if (item is not JsonObject obj) continue;
                totalItems++;

                bool diag = totalItems <= 3;

                var (filePath, _) = FindWavPath(obj);
                if (filePath == null)
                {
                    if (diag) log.AppendLine($"  [{totalItems}] WAVパス見つからず");
                    continue;
                }
                wavItems++;

                var parsed = FilenameParser.TryParse(filePath);
                if (parsed == null)
                {
                    if (diag) log.AppendLine($"  [{wavItems}] ファイル名パース失敗: {Path.GetFileName(filePath)}");
                    continue;
                }

                // 末尾無音カット: WAV 解析でフレーム数を決定
                // ファイルが存在しない場合は YMM4 の元の Length を使う
                int lengthFrames;
                if (File.Exists(parsed.FullPath))
                {
                    var (trimSec, wavDiag) = WavSilenceTrimmer.GetTrimmedDuration(
                        parsed.FullPath,
                        thresholdDb:   settings.SilenceThresholdDb,
                        tailMarginSec: settings.TailMarginSec);
                    lengthFrames = (int)Math.Ceiling(trimSec * fps);
                    if (lengthFrames <= 0) lengthFrames = 1;
                    log.AppendLine($"  [{wavItems}] {Path.GetFileName(parsed.FullPath)}: {wavDiag} → {lengthFrames}fr");
                }
                else
                {
                    lengthFrames = obj["Length"]?.GetValue<int>() ?? 1;
                    if (lengthFrames <= 0) lengthFrames = 1;
                    log.AppendLine($"  [{wavItems}] {Path.GetFileName(parsed.FullPath)}: ファイル不存在 origLen={lengthFrames}fr");
                }

                entries.Add((obj, parsed.Index, lengthFrames));
            }
        }

        log.AppendLine($"全アイテム: {totalItems}件 / WAV: {wavItems}件 / 対象: {entries.Count}件");

        if (entries.Count == 0)
        {
            LastDiagLog = log.ToString().TrimEnd();
            return 0;
        }

        File.Copy(ymmpPath, ymmpPath + ".bak", overwrite: true);

        var sorted = entries.OrderBy(e => e.Index).ToList();
        long cursor = 0;
        foreach (var (item, _, frameCount) in sorted)
        {
            item["Frame"]  = JsonValue.Create((int)cursor);
            item["Length"] = JsonValue.Create(frameCount);
            cursor += frameCount;
        }

        File.WriteAllText(ymmpPath, doc.ToJsonString(WriteOptions));

        LastDiagLog = log.ToString().TrimEnd();
        var logPath = Path.ChangeExtension(ymmpPath, ".aivoice2helper.log");
        try { File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{LastDiagLog}\n"); }
        catch { }

        AutoReloaded = false;
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.Invoke(() => { AutoReloaded = ProjectDetector.TryReloadProject(ymmpPath); });
        }
        catch { }

        return sorted.Count;
    }

    static (string? Path, string KeyPath) FindWavPath(JsonNode node, string prefix = "")
    {
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                var cur = string.IsNullOrEmpty(prefix) ? kv.Key : $"{prefix}.{kv.Key}";
                if (kv.Value is JsonValue val &&
                    val.TryGetValue<string>(out var s) &&
                    s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    return (s, cur);
                if (kv.Value != null)
                {
                    var (found, key) = FindWavPath(kv.Value, cur);
                    if (found != null) return (found, key);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                var el = arr[i];
                if (el == null) continue;
                var (found, key) = FindWavPath(el, $"{prefix}[{i}]");
                if (found != null) return (found, key);
            }
        }
        return (null, "");
    }
}
