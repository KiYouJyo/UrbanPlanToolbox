简体中文 | [日本語](FirstRunGuide.ja.md) | [English](FirstRunGuide.en.md)

# 首次启动向导架构

`project-status.json` 是 UrbanPlanToolbox 当前产品状态的 SSOT。首次使用向导生命周期状态位于 package-scoped LocalState 的 `first-run-guide.json`，而不是外置业务数据目录。卸载重装或 Windows Reset 清除该 package state 后，向导会在下次启动时再次显示；保留的项目、设置、附件、备份、缓存和日志不会被视为已经完成向导的证据，也不会被删除。

状态 schema 2 只以有效的 `FirstRunGuideState` 判断自动显示。真实完成的 schema 1 `Completed` 记录迁移并保留；旧 `ExistingUserMigrated` synthetic completion 会迁移为 `Pending`，因此显示一次向导。损坏或不受支持的状态 fail safe 为显示向导，且未来 schema 不会被覆盖。Escape 关闭不完成；Skip 与最后一步 Start 保存完成；设置页手动打开始终可用且不重置完成状态。

### 视觉 Surface 合同

首次使用向导不维护独立的 Light/Dark 背景颜色。外层背景复用主应用导航边栏的主题 Surface；中央 GuideCard 复用应用普通卡片的主题 Surface。因此 Light、Dark、System 与 High Contrast 均由应用现有主题资源统一控制。First Run 不得复制或硬编码另一套导航背景或卡片颜色。

The first-run guide is a reusable in-window host, not a second window or a native splash-screen replacement. `MainWindow` creates one `FirstRunGuideHost` over the existing shell. Closing the host leaves the existing page, navigation selection, title bar, backdrop, and activation flow intact.

`FirstRunExperienceService` stores only machine-local lifecycle state in `first-run-guide.json` under the packaged `ApplicationData.Current.LocalFolder`. This package-scoped location is cleared by a standard uninstall and is separate from `settings.json`, project data, backups, imports, exports, and restore-defaults operations. Unpackaged development and injected tests use the existing application-data root. `CurrentFirstRunGuideVersion` is currently `1`; later guide revisions can increment this version without coupling the guide to the product version.

On the first state read, an existing settings file, data directory, or managed attachments directory identifies an installation that predates the guide, so that installation is marked complete and is not forced through onboarding. An empty app-data root remains eligible for automatic onboarding. State read or write failures are non-fatal; the shell can still start and a failed completion remains retryable.

The lifecycle state records `Unknown`, `NewInstallation`, `ExistingUserMigrated`, `Pending`, or `Completed`. The guide is shown automatically after the first frame, infrastructure initialization, and reminder refresh only for a new installation with no valid guide state. Existing v1.3.0-or-earlier settings/data are migrated as completed once, so an upgrade is not forced through onboarding. Completion and skip both persist the current guide version. Closing the app or pressing Escape does not persist completion, so an interrupted automatic guide is offered again. Manual reopening from the Application settings card uses the same MainWindow-level host and never resets lifecycle state.

The host is an opaque, hit-testable overlay sibling of `RootFrame`. It blocks page scrolling and pointer input while leaving the title-bar/system-button area owned by the window. All four steps share one responsive outer card size; only the body scrolls, while the header and footer remain visible. Language changes refresh the current step in place without recreating the host, changing its size from content, or opening a second instance. Focus is moved into the guide and restored to the opening control when it closes.

## Clean-install validation boundary

The installer intentionally preserves the package-external user-data root at `%LocalAppData%\UrbanPlanToolbox`; uninstalling the MSIX therefore does not prove that all application data is gone. The uninstall script also writes an uninstall log there. A true clean-install test must use an isolated data root or explicitly verify that no `settings.json`, `first-run-guide.json`, `data`, or `attachments` history remains. Run `packaging\Test-CleanInstallPrerequisites.ps1` after uninstall and before installation. Do not restore the LocalState or package-external data backup in this test. Restore data only in the separate upgrade-compatibility test, where suppressing automatic onboarding is expected.

At launch, `App` calls `FirstRunExperienceService.PrepareForLaunch()` before `SettingsService.Load()` and before other initialization. This snapshots the pre-existing legacy-data state; a default file created later in the same launch cannot turn a new installation into an existing user. The App, MainWindow, and guide host use the same singleton service. Missing package-local state is migrated once: existing historical data becomes `Completed`, while an empty installation becomes `NewInstallation` and remains eligible for automatic onboarding.

The three resource catalogs contain the same guide key set. Dynamic text is resolved through `LocalizationService`, so reopening after a language switch uses the active language. Privacy is opened through the packaged `PRIVACY.md` document; the guide adds no networking, telemetry, project-data examples, or consent state.
