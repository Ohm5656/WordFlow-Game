# Cloud Fog — Second Smoke Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second, grey, cloud-cluster-shaped smoke layer on top of the existing purple edge-fog layer in `CutScene_bear`, locked to the same puzzle countdown.

**Architecture:** A new procedural shader (`WordFlow/CloudFog`) on a second full-screen `RawImage` (`CloudOverlay`) in the same Canvas, driven by a second instance of the **existing, unmodified** `FogController`. `FogController` talks to its material through property *names* (`_Progress`, `_FogTime`, `_Pulse`) and reads the clock through **static** `Func`s that `WordAssemblyTimer` registers — so a second instance syncs to layer 1 for free. **Zero new runtime C#.**

**Tech Stack:** Unity (built-in render pipeline), CG/HLSL shader, UGUI ScreenSpaceOverlay Canvas, Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`) for driving the Editor.

Spec: `docs/superpowers/specs/2026-07-13-cloud-fog-second-layer-design.md`

## Global Constraints

- **Do not modify** `Assets/Scenes/region 1/EdgeFox.shader`, `Assets/Scenes/region 1/EdgeFogMaterial.mat`, `Assets/Scripts/Common/FogController.cs`, `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`, or the existing `FogOverlay` GameObject's own components/values. Layer 1 behaviour must be byte-for-byte unchanged.
- The new shader **must** declare `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}`. UGUI assigns the graphic texture to `_MainTex` on every graphic; without the declaration Unity logs `doesn't have a texture property '_MainTex'` every frame, which trips Error Pause and auto-pauses play mode. The fragment ignores it.
- Layer 2 covers the **whole screen uniformly** — no edge mask, no radial centre-clear. Readability is tuned with `_Density` alone.
- Layer 2 draws **above** `FogOverlay` and **below** `book_craft`, `book_craft_pa`, `book_craft_ga`, `time_root` and the stones. All of these live in one ScreenSpaceOverlay Canvas, where **sibling index is the draw order**.
- Every Unity scene edit happens **atomically inside one editor script that ends with `EditorSceneManager.SaveScene`**. Scene GameObjects created through separate MCP calls vanish on domain reload.
- `mcp__anklebreaker__unity_execute_code` **does not work in this project.** Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- Before any MCP call in a fresh session: `mcp__anklebreaker__unity_select_instance` with port `7890`. The bridge port can hop after a domain reload — re-run `unity_list_instances` if calls start failing.
- UI on a ScreenSpaceOverlay Canvas is only visible via `mcp__anklebreaker__unity_screenshot_game` (reads the framebuffer). `unity_graphics_game_capture` renders through the camera and will show an empty scene. Screenshots land ~75s late — poll, do not assume failure.
- Commit after each task.

---

### Task 1: The `WordFlow/CloudFog` shader

**Files:**
- Create: `Assets/Scenes/region 1/CloudFog.shader`

**Interfaces:**
- Consumes: nothing.
- Produces: `Shader "WordFlow/CloudFog"` with the driven properties `_Progress` (float 0..1), `_FogTime` (float, seconds, monotonic), `_Pulse` (float 0..1) — these names are what `FogController` sets and Task 2's material depends on. Tunables: `_CloudColor`, `_Density`, `_CloudScale`, `_Coverage`, `_Fluff`, `_Softness`, `_DriftSpeed`, `_BoilSpeed`, `_FadeIn`, `_PulseSwell`.

- [ ] **Step 1: Write the shader**

Create `Assets/Scenes/region 1/CloudFog.shader` with exactly this content:

