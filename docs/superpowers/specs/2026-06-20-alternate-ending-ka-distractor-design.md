# Alternate Ending — ก Distractor → กา Reveals Its Meaning (Design)

**Date:** 2026-06-20
**Branch:** `feat/word-build-encounter-prototype`
**Scope:** Add a single curated **distractor stone (ก)** to the ปา word-build encounter so the
child can build **กา (crow)** — a real-but-wrong word — and turn that build into a gentle,
non-punishing teaching beat. Adventure Redesign only; targets
`word_build_paa_polished.unity` and the `paa`/`kaa`/`stone_ko`/`e_paa_wrong_effect` assets.
Does **not** touch the old quest flow or `word_build_prototype.unity`.

Source of intent: vault `wordflow-game-design.md` "Planned evolution (2026-06-20) —
trial-and-error: a wrong word reveals its meaning" (lines 226–246) and memory
`change-backlog-2026-06-20` item #6. This is the one backlog item that **changes the
as-built loop**, which is why it gets its own spec.

## Goal

Make a mistake *teach* instead of just resetting. Today the ปา tray holds only ป + า, so a
real-but-wrong word (`WrongWord`) is **unreachable** — the child can only build ปา or a
non-word. Adding a ก stone makes **กา (crow)** reachable. Building it plays a reveal that is
**structurally symmetric to the correct path** (a stone/figure pops, then an effect cutscene),
but with a *different, harmless* effect: the crow appears and **deals nothing**, the bear is
present, and the **owl gently says "let's try another one."** Then the child returns to Build.
The encounter stays unwinnable until ปา is built — but **no attempt is wasted and the child is
never made to feel wrong.**

## Behaviour (the loop change)

Tray becomes **ป  า  ก** (two slots, three tiles — the third is a distractor). Branch by what
is built (unchanged decision rule — *what was built*, never pronunciation):

> **Correct (ปา):** ปา stone pops → `outroCutscene` (success FX) → "+1 level". *(unchanged)*
>
> **Wrong-but-real (กา):** post-build echo voices **"กอ อา กา"** → mic → **กา crow figure
> pops** (the `RevealStone` beat, mirroring the success stone) → **`wrongEffectCutscene`**
> plays the *harmless* crow effect: crow shown "on top, dealing nothing", the bear present,
> the **owl says "let's try another one"** → back to Build. No heart/penalty, no embarrassment.
>
> **Non-word (ปก, กป, …):** black smoke + soft SFX → back to Build. *(unchanged)*

This matches the existing `ResolveBuild` real-but-wrong branch almost exactly — it already pops
the built word's stone and plays `wrongEffectCutscene`, then returns to Build. The work is
making กา **reachable** and giving it **content**; the branch logic itself barely changes.

## Components

### 1. Core (TDD) — decouple slot count from tray count
`EncounterModel` currently forces `SlotCount == TrayCount` (`_slotToTray = new int[trayCount]`).
A distractor needs **3 tray tiles but 2 slots**. Add an overload:

```csharp
public EncounterModel(IReadOnlyList<string> trayGraphemes, int slotCount)
```

- Keep the existing 1-arg constructor delegating with `slotCount = trayGraphemes.Count`, so
  **every existing caller and test is byte-for-byte unchanged**.
- `slotCount` must be `>= 1` and `<= trayGraphemes.Count` (guard with `ArgumentException`).
- Tiles at tray indices `>= slotCount` worth of "extra" are just ordinary tray tiles that
  happen never to be required; placement/return/BuiltString logic is already index-based and
  needs no further change.

**New tests** (`EncounterModelTests`):
- 3 tray tiles / 2 slots: placing the two target tiles → `IsComplete`, `BuiltString` = target.
- Building the wrong word with the distractor (place ก then า) → `IsComplete`, `BuiltString` = "กา".
- Distractor left in tray → still completable with the correct two; returning a slot frees the
  right tile.
- Constructor guards: `slotCount` 0 or `> trayCount` throws.

### 2. Data — activate the reserved distractor list
`EncounterConfig` already reserves it in a comment. Make it real:

```csharp
public List<StoneTileData> distractorTiles = new List<StoneTileData>(); // extra tray tiles, never required
```

Empty list = today's behaviour exactly (no distractor). No other config change.

### 3. Controller — compose tray from target + distractors
`WordBuildEncounterController` currently indexes tile data as `config.target.tiles[trayIndex]`
in four places (`OnTileTapped`, `OnTileHovered`, `MakeTile`, `PlayPostBuildEcho` fallback).
With distractors appended, `trayIndex` can exceed `target.tiles.Count`, so:

- Build one combined, ordered list once per encounter:
  `_trayData = target.tiles + config.distractorTiles` (target tiles first, distractors after).
- Drive the model with `new EncounterModel(graphemes, slotCount: target.tiles.Count)` where
  `graphemes` is `_trayData` mapped to graphemes.
