# practice_night — Design Spec

**Date:** 2026-07-21
**Scene:** `Assets/Scenes/region 1/practice_night.unity`
**Status:** Approved design — ready for implementation plan

## 1. Goal

Turn `practice_night` into a short, low-pressure **nighttime practice** encounter. On
entry the scene randomly picks **one** of two Quest-1-flavoured events, the owl gives a
short one-line prompt, the child assembles the target word with the book + stones, and on
success the event resolves visually and the scene fades back to **WorldMap**.

This is *practice*, so it deliberately drops the pressure and plumbing of the real quest:
- **No smoke clock** (no `WordAssemblyTimer`, no `FogController`).
- **No backend** — no mic recording, no `/grade`, no telemetry.
- **No stars.**
- **One event per entry**, then return to WorldMap.

The moment-to-moment *feel* (stone reveal bounce, snap, book/result pop, red misassembly
lock) must match the real game.

## 2. Constraints / decisions (confirmed with user)

| Topic | Decision |
|---|---|
| Events | The two described events only (see §6). Words: **กา** and **ปา**. |
| Session flow | **1 event per scene entry**, then leave. |
| Return scene | **WorldMap** |
| Voice/backend | **Playback only** — a sound button to hear the word. No mic, no `/grade`, no telemetry. |
| Owl | **Short one-line comment per event** (no full CutScene_bear/ga intro). |
| Intro pacing | **Short** — crow animation + one owl line, then play. No zoom/spotlight. |
| Stars | **None.** |
| Background | **Night** (scene already has the night system). |
| Wrong answer | **Red screen lock** using `MisassemblyLock`, **without smoke**. |

## 3. Current scene state

`practice_night` was cloned from a bear/boss cutscene scene and already contains most of
what we need, plus leftovers to remove.

**Keep / reuse (already present):**
- `Main Camera`, `Grid` (tilemap), `Objects` (trees/structures/animals — the map art incl. the scarecrow)
- `NightLighting` (night overlay system — `NightLighting.SetNight(0..1)`)
- `Reference Forest Night Background` (`ReferenceForestNightBackground` + `ReferenceForestNightActorCalibration`)
- `EventSystem`
- `WordAssembly` (Canvas — the book + stones + slots + result pages; see §7 for structure)

**Remove (leftover from the clone):**
- `BearIntroSpotlight` (`BearIntroSequence`) and `BearIntroCanvas`
- `BossUI`
- `Objects/quest_sequence` (`QuestPathSequence`) and `Objects/quest` (`QuestProximityReveal`) — the walk-in quest scripting is not used here
- Any `WordAssemblyTimer` / `FogController` / smoke objects if present under `WordAssembly` (verify and delete — practice has no clock)

> Note: the reference screenshot shows a *daytime* map, but the scene already carries the
> night components. Implementation must ensure the night look is actually applied on load
> (`NightLighting.SetNight(1)` or equivalent, matching `reference_forest` night values).

## 4. Reuse inventory (exact assets/components)

| Purpose | Asset / component | Notes |
|---|---|---|
| Crow actor | `Assets/Art/quest_map/Crow.prefab` | UGUI `Image` + `Animator`, states `ga_fly`, `ga_stone`, `ga_set_free` |
| Crow fly-in + petrify | `CrowEntranceCutscene.cs` | Flies `wp_0→wp_1→wp_center` on `ga_fly`, switches to `ga_stone`. Reusable for Event A intro. |
| Crow set-free (revert) | `CrowSetFreeCutscene.cs` / `SuccessGaReturn.cs` + `ga_set_free.anim` | Event A resolution (stone crow → normal → fly away) |
| Crow flight anims | `ga_fly.anim`, `ga_left(_loop).anim`, `ga_right(_loop).anim` | Event B "circling the scarecrow" |
| Stone drag/slot | `MagicStonePuzzleStone.cs` | The per-stone drag + snap component (reused as-is) |
| Red misassembly lock | `MisassemblyLock.cs` | Red edge vignette + beeps. **Works standalone without a timer** — verify null-guards on `WordAssemblyTimer`/`FogController` paths. |
| Owl | `Assets/Art/quest_map/owl/Owl.prefab` or `Assets/Prefabs/OwlGuide.prefab`; `OwlGreetingCutscene.cs` / `OwlHelloSequence.cs` | Reuse the owl + speech-bubble pattern for a single short line |
| Result book pages | `book_craft`, `book_craft_pa`, `book_craft_ga`, `book_craft_success` (under `WordAssembly`) + `Assets/Art/quest_map/book_craft_crow.prefab` | Result reveal pages |
| Night | `NightLighting.cs`, `ReferenceForestNightBackground.cs` | Already in scene |
| Scene fade | `SceneFadeController.cs` (`SceneFadeController.Cover(duration)`) | Fade to WorldMap |
| Audio | `GameAudio` | Click / crow loop SFX |

