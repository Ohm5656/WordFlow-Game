# Reference Forest Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recreate the `ref.webp` top-down forest overworld as a new Unity scene at a recognizable-layout fidelity, using only existing project assets, driven through the Unity MCP server.

**Architecture:** A `map_layout.json` file is the editable source of truth (36×20 grid, 16px tiles). A single re-runnable Editor script `MapLayoutBuilder.cs` reads it, clears & repaints tilemap layers, and instantiates object/prefab landmarks. We drive scene creation, the build, and screenshot verification through the Unity MCP server (Editor is open).

**Tech Stack:** Unity 6000.4.3f1, 2D Tilemap + URP, FangAutoTile rule-tiles, `Super_Retro_Collection` asset pack, Unity MCP server (coplay-mcp tools), C# Editor scripting.

## Global Constraints

- Use ONLY assets already in the project. Every asset referenced in `map_layout.json` carries its full `Assets/...` path. Verbatim rule from spec.
- Do NOT import or invent new art. Anything with no exact match → `MISSING_ASSETS` list + closest existing substitute.
- Grid is exactly **36 wide × 20 tall**, tile size **16 px**, PPU **16**. Snap everything to integer cells; no sub-pixel offsets.
- Render order (bottom→top), each its own Sorting Layer: `L0_Ground` → `L1_Water` → `L2_Path` → `L3_GroundDeco` → `L4_Trees` → `L5_Structures` → `L6_Characters`.
- Tilemap Renderer sort mode = **Individual**; scene Transparency Sort Mode = **Custom Axis (0,1,0)** so taller objects Y-sort correctly.
- Do NOT delete or modify any existing scene/asset. Only the NEW scene `Assets/Scenes/region 1/reference_forest.unity` and the NEW files below are created.
- Unity Editor must be open for MCP tools to act. If a `get_unity_editor_state` call errors with "Unity Editor is not running", STOP and ask the user to open the project.
- Anchor → cell mapping: `cellX = round(pct_x/100 * 36)`, `cellY = round(pct_y/100 * 20)`. Cell origin is TOP-LEFT for authoring; the builder converts to Unity's bottom-left tilemap space as `unityY = (height-1) - rowFromTop`.

## MCP Tooling (UnityMCP server) — authoritative, supersedes any `coplay-mcp` tool names in task bodies below

The live bridge is the **`UnityMCP`** server (package `com.coplaydev.unity-mcp`), reached only after `/mcp` reconnect. Use these tools; ignore the `coplay-mcp` names written in Tasks 2/4/5/6:

| Operation | UnityMCP tool |
|-----------|---------------|
| Create/load/save/query scene | `manage_scene` (action create/load/save/get_active/get_hierarchy) |
| Create GameObject, set parent, add components, set transform | `manage_gameobject` (action create/modify; `parent`, `components_to_add`, `component_properties`) |
| Add/remove component, set component property | `manage_components` |
| Run arbitrary C# (in-memory, no file) | `execute_code` (action execute) — used for Sorting Layers, GraphicsSettings transparency sort, and invoking `MapLayoutBuilder.Build()` |
| Create/edit a real C# script file | `manage_script` / `create_script` |
| Check compile errors / read console | `read_console` (action get, types ["error"]) — poll after script changes |
| Screenshot Scene/Game view | `manage_camera` (action screenshot / screenshot_multiview) |
| Find GameObjects | `find_gameobjects` |

Notes: Sorting Layers have no dedicated tool → create via `execute_code` editing the `TagManager` asset. Transparency Sort Mode → `execute_code` setting `GraphicsSettings.transparencySortMode/Axis` (or `manage_graphics`). The MapLayoutBuilder is invoked each round via `execute_code` calling `MapLayoutBuilder.Build();`. Only one Unity instance is connected, so no `set_active_instance` needed. **If any UnityMCP call returns a connection / "not running" error, STOP and report BLOCKED — the MCP server must be reconnected via `/mcp` by the human; a subagent cannot fix it.**

## File Structure

