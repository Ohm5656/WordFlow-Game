# Cloud Fog — Edge-Creep Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the grey cloud layer (`CloudOverlay`) creep inward from the four screen edges together with the purple smoke, instead of appearing as a uniform full-screen field.

**Architecture:** Shader-only change. `_Progress` currently drives a global coverage threshold; it must instead drive an **edge band** using the same reach maths `EdgeFog` uses. The band's front is fed into the puff coverage threshold (not multiplied into the final alpha), so the advancing edge dissolves into individual clumps rather than ending on a smooth rectangle.

**Tech Stack:** Unity (built-in RP), CG/HLSL shader, Unity MCP (`anklebreaker`).

Spec (revised): `docs/superpowers/specs/2026-07-13-cloud-fog-second-layer-design.md` — requirement 4 and the "Growth" section were rewritten; this plan implements that revision.

## Context — what already exists

The layer was built and committed (`6f0068217`, `0a73aef26`, `1ee17c4cb`). Everything except the shader's growth maths is correct and stays:

- `Assets/Scenes/region 1/CloudFog.shader` — `Shader "WordFlow/CloudFog"`. **This is the only file whose logic changes.**
- `Assets/Scenes/region 1/CloudFogMaterial.mat` — one value changes (`_Coverage`).
- `CloudOverlay` GameObject in `CutScene_bear` (RawImage + `FogController`, sibling index directly above `FogOverlay`) — **no change**.
- `Assets/Editor/AddCloudLayer.cs`, `FixFogLayer.cs`, `FogPreview.cs` — **no change**.

## Global Constraints

- **Do not modify** `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`, `WordAssemblyTimer.cs`, the `FogOverlay` GameObject, or `CutScene_bear.unity`. Layer 1 stays byte-for-byte identical, and this fix needs no scene edit at all.
- Do not touch `AddCloudLayer.cs` / `FixFogLayer.cs` / `FogPreview.cs` — they are already correct.
- Keep `[PerRendererData] _MainTex` declared in the shader. Removing it brings back the per-frame `doesn't have a texture property '_MainTex'` error, which trips Error Pause and auto-pauses play mode.
- Before any MCP call: `mcp__anklebreaker__unity_list_instances`, then `unity_select_instance`. **The port hops between 7890 and 7891 on domain reload** — if calls start timing out, re-list and re-select rather than assuming Unity died. Pass `port:` on every call.
- `mcp__anklebreaker__unity_execute_code` does not work in this project. Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- **The Game view must be open or `unity_screenshot_game` silently captures a flat ~100KB image of the camera clear colour with no UI.** Run `unity_execute_menu_item("Window/General/Game")` once before the first screenshot, and sanity-check the resulting PNG is multi-MB, not ~100KB. Screenshots also land ~60-75s after the call returns — poll for the file, do not assume failure.
- Commit after each task.

---

### Task 1: Rewrite the shader's growth maths as an edge band

**Files:**
- Modify: `Assets/Scenes/region 1/CloudFog.shader` (Properties block + the `frag` function)
- Modify: `Assets/Scenes/region 1/CloudFogMaterial.mat` (one serialized float)

**Interfaces:**
- Consumes: `_Progress`, `_FogTime`, `_Pulse` — still set by `FogController` by name. Unchanged.
- Produces: four new tunables on the material — `_MaxReach`, `_CoreFrac`, `_EdgeSoftness`, `_PulseReach`. `_Coverage` changes meaning (now "how much of the puff field shows deep inside the band" rather than "base coverage before progress adds to it").

- [ ] **Step 1: Add the four new properties**

In `Assets/Scenes/region 1/CloudFog.shader`, in the `Properties` block, replace this line:

```hlsl
        _Coverage ("Base Coverage", Range(0, 1)) = 0.35
```

with these five lines:

```hlsl
        _Coverage ("Coverage Inside Band", Range(0, 1)) = 0.55

        _MaxReach ("Max Reach", Range(0, 0.5)) = 0.26
        _CoreFrac ("Solid Core Fraction", Range(0, 1)) = 0.4
        _EdgeSoftness ("Front Softness", Range(0.01, 0.5)) = 0.15
```

