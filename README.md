# YMM4 AIVOICE2 Helper Plugin

[![Release](https://img.shields.io/github/v/release/kokomme/ymm-aivoice2-plugin?label=ダウンロード&style=for-the-badge)](https://github.com/kokomme/ymm-aivoice2-plugin/releases/latest)

[ゆっくりMovieMaker4 (YMM4)](https://manjubox.net/ymm4/) 向けプラグインです。  
**A.I.VOICE 2** で書き出したWAVをYMM4に取り込む際の2つの手作業を自動化します。

---

## 解決する問題

### 問題 1 — 掛け合いが同じ位置から始まる

YMM4のファイル監視で複数キャラのWAVを自動配置すると、すべてのクリップがタイムラインの**同じ再生位置**から始まってしまいます。

```
Before:
  レイヤー1 (キャラA): [000_A_こんにちは......] ← フレーム0から
  レイヤー2 (キャラB): [001_B_よろしくね......] ← 同じくフレーム0から ❌

After:
  レイヤー1 (キャラA): [000_A_こんにちは......]
  レイヤー2 (キャラB):                          [001_B_よろしくね......] ✅
```

### 問題 2 — WAVの末尾に無音が余る

A.I.VOICE 2 の書き出しWAVには末尾に長い無音が入っており、タイムラインの尺が伸びすぎます。

```
Before: [音声データ.......][--- 無音 ---]
After:  [音声データ.......][余白50ms]    ✅
```

---

## インストール

### 必要環境

- **ゆっくりMovieMaker4** v4.23.0.0 以降
- Windows 10 (1904) 以降 / .NET 8.0 ランタイム（YMM4に同梱）

### ダウンロード

1. **[最新リリースのページ](https://github.com/kokomme/ymm-aivoice2-plugin/releases/latest)** を開く
2. `YmmAivoice2Plugin-vX.X.X.zip` をダウンロード
3. ZIPを解凍して `YmmAivoice2Plugin.dll` を取り出す

### 配置

YMM4のインストールフォルダ内の `Plugins/` に以下の構成で配置します:

```
ゆっくりMovieMaker4/
└── Plugins/
    └── YmmAivoice2Helper/
        └── YmmAivoice2Plugin.dll   ← ここに置く
```

> **Pluginsフォルダの場所**: YMM4を起動 →「ファイル」→「プラグインフォルダを開く」で確認できます。

4. **YMM4を再起動**するとプラグインが読み込まれます。

---

## 使い方

### ファイル名の形式

A.I.VOICE 2 の書き出し設定で、以下の形式のファイル名を使用してください。

```
000_キャラ名_セリフ冒頭10文字.wav
│    │       └─ セリフの冒頭10文字（自由）
│    └─────── キャラクター名（アンダースコア不可）
└──────────── 3桁の連番（000〜）
```

**例:**
```
000_春日部つむぎ_こんにちは世界.wav
001_ずんだもん_よろしくお願い.wav
002_春日部つむぎ_ありがとうご.wav
```

### 手順

1. A.I.VOICE 2 でセリフを書き出す（上記の命名規則で連番WAVを出力）
2. YMM4のファイル監視でWAVを自動配置する（この時点では全クリップが同じ位置）
3. YMM4のプラグインパネルで **「整理を実行」** ボタンを押す
4. 自動で以下が行われます:
   - 各クリップの末尾無音をカット
   - 連番順に並べ直し、前のクリップが終わった位置に次のクリップを配置

### 設定項目

| 設定 | デフォルト | 説明 |
|------|-----------|------|
| 無音閾値 | -40 dB | これより小さい音を「無音」と判断してカット |
| 末尾マージン | 50 ms | 無音カット後に残す余韻の長さ |

---

## ビルド（開発者向け）

### 必要なもの

- .NET 8.0 SDK
- YMM4の `YukkuriMovieMaker.Plugin.dll`（YMM4インストールフォルダから取得）

### 手順

```bash
git clone https://github.com/kokomme/ymm-aivoice2-plugin.git
cd ymm-aivoice2-plugin

# YMM4のDLLをLibs/にコピー
cp "C:\ゆっくりMovieMaker4\YukkuriMovieMaker.Plugin.dll" Libs/

dotnet build -c Release -r win-x64 --no-self-contained
# → bin/Release/net8.0-windows10.0.19041.0/win-x64/YmmAivoice2Plugin.dll
```

### リリース

`v` から始まるタグをpushするとGitHub Actionsが自動でビルド＆リリースを作成します。

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## 現在の制限事項

YMM4 Plugin APIの以下の部分は未実装です（`ProcessCommand.cs` の `// [API要変更]` 箇所）。  
[ymmapi.pages.dev](https://ymmapi.pages.dev/) を参照して実装を完成させる必要があります。

- タイムラインアイテムの取得・操作（Frame / Length プロパティ）
- プロジェクトFPSの取得

---

## ライセンス

MIT
