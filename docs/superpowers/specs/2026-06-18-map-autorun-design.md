# Map Auto-Run — Design & Build Spec (for the new teammate)

**Date:** 2026-06-18
**Status:** SPEC — not yet built. Written for a teammate using Claude Code who is **new to
this repo**. Read this top to bottom before writing any code.
**Owner of this task:** (new teammate)
**Depends on / hands off to:** the map-scene author (owns `map` scene, camera, progression).
**Relates to:** `wordflow-region-1.md` (vault — the Region 1 trail design),
`2026-06-17-encounter-cutscene-design.md`, `2026-06-14-word-build-encounter-design.md`.

---

## TL;DR (read this first)

Build the thing that **auto-walks the player along the Region 1 trail and, on reaching an
encounter stop, transitions into that encounter's scene.** The player has **no movement
control** — they just walk forward and stop at each of the day's 5 encounters.

You build it as a **self-contained Player prefab + 3 small scripts, in your OWN test scene,
on your OWN git branch.** You do **NOT** open or edit the map scene. The map author drops
your prefab into their scene at integration time. This is the one rule that keeps your work
from corrupting theirs (see "Why the isolation rule" below).

---

## Problem

Region 1 is **one linear top-down trail**. The character **auto-moves forward** (zero
movement control is a locked design rule — the audience is young LD children) and stops at
**5 encounter points per in-game day**. Each encounter is a **separate Unity scene** that we
`SceneManager.LoadScene(...)` into. Today there is **no map and no way to reach an
encounter** — encounters only run as standalone scenes. Your job is the missing piece: the
walking + the hand-off into an encounter.

## Goals

- The player **auto-walks** along a designer-placed trail of waypoints, playing a
  walk-cycle animation, facing the direction of travel.
- At an **encounter stop**, the player **halts**, plays idle, and **transitions into that
  encounter's scene** (brief fade → `LoadScene`).
- **Resumable:** the runner can **start at waypoint N**, so "resume where you left off
  today" works (the *value* of N comes from save state you do NOT own — you just accept it).
- Delivered as a **droppable prefab + scripts**, integrated by the map author with **zero
  edits to the map scene by you**.
- Fits the repo's conventions: `sealed class : MonoBehaviour`, `[SerializeField]` tunables
  under `[Header]`, self-wiring by serialized ref with name-lookup fallback, coroutine-based
  motion, null-safe / degrade-don't-throw.

## Non-goals (explicitly NOT your task — do not build these)

- **Camera.** The map author owns camera-follow. Use a throwaway camera in your test scene
  only.
- **Progression / save state / "which day" / auto-save at nightfall.** Someone else owns
  PlayerPrefs and day logic. You only expose a `startWaypointIndex` they set.
- **The encounter scene itself** — already exists (`word_build_*` scenes). You only load it.
- **Returning from the encounter back to the map** — out of scope (a separate integration
  task). Just make sure your runner *can* resume at an index so that return is possible
  later.
- **Scenery, recolor, fog, the owl guide companion, scene transitions' art polish.**
- **Multiple directions / pathfinding / player input of any kind.** One forward trail.

If you find yourself building any of the above, stop — you've left your lane.

## Locked decisions (already settled — do not re-litigate)

| Decision | Choice | Source |
|---|---|---|
| Map → encounter hand-off | **Separate scene** via `SceneManager.LoadScene` | user, 2026-06-18 |
| Trail shape | **Designer-placed waypoint markers** (not a straight scroll) | user, 2026-06-18 |
| Camera ownership | **Map author**, not auto-run | user, 2026-06-18 |
| Player movement control | **None** — pure auto-walk (locked game rule) | `wordflow-region-1.md` |
| View | **Top-down** travel | `wordflow-region-1.md` |
| Encounters per day | **5** stops along the trail | `wordflow-region-1.md` |
| Assembly | `WordFlow.Adventure` (new `Map/` folder, no new asmdef) | repo convention |

---

## Inputs you must confirm BEFORE coding

1. **Player walk-cycle sprite.** You were told real sprites exist, but as of 2026-06-18 the
   repo's `Assets/Art/` only has **owl** (`visaul_novel/character/owl/owl_01..10.png`, a
   10-frame sequence) and **bear** (cutscene). There is **no top-down player walk-cycle**
   committed yet. **Ask the user / map author where the player walk-cycle art is.** Until you
   have it, develop against a **placeholder** (a colored square `SpriteRenderer` with a
   simple bob) — the prefab swap-in is trivial later, so this does not block you.
   - The repo's reference pattern for a multi-frame character is the owl: a **numbered PNG
     sequence** animated either by an `Animator` clip (see `bear_root.controller` +
     `Bear_Entrance.anim`) or a tiny frame-cycling coroutine. Mirror whichever matches how
     the player art is delivered (sprite sheet vs. loose frames).
