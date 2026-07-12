# Smoke Clock v3 (pacing + readability pass) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the CutScene_bear smoke clock *pressuring* instead of *blinding* — a smoke frame that never reaches the centre of the screen, that stays completely absent for the first 10 seconds, and that lurches inward on every countdown beep.

**Architecture:** v2 (commits `eb8b245b0`..`9c6811f81`) built the smoke *look* — fbm + domain warp + solid core + soft front, synced to `WordAssemblyTimer`. It is over-tuned: at `_Progress = 1` the four soft tails overlap in the middle and white out the whole screen, and the smoke starts the instant the book pops in. v3 changes no rendering technique — it re-tunes reach/opacity so the smoke stays a *frame*, re-times it into three acts via the existing `fogCurve`, and adds one new mechanic: a per-beep pulse that syncs the smoke to the countdown audio.

**Tech Stack:** Unity 6 (6000.4.3f1), plain CG unlit shader on a UGUI ScreenSpaceOverlay RawImage, C# MonoBehaviour, Editor MenuItem tooling, Unity MCP (anklebreaker + UnityMCP servers).

## Design rationale (read this before touching numbers)

Pressure comes from *watching a threat close in*, not from *being unable to see*. Every game that does timer/danger pressure well keeps the player's decision-making area clear: Overwatch low-health is a red vignette with a crystal-clear centre; Mario Kart's blue-shell warning never covers the road. The reference clip the user liked has fully-opaque fog because there the fog **is the lose state** — here the smoke is a **countdown**, a different job.

Two concrete defects in v2, both confirmed in play-mode screenshots:

1. **Too opaque, reaches too far.** `_MaxReach 0.34` + `_Softness 0.22` means the fog's influence extends `0.34 + 0.22 = 0.56` in UV from *every* edge. Opposite edges overlap past the centre (0.5), so at `_Progress = 1` the entire screen is solid gray. Fix: `_MaxReach 0.26` + `_Softness 0.15` → influence ends at `0.41 < 0.5`, so the centre alpha is mathematically **0** even at full progress. The smoke becomes a heavy frame that curls *behind* the book (draw order already puts the book above the fog) and never touches the middle.

2. **No grace period.** `_Progress = 1 - remaining/totalSeconds` is linear, so smoke appears the instant `BeginFresh()` fires — the same frame the book pops in. The player has not read the puzzle yet. Fix: re-author `fogCurve` (already a serialized `AnimationCurve` on `FogController` — no new code path) so it stays flat at 0 for the first third.

The three acts land on the beat the game already has: `WordAssemblyTimer.countdownAt = 10f`, so the countdown beep starts with exactly 10 seconds left.

| Act | Clock | Normalized progress in | `fogCurve` out | What the player sees |
|-----|-------|------------------------|----------------|----------------------|
| 1 — Calm | 30s → 20s | 0.00 → 0.33 | **0.00** | Nothing. Clean screen, read the puzzle. |
| 2 — Creep | 20s → 10s | 0.33 → 0.67 | 0.00 → 0.30 | Thin haze finds the edges. "Something's coming." |
| 3 — Panic | 10s → 0s | 0.67 → 1.00 | 0.30 → 1.00 | Thick frame, churning, **lurching inward on every beep**, colour darkening. |

The new **pulse** is the highest-value addition: on each countdown beep the smoke surges ~3% further in, then settles. Audio and visual hitting the same beat is what makes a countdown feel like a countdown rather than a filter.

