# Smoke Clock v2 (EdgeFog realism pass) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the CutScene_bear smoke clock read as real, pressuring smoke — an opaque billowing front that creeps in from all 4 screen edges and washes the scene out — instead of the current flat, patchy, half-transparent veil.

**Architecture:** No new GameObjects, no particle system, no new assets. The existing pieces stay exactly as they are (`FogOverlay` RawImage + `FogController` + `WordAssemblyTimer` progress providers + draw order below the book UI). Only the *look* changes: rewrite the fragment math of `EdgeFox.shader` (fbm + domain warp + solid core + long soft tail + front-only wisps + radial inward flow), let `FogController` drive its own accelerating `_FogTime`, and retune `EdgeFogMaterial.mat`.

**Tech Stack:** Unity 6 (URP 17.4 in manifest, but this is a plain built-in-style CG unlit shader drawn by a ScreenSpaceOverlay Canvas — keep it that way, do NOT convert to Shader Graph or URP HLSL), UGUI RawImage, C# MonoBehaviour, Editor MenuItem tooling.

## Global Constraints

- **Do NOT remove `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` from the shader Properties block.** UGUI always assigns the graphic texture to `_MainTex`; without the declaration Unity logs an error every frame and trips Error Pause. The fragment must keep ignoring it — the fog is 100% procedural.
- Shader name stays `"WordFlow/EdgeFog"` and the file stays at `Assets/Scenes/region 1/EdgeFox.shader` (note the typo in the filename — do not rename it, the .mat references it by GUID and the guid lives in `EdgeFox.shader.meta`).
- Keep the existing property names `_FogColor _Progress _MaxReach _Softness _NoiseScale _NoiseStrength _Density _FogTime _Speed` — the C# and the editor tools set them by name. New properties may be added.
- `FogController` lives in assembly `Game.Common` and must NOT reference `WordAssemblyTimer` (Assembly-CSharp) directly. The `SmokeActiveProvider` / `SmokeProgressProvider` static `Func`s stay.
- Fog draw order is already correct: `FogOverlay` sits just above `background`, so `book_craft*`, the letter stones and `time_root` render ABOVE the fog. This means the fog can be fully opaque without ever hurting readability — do not add a "center clear" mask, it is not needed.
- Unity MCP gotchas in this project: `unity_execute_code` does not work — use Editor `MenuItem` scripts + `unity_execute_menu_item`. Call `unity_select_instance` (port 7890) first. Overlay UI only shows in `unity_screenshot_game` (framebuffer), NOT `unity_graphics_game_capture`. Screenshots land ~75s late.
- There is no automated test for a shader. The check after every code task is: (1) `unity_get_compilation_errors` clean, (2) a Game-view screenshot at a known `_Progress` that visibly matches the intent.

---

## Reference analysis (why the current smoke fails)

The reference clip's fog feels real because of four things the current shader does not do:

| Reference behaviour | Current shader | Fix |
|---|---|---|
| Fog is **fully opaque** at the edge — the scene is *gone* there | `_Density = 0.55` caps alpha at 55%, so nothing is ever hidden | `_Density = 1`, and shape the falloff instead of capping it |
| The **core is solid**, only the *front* is wispy | `fog *= lerp(0.35, 1.0, detail)` multiplies noise over the whole field, including the screen edge → the deepest fog is full of holes | Apply the wisp modulation weighted by `1 - coreMask`, so noise only eats the leading edge |
| Big **billowing curls**, long soft gradient | single-octave value noise at scale 5, transition band `smoothstep(reach±0.08)` → repeated blobs behind a hard wall | 4-octave fbm + domain warp; falloff `1 - smoothstep(core, reach + softness, d)` giving a solid core and a long feathered tail |
| Smoke **advances and boils** toward the middle | noise scrolls sideways at constant speed (`uv + time`) | flow direction = radially inward (`-normalize(uv-0.5)`), warp offsets animate over time (boil), and the whole time base accelerates as the clock runs out |

Keeping the 4-edge closing frame is intentional (user confirmed) — only the material of the smoke changes.

---

## File Structure

