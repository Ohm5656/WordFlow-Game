# Reference Forest Map — Recognizable Recreation (Design)

**Date:** 2026-06-17
**Status:** Approved design → ready for implementation plan
**Branch:** feat/word-build-encounter-prototype

## Goal

Recreate the top-down pixel-art forest overworld in `ref.webp` inside Unity as a
new scene, using **only assets already in the project**. Target a **recognizable
layout match** (~80–90% "feels like the reference"), not a per-pixel match.

## Established facts

- **Reference image:** `ref.webp`, 1438×810 px (an upscaled ~2.5× marketing-style
  screenshot, not native resolution).
- **Tile size:** 16 px (sprites import at PPU 16).
- **Asset source:** `Assets/Gif/Super_Retro_Collection` — ships **Autotiles**
  (rule-tiles that auto-resolve grass/water/path edges), **Legacytiles** (manual),
  ready-made TilePalette prefabs (`SuperRetro_AutotilePalette.prefab`, etc.), and
  atlases (`legacy_atlas.png`, `original_atlas.png`, `stacked_trees.png`).
- **Grid:** **36 wide × 20 tall** cells (16:9, matches the reference aspect and
  makes the anchor-table percentages map to clean cells).

## Reference content (what must appear)

Two wooden cabins (left ≈20%/25%, right ≈89%/26%); a central vertical river flowing
top→bottom; a horizontal wooden bridge crossing it near the bottom (≈42%/84%); a
central island in the river with a red/autumn tree (≈45%/62%); blue-tree clusters
(top-left ≈26%/12%, top-right ≈85%/13%); purple-tree clusters (center ≈55%/24%,
left ≈12%/55%); green conifer trees throughout; corner water channels (top-left,
top-right, bottom-right); rocks, stumps, mushrooms, flowers as ground decoration;
NPC groups near each cabin (≈20%/37% and ≈91%/37%); cobblestone paths by each cabin.

## Architecture

The core decision: **JSON layout file as source of truth + a single re-runnable
Editor C# script that builds the scene, all driven through the Unity MCP server.**
This satisfies both the "MCP-driven" requirement and the "re-runnable placement
script" deliverable, while avoiding hundreds of slow individual MCP tile-paint calls.

### Components

1. **`map_layout.json`** — editable source of truth. Shape:
   ```json
   {
     "width": 36, "height": 20, "tile_px": 16,
     "layers": {
       "L0_Ground": [[...]], "L1_Water": [[...]], "L2_Path": [[...]],
       "L3_GroundDeco": [[...]], "L4_Trees": [[...]]
     },
     "objects": [
       {"asset": "cabin", "x": 7, "y": 5, "layer": "L5_Structures"},
       {"asset": "npc_blue", "x": 7, "y": 7, "layer": "L6_Characters"}
     ]
   }
   ```
   - Tilemap layers are 2D arrays of asset-IDs (string keys → resolved to tiles).
   - Large props / prefabs / characters go in the `objects` list.

2. **`MapLayoutBuilder.cs`** (Editor script, under `Assets/Editor/`) — reads
   `map_layout.json`, **clears and repaints** the target scene's tilemaps and
   re-instantiates objects. Fully re-runnable: edit JSON → re-run → regenerated map.
   Exposed via a menu item (and callable in batchmode) so MCP can trigger it.

3. **Scene** — a **new** scene `Assets/Scenes/region 1/reference_forest.unity`.
   No existing scene is modified or deleted.

### Layers (bottom → top render order), each its own Sorting Layer

| Layer | Content | Tech |
|-------|---------|------|
| L0_Ground | grass base | autotile |
| L1_Water | river + corner channels | autotile |
| L2_Path | cobblestone paths | autotile/tile |
| L3_GroundDeco | flowers, mushrooms, small rocks | tiles/sprites |
| L4_Trees | conifer / blue / purple / red trees, big rocks, stumps | sprites, Y-sorted |
| L5_Structures | cabins, bridge | prefabs/sprites, Y-sorted |
| L6_Characters | NPC groups | prefabs/sprites, Y-sorted |

Tilemap Renderer uses **Individual** sort mode + Transparency Sort = Y so taller
objects (trees, cabins) overlap correctly by Y position.

### Coordinate mapping

Anchor percentages (origin = top-left) → cells via
`cellX = round(pct_x/100 * 36)`, `cellY = round(pct_y/100 * 20)`. Anchors are
starting points; refined by visual comparison in the verify loop.

## Workflow & gates

- **Phase 0 — Asset inventory (GATE):** Map every reference element to a concrete
  asset path. Produce a `MISSING_ASSETS` list for anything with no exact match,
  choosing the closest existing asset as substitute. **Stop and show the user.**
- **Phase 1 — Scene & grid setup:** Create scene, Grid, the 7 tilemap layers with
  sorting layers, configure Y-sorting.
- **Phase 2 — Coordinate mapping:** Convert anchors to cells (done above).
- **Phase 3 — `map_layout.json` (GATE):** Build the full layout file. **Stop and
  show the user** before any placement.
- **Phase 4 — Build:** Run `MapLayoutBuilder` via MCP to paint/instantiate in layer
  order L0→L6.
- **Phase 5 — Verify loop:** Screenshot the Scene/Game view framed to the ref aspect,
  diff against `ref.webp`, list mismatches, edit JSON, re-run. Repeat until >90%
  match; then user does final visual check.

## Constraints / hard rules

- Only assets already in the project. Every used asset's path is listed.
- No new art imported or invented. Missing elements → `MISSING_ASSETS` + closest match.
- Pixel-perfect snapping to the grid; no sub-pixel offsets.
- **Stop and ask** before deleting or modifying any existing scene object.
- Unity Editor must be open for MCP to act (user will open it). Batchmode is the
  fallback but loses live screenshot verification.

## Deliverables

- `map_layout.json` (editable source of truth)
- `Assets/Editor/MapLayoutBuilder.cs` (re-runnable placement script)
- `Assets/Scenes/region 1/reference_forest.unity` (built scene)
- `MISSING_ASSETS` list
- Final screenshot vs `ref.webp` comparison

## Out of scope (YAGNI)

- Per-pixel tile matching.
- Collision, gameplay logic, navmesh, lighting polish.
- Reusing or fixing the prior Python scripts / `real_region1*` scenes.
