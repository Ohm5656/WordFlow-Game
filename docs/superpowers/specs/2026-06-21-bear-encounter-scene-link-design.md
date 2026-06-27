# Bear Encounter Scene Link — reference_forest ↔ word_build_paa_polished

**Date:** 2026-06-21
**Branch:** feat/word-build-encounter-prototype

## Goal

Turn the first quest beat in `reference_forest` (the `ExhaustedVillager_Sitting`
quest) into the entry point for the `word_build_paa_polished` bear encounter.

- **Visual:** the first quest actor shows a **bear** instead of the villager.
- **Old flow:** walk up to the quest point → actor reveals → fades out in place → walk on.
- **New flow:** walk up → bear + "!" reveal → fade to black → load
  `word_build_paa_polished` → player builds the word (bear flees on success) →
  fade back → resume `reference_forest` at Beat 2, bear now gone.
- Quests 2–5 and everything else are unchanged.

## Approach

Cross-scene state is kept in a small static holder (`BearEncounterFlow`), the
same pattern the project already uses (`MagicStonePuzzleController.IsRetryAfterCrowRequested`).
No PlayerPrefs, no additive scenes.

## Components

### 1. Villager → bear (scene data)
In `reference_forest`, the quest-1 actor's `SpriteRenderer` (the one wired to
`QuestPathSequence.villager`) gets its sprite set to `Assets/Art/quest_map/bear.png`.
The GameObject, hierarchy, and the "!" marker stay as-is. Scale may need a small
tuning pass.

### 2. `BearEncounterFlow` (new static class)
Two flags, surviving scene loads (reset on domain reload / play stop):
- `ReturnToForest` — set true by the forest before loading the encounter; tells
  the encounter "you were entered from the forest, return there on success."
- `ResumeAtBeat2` — set true while returning; tells the forest to skip Beat 1.

### 3. `QuestPathSequence` — Beat 1 only
- **First run:** walk to wp_1 → face wp_2 → reveal bear + "!" → brief hold (bear
  stays visible) → `SceneFadeController.Cover()` → set `ReturnToForest = true` →
  `SceneManager.LoadScene("word_build_paa_polished")` → end the coroutine.
- **Resume run** (`ResumeAtBeat2`): clear the flag → teleport the body to wp_1
  (where the bear stood) → bear is already hidden by `HideAtStart` (= "disappeared")
  → fall straight into Beat 2.
- Beats 2–5 untouched. Still gated behind `WaitUntil(SceneFadeController.RevealComplete)`.

### 4. `WordBuildEncounterController` — exit
- On a **correct** build (`Phase.Done`, after the outro/bear-flee effect): if
  `BearEncounterFlow.ReturnToForest` is true → set `ResumeAtBeat2 = true`, clear
  `ReturnToForest`, `SceneFadeController.Cover()`, `LoadScene("reference_forest")`.
- If `ReturnToForest` is false (scene opened standalone for testing) → behave as
  today (stay on Done, no scene load).
- Wrong-word / non-word still loop back inside the scene; no return.

### 5. `SceneFadeController` — symmetric reveal
Add `word_build_paa_polished` to the self-revealing scene allowlist so entering it
fades in smoothly (today only `reference_forest` self-reveals). Both directions
fade cleanly.

### 6. Build Settings
Add `Assets/Scenes/region 1/adventure/word_build_paa_polished.unity` (enabled) so
`LoadScene("word_build_paa_polished")` resolves in builds.

## Edge cases
- Encounter opened directly (no forest): `ReturnToForest` false → no auto-return.
- Re-entering forest re-runs `ReferenceForestPlayHider` (idempotent) and the
  forest reveal (black → clear), so the hero never moves under black.
- Only a *correct* build returns; failures keep the existing in-scene loop.

## Out of scope
Quests 2–5, the word-build encounter internals, and all other scenes/flows stay
exactly as they are.