- **Modify** `Assets/Scenes/region 1/EdgeFox.shader` — the entire fragment/helper section is replaced; Properties block gains 4 new knobs (`_CoreFrac`, `_WarpAmount`, `_WispStrength`, `_BoilSpeed`).
- **Modify** `Assets/Scripts/Common/FogController.cs` — stop feeding `Time.unscaledTime` into `_FogTime`; accumulate a local `fogTime` whose rate scales with progress (urgency).
- **Modify** `Assets/Scenes/region 1/EdgeFogMaterial.mat` — new default values (do this through the Unity Inspector / an editor script, not by hand-editing YAML, so the new float properties get serialized correctly).
- **Modify** `Assets/Editor/FogPreview.cs` — add 30% / 60% / 100% preview presets so the shape can be eyeballed at three points of the countdown without play mode.

---

### Task 1: Rewrite the EdgeFog fragment shader

**Files:**
- Modify: `Assets/Scenes/region 1/EdgeFox.shader` (whole file replaced)

**Interfaces:**
- Consumes: nothing.
- Produces: shader `"WordFlow/EdgeFog"` with float properties `_Progress _MaxReach _CoreFrac _Softness _NoiseScale _NoiseStrength _WarpAmount _WispStrength _Density _FogTime _Speed _BoilSpeed` and color `_FogColor`. `FogController` (Task 2) sets `_Progress` and `_FogTime` by name; `FogPreview` (Task 4) sets `_Progress`.

- [ ] **Step 1: Replace the whole shader file with the version below**

```hlsl
Shader "WordFlow/EdgeFog"
{
    Properties
    {
        // UGUI (RawImage/Image) always assigns the graphic texture to _MainTex; declaring it here
        // silences the per-frame "doesn't have a texture property '_MainTex'" console error (which
        // also trips Error Pause). The fragment ignores it — the fog is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FogColor ("Fog Color", Color) = (1, 1, 1, 1)
        _Progress ("Fog Progress", Range(0, 1)) = 0

        _MaxReach ("Max Reach", Range(0, 0.5)) = 0.34
        _CoreFrac ("Solid Core Fraction", Range(0, 1)) = 0.35
        _Softness ("Front Softness", Range(0.01, 0.5)) = 0.22

        _NoiseScale ("Noise Scale", Float) = 2.5
        _NoiseStrength ("Front Wobble", Range(0, 0.4)) = 0.18
        _WarpAmount ("Curl / Warp", Range(0, 1)) = 0.4
        _WispStrength ("Wisp Strength", Range(0, 1)) = 0.6

        _Density ("Density", Range(0, 1)) = 1

        _FogTime ("Fog Time", Float) = 0
        _Speed ("Inward Drift Speed", Float) = 0.05
        _BoilSpeed ("Boil Speed", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _FogColor;

            float _Progress;
            float _MaxReach;
            float _CoreFrac;
            float _Softness;

            float _NoiseScale;
            float _NoiseStrength;
            float _WarpAmount;
            float _WispStrength;
            float _Density;

            float _FogTime;
            float _Speed;
            float _BoilSpeed;


            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }


            float random(float2 p)
            {
                return frac(
                    sin(dot(p, float2(12.9898, 78.233)))
                    * 43758.5453
                );
            }


            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = random(i);
                float b = random(i + float2(1, 0));
                float c = random(i + float2(0, 1));
                float d = random(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }


            // Fractal brownian motion: 4 octaves of value noise. This is what turns the old flat
            // blobs into cloud-like billows. Result is normalised back to roughly 0..1.
            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    sum += noise(p) * amp;

                    // 2.02 (not 2.0) + an offset keeps the octaves from lining up on a grid.
                    p = p * 2.02 + float2(17.3, 9.1);
                    amp *= 0.5;
                }

                return sum / 0.9375;
            }


            // Domain-warped fbm: sample the noise field at coordinates that are themselves pushed
            // around by another noise field. This is what makes the smoke curl instead of slide.
            float SmokeField(float2 p, float boil)
            {
                float2 w = float2(
                    fbm(p + float2(0.0, boil)),
                    fbm(p + float2(5.2, 1.3) + float2(boil * 0.8, 0.0))
                );

                return fbm(p + (w - 0.5) * (_WarpAmount * 4.0));
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float t = _FogTime;
                float boil = t * _BoilSpeed;

                // The whole noise field drifts toward the centre of the screen, so every edge's
                // smoke reads as advancing inward rather than scrolling sideways.
                float2 c = uv - 0.5;
                float2 inward = -c / max(length(c), 0.001);
                float2 flow = inward * (t * _Speed);

                float2 p = uv * _NoiseScale + flow;

                // Distance to the nearest screen edge. min() over the 4 sides is the same closing
                // rectangular frame the old max()-of-4-sides produced, for a quarter of the cost.
                float d = min(
                    min(uv.x, 1.0 - uv.x),
                    min(uv.y, 1.0 - uv.y)
                );

                float reach = _Progress * _MaxReach;
                float core = reach * _CoreFrac;

                // Wobble the front, but fade the wobble out to nothing at the screen edge so the
                // core never develops holes.
                float n = SmokeField(p, boil);
                float edgeGuard = saturate(d / max(reach, 0.001));
                float dd = d + (n - 0.5) * _NoiseStrength * edgeGuard;

                // Solid up to the core, then one long soft gradient out past the reach. This wide
                // tail is what the reference fog has and the old hard smoothstep band did not.
                float fog = 1.0 - smoothstep(
                    core,
                    reach + _Softness,
                    dd
                );

                // Wisps: high-frequency detail that only eats the leading edge. coreMask -> 1 deep
                // inside the smoke, so the core stays opaque.
                float detail = fbm(
                    uv * _NoiseScale * 3.0
                    + flow * 1.6
                    + float2(boil * 0.4, -boil * 0.25)
                );

                float wisp = lerp(1.0 - _WispStrength, 1.0, detail);
                float coreMask = smoothstep(0.75, 1.0, fog);

                fog *= lerp(wisp, 1.0, coreMask);

                fog = saturate(fog) * _Density;

                // No smoke at all before the clock starts.
                fog *= smoothstep(0.0, 0.02, _Progress);

                return float4(
                    _FogColor.rgb,
                    fog * _FogColor.a
                );
            }

            ENDCG
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run (Unity MCP): `unity_select_instance` (port 7890), then `unity_get_compilation_errors`.
Expected: no errors mentioning `EdgeFox.shader`. If the shader itself fails, Unity reports it in the console — also run `unity_console_log` and confirm there is no `Shader error in 'WordFlow/EdgeFog'`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scenes/region 1/EdgeFox.shader"
git commit -m "feat(fog): fbm + domain-warped smoke with solid core and soft front"
```

