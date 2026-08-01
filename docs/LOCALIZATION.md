# 本地化说明（v0.3.7 三语基础）

## 支持语言

- 简体中文：`zh-CN`
- 日语：`ja-JP`
- 英语：`en-US`

## 默认与回退语言

- 默认语言为 `zh-CN`，也是最终回退语言。
- 跟随系统时，系统语言不受支持会回退到 `zh-CN`。
- 未知或损坏的语言设置安全回退为“跟随系统”（内部值 `system`）。

## 资源目录结构

所有界面文案位于 MRT Core 资源目录，每种语言一个 `Resources.resw`：

```text
Strings/
  zh-CN/Resources.resw
  ja-JP/Resources.resw
  en-US/Resources.resw
```

三套资源必须拥有完全相同的键集合；`Package.appxmanifest` 同时声明三种支持语言，默认语言为 `zh-CN`。

## 资源键命名规则

- 使用稳定英文命名，不使用中文或日文显示文字作为标识。
- 按语义分段命名，例如 `Navigation_Search`、`Tool_PlanningIndicator_Name`、`Category_MasterPlanning`、`Error_InvalidNumber`。
- 同一语义只保留一个资源键：XAML 与 C# 共用同一个键。
- XAML 的 `x:Uid` 资源使用规范的属性后缀（如 `Setting_Theme.Header`、`Home_Title.Text`、`Action_Calculate.Content`）。
- 资源键不能同时是叶子值和范围：如果一个名称已有 `.属性` 子键，则不能再存在同名裸键（MRT Core PRI 约束）。
- 格式字符串使用 `{0}`、`{1}` 占位符；三种语言的占位符编号和数量必须一致。
- 在 `comment` 列记录必要的上下文。
- 不把颜色、尺寸和业务常量放入字符串资源。

## XAML 静态文字

- 静态界面文字优先使用 `x:Uid`，键名即语义名，资源键为 `<Uid>.<属性>`。
- 页面标题、输入框标题等同时被 C# 使用的文字，在代码中通过本地化服务设置，保持单一资源键。

## C# 动态文字

- 通过 `UrbanPlanToolbox.Services.ILocalizationService` 读取：
  - `GetString(resourceKey)`：返回当前语言字符串；
  - `GetFormattedString(resourceKey, arguments)`：填充占位符。
- 生产环境使用 `LocalizationService.Default`（基于 Windows App SDK 的 MRT Core `ResourceLoader`）。
- 禁止在页面中自行创建 `ResourceLoader` 或重复实现辅助方法；禁止引入第三方本地化框架或在线翻译。
- 未知资源键安全返回 `!键名!` 占位，不会导致崩溃。

## 如何新增资源键

1. 在三种 `Resources.resw` 中同时添加同名的 `<data name="...">` 条目。
2. 如果包含占位符，保持三种语言占位符一致。
3. 如键被 XAML `x:Uid` 使用，命名为 `<Uid>.<属性>`；如键被 C# 读取，使用裸语义名。
4. 运行本地化测试确认键集合一致且无空值。

## 如何新增语言

1. 新建 `Strings/<BCP-47>/Resources.resw`，复制现有键集合并翻译。
2. 在 `Package.appxmanifest` 的 `<Resources>` 中声明新语言。
3. 在 `LanguagePreference.SupportedBcp47Languages` 中加入新语言标签。
4. 在设置页语言列表中新增选项（显示名称与内部 BCP-47 值分离）。
5. 运行全部测试并补充该语言的解析与搜索测试。

## 语言设置与重启生效机制

- 设置页提供：跟随系统（`system`）、简体中文（`zh-CN`）、日本語（`ja-JP`）、English（`en-US`）。
- 语言偏好保存在现有设置文件 `%LOCALAPPDATA%\UrbanPlanToolbox\settings.json` 的 `Language` 字段。
- 选择具体语言时保存对应 BCP-47 标签；选择“跟随系统”时保存 `system`（清除覆盖）。
- 应用启动时，`App.OnLaunched` 在创建 `MainWindow` 和加载本地化资源之前调用
  `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride` 应用语言覆盖。
- 修改语言后只显示“下次启动时生效”的提示，不自动重启、不实现整棵可视化树的实时刷新；同一语言重复选择不重复提示。

## ToolDefinition、分类与搜索如何使用资源键

- `ToolDefinition` 保存稳定的工具 ID、`NameResourceKey`、`DescriptionResourceKey` 和 `SearchKeywordsResourceKey`（关键词为每行一个）。
- `ToolCategoryDefinition` 保存稳定的分类 ID 与 `NameResourceKey`；`PrimaryNavigationDefinition` 保存稳定的导航 ID 与 `NameResourceKey`。
- UI 边界通过 `ILocalizationService` 解析显示名称和说明；收藏与设置只保存稳定 ID，不写入已解析文字。
- `ToolSearchService` 使用当前语言解析名称、说明和关键词建立搜索匹配；用户输入不会被翻译，搜索结果仍通过稳定工具 ID 打开。
- 分组标题使用稳定的拼音首字母元数据；“已收藏”分组标题通过 `Search_FavoritesHeader` 本地化。

