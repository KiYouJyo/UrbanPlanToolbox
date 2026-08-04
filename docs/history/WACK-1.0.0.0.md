# Store 1.0.0.0 WACK 分析

分析日期：2026-08-02

## 报告真实性与测试对象

本分析基于本轮实际生成的 WACK XML 报告，不沿用历史 WACK 结果。报告在本地验收目录中实际存在，文件大小约 4.7 MB，生成时间为 `2026-08-02 17:58:11`。原始 XML 不提交仓库，继续保留在本地验收目录。

- WACK 版本：`10.0.26100.8249`
- 验证方式：命令行
- 工具架构：x64
- `PARTIAL_RUN`：`FALSE`
- 报告总体结果：`PASS`
- 测试系统：Windows 11，OS build `10.0.26200.0`
- 测试包：Store x64 bundle，版本 `1.0.0.0`
- PackageFullName：`JoKiy.UrbanPlanToolbox_1.0.0.0_x64__4wdwgytaw3v2m`
- PackageFamilyName：`JoKiy.UrbanPlanToolbox_4wdwgytaw3v2m`
- Identity Name：`JoKiy.UrbanPlanToolbox`
- Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`

报告包含完整的测试结束信息、包清单和资源包信息，没有发现权限不足、测试中断或 XML 损坏迹象。

## 统计

报告共包含 24 个测试节点：

| 状态 | 数量 | 说明 |
| --- | ---: | --- |
| PASS | 23 | 包合规性、清单、资源、能力、元数据、DPI 等测试通过 |
| WARNING | 0 | 无独立 WARNING 状态 |
| FAIL | 1 | 仅一个可选的包健全性扫描 |
| NOT APPLICABLE | 0 | 无 |
| 其他 | 0 | 无 |

总体结果仍为 WACK `PASS`。唯一 FAIL 的测试标记为 `OPTIONAL=TRUE`。

## 非 PASS 项

### 已阻止的可执行文件

- 测试名称：已阻止的可执行文件
- 测试索引：88
- 所属项：程序包健全性测试
- 状态：`FAIL`
- 是否可选：是
- 涉及文件/API：
  - `UrbanPlanToolbox.exe`：`shell32.dll!ShellExecuteW`
  - `System.Diagnostics.Process.dll`：`shell32.dll!ShellExecuteExW`
  - `coreclr.dll`、`mscordbi.dll`：`kernel32.dll!CreateProcessW`
  - `Microsoft.WindowsAppRuntime.Bootstrap.dll`：`shell32.dll!ShellExecuteExW`
  - 其他 .NET、Windows App SDK、DirectML、ONNX Runtime 和资源文件包含被扫描器识别为 `cmd`、`reg`、`MSBuild` 等不可执行引用字符串

原始扫描摘要是“包含对启动流程相关 API 的引用”或“包含不可执行引用”。这不是应用崩溃、非法 API、清单错误或包签名失败报告。

从当前源代码看，应用使用 Windows `Launcher.LaunchUriAsync`/`LaunchFolderAsync` 响应用户点击打开仓库、Issue、法规、Store 页面和用户选择的文件夹；没有发现直接调用 `System.Diagnostics.Process`、`CreateProcess` 或 `ShellExecute` 的应用代码。WACK 对托管运行时、Windows App SDK 和依赖库进行静态引用/字符串扫描时，将这些系统启动能力列为命中项是可解释的。

判断：

- 是否属于应用自身问题：没有证据表明存在违规的后台进程启动；`UrbanPlanToolbox.exe` 的命中与用户主动外部链接启动路径相关，属于可解释的应用入口命中。
- 是否属于 Microsoft Store 硬阻断：本报告没有显示应用专属硬阻断。该测试为可选项，且 WACK 总体结果为 `PASS`；其余包健全性、清单、能力和平台文件测试均通过。
- 建议：本轮不为消除该可选静态扫描误报而修改代码或移除已批准的用户主动外部链接能力。提交 Partner Center 时保留该 WACK 结果和上述用途说明；若 Partner Center 后续针对该项提出具体认证意见，再按具体意见进行最小修复和重新 WACK。

## 重点区域结论

- Package compliance：通过，包括应用清单、企业功能、资源包、注册表检查和文件关联相关检查。
- Supported API / Windows Runtime metadata：通过；ExclusiveTo、类型位置、大小写、类型名称、属性和元数据正确性均通过。
- Security：私有代码签名和禁止文件分析器通过。
- Manifest / capabilities：应用清单、特殊用途功能和品牌检查通过；`runFullTrust` 相关特殊用途能力未产生失败结果。
- Performance / Launch / Crash：报告中没有这些类别的失败或中断；WACK 已完整结束。
- DPI：`DPIAwarenessValidation` 通过。
- Debug/test files：调试配置测试通过；没有因 Debug 构建或测试文件导致的失败。
- Package structure / blocking-file scan：资源包、平台文件和存档文件使用通过；唯一非 PASS 是上面的可选“已阻止的可执行文件”静态扫描。

## 结论

结论：**A. WACK 方面可以继续合并准备**。

本次 WACK 已完整执行，没有发现应用专属硬阻断。唯一 FAIL 是明确标记为可选的静态扫描项，命中系统运行库、Windows App SDK 依赖和用户主动外部链接启动所需的系统 API；它不应被改写成 PASS，但也没有证据表明需要本轮修改源码。

这是首次 Store 发布前的历史验收记录。当前发布状态和可复用流程请参阅 [Microsoft Store 发布指南](../STORE-PUBLISHING.md)；本文件不代表当前待提交或阻断状态。