- `map_layout.json` (repo root) — editable source of truth: grid dims, an `assets` registry (id → full path + type), 2D arrays per tilemap layer, and an `objects` list for prefabs/sprites.
- `Assets/Editor/MapLayoutBuilder.cs` — Editor script. Menu item `Tools/Reference Map/Build From Layout`. Reads `map_layout.json`, clears & repaints, re-instantiates objects. Re-runnable. Also a static `Build()` entry callable from MCP `execute_script`.
- `Assets/Scenes/region 1/reference_forest.unity` — the built scene (created in Task 2).
- `docs/superpowers/MISSING_ASSETS.md` — the missing-asset log + chosen substitutes (created in Task 1).

---

## Task 1: Phase 0 — Asset inventory + MISSING_ASSETS (GATE)

**Files:**
- Create: `docs/superpowers/MISSING_ASSETS.md`

**Interfaces:**
- Produces: a confirmed mapping `referenceElement → {assetId, fullPath, type}` for every visual element, used verbatim as the `assets` registry in Task 3. Asset IDs are short kebab/snake strings (e.g. `grass`, `water`, `path`, `tree_conifer`, `tree_blue`, `tree_purple`, `tree_red`, `cabin`, `bridge`, `rock`, `stump`, `mushroom`, `flower`, `npc_a`).

**Reference elements to map:** grass base, water (river + corner channels), cobblestone path, conifer/green tree, blue tree, purple tree, red/autumn tree, wooden cabin, wooden bridge, rock, stump, mushroom, flower, NPC.

**Known candidate locations (verify each, capture exact paths):**
- Ground/Water/Path autotiles: `Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/*.asset` (FangAutoTile rule-tiles).
- Plain tiles: `.../TilePalette/Tiles/*.asset` and `.../Legacytiles/*.asset`.
- Trees: `Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/Sprites/tree_*.png` (multiple color variants).
- Houses (cabins): `.../Prefabs/Houses/house_01..26.prefab`.
- Rocks: `.../Prefabs/Rocks/`. Lamps/Statues/Torii also available.
- Bridge: `Assets/Prefabs/Generated/reference_plank_bridge.prefab` (existing project asset — reuse allowed).
- NPCs: `.../Resources/ARPG/character_0..31/` (sprite sets, NOT prefabs — see note).
- Deco (mushroom/flower/stump): search the atlases / tile sets and `original_atlas.png` slices.

- [ ] **Step 1: Confirm Unity Editor is live**

MCP: call `get_unity_editor_state`. Expected: returns state (not the "not running" error). If it errors, STOP and ask the user to open the project.

- [ ] **Step 2: Enumerate candidate assets**

Use the Read/Glob/Grep tools (faster than MCP) to list the candidate folders above and read tree/house sprite previews where needed. For trees, open `Prefabs/Trees/Sprites/` and identify which `tree_NN.png` reads as green-conifer, which as blue, which as purple, which as red/autumn — by reading the PNGs. Record exact paths.

- [ ] **Step 3: Decide NPC handling**

ARPG characters are sprite folders, not prefabs. Closest-match decision: instantiate a `GameObject` with a `SpriteRenderer` using a chosen `character_X` idle sprite (front-facing). Pick 2–3 distinct `character_X` sprites for variety. Record their exact sprite paths. Log "no ready NPC prefab" in MISSING_ASSETS with this substitution.

- [ ] **Step 4: Write the inventory + MISSING_ASSETS doc**

