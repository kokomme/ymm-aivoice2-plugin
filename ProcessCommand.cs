namespace YmmAivoice2Plugin;

public static class ProcessCommand
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string LastDiagLog  { get; private set; } = "";
    public static bool   AutoReloaded { get; private set; } = false;

    public static int Execute()
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

        double internalFps = DetectInternalFps(timelines, out string fpsDiag);
        log.AppendLine($"内部FPS: {internalFps} ({fpsDiag})");

        // WAVパス → 全JsonObjectのリスト (重複タイムライン対応)
        var wavToObjs = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
        var entries   = new List<(string WavPath, int Index, int LengthFrames)>();
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

                if (!wavToObjs.TryGetValue(filePath, out var objList))
                    wavToObjs[filePath] = objList = new List<JsonObject>();
                objList.Add(obj);

                if (objList.Count > 1) continue; // 重複は後でまとめて更新

                var parsed = FilenameParser.TryParse(filePath);
                if (parsed == null)
                {
                    log.AppendLine($"  [skip] ファイル名パース失敗: {Path.GetFileName(filePath)}");
                    continue;
                }

                int newLength = ComputeLength(obj, internalFps, out string lenDiag);
                log.AppendLine($"  [{entries.Count + 1}] {Path.GetFileName(parsed.FullPath)}: {lenDiag}");
                entries.Add((filePath, parsed.Index, newLength));
            }
        }

        int uniqueWav = wavToObjs.Count;
        int dupObjs   = wavItems - uniqueWav;
        log.AppendLine($"全アイテム: {totalItems}件 / WAV: {wavItems}件 / ユニーク: {uniqueWav}件" +
                       (dupObjs > 0 ? $" (重複{dupObjs}件)" : ""));

        if (entries.Count == 0)
        {
            LastDiagLog = log.ToString().TrimEnd();
            return 0;
        }

        File.Copy(ymmpPath, ymmpPath + ".bak", overwrite: true);

        var sorted = entries.OrderBy(e => e.Index).ToList();
        long cursor = 0;
        foreach (var (wavPath, _, frameCount) in sorted)
        {
            foreach (var obj in wavToObjs[wavPath])
            {
                obj["Frame"]  = JsonValue.Create((int)cursor);
                obj["Length"] = JsonValue.Create(frameCount);
                // Layer は D&D 配置のまま変更しない
            }
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

    static double DetectInternalFps(JsonArray timelines, out string diag)
    {
        var candidates = new List<double>();

        foreach (var timeline in timelines)
        {
            if (timeline?["FPS"]?.GetValue<double>() is double tFps && tFps > 0)
            {
                diag = $"Timeline.FPS={tFps}";
                return tFps;
            }

            foreach (var item in timeline?["Items"]?.AsArray() ?? new JsonArray())
            {
                if (item is not JsonObject obj) continue;
                var origLen = obj["Length"]?.GetValue<int>() ?? 0;
                if (origLen <= 0) continue;
                if (obj["VoiceLength"]?.GetValue<string>() is not string vlStr) continue;
                if (!TimeSpan.TryParse(vlStr, out var vl) || vl.TotalSeconds <= 0) continue;
                var addTime = obj["AdditionalTime"]?.GetValue<double>() ?? 0.0;
                var total   = vl.TotalSeconds + addTime;
                if (total <= 0) continue;
                var implied = origLen / total;
                if (implied >= 20 && implied <= 300)
                    candidates.Add(implied);
            }
        }

        if (candidates.Count > 0)
        {
            var max     = candidates.Max();
            var rounded = Math.Round(max);
            diag = $"候補=[{string.Join(", ", candidates.Select(c => $"{c:F1}"))}] 最大={max:F2} → {rounded}";
            return rounded;
        }

        diag = "検出失敗 → 30フォールバック";
        return 30.0;
    }

    // VoiceLength + AdditionalTime をそのままフレーム数に変換（末尾カットなし）
    static int ComputeLength(JsonObject obj, double fps, out string diag)
    {
        if (obj["VoiceLength"]?.GetValue<string>() is string vlStr &&
            TimeSpan.TryParse(vlStr, out var vl))
        {
            double addTime = obj["AdditionalTime"]?.GetValue<double>() ?? 0.0;
            double fullSec = vl.TotalSeconds + addTime;
            int newLen     = Math.Max(1, (int)Math.Ceiling(fullSec * fps));
            diag = $"voice={vl.TotalSeconds:F2}s + add={addTime:F2}s = {fullSec:F2}s ({newLen}fr)";
            return newLen;
        }

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
