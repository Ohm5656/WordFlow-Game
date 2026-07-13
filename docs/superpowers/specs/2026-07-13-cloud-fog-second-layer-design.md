# Second Smoke Layer (CutScene_bear) — grey haze over the purple front

Date: 2026-07-13
Status: approved (rev 3 — supersedes the "clustered clouds" design)

## Problem

`CutScene_bear` has one smoke layer: `FogOverlay`, a full-screen `RawImage` running
`WordFlow/EdgeFog` (`Assets/Scenes/region 1/EdgeFox.shader`) driven by the puzzle countdown
(`WordAssemblyTimer` → `FogController` → `_Progress`). It renders a **purple** smoke front creeping
in from the four screen edges.

That layer is finished and correct. Nobody touches it.

One smoke colour reads flat. We want a **second layer** for depth: the same creeping smoke front,
in **grey**, stacked over the purple one.

### Why not the "clustered clouds" design (rev 1-2)

Two builds tried giving layer 2 a distinct *silhouette* — puffy cumulus clusters from a procedural
puff-field shader (`CloudFog.shader`). Both were rejected on sight: the cloud shapes read as
decorative weather, not as a threat closing in. They pulled focus away from the smoke clock, which
is the whole point of the mechanic — a pressure gauge that makes the countdown legible and tense.

**The lesson: layer 2 must not have its own visual identity. It is a second coat of the same smoke.**
The depth comes from colour and offset, not from shape.

## Requirements

1. **Layer 1 untouched.** `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`,
   `WordAssemblyTimer.cs` and the `FogOverlay` GameObject stay byte-for-byte identical.
2. **Same smoke, different coat.** Layer 2 is the *same* edge-creeping front as layer 1 — same
   shader, same silhouette language, same reach maths. No second shape vocabulary.
3. **Colour: grey.** Neutral smoke-grey, no purple.
4. **Thin enough to read as a second layer, not a repaint.** Layer 2 must not bury the purple. It
   tints and softens; the purple front stays the dominant read.
5. **Fully tunable in the Inspector.** Every knob (colour, density, reach, noise, speeds) lives on
   layer 2's material. Changing the look must never require a code edit.
6. **Timing: locked to layer 1.** Same countdown, same `_Progress`. Both arrive together (smoke
   starts at 25s remaining), advance together, peak together at 0s. Frozen while the timer is paused.
7. **Draw order: above the purple smoke, below the gameplay UI.** `book_craft`, `book_craft_pa`,
   `book_craft_ga`, `time_root` and the stones render on top of both layers.

## Design

**No second shader.** Layer 2 runs `WordFlow/EdgeFog` — the *same shader asset as layer 1* — through
a second material. `CloudFog.shader` is deleted.

The two layers do not collapse into one because `EdgeFog`'s silhouette is generated from a noise
field whose shape is a function of `_NoiseScale`, `_Speed`, `_BoilSpeed` and `_WarpAmount`. Give
layer 2 different values for those and it produces a *different body of smoke* from the same code —
same visual language, different billows. `FogController` already instances its own material copy per
overlay (`new Material(fogImage.material)` in `Awake`), so the two never share state.

### Layer 2's material: `Assets/Scenes/region 1/CloudFogMaterial.mat`

Re-pointed from `WordFlow/CloudFog` to `WordFlow/EdgeFog`. Starting values, all Inspector-tunable:

| Property | Layer 1 (purple) | **Layer 2 (grey)** | Why |
|---|---|---|---|
| `_FogColor` | `(0.48, 0.28, 0.56)` | **`(0.62, 0.64, 0.68)`** | smoke grey |
| `_Density` | `0.75` | **`0.35`** | thin — tints the purple, never buries it (requirement 4) |
| `_MaxReach` | `0.23` | **`0.28`** | grey runs slightly ahead of the purple, so the front reads as a grey outer haze wrapping a denser purple core — that offset *is* the depth |
| `_CoreFrac` | `0.4` | **`0.35`** | slightly softer core |
| `_Softness` | `0.15` | **`0.2`** | wider tail on the outer coat |
| `_NoiseScale` | `2.0` | **`3.4`** | finer billows → a different body of smoke from the same shader |
| `_NoiseStrength` | `0.18` | **`0.22`** | |
| `_WarpAmount` | `0.55` | **`0.45`** | |
| `_WispStrength` | `0.7` | **`0.75`** | |
| `_Speed` | `0.05` | **`0.035`** | drifts at its own rate so the two fronts never lock together |
| `_BoilSpeed` | `0.16` | **`0.10`** | churns at its own rate |
| `_FadeIn` | `0.5` | **`0.5`** | arrives with layer 1 |
| `_PulseReach` | `0.03` | **`0.03`** | lurches on the same beep |

### Everything else stays

- `CloudOverlay` GameObject (RawImage + `FogController`, sibling index directly above `FogOverlay`) —
  unchanged. It never cared which shader its material used.
- `FogController.cs` — unchanged, and still zero new runtime C#. It drives `_Progress` / `_FogTime` /
  `_Pulse` by name and reads the clock through static `Func`s, so the second instance stays in sync
  for free.
- `Tools/Quest/Fix Fog Layer` (keeps `CloudOverlay` anchored above `FogOverlay`) and
  `Tools/Quest/Fog Preview *` (drives `_Progress` on both materials) — unchanged.
- `Assets/Editor/AddCloudLayer.cs` — one constant changes: `ShaderName` becomes `"WordFlow/EdgeFog"`,
  so re-running the tool cannot recreate a material on a shader that no longer exists.

### Deleted

- `Assets/Scenes/region 1/CloudFog.shader` (+ `.meta`) — the puff-field shader, dead once layer 2
  runs `EdgeFog`.

## Verification

1. `Tools/Quest/Fog Preview 60%` → the smoke front is visibly **two-tone**: a grey outer haze with a
   denser purple core behind it. Not a flat grey wash, not a purple front with a grey tint that could
   pass for one layer. Capture with `unity_screenshot_game` (framebuffer) — `unity_graphics_game_capture`
   skips ScreenSpaceOverlay UI. **The Game view must be open** or the capture is a blank ~100KB image.
2. `Tools/Quest/Fog Preview 100%` → both fronts at max reach, centre still clear, book/stones/timer
   readable.
3. `Tools/Quest/Fog Preview Off` → both clear.
4. Play through to the word build: both layers arrive together from the edges at 25s remaining,
   advance together, peak together at 0s. No console errors, no Error Pause.
5. `git diff` touches only `CloudFogMaterial.mat`, `AddCloudLayer.cs`, and the deleted
   `CloudFog.shader`. `EdgeFox.shader` / `EdgeFogMaterial.mat` / `FogController.cs` /
   `WordAssemblyTimer.cs` / `CutScene_bear.unity` untouched.

## Out of scope (deliberately cut)

- A second shader of any kind. Rev 1-2 proved the extra shape vocabulary is a liability, not a feature.
- Cloud sprites, ParticleSystems, radial centre-clear masks.
- Any new runtime C#.
