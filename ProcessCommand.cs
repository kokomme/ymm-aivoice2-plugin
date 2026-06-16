#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif

namespace YmmAivoice2Plugin;

/// <summary>
/// 「整理を実行」ボタンのコアロジック。
///
/// 処理内容:
///   1. タイムラインのボイスアイテムからパターン一致するものを取得
///   2. WAV末尾無音を解析しトリム後の長さを計算
///   3. 連番順に並べ、各アイテムの開始フレームを前のアイテムの終了フレームに設定
///
/// YMM4 API バインドについて:
///   このファイルでは IProject / ITimeline / IVoiceItem の3インターフェースを使う。
///   実際のクラス名・プロパティ名は ymmapi.pages.dev で確認して修正してください。
///   変更が必要な箇所には // [API] コメントを付けています。
/// </summary>
public static class ProcessCommand
{
    /// <summary>
    /// プロジェクトのタイムラインを整理する。
    /// </summary>
    /// <param name="project">YMM4の現在プロジェクト</param>
    /// <param name="settings">プラグイン設定</param>
    /// <returns>処理したアイテム数</returns>
    public static int Execute(IProject project, PluginSettings settings)
    {
        // [API] タイムライン上のすべてのボイスアイテムを収集する。
        // YMM4のAPIに合わせて変更してください。
        var voiceItems = CollectVoiceItems(project);

        if (voiceItems.Count == 0) return 0;

        // WAV無音解析と連番ソート
        var entries = voiceItems
            .Select(item => new
            {
                Item = item,
                Parsed = FilenameParser.TryParse(GetFilePath(item)),  // [API]
                TrimmedSec = default(double?)
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

        // [API] プロジェクトのFPSを取得する。
        double fps = GetProjectFps(project);

        // 連番順に開始フレームを詰めて配置する
        long cursor = 0;
        foreach (var entry in entries)
        {
            long lengthFrames = (long)Math.Ceiling(entry.TrimmedSec * fps);
            if (lengthFrames <= 0) lengthFrames = 1;

            // [API] アイテムの開始フレームと長さを設定する。
            SetItemFrame(entry.Item, cursor);
            SetItemLength(entry.Item, lengthFrames);

            cursor += lengthFrames;
        }

        return entries.Count;
    }

    // ================================================================
    // 以下の private メソッドは YMM4 API に合わせて実装してください。
    // ymmapi.pages.dev を参照し、正しいクラス名・メソッドに変更すること。
    // ================================================================

    /// <summary>
    /// [API要変更] タイムライン上の全レイヤーからボイスアイテムを収集する。
    /// </summary>
    static List<object> CollectVoiceItems(IProject project)
    {
        // TODO: 実際のYMM4 APIに置き換える
        // 例:
        //   return project.Timeline.Layers
        //       .SelectMany(layer => layer.Items)
        //       .OfType<IVoiceItem>()
        //       .Cast<object>()
        //       .ToList();
        throw new NotImplementedException(
            "CollectVoiceItems: ymmapi.pages.dev でタイムラインアイテム取得APIを確認して実装してください。");
    }

    /// <summary>
    /// [API要変更] ボイスアイテムのWAVファイルパスを取得する。
    /// </summary>
    static string GetFilePath(object voiceItem)
    {
        // TODO: 実際のYMM4 APIに置き換える
        // 例:
        //   return ((IVoiceItem)voiceItem).FilePath;
        throw new NotImplementedException(
            "GetFilePath: ymmapi.pages.dev でVoiceItemのFilePathプロパティを確認して実装してください。");
    }

    /// <summary>
    /// [API要変更] プロジェクトのFPS（フレームレート）を取得する。
    /// </summary>
    static double GetProjectFps(IProject project)
    {
        // TODO: 実際のYMM4 APIに置き換える
        // 例:
        //   return project.FPS;
        //   または project.FrameRate など
        throw new NotImplementedException(
            "GetProjectFps: ymmapi.pages.dev でIProjectのFPSプロパティを確認して実装してください。");
    }

    /// <summary>
    /// [API要変更] アイテムの開始フレームを設定する。
    /// </summary>
    static void SetItemFrame(object voiceItem, long frame)
    {
        // TODO: 実際のYMM4 APIに置き換える
        // 例:
        //   ((IVoiceItem)voiceItem).Frame = frame;
        throw new NotImplementedException(
            "SetItemFrame: ymmapi.pages.dev でVoiceItemのFrame設定方法を確認して実装してください。");
    }

    /// <summary>
    /// [API要変更] アイテムの長さ（フレーム数）を設定する。
    /// </summary>
    static void SetItemLength(object voiceItem, long lengthFrames)
    {
        // TODO: 実際のYMM4 APIに置き換える
        // 例:
        //   ((IVoiceItem)voiceItem).Length = lengthFrames;
        throw new NotImplementedException(
            "SetItemLength: ymmapi.pages.dev でVoiceItemのLength設定方法を確認して実装してください。");
    }
}
