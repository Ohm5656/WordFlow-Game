# Encounter-Loop Polish Pass — Design

**Date:** 2026-06-20
**Branch:** `feat/word-build-encounter-prototype`
**Scope:** Visual + authorability polish on the word-build encounter and its intro/outro
cutscenes, plus a voice-line rewrite. Touches the Adventure Redesign only. Does **not** touch
the old quest flow or `word_build_prototype.unity`; work targets
`word_build_paa_polished.unity` and the `e_*` cutscene assets.

## Goal

Make one full encounter (intro cutscene → word build → outro cutscene) look and sound
demo-ready: a real background behind the book, cleanly separated word slots, bigger stones,
icon-only (transparent) buttons, author-tunable cutscene character sizes/dimming, a working
"float-in" entrance animation, and more natural Thai voice lines.

## Division of labour

Per the user's "mix" choice:
- **Code/data/structure (assistant, via MCP + file edits):** all C# field additions, sizing
  defaults, the new background GameObject, cutscene-player changes, and the `tts_lines.json`
  rewrites.
- **Visual authoring (user, via a short tutorial):** assigning sprites (background, slot
  frame, button icons), setting transparency alphas, and nudging rects in
  `word_build_paa_polished.unity`.

The LD **no-on-screen-text** rule still holds for every child-facing affordance.

---

## Workstream 1 — Word-build page layout

### 1a. Tunable sizes (code — `WordBuildEncounterController`)

Replace hardcoded magic numbers with `[SerializeField]` fields (bigger defaults). Current →
new default:

| Field (new) | Replaces | Current | New default |
|---|---|---|---|
| `tileSize` (Vector2) | `MakeTile` `sizeDelta = 120×120` | 120 | **200×200** |
| `slotSize` (Vector2) | `MakeSlot` `sizeDelta = 120×120` | 120 | **210×210** |
| `slotGap` (float) | (new) widens spacing so 2 slots read as 2 cells | n/a | **40** |
| `stoneRevealSize` (Vector2) | `RevealStone` `sizeDelta = 260×260` | 260 | **420×420** |
| `smokeSize` (Vector2) | `SmokePuff` `sizeDelta = 240×240` | 240 | **400×400** |
| `slotFrameSprite` (Sprite, nullable) | `MakeSlot` faint white box | — | null → keep box |
| `slotColor` (Color) | `MakeSlot` `(1,1,1,0.12)` | 0.12α | authorable |

- `DistributeX` stays the math source of truth but uses `slotSize.x + slotGap` to lay out the
  two slots so they sit as two visibly distinct cells instead of crowding one box.
- If `slotFrameSprite` is assigned, each slot shows it (`preserveAspect`) instead of the faint
  square — giving a clear "drop here" frame. Null → current faint box (graceful default).
- All fields are nullable/defaulted so an un-tuned scene still runs.

### 1b. Background behind the book (structure — assistant; sprite — user)

- **Assistant:** add a full-screen `SceneBackground` `Image` as the **first** child of
  `EncounterCanvas` (drawn behind `BookBackground`): anchors stretched to canvas,
  `raycastTarget = false`, `preserveAspect = false`. Left spriteless (shows nothing) so it is
  inert until the user assigns art.
- **User (tutorial):** drag the chosen background sprite onto `SceneBackground.Image.sprite`.
  This replaces the blue clear-colour that currently shows around the non-full book page.

### 1c. Transparency (user, tutorial)

- `SlotContainer` Image alpha → **0** (container becomes invisible; only the slot cells show).
- Each of `Listen` / `Confirm` / `Mic` button background Image alpha → **0**; keep only the
  icon child. (Icons remain the sole affordance — no-text rule.)

> Why two slots already "should" work: the controller already builds `n` slots (2 for ปา).
> The "one window" look comes from the visible `SlotContainer` background + small faint slots.
> Transparent container + bigger framed slots (1a/1c) resolves it without new slot logic.

---

## Workstream 2 — Cutscene character sizing (code — `CutsceneData` + `CutscenePlayer`)

Characters are currently hardcoded to 520×620. Add author-tunable size:

- `CutsceneCharacter.size` (Vector2, default `(0,0)` = "use default 520×620") — per extra
  character (bear, owl, beam, …).
- `CutsceneFrame.npcSize` (Vector2, default `(0,0)` = default) — the primary `npc`.
- `CutscenePlayer.ApplyNpc` / `ApplyExtraCharacters` apply the size when non-zero, else keep
  520×620. Existing assets (all zero) are unchanged.

Result: the PAA intro can author "owl smaller, bear much bigger" directly on the frame/asset.

---

## Workstream 3 — Per-cutscene dimming (code — `CutsceneData` + `CutscenePlayer`)

**Bug fixed in passing:** `CutscenePlayer` currently tints its single backdrop near-black
**and** uses that same backdrop to hold `frame.background`, so any background sprite renders
darkened. Split the overlay into two layers:

- **BG layer** (`Image`, white tint, holds `frame.background`) — shows the scene faithfully.
- **Dim layer** (`Image`, black, stretched, above BG and below characters) — alpha =
  `CutsceneData.backdropDim`.

New authorable fields on `CutsceneData` (defaults preserve current feel):

| Field | Default | Meaning |
|---|---|---|
| `backdropDim` (0–1) | **0.35** | darkness of the black layer over the background |
| `focusDimAlpha` (0–1) | **0.5** | alpha of non-focused characters when a focus is set |
| `focusScale` | **1.15** | focused character scale-up |
| `unfocusScale` | **0.92** | non-focused character scale-down |

