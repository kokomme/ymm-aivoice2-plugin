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
        log.AppendLine($"FPS: {fps} / 末尾カット: {settings.TailCutSec:F1}s");

        var timelines = doc["Timelines"]?.AsArray();
        if (timelines == null) { LastDiagLog = log + "Timelinesキーなし"; return 0; }

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

                var (filePath, _) = FindWavPath(obj);
                if (filePath == null) continue;
                wavItems++;

                var parsed = FilenameParser.TryParse(filePath);
                if (parsed == null)
                {
                    log.AppendLine($"  [{wavItems}] ファイル名パース失敗: {Path.GetFileName(filePath)}");
                    continue;
                }

                int lengthFrames;
                if (File.Exists(parsed.FullPath))
                {
                    // 常にWAVファイルの実長を基準にする
                    // (JSONのLengthは過去の実行で書き換えられている可能性があるため使わない)
                    var (fullSec, diag) = WavSilenceTrimmer.GetFullDuration(parsed.FullPath);
                    double trimSec = settings.TailCutSec > 0
                        ? Math.Max(0.1, fullSec - settings.TailCutSec)
                        : fullSec;
                    lengthFrames = Math.Max(1, (int)Math.Ceiling(trimSec * fps));
                    log.AppendLine($"  [{wavItems}] {Path.GetFileName(parsed.FullPath)}: {diag} cut={settings.TailCutSec:F1}s → {trimSec:F2}s ({lengthFrames}fr)");
                }
                else
                {
                    // ファイル不存在時のみJSONのLengthを使用
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

        // プロジェクトの総フレーム数を確認・延長（VideoInfo の全フィールドをログ出力）
        var videoInfo = doc["VideoInfo"]?.AsObject();
        if (videoInfo != null)
        {
            log.AppendLine("[VideoInfo]");
            foreach (var kv in videoInfo)
                log.AppendLine($"  {kv.Key} = {kv.Value}");

            // 総フレーム数フィールドを探して必要なら延長する
            foreach (var fn in new[] { "TotalFrame", "Frame", "Duration", "TotalFrameCount", "Length" })
            {
                if (videoInfo[fn] is JsonValue fv && fv.TryGetValue<int>(out var total))
                {
                    log.AppendLine($"  → 総フレーム数フィールド: {fn} = {total}, cursor = {cursor}");
                    if (cursor > total)
                    {
                        videoInfo[fn] = JsonValue.Create((int)cursor);
                        log.AppendLine($"  → {fn} を {total} → {cursor} に延長");
                    }
                    break;
                }
            }
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