Write `docs/superpowers/MISSING_ASSETS.md` with two tables: (a) a full inventory table `assetId | type (tile/sprite/prefab) | fullPath | represents`; (b) a MISSING_ASSETS table `referenceElement | why no exact match | chosen substitute (assetId + path)`. Every reference element from the list above must appear in one table.

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/MISSING_ASSETS.md
git commit -m "docs: Phase 0 asset inventory + MISSING_ASSETS for reference forest map"
```

- [ ] **Step 6: GATE — show the user**

Present both tables to the user. Explicitly ask: "These are the asset mappings and substitutions. Approve before I author the layout?" Do NOT proceed to Task 2 until the user approves. If the user wants different assets, edit the doc and re-show.

---

## Task 2: Phase 1 — Create scene, grid, layers (via MCP)

**Files:**
- Create: `Assets/Scenes/region 1/reference_forest.unity`

**Interfaces:**
- Produces: a saved scene containing GameObject `Grid` with 7 child Tilemaps named exactly `L0_Ground`, `L1_Water`, `L2_Path`, `L3_GroundDeco`, `L4_Trees`, `L5_Structures`, `L6_Characters`, plus empty parent GameObjects `Objects/L5_Structures` and `Objects/L6_Characters` for instantiated props/NPCs. Task 4's builder finds layers/objects by these exact names.

- [ ] **Step 1: Create the new scene**

MCP: `create_scene` at path `Assets/Scenes/region 1/reference_forest.unity` (2D / empty). Then `open_scene` on it. Verify with `get_unity_editor_state` that the active scene is `reference_forest`.

- [ ] **Step 2: Create the Grid + 7 tilemap layers**

MCP: `create_game_object` named `Grid` with a `Grid` component (cell size 1,1,0). For each layer name `L0_Ground … L4_Trees` create a child GameObject with `Tilemap` + `TilemapRenderer` components, parented to `Grid` (`parent_game_object`). (L5/L6 are object parents, not tilemaps — created in Step 4.)

Note: L0–L4 are tilemaps; L5_Structures and L6_Characters hold instantiated prefabs/sprites, so they are created as tilemaps only if you also want tile-based structures — per design they are object layers. Create tilemaps for L0_Ground, L1_Water, L2_Path, L3_GroundDeco, L4_Trees only.

- [ ] **Step 3: Create Sorting Layers + assign**

MCP: ensure Sorting Layers exist in this order: `L0_Ground, L1_Water, L2_Path, L3_GroundDeco, L4_Trees, L5_Structures, L6_Characters` (use `set_property` / `execute_script` to add tags/sorting layers via `UnityEditorInternal.InternalEditorUtility` if no direct tool). Assign each TilemapRenderer's `sortingLayerName` to its matching layer. Set each L0–L4 TilemapRenderer `mode = Individual` (`TilemapRenderer.Mode.Individual`).

- [ ] **Step 4: Create object parents + Y-sort config**

MCP: create empty GameObjects `Objects` (root), with children `L5_Structures` and `L6_Characters`. Set the scene's Transparency Sort Mode to Custom Axis (0,1,0) via `execute_script` (`GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis; GraphicsSettings.transparencySortAxis = new Vector3(0,1,0);`). This makes higher-Y objects render behind lower-Y.

- [ ] **Step 5: Save + verify**

MCP: `save_scene`. Then `list_game_objects_in_hierarchy` and confirm `Grid` has the 5 tilemap children and `Objects` has `L5_Structures`, `L6_Characters`. Expected: all 8 named objects present.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/region 1/reference_forest.unity" "Assets/Scenes/region 1/reference_forest.unity.meta"
git commit -m "feat: scaffold reference_forest scene with grid + sorting layers"
```

---

## Task 3: Phase 3 — Author map_layout.json (GATE)

**Files:**
- Create: `map_layout.json` (repo root)

**Interfaces:**
- Consumes: the approved asset registry from Task 1.
- Produces: `map_layout.json` consumed by `MapLayoutBuilder` (Task 4). Exact schema below; the builder's parser depends on these field names.

**Use the EXACT asset paths from `docs/superpowers/MISSING_ASSETS.md` Table A** (verified, approved at the gate). Do not invent paths.