Deliberately **not** doing: red/danger tint (this is a children's Thai-literacy game — an oppressive storm-gray reads as pressure without reading as horror), screen shake, blur.

## Global Constraints

- **Do NOT remove `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` from the shader Properties block.** UGUI assigns the graphic texture to `_MainTex`; without the declaration Unity logs an error every frame and trips Error Pause.
- Shader stays `"WordFlow/EdgeFog"` at `Assets/Scenes/region 1/EdgeFox.shader` (filename typo is intentional — the .mat references it by GUID).
- Keep every existing property name. New properties may be added.
- `FogController` is in assembly `Game.Common` and must **not** reference `WordAssemblyTimer` (Assembly-CSharp) directly. Communication stays via the static `System.Func` providers.
- Fog draw order is already correct (`FogOverlay` sits just above `background`; book, stones and `time_root` render above it). Do not change sibling order, and do not add a centre-clear mask — the reach math already guarantees a clear centre.
- **Unity MCP gotchas in this project:**
  - `unity_execute_code` does **not** work. Use Editor `MenuItem` scripts + `unity_execute_menu_item`.
  - Call `unity_list_instances` → `unity_select_instance` first, and **re-run it after every script compile** — the bridge port hops on domain reload (it went 7890 → 7891 during v2).
  - `unity_get_compilation_errors` only covers **C#** (CompilationPipeline). It does **not** report shader HLSL errors. Use `Tools/Quest/Fog Shader Check` (`Assets/Editor/FogShaderCheck.cs`, added in v2) for those.
  - Game-view screenshots must use `unity_screenshot_game` (framebuffer), never `unity_graphics_game_capture` (skips ScreenSpaceOverlay). They land ~60-90s late — poll for the file.
  - Scene edits vanish on domain reload. Any scene mutation must happen atomically inside one editor script that ends with `EditorSceneManager.SaveScene`.
- **Verify visuals by sampling pixels, not by eyeballing.** v2 wasted an hour because pale fog over a pale background is invisible to the eye but obvious in the pixel values. Use the Python snippet in Task 5.

---

## File Structure

- **Modify** `Assets/Scenes/region 1/EdgeFox.shader` — add `_PanicColor`, `_Pulse`, `_PulseReach`; fold the pulse into `reach`; lerp the colour by progress.
- **Modify** `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs` — expose `SmokeRemaining` and register a third provider.
- **Modify** `Assets/Scripts/Common/FogController.cs` — consume `SmokeRemainingProvider`, compute the per-beep pulse, push `_Pulse` to the material.
- **Modify** `Assets/Editor/FogTune.cs` — new material values, plus write the three-act `fogCurve` onto the `FogController` instance in the scene and save the scene.

---

### Task 1: Shader — pulse and panic colour

**Files:**
- Modify: `Assets/Scenes/region 1/EdgeFox.shader`

**Interfaces:**
- Consumes: nothing.
- Produces: three new shader properties that Task 3 and Task 4 drive by name — `_Pulse` (float, 0..1, set every frame by `FogController`), `_PulseReach` (float, material-authored), `_PanicColor` (colour, material-authored).

- [ ] **Step 1: Add the three new properties**

In the `Properties` block, replace this line:

```hlsl
        _FogColor ("Fog Color", Color) = (0.78, 0.80, 0.84, 1)
```

with:

```hlsl
        _FogColor ("Fog Color", Color) = (0.78, 0.80, 0.84, 1)
        _PanicColor ("Panic Color (last 30%)", Color) = (0.52, 0.55, 0.63, 1)
```

Then, immediately after the `_BoilSpeed` line, add:

```hlsl
        _Pulse ("Beat Pulse (driven)", Range(0, 1)) = 0
        _PulseReach ("Beat Pulse Reach", Range(0, 0.1)) = 0.03
```

- [ ] **Step 2: Declare them in the CG block**

After the existing `float4 _FogColor;` declaration, add:

```hlsl
            float4 _PanicColor;
```

After the existing `float _BoilSpeed;` declaration, add:

```hlsl
            float _Pulse;
            float _PulseReach;
```

- [ ] **Step 3: Fold the pulse into reach**

In `frag`, replace:

```hlsl
                float reach = _Progress * _MaxReach;
                float core = reach * _CoreFrac;
```

with:

```hlsl
                // Each countdown beep drives _Pulse to 1, so the whole smoke front lurches inward
                // and settles back. Audio and visual landing on the same beat is what sells the
                // countdown — without it the fog is just a filter that happens to be growing.
                float reach = _Progress * _MaxReach + _Pulse * _PulseReach;
                float core = reach * _CoreFrac;
```

- [ ] **Step 4: Darken the smoke through the panic act**

Replace the final return:

```hlsl
                return float4(
                    _FogColor.rgb,
                    fog * _FogColor.a
                );
```

with:

```hlsl
                // Act 3: the smoke turns darker and heavier as the clock runs out. Deliberately a
                // storm-gray, not a red — this is a children's game, the goal is pressure not dread.
                float3 col = lerp(
                    _FogColor.rgb,
                    _PanicColor.rgb,
                    smoothstep(0.7, 1.0, _Progress)
                );

                return float4(
                    col,
                    fog * _FogColor.a
                );
```

- [ ] **Step 5: Compile and verify — C# AND shader**

```
unity_list_instances            # port may have hopped
unity_select_instance <port>
unity_get_compilation_errors    # C# only
unity_execute_menu_item "Tools/Quest/Fog Shader Check"
```

Then `read_console` filtered on `FogShaderCheck`.
Expected: `[FogShaderCheck] isSupported=True` and `[FogShaderCheck] message count = 0`.
If `message count` is non-zero, each message is logged as a warning with file:line — fix and re-run.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/region 1/EdgeFox.shader"
git commit -m "feat(fog): beat pulse + panic colour on the smoke clock"
```

---

### Task 2: WordAssemblyTimer — expose remaining seconds

`FogController` needs the raw countdown seconds (not the 0..1 progress) to know when a beep lands. It cannot reference `WordAssemblyTimer` directly (assembly boundary), so this goes through a third static `Func`, exactly like the two that already exist.

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`

**Interfaces:**
- Consumes: `FogController.SmokeRemainingProvider` (the static field added in Task 3 — if you are doing Task 2 before Task 3 the code will not compile until Task 3 lands, so **do Task 3 first or do both before compiling**).
- Produces: `WordAssemblyTimer.SmokeRemaining` (float, seconds left, 0 when not started).

- [ ] **Step 1: Add the property**

After the existing `SmokeProgress01` property, add:

```csharp
    /// <summary>Seconds left on the clock (0 when not started). The smoke fog reads this to sync a
    /// visual pulse to each countdown beep — the beep fires when the displayed second ticks over.</summary>
    public float SmokeRemaining => started ? remaining : 0f;
```

- [ ] **Step 2: Register the provider in Awake**

In `Awake()`, after the two existing provider assignments, add:

```csharp
        FogController.SmokeRemainingProvider = () => Instance != null ? Instance.SmokeRemaining : 0f;
```

- [ ] **Step 3: Clear it in OnDestroy**

In `OnDestroy()`, inside the `if (Instance == this)` block, after the two existing nulls, add:

```csharp
            FogController.SmokeRemainingProvider = null;
```

- [ ] **Step 4: Do NOT compile yet** — `FogController.SmokeRemainingProvider` does not exist until Task 3. Proceed straight to Task 3, then compile once.

---

### Task 3: FogController — drive the per-beep pulse

**Files:**
- Modify: `Assets/Scripts/Common/FogController.cs`

**Interfaces:**
- Consumes: `SmokeRemainingProvider` (registered by Task 2), shader property `_Pulse` (Task 1).
- Produces: `public static System.Func<float> SmokeRemainingProvider;` — the field Task 2 assigns to.

- [ ] **Step 1: Add the provider field**

Next to the existing `SmokeActiveProvider` / `SmokeProgressProvider` declarations, add:

```csharp
    /// Seconds left on the puzzle clock. Used to land a visual pulse on each countdown beep.
    public static System.Func<float> SmokeRemainingProvider;
```

- [ ] **Step 2: Add the pulse tunables**

In the `[Header("Urgency")]` block, after the `urgency` field, add:

```csharp
    [Tooltip("Seconds left at which the countdown beep starts — must match WordAssemblyTimer's " +
             "countdownAt. Inside this window the smoke lurches inward on every beep.")]
    [SerializeField]
    private float pulseWindow = 10f;

    [Tooltip("How fast each beat pulse decays. Higher = sharper, snappier lurch.")]
    [SerializeField, Range(1f, 12f)]
    private float pulseDecay = 6f;
```

- [ ] **Step 3: Add the shader property ID**

Next to `ProgressID` / `FogTimeID`, add:

```csharp
    private static readonly int PulseID =
        Shader.PropertyToID("_Pulse");
```

- [ ] **Step 4: Compute and push the pulse in Update**

At the end of `Update()`, after the existing `fogMaterial.SetFloat(FogTimeID, fogTime);` call, add:

```csharp
        fogMaterial.SetFloat(
            PulseID,
            ComputeBeatPulse()
        );
    }


    /// <summary>
    /// 1 at the instant a countdown beep lands, decaying to ~0 before the next one. The beep fires
    /// when the displayed second ticks over, i.e. when the remaining time crosses an integer — so
    /// the time since the last beep is 1 - frac(remaining).
    /// </summary>
    private float ComputeBeatPulse()
    {
        if (SmokeRemainingProvider == null)
        {
            return 0f;
        }

        float remaining = SmokeRemainingProvider();

        if (remaining <= 0.01f || remaining > pulseWindow)
        {
            return 0f;
        }

        float sinceBeep = 1f - (remaining - Mathf.Floor(remaining));

        return Mathf.Exp(-sinceBeep * pulseDecay);
    }
```

(Note the closing brace: `Update()` now ends right after the `SetFloat(PulseID, ...)` call, and `ComputeBeatPulse()` is a new method that follows it.)

- [ ] **Step 5: Compile — both files together**

```
unity_list_instances            # port may have hopped
unity_select_instance <port>
unity_get_compilation_errors
```
Expected: 0 errors. If `SmokeRemainingProvider` is reported as missing, Task 2 Step 1-3 were not applied.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Common/FogController.cs Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs
git commit -m "feat(fog): smoke lurches inward on every countdown beep"
```

---

### Task 4: Retune the material and author the three-act curve

The `fogCurve` lives as a serialized `AnimationCurve` on the `FogController` component in `CutScene_bear` — changing the C# default will **not** touch the existing instance, so it must be written into the scene. Do the material and the curve in one atomic editor script that ends in a scene save.

**Files:**
- Modify: `Assets/Editor/FogTune.cs`
- Modified as a side effect: `Assets/Scenes/region 1/EdgeFogMaterial.mat`, `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Consumes: shader property names from Task 1, serialized field names `fogCurve` and `urgency` from `FogController`.
- Produces: `Tools/Quest/Fog Apply Defaults` (updated), which now also writes the scene.

- [ ] **Step 1: Replace `Assets/Editor/FogTune.cs` entirely**

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Fog Apply Defaults
/// Writes the smoke-clock v3 tuning: material values + the three-act fogCurve on the FogController
/// in CutScene_bear. Kept as a MenuItem because unity_execute_code does not work in this project.
/// Scene edits are done atomically and saved immediately — scene mutations are lost on domain reload.
public static class FogTune
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    [MenuItem("Tools/Quest/Fog Apply Defaults")]
    public static void ApplyDefaults()
    {
        ApplyMaterial();
        ApplyScene();
    }

    static void ApplyMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogTune] material not found: {MatPath}"); return; }

        // Pale cool gray, not pure white: the puzzle's book page / canvas backdrop is itself
        // near-white, so white-on-white fog is invisible.
        mat.SetColor("_FogColor", new Color(0.78f, 0.80f, 0.84f, 1f));

        // Act 3 darkens toward a heavy storm-gray. Not red — this is a children's game.
        mat.SetColor("_PanicColor", new Color(0.52f, 0.55f, 0.63f, 1f));

        // Reach + softness are the readability guarantee: influence ends at 0.26 + 0.15 = 0.41,
        // which is < 0.5, so the four fronts can never meet and the centre alpha is exactly 0 even
        // at full progress. The smoke is a frame, never a blindfold.
        mat.SetFloat("_MaxReach", 0.26f);
        mat.SetFloat("_Softness", 0.15f);
        mat.SetFloat("_CoreFrac", 0.45f);
        mat.SetFloat("_Density", 0.9f);

        mat.SetFloat("_NoiseScale", 2.0f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.55f);
        mat.SetFloat("_WispStrength", 0.7f);

        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.16f);

        mat.SetFloat("_Pulse", 0f);
        mat.SetFloat("_PulseReach", 0.03f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] material: smoke clock v3 values applied");
    }

    static void ApplyScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var fog = Object.FindAnyObjectByType<FogController>();
        if (fog == null) { Debug.LogError("[FogTune] FogController not found in CutScene_bear"); return; }

        // Three acts over the 30s clock. Flat 0 for the first third so the player gets a clean
        // screen to read the puzzle when the book pops in, then a creep, then a rush that lands
        // exactly where WordAssemblyTimer.countdownAt (10s) starts the beep.
        var curve = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),   // 30s left — book pops in, screen clean
            new Keyframe(0.33f, 0.00f),   // 20s left — still clean
            new Keyframe(0.50f, 0.12f),   // 15s left — first wisps find the edges
            new Keyframe(0.67f, 0.30f),   // 10s left — beep starts, smoke clearly present
            new Keyframe(0.85f, 0.62f),   //  4.5s left
            new Keyframe(1.00f, 1.00f)    //  0s left — full frame
        );

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        var so = new SerializedObject(fog);
        so.FindProperty("fogCurve").animationCurveValue = curve;
        so.FindProperty("urgency").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(fog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[FogTune] scene: three-act fogCurve + urgency=3 written to CutScene_bear");
    }
}
```

- [ ] **Step 2: Compile, then run it**

```
unity_list_instances / unity_select_instance
unity_get_compilation_errors      # expect 0
unity_execute_menu_item "Tools/Quest/Fog Apply Defaults"
```

Expected console:
```
[FogTune] material: smoke clock v3 values applied
[FogTune] scene: three-act fogCurve + urgency=3 written to CutScene_bear
```

- [ ] **Step 3: Confirm both files actually changed**

Read `Assets/Scenes/region 1/EdgeFogMaterial.mat` — `m_Floats` must contain `_PulseReach: 0.03`, `_MaxReach: 0.26`, `_Softness: 0.15`, `_Density: 0.9`, and `m_Colors` must contain `_PanicColor`.

Run `git diff --stat -- "Assets/Scenes/region 1/CutScene_bear.unity"` — it must show the scene as modified (the curve keys + urgency).

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/FogTune.cs "Assets/Scenes/region 1/EdgeFogMaterial.mat" "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(fog): three-act smoke pacing, frame-only reach, no blindfold"
```

