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

在 Visual Studio 中选择 `Release|x64`，使用“发布 -> 创建应用包”。显示版本为 `0.1.0`，MSIX 版本为 `0.1.0.0`；每次发布都须同步更新项目、清单、关于页、README 和 CHANGELOG。

当前 CI/本地构建生成的是**未签名测试 MSIX**。它尚未完成证书信任、本机安装或卸载验证，不能直接作为可安装发布包分发。

本地测试可通过 Visual Studio 向导创建自签名证书，安装到“本地计算机/受信任人员”后再双击 MSIX。证书私钥、密码、MSIX 和发布输出均不可提交 Git。可在“设置 -> 应用 -> 已安装的应用”卸载。

自签名证书不适合正式公开发布；公开发布应使用受信任代码签名证书或 Microsoft Store。升级包须保持相同发布者身份并提高 MSIX 版本。

## 验收记录

v0.1.0 已通过人工 UI 验收：首页和 NavigationView 页面切换、示例数据、规划指标计算、建筑面积和停车位联动、冲突提示、自动计算、显示精度、复制、清空、主题和设置持久化均正常。桌面自动化工具无法可靠触发首页入口，但人工操作确认应用导航正常，该自动化兼容性问题不阻塞发布。

## 发布清单

建议 Git 标签 `v0.1.0`。GitHub Release 可上传 MSIX、`.appinstaller`（如有）及安装说明；不得上传证书或私钥。首版 Release Notes：提供本地规划指标快速计算，数据不上传。
