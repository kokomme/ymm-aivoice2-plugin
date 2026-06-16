global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text.RegularExpressions;
global using System.Threading.Tasks;

#if !YMM4_SDK
// 実DLLがない場合、スタブの型をグローバルエイリアスとして注入する。
// YukkuriMovieMaker.Plugin 名前空間には一切の型を定義しない（型衝突防止）。
global using IPlugin                = YmmAivoice2Plugin.Stubs.IPlugin;
global using IToolPlugin            = YmmAivoice2Plugin.Stubs.IToolPlugin;
global using PluginDetailsAttribute = YmmAivoice2Plugin.Stubs.PluginDetailsAttribute;
global using PluginType             = YmmAivoice2Plugin.Stubs.PluginType;
#endif
