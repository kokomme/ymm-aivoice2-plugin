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

                // --- 診断: 最初の3アイテムの詳細を出力 ---
                bool diag = totalItems <= 3;
                if (diag)
                {
                    var allKeys = string.Join(", ", obj.Select(kv => kv.Key));
                    log.AppendLine($"  [{totalItems}] keys={allKeys}");

                    // Hatsuon の実際の値を表示
                    var hatsuonRaw = obj["Hatsuon"]?.ToJsonString() ?? "(null)";
                    var preview = hatsuonRaw.Length > 100 ? hatsuonRaw[..100] + "…" : hatsuonRaw;
                    log.AppendLine($"      Hatsuon={preview}");

                    // VoiceCache の値も表示
                    var vcRaw = obj["VoiceCache"]?.ToJsonString() ?? "(null)";
                    var vcPreview = vcRaw.Length > 100 ? vcRaw[..100] + "…" : vcRaw;
                    log.AppendLine($"      VoiceCache={vcPreview}");
                }

                // WAVパスを再帰検索
                var (filePath, keyPath) = FindWavPath(obj);
                if (filePath == null)
                {
                    if (diag) log.AppendLine($"      → WAVパス見つからず");
                    continue;
                }
                wavItems++;
                if (diag) log.AppendLine($"      WAV at '{keyPath}' = {filePath}");

                var parsed = FilenameParser.TryParse(filePath);
                // 文字コード診断 (パース失敗時の原因特定用)
                var rawName = Path.GetFileName(filePath.Replace('¥', '\\')); 
                if (diag) log.AppendLine($"      name={rawName} chars=[{string.Join(",", rawName.Take(8).Select(c => $"{(int)c:X4}"))}]");
                if (parsed == null)
                {
                    // パターン不一致の場合: ファイル名を表示して原因確認
                    if (diag) log.AppendLine($"      → ファイル名パース失敗: {Path.GetFileName(filePath)}");
                    continue;
                }

                if (!File.Exists(parsed.FullPath))
                {
                    if (diag) log.AppendLine($"      → ファイル不存在: {parsed.FullPath}");
                    continue;
                }

                double trimmedSec;
                if (settings.TrimSilence)
                {
                    var (ts, fs2, wavDiag) = WavSilenceTrimmer.GetTrimmedDurationSecWithDiag(
                        parsed.FullPath,
                        silenceThresholdDb: settings.SilenceThresholdDb,
                        tailMarginSec: settings.TailMarginSec);
                    trimmedSec = ts;
                    if (diag || wavItems <= 3) log.AppendLine($"      [{wavItems}] {Path.GetFileName(parsed.FullPath)}: {wavDiag}");
                }
                else
                    trimmedSec = WavSilenceTrimmer.GetFullDurationSec(parsed.FullPath);

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