## 5. Architecture

**Chosen approach: a new lightweight controller pair. Do NOT reuse `MagicStonePuzzleController`.**

`MagicStonePuzzleController` (2722 lines) is tightly coupled to the smoke timer, backend
grading, mic, dual-valid-word routing, and scene transitions — all of which practice does
not want. Reusing it means fighting all those branches. Instead we write two small,
focused scripts and reuse the *pieces* (stone component, misassembly lock, result-page
prefabs, easing helpers).

### 5.1 New scripts

**`PracticeNightController.cs`** — scene orchestrator (state machine for one entry):
1. Apply night, fade in.
2. Pick a random `PracticeEvent` (A or B).
3. Play the event's **intro** (crow animation) + the owl's **one-line** prompt.
4. Reveal the book + stones and hand control to `PracticeWordAssembly` configured for the
   event's target word.
5. On the assembly's **success callback**: play the event's **resolution** animation, then
   fade to WorldMap.
6. (Wrong answers are handled entirely inside `PracticeWordAssembly`; the orchestrator is
   not involved until success.)

**`PracticeWordAssembly.cs`** — the light word-assembly mechanic:
- Reveals the 3 stones (`stone1`=ก, `stone2`=ป, `stone3`=า) with the same reveal bounce the
  game uses, using `MagicStonePuzzleStone`.
