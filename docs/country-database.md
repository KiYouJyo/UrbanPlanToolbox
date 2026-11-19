# Country database

The primary navigation now includes Database immediately below Search. The country directory uses the existing Fluent card style and horizontal Asia / Europe / Americas / Africa / Oceania categories. Japan is implemented; other regions show an empty state. Add future countries through the directory model and a country-specific data adapter rather than reusing Japanese administrative rules.

Japan uses an app-local WebView2 page and bundled N03-2026 simplified polygons. GSI basemap tiles remain online. Tile failures do not block polygon loading. Source geometry and vendor assets are pinned in `packaging/database-assets.json`; `Prepare-DatabaseAssets.ps1` verifies existing files and restores missing assets before an app build. Source revisions cannot silently change the bundled data. Initial app builds require network access to these sources.

## Data workflow

Import CSV, XLS or XLSX, select a worksheet, code column and one or more indicator columns, then provide a title, statistical date, source, unit and definition. Preview separates matched, unknown/malformed and duplicate codes. A duplicate blocks saving; unmatched rows are retained with the original table and can be downloaded in a match report. Five-digit municipal codes and checksum-validated six-digit codes are supported; leading zeros and prefecture codes are normalized. Designated-city parent codes use an explicit 20-city mapping. Name-only joins, boundary-year conversion and automatic aggregation are intentionally not implemented. Suppressed/blank values remain distinct from zero.

Datasets are atomically stored in `%LOCALAPPDATA%/UrbanPlanToolbox/data/databases/JP/datasets.json` independently of the browser cache, inside a versioned JSON envelope. The Database JSON backup button exports the full library; importing that JSON restores it after explicit confirmation. This database is not included in the existing general toolbox backup format. Each dataset retains source rows, matched rows, fields, statistical date, definition, unit, source and import time. Native persistence acknowledgements are required before reporting success. The browser-only QA fallback uses localStorage and is not the desktop persistence mechanism.

Map clicks and search results add/remove areas in multi-select mode. Single-select mode replaces the selection. The table filters the current administrative level by name/code and optional numeric bounds of the selected metric, with checkboxes and select-filtered-results. Missing values are excluded by numeric bounds. Thematic fills do not aggregate different levels or years.

Export formats:

- GeoJSON: one FeatureCollection containing every selected geometry and all linked datasets' fields and provenance.
- CSV: one row per selected area and all linked fields; preserves leading-zero code strings in the file (spreadsheet software may require importing that column as text).
- Shapefile ZIP: `.shp`, `.shx`, UTF-8 `.dbf`, `.cpg`, WGS84 `.prj`, field-name mapping, full attribute CSV and source notes. DBF fields use ASCII IDs because of its 10-byte limit; strings are capped at 254 UTF-8 bytes. Full names/values are retained in the accompanying files. No geometric dissolve occurs.

The native bridge accepts messages only from the bundled HTTPS virtual origin. It uses native file save dialogs and serialized operations; page-supplied filesystem paths are never accepted.

## Validation

Run `node --test tests/database-core.test.mjs` for code matching, missing values and joining. Windows CI builds the WinUI shell and runs the native Shapefile round-trip test (multiple polygons, a hole, leading-zero codes and Japanese attributes). Linux can run the focused export/navigation tests with .NET 10 and EnableWindowsTargeting but cannot validate the WinUI window or MSIX installation.