---

### Task 2: Accelerating fog time in FogController

The smoke must churn faster as the clock runs out — a constant flow rate is the single biggest reason the current fog feels inert. `_FogTime` currently comes straight from `Time.unscaledTime`; replace it with a locally accumulated clock whose rate scales with the countdown progress.

**Files:**
- Modify: `Assets/Scripts/Common/FogController.cs`

**Interfaces:**
- Consumes: shader properties `_Progress` and `_FogTime` from Task 1.
- Produces: nothing new for other tasks. `SmokeActiveProvider` / `SmokeProgressProvider` static Funcs are unchanged — `WordAssemblyTimer` keeps working untouched.

- [ ] **Step 1: Add the urgency field**

In `Assets/Scripts/Common/FogController.cs`, inside the `[Header("Timing")]` block, after the `fogCurve` field, add:

```csharp
    [Header("Urgency")]
    [Tooltip("How much faster the smoke churns as the clock runs out. 0 = constant speed, " +
             "2 = 3x the drift/boil rate at zero seconds left.")]
    [SerializeField, Range(0f, 4f)]
    private float urgency = 2f;
```

- [ ] **Step 2: Add the local fog clock field**

Next to `private float elapsedTime;` add:

```csharp
    // Own time base instead of Time.unscaledTime: it speeds up with the countdown so the smoke
    // visibly churns harder in the last seconds. Monotonic, so the noise never jumps backwards.
    private float fogTime;
```

- [ ] **Step 3: Drive it in Update**

Replace the body of `Update()` (currently ends with `fogMaterial.SetFloat(FogTimeID, Time.unscaledTime);`) with:

```csharp
    private void Update()
    {
        float progress;

        if (SmokeActiveProvider != null && SmokeProgressProvider != null)
        {
            // Sync to the puzzle clock: no fog before the build starts, creeping in as the
            // countdown runs, full reach exactly when time is up. Frozen while the timer is paused.
            progress = SmokeActiveProvider()
                ? fogCurve.Evaluate(SmokeProgressProvider())
                : 0f;
        }
        else
        {
            // Standalone fallback (e.g. testing the fog scene on its own).
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / fogDuration);
            progress = fogCurve.Evaluate(normalizedTime);
        }

        SetProgress(progress);

        fogTime += Time.unscaledDeltaTime * (1f + progress * urgency);

        fogMaterial.SetFloat(
            FogTimeID,
            fogTime
        );
    }
```

- [ ] **Step 4: Reset it with the fog**

In `ResetFog()`, add `fogTime = 0f;` next to `elapsedTime = 0f;`:

