# Night Redo Mode v2 — spotlight rework (2026-07-14)

**This is a REVISION of the v1 implementation, which is already in the working tree, UNCOMMITTED.**
Read `2026-07-14-night-redo-mode.md` (v1) for the foundation. v1 compiles clean and its state layer
works (verified via `Tools/Night/Log State`). v2 replaces v1's *visuals and entry point*; the state
layer, the day-end handoff, and the puzzle-entry branch all stay.

## What the user actually wants (corrected + confirmed 2026-07-14)

- **Day**: unchanged, exactly the existing logic. Night falls at the end → hero enters the house →
  fade → WorldMap. The star board on quests is **hidden during the day**.
- **WorldMap night**: dark **like reference_forest's night — properly dark**, with light **only on
  the playable island** (a soft circular opening, like the forest's lamp cutouts). **NO star button**
  (v1's blinking star icon is deleted). The child clicks the island itself to enter the night redo.
- **reference_forest night**: map dark (existing `NightLighting.SetNight(1)` look). A circle of
  light **follows the hero** while walking. Every quest that is not yet 3★ is **visible from scene
  start**: the quest actor (bear / crows) with **NO "!" marker**, plus a wooden **star board**
  (`panel_star.png` + gold `star.png` on earned slots + `effect_star.png` sparkle) floating beside
  it. Each shown quest also gets its **own pool of light** so it reads from afar (confirmed).
- **CutScene_bear / CutScene_ga night**: whole scene dark, with a **circle of light that follows the
  bear / crow entrance animation**. When the entrance ends and the craft book pops, the **circle
  fades away and the whole screen settles to a flat dim** (confirmed: "วงจางหาย → เหลือมืดจางทั้งจอ")
  so the book/stones/smoke stay readable. Success scenes after: **flat dim only**.
- **All quests 3★**: WorldMap **stays night**; the island is still lit + clickable; entering the
  forest just walks the hero home past everything (confirmed: "ค้างกลางคืน เข้าเกาะได้แต่ไม่มีเควส").
  `NightPhase` never auto-clears.

## What stays from v1 (do NOT touch these)

- `Assets/Scripts/Common/NightMode.cs` — except one addition (§1).
- `Assets/Scripts/Common/QuestStars.cs` — unchanged.
- `MagicStonePuzzleController.cs` — the `QuestStars.RecordBest` call stays as-is.
- `OwlGreetingCutscene.cs` — `NightRedoRoutine` branch stays as-is (no owl, fresh 30s, book pops).
- `QuestPathSequence.cs` — day-end handoff (`NightPhase = true` → Cover → WorldMap), the
  `Start()` branch, the serialized night fields: all stay. The night route itself is revised (§4).
- `WorldMapProblemIslands.cs` — `PlayableSceneName` accessor + night unlock-replay suppression stay;
  one more accessor + one hook added (§3).
- `Assets/Editor/NightModeTools.cs` — stays (update the quest-id reference per §1).
- `Assets/Resources/Stars/star.png` — stays (already imported as Single sprite).

## What is deleted/rewritten from v1

- `WorldMapNight.cs` — **rewritten** (no canvas, no button; world-space dark overlay + island hole).
- `NightTintOverlay.cs` — **rewritten** (flat tint → spotlight-follow + settle-to-dim).
- `QuestStarBadge.cs` — **rewritten** (single stars → panel + stars + sparkle composite).

---

## 0. Ground truth (verified — do not re-derive)

- Shader `NSC/NightOverlayCutout` at `Assets/Scenes/region 1/adventure/Shaders/NightOverlayCutout.shader`
  (guid `0a1b2c3d4e5f60718293a4b5c6d7e8f9`): full-screen sprite tinted by vertex color, cuts soft
  circular holes. `_LightData0.._LightData3` = `(centerX, centerY, innerR, outerR)` in **world
  units**; slot with `w<=0` ignored; `_MinimumDarkness` = floor inside a hole. Consumers:
  `NightLighting` (forest, 4 fixture slots, via MaterialPropertyBlock) and `BearIntroSequence`
  (bear-intro spotlight, slots 0-1).
- `NightDark` overlay in reference_forest renders at **sortingOrder 32000**.
- Sibling assets: `NightDarkOverlay.mat` (guid `5e9b42669bd777a4b9fb2b54c57aaef8`),
  `NightOverlayWhite.png` (8x8 white sprite, guid `942b28031bcb39644999f62a6a516dd8`, Single).
- Board art (`Assets/Art/quest_map/`): `panel_star.png` = wooden board, 3 embossed star slots
  (**spriteMode 2** in source); `star.png` = gold star (Single); `effect_star.png` = sparkle
  (**spriteMode 2** in source). Copies for runtime loading must be flipped to **spriteMode 1**
  (edit the copied `.meta`: `spriteMode: 2` → `spriteMode: 1`, exactly as was done for
  `Resources/Stars/star.png`).
- UGUI `Image`/`RawImage` **cannot use MaterialPropertyBlock** — instantiate the material and set
  properties on the instance. `LockVignette.shader` (used by MisassemblyLock) is the in-repo
  precedent for a procedural full-screen UGUI overlay on a RawImage.
- On a `ScreenSpaceOverlay` canvas, `unity_ObjectToWorld` positions ARE screen pixels, and a UGUI
  element's `transform.position` is in that same space — so the world-space cutout shader works
  unmodified on a full-screen UGUI overlay, with `_LightData.xy = bearTransform.position` and radii
  in **screen pixels** (scale them by `Screen.height / 1080f` so they hold on any resolution).
- In the night flow, the exact "entrance ended, puzzle begins" boundary is
  `WordAssemblyTimer.Instance.SmokeActive` flipping true — `NightRedoRoutine` calls `BeginFresh()`
  right when the book pops. No wiring needed to detect it.
- `CrowEntranceCutscene` (CutScene_ga) moves the crow prefab's own transform (`transform.position`)
  wp_0 → wp_1 → wp_center; `BearCutscene` (CutScene_bear) likewise walks its own transform. Both are
  found by type at scene load — those transforms are the spotlight targets.
- WorldMap island click = `WorldMapProblemIslands.HandlePlayableIslandClick` →
  `LoadNextSceneRoutine` (fade + `LoadScene`). Island world bounds:
  `TryGetPlayableIslandSpriteBounds` (private).

---

## 1. Single source of quest ids — `NightMode.cs`

v1 duplicated `{"paa","kaa"}` in three files. Move it here:

```csharp
    /// The quests that own a puzzle, in walk order. Must equal each puzzle's targetWordId.
    public static readonly string[] QuestIds = { "paa", "kaa" };
```

Update `WorldMapNight` (rewrite uses it), `NightModeTools` (replace its private array), and leave
`QuestPathSequence.nightQuestIds` serialized as-is (its default already matches).

## 2. Runtime-loadable night assets — `Assets/Resources/Night/`

Create the folder and copy (plain file copy, then fix the copied `.meta` per §0):

| Source | → Resources copy | Import fix |
|---|---|---|
| `Assets/Scenes/region 1/adventure/Shaders/NightDarkOverlay.mat` | `Assets/Resources/Night/NightCutoutOverlay.mat` | none (keeps shader ref by guid) |
| `Assets/Scenes/region 1/adventure/Shaders/NightOverlayWhite.png` | `Assets/Resources/Night/night_white.png` | none (already Single) |
| `Assets/Art/quest_map/panel_star.png` | `Assets/Resources/Stars/panel_star.png` | `spriteMode: 2` → `1` |
| `Assets/Art/quest_map/effect_star.png` | `Assets/Resources/Stars/effect_star.png` | `spriteMode: 2` → `1` |

Do NOT copy the `.meta` files from the source (let Unity mint new guids), EXCEPT you may copy then
hand-edit — either way the copy must get a **new guid** and the stated import settings. The `.mat`
copy keeps working because it references the shader by its stable guid.

Having the material in `Resources/` also guarantees the shader ships in builds regardless of scene
references — after this, `Shader.Find` is not needed anywhere.

## 3. WorldMap night — rewrite `WorldMapNight.cs`

Same self-bootstrap skeleton (RuntimeInitializeOnLoad + sceneLoaded, scene name == `"WorldMap"`,
gate on `NightMode.NightPhase`, guard on `GameObject.Find`). New behaviour:

1. **Dark overlay (world-space, not a canvas)** — mirror the forest's NightDark rig:
   - `SpriteRenderer` on a child GO; `sprite = Resources.Load<Sprite>("Night/night_white")`;
     `sharedMaterial = Resources.Load<Material>("Night/NightCutoutOverlay")`;
     `sortingOrder = 32000`.
   - Position at the camera's XY; scale to cover the view:
     `height = 2f * cam.orthographicSize; width = height * cam.aspect;` white sprite is 8px @ PPU
     100 = 0.08 world units → `localScale = (width/0.08f, height/0.08f, 1) * 1.1f` (10% overscan).
   - Color = forest values: `new Color(0.04f, 0.09f, 0.20f)` with **alpha eased 0 → 0.82f** over
     ~1.5s after the WorldMap fade-in clears (start the fade at `t≈1.7s`, matching v1's delay), so
     the map visibly sinks into night.
   - **Island hole** via MaterialPropertyBlock (SpriteRenderer supports it — same pattern as
     `NightLighting.SyncOverlayOpenings`): slot 0 = island bounds centre,
     `outerR = max(bounds.extents.x, bounds.extents.y) * 1.6f`, `innerR = outerR * 0.45f`,
     `_MinimumDarkness = 0.15f`. Slots 1-3 zero. Re-push the block once after setting color each
     frame of the fade (color writes don't disturb the block, but push once at setup and once at
     the end to be safe).
   - Island bounds: add to `WorldMapProblemIslands`:
     ```csharp
     public bool TryGetPlayableIslandWorldBounds(out Bounds bounds) =>
         TryGetPlayableIslandSpriteBounds(out bounds);
     ```
     `WorldMapNight` polls `FindObjectOfType<WorldMapProblemIslands>()` until `progressApplied`
     makes bounds available (retry in a coroutine for up to ~3s; if it never resolves, log a
     warning and show the overlay with no hole rather than aborting).
2. **No button, no canvas, no click handling.** Delete all of v1's UI + pointer code from this file.
3. **Entry hook** — the island click itself starts the session. In
   `WorldMapProblemIslands.LoadNextSceneRoutine`, right before `SceneManager.LoadScene`:
   ```csharp
             if (!string.IsNullOrWhiteSpace(sceneName))
             {
                 if (NightMode.NightPhase)
                 {
                     // Night: entering the island IS the redo run. Fresh session; drop any stale
                     // wrong-word retry flags so the puzzle takes the fresh-clock path.
                     NightMode.BeginSession(NightMode.QuestIds);
                     MagicStonePuzzleController.ConsumeRetryAfterCrow();
                     MagicStonePuzzleController.ConsumeRetryAfterAlt();
                 }
                 yield return SceneFadeController.Cover(sceneExitCoverDuration);
                 SceneManager.LoadScene(sceneName.Trim());
             }
   ```
   (This file already references `NightMode`; MagicStonePuzzleController is in the same assembly.)

## 4. Forest night — shader +4 slots, hero/quest lights, actors visible from start

### 4.1 Extend the cutout shader to 8 slots
Edit `NightOverlayCutout.shader` **in place** (guid untouched — both existing consumers keep
working; they only write slots 0-3 and unused slots stay `w=0` = ignored):
- Declare `_LightData4.._LightData7`.
- Add four more `dark = min(dark, DarknessFromLight(_LightData4..7, i.world));` lines.
- Update the header comment ("up to four" → "up to eight").

### 4.2 `NightLighting.cs` — dynamic light API
The forest's 4 fixture lights own slots 0-3. Add slots 4-7 for runtime lights:

```csharp
    // Runtime lights (slots 4-7): hero + night-quest pools. Zeroed = off.
    private readonly Vector4[] dynamicLights = new Vector4[4];

    /// slot 0-3 → shader slots 4-7. Pass outerRadius <= 0 to switch the slot off.
    public void SetDynamicLight(int slot, Vector2 worldPos, float innerRadius, float outerRadius)
    {
        if (slot < 0 || slot >= dynamicLights.Length) return;
        dynamicLights[slot] = outerRadius > 0f
            ? new Vector4(worldPos.x, worldPos.y, innerRadius, outerRadius)
            : Vector4.zero;
        SyncOverlayOpenings();
    }
```

In `SyncOverlayOpenings`, after the existing 0-3 loop, push the four dynamic slots (extend the
static `LightDataIds` array to 8 ids). **The property block is re-fetched and fully rewritten each
sync, so all 8 slots must be written every time** — never write only the changed one.

### 4.3 `QuestPathSequence.cs` — night visuals

New serialized fields (defaults only, no scene edits):
```csharp
    [Tooltip("Radius of the light circle following the hero at night (world units).")]
    [SerializeField] private float nightHeroLightRadius = 2.6f;
    [Tooltip("Radius of the light pool on each shown night quest (world units).")]
    [SerializeField] private float nightQuestLightRadius = 3.2f;
```

**`Awake()` night branch** (replaces v1's minimal one):
```csharp
        if (NightMode.RedoActive)
        {
            SwapCrops();   // the day run healed them; the scene reloads blighted

            // Show every offerable quest from the very start — actor only, NO "!" marker.
            // The returned quest (ActiveQuest) is shown too, so it can fade off on resume.
            string active = NightMode.ActiveQuest;
            for (int i = 0; i < nightQuestIds.Length; i++)
            {
                string id = nightQuestIds[i];
                bool offerable = QuestStars.NeedsRedo(id) && !NightMode.IsDoneThisNight(id);
                if (offerable || id == active)
                {
                    ShowQuestActors(id);   // v1 helper: ShowQuest1Actor / ShowResumeCrows
                }
            }
        }
```
(`nightLighting.SetNight(RedoActive ? 1f : 0f)` from v1 stays.)

**`Start()` night side**: before starting `RunNightRedo`, spawn boards + start crow flight + quest
light pools for every shown quest (must be in Start, not Awake — coroutines and fade state):
- For each shown quest id: `QuestStarBadge.Spawn(anchor, offset, QuestStars.Get(id))` (§5) and
  `nightLighting.SetDynamicLight(1 + index, anchorXY, r*0.35f, nightQuestLightRadius)`
  (slot 1+i → shader slots 5,6; slot 4/shader-slot-4 is reserved for the hero).
- Board/light **anchors**: `"paa"` → the bear (`villager.transform`); `"kaa"` → the detached crow
  mark position (`crowMarkRoot.transform`, world ~(31.9, 13.4)) **without activating it** — a fixed
  point, because the crows fly. If `kaa` is shown, also `StartCrowFlight()` so the crows patrol.

**Hero light** — extend the existing `LateUpdate` (it already handles facingLock):
```csharp
        if (NightMode.RedoActive && nightLighting != null && body != null)
        {
            nightLighting.SetDynamicLight(0, body.position,
                nightHeroLightRadius * 0.35f, nightHeroLightRadius);
        }
```

**`RunNightRedo()` revisions** (v1's structure survives; deltas only):
- Delete the `RevealQuestActors` step entirely (actors are visible from Awake). Keep `WalkToQuest`,
  the hold, ActiveQuest set, retry-flag consume, cover + load. Also delete v1's `RevealQuestActors`
  method and its `ShowStarBadge` coroutine call at arrival (boards spawn in Start now). `Vibrate()`
  on arrival stays.
- On resume (`returned` != empty): actor + board are already visible from Awake/Start. After the
  reveal completes: `GameAudio.PlayAfterQuest()`, hold `questAutoHold`, then fade out the actor
  (`FadeOutQuestActors(returned)` — v1 helper) **and its board**
  (`QuestStarBadge.FadeOut(returned)`), and switch off its quest light
  (`nightLighting.SetDynamicLight(1 + IndexOfQuest(returned), default, 0f, 0f)`).
- When loading a puzzle scene or the WorldMap, call `QuestStarBadge.HideAll()` first (already in
  `OnDisable` from v1 — keep that too).
- The rest (loop, WalkHome, EndSession → WorldMap) stays. Note per the all-3★ decision: entering
  with nothing offerable simply walks wp_1 → wp_2 → home — the existing loop + WalkHome already do
  this because `WalkToQuest` runs unconditionally.

## 5. Star board — rewrite `QuestStarBadge.cs`

Composite, world-space, runtime-built. Keyed by quest id so a single board can be faded per quest:

```csharp
public static class QuestStarBadge
{
    // panel_star is 1536x1024 (verify actual size on import); at PPU 100 scale ~0.14 => ~2.1 world
    // units wide. Slot centres sit at roughly x = -0.29, 0.0, +0.29 of panel width (fractions
    // 0.253 / 0.5 / 0.747 measured off the art) and y ≈ -0.03 — expose these as consts and eyeball
    // in-editor; the art is symmetric so the middle one is exact.

    public static void Spawn(string questId, Transform anchor, Vector3 offset, int filledStars) { ... }
    public static IEnumerator FadeOut(string questId, float duration) { ... }
    public static void HideAll() { ... }
}
```

Build order per board (all `SpriteRenderer`, all above the dark overlay):
1. Root GO at `anchor.position + offset` (reuse `nightBadgeOffset`, default `(0, 1.6f, 0)`).
2. Panel: `Resources.Load<Sprite>("Stars/panel_star")`, `sortingOrder = 32100`, scaled to ~2.1
   world units wide.
3. Per earned slot `i < filledStars`: sparkle `Stars/effect_star` at the slot centre,
   `sortingOrder = 32101`, then gold `Stars/star` on top, `sortingOrder = 32102`, sized to fit the
   slot circle (~55% of slot pitch). Slots beyond `filledStars` stay empty — the embossed art IS
   the empty state.
4. Fade the whole board's sprites 0→1 over ~0.5s on spawn (coroutine on a hidden runner
   MonoBehaviour, or spawn from QuestPathSequence and let IT run the fade — simpler: `Spawn`
   returns the root and QuestPathSequence starts the fade coroutine; pick one and keep it small).
5. A gentle idle float is optional — `MarkerBob` (already in Common/) can be added to the root
   with small amplitude if it drops in cleanly; skip if it fights anything.

**Sorting**: 32100+ sits above `NightDark` (32000) → the board is never darkened; it glows out of
the night by construction. The quest light pool (§4.3) is what lights the actor beneath it.

## 6. Cutscene night — rewrite `NightTintOverlay.cs`

Keep the file name, self-bootstrap skeleton, `NightMode.RedoActive` gate, and scene list. New
behaviour splits by scene:

- **`CutScene_bear`, `CutScene_ga`** → dark + spotlight-follow, then settle to flat dim.
- **`Success_pa`, `Success_ga`, `Success_ga_correct`, `Success_ta_incorrect`** → flat dim only
  (v1 behaviour, alpha `0.38f`).

Spotlight implementation:
1. Full-screen **RawImage** on a `ScreenSpaceOverlay` canvas, `sortingOrder = 9000`,
   `raycastTarget = false`, `color = new Color(0.03f, 0.05f, 0.14f, 0.85f)` (spotlight phase is
   darker than the flat dim — that is the point of the beat).
2. `material = new Material(Resources.Load<Material>("Night/NightCutoutOverlay"))` — an
   **instance** (UGUI has no property blocks). Destroy the instance in `OnDestroy`.
3. Find the follow target after scene load:
   `FindObjectOfType<BearCutscene>()?.transform ?? FindObjectOfType<CrowEntranceCutscene>()?.transform`
   (bear scene has no CrowEntranceCutscene and vice versa). Null target → skip straight to flat dim.
4. **Every frame while spotlighting**: `material.SetVector("_LightData0", new Vector4(
   target.position.x, target.position.y, r * 0.4f, r))` where
   `r = Screen.height * spotRadiusFactor` (`spotRadiusFactor = 0.42f`, serialized default). Also
   `material.SetFloat("_MinimumDarkness", 0.1f)`. RawImage UVs/vertex color drive the tint exactly
   like a sprite. (Positions are screen pixels — see §0.)
5. **Settle trigger**: when `WordAssemblyTimer.Instance != null && WordAssemblyTimer.Instance.SmokeActive`
   first becomes true (the book popped → puzzle begins), run the settle: over ~0.8s, lerp the
   overlay color alpha `0.85f → 0.38f` AND grow the hole radius to `Screen.width * 2f` (the hole
   swallows the screen — visually the circle "fades away"), then zero `_LightData0` and stop
   updating. The flat `0.38f` dim remains for the whole puzzle.
6. Success scenes: same overlay, no target, alpha fixed `0.38f`, `_LightData0` zero. (One component
   handles both modes; a `bool spotlightMode` picked off the scene name.)

Fail-safe: if `SmokeActive` never fires within 90s (edge: quest opened without the timer), settle
anyway.

## 7. Editor tools — `NightModeTools.cs` touch-up

- Replace its private `QuestIds` with `NightMode.QuestIds`.
- Menus stay the same otherwise. (The reset already clears everything relevant.)

---

## 8. File summary (v2 deltas)

**Edited**
- `Assets/Scenes/region 1/adventure/Shaders/NightOverlayCutout.shader` — 4 → 8 light slots (§4.1)
- `Assets/Scripts/Region1/ReferenceForest/NightLighting.cs` — `SetDynamicLight` + 8-slot sync (§4.2)
- `Assets/Scripts/Common/NightMode.cs` — add `QuestIds` (§1)
- `Assets/Scripts/WorldMap/WorldMapProblemIslands.cs` — bounds accessor + night session hook in
  `LoadNextSceneRoutine` (§3)
- `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs` — Awake shows actors, Start spawns
  boards/lights, LateUpdate hero light, RunNightRedo trims reveal step (§4.3)
- `Assets/Editor/NightModeTools.cs` — use `NightMode.QuestIds` (§7)

**Rewritten**
- `Assets/Scripts/WorldMap/WorldMapNight.cs` (§3)
- `Assets/Scripts/Common/NightTintOverlay.cs` (§6)
- `Assets/Scripts/Region1/ReferenceForest/QuestStarBadge.cs` (§5)

**New assets** (§2)
- `Assets/Resources/Night/NightCutoutOverlay.mat`, `Assets/Resources/Night/night_white.png`
- `Assets/Resources/Stars/panel_star.png`, `Assets/Resources/Stars/effect_star.png`
  (both metas flipped to `spriteMode: 1`)

**Unchanged** — everything else from v1 (see "What stays").

## 9. Test plan

1. Compile clean (`unity_get_compilation_errors`); the two existing shader consumers still render
   (open reference_forest, `Tools/Quest/Night Preview On` → fixtures still cut holes → `Off`,
   verify NightDark alpha back to 0 before any save — per the project memory).
2. `Tools/Night/Simulate Day Done (2★ + 3★)` → open WorldMap, play: map fades to proper darkness
   with **only Island2 lit**, no star icon anywhere, unlock animation does NOT replay. Click the
   island → forest.
3. Forest: already night at frame one; **bear visible immediately with the wooden 3-slot board
   showing 2 gold stars + sparkles**, no "!" anywhere; a light circle tracks the hero as it walks;
   a light pool sits on the bear. The crow spot shows nothing (3★).
4. Enter CutScene_bear: screen is dark with a light circle **following the bear** as it walks in;
   no owl; the moment the book pops, the circle melts away into a uniform dim; puzzle plays
   normally (clock 30s, smoke normal, stones readable).
5. Build ปา fast → 3⭐ → Success_pa is flat-dim → back to forest (night): bear + board fade off,
   hero walks past the crow spot (nothing shown), walks home, enters the house → WorldMap still
   night, island still lit (nothing left to redo, but per the decision the island stays enterable).
6. `Tools/Night/Simulate Day Done (2★ + 2★)` → forest shows BOTH quests (bear board + crow board at
   the crow-mark spot with crows flying), chains bear → crow → home.
7. Day regression: `Tools/Night/Reset Night + Stars` → full day run → boards never appear, no
   overlays anywhere, night falls only at Beat 6 as always.

## 10. Gotchas

1. **Write all 8 shader slots on every property-block sync** (§4.2) — the block is rebuilt from the
   renderer each call; a partial write leaves stale light positions.
2. **UGUI ≠ property blocks.** The cutscene overlay must instantiate its material; destroy the
   instance on scene unload or it leaks into the next scene's material list.
3. **Screen-pixel radii.** The cutscene spotlight radius is in actual screen pixels, not reference
   resolution — always derive from `Screen.height`.
4. **The copied PNGs must become Single sprites** (`spriteMode: 1` in the copied meta) or
   `Resources.Load<Sprite>` returns only the first sub-sprite of a Multiple sheet (or null).
5. **Do not activate the crow mark** (`crowMarkRoot`) when using it as the kaa board anchor — read
   its transform only. Activating it shows the "!" the user explicitly removed.
6. **Board must sit at sortingOrder ≥ 32100** — anything under NightDark's 32000 is swallowed by
   the darkness.
7. **BearIntroSequence + NightLighting share the shader.** The 8-slot extension must keep slots 0-3
   semantics identical; do not renumber, do not change `DarknessFromLight`.
8. **The WorldMap overlay must not intercept clicks** — it is a SpriteRenderer with no collider;
   never add a collider or a canvas to it. Island clicks go through
   `WorldMapProblemIslands.HandlePlayableIslandClick` (screen-point → bounds test) unaffected.
9. **Stale retry flags** (v1 gotcha, still live): they are consumed at the island click now (§3) —
   keep the consume calls in `RunNightRedo`'s pre-LoadScene step too (belt and braces, they're
   idempotent).
10. **v1's star button code must be fully gone** — if a blinking star still appears on the night
    map, `WorldMapNight` was not actually rewritten.
