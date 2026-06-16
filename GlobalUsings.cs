global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text.RegularExpressions;
global using System.Threading.Tasks;

#if !YMM4_SDK
// 実DLLがない場合、スタブの型をグローバルエイリアスとして注入する。
// YukkuriMovieMaker.Plugin 名前空間に一切の型を定義しないことが重要
// （型衝突による YMM4 PluginLoader クラッシュを防ぐため）。
global using IPlugin  = YmmAivoice2Plugin.Stubs.IPlugin;
global using IProject = YmmAivoice2Plugin.Stubs.IProject;
#endif