## 不翻译用户数据的原则

- 收藏数据、设置、用户输入和项目资料只保存稳定 ID 或原始内容，不因语言切换而翻译或改写。
- 品牌名（UrbanPlanToolbox）、URL、工具 ID、分类 ID、路由 ID、文件名、版本号、单位符号（m、m²、ha、% 等）和程序代码不翻译。

## 三语术语表（v0.3.7 基线）

### 一级导航

| 中文 | 日本語 | English |
| --- | --- | --- |
| 欢迎页面 | ホーム | Home |
| 常用功能（预留） | お気に入り | Favorites |
| 搜索 | 検索 | Search |
| 设计工具 | 設計ツール | Design Tools |
| 科研工具 | 研究ツール | Research Tools |
| 项目归档 | プロジェクトアーカイブ | Project Archive |
| 关于 | このアプリについて | About |
| 设置 | 設定 | Settings |

### 设计分类

| 中文 | 日本語 | English |
| --- | --- | --- |
| 前期分析 | 事前分析 | Preliminary Analysis |
| 实地调研 | 現地調査 | Field Research |
| 方案推导 | コンセプト検討 | Concept Development |
| 总体设计 | 全体計画 | Master Planning |
| 详细设计 | 詳細設計 | Detailed Design |

### 科研分类

| 中文 | 日本語 | English |
| --- | --- | --- |
| 前期工具 | 研究準備 | Research Preparation |
| 地理工具 | 地理ツール | Geographic Tools |
| 数据工具 | データツール | Data Tools |

### 工具

| 中文 | 日本語 | English |
| --- | --- | --- |
| 规划指标快速计算器 | 計画指標計算 | Planning Metrics Calculator |
| 单位与比例尺换算器 | 単位・縮尺変換 | Unit & Scale Converter |

### 常用操作与状态

| 中文 | 日本語 | English |
| --- | --- | --- |
| 打开工具 | ツールを開く | Open Tool |
| 添加到常用功能 | お気に入りに追加 | Add to Favorites |
| 从常用功能移除 | お気に入りから削除 | Remove from Favorites |
| 检查更新 | 更新を確認 | Check for Updates |
| 复制结果 | 結果をコピー | Copy Results |
| 重置 | リセット | Reset |
| 自动计算 | 自動計算 | Auto-calculate |
| 显示精度 | 表示精度 | Display Precision |
| 主题 | テーマ | Theme |
| 语言 | 言語 | Language |
| 跟随系统 | システム設定に従う | Use System Setting |
| 预览版 | プレビュー版 | Preview |
| 尚未收藏工具 | お気に入りのツールはありません | No favorite tools yet |
| 当前分类暂无已上线工具 | このカテゴリには利用可能なツールがまだありません | No tools are available in this category yet |
| 未找到匹配的工具 | 一致するツールが見つかりません | No matching tools found |
| 项目归档功能仍在规划中 | プロジェクトアーカイブは準備中です | Project Archive is planned for a future update |
| 语言将在下次启动时生效。 | 言語の変更は次回起動時に反映されます。 | The language change will take effect the next time the app starts. |

### v0.3.9 项目与数据管理

| 中文 | 日本語 | English |
| --- | --- | --- |
| 项目主页 | プロジェクトホーム | Project Home |
| 项目工作台 | プロジェクトワークスペース | Project Workspace |
| 设计项目 | 設計プロジェクト | Design Projects |
| 研究项目 | 研究プロジェクト | Research Projects |
| 研究领域 | 研究分野 | Research Field |
| 研究对象 | 研究対象 | Research Subject |
| 研究方法 | 研究方法 | Research Methods |
| 待办事项 | タスク | Todos |
| 规划指标快照 | 計画指標スナップショット | Planning Snapshot |
| 工作文件夹 | 作業フォルダー | Work Folder |
| 归档项目 | プロジェクトをアーカイブ | Archive Project |
| 恢复项目 | プロジェクトを復元 | Restore Project |
| 数据管理 | データ管理 | Data Management |
| 导出数据 | データをエクスポート | Export Data |
| 导入数据 | データをインポート | Import Data |

原“项目归档功能仍在规划中”属于 v0.3.7 历史基线；v0.3.9 页面已实现，实际界面使用新的归档与恢复资源键。

## 人工审校流程

- 本基线译法是开发基线，不代表用户已完成最终语言审核。
- 日语是否完全自然、英语是否达到母语软件表达、专业规划术语、文字长度与视觉层级均由用户人工审校后最终确认。
- 如某条译文与实际功能明显不符，应保持全局一致并记录在案，等待用户审校，不应自行大范围改写术语。
## MSIX package language verification

The source manifest uses `<Resource Language="x-generate" />`. The Windows SDK expands it from the qualified `Strings/zh-CN`, `Strings/ja-JP`, and `Strings/en-US` RESW files when it creates the final package manifest. Source RESW presence alone is not release evidence: before a local signed package is installed, run `packaging/Test-PackagedLanguageResources.ps1` against the actual MSIX. It verifies both the packaged `AppxManifest.xml` language declarations and `resources.pri` candidates.
