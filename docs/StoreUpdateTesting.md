# Microsoft Store 应用内更新测试

本地开发使用与正式服务相同的 `IAppUpdateService` 接口。仅 Debug 构建可通过环境变量 `URBANPLANTOOLBOX_FAKE_UPDATE_SCENARIO` 选择 `UpToDate`、`UpdateAvailable`、`Cancelled`、`NetworkError`、`StoreUnavailable`、`DownloadFailed`、`InstallFailed`、`UnsupportedChannel` 或 `InstallWillCloseApp`。Release 构建忽略该变量。

真实 Microsoft Store 更新需要已经由 Store 安装、并具有 Store 包身份的旧版本。将更高的包版本上传至 Partner Center Package Flight，把测试 Microsoft 帐户加入 flight，等待该帐户接收更新。为避免 Store 自动更新抢先完成测试，请在隔离的测试设备或虚拟机中关闭自动更新，并从旧版本的“关于”页依次执行检查、下载和安装。

通过 Package Flight 分别测试无更新、可用更新、下载/安装进度、用户取消、网络中断和失败后的重试。建议使用可还原快照的虚拟机重复这些场景；安装可能关闭应用。

GitHub 侧载包、自签名测试包和 Store 包的 Identity 与 Publisher 不同，不能彼此覆盖。侧载包不会调用 Store 更新 API，而是显示不支持提示并只提供 GitHub Releases 备用入口。