```csharp
    public void ResetFog()
    {
        elapsedTime = 0f;
        fogTime = 0f;

        SetProgress(0f);
    }
```

- [ ] **Step 5: Verify compile**

Run (Unity MCP): `unity_get_compilation_errors`.
Expected: clean. `FogController` must still be in `Game.Common` with no reference to `WordAssemblyTimer`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Common/FogController.cs
git commit -m "feat(fog): fog time accelerates as the puzzle clock runs out"
```

---

### Task 3: Retune EdgeFogMaterial

The old material still carries `_Density 0.55` etc. and has none of the new float properties serialized. Write them with an editor script (a MenuItem — `unity_execute_code` does not work in this project) rather than hand-editing the `.mat` YAML.

**Files:**
- Create: `Assets/Editor/FogTune.cs`
- Modify (as a side effect of running it): `Assets/Scenes/region 1/EdgeFogMaterial.mat`

**Interfaces:**
- Consumes: the shader property names from Task 1.
- Produces: `Tools/Quest/Fog Apply Defaults` menu item.

- [ ] **Step 1: Create the editor script**

```csharp
using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Apply Defaults
/// Writes the tuned smoke-clock v2 values onto the shared EdgeFogMaterial. Kept as a MenuItem
/// because unity_execute_code does not work in this project — this is how an agent can set the
/// material without hand-editing the .mat YAML.
public static class FogTune
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";

    [MenuItem("Tools/Quest/Fog Apply Defaults")]
    public static void ApplyDefaults()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogTune] material not found: {MatPath}"); return; }

        mat.SetColor("_FogColor", Color.white);
        mat.SetFloat("_Density", 1f);
        mat.SetFloat("_MaxReach", 0.34f);
        mat.SetFloat("_CoreFrac", 0.35f);
        mat.SetFloat("_Softness", 0.22f);
        mat.SetFloat("_NoiseScale", 2.5f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.4f);
        mat.SetFloat("_WispStrength", 0.6f);
        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.12f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] smoke clock v2 defaults applied");
    }
}
```

- [ ] **Step 2: Run it**

Run (Unity MCP): `unity_execute_menu_item` with `Tools/Quest/Fog Apply Defaults`.
Expected console: `[FogTune] smoke clock v2 defaults applied`.

- [ ] **Step 3: Confirm the .mat picked up the new floats**

Read `Assets/Scenes/region 1/EdgeFogMaterial.mat` and confirm `m_Floats` now contains `_CoreFrac`, `_WarpAmount`, `_WispStrength`, `_BoilSpeed` and that `_Density: 1`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/FogTune.cs "Assets/Scenes/region 1/EdgeFogMaterial.mat"
git commit -m "chore(fog): tuned smoke clock v2 material defaults"
```

---

### Task 4: Preview presets and visual verification

**Files:**
- Modify: `Assets/Editor/FogPreview.cs`

**Interfaces:**
- Consumes: `_Progress` on the shared material.
- Produces: `Tools/Quest/Fog Preview 30%|60%|100%` and `Tools/Quest/Fog Preview Off`.

- [ ] **Step 1: Replace FogPreview.cs**

```csharp
using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Preview 30%|60%|100%|Off
/// Sets the shared EdgeFogMaterial's _Progress so you can see the smoke shape in the Game view
/// without entering play mode / running the whole puzzle. "Off" clears it back to 0.
/// (At runtime FogController instances the material and drives _Progress from the clock, so this
/// preview value never affects an actual play session.)
public static class FogPreview
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";

    [MenuItem("Tools/Quest/Fog Preview 30%")]
    public static void P30() => Set(0.3f);

    [MenuItem("Tools/Quest/Fog Preview 60%")]
    public static void P60() => Set(0.6f);

    [MenuItem("Tools/Quest/Fog Preview 100%")]
    public static void P100() => Set(1f);

    [MenuItem("Tools/Quest/Fog Preview Off")]
    public static void Off() => Set(0f);

    static void Set(float progress)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogPreview] material not found: {MatPath}"); return; }
        mat.SetFloat("_Progress", progress);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FogPreview] _Progress = {progress}");
    }
}
```

Note: `Tools/Quest/Fog Preview On` is gone. Grep for it (`Grep pattern "Fog Preview On"`) and confirm nothing else calls it before committing.

- [ ] **Step 2: Capture the three checkpoints**

