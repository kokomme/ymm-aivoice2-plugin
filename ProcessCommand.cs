namespace YmmAivoice2Plugin;

public static class ProcessCommand
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// 現在のYMM4プロジェクトを整理する。
    /// 戻り値: 処理件数（-1 = プロジェクト未検出）
    /// </summary>
    public static int Execute(PluginSettings settings)
    {
        var ymmpPath = ProjectDetector.GetCurrentProjectPath();
        if (ymmpPath == null) return -1;

        var json = File.ReadAllText(ymmpPath);
        var doc  = JsonNode.Parse(json);
        if (doc == null) return -1;

        double fps = doc["VideoInfo"]?["FPS"]?.GetValue<double>() ?? 30.0;

        // 対象ボイスアイテムを収集
        var entries = new List<(JsonObject Item, ParsedVoiceFile Parsed, double TrimmedSec)>();

        var timelines = doc["Timelines"]?.AsArray();
        if (timelines == null) return 0;

        foreach (var timeline in timelines)
        {
            var items = timeline?["Items"]?.AsArray();
            if (items == null) continue;

            foreach (var item in items)
            {
                if (item is not JsonObject obj) continue;

                var filePath = obj["FilePath"]?.GetValue<string>();
                if (string.IsNullOrEmpty(filePath)) continue;
                if (!filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;

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

        if (entries.Count == 0) return 0;

        // バックアップ
        File.Copy(ymmpPath, ymmpPath + ".bak", overwrite: true);

        // 連番順に開始フレームを詰めて配置
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
}
