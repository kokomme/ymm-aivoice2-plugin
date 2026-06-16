#if YMM4_SDK
using YukkuriMovieMaker.Plugin;
#endif

namespace YmmAivoice2Plugin;

public class Aivoice2HelperPlugin : IToolPlugin
{
    public string Name => "AIVOICE2 Helper";
    public Type ViewModelType => typeof(Aivoice2HelperViewModel);
    public Type ViewType => typeof(SettingsPanel);
}
