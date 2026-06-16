#if !YMM4_SDK
// YukkuriMovieMaker.Plugin.dll が Libs/ に存在しない場合のコンパイル用スタブ。
// YukkuriMovieMaker.Plugin 名前空間とは別にして型衝突を避ける。
// 実DLLを Libs/ に追加したら自動的にこのスタブは無効になる。
namespace YmmAivoice2Plugin.Stubs
{
    public interface IPlugin
    {
        object Details { get; }
        object? CreateSettingsControl();
    }

    public interface IProject { }
}
#endif
