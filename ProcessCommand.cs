namespace YmmAivoice2Plugin;

// [API要変更] このファイルのメソッドはすべて NotImplementedException を投げるプレースホルダです。
// ymmapi.pages.dev でYMM4のタイムラインAPIを確認して実装してください。
public static class ProcessCommand
{
    public static int Execute(PluginSettings settings)
    {
        var voiceItems = CollectVoiceItems();

        if (voiceItems.Count == 0) return 0;

        var entries = voiceItems
            .Select(item => new
            {
                Item   = item,
                Parsed = FilenameParser.TryParse(GetFilePath(item)),
            })
            .Where(x => x.Parsed is not null)
            .Select(x => new
            {
                x.Item,
                x.Parsed,
                TrimmedSec = WavSilenceTrimmer.GetTrimmedDurationSec(
                    x.Parsed!.FullPath,
                    silenceThresholdDb: settings.SilenceThresholdDb,
                    tailMarginSec: settings.TailMarginSec)
            })
            .OrderBy(x => x.Parsed!.Index)
            .ToList();

        double fps = GetProjectFps();

        long cursor = 0;
        foreach (var entry in entries)
        {
            long lengthFrames = (long)Math.Ceiling(entry.TrimmedSec * fps);
            if (lengthFrames <= 0) lengthFrames = 1;
            SetItemFrame(entry.Item, cursor);
            SetItemLength(entry.Item, lengthFrames);
            cursor += lengthFrames;
        }

        return entries.Count;
    }

    // [API要変更] タイムライン上の全レイヤーからボイスアイテムを収集する
    static List<object> CollectVoiceItems()
        => throw new NotImplementedException("CollectVoiceItems: ymmapi.pages.dev でタイムラインアイテム取得APIを確認して実装してください。");

    // [API要変更] ボイスアイテムのWAVファイルパスを取得する
    static string GetFilePath(object voiceItem)
        => throw new NotImplementedException("GetFilePath: ymmapi.pages.dev でVoiceItemのFilePathプロパティを確認して実装してください。");

    // [API要変更] プロジェクトのFPS（フレームレート）を取得する
    static double GetProjectFps()
        => throw new NotImplementedException("GetProjectFps: ymmapi.pages.dev でIProjectのFPSプロパティを確認して実装してください。");

    // [API要変更] アイテムの開始フレームを設定する
    static void SetItemFrame(object voiceItem, long frame)
        => throw new NotImplementedException("SetItemFrame: ymmapi.pages.dev でVoiceItemのFrame設定方法を確認して実装してください。");

    // [API要変更] アイテムの長さ（フレーム数）を設定する
    static void SetItemLength(object voiceItem, long lengthFrames)
        => throw new NotImplementedException("SetItemLength: ymmapi.pages.dev でVoiceItemのLength設定方法を確認して実装してください。");
}