```hlsl
Shader "WordFlow/CloudFog"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it here silences
        // the per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause
        // and auto-pauses play mode). The fragment ignores it — the clouds are procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _CloudColor ("Cloud Color", Color) = (0.62, 0.64, 0.68, 1)
        _Density ("Density", Range(0, 1)) = 0.55

        _CloudScale ("Cloud Scale (cells across)", Float) = 3.5
        _Coverage ("Base Coverage", Range(0, 1)) = 0.35
        _Fluff ("Edge Fluff", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18

        _DriftSpeed ("Drift Speed", Float) = 0.02
        _BoilSpeed ("Boil Speed", Float) = 0.08

        _FadeIn ("Opacity Fade-In (progress)", Range(0.02, 1)) = 0.45
        _PulseSwell ("Beat Pulse Swell", Range(0, 0.2)) = 0.04

        _Progress ("Progress (driven)", Range(0, 1)) = 0
        _FogTime ("Fog Time (driven)", Float) = 0
        _Pulse ("Beat Pulse (driven)", Range(0, 1)) = 0
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

            float4 _CloudColor;
            float _Density;

            float _CloudScale;
            float _Coverage;
            float _Fluff;
            float _Softness;

            float _DriftSpeed;
            float _BoilSpeed;

            float _FadeIn;
            float _PulseSwell;

            float _Progress;
            float _FogTime;
            float _Pulse;


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


            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    sum += noise(p) * amp;

                    p = p * 2.02 + float2(17.3, 9.1);
                    amp *= 0.5;
                }

                return sum / 0.9375;
            }


            // One round puff per cell, its centre and radius hashed from the cell id. Sampled over
            // the 3x3 neighbourhood and unioned with max(), so a puff that straddles a cell border
            // still bleeds into its neighbours — that soft union is what makes the puffs clump into
            // clusters instead of sitting in a visible grid.
            float PuffField(float2 p, float swell)
            {
                float2 cell = floor(p);
                float2 f = p - cell;

                float best = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 id = cell + o;

                        float2 jitter = float2(
                            random(id),
                            random(id + 31.7)
                        );

                        float2 center = o + 0.15 + jitter * 0.7;

                        float radius =
                            0.30
                            + random(id + 71.3) * 0.22
                            + swell;

                        float d = length(f - center);

                        float puff = 1.0 - smoothstep(
                            radius - _Softness,
                            radius,
                            d
                        );

                        best = max(best, puff);
                    }
                }

                return best;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float t = _FogTime;
                float boil = t * _BoilSpeed;

                // The overlay is a 16:9 rect but uv is 0..1 on both axes, so sample x in a stretched
                // space — otherwise every puff comes out as a squashed ellipse.
                float2 uv = float2(i.uv.x * 1.7777, i.uv.y);

                float2 p = uv * _CloudScale + float2(t * _DriftSpeed, 0.0);

                // Push the sample point around with fbm before evaluating the puffs, so the puff
                // edges break into cauliflower lumps. Without this they read as soap bubbles.
                float2 warp = float2(
                    fbm(p * 1.7 + float2(0.0, boil)),
                    fbm(p * 1.7 + float2(5.2, 1.3) - float2(boil * 0.6, 0.0))
                ) - 0.5;

                // Each countdown beep drives _Pulse to 1, swelling every puff — the clouds breathe
                // on the same beat the purple smoke lurches on.
                float puffs = PuffField(
                    p + warp * _Fluff * 2.0,
                    _Pulse * _PulseSwell
                );

                // Progress opens the clouds up: the threshold drops, so more of the puff field
                // crosses it and the clusters visibly grow and merge as the clock runs out.
                float cover = _Coverage + _Progress * (1.0 - _Coverage);
                float threshold = 1.0 - cover;

                float clouds = smoothstep(
                    threshold,
                    threshold + _Softness,
                    puffs
                );

                clouds = saturate(clouds) * _Density;

                // Materialise out of nothing instead of switching on at full weight.
                clouds *= smoothstep(0.0, _FadeIn, _Progress);

                return float4(
                    _CloudColor.rgb,
                    clouds * _CloudColor.a
                );
            }

            ENDCG
        }
    }
}
```

- [ ] **Step 2: Let Unity import it, then check for shader compile errors**

Unity compiles shaders on import. Focus the Editor so it refreshes, then read the console.

```
mcp__anklebreaker__unity_select_instance   port: 7890
mcp__anklebreaker__unity_console_clear
mcp__anklebreaker__unity_editor_state
mcp__anklebreaker__unity_console_log        (types: Error, Warning)
```

