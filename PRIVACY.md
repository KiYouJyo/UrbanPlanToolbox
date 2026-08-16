# UrbanPlanToolbox Privacy / 隐私政策 / プライバシーポリシー

Version 1.8.2 · Last updated 2026-08-16

## 简体中文

UrbanPlanToolbox 不要求应用账户，不收集、出售用户个人信息，不包含广告、遥测、追踪、自动崩溃上传或自动日志上传。项目、设置、收藏、工具数据、时间节点、通知数据和应用管理的附件默认保存在本机 `%LocalAppData%\UrbanPlanToolbox`。

用户可主动配置自己的 WebDAV 服务用于云存档。只有在用户明确执行“测试并保存连接”“立即创建云存档”“管理云存档”“恢复”或“删除”等 WebDAV 操作时，应用才会连接用户填写的 WebDAV 服务器。云存档使用现有 `.uptbackup` 格式；其中包含可迁移的应用数据，但不包含 WebDAV 密码。WebDAV 用户名、服务器地址和远端目录保存在本机，密码使用 Windows Credential Locker 保存。应用不会把 WebDAV 密码写入日志、`.uptbackup` 或仓库文件。使用 HTTP 而非 HTTPS 时，网络传输可能无法保护凭据，应用会提示优先使用 HTTPS。远端数据由用户选择的 WebDAV 服务提供商负责，其隐私与数据保留规则由该服务提供商决定。

WebDAV 云存档不会成为应用的主数据源；网络不可用不会阻止本地项目和工具继续工作。“清空所有本地数据”会删除本机 WebDAV 配置和本机保存的 WebDAV 凭据，但不会自动删除远端云存档。只有用户在云存档管理界面明确确认删除某个远端存档时，应用才会向 WebDAV 服务发送删除请求。

用户选择的工作文件夹只保存引用和系统访问令牌；应用不会自动复制、扫描或删除外部文件夹。Shapefile 等坐标文件只在设备本机读取和转换，不会因 WebDAV 功能自动上传。Microsoft Store 版仅在更新相关操作时访问 Microsoft Store 服务；GitHub 旁加载版仅在更新相关操作时访问 GitHub Releases API。项目、政策和支持链接只在用户明确操作后由系统默认浏览器打开。

## 日本語

UrbanPlanToolbox はアプリ独自のアカウントを要求せず、個人情報を収集・販売しません。広告、テレメトリ、トラッキング、自動クラッシュ送信、自動ログ送信もありません。プロジェクト、設定、お気に入り、ツールデータ、マイルストーン、通知データ、アプリ管理の添付ファイルは既定で `%LocalAppData%\UrbanPlanToolbox` にローカル保存されます。

ユーザーは自分の WebDAV サービスを明示的に設定し、クラウドアーカイブに使用できます。アプリが WebDAV サーバーへ接続するのは、ユーザーが「接続テストして保存」「今すぐクラウド保存」「クラウドアーカイブを管理」「復元」「削除」などの WebDAV 操作を明示的に実行した場合だけです。クラウドアーカイブは既存の `.uptbackup` 形式を使用し、移行可能なアプリデータを含みますが、WebDAV パスワードは含みません。WebDAV のサーバー URL、ユーザー名、リモートフォルダーはローカルに保存し、パスワードは Windows Credential Locker に保存します。パスワードをログ、`.uptbackup`、リポジトリファイルへ書き込みません。HTTP 接続では資格情報が十分に保護されない可能性があるため、HTTPS を推奨する警告を表示します。リモートデータには、ユーザーが選択した WebDAV 提供者のプライバシーおよび保持ポリシーが適用されます。

WebDAV はローカルデータの代わりの主データソースではありません。ネットワークが利用できなくてもローカルのプロジェクトとツールは使用できます。「すべてのローカルデータを消去」はローカルの WebDAV 設定と保存済み資格情報を削除しますが、リモートのクラウドアーカイブは自動削除しません。リモート削除はクラウドアーカイブ管理画面でユーザーが対象ファイルの削除を明示的に確認した場合だけ実行します。

ユーザーが選択した作業フォルダーは参照と OS のアクセス トークンだけを保持し、外部フォルダーを自動コピー、スキャン、削除しません。Shapefile などの座標ファイルは端末上でのみ処理され、WebDAV 機能によって自動アップロードされません。Microsoft Store 版は更新操作時のみ Store サービスへ、GitHub サイドロード版は更新操作時のみ GitHub Releases API へ接続します。

## English

UrbanPlanToolbox requires no app-specific account and does not collect or sell personal information. It has no ads, telemetry, tracking, automatic crash upload, or automatic log upload. Projects, settings, favorites, tool data, milestones, notification data, and app-managed attachments are stored locally by default under `%LocalAppData%\UrbanPlanToolbox`.

Users may explicitly configure their own WebDAV service for cloud archives. The app connects to the supplied WebDAV server only when the user explicitly performs WebDAV actions such as Test & Save, Create cloud archive, Manage cloud archives, Restore, or Delete. Cloud archives use the existing `.uptbackup` format. They contain portable application data but do not contain the WebDAV password. The WebDAV server URL, username, and remote folder are stored locally; the password is stored with Windows Credential Locker. The app does not write the WebDAV password to logs, `.uptbackup` files, or repository files. HTTP may not adequately protect credentials in transit, so the app warns users to prefer HTTPS. Remote data is also subject to the privacy and retention policies of the WebDAV provider selected by the user.

WebDAV does not replace the local data store as the authoritative source. Loss of network access does not block local project or tool use. Clear all local data removes the local WebDAV configuration and locally stored WebDAV credential, but it does not automatically delete remote cloud archives. A remote archive is deleted only after the user explicitly confirms deletion for that archive in the cloud archive manager.

A selected work folder is retained only as a reference and OS access token; the app does not automatically copy, scan, or delete the external folder. Coordinate datasets such as Shapefiles are processed locally and are not automatically uploaded by the WebDAV feature. The Microsoft Store edition contacts Microsoft Store services for update-related actions, while the GitHub sideload edition contacts the GitHub Releases API for update-related actions. Project, policy, and support links open only after explicit user action.
