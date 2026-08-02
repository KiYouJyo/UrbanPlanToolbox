# New tool development template

Each production tool must be implemented through this checklist; do not add an unreferenced sample tool to the build.

1. Assign an immutable lowercase stable tool ID and select one or more category placements. Keep the legacy primary/secondary fields compatible with existing tools.
2. Add the definition to `ToolRegistry`, route it through `ToolNavigation`, and provide a real page type.
3. Add Chinese, Japanese, and English name, description, and search-keyword resources.
4. Verify every category placement, the search index, and `FavoriteToolsService` operate on the one stable ID; search must show a multi-placement tool only once.
5. Keep page interaction separate from a business/storage service.
6. Give the tool an independent `ToolSchemaVersion`; store JSON only under `data/tools/<stable-tool-id>` through `IAppDataPathProvider` and `JsonDataStorage`.
7. Store binary copies only under `attachments/tools/<stable-tool-id>` with safe relative references; never retain source paths or alter source files.
8. Reuse atomic save, last-valid recovery, future-version refusal, and migration infrastructure.
9. Include data and managed attachments in the validated `.uptbackup` export/import flow.
10. Make layouts responsive, localizable, keyboard-accessible, and usable in light, dark, and high-contrast themes.
11. Add contract tests for ID uniqueness, categories, page routing, all three resource sets, search, favorites, path safety, data round-trip, recovery, future-schema refusal, and backup contents.
12. Validate Debug/Release x64, a signed non-development MSIX, and manual UI/data-loop acceptance before release.

`color-palette-recorder` is the first real implementation of this template. `workflow-review-checklist` is the second real sample and validates a pure structured-data tool with multiple placements. `architecture-planning-regulations-index` is the third sample and validates a read-only packaged catalog with development-time import and official-link boundaries. Both schemas are intentionally separate from `ProjectSchemaVersion`; the portable container remains `BackupFormatVersion = 1`.

`design-concept-dictionary` is the fourth sample: it validates an offline editable dictionary with independent schema version 1, deep-copy editing/duplication, generic editable tag lists, and backup validation without attachments.
