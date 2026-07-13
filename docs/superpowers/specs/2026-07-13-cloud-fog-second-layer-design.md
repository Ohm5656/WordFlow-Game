# Cloud Fog — Second Smoke Layer (CutScene_bear)

Date: 2026-07-13
Status: approved, ready for implementation

## Problem

`CutScene_bear` currently has exactly one smoke layer: `FogOverlay`, a full-screen `RawImage`
running the `WordFlow/EdgeFog` shader (`Assets/Scenes/region 1/EdgeFox.shader`). It renders a
**purple** smoke frame that creeps inward from the four screen edges, driven by the puzzle
countdown (`WordAssemblyTimer` → `FogController` → `_Progress`).

That layer is finished and correct. Nobody touches it.

What is missing is depth: one smoke shape reads flat. We want a **second layer** with a different
silhouette and a different colour — **grey, cloud-coloured, made of clustered puffy clouds** rather
than an edge-creeping fog front — sitting on top of the purple smoke.

## Requirements

1. **New layer only.** `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs` and the existing
   `FogOverlay` GameObject are not modified in any way that changes layer-1 behaviour.
2. **Shape: clustered clouds.** Overlapping rounded puffs with fluffy, noise-broken edges —
   visually distinct from layer 1's edge fog front. Not a frame, not a gradient.
3. **Colour: cloud grey.** Neutral storm-cloud grey, no purple.
4. **Coverage: uniform full screen.** No edge mask, no radial centre-clear. Readability is managed
   with the single `_Density` knob.
5. **Timing: locked to layer 1.** Same countdown, same `_Progress`. Both layers appear together
   (smoke starts at 25s remaining) and thicken together, reaching maximum exactly when the clock
   hits zero. Frozen while the timer is paused.
6. **Draw order: above the purple smoke, below the gameplay UI.** `book_craft`, `book_craft_pa`,
   `book_craft_ga`, `time_root` and the stones must still render on top of both layers.

## Design

Three new files, one new GameObject, two small edits to existing editor tools. No new runtime C#.

### 1. `Assets/Scenes/region 1/CloudFog.shader` — `Shader "WordFlow/CloudFog"`

A UGUI-compatible unlit transparent shader. Independent of `EdgeFog`; shares only property *names*.

**Silhouette — the puff field.** UV space is divided into cells (`_CloudScale`). Each cell holds one
round puff whose centre and radius are hashed from the cell id, so puffs sit at irregular positions
and overlap into clusters. Puff coverage is accumulated across the 3×3 cell neighbourhood (a puff
near a cell border must still bleed into the neighbouring cell) and combined with `max` — a soft
union, which is what produces the cauliflower cluster silhouette rather than a bank of separate
circles.

**Fluff.** The puff edge is displaced by fbm value noise (`_Fluff`), reusing the same 4-octave fbm
construction as `EdgeFog` (copied, not shared — a `.cginc` for two shaders is not worth the file).
Without this the puffs read as bubbles; with it they read as clouds.

**Motion.** The noise/puff field drifts sideways (`_DriftSpeed`) and boils in place (`_BoilSpeed`),
both fed from `_FogTime`. Slow: clouds hang, they do not race.

**Growth.** `_Progress` (0..1) drives two things:
- coverage threshold — as progress rises, the alpha threshold drops, so more puffs cross it and the
  clusters visibly grow and merge;
- opacity — `smoothstep(0, _FadeIn, _Progress)` so the layer materialises out of nothing instead of
  switching on (same fix already applied to `EdgeFog`).

**Beat pulse.** `_Pulse` (1 on each countdown beep, decaying) swells every puff radius by
`_PulseSwell`, so the clouds breathe on the same beat the purple smoke lurches on.

**Required declaration.** `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` MUST be
declared even though the fragment ignores it. UGUI always assigns the graphic texture to `_MainTex`;
without the declaration Unity logs `doesn't have a texture property '_MainTex'` every frame, which
trips Error Pause and makes play mode auto-pause. This bit `EdgeFog` already — do not repeat it.

**Properties**

