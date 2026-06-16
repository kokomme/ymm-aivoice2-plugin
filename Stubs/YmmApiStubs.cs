#if !YMM4_SDK
// YukkuriMovieMaker.Plugin.dll が Libs/ に存在しない場合のコンパイル用スタブ。
// 実際のYMM4 APIとは異なる可能性があります。
// ProcessCommand.cs の [API要変更] 箇所を実装する際はこのファイルを参考にしてください。
namespace YukkuriMovieMaker.Plugin
{
    public interface IPlugin
    {
        object Details { get; }
        object? CreateSettingsControl();
    }

    public interface IProject { }
}
#endif
