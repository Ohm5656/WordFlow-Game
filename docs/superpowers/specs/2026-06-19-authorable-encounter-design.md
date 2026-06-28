# Authorable, Book-Themed Encounter — Design

**Date:** 2026-06-19
**Branch:** `feat/word-build-encounter-prototype`
**Status:** Design — pending user review before implementation plan.

## Problem

The word-build encounter loop works, but it cannot be laid out or art-directed in the
Editor:

1. **The build page is built in code at Play time.** `WordBuildEncounterController.BuildUi()`
   and `BuildBoard()` create the canvas, three buttons, HUD, tiles, and slots from scratch,
   positioning everything with constants (`new Vector2(-160,90)`, `TrayY=-120`, `SlotY=60`,
   `RowX(...)`). Nothing exists in the Editor to see or drag, so fixing positions means
   editing magic numbers in C# and re-entering Play mode to guess again. The page is also a
   plain blue background.
2. **Cutscenes can only show one character per frame.** `CutscenePlayer` has a single `_npc`
   `Image`; `ApplyNpc` sets one sprite and parks it Left/Center/Right via `frame.side`. The
   intro therefore can only show owl *or* bear, and "focusing" a speaker isn't expressible.

This was initially framed as "should the loop be separate scenes?" — but separate scenes
would re-introduce cross-scene state plumbing (the loop's `EncounterModel`, `BuiltString`,
`BuildLatencyTracker`, and `/grade` context all live in memory), add load seams at the
emotional peak (build → stone pop → effect), and make every wrong-word retry a jarring
reload. None of that fixes positioning. **The real need is visual authorability, not scene
separation.** The encounter stays one continuous scene with its existing phase machine.

## Goals

- Lay out and art-direct the build page in the Editor (drag, see it, no magic numbers).
- The build page reads as a **book** (reuse `Assets/Art/visaul_novel/quest/book_craft.png`,
  exactly as the old `cut_scene1` craft scene does), with the magic stone conjured *from*
  the book.
- The intro shows **owl and bear together**, with per-frame **focus** (zoom in on the active
  character + dim the others) — bear focused as it rushes in, owl focused as it speaks.
- Provide code slots for **bear join / flee** animation and a **following magic beam**; the
  art and `AnimationClip`s are authored manually by the user.

## Non-Goals

- No splitting the encounter into multiple scenes.
- No changes to the Build/Confirm/Echo/Mic/Resolve phase logic, the `OutcomeEvaluator`, or
  the `/grade` path. This is a *view/authoring* change only.
- No bespoke beam-tracking system. The "following" beam is baked into a user-authored
  `AnimationClip`, played through the existing best-effort legacy-anim path.
- No new art, animation clips, or final positioning by the assistant — those are the user's
  manual work. The assistant delivers structure, scaffolding, and playback hooks.
- The old quest flow (`MagicStonePuzzleController`, `cut_scene1`, etc.) is untouched.

## Division of Labor

| Assistant (code/scaffold) | User (manual art/animation) |
|---|---|
| Refactor controller to reference an authored hierarchy | Final positioning / sizing in Editor |
| Scaffold `EncounterCanvas` hierarchy via Unity MCP, assign `book_craft.png`, set layout groups | Polished book art / page framing |
| Add multi-character + focus support to `CutscenePlayer` / `CutsceneFrame` | Owl/bear/beam sprites |
| Drive authored legacy `AnimationClip`s for extra characters | Author the bear-join, bear-flee, beam-follow clips |

## Component 1 — Book-styled, authored build page

### Authored hierarchy (scaffolded via MCP)

```
EncounterCanvas (ScreenSpaceOverlay, ScaleWithScreenSize, ref 1280x720)
├── BookBackground    Image = book_craft.png (replaces the plain-blue runtime canvas)
├── SlotContainer     positioned/sized RectTransform — sits on the book's page
├── TrayContainer     positioned/sized RectTransform — lower page
├── StoneRevealAnchor RectTransform — where the magic stone / smoke pops (book center)
├── ListenButton      Button (speaker icon)
├── ConfirmButton     Button (check icon)
└── MicButton         Button (mic icon)
```

The assistant scaffolds this hierarchy with rough positions and the `book_craft` sprite
assigned, so the user has real objects to drag immediately.

### Controller changes (`WordBuildEncounterController`)

- Add `[SerializeField]` references: `RectTransform trayContainer`, `RectTransform slotContainer`,
  `RectTransform stoneRevealAnchor`, and the three `Button`s (`listenButton`, `confirmButton`,
  `micButton`) plus the mic `Image` for the pulse. (Keep existing `audioSource`,
  `cutscenePlayer`, `config`, `database`, `gradeClient`.)