And below the existing `_PulseSwell` property line, add:

```hlsl
        _PulseReach ("Beat Pulse Reach", Range(0, 0.1)) = 0.03
```

- [ ] **Step 2: Declare the new uniforms**

In the same file, in the CGPROGRAM uniform block, find:

```hlsl
            float _CloudScale;
            float _Coverage;
            float _Fluff;
            float _Softness;
```

and replace it with:

```hlsl
            float _CloudScale;
            float _Coverage;
            float _Fluff;
            float _Softness;

            float _MaxReach;
            float _CoreFrac;
            float _EdgeSoftness;
```

Then find:

```hlsl
            float _FadeIn;
            float _PulseSwell;
```

and replace it with:

```hlsl
            float _FadeIn;
            float _PulseSwell;
            float _PulseReach;
```

- [ ] **Step 3: Replace the growth maths in `frag`**

In the `frag` function, find this block (the global-coverage version):

```hlsl
                // Progress opens the clouds up: the threshold drops, so more of the puff field
                // crosses it and the clusters visibly grow and merge as the clock runs out.
                float cover = _Coverage + _Progress * (1.0 - _Coverage);
                float threshold = 1.0 - cover;

                float clouds = smoothstep(
                    threshold,
                    threshold + _Softness,
                    puffs
                );
```

and replace it with:

```hlsl
                // Distance to the nearest screen edge, measured in unstretched uv — the same ruler
                // EdgeFog uses, so the two layers advance in step instead of drifting apart.
                float d = min(
                    min(i.uv.x, 1.0 - i.uv.x),
                    min(i.uv.y, 1.0 - i.uv.y)
                );

                // Same reach maths as the purple layer: the band grows in from all four edges as the
                // clock runs out and lurches on each countdown beep. _MaxReach < 0.5, so the front
                // never reaches the centre and the book stays readable.
                float reach = _Progress * _MaxReach + _Pulse * _PulseReach;
                float core = reach * _CoreFrac;

                float front = 1.0 - smoothstep(
                    core,
                    reach + _EdgeSoftness,
                    d
                );

                // The front feeds the puff coverage THRESHOLD rather than multiplying the final
                // alpha. Deep in the band every puff clears the threshold; at the leading edge only
                // the biggest ones do. That is what dissolves the advancing front into separate
                // clumps and stragglers — multiplying the alpha instead would fade a full-screen
                // cloud field behind a smooth rectangular window, which is the bug this fixes.
                float threshold = lerp(1.0, 1.0 - _Coverage, front);

                float clouds = smoothstep(
                    threshold,
                    threshold + _Softness,
                    puffs
                );
```

Leave everything after it (`clouds = saturate(clouds) * _Density;`, the `_FadeIn` ramp, the return) exactly as is.

- [ ] **Step 4: Update the stale `_Coverage` value on the material**

The material was created before this change and has `_Coverage: 0.35` serialized, which under the new meaning gives a band so sparse only puff cores show. The four new properties are absent from the `.mat` and will correctly pick up the shader defaults; only this one line is stale.

In `Assets/Scenes/region 1/CloudFogMaterial.mat`, find:

```yaml
    - _Coverage: 0.35
```

and change it to:

```yaml
    - _Coverage: 0.55
```

- [ ] **Step 5: Verify the shader compiles**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance    port: <from the list>
mcp__anklebreaker__unity_console_clear      port: <port>
mcp__anklebreaker__unity_execute_menu_item  menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_console_log        type: "error"   port: <port>
```

Expected: no `Shader error in 'WordFlow/CloudFog'`. A shader that fails to compile renders magenta — if you see errors, fix them before going on. (`[Telemetry] 404` / `[Session] open failed` ngrok warnings are pre-existing and unrelated; ignore them.)

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/region 1/CloudFog.shader" "Assets/Scenes/region 1/CloudFogMaterial.mat"
git commit -m "fix(fog): the grey clouds now creep in from the edges with the purple smoke"
```

