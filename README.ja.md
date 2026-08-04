日本語 | [简体中文](README.md) | [English](README.en.md)

# UrbanPlanToolbox

都市・地域計画、建築設計、空間研究のためのオフライン優先 Windows ツールボックス。

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) ![Version](https://img.shields.io/badge/version-0.5.0%20Preview-orange) ![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4?logo=windows) ![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet) ![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-0078D4)

## アプリを入手

インストールと更新には [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW) を推奨します。利用者向け製品バージョンは **0.5.0 Preview** です。Store の技術パッケージバージョン `1.0.0.0` は別の値です。

## UrbanPlanToolbox について

UrbanPlanToolbox は、都市・地域計画、建築設計、空間研究のための Windows デスクトップツールボックスです。プロジェクト、ツールデータ、バックアップは原則としてこのデバイスに保存され、主要機能にアカウント、クラウド同期、インターネット接続は必要ありません。

## 主な機能

- 設計・研究プロジェクトの管理、プロジェクトホーム、ワークスペース、アーカイブ、復元。
- プロジェクトのマイルストーンと Windows ローカル通知、作業フォルダーへの入口。
- 計画指標の簡易計算、単位・縮尺換算。
- カラーパレット記録、ワークフローチェックリスト、建築・計画法規インデックス、デザインコンセプト辞典。
- ローカル検索とお気に入り、`.uptbackup` のエクスポート、インポート、復元。
- 中国語（簡体字）、日本語、英語、ライト・ダーク・システムテーマ。

## インストール

一般利用者には Microsoft Store を第一の配布経路として案内しています。リポジトリには x64 framework-dependent の自己署名サイドロード手順もありますが、Store 版より遅れる場合があり、同期された配布とは扱いません。該当する [GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) の説明とチェックサムを確認してください。

## プライバシーとオフライン設計

Microsoft Store 版の更新は Microsoft Store が管理します。サイドロード版が GitHub Releases API にアクセスするのは、利用者が更新確認を明示的に実行した場合だけです。外部の法規、サポート、プロジェクトのリンクは利用者が選択したときだけ開きます。アカウント、広告、テレメトリ、追跡、自動クラッシュ送信、自動的なユーザーデータ送信はありません。プロジェクトとツールのデータはデバイスに保存されます。

[PRIVACY.md](PRIVACY.md) と[オンラインプライバシーポリシー](https://kiyoujyo.github.io/UrbanPlanToolbox/privacy/)をご覧ください。

## システム要件

Windows 10 17763 以降、x64。開発には .NET 10、WinUI 3、Windows App SDK、Windows SDK 10.0.26100.0 が必要です。

## データとバックアップ

アプリデータはローカルのアプリデータディレクトリに保存されます。設定からマニフェストと SHA-256 検証付きの `.uptbackup` をエクスポート、インポート、復元できます。作業フォルダーの内容はバックアップにコピーまたはアップロードされず、インポート後は再選択が必要です。

## ドキュメントと開発

- [ロードマップとバージョン方針](docs/ROADMAP.md)
- [リリースガイド](docs/RELEASE.md)
- [Microsoft Store 公開ガイド](docs/STORE-PUBLISHING.md)
- [変更履歴](CHANGELOG.md)
- [第三者ライセンス情報](THIRD-PARTY-NOTICES.md)

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
```

## 問い合わせ

[GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) または[サポートページ](https://kiyoujyo.github.io/UrbanPlanToolbox/support/)をご利用ください。診断情報を共有する前に、ローカルパス、プロジェクト内容、個人情報を削除してください。

## ライセンス

このリポジトリには現在、プロジェクトのライセンスが明記されていません。決定されるまで、ライセンス付きオープンソースソフトウェアとは表現しないでください。依存関係と外部データの出典は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に記載しています。