---

### Task 5: Verify — the centre must stay clear, act 1 must stay empty

**Files:** none (verification only).

- [ ] **Step 1: Static check at full progress — the most important assertion**

```
unity_execute_menu_item "Tools/Quest/Fog Preview 100%"
unity_screenshot_game  path: Assets/Screenshots/v3_full.png
```

Poll for the file (it lands ~60-90s later), then sample it. **Do not judge this by eye** — pale fog over a pale background reads as "nothing there" visually while being clearly present in the pixel values, which is exactly the trap that cost an hour in v2:

```bash
cd "C:/Users/NTP/Documents/NSC-Game" && python -c "
from PIL import Image
img = Image.open('Assets/Screenshots/v3_full.png').convert('RGB')
w,h = img.size
px = img.load()
# fog-only pixels: sample the top strip, which has no gameplay UI over it
for t in [0.00,0.02,0.05,0.08,0.11,0.14,0.18,0.22,0.26,0.32,0.38,0.44,0.50]:
    y = int(t*h); x = int(0.5*w)
    print(f'depth {t:.2f} from top edge -> rgb {px[x,y]}')
"
```

Expected: near the top edge the pixels are a solid gray (~200,205,215 blended over the sky); by `depth 0.41` they must be **indistinguishable from the un-fogged background**, and at `depth 0.50` (the vertical centre) there must be **zero** fog contribution. If the centre is tinted, `_MaxReach + _Softness` exceeded 0.5 — re-check Task 4's material values.

