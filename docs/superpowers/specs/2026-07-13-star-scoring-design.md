# Star Scoring & Award Animation (CutScene_bear + Success scenes)

Date: 2026-07-13
Status: approved, ready for implementation

## Problem

The word-build round has no score. The child assembles a word, pronounces it, and moves on with no
signal about how well they did. We want a **0-3 star verdict** with a juicy award animation, using the
`star_root` UI the designer has already laid out in `CutScene_bear`.

This is the regional-round star verdict from [[wordflow-regional-stars-scope]] — stars replace pets
for this milestone.

## Scoring rules

Three stars, each with its own condition. All are evaluated inside `CutScene_bear`.

| Star | Condition | Awarded at |
|---|---|---|
| **1** | A valid word was assembled — **either** the target (ปา) **or** the alt (กา). No time condition. | the moment the word completes (`WordResultRoutine`) |
| **2** | The assembled word was **correct** (ปา). | pronunciation finishes (`RecordingSuccessRoutine`) |
| **3** | The word was **correct** *and* it was assembled **before the clock ran out** (smoke had not fully closed). | same moment as star 2 — both fly up together |

Resulting totals:

- **1 star** — built กา (wrong word). Star 2 and 3 are withheld. → `Success_ga` → back to the puzzle to retry.
- **2 stars** — built ปา but after the clock hit zero.
- **3 stars** — built ปา before the clock hit zero.

Note there is deliberately **no time gate on star 1**: a child who runs out of time still gets a star
for finishing the word. The clock only decides star 3.

**Stars do not carry across a retry.** After the กา detour the child returns to `CutScene_bear` with
the star board empty and earns again from scratch. (The *clock*, by contrast, resumes where it left
off — that is existing behaviour and is not touched.)

## Hard constraint: the smoke clock is untouchable

`WordAssemblyTimer` is the source of truth for the smoke clock ([[smoke-clock-edge-fog]]) and must not
be modified. Everything the star system needs from it is already public and read-only:

- `WordAssemblyTimer.Instance.SmokeRemaining` — seconds left; `> 0` at assembly time **is** the star-3 condition.

## The trap: `star_root` is still the timer's board

`WordAssemblyTimer` is **still attached to `star_root`** (the designer renamed `time_root` and swapped
its children, but the component stayed). The timer calls `HideBoard()` → `CanvasGroup.alpha = 0` **on
its own GameObject** every time it `Pause()`s — and `Pause()` fires exactly when the word is built.

Left as-is, **every star would vanish the instant it was awarded.**

**Fix, without touching the timer:** move the star visuals out from under `star_root` into a new
sibling GameObject `star_hud` (same Canvas, same RectTransform values, so the layout is unchanged).
`star_root` keeps `WordAssemblyTimer` + its `AudioSource` and goes on driving the clock, the beep and
the smoke — now with nothing visible under it (its `digits` array is already empty; `SetDigit` null-guards,
so nothing breaks).

## Design

### `star_hud` — the star board

Children (all already authored in the scene, currently under `star_root`):

| Object(s) | Role |
|---|---|
| `star_frame` | the board / frame. The only thing visible before any star is earned. |
| `star_start` | where an awarded star pops in — screen centre. Hidden until an award plays. |
| `effect_star1`..`effect_star4` | the centre burst. Their **authored positions are the scatter destinations** — the animation flies them out from `star_start` to where they already sit, then fades them. |
| `star_end`, `star_end (1)`, `star_end (2)` | the three resting slots. Position **and size** are read from these at runtime — the flying star lerps into them. Hidden until stamped. |
| `effect_panel1` ×3 | the **stamp** effect at each slot (one per slot). |
| `effect_panel2`..`effect_panel5` ×3 | the **burst** at each slot — again, authored positions are the scatter destinations. |