2. **The encounter scene name(s)** to load. The prototype scenes are
   `word_build_prototype` and `word_build_paa_polished`. For your test, wire a marker to
   `word_build_paa_polished`. **Any scene you load must be added & enabled in Build
   Settings** or `LoadScene` fails silently — confirm with the map author who maintains the
   Build Settings list.

If either is unclear, **ask before building** — don't guess and don't invent a folder path.

---

## The interface (the ONE seam between you and the map author)

Keep the contract this small. Everything flows through it.

```
WHAT YOU OWN (prefab + scripts):
  PlayerAutoRunner  — walks the waypoints, animates, stops at encounters, fires the hand-off.

WHAT THE MAP AUTHOR OWNS (lives in the map scene):
  - a TrailPath holding the ordered list of waypoints for the current day
  - an EncounterMarker on each of the 5 encounter waypoints, carrying that encounter's
    target scene name
  - the camera, and the startWaypointIndex value (from save state)
  - adding the encounter scenes to Build Settings

THE HAND-OFF (what your runner does on arrival at an encounter waypoint):
  1. stop, play idle
  2. raise  public UnityEvent<EncounterMarker> OnEncounterReached  (so save-progress code
     can hook in later — you don't implement that hook)
  3. brief fade, then SceneManager.LoadScene(marker.TargetSceneName)
```

That's it. The map author never needs to read your code; you never need to read theirs.

---

## Components to build (3 small scripts + 1 prefab)

Put scripts in a **new folder** `Assets/Scripts/Adventure/Map/`. They compile into the
existing `WordFlow.Adventure` assembly (no new asmdef). All `sealed class : MonoBehaviour`.

### 1. `EncounterMarker.cs`
A marker the map author puts on each encounter waypoint.
```csharp
[SerializeField] private string targetSceneName;   // bare scene name, e.g. "word_build_paa_polished"
public string TargetSceneName => targetSceneName;
// optional: [SerializeField] string encounterId;  // for later save-state use
```
Nothing else. It's a data tag.

### 2. `TrailPath.cs`
Holds the **ordered** waypoint list for the current day. Lives in the map scene, owned by
the map author.
```csharp
[SerializeField] private List<Transform> waypoints = new();   // in walk order
public IReadOnlyList<Transform> Waypoints => waypoints;
// Optional editor nicety: OnDrawGizmos draws lines between waypoints so the trail is visible.
```