- Replace the four `config.target.tiles[trayIndex]` reads with `_trayData[trayIndex]`.

That is the whole controller change — layout (`DistributeX`) already spreads `TrayCount` tiles,
so three tiles lay out without new math.

### 4. Content
**I bake (Gemini TTS, owl voice "Leda", same `Assets/Audio/Adventure/` pattern as Part A):**
- `stone_ko.phonemeAudio` → **"กอ"** (`ko.wav`) — hover/place phonics for ก.
- `kaa.wordAudio` → **"กา"** (`kaa_word.wav`) — RevealStone replay.
- `kaa.soundOutAudio` → **"กอ อา กา"** (`kaa_sound_out.wav`) — post-build echo for กา.
- Draft the owl line **"ลองอีกคำนะ" / "let's try another one"** for `e_paa_wrong_effect` (Thai
  text approved by the user before bake, per the Part-1 voice-line process).

**User authors (art already exists — paths below):**
- `stone_ko.icon` ← `Assets/Art/visaul_novel/quest/stone  ก.png` (the ก stone art already in repo).
- `kaa.magicStone` ← `Assets/Art/visaul_novel/background/crow transparent.png` (the crow that
  "pops" and deals nothing), **or** leave null to skip the pop and let the cutscene carry the crow.
- `e_paa_wrong_effect.asset`: author a frame with the **bear** (`character/bear/…`) + the **crow**
  (`crow transparent.png`) over the cut-scene background, with the owl voice line. Owl/bear may
  use the `anim` option (owl_01–10 frames / bear animation art) — same authoring as intro/outro.

**I wire:**
- `encounter_paa_supported.asset`: add `stone_ko` to `distractorTiles`.
- Assign the baked clips to `stone_ko`/`kaa` (MCP `manage_scriptable_object`).

### 5. No change
The wrong-word branch logic, `OutcomeEvaluator` (kaa is already a known word → tagged
`WrongWord`), `/grade` (targets the built word kaa), telemetry, and the correct path are all
untouched — กา simply becomes reachable.

## Art inventory (verified present)

| Need | Asset |
|---|---|
| ก stone icon | `Assets/Art/visaul_novel/quest/stone  ก.png` |
| Crow ("deals nothing") | `Assets/Art/visaul_novel/background/crow transparent.png` |
| Bear | `Assets/Art/visaul_novel/character/bear/bear-cutscence.png`, `bear animation.png` |
| Owl (animatable) | `Assets/Art/visaul_novel/character/owl/owl_01.png … owl_10.png` |
| Cut-scene background | `Assets/Art/visaul_novel/background/background_cut_scene.png` |

## Division of labour
- **Assistant:** Core overload + tests, `EncounterConfig` field, controller `_trayData` refactor,
  bake the four audio clips, assign clips + add the distractor to the config.
- **User:** assign `stone_ko.icon` + `kaa.magicStone`, author the `e_paa_wrong_effect` frame
  (bear + crow + owl line), approve the Thai voice text, Play-test the reveal.

## Testing & verification
- EditMode suite stays green and **grows** (new `EncounterModelTests` for the slot/tray split).
  Run `run_tests(mode="EditMode")`; confirm `OutcomeEvaluatorTests` still tags กา as `WrongWord`.
- After each `.cs` edit: `is_compiling == false` then `read_console(types=["error"])` = zero errors.
- Reveal visuals + audio are **user-verified in Play mode** (overlay/cutscene canvases are not
  screenshot-capturable here).
- Manual flow check: build กา → confirm → echo "กอ อา กา" → mic → crow pops → wrong-effect
  cutscene (bear + crow + owl "try another one") → back to Build with ป า ก still in the tray;
  then build ปา → success path unaffected.

## Out of scope / will not touch
- Old quest flow; `word_build_prototype.unity`.
- The `/grade` mic-capture issue (separate known bug).
- Boss-mode shuffle/multiple distractors (still forward-compat only).
- Thai TMP font swap (pre-existing follow-up).
- Authoring the crow/bear/owl art itself (already supplied) and the actual cutscene frame layout
  (user authors).

## Risks
- **Reachable non-words multiply.** With ก in the tray the child can build ปก/กป/าก/าป etc.;
  all already resolve as `NonWord` (smoke → retry), so no new branch — but confirm none of those
  combos is accidentally a known word in the database (they are not, today).
- **The reveal must read as "harmless," not "fail."** Tone lives entirely in the
  `wrongEffectCutscene` content (gentle owl line, crow that visibly does nothing) — a user
  authoring + Play-test gate, not a code guarantee.
- **Overlay/cutscene not screenshot-verifiable here** → tight user-in-the-loop on visuals.
- **Uncached TTS hits Google free-tier quota** → bake the four clips before the demo.
