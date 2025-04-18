# Unity MCP 統合フレームワーク

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
![Unity](https://img.shields.io/badge/Unity-2023.2.19f1+-black.svg)
![.NET](https://img.shields.io/badge/.NET-C%23_9.0-purple.svg)

Unity と Model Context Protocol (MCP) を統合するための拡張可能なフレームワークです。このフレームワークにより、Claude などの AI 言語モデルがスケーラブルなコマンドハンドラーアーキテクチャを通じて Unity エディタと直接対話することができます。

## 🌟 特徴

- **拡張可能なプラグインアーキテクチャ**: カスタムコマンドハンドラーを作成・登録して機能を拡張
- **MCP 統合**: モデルコンテキストプロトコルをベースに AI モデルとシームレスに統合
- **TypeScript & C# サポート**: サーバーコンポーネントは TypeScript、Unity コンポーネントは C#
- **エディタ統合**: カスタマイズ可能な設定を持つエディタツールとして動作
- **自動検出**: コマンドハンドラーの自動検出と登録
- **通信**: Unity と外部 AI サービス間の TCP/IP ベースの通信

## 📋 必要条件

- Unity 2023.2.19f1 以上
- .NET/C# 9.0
- Node.js 18.0.0 以上と npm（TypeScript サーバー用）
   - [Node.js 公式サイト](https://nodejs.org/)からインストールしてください

## 🚀 はじめに

### インストール方法

1. Unity パッケージマネージャーを使用してインストール:
   - パッケージマネージャーを開く (Window > Package Manager)
   - 「+」ボタンをクリック
   - 「Add package from git URL...」を選択
   - 入力: `https://github.com/isuzu-shiranui/UnityMCP.git?path=jp.shiranui-isuzu.unity-mcp`

### クイックセットアップ

1. Unity を開き、Edit > Preferences > Unity MCP に移動
2. 接続設定 (ホストとポート) を構成
3. 「Start Server」をクリックして接続の待ち受けを開始

### Claude Desktop との連携

1. リリースページから最新のZIPファイルをダウンロードして解凍します
2. `build/index.js` ファイルのフルパスを控えておきます
3. Claude Desktop の設定ファイル `claude_desktop_config.json` を開きます
4. 以下の内容を追加して保存します:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": [
        "path/to/index.js"
      ]
    }
  }
}
```
※ `path/to/index.js` は実際のパスに置き換えてください（Windowsの場合はバックスラッシュをエスケープ"\\\\"するか、フォワードスラッシュ"/"を使用）

## 🔌 アーキテクチャ

Unity MCP フレームワークは主に 2 つのコンポーネントで構成されています:

### 1. Unity C# プラグイン

- **McpServer**: TCP 接続をリッスンしコマンドをルーティングするコアサーバー
- **IMcpCommandHandler**: カスタムコマンドハンドラーを作成するためのインターフェース
- **McpSettings**: プラグイン設定を管理
- **McpServiceManager**: サービス管理のための依存性注入システム
- **McpHandlerDiscovery**: コマンドハンドラーを自動検出して登録

### 2. TypeScript MCP クライアント

- **HandlerAdapter**: コマンドハンドラーを MCP SDK ツールに適応
- **HandlerDiscovery**: コマンドハンドラーの検出と登録
- **UnityConnection**: Unity との TCP/IP 通信を管理
- **BaseCommandHandler**: コマンドハンドラーを実装するためのベースクラス

## 🔬 サンプルコード

パッケージには以下のサンプルが含まれています：

1. **Unity MCP Handler Samples**
   - C#実装のサンプルコード
   - そのままプロジェクトにインポートして使用可能

2. **Unity MCP Handler Samples JavaScript**
   - JavaScript実装のサンプルコード
   - この中のJSファイルは`build/handlers`ディレクトリにコピーして使用してください

> ⚠️ **注意**: サンプルコードには任意コード実行機能が含まれています。本番環境での使用には十分注意してください。

サンプルのインポート方法:
1. Unity パッケージマネージャーで本パッケージを選択
2. 「Samples」タブをクリック
3. 必要なサンプルの「Import」ボタンをクリック

## 🛠️ カスタムコマンドハンドラーの作成

### C# (Unity)

`IMcpCommandHandler` を実装する新しいクラスを作成:

```csharp
using Newtonsoft.Json.Linq;
using UnityMCP.Editor.Core;

