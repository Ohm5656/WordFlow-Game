# Grey Smoke Coat — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace layer 2's puffy-cloud look with a second coat of the *same* creeping smoke as layer 1 — grey, thin, offset — and delete the cloud shader.

**Architecture:** Layer 2 stops having its own shader. `CloudOverlay`'s material is re-pointed to `WordFlow/EdgeFog`, the shader layer 1 already uses, with grey/thin/different-noise values. `EdgeFog`'s silhouette is a function of `_NoiseScale` / `_Speed` / `_BoilSpeed` / `_WarpAmount`, so different values yield a different body of smoke from the same code. `CloudFog.shader` is deleted. No scene edit, no new C#.

**Tech Stack:** Unity (built-in RP), Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`).

Spec (rev 3): `docs/superpowers/specs/2026-07-13-cloud-fog-second-layer-design.md`

## Context — what exists right now

Layer 2 was built over three earlier commits and currently renders **grey puffy cloud clusters** creeping in from the edges. The clusters were rejected: they read as decorative weather and pull focus off the smoke clock. Everything about layer 2 stays *except* its shader and material values.

- `Assets/Scenes/region 1/CloudFog.shader` — the puff-field shader. **Gets deleted.**
- `Assets/Scenes/region 1/CloudFogMaterial.mat` — currently on `WordFlow/CloudFog`. **Gets re-pointed to `WordFlow/EdgeFog` with new values.**
- `Assets/Editor/AddCloudLayer.cs` — one constant changes.
- `CloudOverlay` GameObject in `CutScene_bear` (RawImage + `FogController`, sibling index directly above `FogOverlay`) — **no change**. It never cared which shader its material ran.
- `FixFogLayer.cs`, `FogPreview.cs`, `FogController.cs` — **no change**.

## Global Constraints

- **Do not modify** `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`, `WordAssemblyTimer.cs`, the `FogOverlay` GameObject, or `CutScene_bear.unity`. Layer 1 stays byte-for-byte identical and this change needs no scene edit.
- Do not write a new shader. The entire point of this revision is that layer 2 has no shader of its own.
- Before any MCP call: `mcp__anklebreaker__unity_list_instances`, then `unity_select_instance`. **The port hops between 7890 and 7891 on domain reload** — if calls time out, re-list and re-select rather than assuming Unity died. Pass `port:` on every call.
- `mcp__anklebreaker__unity_execute_code` does not work in this project. Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- **The Game view must be open or `unity_screenshot_game` silently captures a flat ~100KB image with no UI.** Run `unity_execute_menu_item("Window/General/Game")` once before the first screenshot and sanity-check the PNG is multi-MB. Screenshots land ~60-75s after the call returns — poll for the file.
- The `[Telemetry] 404` / `[Session] open failed` ngrok warnings in the console are pre-existing and unrelated. Ignore them.
- Commit after each task.

---

### Task 1: Re-point layer 2 onto the EdgeFog shader and delete CloudFog

**Files:**
- Rewrite: `Assets/Scenes/region 1/CloudFogMaterial.mat`
- Modify: `Assets/Editor/AddCloudLayer.cs:15`
- Delete: `Assets/Scenes/region 1/CloudFog.shader` and `Assets/Scenes/region 1/CloudFog.shader.meta`

**Interfaces:**
- Consumes: `Shader "WordFlow/EdgeFog"` (GUID `a7a84712c5d6f60419f9b2587de7d749`, defined in `Assets/Scenes/region 1/EdgeFox.shader`) — layer 1's shader, used as-is and unmodified. Its driven properties are `_Progress`, `_FogTime`, `_Pulse`, set by `FogController` by name.
- Produces: `CloudFogMaterial.mat` running `WordFlow/EdgeFog` in grey. Same asset path and GUID as before, so `CloudOverlay`'s RawImage reference and `FogPreview`'s path lookup both keep working untouched.

- [ ] **Step 1: Rewrite the material**

Replace the entire contents of `Assets/Scenes/region 1/CloudFogMaterial.mat` with:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: CloudFogMaterial
  m_Shader: {fileID: 4800000, guid: a7a84712c5d6f60419f9b2587de7d749, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _BoilSpeed: 0.1
    - _CoreFrac: 0.35
    - _Density: 0.35
    - _FadeIn: 0.5
    - _FogTime: 0
    - _MaxReach: 0.28
    - _NoiseScale: 3.4
    - _NoiseStrength: 0.22
    - _Progress: 0
    - _Pulse: 0
    - _PulseReach: 0.03
    - _Softness: 0.2
    - _Speed: 0.035
    - _WarpAmount: 0.45
    - _WispStrength: 0.75
    m_Colors:
    - _FogColor: {r: 0.62, g: 0.64, b: 0.68, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
```

The shader GUID `a7a84712c5d6f60419f9b2587de7d749` is `EdgeFox.shader`. The values differ from
`EdgeFogMaterial.mat` on purpose — grey, thinner (`_Density` 0.35 vs 0.75), reaching slightly further
(`_MaxReach` 0.28 vs 0.23), and on a different noise field (`_NoiseScale` 3.4 vs 2.0, slower
`_Speed`/`_BoilSpeed`) so the two fronts are different bodies of smoke rather than the same one
tinted twice.

- [ ] **Step 2: Point `AddCloudLayer` at the right shader**

In `Assets/Editor/AddCloudLayer.cs`, change line 15 from:

```csharp
    const string ShaderName = "WordFlow/CloudFog";
```

to:

```csharp
    const string ShaderName = "WordFlow/EdgeFog";
```

