# 通用交互组件

v0.3.10 将窗口级交互收敛为小型、可测试的服务，而不替换已验收的项目业务规则。

- `AppDialogService` 以当前 `XamlRoot` 串行显示 `ContentDialog`；不可用或已关闭的根安全返回，不允许多个对话框并发。
- `AppNotificationService` 向主窗口通知宿主发布信息、成功、警告和错误；相同内容在短窗口内去重。错误通知默认保留，通知不得含项目正文、授权 token、证书或私钥。
- `AsyncOperationRunner` 对单个稳定操作键拒绝重复执行，并保留 Idle、Running、Succeeded、Failed、Canceled 状态；不同键互不阻塞。
- `UnsavedChangesGuard` 只在页面确有修改时请求“保存并继续、放弃、取消”，并且保存失败时不允许离开。

页面只把通用确认、通知和执行状态交给这些服务。项目字段、导入格式、删除名称校验及业务验证仍在各自页面或服务中。新增可见文字必须同步三套 RESW，并为交互控件提供可访问名称。