Expected: no `Shader error in 'WordFlow/CloudFog'` lines. If any appear, fix the shader and re-check — a shader that fails to compile renders as magenta and the whole task is worthless.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scenes/region 1/CloudFog.shader" "Assets/Scenes/region 1/CloudFog.shader.meta"
git commit -m "feat(fog): add the WordFlow/CloudFog shader — grey clustered cloud puffs"
```

---

### Task 2: Build the `CloudOverlay` layer into the scene

**Files:**
- Create: `Assets/Editor/AddCloudLayer.cs`
- Creates as a side effect (by script, not by hand — a hand-written `.mat` would need the shader's GUID): `Assets/Scenes/region 1/CloudFogMaterial.mat`
- Modifies (scene, via the script): `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Consumes: `Shader "WordFlow/CloudFog"` from Task 1; the existing `FogController` component (`Assets/Scripts/Common/FogController.cs`, unmodified) and its private serialized field `fogImage` (a `RawImage`, wired via `SerializedObject`); the existing `FogOverlay` GameObject.
- Produces: menu item `Tools/Quest/Add Cloud Layer`; a `CloudOverlay` GameObject (RawImage + FogController) under the same Canvas as `FogOverlay`, at sibling index `FogOverlay + 1`; the asset `Assets/Scenes/region 1/CloudFogMaterial.mat`. Tasks 3 and 4 depend on both names exactly as spelled.

- [ ] **Step 1: Write the editor script**

Create `Assets/Editor/AddCloudLayer.cs`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Add Cloud Layer
/// Adds the second smoke layer to CutScene_bear: a full-screen grey cloud-cluster overlay
/// (WordFlow/CloudFog) sitting directly above the purple FogOverlay and below the gameplay UI.
/// Reuses FogController as-is — it drives _Progress/_FogTime/_Pulse by name and reads the puzzle
/// clock through static providers, so the second instance syncs to the first for free.
/// Idempotent: re-running it updates the existing CloudOverlay instead of adding another.
public static class AddCloudLayer
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string ShaderName = "WordFlow/CloudFog";
    const string MatPath = "Assets/Scenes/region 1/CloudFogMaterial.mat";
    const string OverlayName = "CloudOverlay";

    [MenuItem("Tools/Quest/Add Cloud Layer")]
    public static void Run()
    {
        var mat = EnsureMaterial();
        if (mat == null) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // FogOverlay is the layer-1 smoke. Find it by its FogController, then walk to its parent
        // Canvas — the cloud layer must live in the same Canvas for sibling index to order them.
        var fog = Object.FindFirstObjectByType<FogController>(FindObjectsInactive.Include);
        if (fog == null)
        {
            Debug.LogError("[AddCloudLayer] no FogController (FogOverlay) found in CutScene_bear");
            return;
        }

        Transform fogT = fog.transform;
        Transform canvas = fogT.parent;
        if (canvas == null)
        {
            Debug.LogError("[AddCloudLayer] FogOverlay has no parent Canvas");
            return;
        }

        Transform existing = canvas.Find(OverlayName);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(OverlayName, typeof(RectTransform));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Add Cloud Layer");
            go.transform.SetParent(canvas, false);
        }

        // Stretch to fill the Canvas.
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        var raw = go.GetComponent<RawImage>();
        if (raw == null) raw = go.AddComponent<RawImage>();
        raw.material = mat;
        raw.color = Color.white;
        raw.raycastTarget = false;

        var ctrl = go.GetComponent<FogController>();
        if (ctrl == null) ctrl = go.AddComponent<FogController>();

        var so = new SerializedObject(ctrl);
        var imgProp = so.FindProperty("fogImage");
        if (imgProp != null && imgProp.objectReferenceValue != raw)
        {
            imgProp.objectReferenceValue = raw;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Directly above the purple smoke, still below book_craft* / time_root / stones — one
        // ScreenSpaceOverlay Canvas, so sibling index is the draw order.
        go.transform.SetSiblingIndex(fogT.GetSiblingIndex() + 1);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[AddCloudLayer] DONE — {OverlayName} at sibling index " +
                  $"{go.transform.GetSiblingIndex()} (FogOverlay is {fogT.GetSiblingIndex()}), scene saved");
    }

    static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat != null) return mat;

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[AddCloudLayer] shader not found: {ShaderName} — is CloudFog.shader imported and compiling?");
            return null;
        }

        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AddCloudLayer] created {MatPath}");
        return mat;
    }
}
```

- [ ] **Step 2: Check it compiles**

```
mcp__anklebreaker__unity_console_clear
mcp__anklebreaker__unity_get_compilation_errors
```

Expected: no errors. (`FogController` has no namespace and lives in `Game.Common`, which `Assembly-CSharp-Editor` already references — `FixFogLayer.cs` in the same folder uses it the same way, so this compiles.)

- [ ] **Step 3: Run the menu item**

```
mcp__anklebreaker__unity_execute_menu_item   menu_path: "Tools/Quest/Add Cloud Layer"
mcp__anklebreaker__unity_console_log
```

Expected console line: `[AddCloudLayer] DONE — CloudOverlay at sibling index N (FogOverlay is N-1), scene saved`

- [ ] **Step 4: Verify the hierarchy**

```
mcp__anklebreaker__unity_scene_hierarchy
```

Expected, under the Canvas, in this order: `background` … `FogOverlay` … `CloudOverlay` … then `book_craft`, `book_craft_pa`, `book_craft_ga`, `time_root` at **higher** sibling indices. If `CloudOverlay` ends up above any of those, stop — it will cover the book.

```
mcp__anklebreaker__unity_gameobject_info   name: "CloudOverlay"
```

Expected: `RectTransform` stretched (anchorMin 0,0 / anchorMax 1,1, offsets 0), `RawImage` with material `CloudFogMaterial`, `FogController` with `fogImage` pointing at that RawImage.

- [ ] **Step 5: Run it a second time (idempotence)**

```
mcp__anklebreaker__unity_execute_menu_item   menu_path: "Tools/Quest/Add Cloud Layer"
mcp__anklebreaker__unity_scene_hierarchy
```

Expected: still exactly **one** `CloudOverlay`. If there are two, the `canvas.Find(OverlayName)` reuse path is broken — fix before continuing.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/AddCloudLayer.cs Assets/Editor/AddCloudLayer.cs.meta \
        "Assets/Scenes/region 1/CloudFogMaterial.mat" "Assets/Scenes/region 1/CloudFogMaterial.mat.meta" \
        "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(fog): add the CloudOverlay layer above the purple smoke in CutScene_bear"
```

