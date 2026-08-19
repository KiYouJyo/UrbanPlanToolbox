简体中文 | [日本語](RELEASE-NOTES-v1.9.2.ja.md) | [English](RELEASE-NOTES-v1.9.2.en.md)

# UrbanPlanToolbox v1.9.2 专业资料库与 Data Pack 1.0

- 按新版 Figma 设计重构建筑与规划法规索引、中日英规划术语库和设计理念词典，统一为检索列表、详情面板和独立数据源卡片。
- 三套资料库正式接入 UrbanPlanToolbox_Data，不再把主仓库旧数据文件作为新版页面运行时数据源；数据版本与应用版本独立演进。
- 新增 DataPackResolver、DataPackCatalogService 与 DataPackInstaller：支持官方 Catalog 检查、GitHub Release 下载、本地 .uptdata 导入和上一版本回退。
- Data Pack 1.0 在激活前校验 Pack ID、Schema、最低应用版本、路径安全、文件大小、包级与载荷级 SHA-256，并拒绝未声明文件和路径穿越。
- 首批 2026.08.1 数据面向 221 条法规索引、140 个中日英规划术语及 18 条设计理念；页面按实际数据包内容动态生成筛选项、计数与来源信息。
- 数据更新由用户主动触发；网络不可用时已安装数据包仍可离线使用，本地旧版本保留用于回退。
