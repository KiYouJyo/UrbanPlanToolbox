日本語 | [简体中文](README.md) | [English](README.en.md)

# UrbanPlanToolbox

都市・地域計画、建築設計、空間研究向けのオフライン優先 Windows ツールボックスです。

[![MIT License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) [![CI](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/UrbanPlanToolbox/actions/workflows/ci.yml) [![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/UrbanPlanToolbox?display_name=tag&sort=semver)](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) [![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20WinUI%203-0078D4?logo=windows)](https://github.com/KiYouJyo/UrbanPlanToolbox) [![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoftstore&logoColor=white)](https://apps.microsoft.com/detail/9MWDPJG1BHKW)

## 入手

- [GitHub 最新正式 Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest)：より頻繁に更新される x64 サイドロード版です。
- [Microsoft Store](https://apps.microsoft.com/detail/9MWDPJG1BHKW)：Microsoft Store からインストール・更新したいユーザー向けです。

2 つのチャネルは ID と更新経路が独立しており、相互に上書き更新できません。バージョンの詳細は [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) と [CHANGELOG.md](CHANGELOG.md) を参照してください。

## 主な機能

- 設計・研究プロジェクト管理、プロジェクトホーム、ワークスペース、アーカイブ、復元。
- プロジェクトのマイルストーン、ローカル通知、作業フォルダーへのアクセス。
- 計画指標、単位・縮尺変換、カラーパレット、ワークフローチェックリスト。
- 建築・計画規則、デザインコンセプト辞典、ローカル検索、お気に入り。
- WGS 84、GCJ-02、BD-09 のポイント変換と Shapefile のローカル処理。
- 調査写真の整理、EXIF/GPS 読み取り、GIS ポイント、CSV 出力。
- 简体中文、日本語、English；ライト、ダーク、システムテーマ。

## インストールと更新

### GitHub 初回インストール

[最新 GitHub Release](https://github.com/KiYouJyo/UrbanPlanToolbox/releases/latest) からワンクリックインストーラーをダウンロードしてください。必要な証明書設定を完了し、Windows App Installer でアプリをインストールするため、その後はアプリ内で更新できます。裸の `.appinstaller` は通常の初回インストール入口として推奨しません。

### その後の更新

About を開き、「更新を確認」を選択してください。GitHub 版は Windows App Installer で更新し、Microsoft Store 版は Microsoft Store が管理します。

### 上級者向けインストール

手動配置や検証用に、Release Assets から `.appinstaller`、`.msixbundle`、SHA-256 マニフェストを取得できます。

## プライバシーとオフライン設計

コア機能にアカウント、クラウド同期、インターネット接続は必要ありません。プロジェクト、ツールデータ、バックアップは原則として端末内に保存されます。更新確認はユーザーが要求した場合だけ対象チャネルへアクセスします。写真、GPS、座標データはローカルで処理します。

## システム要件

Windows 10 17763 以降、x64。

## データとバックアップ

設定から、マニフェストと SHA-256 検証付きの `.uptbackup` をエクスポート、インポート、復元できます。作業フォルダーの内容はバックアップへアップロード・コピーされず、インポート後に再選択が必要です。

## 言語

简体中文、日本語、English に対応し、Settings で即時に切り替えられます。

## ドキュメント

- [ロードマップとバージョン方針](docs/ROADMAP.md)
- [リリースガイド](docs/RELEASE.md)
- [データ保存](docs/DATA_STORAGE.md) · [データバックアップ](docs/DATA_BACKUP.md)
- [変更履歴](CHANGELOG.md)
- [プライバシーポリシー](PRIVACY.md) · [第三者通知](THIRD-PARTY-NOTICES.md)

## 開発とビルド

```powershell
dotnet restore UrbanPlanToolbox.slnx -p:Configuration=Debug -p:Platform=x64
dotnet test UrbanPlanToolbox.slnx -c Debug -p:Platform=x64 --no-restore
```

完全なビルドとリリース手順は [docs/RELEASE.md](docs/RELEASE.md) を参照してください。

## フィードバック

[GitHub Issues](https://github.com/KiYouJyo/UrbanPlanToolbox/issues) または[サポートページ](https://kiyoujyo.github.io/UrbanPlanToolbox/support/)から報告してください。診断情報を共有する前に、ローカルパスと個人データを削除してください。

## License

UrbanPlanToolbox は [MIT License](LICENSE) のオープンソースです。