Otherwise a future run of `Tools/Quest/Add Cloud Layer` (after the material is ever deleted) would
try to build the material on a shader that no longer exists and log an error.

- [ ] **Step 3: Delete the cloud shader**

```bash
git rm "Assets/Scenes/region 1/CloudFog.shader" "Assets/Scenes/region 1/CloudFog.shader.meta"
```

- [ ] **Step 4: Reimport and verify nothing broke**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance    port: <from the list>
mcp__anklebreaker__unity_console_clear      port: <port>
mcp__anklebreaker__unity_execute_menu_item  menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
mcp__anklebreaker__unity_console_log        type: "error"   port: <port>
```

Expected: zero compilation errors, and **no missing-shader / missing-material errors**. A material
whose shader is gone renders magenta — if `CloudOverlay` goes magenta, Step 1 did not take (check the
GUID).

Confirm the material actually landed on the right shader:

```
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "CloudOverlay"   componentType: "RawImage"   port: <port>
```

Expected: `m_Material` → `CloudFogMaterial` at `Assets/Scenes/region 1/CloudFogMaterial.mat`.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scenes/region 1/CloudFogMaterial.mat" Assets/Editor/AddCloudLayer.cs
git commit -m "refactor(fog): layer 2 is now a grey coat of the same EdgeFog smoke, not clouds

The puff-field cloud shader read as decorative weather and pulled focus off
the smoke clock. Layer 2 now runs layer 1's shader through a second material
- grey, thinner, on a different noise field - so the depth comes from colour
and offset instead of a second shape vocabulary. CloudFog.shader is deleted."
```

---

### Task 2: Visual verification — prove it reads as two coats

**Files:**
- Modify (tuning values only, if needed): `Assets/Scenes/region 1/CloudFogMaterial.mat`

**Interfaces:**
- Consumes: Task 1's material.
- Produces: nothing. This is the gate.

- [ ] **Step 1: Open the Game view**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"   port: <port>
```

- [ ] **Step 2: Shoot 60% — the shot that decides it**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 60%"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/GreyCoat_60.png"   port: <port>
```

Poll for the file (~60-75s, must be multi-MB), then read it.

**Expected — the pass/fail criterion:** the smoke front reads as **two tones**: a grey outer haze
with a denser purple core behind/inside it. Both are the same kind of smoke — billowing, edge-creeping
— not two different shapes.

**Fail modes and what they mean:**
- Front looks like one flat grey wall → grey is too heavy; lower `_Density`.
- Cannot tell there are two layers at all → the fronts are too similar; push `_NoiseScale` further
  apart (3.4 → 4.5) and/or widen the `_MaxReach` gap (0.28 → 0.31).
- Any puffy round cloud shapes still visible → the old shader is still bound. Re-check Task 1 Step 1.

- [ ] **Step 3: Shoot 100%**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 100%"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/GreyCoat_100.png"   port: <port>
```

Expected: both fronts at max reach, centre still clear, book / stones / timer readable. The purple is
still the dominant read — the grey has not buried it.

- [ ] **Step 4: Tune if needed**

Edit values in `Assets/Scenes/region 1/CloudFogMaterial.mat` and re-shoot. Never edit a shader for
these.

| Symptom | Knob |
|---|---|
| Grey buries the purple | lower `_Density` (0.35 → 0.25) |
| Grey barely visible | raise `_Density` (0.35 → 0.5) |
| Two layers indistinguishable | widen the `_NoiseScale` gap (3.4 → 4.5) and the `_MaxReach` gap (0.28 → 0.31) |
| Grey haze reaches too far in / crowds the book | lower `_MaxReach` (0.28 → 0.24) |
| Grey too cold / too blue | warm `_FogColor` toward `(0.66, 0.65, 0.64)` |
| Fronts move in lockstep | pull `_Speed` / `_BoilSpeed` further from layer 1's `0.05` / `0.16` |

- [ ] **Step 5: Clear the preview**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview Off"   port: <port>
```

Expected console: `[FogPreview] _Progress = 0`, no missing-material error.

- [ ] **Step 6: Play-mode check**

```
mcp__anklebreaker__unity_play_mode   action: "play"   port: <port>
```

Confirm `isPlaying: true` via `unity_editor_state` (the first `play` after a domain reload sometimes
does not take — call `play` again if it reports `false`). The bear intro runs ~60s before the word
build; wait it out, screenshot, then:

```
mcp__anklebreaker__unity_console_log   type: "error"   port: <port>
mcp__anklebreaker__unity_play_mode     action: "stop"   port: <port>
```

Expected: both coats arrive together from the edges once the clock passes 25s remaining, advance
together, peak together at 0s. Book, stones and `time_root` legible throughout. No console errors, no
Error Pause.

- [ ] **Step 7: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/CloudFogMaterial.mat"
git commit -m "feat(fog): tune the grey smoke coat against the purple front"
```

(Skip if you tuned nothing.)

---

## Done when

- The smoke front reads as two coats: grey outer haze over a denser purple core, both the same kind of billowing edge-creeping smoke.
- No puffy cloud shapes anywhere.
- Both coats arrive and peak together with the countdown; book, stones and timer stay readable.
- Every look knob is on `CloudFogMaterial` and adjustable in the Inspector.
- `CloudFog.shader` is gone from the repo.
- `git diff` touches only `CloudFogMaterial.mat`, `AddCloudLayer.cs`, and the deleted shader. `EdgeFox.shader` / `EdgeFogMaterial.mat` / `FogController.cs` / `WordAssemblyTimer.cs` / `CutScene_bear.unity` untouched.