The `×3` copies are named with Unity's duplicate suffix and **irregular internal spacing**
(`effect_panel3 `, `effect_panel3  (1)`, `effect_panel4  (2)`, …). Wiring must normalise names
(strip all whitespace) before matching, never compare raw strings.

Everything except `star_frame` starts hidden. The script forces this in `Awake` rather than trusting
scene state.

### Award animation (one star)

1. **Pop** — `star_start` scales `0 → 1` with an ease-out-back overshoot, alpha `0 → 1` (~0.35s).
   Simultaneously the four `effect_star*` shoot from `star_start`'s position out to their authored
   positions, scaling up, then fade to zero (~0.45s). This is the "burst" the designer asked for.
2. **Hold** (~0.3s) — let the child register the star.
3. **Fly** — the star travels `star_start → star_end[i]` along a **quadratic bezier arc** (control
   point lifted above the midpoint) while its `sizeDelta` and scale lerp from the start values to the
   slot's authored values. Ease-in-out, ~0.6s, with a small anticipation dip (scale 0.9) before launch
   and a spin. This is what makes it read as a game reward rather than a UI tween.
4. **Stamp** — on landing, `star_end[i]` is enabled with an ease-out-back overshoot (1.25 → 1.0),
   `star_start` hides, `effect_panel1[i]` plays the impact stamp (scale up + fade out), and
   `effect_panel2..5[i]` burst out to their authored positions and fade.

Two stars awarded at once (the star-2 + star-3 case) run this **sequentially**, ~0.25s apart. They do
not fly simultaneously — sequential reads better and lets each landing land its own beat.

### Where the stars are shown

| Scene | What shows | Why |
|---|---|---|
| `CutScene_bear` | the live award animations (star 1 at assembly; stars 2+3 at pronunciation) | where the stars are earned |
| `Success_pa` (2-3 stars) | the earned stars **pop in on the board** while the owl praises (`paa_outro_owl_1`), then on to the next level | the praise beat — existing owl epilogue is not changed, the board is added alongside |
| `Success_ga` (1 star) | the one earned star shows on the board during the existing crow celebration, then **fades out** — the "your stars reset" feeling — before the scene fades back to the puzzle | the encouragement beat. The existing `SuccessGaReturn` flow is not changed, the board is added alongside |

In the Success scenes the stars **do not fly** — they were already flown in `CutScene_bear`. They pop
into their slots directly (0.15s apart, with the stamp effect) as a recap.

### Persistence

One `PlayerPrefs` int, `StarEarnedCount`:

- written by `CutScene_bear` on every award,
- read by `Success_pa` / `Success_ga` to know how many to show,
- **reset to 0 in `CutScene_bear`'s `Awake`** — which is what makes stars not survive a retry
  (requirement above), on both a fresh start and a return from `Success_ga`.

### Hook points (exactly two, both in `MagicStonePuzzleController`)

- `WordResultRoutine()` (line ~856, right after the existing `WordAssemblyTimer.Instance?.Pause()`):
  capture `bool builtInTime = WordAssemblyTimer.Instance != null && WordAssemblyTimer.Instance.SmokeRemaining > 0f`
  into a field, then award **star 1**.
- `RecordingSuccessRoutine()` (line ~1654, before `PlaySceneTransition`): if
  `activeResultWord == targetWord`, award **star 2**, and **star 3** as well when `builtInTime` — then
  wait for the award animation to finish before the scene transition.

Both calls go through `StarHud.Instance?.…`, so the other puzzle scenes (`CutScene_ga`, `CutScene_ta`),
which share `MagicStonePuzzleController` but have no star board, no-op safely.

## Out of scope

- Sending the star count to the backend / telemetry.
- Star boards in `CutScene_ga`, `CutScene_ta`, `Success_ga_correct`, `Success_ta_incorrect`.
- Any change to `WordAssemblyTimer`, the smoke shaders/materials, or the owl/crow cutscene timing.
- A star SFX (an optional `AudioClip` slot is exposed and left empty; wire it later if wanted).
