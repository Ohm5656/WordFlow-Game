# Island2 → reference_forest fade transition + runtime object hiding

Date: 2026-06-20

## Goals

1. **reference_forest**: hide three objects when Play is pressed — `bird6_16x20_6`,
   `ExhaustedVillager_Lying`, and `QuestMarker_Exclamation (1)` (the exact name, distinct from
   the bare `QuestMarker_Exclamation`). They stay visible in the editor; only hidden at runtime.
2. **WorldMap**: clicking the playable island (`Island2`) loads `reference_forest` with a black
   fade-out (cover) on WorldMap, then a black fade-in (reveal) in reference_forest. The level is
   only reachable once the island is unlocked (existing gating). The reveal must complete fully
   before gameplay (the scripted `QuestPathSequence` intro) begins.

## Existing systems

- `WorldMapFadeIn` / `QuestMapFadeIn`: each self-bootstraps on scene load (RuntimeInitializeOnLoad
  + `SceneManager.sceneLoaded`), creates a full-screen black `Canvas` (sortingOrder 10000) at
  alpha 1, and eases it to 0 (reveal). Neither does a cover (fade-to-black) on exit.
- `WorldMapProblemIslands`: already gates play — only the highest *unlocked* island becomes
  clickable; locked islands have their `ClickArea` disabled. The click runs `LoadNextSceneRoutine()`,
  which currently calls `SceneManager.LoadScene` directly.
- `QuestPathSequence`: reference_forest's scripted intro, started from `Start()`.
- `reference_forest` is NOT in `EditorBuildSettings` (guid `32210b23d8c9a974aa3e9371256251af`).
- `Island2` in `WorldMap.unity` has `SceneName: quest_map1` (to be re-pointed).

## Design

### Reusable fade controller — `SceneFadeController` (new runtime script)
Owns a black full-screen overlay (same canvas setup as the existing fade scripts). Provides:
- `static IEnumerator Cover(float duration)` — creates the overlay and eases alpha 0→1; leaves it
  up so the caller can `LoadScene` while black. Used by the WorldMap exit.
- A self-bootstrapping **reveal** for `reference_forest` (RuntimeInitializeOnLoad + `sceneLoaded`,
  matching the existing scripts): on entering reference_forest, sets `RevealComplete = false`,
  creates the overlay at alpha 1, eases to 0, then sets `RevealComplete = true`.
- `static bool RevealComplete` — defaults **true** (so gameplay is never permanently blocked if no
  fade runs); set false the moment a reference_forest reveal begins, true when it finishes. The
  `sceneLoaded` handler sets it false before `QuestPathSequence.Start` runs (Unity order:
  Awake → OnEnable → sceneLoaded → Start), so the gate is armed in time.

The existing `WorldMapFadeIn` / `QuestMapFadeIn` are left untouched (no scope creep).

### WorldMap exit (`WorldMapProblemIslands`, edit)
In `LoadNextSceneRoutine()`, after the existing gated click + `loadDelayAfterClick`, run
`yield return SceneFadeController.Cover(coverDuration)` before `SceneManager.LoadScene`. Gating is
preserved by construction: this routine is only reachable from the unlock-gated click path.

### Re-target + build settings
- `WorldMap.unity`: `Island2` `SceneName` `quest_map1` → `reference_forest` (one-line scalar edit).
- `EditorBuildSettings.asset`: add `Assets/Scenes/region 1/reference_forest.unity`.

### Gate gameplay (`QuestPathSequence`, edit)
At the top of `RunSequence()`, `yield return new WaitUntil(() => SceneFadeController.RevealComplete)`
before any `startDelay` / movement, so the reveal finishes fully before the hero moves.

### Hide three objects (`ReferenceForestPlayHider`, new runtime script)
Self-bootstraps on reference_forest load (same pattern), walks the scene roots (incl. inactive),
and `SetActive(false)` on the three exact-named objects. Runs at `AfterSceneLoad`, before the first
render; the black reveal overlay also covers any momentary flash. No scene wiring required.

## Why no in-editor scene wiring
All three reference_forest behaviors self-bootstrap from scene name, so the only edits to versioned
assets are the one-line scene re-point and the build-settings entry — avoiding the domain-reload
scene-edit fragility noted for this project.

## Out of scope
- Refactoring the two existing fade-in scripts.
- Any change to locked-island behavior or the unlock animation.
