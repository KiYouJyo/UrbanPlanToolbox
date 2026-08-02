# Regulations source workbook

`中日美欧建筑规划法规索引.xlsx` is the approved development-time source for the v0.4.2 catalog. The authoritative worksheet is `法规总表`; `官方入口` is imported separately, while country worksheets and `总览` are verification views only.

The app does not read Excel, Office, Python, or an online database. Run `tools/data-import/Convert-RegulationsWorkbook.py` with the bundled Python runtime to regenerate `Assets/Data/RegulationsIndex/regulations-index.v1.json`. The workbook is excluded from the MSIX and release archives.
