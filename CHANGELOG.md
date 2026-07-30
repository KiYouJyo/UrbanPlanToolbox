# 更改日志

## 0.1.1

### Changed

- 增加双击 CMD 安装与卸载入口，并自动请求管理员权限。
- 将 MSIX、CER 和 Windows App Runtime 依赖隐藏在 `payload` 目录，降低直接双击 MSIX 的误操作风险。
- 安装前检测兼容的 Windows App Runtime，避免重复安装共享运行库。
- 安装失败时仅回滚本次导入的准确测试证书。
- 修复 x64 平台默认 Runtime Identifier 映射，确保 `Platform=x64` 默认生成 `win-x64`。

### Validation

- 人工验收通过：双击 CMD 安装/卸载、UAC 提升、已有兼容 Runtime 时仅安装主 MSIX、包身份启动、基础页面功能和准确测试证书清理。
- 尚未验证：UAC 取消、缺少兼容 Runtime 时的依赖回退，以及干净虚拟机或 Windows Sandbox 安装。

## 0.1.0

### Added

- 创建 WinUI 3 城市规划辅助工具箱基础框架。
- 增加规划指标快速计算器及建筑面积联动。
- 支持容积率、建筑密度、绿地率、人口/户均、停车位和公共服务设施配比。
- 支持主题设置、复制结果和单项目 MSIX 配置。
- 完成人工 UI 验收；桌面自动化首页入口兼容性问题不阻塞发布。