---

### Task 2: Visual verification — prove the band creeps

**Files:**
- Modify (tuning values only, if needed): `Assets/Scenes/region 1/CloudFogMaterial.mat`

**Interfaces:**
- Consumes: Task 1's shader.
- Produces: nothing. This is the gate — the whole point of the fix is what these screenshots show.

- [ ] **Step 1: Open the Game view (or every screenshot is a blank rectangle)**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"   port: <port>
```

- [ ] **Step 2: Shoot 15% — the shot that proves the fix**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 15%"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/CloudEdge_15.png"   port: <port>
```

Poll for the file (it lands ~60-75s later) and check it is multi-MB, then read it.

**Expected — this is the pass/fail criterion for the whole plan:** grey clouds form a **thin fringe hugging the four screen edges**, and **the middle of the screen is empty**. If the clouds are spread across the whole screen (even faintly), the fix did not take — the `front` term is not reaching the threshold. Re-check Step 3 of Task 1.

- [ ] **Step 3: Shoot 60% and 100%**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 60%"    port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/CloudEdge_60.png"   port: <port>
```

Expected at 60%: the grey band has advanced inward, sitting over the purple one, its leading edge broken into puffs and clumps (not a smooth rectangle). Centre still clear.

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 100%"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/CloudEdge_100.png"   port: <port>
```

Expected at 100%: both bands at maximum reach, centre still clear, book and stones readable.

- [ ] **Step 4: Tune if needed**

Edit values in `Assets/Scenes/region 1/CloudFogMaterial.mat` and re-shoot. Do not edit the shader for these — that is what the knobs are for.

| Symptom | Knob |
|---|---|
| Band too shallow / clouds barely enter | raise `_MaxReach` (0.26 → 0.32; keep `< 0.5`) |
| Band eats the book | lower `_MaxReach` (0.26 → 0.20) |
| Front is a smooth rectangle, not clumpy | raise `_EdgeSoftness` (0.15 → 0.25) and/or lower `_CoreFrac` (0.4 → 0.25) — a wider, softer front gives the puffs more room to break it up |
| Band is sparse / gappy deep inside | raise `_Coverage` (0.55 → 0.7) |
| Clouds too heavy | lower `_Density` (0.55 → 0.4) |
| Puffs too small / too many | lower `_CloudScale` (3.5 → 2.5) |

- [ ] **Step 5: Clear the preview**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview Off"   port: <port>
```

Expected console: `[FogPreview] _Progress = 0`, no missing-material error.

- [ ] **Step 6: Play-mode check**

```
mcp__anklebreaker__unity_play_mode   action: "play"   port: <port>
```

Confirm `isPlaying: true` via `unity_editor_state` (the first `play` call after a domain reload sometimes does not take — if it reports `false`, call `play` again). Let it run to the word build, screenshot, then:

```
mcp__anklebreaker__unity_console_log   type: "error"   port: <port>
mcp__anklebreaker__unity_play_mode     action: "stop"   port: <port>
```

Expected: both layers arrive together from the edges once the clock passes 25s remaining, advance inward together, peak together at 0s. Book, stones and `time_root` legible throughout. No `_MainTex` spam, no Error Pause. (The ngrok `[Telemetry] 404` warnings are pre-existing — not a failure.)

- [ ] **Step 7: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/CloudFogMaterial.mat"
git commit -m "feat(fog): tune the grey cloud band against the purple smoke front"
```

(If you tuned nothing, skip — nothing to commit.)

---

## Done when

- At `Fog Preview 15%` the grey clouds are a thin fringe on the four edges and the centre of the screen is empty.
- At 60% and 100% the band has advanced inward with a clumpy, puff-broken front, still clearing the centre.
- In play mode both layers arrive from the edges together and peak together at 0s.
- `git diff` touches only `CloudFog.shader` and `CloudFogMaterial.mat`. No scene edit, no C# edit, and `EdgeFox.shader` / `EdgeFogMaterial.mat` / `FogController.cs` / `WordAssemblyTimer.cs` untouched.