---

### Task 3: Keep the existing fog tools aware of the cloud layer

**Files:**
- Modify: `Assets/Editor/FixFogLayer.cs` (append after the existing `fogT.SetSiblingIndex(target);`, before `MarkSceneDirty`)
- Modify: `Assets/Editor/FogPreview.cs:30-38` (the `Set` method)

**Interfaces:**
- Consumes: `CloudOverlay` (name) and `Assets/Scenes/region 1/CloudFogMaterial.mat` (path) from Task 2.
- Produces: nothing new. `Tools/Quest/Fix Fog Layer` keeps the cloud layer directly above the fog layer; `Tools/Quest/Fog Preview *` now previews both layers.

Without this, the next run of `Fix Fog Layer` silently drops `CloudOverlay` out of order, and `Fog Preview` only ever moves the purple smoke.

- [ ] **Step 1: Extend `FixFogLayer.cs`**

In `Assets/Editor/FixFogLayer.cs`, after the line `fogT.SetSiblingIndex(target);` and before `EditorSceneManager.MarkSceneDirty(scene);`, insert:

```csharp
        // The grey cloud layer (Tools/Quest/Add Cloud Layer) rides directly above the purple smoke.
        // Re-anchor it here or every run of this tool would leave it stranded at its old index.
        Transform cloud = parent.Find("CloudOverlay");
        if (cloud != null)
        {
            cloud.SetSiblingIndex(fogT.GetSiblingIndex() + 1);
        }
```

Also update the class doc comment's first line to mention it moves both overlays — leave the rest as is.

- [ ] **Step 2: Extend `FogPreview.cs`**

Replace the `Set` method in `Assets/Editor/FogPreview.cs` with:

```csharp
    static void Set(float progress)
    {
        SetOn("Assets/Scenes/region 1/EdgeFogMaterial.mat", progress);
        SetOn("Assets/Scenes/region 1/CloudFogMaterial.mat", progress);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FogPreview] _Progress = {progress}");
    }

    static void SetOn(string path, float progress)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { Debug.LogError($"[FogPreview] material not found: {path}"); return; }
        mat.SetFloat("_Progress", progress);
        EditorUtility.SetDirty(mat);
    }
```

The old `const string MatPath` is now unused — delete it.

- [ ] **Step 3: Check it compiles**

```
mcp__anklebreaker__unity_console_clear
mcp__anklebreaker__unity_get_compilation_errors
```

Expected: no errors.

