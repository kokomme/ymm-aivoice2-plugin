using System.Text.RegularExpressions;

namespace YmmAivoice2Plugin;

public sealed record ParsedVoiceFile(
    int Index,
    string CharacterName,
    string TextHint,
    string FullPath);

public static class FilenameParser
{
    static readonly Regex Pattern =
        new(@"^(\d{3})_([^_]+)_(.+)\.wav$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ParsedVoiceFile? TryParse(string filePath)
    {
        // 日本語Windowsでは¥(U+00A5)がパス区切りとして使われる
        // Path.GetFileName は U+00A5 を区切りと認識しないため正規化する
        filePath = filePath.Replace('¥', '\\');

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