**Schema (exact) — reflects the locked design (3 tilemaps only; trees/cabins/bridge/rocks/npcs are objects; NO ground-deco layer):**
```json
{
  "width": 36,
  "height": 20,
  "tile_px": 16,
  "assets": {
    "grass":  { "type": "tile",   "path": "Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/root/single/New Fang Auto Tile.asset" },
    "water":  { "type": "tile",   "path": "Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/root/animated/overworld_anim_1.asset" },
    "path":   { "type": "tile",   "path": "Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/root/single/New FangAuto Tile 1.asset" },
    "tree_conifer": { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/Sprites/tree_01.png" },
    "tree_blue":    { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/Sprites/tree_09.png" },
    "tree_purple":  { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/Sprites/tree_14.png" },
    "tree_red":     { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/Sprites/tree_19.png" },
    "cabin":  { "type": "prefab", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Houses/house_14.prefab" },
    "bridge": { "type": "prefab", "path": "Assets/Prefabs/Generated/reference_plank_bridge.prefab" },
    "rock":   { "type": "prefab", "path": "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_20.prefab" },
    "npc_a":  { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/ARPG/character_0/walk_shields_1.png" },
    "npc_b":  { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/ARPG/character_2/walk_shields_1.png" },
    "npc_c":  { "type": "sprite", "path": "Assets/Gif/Super_Retro_Collection/Resources/ARPG/character_3/walk_shields_1.png" }
  },
  "layers": {
    "L0_Ground": [["grass", "..."], ["..."]],
    "L1_Water":  [[null, "water"], ["..."]],
    "L2_Path":   [[null, "..."]]
  },
  "objects": [
    { "asset": "cabin",  "x": 7,  "y": 5,  "layer": "L5_Structures" },
    { "asset": "bridge", "x": 15, "y": 17, "layer": "L5_Structures" },
    { "asset": "tree_red", "x": 16, "y": 12, "layer": "L4_Trees" },
    { "asset": "npc_a",  "x": 7,  "y": 7,  "layer": "L6_Characters" }
  ]
}
```
Rules:
- `layers` has EXACTLY three keys: `L0_Ground`, `L1_Water`, `L2_Path`. Each is exactly 20 rows (top→bottom) × 36 cols; `null` = empty cell. There is NO `L3_GroundDeco` / `L4_Trees` tilemap.
- ALL trees, cabins, bridge, rocks, NPCs are entries in `objects` (so the builder Y-sorts them). `layer` must be one of `L4_Trees` (all trees + rocks), `L5_Structures` (cabins, bridge), `L6_Characters` (npcs).
- `x,y` are TOP-LEFT origin cells (x: 0..35 left→right, y: 0..19 top→bottom). The builder flips y to Unity space.

- [ ] **Step 1: Fill the asset registry**

Copy the approved Task-1 mappings into `assets`. Every id used anywhere in `layers`/`objects` must exist here with a real path.

- [ ] **Step 2: Author L0_Ground**

Fill the entire 20×36 `L0_Ground` array with `"grass"` (base layer is fully grassed).

- [ ] **Step 3: Author L1_Water (river + channels)**

Paint a central vertical river band (cols ~14–17, %X 40–48) flowing from row ~5 (%Y 28) to row ~17 (%Y 84), with a 2–3 cell island gap around cell (16,12) (the red-tree island, %45/62). Add corner channels: top-left around (4,3) %12/14, top-right around (29,2) %80/12, bottom-right around (28,16) %78/80. Set those cells to `"water"`, rest `null`.

- [ ] **Step 4: Author L2_Path**

Short cobblestone path stubs at each cabin doorstep: left cabin around (9,7) %24/35, right cabin around (32,7) %90/36. A few `"path"` cells each, rest `null`.

- [ ] **Step 5: Author objects (trees, rocks, cabins, bridge, NPCs)**

Add to `objects` (all with `x,y` top-left cells + `layer`):
- `L5_Structures`: left cabin (7,5), right cabin (32,5); bridge (15,17).
- `L4_Trees`: red tree on island (16,12); blue clusters around (9,2)+(31,3); purple clusters around (20,5)+(4,11); a dozen+ `tree_conifer` filling the remaining green areas (avoid water/path cells); a few `rock` near water edges.
- `L6_Characters`: NPC group near left cabin (7,7),(8,7) and right cabin (33,7),(32,8), mixing `npc_a/npc_b/npc_c`.

Use the anchor table for placement; refine in Task 6. Keep trees off water cells.

- [ ] **Step 6: Validate JSON**