- [ ] **Step 4: Verify `Fix Fog Layer` still leaves the cloud on top of the fog**

```
mcp__anklebreaker__unity_execute_menu_item   menu_path: "Tools/Quest/Fix Fog Layer"
mcp__anklebreaker__unity_scene_hierarchy
```

Expected: `FogOverlay` then `CloudOverlay` adjacent, both still below `book_craft*` / `time_root`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/FixFogLayer.cs Assets/Editor/FogPreview.cs "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "chore(fog): teach Fix Fog Layer and Fog Preview about the cloud layer"
```

---

### Task 4: Visual verification and tuning

**Files:**
- Modify (tuning values only): `Assets/Scenes/region 1/CloudFogMaterial.mat`

**Interfaces:**
- Consumes: everything from Tasks 1-3.
- Produces: nothing. This task is the gate — it is where the feature is proven.

- [ ] **Step 1: Preview both layers at 60%**

```
mcp__anklebreaker__unity_execute_menu_item   menu_path: "Tools/Quest/Fog Preview 60%"
mcp__anklebreaker__unity_screenshot_game
```

The screenshot takes roughly 75 seconds to come back — that is normal, wait for it, do not conclude the tool is broken.

Expected in the image:
- purple smoke creeping in from the four edges (layer 1, unchanged);
- **grey puffy cloud clusters spread over the whole screen**, drawn on top of the purple;
- clouds are lumpy and clustered — not circles, not a uniform haze, not an edge frame;
- the book and the stones are still clearly readable.

- [ ] **Step 2: Tune if it looks wrong**

Edit `Assets/Scenes/region 1/CloudFogMaterial.mat` values in the Inspector (or via `mcp__anklebreaker__unity_component_set_property` on the material), then re-shoot Step 1. Do **not** edit the shader for these — that is what the knobs are for.

| Symptom | Knob |
|---|---|
| Clouds too heavy / book hard to read | lower `_Density` (0.55 → 0.35) |
| Clouds too few / too small | raise `_Coverage` (0.35 → 0.5) or lower `_CloudScale` (3.5 → 2.5 = fewer, bigger puffs) |
| Puffs look like bubbles, not clouds | raise `_Fluff` (0.35 → 0.6) |
| Clouds look like a grid | raise `_Fluff`; if it persists, raise `_CloudScale` |
| Too grey / washes out the purple | make `_CloudColor` lighter, or lower `_Density` |
| Motion too fast/slow | `_DriftSpeed`, `_BoilSpeed` |

- [ ] **Step 3: Clear the preview**

```
mcp__anklebreaker__unity_execute_menu_item   menu_path: "Tools/Quest/Fog Preview Off"
```

Both materials' `_Progress` back to 0. Confirm the console prints `[FogPreview] _Progress = 0` once (not an error about a missing material).

- [ ] **Step 4: Play-mode check**

Enter play mode on `CutScene_bear` and play through to the word build.

```
mcp__anklebreaker__unity_play_mode   action: "play"
```

Expected:
- clean screen for the first 5 seconds of the 30s clock (smoke starts at 25s remaining);
- **both** layers fade in together from that point — grey clouds and purple smoke, not one then the other;
- both thicken as the clock runs, peaking at 0s;
- both freeze while the timer is paused (word built);
- book, stones and `time_root` legible on top throughout;
- **console clean** — in particular no `doesn't have a texture property '_MainTex'` spam and no Error Pause.

```
mcp__anklebreaker__unity_console_log   (types: Error)
mcp__anklebreaker__unity_play_mode     action: "stop"
```

If the two layers do not fade in at the same moment, the second `FogController` is not seeing the static providers — check `CloudOverlay`'s `FogController` is enabled and its `fogImage` is wired (a null `fogImage` disables the component in `Awake` with a console error).

- [ ] **Step 5: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/CloudFogMaterial.mat"
git commit -m "feat(fog): tune the grey cloud layer against the purple smoke"
```

---

## Done when

- `CutScene_bear` shows two smoke layers during the word-build countdown: purple edge fog (unchanged) and grey clustered clouds on top of it.
- Both start together at 25s remaining and peak together at 0s.
- The book, stones and timer remain readable.
- `git diff` touches **no** layer-1 file: `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`, `WordAssemblyTimer.cs` are all untouched.
