UrbanPlanToolbox v{{DISPLAY_VERSION}} - GitHub one-click installer
============================================================

English
-------
This is the x64 framework-dependent GitHub sideload installer for MSIX {{PACKAGE_VERSION}}. The package is signed with the UrbanPlanToolbox self-signed release certificate, so first-time GitHub installation requires trusting that exact public certificate. The installer handles this automatically.

Install
1. Fully extract the ZIP. Do not run files from the ZIP preview.
2. Double-click "1-Install-UrbanPlanToolbox.cmd" and accept the Windows UAC prompt.
3. Do not manually open files inside the payload folder. The installer verifies the downloaded MSIXBundle and imports only the exact release public certificate it needs.

Security
- The public certificate is imported only into LocalMachine\TrustedPeople, never Trusted Root.
- The installer prefers an already installed compatible Microsoft Windows App Runtime and uses packaged x64 prerequisites only when required.
- It does not disable security software or change the global PowerShell execution policy.

Uninstall
Double-click "2-Uninstall-UrbanPlanToolbox.cmd". It removes only the GitHub sideload UrbanPlanToolbox package and its matching test/release certificate; shared runtimes are not removed.

No private key, PFX/P12 file, or certificate password is included in the repository or installer package.

中文
----
这是 UrbanPlanToolbox v{{DISPLAY_VERSION}} 的 x64 framework-dependent GitHub 旁加载安装包（MSIX {{PACKAGE_VERSION}}）。GitHub 版本使用自签名发布证书；首次安装所需的准确公钥信任由安装脚本自动处理。

安装：完整解压 ZIP 后，双击“1-Install-UrbanPlanToolbox.cmd”并接受 UAC。不要直接运行 payload 中的文件。
卸载：双击“2-Uninstall-UrbanPlanToolbox.cmd”。脚本只移除 GitHub 旁加载版 UrbanPlanToolbox 及其对应证书，不会移除共享 Runtime。
安全边界：证书只导入 LocalMachine\TrustedPeople，不导入 Trusted Root；安装包不包含私钥、PFX/P12 或证书密码。

日本語
------
これは UrbanPlanToolbox v{{DISPLAY_VERSION}} の x64 framework-dependent GitHub サイドロード用インストーラー（MSIX {{PACKAGE_VERSION}}）です。GitHub 版は自己署名のリリース証明書を使用し、初回インストールに必要な公開証明書の信頼設定はインストールスクリプトが自動的に行います。

インストール：ZIP を完全に展開してから「1-Install-UrbanPlanToolbox.cmd」を実行し、UAC を許可してください。payload 内のファイルを直接実行しないでください。
アンインストール：「2-Uninstall-UrbanPlanToolbox.cmd」を実行してください。GitHub サイドロード版と対応する証明書のみを削除し、共有 Runtime は削除しません。
セキュリティ：証明書は LocalMachine\TrustedPeople のみに登録し、Trusted Root には登録しません。秘密鍵、PFX/P12、証明書パスワードは含まれません。