Run (note: only 3 layers expected): `python -c "import json; d=json.load(open('map_layout.json')); assert d['width']==36 and d['height']==20; assert set(d['layers'])=={'L0_Ground','L1_Water','L2_Path'}, d['layers'].keys(); [print('BAD LAYER',k,len(v),len(v[0])) for k,v in d['layers'].items() if len(v)!=20 or any(len(r)!=36 for r in v)]; ids=set(d['assets']); bad=[o for o in d['objects'] if o['asset'] not in ids or o['layer'] not in {'L4_Trees','L5_Structures','L6_Characters'}]; print('BAD OBJ', bad) if bad else print('ok', len(d['objects']), 'objects')"`
Expected: prints `ok <n> objects`, no `BAD LAYER` / `BAD OBJ` lines.

- [ ] **Step 7: Commit**

```bash
git add map_layout.json
git commit -m "feat: author map_layout.json for reference forest map"
```

- [ ] **Step 8: GATE — show the user**

Show `map_layout.json` (or a summarized cell/landmark map) to the user. Ask for approval before building. Do NOT proceed to Task 5's build run until approved (Task 4 may be written in parallel).

---

## Task 4: Build MapLayoutBuilder.cs editor script

**Files:**
- Create: `Assets/Editor/MapLayoutBuilder.cs`

**Interfaces:**
- Consumes: `map_layout.json` (3 tilemap layers `L0_Ground`/`L1_Water`/`L2_Path` + an `objects` list). Scene object names from Task 2: tilemaps live at `Grid/L0_Ground`, `Grid/L1_Water`, `Grid/L2_Path`; object parents at `Objects/L4_Trees`, `Objects/L5_Structures`, `Objects/L6_Characters`.
- Produces: `[MenuItem("Tools/Reference Map/Build From Layout")] public static void Build()`. Re-runnable: clears the 3 tilemaps + the 3 object groups, then repaints tiles and instantiates objects. **Invoked via UnityMCP `execute_menu_item` with menu_path `Tools/Reference Map/Build From Layout`** — `execute_code` is unavailable in this environment (no Roslyn; CodeDom command line too long), so a real `[MenuItem]` is the only way to run it.

**Design constraints baked in (from Task 2 reality):**
- No named Sorting Layers exist. Stacking is by `sortingOrder` int. Tilemaps already carry sortingOrder 0/10/20. Each placed object gets `sortingOrder = 5000 - round(worldY*10) (+1 if on L6_Characters)` so lower-on-screen renders in front and all objects sit above the tilemaps.
- Objects are bottom-anchored: lift the instance by `(rendererExtents.y - 0.5)` so a tall sprite's base sits on its cell.
- Sprites may be sub-assets of a multi-sprite texture (NPC sheets) — resolve via `LoadAllAssetsAtPath` fallback.

- [ ] **Step 1: Write `Assets/Editor/MiniJson.cs`**

Create `Assets/Editor/MiniJson.cs` containing the public-domain **MiniJSON** parser (class `MiniJson` with `static object Parse(string)` returning nested `Dictionary<string,object>` / `List<object>` / boxed `double`/`long`/`bool`/`string`). Copy it verbatim from the well-known public-domain MiniJSON source. `JsonUtility` cannot parse arbitrary dictionaries, which is why this is needed.

