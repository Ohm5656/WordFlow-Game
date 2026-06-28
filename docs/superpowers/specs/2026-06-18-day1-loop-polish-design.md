# Day-1 Loop Polish — Design Spec

**Date:** 2026-06-18
**Status:** design approved (verbal), pending spec review
**Branch:** `feat/word-build-encounter-prototype`
**Extends:** `2026-06-14-word-build-encounter-design.md`, `2026-06-17-encounter-cutscene-design.md`

## Context

The word-build encounter loop (`word_build_prototype.unity` +
`WordBuildEncounterController`) is fully built and runs end-to-end: Building → Confirm
→ Echo → Mic → Resolving → Done, with intro/outro cutscenes wired. But it has three
problems that block a "full polished Day-1 loop": the cutscene voice gets cut off, the
mic phase fires-and-forgets so it "just cuts," and the whole UI is placeholder colored
squares + **text labels** — which violates the project's core LD no-text rule.

The only adventure scene is `word_build_prototype.unity`. The only authored encounters
are **กา** and **ปา** (owl, and owl+bear) — there is **no ยา** config or cutscene yet,
even though ยา is the narrative Encounter 1. ปา was built as an (incomplete) clinic demo.

### Decision (locked 2026-06-18)

Polish the **loop itself**, validated on the existing **ปา / bear** encounter (it has
cutscenes + bear art and needs finishing). The loop is data-driven and word-agnostic, so
**ยา becomes a pure content drop-in afterward** (author its cutscenes + owl lines + stone
art; zero engine work). Building ยา content is a **follow-up, out of scope here.**

## Hard rule: everything child-facing is an ICON, never text

Audience = Thai LD children who cannot read. Vault `wordflow-game-design.md`:
"Minimal text by design… icon-driven." This covers **all** UI, not just dialogue:

- "Say it" → **mic icon** (glows/pulses while recording)
- "Confirm" → **checkmark icon** (or a glowing "go" gem)
- "Hear it" → **speaker icon** (Supported mode only)
- HUD prompt text → **removed**; the prompt is carried by NPC/owl voice + the empty stone
  slots themselves.
- The **only** on-screen glyphs are the Thai grapheme tiles being built (the learning
  content, not UI).
- Dev-only debug toggles (Mode/Echo) may keep text **but must be hidden in the child build.**
- Where final icon art isn't ready, use a clearly *iconic* placeholder glyph — **never a
  text label, even as a stopgap.**

## The polished loop

```
intro cutscene (owl/NPC voice via /tts, no text)
  -> BUILD: tap a stone tile -> it SLIDES into the next slot; tap a slot -> tile SLIDES back
  -> CONFIRM: explicit checkmark-icon button (not auto-confirm)
  -> post-build ECHO: owl voices what was built (always)
  -> MIC: tap mic icon -> record + glow/pulse; tap again or 5s auto-stop
  -> /grade fires INVISIBLY in background (child sees nothing about pronunciation)
  -> resolve by what was BUILT:
       REAL WORD (correct OR wrong):
         magic stone of THAT word POPS ~1.5s + replays its sound  (look + sound memory anchor)
         -> word-EFFECT cutscene plays (the word's magic happening)
         -> Correct  = +1 level / progress
         -> WrongWord = no progress, back to BUILD (child still learned that word's meaning)
       NON-WORD:
         black smoke puff + funny non-demotivating SFX, no stone
         -> back to BUILD
```

Rationale: the child learns each word's **look + sound + meaning through trial and error**.
Real words (which have stone assets) always get the reveal + effect cutscene, even when
wrong-for-the-context — that *is* the learning. Non-words (no asset, no meaning) get a
gentle funny "nope" and a retry, never enshrined with a fake stone.

> Prototype reality: with only ปา's two tiles in the tray, **WrongWord is unreachable**
> (only Correct or NonWord fire). The WrongWord branch is built properly anyway — same code
> path — and becomes reachable when boss distractor tiles are added later.

## Changes

### 1. Build mechanic — click-slide + reusable stone art (`WordBuildEncounterController`, `StoneTile`, `TileSlot`)
- Keep click-to-place + the **explicit Confirm** step (already in code; not auto-confirm).
- Replace instant `RebuildTrayAndSlots()` snapping with a **slide animation**: tapping a tray
  tile slides it into the target slot; tapping a filled slot slides it back to its tray
  position. Hand-rolled coroutine lerp (matches old-flow style), tap-gated by phase.
- Replace the placeholder colored squares with the **reusable magic-stone sprites** used by
  the old `MagicStonePuzzleController` in `cut_scene1`. **Investigation step:** identify the
  exact stone sprites/material there and whether per-grapheme art exists or one shared stone
  frame is reused. Wire via the existing nullable `StoneTileData.icon` (null-safe: no art →
  current placeholder, so nothing breaks if a sprite is missing).

### 2. Cutscene voice never cut off (`CutscenePlayer`) — Bug A
- Root cause: `ttsTimeout = 3f` makes the player give up after 3s, play nothing, and advance
  on `holdSeconds`; the wait window can also start before the clip plays.
