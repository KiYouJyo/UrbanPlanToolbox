# Project schema fixtures

These files are byte-stable, minimal persisted project envelopes used to prove
the migration contract.  Provenance is kept here rather than inside the JSON so
the fixtures retain the shapes that the application serialized.

| Schema | Source commit | Public history | Notes |
| --- | --- | --- | --- |
| v1 | `83eb240de2f436bf631c41094a6543ad31dcd452` | Released in the v0.3.x line | Initial project workspace envelope; project details were top-level fields. |
| v2 | `467417b9cea32996084480a99c96df7240ca6808` | Released in the v0.4.0 line | Added `planningRequirements` and `milestones`; existing project fields remained top-level. |
| v3 | `8d74e4d19713bca5ca3266bff2e0ce5cd07366b4` | Released in the v0.5.0 line | Added `kind`, `designDetails`, and `researchDetails`. |

The unversioned project reader is a compatibility path for early local files;
it is not represented as a separate published schema.  Backup containers have
their own lifecycle and are currently format v2.
