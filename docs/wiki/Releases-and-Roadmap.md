# 版本与路线图

本页解释 UrbanPlanToolbox 的版本和路线图怎么读，不建立另一套独立发布记录。

> **当前状态的唯一权威来源**是 [`docs/project-status.json`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/docs/project-status.json)。历史版本变化以 [`CHANGELOG.md`](https://github.com/KiYouJyo/UrbanPlanToolbox/blob/main/CHANGELOG.md) 和 [GitHub Releases](https://github.com/KiYouJyo/UrbanPlanToolbox/releases) 为准。

## 当前稳定线

按 2026-08-21 的项目状态：

- 产品版本：**1.9.3**
- 生命周期：`stable-1.x`
- 平台：Windows
- 架构：x64
- 设计原则：offline-first
- GitHub 最新正式产品版本：1.9.3
- Microsoft Store 公布产品版本：1.9.3
- 当前项目 schema：3
- 当前备份格式：2

这些字段会继续变化，因此后续应优先查看 `project-status.json`，不要只依赖本页中的基线快照。

## 两条发行渠道

### GitHub

GitHub 正式版可以在完成发布批准与验证后较频繁发布。

当前应用内更新路径概念上为：

```text
检查更新
→ 下载 MSIXBundle
→ SHA-256 校验
→ Authenticode / MSIX 签名校验
→ Windows 包部署
→ 重启
```

### Microsoft Store

Microsoft Store 版由 Partner Center 与 Microsoft Store 客户端决定实际公开状态和版本可见性，并使用 Store 管理的更新链。

### 两个渠道不要混为一谈

- 包身份独立；
- 更新链独立；
- 不能直接跨渠道覆盖升级；
- “通过认证”不等于“已经公开发布”；
- WinGet 的 `msstore` 来源仍属于 Microsoft Store 渠道。

## 为什么更新器被标记为冻结

项目已经对 GitHub 与 Microsoft Store 更新机制进行真实发布 / 端到端验收，因此当前政策是不为了代码风格或一般重构随意改动更新器。

未来只有在以下情况下才应重新打开更新机制：

- 已证明的 bug
- 安全问题
- Windows / Microsoft Store 平台变化
- API 兼容性要求

并且需要对受影响渠道重新提供端到端回归证据。

## 1.8 → 1.9 的主要产品演进

这一阶段可以概括为三条主线：

### 桌面工作流

- v1.8.0：后台驻留、通知区域、登录后启动、灵感记录器
- v1.8.1：驻留生命周期、卸载和项目页修正
- v1.8.2：WebDAV 云归档

### 项目工作台

- v1.9.0：固定项目概览 + 12 列可自定义磁贴工作台
- 支持设计 / 研究专属磁贴和响应式布局

### 专业知识库

- v1.9.2：法规、术语、设计理念统一转向独立 Data Pack 1.0
- v1.9.3：设计理念库扩展到 109 条并补齐三语元数据和跨语言搜索

## 当前路线图阶段

项目状态把当前阶段定义为：

**`stabilization-and-productization` —— 稳定化与产品化。**

目前没有在单一事实源中指定一个固定的“下一版本号”。这意味着 Wiki 不应擅自把某个规划功能写成下一版承诺。

当前优先级包括：

1. 保持文档治理和当前状态单一来源；
2. 保护已经冻结的更新机制；
3. 统一工具页布局、状态呈现、响应式行为和可访问性；
4. 保持项目组织轻量，不把独立工具强制串成单一流程；
5. 正式化项目和备份 schema 的迁移合同；
6. 建立启动时间、包体积、依赖和原生组件预算；
7. 在这些基础上继续深化 GIS / 数据互操作与规划专用生产力工具。

## 如何判断某功能“已经实现”

建议按以下证据优先级判断：

1. 当前发布版本的真实代码和可运行行为；
2. `docs/project-status.json`；
3. 对应 Release / `CHANGELOG.md`；
4. 当前详细工程合同；
5. Wiki；
6. Roadmap、讨论或历史设计稿。

Roadmap、Figma 设计稿、Discussions 中的提议都不应自动被写成“已上线功能”。

## 相关页面

- [快速开始](Getting-Started.md)
- [贡献指南](Contributing.md)
- [专业知识库与 Data Pack](Professional-Libraries.md)