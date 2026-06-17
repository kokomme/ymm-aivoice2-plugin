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

        var timelines = doc["Timelines"]?.AsArray();
        if (timelines == null) { LastDiagLog = "Timelinesキーなし"; return 0; }

        // アイテムのVoiceLength+AdditionalTime+Lengthから内部FPSを検出
        double internalFps = DetectInternalFps(timelines);
        log.AppendLine($"内部FPS: {internalFps} / 末尾カット: {settings.TailCutSec:F1}s");

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

                // VoiceLength + AdditionalTime から正しいフレーム数を算出
                // (VoiceLength/AdditionalTimeは本プラグインが変更しないため常に正確)
                int newLength = ComputeLength(obj, settings.TailCutSec, internalFps,
                                              out string lenDiag);
                log.AppendLine($"  [{wavItems}] {Path.GetFileName(parsed.FullPath)}: {lenDiag}");

                entries.Add((obj, parsed.Index, newLength));
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

    // アイテムのVoiceLength・AdditionalTime・Lengthから実際の内部FPSを推定
    static double DetectInternalFps(JsonArray timelines)
    {
        foreach (var timeline in timelines)
        {
            foreach (var item in timeline?["Items"]?.AsArray() ?? new JsonArray())
            {
                if (item is not JsonObject obj) continue;
                var origLen = obj["Length"]?.GetValue<int>() ?? 0;
                if (origLen <= 0) continue;
                if (obj["VoiceLength"]?.GetValue<string>() is not string vlStr) continue;
                if (!TimeSpan.TryParse(vlStr, out var vl) || vl.TotalSeconds <= 0) continue;
                var addTime = obj["AdditionalTime"]?.GetValue<double>() ?? 0.0;
                var total = vl.TotalSeconds + addTime;
                if (total <= 0) continue;
                var implied = origLen / total;
                if (implied >= 20 && implied <= 300)
                    return Math.Round(implied); // 60.19 → 60
            }
        }
        return 30.0; // フォールバック
    }

    // VoiceLength + AdditionalTime を使って新しいLengthを計算
    // TailCutSec 分だけ末尾から削る
    static int ComputeLength(JsonObject obj, double tailCutSec, double fps, out string diag)
    {
        if (obj["VoiceLength"]?.GetValue<string>() is string vlStr &&
            TimeSpan.TryParse(vlStr, out var vl))
        {
            double addTime  = obj["AdditionalTime"]?.GetValue<double>() ?? 0.0;
            double fullSec  = vl.TotalSeconds + addTime;
            double trimSec  = Math.Max(0.1, fullSec - tailCutSec);
            int naturalLen  = (int)Math.Ceiling(fullSec * fps);
            int newLen      = (int)Math.Ceiling(trimSec * fps);
            newLen = Math.Max(1, newLen);
            diag = $"voice={vl.TotalSeconds:F2}s + add={addTime:F2}s = {fullSec:F2}s → cut={tailCutSec:F1}s → {trimSec:F2}s ({newLen}fr / natural={naturalLen}fr)";
            return newLen;
        }

        // VoiceLength が取れない場合は元のLengthをそのまま使う
        int origLen = obj["Length"]?.GetValue<int>() ?? 1;
        diag = $"VoiceLength取得失敗 origLen={origLen}fr";
        return origLen;
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
