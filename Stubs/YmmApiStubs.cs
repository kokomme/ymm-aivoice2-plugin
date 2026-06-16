#if !YMM4_SDK
// YukkuriMovieMaker.Plugin.dll が Libs/ に存在しない場合のコンパイル用スタブ。
// 実DLLを Libs/ に追加したらこのファイルは自動的に無効になる (#if !YMM4_SDK)。
using System.Windows;

namespace YmmAivoice2Plugin.Stubs
{
    public enum PluginType
    {
        Unknown, Voice, Tool, ImageSource, VideoSource, AudioSource,
        Tachie, Shape, Brush, Transition, TextCompletion, Transcription,
        VideoWriter, VideoEffect, AudioEffect, Other
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginDetailsAttribute : Attribute
    {
        public string AuthorName  { get; set; } = "";
        public string ContentId   { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public interface IPlugin
    {
        string Name { get; }
        PluginDetailsAttribute Details { get; }
        PluginType PluginType { get; }
        void Initialize();
        event EventHandler? CreateNewToolViewRequested;
        string[] GetUserPluginFiles();
    }

    public interface IToolPlugin : IPlugin
    {
        Type ViewModelType { get; }
        Type ViewType { get; }
        UIElement[] GetControls();
        object[]    GetToolBarGroups();
    }
}
#endif