- [ ] **Step 2: Write `Assets/Editor/MapLayoutBuilder.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class MapLayoutBuilder
{
    const string LayoutPath = "map_layout.json";

    [MenuItem("Tools/Reference Map/Build From Layout")]
    public static void Build()
    {
        if (!File.Exists(LayoutPath)) { Debug.LogError($"MapLayoutBuilder: missing {LayoutPath}"); return; }
        var layout = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(LayoutPath));
        int height = Convert.ToInt32(layout["height"]);
        var assets  = (Dictionary<string, object>)layout["assets"];
        var layers  = (Dictionary<string, object>)layout["layers"];
        var objects = (List<object>)layout["objects"];

        ClearAll();
        PaintLayers(layers, assets, height);
        PlaceObjects(objects, assets, height);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("MapLayoutBuilder: build complete");
    }

    static Tilemap Tilemap(string name) => GameObject.Find($"Grid/{name}")?.GetComponent<Tilemap>();
    static Transform ObjParent(string layer) => GameObject.Find($"Objects/{layer}")?.transform;
    static int OrderFor(float worldY, bool character) => 5000 - Mathf.RoundToInt(worldY * 10f) + (character ? 1 : 0);

    static void ClearAll()
    {
        foreach (var n in new[]{"L0_Ground","L1_Water","L2_Path"}) Tilemap(n)?.ClearAllTiles();
        foreach (var l in new[]{"L4_Trees","L5_Structures","L6_Characters"})
        {
            var p = ObjParent(l); if (p == null) continue;
            for (int i = p.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(p.GetChild(i).gameObject);
        }
    }

    static void PaintLayers(Dictionary<string,object> layers, Dictionary<string,object> assets, int height)
    {
        foreach (var kv in layers)
        {
            var tm = Tilemap(kv.Key); if (tm == null) continue;
            var rows = (List<object>)kv.Value;
            for (int y = 0; y < rows.Count; y++)
            {
                var row = (List<object>)rows[y];
                for (int x = 0; x < row.Count; x++)
                {
                    var id = row[x] as string; if (string.IsNullOrEmpty(id)) continue;
                    var tile = Resolve<TileBase>(assets, id);
                    if (tile) tm.SetTile(new Vector3Int(x, (height - 1) - y, 0), tile);
                }
            }
        }
    }

    static void PlaceObjects(List<object> objects, Dictionary<string,object> assets, int height)
    {
        var ground = Tilemap("L0_Ground");
        foreach (var o in objects)
        {
            var e = (Dictionary<string,object>)o;
            string id = (string)e["asset"];
            int x = Convert.ToInt32(e["x"]); int y = Convert.ToInt32(e["y"]);
            string layer = (string)e["layer"];
            var parent = ObjParent(layer); if (parent == null) { Debug.LogError($"No parent {layer}"); continue; }
            var entry = (Dictionary<string,object>)assets[id];
            string type = (string)entry["type"];
            Vector3 pos = ground.GetCellCenterWorld(new Vector3Int(x, (height - 1) - y, 0));

            GameObject go;
            if (type == "prefab")
            {
                var prefab = Resolve<GameObject>(assets, id); if (prefab == null) continue;
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else // sprite
            {
                var sprite = ResolveSprite((string)entry["path"], id); if (sprite == null) continue;
                go = new GameObject(id);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>().sprite = sprite;
            }
            go.name = $"{id}_{x}_{y}";
            var rend = go.GetComponentInChildren<SpriteRenderer>();
            float lift = rend != null ? rend.bounds.extents.y - 0.5f : 0f;
            go.transform.position = new Vector3(pos.x, pos.y + lift, 0f);
            int order = OrderFor(pos.y, layer == "L6_Characters");
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder = order;
        }
    }

    static T Resolve<T>(Dictionary<string,object> assets, string id) where T : UnityEngine.Object
    {
        var path = (string)((Dictionary<string,object>)assets[id])["path"];
        var obj = AssetDatabase.LoadAssetAtPath<T>(path);
        if (obj == null) Debug.LogError($"MapLayoutBuilder: asset not found id '{id}': {path}");
        return obj;
    }

    static Sprite ResolveSprite(string path, string id)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path)) if (a is Sprite sp) return sp;
        Debug.LogError($"MapLayoutBuilder: sprite not found id '{id}': {path}");
        return null;
    }
}
```

- [ ] **Step 3: Trigger a recompile + check for errors**

