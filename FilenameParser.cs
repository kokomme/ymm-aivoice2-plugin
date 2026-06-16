using System.Text.RegularExpressions;

namespace YmmAivoice2Plugin;

public sealed record ParsedVoiceFile(
    int Index,
    string CharacterName,
    string TextHint,
    string FullPath);

public static class FilenameParser
{
    // 先頭が3桁数字+アンダースコアなら対象（AIVOICE2連番形式）
    static readonly Regex IndexPattern = new(@"^(\d{3,4})_", RegexOptions.Compiled);

    public static ParsedVoiceFile? TryParse(string filePath)
    {
        // 日本語Windows の ¥(U+00A5) をバックスラッシュ(U+005C)に正規化
        filePath = filePath.Replace('¥', '\\');

        // AIVOICE2 がファイル名の先頭にスペースを付けて出力することがあるため Trim する
        var name = Path.GetFileName(filePath).Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var m = IndexPattern.Match(name);
        if (!m.Success) return null;

        return new ParsedVoiceFile(
            Index: int.Parse(m.Groups[1].Value),
            CharacterName: "",
            TextHint: name,
            FullPath: filePath);
    }

    public static bool IsMatch(string filePath) => TryParse(filePath) is not null;
}
