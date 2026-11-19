# 貢献ガイド

[简体中文](CONTRIBUTING.md) | [English](CONTRIBUTING.en.md)

Issue、ドキュメントの改善、十分に説明されたコード変更の提案を歓迎します。Pull Request を作成する前に、まず Issue で問題や提案を説明してください。

## 対象範囲

再現可能な不具合、プライバシーまたはデータセキュリティの問題、既存ドキュメントの修正、現在のロードマップに沿った改善を優先します。新しいツールやデータソースについては、ユーザーシナリオ、データの範囲、バックアップへの影響、三言語文案を先に説明してください。

## Issue と Pull Request

Issue にはアプリのバージョン、インストール経路、Windows のバージョン、再現手順、期待結果、実際の結果を含めてください。Pull Request には変更範囲、検証方法、ローカルデータへの影響の有無を記載してください。提出前にテスト、Debug/Release x64 ビルド、`git diff --check` を実行してください。

## ブランチとコミット

`main` からトピックブランチを作成し、`docs: ...`、`fix: ...`、`feat: ...` のような簡潔な Conventional Commits 形式のメッセージを使用してください。`main` を直接変更しないでください。

## ビルドとテスト

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
dotnet build UrbanPlanToolbox.slnx -c Release -p:Platform=x64 --no-restore
```

`Strings/zh-CN`、`Strings/ja-JP`、`Strings/en-US` の三言語リソースは同じキーセットを維持してください。新しいツールは[ツール開発テンプレート](docs/TOOL_DEVELOPMENT_TEMPLATE.ja.md)に従い、登録、ナビゲーション、検索、お気に入り、保存、バックアップ、ローカライズの契約に接続してください。

## データと秘密情報

証明書、秘密鍵、PFX、Token、ユーザーデータ、マシン固有のパス、MSIX、その他のビルド成果物をコミットしないでください。本アプリはローカル優先で、ユーザーデータを自動的にアップロードしません。

## コミュニティ文書

- [セキュリティポリシー](SECURITY.ja.md)
- [行動規範](CODE_OF_CONDUCT.ja.md)