### 3. `PlayerAutoRunner.cs` (the core)
```csharp
[Header("Wiring")]
[SerializeField] private TrailPath path;            // self-wire: if null, FindObjectOfType<TrailPath>()
[SerializeField] private SpriteRenderer sprite;     // for facing flip; null-safe
[SerializeField] private Animator animator;         // optional; null-safe (placeholder has none)

[Header("Movement")]
[SerializeField] private float walkSpeed = 2.5f;          // units/sec — tune in play
[SerializeField] private float arriveThreshold = 0.05f;   // distance to count as "arrived"
[SerializeField] private int startWaypointIndex = 0;      // resume support (set by map author)

[Header("Transition")]
[SerializeField] private float fadeSeconds = 0.4f;

public UnityEvent<EncounterMarker> OnEncounterReached;     // raised before the scene loads
```
Behaviour (one coroutine, MoveTowards-style):
- Start at `startWaypointIndex`. For each subsequent waypoint:
  - while not within `arriveThreshold`: `MoveTowards` it at `walkSpeed`, set animator
    `IsMoving=true` (null-safe), **flip `sprite.flipX`** based on horizontal direction of
    travel (top-down walk-cycles are usually side-facing; if the art is 4-directional, leave
    a TODO and keep flip-X as the minimal version — note it as a tunable, don't over-build).
  - on arrival: set `IsMoving=false`. If the waypoint has an `EncounterMarker`:
    1. play idle, 2. `OnEncounterReached?.Invoke(marker)`, 3. `StartCoroutine(FadeAndLoad(marker))`,
    4. **stop the walk loop** (the scene is leaving).
  - else continue to the next waypoint.
- `FadeAndLoad`: spawn a high-sorting full-screen black `Image` on its own
  `ScreenSpaceOverlay` canvas (the repo does transition overlays exactly this way — see
  `MagicStonePuzzleController` white-flash), `Lerp` alpha 0→1 over `fadeSeconds`, then
  `SceneManager.LoadScene(marker.TargetSceneName)`. Guard against an empty scene name
  (log + don't load — degrade, don't throw).
- **Null-safety:** missing `path`/empty waypoints → log a warning and idle, don't throw.

**No Core/unit-test layer required here.** This is simple `MoveTowards` glue; per the
precedent in `2026-06-17-encounter-cutscene-design.md` ("a list index does not earn a
unit-tested model"), do not add a `WordFlow.Adventure.Core` model or test for it. (If you
*want* one, the only test-worthy nugget is a pure "advance toward target by speed*dt" helper
— optional, not asked for.)

### 4. `Player.prefab`
`Assets/Prefabs/Adventure/Player.prefab` — a GameObject with `SpriteRenderer` +
`PlayerAutoRunner` (+ `Animator` once real art lands). This is the single thing the map
author drops into their scene.

---

## Build plan (ordered — your Claude Code can execute this)

Work in **`word_build`-style isolation**: your own branch, your own test scene.

```
0. git checkout -b feat/map-autorun         → verify: `git branch --show-current` is feat/map-autorun
1. Read this whole spec + skim wordflow-region-1.md (vault) "Map" section.
2. Create your test scene: Assets/Scenes/region 1/adventure/autorun_testbed.unity
   (NOT the real map scene). Add a Camera + a Directional Light.   → verify: scene opens, renders.
3. Write EncounterMarker.cs, TrailPath.cs in Assets/Scripts/Adventure/Map/.
   → verify: mcpforunity://editor/state is_compiling == false, then read_console(types=["error"]) is clean.
4. Write PlayerAutoRunner.cs.   → verify: compiles clean (same check as step 3).
5. In the testbed: place ~5 empty GameObjects as waypoints in a curving line; put a
   TrailPath on one object listing them in order; add an EncounterMarker to the last one
   with targetSceneName = "word_build_paa_polished".
6. Build the Player: placeholder SpriteRenderer (colored square) + PlayerAutoRunner, wire
   path. Save as Assets/Prefabs/Adventure/Player.prefab.   → verify: prefab created.
7. Add word_build_paa_polished to Build Settings (test only).
8. Enter Play mode.  → VERIFY (the success criteria, below) with a screenshot via
   manage_camera(action="screenshot").
9. Swap placeholder for real walk-cycle art ONCE you have it (Animator IsMoving bool or
   frame-cycler). Re-verify walk + flip look right.
10. Commit on your branch. Hand the prefab + scripts to the map author. Do NOT merge into
    the map scene yourself.
```

## Success criteria (loop until ALL are true — don't claim done before)

Run it in Play mode and confirm by **watching**, not by assuming:
1. The player **walks waypoint-to-waypoint** along the trail at a steady speed, smoothly.
2. The walk **animation plays while moving** and **stops (idle) on arrival** (with real art;
   placeholder shows the bob).
3. The sprite **faces the travel direction** (flips left/right correctly).
4. On reaching the **encounter waypoint**, the screen **fades** and the game **loads
   `word_build_paa_polished`** (you land in the encounter).
5. Setting `startWaypointIndex = 2` makes the player **start at the 3rd waypoint** (resume
   works).
6. Empty/missing path or empty scene name **logs a warning and does not crash** (Console has
   no red exceptions).
7. You **never opened or saved `map.unity`** (`git status` shows only your new files +
   testbed scene).

---

## Why the isolation rule (don't skip this)

Unity `.unity` and `.prefab` files are **effectively unmergeable in git** — if you and the
map author edit the map scene at the same time, you don't get a clean conflict, you can get a
**silently corrupted scene** (lost objects, broken references). So: you build in your own
test scene and hand over a prefab; the map author integrates it in a single controlled step.
Branches are cheap; a corrupted scene the day before the demo is not.

## Coordination checklist (with the map author)

- [ ] Agree the seam above (TrailPath + EncounterMarker(targetSceneName) + Player prefab +
      OnEncounterReached) — that's the whole contract.
- [ ] Confirm where the **player walk-cycle art** lives (or that you start on placeholder).
- [ ] Confirm **who adds encounter scenes to Build Settings** (the map author maintains it).
- [ ] You stay on `feat/map-autorun`; they stay on their branch. The prefab lands in
      `map.unity` exactly once, by them.

## Open questions to raise with the user before/while building

1. Where is the top-down **player walk-cycle** art? (Not in the repo as of 2026-06-18.)
2. Is the walk-cycle **side-facing** (flip-X is enough) or **4-directional** (needs a small
   direction→clip selector)? Affects step 9 only.
3. Final **encounter scene naming** for real days (test uses `word_build_paa_polished`).
```