| Property | Purpose | Default |
|---|---|---|
| `_MainTex` | `[PerRendererData]`, ignored (see above) | white |
| `_CloudColor` | cloud grey | `(0.62, 0.64, 0.68, 1)` |
| `_Density` | overall opacity — the single readability knob | `0.55` |
| `_CloudScale` | cells across the screen; higher = more, smaller puffs | `3.5` |
| `_Coverage` | base coverage before `_Progress` adds to it | `0.35` |
| `_Fluff` | fbm edge break-up strength | `0.35` |
| `_Softness` | puff edge softness | `0.18` |
| `_DriftSpeed` | sideways drift | `0.02` |
| `_BoilSpeed` | in-place churn | `0.08` |
| `_FadeIn` | progress over which opacity ramps in | `0.45` |
| `_PulseSwell` | puff swell per beat | `0.04` |
| `_Progress` | 0..1, driven by `FogController` | `0` |
| `_FogTime` | driven by `FogController` | `0` |
| `_Pulse` | driven by `FogController` | `0` |

Render state matches `EdgeFog`: `Queue=Transparent`, `Blend SrcAlpha OneMinusSrcAlpha`,
`ZWrite Off`, `ZTest Always`, `Cull Off`.

### 2. `Assets/Scenes/region 1/CloudFogMaterial.mat`

Material on `WordFlow/CloudFog`, defaults as tabled above. All tuning happens here — no config
asset, no tuning script.

### 3. `Assets/Editor/AddCloudLayer.cs` — `Tools/Quest/Add Cloud Layer`

Idempotent one-shot scene builder (re-running it must not create a second overlay). In a single
editor script, so the scene edit survives domain reload:

1. `EditorSceneManager.OpenScene("Assets/Scenes/region 1/CutScene_bear.unity", Single)`.
2. Find the existing `FogOverlay` (via `FogController` / by name) and take its parent Canvas.
3. Find or create a child `CloudOverlay` under that same Canvas:
   - `RectTransform` stretched full screen (anchorMin `0,0`, anchorMax `1,1`, offsets `0`, pivot `0.5,0.5`);
   - `RawImage`, material = `CloudFogMaterial`, `raycastTarget = false`;
   - `FogController` (the existing component, unmodified) with `fogImage` wired to its own `RawImage`.
4. `CloudOverlay.SetSiblingIndex(FogOverlay.GetSiblingIndex() + 1)` — above the purple smoke,
   still below `book_craft*` / `time_root` / stones, because those sit at higher sibling indices in
   the same ScreenSpaceOverlay Canvas (sibling index *is* the draw order there).
5. `MarkSceneDirty` + `SaveScene`.

### 4. `Assets/Editor/FixFogLayer.cs` — extend

After it repositions `FogOverlay`, it must also re-anchor `CloudOverlay` to
`FogOverlay` sibling index + 1 when present. Otherwise every future run of `Tools/Quest/Fix Fog Layer`
silently drops the cloud layer out of order.

### 5. `Assets/Editor/FogPreview.cs` — extend

`Set(progress)` currently writes `_Progress` on `EdgeFogMaterial` only. It must write the same value
to `CloudFogMaterial` too, so `Tools/Quest/Fog Preview 15/30/60/100%|Off` previews both layers.

## Why no new runtime C#

`FogController` addresses its material through `Shader.PropertyToID("_Progress" | "_FogTime" | "_Pulse")`
— it is not bound to a particular shader, only to those property names. Its clock hooks
(`SmokeActiveProvider`, `SmokeProgressProvider`, `SmokeRemainingProvider`) are **static** `Func`s
registered once by `WordAssemblyTimer`, so a second `FogController` instance reads the same clock and
is automatically in sync with layer 1. `Awake()` already instances its own material copy, so the two
overlays do not fight over shared state and editor preview values never leak into a play session.

So layer 2 is a second `FogController` on a second RawImage pointed at a second material. Nothing else.

## Verification

1. `Tools/Quest/Add Cloud Layer` → console reports the sibling index; no errors.
2. `Tools/Quest/Fog Preview 60%` → Game view shows purple edge smoke *and* grey cloud clusters,
   grey on top. Capture with `unity_screenshot_game` (framebuffer) — `unity_graphics_game_capture`
   renders through the camera and skips ScreenSpaceOverlay UI, so it will look empty.
3. `Tools/Quest/Fog Preview Off` → both layers clear.
4. Play `CutScene_bear` through to the word build: nothing for the first 5s of the 30s clock, both
   layers fade in together from 25s remaining, both thicken, both peak at 0s. Book, stones and the
   timer stay legible on top. No `_MainTex` console spam, no Error Pause.
5. `Tools/Quest/Fix Fog Layer` re-run → cloud layer still directly above the fog layer.

## Out of scope (deliberately cut)

- Radial centre-clear mask — rejected; readability is `_Density` only.
- Any change to `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`, or the timer.
- Cloud sprite assets / ParticleSystem approaches.
- A shared `.cginc` for the fbm helpers — two shaders do not justify it.
