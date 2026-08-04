# Shapefile compatibility (v1.1.0)

UrbanPlanToolbox 1.1.0 uses `NetTopologySuite` 2.6.0 and `NetTopologySuite.IO.Esri.Shapefile` 1.2.0 for fully local processing. No coordinate or file is uploaded.

| Shape type | Status |
| --- | --- |
| Point, MultiPoint, PolyLine, Polygon | Supported after round-trip tests, including multipart lines and polygon rings. |
| PointZ/M, MultiPointZ/M, PolyLineZ/M, PolygonZ/M | Rejected before conversion. The current release does not claim lossless Z/M retention. |
| NullShape | Rejected before conversion to preserve DBF record correspondence. |

Only longitude/latitude datasets are supported. Projected coordinate systems are not supported. WGS 84 output receives a WGS 84 `.prj`; GCJ-02 and BD-09 output intentionally has no EPSG:4326 `.prj` and includes a non-standard coordinate-system metadata JSON file. GCJ-02 and BD-09 use public approximation algorithms and are not surveying, approval, construction, or legal transformations.
