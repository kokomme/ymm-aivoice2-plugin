#if !YMM4_SDK
namespace YmmAivoice2Plugin.Stubs
{
    public interface IToolPlugin
    {
        string Name { get; }
        Type ViewModelType { get; }
        Type ViewType { get; }
    }
}
#endif