MCP (UnityMCP): call `refresh_unity` (or `manage_asset` import) so Unity picks up the new scripts, then poll `read_console` (action get, types ["error"]) until no compile errors mention `MapLayoutBuilder` or `MiniJson`. Expected: clean compile. If errors, fix the script and re-check.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/MapLayoutBuilder.cs Assets/Editor/MapLayoutBuilder.cs.meta Assets/Editor/MiniJson.cs Assets/Editor/MiniJson.cs.meta
git commit -m "feat: add MapLayoutBuilder editor script (reads map_layout.json)"
```

---

## Task 5: Phase 4 — Run the build via MCP, first render

**Files:**
- Modify (generated content): `Assets/Scenes/region 1/reference_forest.unity`

**Interfaces:**
- Consumes: `MapLayoutBuilder.Build()`, the saved scene, approved `map_layout.json`.

- [ ] **Step 1: Ensure correct scene is open**

MCP (UnityMCP): `manage_scene` action=load `Assets/Scenes/region 1/reference_forest.unity`. Verify via `manage_scene` action=get_active.

- [ ] **Step 2: Run the builder**

MCP (UnityMCP): `execute_menu_item` menu_path `Tools/Reference Map/Build From Layout`. Then `read_console` (types ["error","log"]) — expect `MapLayoutBuilder: build complete`, no "asset not found" errors. If any "asset not found", fix the path in `map_layout.json` and re-run.

- [ ] **Step 3: Save scene**

MCP (UnityMCP): `manage_scene` action=save.

- [ ] **Step 4: First screenshot**

MCP (UnityMCP): `manage_camera` action=screenshot, capture_source=game_view, include_image=true, max_resolution 640 (camera already framed to the 36×20 grid). Save/inspect the image. This is the Task 6 baseline.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scenes/region 1/reference_forest.unity"
git commit -m "feat: build reference forest map from layout (first pass)"
```

---

## Task 6: Phase 5 — Verify loop, iterate to >90%

**Files:**
- Modify: `map_layout.json` (each iteration)

- [ ] **Step 1: Side-by-side compare**

Place the latest scene screenshot next to `ref.webp`. Produce a mismatch checklist: river path, bridge position, each cabin cell, island/red-tree, each tree cluster color+position, corner channels, NPC groups, overall density. Note each as ✓ / off-by-N / wrong-asset.

- [ ] **Step 2: Edit map_layout.json to fix the top mismatches**

Adjust cells/object coords/asset ids for the worst offenders only (don't churn everything). Re-validate JSON (Task 3 Step 6 command).

- [ ] **Step 3: Rebuild + rescreenshot**

MCP (UnityMCP): `execute_menu_item Tools/Reference Map/Build From Layout` → `manage_scene` save → `manage_camera` screenshot. Report a short diff vs previous round.

- [ ] **Step 4: Repeat Steps 1–3 until >90% recognizable**

Stop when the checklist is mostly ✓ and the layout clearly reads as the reference. Report the round-by-round diff.

- [ ] **Step 5: Commit each meaningful round**

```bash
git add map_layout.json "Assets/Scenes/region 1/reference_forest.unity"
git commit -m "fix: refine reference forest map (round N) — <what changed>"
```

- [ ] **Step 6: Final hand-off**

Present the final screenshot vs `ref.webp` and the deliverables list (`map_layout.json`, `MapLayoutBuilder.cs`, `reference_forest.unity`, `MISSING_ASSETS.md`). Ask the user for the final visual sign-off.

---

## Self-Review notes

- **Spec coverage:** Phase 0 (Task 1), Phase 1 (Task 2), Phase 2 anchors (Global Constraints + Task 3), Phase 3 (Task 3), Phase 4 (Tasks 4–5), Phase 5 (Task 6). Deliverables all produced. ✓
- **Gates:** Task 1 Step 6 and Task 3 Step 8 are hard stops. ✓
- **Naming consistency:** layer names, `Build()`, `ClearAll/PaintLayers/PlaceObjects`, `Resolve<T>` consistent across Tasks 2/4/5. Object parents `Objects/L4_Trees`,`Objects/L5_Structures`,`Objects/L6_Characters` consistent. ✓
- **Tooling:** all live-Unity steps use the **UnityMCP** server per the MCP Tooling table (the `coplay-mcp` names originally written here are superseded). `execute_code` is unavailable, so the builder runs via `execute_menu_item`.