`CutscenePlayer` reads these **directly** from the `CutsceneData` being played (no sentinel
logic — the values above are the SO field defaults, so an un-tuned asset reproduces today's
look). The now-redundant player-level `dimAlpha`/`focusScale`/`unfocusScale` serialized fields
are removed to keep one source of truth.

---

## Workstream 4 — "Float-in" entrance animation (code — `CutsceneData` + `CutscenePlayer`)

The bear `anim` referenced in `e_paa_intro.asset` cannot play: `PlayClipOn` skips non-legacy
clips, and `Bear_Entrance.anim` is a sprite-swap clip bound to a `bear_sprite (1..9)` child
hierarchy — built for a bear **prefab**, not the cutscene's single flat `Image`. Rather than
drag that prefab into the overlay, we add a **procedural entrance** modelled on the old
`BearCutsceneEntrance.PlayDirectEntrance` (the "simple bear slowly floating in" the user
referenced): ease position from an off-position to the resting spot with `SmoothStep`, a gentle
sine bob, and a slight scale-up.

New fields (per character, so owl/bear/extras each choose their own):

- `CutsceneCharacter.entrance` (enum `EntranceMode { None, SlideInLeft, SlideInRight, SlideInUp, ScaleIn, Bob }`, default `None`)
- `CutsceneCharacter.entranceSeconds` (float, default `0.8`)
- `CutsceneFrame.npcEntrance` (enum, default `None`) + `npcEntranceSeconds` (float, default `0.8`)

> **DECISION (2026-06-20, as-built) — `ScaleIn` NOT implemented. TODO if wanted.**
> The shipped enum is `EntranceMode { None, SlideInLeft, SlideInRight, SlideInUp, Bob }`
> (no `ScaleIn`). `ScaleIn` was intentionally dropped during implementation because a
> scale-based entrance fights the focus zoom/dim system (which also writes `localScale`),
> producing conflicting transforms. The position-only `SlideIn*`/`Bob` entrances avoid this.
> **The enum is serialized by index** — if `ScaleIn` is added later it MUST be appended
> **after `Bob`** (giving `Bob=4`, `ScaleIn=5`) to preserve existing asset indices; do not
> insert it in the middle. To revisit: add the enum value + a `ScaleIn` case in
> `CutscenePlayer.EntranceRoutine` (ease scale 0.2→1) and decide how it coexists with focus
> scaling. Flagged by final review item I1.

Behaviour in `CutscenePlayer`:
- The character's **resting** position is its existing `offset` (extras) / side-anchor (npc).
  A `SlideIn*` starts at resting ± an off-screen delta and eases to rest. `ScaleIn` eases
  scale 0.2→1. `Bob` is a looping idle sine on Y. `None` = current static behaviour.
- Easing reuses the project's `SmoothStep` style; the bob envelope matches `PlayDirectEntrance`
  (`sin(t·π)` so it settles).
- Entrance runs when a frame first shows the character; it never blocks voice/auto-advance
  (the frame still holds for `max(holdSeconds, clipLength)`).

The legacy/non-legacy `PlayClipOn` path is **left intact** (still best-effort for any future
legacy clip); `Bear_Entrance.anim` simply stays unused for the flat-image cutscene.

**Content (asset edit):** set the PAA intro bear's `entrance = SlideInRight` (charges in from
the right) so the demo shows the motion; owl can use `Bob` or `None`.

---

## Workstream 5 — Rewrite the TTS voice lines (content — `tts_lines.json`)

Scope: **all three** encounter sets — `e1_*` (yaa/villager), `kaa_*` (crow), `paa_*` (bear).
Edit the Thai `text` and `style` per line for more natural, child-directed delivery; keep the
`line_id`s, `voice`s, and `en` glosses stable (IDs are the cache key and the Unity references).

Process:
1. Assistant drafts revised Thai `text` + tightened `style` for each line in scope.
2. **User (native Thai speaker) approves/corrects the Thai** before commit — content authority
   stays with the user.
3. After approval: re-run `gateway/scripts/prewarm_tts.py` to regenerate the disk cache (text
   changes ⇒ new cache entries) and restart the `:8001` gateway with `AUTH_IMPL=fake`.
   Uncached lines hit the Google free-tier daily quota — prewarm before the demo.

No Unity-side change is required for the rewrite (the client resolves by `line_id`).

---

## Out of scope / will not touch

- Old quest-flow scenes/scripts; `word_build_prototype.unity`.
- The `/grade` mic-capture issue and the mid-edit `GradeApiClient.cs` (separate known bug).
- Swapping the Thai TMP font (pre-existing follow-up).
- Authoring the actual art assets (user supplies sprites).

## Testing & verification

- After each C# edit: wait for `is_compiling == false`, then `read_console(types=["error"])`
  — zero errors before using new fields.
- EditMode suite stays **24/24** (changes are View/Data only; no `Core/` gameplay rules
  touched, so no Core tests change). Run `run_tests(mode="EditMode")` to confirm green.
- Visual pieces (book page, slots, buttons, cutscene) are verified by the **user in Play mode**
  — MCP cannot screenshot the ScreenSpace-Overlay canvases.
- Voice lines verified by listening after prewarm.

## Risks

- Overlay UI is not screenshot-verifiable here → tight user-in-the-loop iteration on visuals.
- `tts_lines.json` text changes invalidate cache → must prewarm or the demo stalls on quota.
- Procedural entrance is intentionally simpler than the authored `Bear_Entrance.anim`; if the
  user later wants that exact sprite-swap, it needs the bear prefab path (deferred, not in
  this pass).
