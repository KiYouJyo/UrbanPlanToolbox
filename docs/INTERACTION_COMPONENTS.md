简体中文 | [日本語](INTERACTION_COMPONENTS.ja.md) | [English](INTERACTION_COMPONENTS.en.md)

# 通用交互组件合同

## Transient Surface 合同

UrbanPlanToolbox 的应用内 ContentDialog、ComboBox 下拉层及 Flyout 不维护独立的 Light/Dark 色板。这些 transient surface 复用应用共享的主题 Surface、边框、文字与交互状态资源；业务页面只提供内容和交互，不自行定义弹窗颜色。系统拥有的 Picker、UAC、Store 与权限界面不属于此合同。

临时浮层只定制主题 Surface，不替换 WinUI 默认控件模板或几何资源。ContentDialog 主体必须保持不透明，模态遮罩仅作用于弹窗外部区域；ComboBox 下拉层使用应用级 transient Surface，同时保留 WinUI 默认圆角、动画、选中和键盘行为。

ComboBox 展开层通过 WinUI 原生 `ComboBoxDropDownBackground` 主题资源接入应用共享 transient Surface；应用保留 WinUI 默认 ComboBox 模板、圆角、动画及选项交互状态。

`AppDialogService` serializes `ContentDialog` presentation against the current `XamlRoot`. `AppNotificationService` presents host-window success, warning, and error notifications without user payload, tokens, certificates, or private keys. `AsyncOperationRunner` prevents duplicate work for a stable operation key and retains `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` states. `UnsavedChangesGuard` requires a real save, discard, or cancel decision before leaving modified content.

Pages delegate generic confirmation, notification, and operation state to these services while retaining domain validation in their respective pages or services. New visible text must be present in all three RESW catalogs and interactive controls require accessible names.

## Tool Page state target

`Idle`, `Loading`, `Success`, `Warning`, `Error`, `Empty`, and `Disabled` are the target design contract for tool-page state presentation. This is planned standardization, not a claim that every existing page already uses one implementation.