- `BuildUi()` no longer creates the canvas/buttons/HUD. It validates the serialized refs and
  wires button `onClick` listeners (`OnListen`/`OnConfirm`/`OnMic`). Missing ref → log + skip
  (null-safe house style); the encounter degrades, never throws.
- `BuildBoard()` instantiates tiles into `trayContainer` and slots into `slotContainer`. The
  controller distributes children evenly across each container's authored rect (a small
  `DistributeX(i, n, container)` helper replacing the constant-based `RowX`/`TrayY`/`SlotY`).
  A `HorizontalLayoutGroup` is deliberately **not** used: it rewrites child `anchoredPosition`
  every layout pass and would fight the tap-to-slide animation. The container's position/size
  (authored by dragging it in the Editor) is what determines where the row sits — no magic
  numbers in code.
- Tile/slot **animation** (the tap-to-slide feel) is preserved: a tile's tray "home" is its
  distributed position in `trayContainer`; it slides to the target slot's position in
  `slotContainer` and back, exactly as today but with positions sourced from the containers.
- `RevealStone` / `SmokePuff` parent their spawned sprite to `stoneRevealAnchor` instead of
  `_boardRoot` with a hardcoded offset.
- The `Make*` factory helpers (`MakeIconButton`, `MakeButton`, `MakeSlot`, `MakeTile`,
  `MakeStretch`, `MakeLabel`, `RowX`) are removed once their callers move to refs — only the
  orphans this change creates, nothing pre-existing.
- Debug HUD/toggles (`showDebugControls`) stay as today (hidden in the child build).

### Risk / scope

A bounded edit to a ~660-line file: the change is confined to *view construction and
placement*. The phase machine (`Building`/`Confirm`/`Echo`/`Mic`/`Resolving`/`Done`), tap
gating, echo, mic, grade, and branch logic are not touched.

## Component 2 — Multi-character cutscene with focus + bear/beam hooks

### Data (`CutsceneFrame` / new `CutsceneCharacter`)

- Keep the existing single `npc` + `side` + `npcAnim` fields (backward compatible — every
  authored cutscene asset keeps working).
- Add `List<CutsceneCharacter> extraCharacters` (default empty). `CutsceneCharacter` =
  `{ Sprite sprite; Vector2 offset; AnimationClip anim; }` — freely positioned by `offset`
  (serves the "fix the positions" need), with an optional legacy clip.
- Add `int focusIndex` (default `-1`). Convention: `-1` = no focus (all characters at full
  brightness/normal scale); `0` = the primary `npc`; `1..N` = `extraCharacters[focusIndex-1]`.

### Player (`CutscenePlayer`)

- Render the primary `npc` (unchanged path) **plus** one `Image` per `extraCharacters` entry,
  positioned by `offset`, each driven through the existing best-effort `PlayNpcAnim` legacy
  path (so authored clips for bear-join, bear-flee, beam-follow just play; failures are
  ignored, sprite still shows).
- Apply **focus** per frame: the focused character lerps to `focusScale` (~1.15) and others to
  a smaller scale (~0.92) with alpha dimmed to `dimAlpha` (~0.5); `focusIndex == -1` resets all
  to full. New serialized tunables: `focusScale`, `dimAlpha`, `focusLerpSeconds`.
- Overlay teardown destroys the extra-character Images alongside the primary.

### How owl + bear + beam map onto this

- Owl = primary `npc`. Bear + beam = two `extraCharacters`, each with its authored clip.
- Bear rushes in → that frame sets `focusIndex` to the bear; owl speaks → next frame sets
  `focusIndex` to `0` (owl). Both remain on screen; emphasis shifts via zoom + dim.
- The beam "follows" via the motion baked into the user's beam `AnimationClip` — no tracker.

## Testing

- **Core is unaffected** — `EncounterModel`, `OutcomeEvaluator`, `WavEncoder`,
  `WordDatabase` tests stay green (run the EditMode suite to confirm no regression).
- The view/authoring changes are validated in the Editor / Play mode (MCP screenshots),
  consistent with how the prototype is otherwise verified; there is no headless harness for
  MonoBehaviour view code.

## Open Follow-ups (unchanged from before, out of scope here)

- Thai-capable TMP font; pre-warming the remaining `/tts` lines blocked by quota.
- Routing the build page's Listen/Echo through `TtsApiClient` (separate feature).