- Accepts stone→slot placement (`inputSlot1`, `inputSlot2`) with snap.
- When both slots are filled, reads the assembled word:
  - **== target** (the event's word) → success reveal: book slides out, result page pops
    (`book_craft_<word>`), plays the word's sound clip (playback only), fire
    `onSuccess` callback.
  - **anything else** (the *other* real word **or** garbage) → **wrong**: run
    `MisassemblyLock.Instance.PlayRoutine()` (red lock, no smoke), then spring the two
    stones home for another try.
- Exposes: `Configure(string targetWord, RectTransform resultPage, AudioClip wordClip, System.Action onSuccess)`,
  `IEnumerator PlayReveal()`, and a `CanInteract` gate.
- Copy the small static easing helpers (`EaseOutBack`, `SmoothStep`) and the result-page
  crossfade/pop coroutine shape from `MagicStonePuzzleController` so the pop matches the
  game. Keep it to the minimum needed.

### 5.2 Data model

```
enum PracticeEventKind { CrowPetrified, CrowCircling }

[Serializable] class PracticeEvent {
    PracticeEventKind kind;
    string targetWord;          // "กา" or "ปา"
    RectTransform resultPage;   // book_craft_ga / book_craft_pa
    AudioClip wordClip;         // playback echo for the word
    string owlLine;             // Thai one-liner
}
```

The two events are configured in the inspector on `PracticeNightController` (a 2-element
array), so wording/clips/pages are data, not code.

## 6. The two events

Both use the **same three stones** (ก / ป / า), which can spell either กา or ปา. The event
decides which is correct; the other real word counts as wrong (red lock).

### Event A — "Crow turned to stone" (target = **กา**)
- **Owl line (example):** "กาตัวนั้นกลายเป็นหินไปแล้ว! เสกคำว่า ‘กา’ ให้มันคืนร่างสิ"
- **Intro:** crow flies in and petrifies — reuse `CrowEntranceCutscene` flow (`ga_fly` →
  `ga_stone`, holds last frame). End state: a stone crow at `wp_center`.
- **Correct (กา):** book slides out, `book_craft_ga` pops, plays กา clip → **resolution:**
  stone crow reverts and flies away (`ga_set_free` via `CrowSetFreeCutscene` / `ga_set_free.anim`).
- **Wrong (ปา or garbage):** red `MisassemblyLock`, stones spring home.

### Event B — "Crow circling the scarecrow" (target = **ปา**)
- **Owl line (example):** "เจ้ากาแอบมากวนหุ่นไล่กา! เสกคำว่า ‘ปา’ ไล่มันไป"
- **Intro:** crow circles the scarecrow — loop `ga_fly` (+ `ga_left/right`) around the
  scarecrow position (small orbit path).
- **Correct (ปา):** book slides out, `book_craft_pa` pops, plays ปา clip → **resolution:**
  crow is shooed — quick "hit/react" then flies off-screen (reuse `ga_fly` moving out).
- **Wrong (กา or garbage):** red `MisassemblyLock`, stones spring home.

> Owl lines above are placeholders; final Thai copy can be tuned during implementation or
> supplied by the user. Keep each to one short sentence.

## 7. WordAssembly prefab structure (reference)

From `MagicStonePuzzleController` field resolution, the `WordAssembly` canvas contains:
- Stones: `stone1` (ก), `stone2` (ป), `stone3` (า) — each gets a `MagicStonePuzzleStone`.
- Slots: `inputSlot1`, `inputSlot2`.
- Pages: `book_craft` (assembly page), `book_craft_pa`, `book_craft_ga`, `book_craft_success`.
- Icons: `icon/sound` (playback), `icon/mic` (**hidden in practice**).

Practice reuses these objects but drives them from `PracticeWordAssembly`. The **mic icon
must stay hidden**; only the **sound** (playback) icon is revealed on success.

## 8. Lifecycle (single entry)

```
Enter practice_night
  → SceneFadeController uncover + NightLighting.SetNight(1)
  → PracticeNightController picks event (A|B)
  → Intro: crow anim (A: fly+petrify / B: circle) + owl one-liner bubble
  → PracticeWordAssembly.PlayReveal()  (stones bounce in, become interactive)
  → child places stones:
        target word  → success reveal (book out + result page pop + word sound)
                         → event resolution anim (A: set-free / B: shoo)
                         → SceneFadeController.Cover → load WorldMap
        other word / garbage → MisassemblyLock red lock (no smoke) → stones home → retry
```

There is no timer and no scene transition on a wrong answer — the child simply retries
until the target word is built.

## 9. Wrong-answer behaviour (red lock, no smoke)

Reuse `MisassemblyLock` exactly as CutScene_bear does, minus the smoke:
- Ensure **no** `WordAssemblyTimer` / `FogController` exists in the scene. `MisassemblyLock`
  reads them defensively; with them absent the red vignette + beep pulse still plays, just
  with nothing for the pulse to push.
- Flow: freeze board → `MisassemblyLock.Instance.PlayRoutine()` → on return, spring the two
  placed stones home (`SetCurrentSlot(-1)` + `ReturnHome()`), re-enable interaction.
- Verify during implementation that `MisassemblyLock.PlayRoutine()` has no hard dependency
  on a live timer (guard/short-circuit if it does).

## 10. Verification / testing

- **Play-mode smoke test** (via Unity MCP): enter play mode on `practice_night`, confirm
  night background is applied, an event intro plays, owl line shows, stones become
  interactive.
- **Correct path:** assemble the event's target word → result page pops, word sound plays,
  resolution animation runs, scene loads WorldMap.
- **Wrong path:** assemble the other real word and a garbage combo → red lock plays (no
  smoke), stones return home, retry still works.
- **No-timer assertion:** confirm no `WordAssemblyTimer`/smoke object in scene; no null-ref
  errors in console during a wrong answer.
- **Randomness:** re-enter several times; both events appear.
- `PracticeWordAssembly` word-check has one runnable self-check (assert target vs
  other-word vs garbage classification) per the project's test conventions.

## 11. Assumptions

- The scarecrow art is already placed in the scene (part of `Objects`); no new scarecrow
  asset is needed. If it is missing, place the existing map sprite used in the screenshot.
- `Crow.prefab`'s Animator exposes `ga_fly`, `ga_stone`, and `ga_set_free`. If `ga_set_free`
  is on a separate controller (`CrowSetFreeCutscene`), reuse that component for the Event A
  resolution.
- WorldMap is reachable by scene name `"WorldMap"` (as in `FirstPageIntro`).
- Word playback clips (กา / ปา) exist or can be sourced from the existing gameplay TTS/baked
  clips used by `MagicStonePuzzleController` (`Resources/TTS`).

## 12. Out of scope

- Entry point wiring (how the player reaches `practice_night` from WorldMap) — this spec
  covers the scene's internal behaviour only.
- Any backend/telemetry/mic integration.
- New crow or scarecrow art.
- Multi-event sessions, scoring, or difficulty progression.

## 13. Files

**New:**
- `Assets/Scripts/Region1/Practice/PracticeNightController.cs`
- `Assets/Scripts/Region1/Practice/PracticeWordAssembly.cs`

**Modified (scene):**
- `Assets/Scenes/region 1/practice_night.unity` — remove leftovers (§3), wire the two new
  controllers + the two `PracticeEvent` entries, ensure night is applied, hide the mic icon.

**Reused unchanged:**
- `MagicStonePuzzleStone.cs`, `MisassemblyLock.cs`, `CrowEntranceCutscene.cs`,
  `CrowSetFreeCutscene.cs`, `SceneFadeController.cs`, owl components, `Crow.prefab`.
