# Claude Code 指示書  
## KeepAlive for Tethering（Windows優先・MAUI対応版）

---

# プロジェクト概要

iPhone のインターネット共有（テザリング）は、一定時間通信が発生しない場合に接続が切断されることがあります。

本アプリは、定期的に微小な通信を発生させることで、

- iPhoneテザリング
- Wi-Fiルーター
- モバイル回線
- 一部VPN接続

などのアイドル判定による切断を軽減することを目的としたツールです。

テザリング切断を100％防止できる保証はありません。

---

# 開発方針

## 最重要方針

まず Windows 版を完成させること。

ただし将来的な

- macOS

対応を見据え、アプリケーションロジックとUIを分離してください。

---

# Claudeへの重要な事前検討事項

実装開始前に以下について設計判断を説明してください。

## 1. Pingの実行要件

説明してほしい内容

- Windowsで管理者権限が必要か
- Windows/macOSでの動作可否
- .NET Ping APIの制約

---

## 2. 通信方式の比較

以下を比較してください。

### Ping

長所

- 通信量が少ない

短所

- OS依存がある

---

### HTTP HEAD

長所

- クロスプラットフォーム

短所

- Pingより通信量が増える

---

### TCP Connect

長所

- 実装が単純

短所

- 接続先依存

---

比較した上で、

「Windows版」
「macOS版」

それぞれに最適な方式を提案してください。

---

## 3. Windowsスリープ時の挙動

検討事項

- スリープ中は停止するか
- 復帰後に再開するか
- 追加対応が必要か

---

## 4. UIフレームワーク選定

以下を比較してください。

### MAUI

### WinUI3

比較観点

- クロスプラットフォーム性
- 保守性
- 学習コスト
- 将来性

---

## 5. 最終的な実装方針

比較結果を踏まえて、

どの構成で実装するかを説明してください。

---

# 技術要件

## 必須

- C#
- .NET 8
- Dependency Injection
- MVVM

---

## UIフレームワーク

優先順位

```text
1. .NET MAUI
2. WinUI3
```

Windows版を最初に完成させること。

将来、

- macOS

へ展開可能な構造にしてください。

---

# 推奨アーキテクチャ

## ソリューション構成

```text
KeepAliveForTethering

├─ KeepAlive.Core
├─ KeepAlive.Maui
├─ KeepAlive.Infrastructure
├─ KeepAlive.Tests
└─ README.md
```

---

## 各プロジェクトの役割

### KeepAlive.Core

OS非依存

```text
Models
Interfaces
Services
Strategies
Configuration
Logging
```

を格納する。

---

### KeepAlive.Infrastructure

OS依存機能

例

```text
自動起動
プラットフォーム固有処理
設定保存
```

---

### KeepAlive.Maui

UI

```text
Views
ViewModels
Resources
Platforms
```

---

### KeepAlive.Tests

単体テスト

以下を対象とする。

```text
Strategy
Settings
KeepAliveService
```

---

# フォルダ構成

```text
KeepAlive.Core

├─ Models
│   ├─ KeepAliveResult.cs
│   ├─ AppSettings.cs
│   └─ LogEntry.cs
│
├─ Interfaces
│   ├─ IKeepAliveStrategy.cs
│   ├─ ISettingsService.cs
│   └─ ILogService.cs
│
├─ Services
│   ├─ KeepAliveWorker.cs
│   ├─ SettingsService.cs
│   └─ LogService.cs
│
├─ Strategies
│   ├─ PingKeepAliveStrategy.cs
│   ├─ HttpHeadKeepAliveStrategy.cs
│   └─ TcpConnectKeepAliveStrategy.cs
│
└─ Configuration
```

---

# アーキテクチャ要件

MVVMを採用してください。

使用ライブラリ

```xml
CommunityToolkit.Mvvm
```

---

# Strategy Pattern

通信方式を切り替え可能にすること。

---

## IKeepAliveStrategy

```csharp
public interface IKeepAliveStrategy
{
    string Name { get; }

    Task<KeepAliveResult> ExecuteAsync(
        CancellationToken cancellationToken);
}
```

---

## PingKeepAliveStrategy

利用API

```csharp
Ping.SendPingAsync
```

---

## HttpHeadKeepAliveStrategy

利用API

```csharp
HttpClient.SendAsync
```

HTTPメソッド

```http
HEAD
```

---

## TcpConnectKeepAliveStrategy

利用