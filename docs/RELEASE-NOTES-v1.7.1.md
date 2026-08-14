简体中文 | [日本語](RELEASE-NOTES-v1.7.1.ja.md) | [English](RELEASE-NOTES-v1.7.1.en.md)

# UrbanPlanToolbox v1.7.1 Store 更新流程修复

- 修复 Microsoft Store 更新下载完成前错误出现“重启并更新”按钮的问题，并改为真正的先下载、后安装两阶段流程。
- 在用户点击“重启并更新”前，不再进入 Store 包部署流程。
- 修复将 Store package progress 的 Completed 错误视为整个更新流程完成的问题，并完善多包和异步回调的状态机回归测试。
- 保持 GitHub 更新流程及应用级更新会话行为不变。