For each of `Tools/Quest/Fog Preview 30%`, `60%`, `100%`:
1. `unity_execute_menu_item`
2. `unity_screenshot_game` (NOT `unity_graphics_game_capture` — that skips ScreenSpaceOverlay). Screenshots land ~75s late; wait for it.

Expected, judged by eye:
- **30%** — a thin haze hugging all 4 edges, wispy uneven front, background still fully readable in the middle.
- **60%** — a clearly opaque white band along each edge with a ragged, curling inner boundary; the forest/bear is being eaten from the sides; the book, stones and timer are still perfectly crisp on top.
- **100%** — a thick frame; only a soft-edged window remains in the middle at ~7% haze. Screen should feel like it is closing in, not like a grey rectangle border.

If the smoke looks like a *dirty texture* rather than *smoke*, the wisp is leaking into the core → lower `_WispStrength` or raise the `coreMask` smoothstep floor (0.75).

- [ ] **Step 3: Turn preview back off before committing**

Run: `Tools/Quest/Fog Preview Off`. The committed `.mat` must have `_Progress: 0`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/FogPreview.cs "Assets/Scenes/region 1/EdgeFogMaterial.mat"
git commit -m "chore(fog): fog preview presets at 30/60/100%"
```

---

### Task 5: Play-mode check and final tuning

- [ ] **Step 1: Run the real countdown**

Enter play mode on `CutScene_bear` (`unity_play_mode`) and let the bear cutscene run into the word-assembly puzzle so `WordAssemblyTimer.BeginFresh()` fires.

Expected:
- **Zero smoke during the bear cutscene** (`SmokeActive` is false until the clock starts). If smoke shows early, `FogController` fell back to its own `fogDuration` timer → the providers were not registered; check `WordAssemblyTimer.Awake` ran.
- Smoke starts at 30s, creeps steadily, and hits full reach exactly at 00:00.
- Smoke visibly churns faster in the last ~10s (that is `urgency`).
- Smoke freezes when a word is built (`Pause()`).

- [ ] **Step 2: Check performance**

The fragment now does ~16 value-noise samples per pixel (4 fbm × 4 octaves). Watch the Stats overlay / `unity_editor_state`. If the frame time regresses noticeably at fullscreen:
- first drop the fbm loop from 4 octaves to 3 (change `k < 4` to `k < 3` and the normaliser `0.9375` to `0.875`),
- then, if still slow, drop the domain warp to a single fbm call (`float2 w = float2(fbm(p), fbm(p + 5.2));` → replace with one `fbm` and reuse it for both components).
Do not optimise pre-emptively — measure first.

- [ ] **Step 3: Tuning table (hand these knobs to the user, they are all on `EdgeFogMaterial`)**

| Want | Knob |
|---|---|
| Smoke comes further in / eats more screen | `_MaxReach` ↑ (0.34 → 0.42) |
| More of it fully opaque (heavier, more oppressive) | `_CoreFrac` ↑ (0.35 → 0.5) |
| Softer, foggier, longer gradient | `_Softness` ↑ (0.22 → 0.35) |
| Bigger, slower billows | `_NoiseScale` ↓ (2.5 → 1.6) |
| More ragged, tongue-like front | `_NoiseStrength` ↑ (0.18 → 0.28) |
| More curl / turbulence | `_WarpAmount` ↑ (0.4 → 0.7) |
| Thinner, more torn leading edge | `_WispStrength` ↑ (0.6 → 0.8) |
| Faster creep | `_Speed` ↑ |
| Faster boiling in place | `_BoilSpeed` ↑ |
| More panic near 0s | `urgency` on `FogController` ↑ |
| Warmer / colder smoke | `_FogColor` (reference is pure white) |

- [ ] **Step 4: Commit any tuning changes**

```bash
git add "Assets/Scenes/region 1/EdgeFogMaterial.mat"
git commit -m "chore(fog): tune smoke clock after play-mode pass"
```

---

## Optional (only if the user asks after seeing v2)

Not in scope. Each of these is a separate, later decision:
- Tint the fog toward sickly grey/red past 85% progress (2 lines: `lerp(_FogColor.rgb, _PanicColor.rgb, smoothstep(0.85, 1, _Progress))`).
- A slow camera/canvas shake under 5 seconds.
- A low rumble that rises with `_Progress` (the `GameAudio` singleton already exists).

## Rollback

Everything is in 4 files and each task is its own commit. `git revert` the range, or `git checkout HEAD~N -- "Assets/Scenes/region 1/EdgeFox.shader"` to get the old shader back.