- Fix: resolve + start the clip first, then hold the frame for the **full clip length** (or
  `holdSeconds`, whichever is longer). A slow/uncached `/tts` fetch **waits** up to a safety
  cap (~8s) instead of dropping audio. Tap still advances/skips early. Null clip → silent
  frame, unchanged.
- The intro voice already routes through `/tts` via `voiceLineId` — this fix is what makes it
  actually fire. No data change.

### 3. Mic = a real record beat (`GradeApiClient`, controller Mic phase) — Bug B
- Split `RecordAndGrade` (single fixed-4s blocking coroutine) into **`StartRecording()`** and
  **`StopAndGrade(ctx, onResult)`**.
- Mic phase: tap mic icon → `StartRecording()` + the icon **glows/pulses** (coroutine; an
  optional 5s depleting ring). Tap again → stop; or **auto-stop at 5s**.
- The resolve sequence **waits for recording to finish** before the stone reveal / effect
  cutscene — this kills the "just cuts" bug. After the clip is captured, the `/grade` POST
  runs in the background (fire-and-forget) and never blocks the visuals.
- No mic / permission denied → degrade gracefully (skip straight to resolve, log it).

### 4. Stone reveal + effect cutscenes (controller Resolving phase, `WordEncounterData`)
- Add nullable `WordEncounterData.magicStone : Sprite` (no-art-safe: null → reveal no-ops).
- New reveal beat after mic: if `database.LookupByThai(built) != null` (real word), pop its
  `magicStone` centered ~1.5s and replay its `wordAudio`, then play the **word-effect
  cutscene** (a `CutsceneData`).
- Effect cutscene fields on `EncounterConfig` (or `WordEncounterData`): **`correctEffectCutscene`**
  and **`wrongEffectCutscene`** (both nullable; null → quick FX only, unchanged-safe). Correct
  reuses the existing `outroCutscene` slot; Wrong is the new one.
- Non-word → **black smoke + funny SFX** beat (replaces the grey `Flash()`), then back to build.
  Correct/Wrong green/blue flashes are superseded by the stone reveal + effect cutscene.

### 5. Build-phase latency KPI (`Core/BuildLatencyTracker.cs` — new, unit-tested)
- Pure C#, zero Unity deps, in the `Core` layer with EditMode tests.
- Fed timestamps; produces **total build latency** + **per-tile placement latencies**.
- Clock starts when tiles become interactive (end of `BeginEncounter`), stops at
  `IsComplete`. Reset on `ResetToTray`.
- Sent as a forward-compat **`buildLatencyMs`** multipart field on the `/grade` POST.
  Backend consumption = flagged follow-up (no backend change here). Latency is measured on
  **building** (cognition / RAN-relevant), NOT on speech onset — per the vault's KPI and to
  avoid the click/click+5s mic confound.

### 6. New voice content to author (cross-repo, backend)
- The **word-effect cutscenes** (correct + wrong) need owl/NPC lines. Author Thai lines, add
  to `wordflow-backend` `gateway/tts_lines.json`, pre-warm via `scripts/prewarm_tts.py`.
- Draft (ปา, for review — *not final*): correct-effect owl line celebrating the word landing;
  wrong-effect owl line gently noting "real word, not the one this needs." Finalize during
  implementation.

### 7. Scene + backend wiring
- **Duplicate** `word_build_prototype.unity` → adjust the copy; leave the original intact.
- Confirm the duplicated scene's `GradeApiClient`/`TtsApiClient` point at **`http://127.0.0.1:8001`**
  with `Authorization: Bearer demo-token`, and the `EventSystem` uses `InputSystemUIInputModule`.
- Live-smoke `/tts` and `/grade` against the backend running with `AUTH_IMPL=fake` on `:8001`.

## Testing

- **Unit (Core):** `BuildLatencyTracker` (total + per-tile, reset) added to
  `WordFlow.Adventure.Tests`; existing EditMode suite stays green. Run via MCP `run_tests`.
- **Manual / MCP:** load the duplicated scene, Play, `manage_camera` screenshots of each beat
  (intro → build slide → confirm → echo → mic glow → stone pop → effect cutscene; and the
  non-word smoke path). Confirm voice plays fully (no cutoff) and mic waits before resolving.

## Out of scope

Old quest flow (untouched), auto-run/world-map, boss/distractor tiles, **ยา content
authoring**, backend consumption of `buildLatencyMs`, final icon/stone art production.

## Open questions / assumptions

1. **cut_scene1 reusable art** — exact stone sprites TBD during implementation (investigation
   step in change #1). Assumption: at least one reusable stone frame exists.
2. **Icon art** — mic/speaker/check icons may not exist; iconic placeholder glyphs acceptable
   for first pass, never text.
3. **Thai TMP font** — the build still renders Thai grapheme tiles; the existing open
   follow-up (LiberationSans renders ก/า as □) must be resolved with a Thai-capable TMP font
   for the tiles to read correctly.
4. **Effect-cutscene fields location** — `EncounterConfig` vs `WordEncounterData`. Lean
   `EncounterConfig` (mirrors existing intro/outro slots); confirm during plan.
```