- [ ] **Step 2: Turn the preview back off**

```
unity_execute_menu_item "Tools/Quest/Fog Preview Off"
```
The committed `.mat` must have `_Progress: 0`.

- [ ] **Step 3: Play-mode — act 1 must be empty**

Enter play mode on `CutScene_bear` and let the bear cutscene run into the puzzle. Screenshot right after the book pops in (the timer will read ~00:28).

Expected: **no smoke at all.** The clock is running, the book is up, the screen is clean. If smoke is visible here, the `fogCurve` was not written to the scene instance — re-run Task 4 and confirm `git diff` shows `CutScene_bear.unity` changed.

- [ ] **Step 4: Play-mode — act 3 must pulse**

Screenshot again when the clock reads under 00:10. Expected: a clear heavy frame of smoke on all four edges, visibly darker than in act 2, and the book/stones/timer still perfectly crisp on top of it. The pulse is motion, so it will not show in a still — confirm it instead by checking that `_Pulse` is non-zero: it is driven every frame, so if `ComputeBeatPulse` were broken it would sit at 0 and the reach would simply not lurch. If you want to prove it, temporarily raise `_PulseReach` to 0.1 in the material and watch the Game view.

- [ ] **Step 5: Play-mode — pause must freeze**

Build a word. The timer pauses (`WordAssemblyTimer.Pause()`), the board hides, and the smoke must **freeze at its current reach** — not advance, not reset.

