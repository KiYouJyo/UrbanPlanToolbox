# 通用交互组件合同

`AppDialogService` serializes `ContentDialog` presentation against the current `XamlRoot`. `AppNotificationService` presents host-window success, warning, and error notifications without user payload, tokens, certificates, or private keys. `AsyncOperationRunner` prevents duplicate work for a stable operation key and retains `Idle`, `Running`, `Succeeded`, `Failed`, and `Canceled` states. `UnsavedChangesGuard` requires a real save, discard, or cancel decision before leaving modified content.

Pages delegate generic confirmation, notification, and operation state to these services while retaining domain validation in their respective pages or services. New visible text must be present in all three RESW catalogs and interactive controls require accessible names.

## Tool Page state target

`Idle`, `Loading`, `Success`, `Warning`, `Error`, `Empty`, and `Disabled` are the target design contract for tool-page state presentation. This is planned standardization, not a claim that every existing page already uses one implementation.
