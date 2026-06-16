using System.Text.RegularExpressions;

namespace YmmAivoice2Plugin;

/// <summary>
/// AIVOICE2書き出しファイル名を解析する。
/// 形式: 000_キャラ名_セリフ10文字.wav
/// </summary>
public sealed record ParsedVoiceFile(
    int Index,
    string CharacterName,
    string TextHint,
    string FullPath);

public static class FilenameParser
{
    static readonly Regex Pattern =
        new(@"^(\d{3})_([^_]+)_(.+)\.wav$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// ファイルパスを解析してParseVoiceFileを返す。マッチしない場合はnull。
    /// </summary>
    public static ParsedVoiceFile? TryParse(string filePath)
    {
        var name = Path.GetFileName(filePath);
        var m = Pattern.Match(name);
        if (!m.Success) return null;

        return new ParsedVoiceFile(
            Index: int.Parse(m.Groups[1].Value),
            CharacterName: m.Groups[2].Value,
            TextHint: m.Groups[3].Value,
            FullPath: filePath);
    }

    public static bool IsMatch(string filePath) => TryParse(filePath) is not null;
}
