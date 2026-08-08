# Third-party notices / 第三方声明 / 第三者告知

Version 1.4.2 · Last updated 2026-08-07

## Application dependencies

The project references the following packages in `UrbanPlanToolbox.csproj`:

- `Microsoft.WindowsAppSDK` `2.3.1` — Microsoft software; see the package license and notices distributed with the SDK.
- `Microsoft.Windows.SDK.BuildTools` `10.0.28000.2526` — Microsoft software; see the package license and notices distributed with the package.
- `Microsoft.Windows.SDK.BuildTools.WinApp` `0.5.0` — Microsoft software; see the package license and notices distributed with the package.
- `NetTopologySuite` `2.6.0` — BSD 3-Clause License.
- `NetTopologySuite.IO.Esri.Shapefile` `1.2.0` — BSD 3-Clause License; forward-only Esri Shapefile readers and writers.
- `MetadataExtractor` `2.9.0` — Apache License 2.0; local EXIF/GPS metadata reading.
- `XmpCore` `6.1.10.1` — transitive metadata dependency; see its package license notice at <https://www.adobe.com/devnet/xmp/library/eula-xmp-library-java.html>.
- `OpenCvSharp4` `4.11.0.20250506` and `OpenCvSharp4.runtime.win` `4.11.0.20250506` — Apache License 2.0; local feature matching, geometric registration, ECC refinement, morphology, and connected-components analysis. The packaged native runtime is Windows x64 only.

Both packages are used only for local geometry and Shapefile I/O. The application does not use an online coordinate conversion service and does not upload Shapefile data.

应用使用 Windows App SDK 和 Windows API 提供 WinUI、文件选择器、默认浏览器和本地通知能力。Windows、Microsoft Store、政府网站和索引中的官方数据门户不是本项目的合作方或赞助方。法规索引是研究辅助工具，每条记录的官方来源仍是权威来源。

仓库没有捆绑外部字体、第三方图标、示例数据或付费服务；图标和界面资源来自仓库内的 `Assets`。项目本身当前未声明许可证，本文件不替用户选择许可证。