- [ ] **Step 6: Commit any tuning, then stop play mode**

```bash
git add "Assets/Scenes/region 1/EdgeFogMaterial.mat"
git commit -m "chore(fog): tune smoke clock v3 after play-mode pass"
```

---

## Tuning table (all on `EdgeFogMaterial` unless noted)

| Want | Knob |
|---|---|
| Smoke frame thicker / thinner | `_MaxReach` — **keep `_MaxReach + _Softness < 0.5`** or the centre stops being clear |
| Softer, foggier edge | `_Softness` — same ceiling applies |
| More of the frame fully opaque | `_CoreFrac` ↑ |
| Overall heavier | `_Density` ↑ (1.0 = fully opaque core) |
| Bigger, slower billows | `_NoiseScale` ↓ |
| More curl / turbulence | `_WarpAmount` ↑ |
| More torn, tongue-like front | `_WispStrength` ↑ / `_NoiseStrength` ↑ |
| Bigger lurch on each beep | `_PulseReach` ↑ (0.03 → 0.06) |
| Snappier lurch | `pulseDecay` on `FogController` ↑ |
| Darker / more oppressive finale | `_PanicColor` |
| More churn in the last seconds | `urgency` on `FogController` ↑ |
| Grace period longer / shorter | the `0.33` key in `FogTune.ApplyScene` (0.33 = 10 of 30 seconds) |

## Rollback

Each task is its own commit. `git revert` the range, or `git checkout <v2-sha> -- "Assets/Scenes/region 1/EdgeFox.shader"` to restore v2's shader. v2's final state is commit `9c6811f81`.
