# Region 1 — Quest Layout & Village Enrichment

> **Update 2026-06-17 (clean-tilemap pass) — see [Cleanup pass](#cleanup-pass-clean-green-tilemap) at the bottom.**
> Trees cut 256 → 54, off-theme torii/statue replaced, light tall-grass detail layer
> (`L3_GroundDeco`) added. Scene backup: `reference_forest_backup_before_clean_tilemap.unity`.


**Date:** 2026-06-17
**Scene:** `Assets/Scenes/region 1/reference_forest.unity` (final working scene)
**Backup:** `Assets/Scenes/region 1/reference_forest_backup_before_quest_layout.unity`
**JSON backup:** `map_layout.backup.json` (repo root — state before this pass)

## How it was edited (regenerable, NOT direct scene edits)

The scene is generated from `map_layout.json` by `Assets/Editor/MapLayoutBuilder.cs`
(menu **Tools → Reference Map → Build From Layout**). All changes in this pass were
made to the **source data**, so the improved map regenerates on every rebuild and is
never lost:

1. `tools/enrich_map_layout.py` transforms `map_layout.json`: adds prop assets,
   navigation paths, decoration objects, and a new `quests` section. Re-runnable
   (it keeps a one-time `map_layout.backup.json`).
2. `MapLayoutBuilder.cs` was extended with `BuildQuests()` — it reads the `quests`
   section and creates a `Quests` root → 5 zone GameObjects → 25 named `QuestPoint_*`
   empty GameObjects positioned at their grid-cell centers. `ClearAll()` now also
   removes the old `Quests` root so rebuilds stay idempotent.

To regenerate after editing data: run `python tools/enrich_map_layout.py` only if you
changed the generator; otherwise just edit `map_layout.json` and run the menu item.

## Major changes

- **Terrain preserved:** L0 grass, L1 river/water/land shape, both straw cabins, the
  central bridge, the island red tree, and the existing forest were all kept. Only
  the 41 trees sitting directly on a new path / quest anchor / prop cell were removed
  so anchors stay visible.
- **Navigation paths (L2_Path):** 43 cobblestone cells forming two main spines —
  bridge ⇄ left village and bridge ⇄ right village — plus short branches into the
  left forest (Zone B) and the lower-right pond/garden (Zone E). No path is painted
  over the river (cols 15–18).
- **Village/forest life:** 25 quest-anchor props + 14 ambient props (barrels, crates,
  pots, potted plants as bushes/flowers, crop/garden plots, campfires, lamps, torches,
  statues, torii gates, a well substitute). Density kept modest and clustered for
  LD-friendly readability; main walkable corridors left open.
- **5 zone landmarks:** torii gates and statues mark zone entrances so each area reads
  as a distinct place.

## Layering / sorting

Unchanged and correct (handled by the builder):
- Tilemaps: L0_Ground order 0 → L1_Water 10 → L2_Path 20.
- Objects (trees, props, structures, NPCs) get a Y-derived `sortingOrder`
  (`5000 - round(worldY*10) + (character?1:0)`), so taller things overlap correctly,
  water sits behind the bridge, and NPCs draw above props on the same cell.
- Quest points are empty logical anchors (no renderer); their visual marker is the
  adjacent prop.

## The 25 quest points

| # | Name | Zone | Cell (x,y) | Anchor | Purpose (TH) |
|---|------|------|-----------|--------|--------------|
| 01 | QuestPoint_01 | A StartingVillage | 5,6 | lamp | พบผู้ใหญ่บ้าน รับเควสต์แรก |
| 02 | QuestPoint_02 | A StartingVillage | 10,6 | torii | ป้ายประกาศ/กระดานข่าวหมู่บ้าน |
| 03 | QuestPoint_03 | A StartingVillage | 4,9 | crate | แผงตลาด: นับ/ขนสินค้า |
| 04 | QuestPoint_04 | A StartingVillage | 10,9 | well | บ่อน้ำหมู่บ้าน: ตักน้ำ |
| 05 | QuestPoint_05 | A StartingVillage | 9,10 | campfire | กองไฟรวมพล: ฟังเรื่องเล่า |
| 06 | QuestPoint_06 | B ForestRocks | 2,13 | rock | ปริศนาก้อนหิน |
| 07 | QuestPoint_07 | B ForestRocks | 6,14 | flower | เก็บดอกไม้ในป่า |
| 08 | QuestPoint_08 | B ForestRocks | 2,16 | crate | คนตัดไม้: เก็บฟืน |
| 09 | QuestPoint_09 | B ForestRocks | 6,17 | statue | ศาลเจ้าในป่า |
| 10 | QuestPoint_10 | B ForestRocks | 4,11 | bush | พุ่มเบอร์รี่: เก็บผลไม้ |
| 11 | QuestPoint_11 | C RiverBridge | 13,13 | pot | จุดตกปลาฝั่งตะวันตก |
| 12 | QuestPoint_12 | C RiverBridge | 20,13 | barrel | จุดตกปลาฝั่งตะวันออก |
| 13 | QuestPoint_13 | C RiverBridge | 13,17 | lamp | ยามเฝ้าสะพาน (ตะวันตก) |
| 14 | QuestPoint_14 | C RiverBridge | 20,17 | lamp | ยามเฝ้าสะพาน (ตะวันออก) |
| 15 | QuestPoint_15 | C RiverBridge | 16,13 | torch | ต้นไม้บนเกาะกลางน้ำ |
| 16 | QuestPoint_16 | D VillageCamp | 29,6 | campfire | หัวหน้าแคมป์: รับภารกิจ |
| 17 | QuestPoint_17 | D VillageCamp | 34,6 | barrel | ร้านค้าแลกเปลี่ยน |
| 18 | QuestPoint_18 | D VillageCamp | 28,9 | well | บ่อน้ำฝั่งขวา |
| 19 | QuestPoint_19 | D VillageCamp | 34,10 | torch | หอสังเกตการณ์/คบเพลิง |
| 20 | QuestPoint_20 | D VillageCamp | 31,10 | crop | แปลงผักหมู่บ้าน |
| 21 | QuestPoint_21 | E PondForest | 33,16 | pot | ริมบ่อน้ำ: สังเกตสัตว์น้ำ |
| 22 | QuestPoint_22 | E PondForest | 27,15 | crop2 | แปลงเกษตร: ปลูก/เก็บเกี่ยว |
| 23 | QuestPoint_23 | E PondForest | 24,17 | campfire | แคมป์ในป่า |
| 24 | QuestPoint_24 | E PondForest | 30,18 | torii | ศาลริมน้ำ |
| 25 | QuestPoint_25 | E PondForest | 35,13 | torch | จุดชมวิว/คบเพลิง |

Zone parents: `QuestZone_A_StartingVillage`, `QuestZone_B_ForestRocks`,
`QuestZone_C_RiverBridge`, `QuestZone_D_VillageCamp`, `QuestZone_E_PondForest`.

## Missing assets (no exact match in the pack — closest substitute used)

| Wanted | Status | Substitute used | Note |
|--------|--------|-----------------|------|
| Fence | missing | `Columns/column_05.prefab` (registered as `column`) | No fence prefab/tile exists; column post used where a rail is wanted. Flag for art pass. |
| Signboard | missing | `Torii/torii_01` (zone-entrance gate) / `column` | No sign asset; torii gate marks zone entrances and reads as a landmark. |
| Well | missing | `Pots/pot_20.prefab` (registered as `well`) | No well prefab; large pot as a stand-in water point. Flag for art pass. |
| Bush | missing | `Potted plants/potted_plant_03.prefab` (`bush`) | No standalone bush; leafy potted plant used. |
| Flower (ground) | missing | `Potted plants/potted_plant_12.prefab` (`flower`) | No ground-flower asset; flower-pot sprite used. |

All other props (barrels, crates, pots, crops, fires/campfire, lamps, torches,
statues, torii) are exact assets already in `Super_Retro_Collection`.

## Files changed

- `map_layout.json` — added assets, paths, props, `quests` section.
- `map_layout.backup.json` — pre-pass snapshot (new).
- `Assets/Editor/MapLayoutBuilder.cs` — `BuildQuests()` + quest clear in `ClearAll()`.
- `tools/enrich_map_layout.py` — the re-runnable data generator (new).
- `Assets/Scenes/region 1/reference_forest_backup_before_quest_layout.unity` — backup (new).
- Camera: `orthographicSize` set to 10.125 so the full 36×20 map fits the 16:9 view.

---

## Cleanup pass (clean green tilemap)

**Goal:** turn the over-decorated forest into a clean, readable green tilemap that
still keeps the straw-village theme, terrain, water, and 25 quest points.

**Method:** source-data only, regenerable. `tools/clean_map_layout.py` transforms
`map_layout.json` (deterministic, re-runnable; one-time backup `map_layout.before_clean.json`).
`MapLayoutBuilder.cs` gained an `L3_GroundDeco` parent in its clear list and a
−2000 sorting offset so ground detail renders beneath trees/props. A
`Objects/L3_GroundDeco` GameObject was added to the scene as the detail parent.

**Backup scene:** `Assets/Scenes/region 1/reference_forest_backup_before_clean_tilemap.unity`
**Working scene:** `Assets/Scenes/region 1/reference_forest.unity`

### Applied cleanup plan
- **Trees 256 → 54 (removed 202).** Kept only a thinned edge border (~50%) + named
  forest clusters (~70%: left forest, top blue groves, center/left purple groves,
  right grove, bottom-right forest) + the island red tree. Everything in open
  interior, around villages, bridge approaches and the pond was cleared.
- **Readable quests:** 5×5 opened around every quest point; 1-cell buffer cleared
  around all paths. No quest point sits under a tree.
- **Off-theme reduced:** 2 large red **torii gates → wooden signposts** (`column`);
  1 large **statue → rock** landmark. 2 clustered bridge ambient lamps removed (7 → 5).
- **Grass detail (new `L3_GroundDeco` layer):** 60 natural tall-grass tufts scattered
  at ~18% of open grass (within the 10–20% target), rendered beneath trees/props.
  Used to lightly fill bare areas without clutter — empty walkable grass is kept
  intentionally for LD readability.
- **Kept:** grass/water/path terrain, both straw cabins, bridge, NPCs, campfires,
  wells (pot substitute), barrels/crates/pots/crops, lamps (reduced), torches, rocks,
  flowers/bushes, all 25 quest points + 5 zone parents.

### Quest anchor changes (positions unchanged)
- QuestPoint_02 (notice board): torii → `column` (wooden signpost)
- QuestPoint_09 (forest shrine): statue → `rock` landmark
- QuestPoint_24 (riverside shrine): torii → `column` (wooden signpost)

### Counts
- Objects 309 → 165 (54 trees, 60 grass tufts, 51 props/structures/NPCs).
- All 25 `QuestPoint_*` across the 5 `QuestZone_*` parents rebuilt and intact.

### Assets removed / changed
- `torii` and `statue` are no longer placed (asset defs remain unused in the dict).
  Reason: off-theme for a straw village (large fantasy/temple objects), per request.
- New asset `grass_detail` → sprite `…/tall_grass_react_on_contact/Sprites/tallgrass_01_front.png`
  (sliced sheet; the builder uses the first frame = a single tuft).

### Edited via
`map_layout.json` + `MapLayoutBuilder.cs` (regenerable) — **not** direct scene editing,
except the one structural `Objects/L3_GroundDeco` parent GameObject (documented here).