namespace YourNamespace.Handlers
{
    internal sealed class YourCommandHandler : IMcpCommandHandler
    {
        public string CommandPrefix => "yourprefix";
        public string Description => "ハンドラーの説明";

        public JObject Execute(string action, JObject parameters)
        {
            // コマンドロジックを実装
            if (action == "yourAction")
            {
                // パラメータを使って何かを実行
                return new JObject
                {
                    ["success"] = true,
                    ["result"] = "結果データ"
                };
            }

            return new JObject
            {
                ["success"] = false,
                ["error"] = $"不明なアクション: {action}"
            };
        }
    }
}
```

**注意**: `IMcpCommandHandler`を実装したクラスはプロジェクト内のどこに配置しても、アセンブリ検索によって自動的に検出・登録されます。

### TypeScript (クライアント)

`BaseCommandHandler` を拡張して新しいハンドラーを作成:

```typescript
import { IMcpToolDefinition } from "../core/interfaces/ICommandHandler.js";
import { JObject } from "../types/index.js";
import { z } from "zod";
import { BaseCommandHandler } from "../core/BaseCommandHandler.js";

export class YourCommandHandler extends BaseCommandHandler {
    public get commandPrefix(): string {
        return "yourprefix";
    }

    public get description(): string {
        return "ハンドラーの説明";
    }

    protected async executeCommand(action: string, parameters: JObject): Promise<JObject> {
        // コマンドロジックを実装
        // リクエストを Unity に転送
        return await this.sendUnityRequest(
            `${this.commandPrefix}.${action}`,
            parameters
        );
    }

    public getToolDefinitions(): Map<string, IMcpToolDefinition> {
        const tools = new Map<string, IMcpToolDefinition>();

        // ツールを定義
        tools.set("yourprefix_yourAction", {
            description: "アクションの説明",
            parameterSchema: {
                param1: z.string().describe("パラメータの説明"),
                param2: z.number().optional().describe("オプションパラメータ")
            }
        });

        return tools;
    }
}
```

### TypeScriptハンドラーのビルドと配置

TypeScriptのカスタムコマンドハンドラーを追加する手順:

1. ソースコードを作成（上記のような形式で実装）
2. TypeScriptプロジェクトでビルドを実行:
   ```bash
   npm run build
   ```
3. ビルドされたJSファイル（`YourCommandHandler.js`）を`build/handlers`ディレクトリに配置
4. サーバーを再起動すると、新しいハンドラーが自動的に検出・登録される

これにより、プロジェクトのソースコードを変更することなく、誰でも簡単に新しい機能を追加できます。

## 🔄 コマンドフロー

1. Claude (または他の AI) が TypeScript で MCP ツールを呼び出す
2. TypeScript サーバーが TCP 経由で Unity にリクエストを転送
3. Unity の McpServer がリクエストを受信し、適切なハンドラーを見つける
4. ハンドラーが Unity のメインスレッドでコマンドを実行
5. 結果が TCP 接続を通じて TypeScript サーバーに戻される
6. TypeScript サーバーが結果をフォーマットして Claude に返す

## ⚙️ 設定

### Unity 設定

Edit > Preferences > Unity MCP から設定にアクセス:

- **Host**: サーバーをバインドする IP アドレス (デフォルト: 127.0.0.1)
- **Port**: リッスンするポート (デフォルト: 27182)
- **Auto-start on Launch**: Unity 起動時に自動的にサーバーを開始
- **Auto-restart on Play Mode Change**: プレイモードの開始/終了時にサーバーを再起動
- **Detailed Logs**: デバッグ用の詳細ログを有効化

### TypeScript 設定

TypeScript サーバーの環境変数:

- `UNITY_HOST`: Unity サーバーホスト (デフォルト: 127.0.0.1)
- `UNITY_PORT`: Unity サーバーポート (デフォルト: 27182)

## 📚 組み込みコマンドハンドラー

### Unity (C#)

- **MenuItemCommandHandler**: Unity エディタのメニュー項目を実行

### TypeScript

- **MenuItemCommandHandler**: メニュー項目の実行を Unity に転送

## ⚠️ セキュリティに関する注意

1. **信頼できないハンドラーを実行しない**: 第三者が作成したハンドラーコードは、事前にセキュリティレビューを行ってから使用してください。


## 📄 ライセンス

このプロジェクトは MIT ライセンスの下で提供されています - 詳細はライセンスファイルを参照してください。

---

Shiranui-Isuzu いすず
