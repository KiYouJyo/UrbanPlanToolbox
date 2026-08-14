简体中文 | [日本語](ROADMAP.ja.md) | [English](ROADMAP.en.md)

# UrbanPlanToolbox 路线图

## Documentation

Current product and channel facts are defined in [project-status.json](project-status.json). Historical changes belong in [CHANGELOG.md](../CHANGELOG.md) and the versioned release notes. This roadmap describes future direction only; it does not assign unapproved work a release number or date.

## Current product stage

UrbanPlanToolbox is in the **1.x stabilization-and-productization** stage. It has grown from a planning calculator into an offline-first professional Windows toolbox spanning project management, planning calculations, design assistance, regulations and terminology, GIS and spatial data, survey photos, drawing comparison, search and favorites, three languages, and two distribution channels.

The central challenge is no longer the number of tools, but making their work flow together. The direction is a lightweight offline workbench for planning, architecture, and spatial research rather than a collection of isolated utilities.

## Priorities

### P0 — Foundation and reliability

1. Keep documentation and current-state metadata governed by SSOT.
2. Freeze updater feature expansion and prove GitHub and Store updater changes with end-to-end evidence.
3. Establish a Tool Page Design System and consistent state presentation.
4. Make Project the practical context for tool workflows.
5. Stabilize schema and migration contracts.

### P1 — Connected professional workflows

1. Improve GIS and data interoperability.
2. Connect planning-specific tools into continuous workflows.
3. Improve responsive behavior and accessibility.
4. Establish performance and dependency budgets.

### P2 — Selective expansion

1. Add more complex design assistance where a proven workflow needs it.
2. Deepen research assistance without compromising offline-first boundaries.
3. Consider ARM64 or other architecture expansion only after demand and validation; it is not a commitment.

## Recommended stages

| Stage | Direction |
| --- | --- |
| A | Maintenance, documentation, and reliability |
| B | Project-centered workflow |
| C | GIS and data interoperability |
| D | Planning productivity |
| E | 2.0 maturity criteria |

These are planning stages, not approved version mappings.

## 2.0 maturity criteria

- Project is the context shared by tool workflows.
- Tool-page experience is consistently designed.
- Schema and migration contracts are formalized and tested.
- Updater regressions are no longer a frequent source of defects.
- Release metadata remains automatically consistent.
- Professional tools form continuous workflows.
- Performance, package size, and dependencies have explicit budgets.
- Three-language and accessibility practices are stable.
