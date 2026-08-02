# 发布与安装说明

## 环境与运行

需要 Windows 10 17763+、.NET 10 SDK、Visual Studio 2026 的“使用 Windows 应用程序开发的 .NET 桌面开发”工作负载，以及 Windows SDK 10.0.26100.0。

```powershell
dotnet restore UrbanPlanToolbox.slnx
dotnet test tests/UrbanPlanToolbox.Tests/UrbanPlanToolbox.Tests.csproj -c Debug -p:Platform=x64
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' UrbanPlanToolbox.csproj /restore /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64
```

使用 `dotnet run --project UrbanPlanToolbox.csproj -p:Platform=x64` 启动调试版本。

## MSIX

在 Visual Studio 中选择 `Release|x64`，使用“发布 -> 创建应用包”。产品显示版本保持为 `0.5.0 Preview`。

当前旁加载人工审阅包使用递增的本机包修订号：此前为 `0.5.0.1`，本轮修复后的审阅包为 `0.5.0.2`。这些版本仅用于本机自签名旁加载，不得直接提交 Microsoft Store。

Microsoft Store 首次候选包使用技术版本 `1.0.0.0`，必须在 Partner Center 关联正式 Identity 和 Publisher 后构建。当前 Store 身份配置为 `Name=JoKiy.UrbanPlanToolbox`、`Publisher=CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`、`PublisherDisplayName=Jo Kiyō`，产品 ID 为 `9MWDPJG1BHKW`。`CN=AppPublisher` 只用于自签名旁加载渠道；GitHub 侧载版和 Microsoft Store 版保持独立发行身份及更新渠道。

当前 CI/本地构建生成的是**未签名测试 MSIX**。它不能直接作为可安装发布包分发；旁加载自签名审阅包须从待审阅的准确提交重新构建，不能复用不同提交的人工验收样品。

本地测试可通过 Visual Studio 向导创建自签名证书，安装到“本地计算机/受信任人员”后再双击 MSIX。证书私钥、密码、MSIX 和发布输出均不可提交 Git。可在“设置 -> 应用 -> 已安装的应用”卸载。

自签名证书不适合正式公开发布；公开发布应使用受信任代码签名证书或 Microsoft Store。升级包须保持相同发布者身份并提高 MSIX 版本。

## 当前有效发布流程

```text
开发与产品化修正
→ 旁加载人工审阅
→ PR 与 CI
→ 合并 main
→ Partner Center 身份关联
→ Store 1.0.0.0 候选包构建
→ 最终 WACK
→ Partner Center 提交
```

## 历史发布记录（仅供参考）

以下内容记录早期版本的验收，不适用于当前 v0.5.0 发布流程。

### 历史验收记录

v0.1.0 已通过人工 UI 验收：首页和 NavigationView 页面切换、示例数据、规划指标计算、建筑面积和停车位联动、冲突提示、自动计算、显示精度、复制、清空、主题和设置持久化均正常。桌面自动化工具无法可靠触发首页入口，但人工操作确认应用导航正常，该自动化兼容性问题不阻塞发布。

v0.1.1 候选安装包已通过人工验收：双击 CMD 安装与卸载、UAC 提升、检测到兼容 Windows App Runtime 时仅安装主 MSIX、从 WindowsApps 包身份启动、首页/计算器/设置/关于页基础功能，以及卸载后准确测试证书清理均正常。共享 Windows App Runtime 未被卸载。

以下路径尚未实际验证：用户取消 UAC、没有兼容 Runtime 时随附依赖的回退安装，以及干净虚拟机或 Windows Sandbox 安装。

## 发布清单

发布标签必须与用户版本一致，例如 v0.1.1 使用 `v0.1.1`。GitHub Release 可上传 MSIX、`.appinstaller`（如有）及安装说明；不得上传证书或私钥。
