namespace YmmAivoice2Plugin;

public static class ProcessCommand
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string LastDiagLog { get; private set; } = "";

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

        var entries = new List<(JsonObject Item, ParsedVoiceFile Parsed, double TrimmedSec)>();

        var timelines = doc["Timelines"]?.AsArray();
        if (timelines == null) { LastDiagLog = log + "Timelinesキーなし"; return 0; }

        int totalItems = 0, wavItems = 0;
        foreach (var timeline in timelines)
        {
            var items = timeline?["Items"]?.AsArray();
            if (items == null) continue;

            foreach (var item in items)
            {
                if (item is not JsonObject obj) continue;
                totalItems++;

                // 最初の3アイテムはタイプとキーを診断ログに出力
                if (totalItems <= 3)
                {
                    var typeName = obj["$type"]?.GetValue<string>() ?? "(no $type)";
                    var shortType = typeName.Split(',')[0].Split('.').LastOrDefault() ?? typeName;
                    var topKeys = string.Join(", ", obj.Select(kv => kv.Key).Take(12));
                    log.AppendLine($"  [{totalItems}] {shortType} keys={topKeys}");
                }

                // item["FilePath"] だけでなく、ネスト内も含めて再帰検索
                var (filePath, keyPath) = FindWavPath(obj);
                if (filePath == null) continue;
                wavItems++;

                if (totalItems <= 3)
                    log.AppendLine($"      WAV at: {keyPath}");

                var parsed = FilenameParser.TryParse(filePath);
                if (parsed == null) continue;

                if (!File.Exists(filePath)) continue;

                var trimmedSec = WavSilenceTrimmer.GetTrimmedDurationSec(
                    filePath,
                    silenceThresholdDb: settings.SilenceThresholdDb,
                    tailMarginSec: settings.TailMarginSec);

                entries.Add((obj, parsed, trimmedSec));
            }
        }

        log.AppendLine($"全アイテム: {totalItems}件 / WAV: {wavItems}件 / 対象: {entries.Count}件");
        LastDiagLog = log.ToString().TrimEnd();

        if (entries.Count == 0) return 0;

        File.Copy(ymmpPath, ymmpPath + ".bak", overwrite: true);

        var sorted = entries.OrderBy(e => e.Parsed.Index).ToList();
        long cursor = 0;
        foreach (var (item, _, trimmedSec) in sorted)
        {
            long lengthFrames = (long)Math.Ceiling(trimmedSec * fps);
            if (lengthFrames <= 0) lengthFrames = 1;

            item["Frame"]  = JsonValue.Create((int)cursor);
            item["Length"] = JsonValue.Create((int)lengthFrames);
            cursor += lengthFrames;
        }

        File.WriteAllText(ymmpPath, doc.ToJsonString(WriteOptions));
        return sorted.Count;
    }

    // JsonObject/Array を再帰走査し、.wav で終わる最初の文字列値を返す
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
                    var (found, foundKey) = FindWavPath(kv.Value, cur);
                    if (found != null) return (found, foundKey);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                var el = arr[i];
                if (el == null) continue;
                var (found, foundKey) = FindWavPath(el, $"{prefix}[{i}]");
                if (found != null) return (found, foundKey);
            }
        }
        return (null, "");
    }
}
