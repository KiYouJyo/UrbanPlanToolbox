[简体中文](RELEASE-NOTES-v1.9.2.md) | [日本語](RELEASE-NOTES-v1.9.2.ja.md) | English

# UrbanPlanToolbox v1.9.2 Professional libraries and Data Pack 1.0

- Rebuilds the architecture/planning regulations index, Chinese-Japanese-English planning terminology, and design concepts dictionary to match the new Figma layouts with unified search lists, detail panes, and dedicated data-source cards.
- Connects all three libraries to UrbanPlanToolbox_Data. The redesigned pages no longer use the legacy data files in the app repository as their runtime source, and data versions can evolve independently of app releases.
- Adds DataPackResolver, DataPackCatalogService, and DataPackInstaller with official catalog checks, GitHub Release downloads, local .uptdata import, and rollback to the previous installed version.
- Before activation, Data Pack 1.0 validates pack ID, schema, minimum app version, path safety, file sizes, package and payload SHA-256 hashes, and rejects undeclared files or path traversal.
- The initial 2026.08.1 set targets 221 regulation entries, 140 trilingual planning terms, and 18 design concepts; filters, counts, and provenance are generated from the active pack.
- Data updates are explicitly user-triggered; installed packs remain available offline when the network is unavailable, and prior local versions are retained for rollback.
