# Smoke Clock v4 (uniform colour + weight recalibration) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One smoke colour for the entire countdown — the dark storm-gray that v3 only reached in the final ~3 seconds — and dial the smoke's weight down so it stops feeling too thick.

**Architecture:** No rendering technique changes. Delete the `_PanicColor` progress-tint from the shader, make `_FogColor` *be* the dark colour, and re-tune three material floats (`_Density`, `_MaxReach`, `_CoreFrac`) plus `_FadeIn`. The `fogCurve`, the beat pulse, the providers, and the draw order all stay exactly as v3 left them.

**Tech Stack:** Unity 6 (6000.4.3f1), plain CG unlit shader on a UGUI ScreenSpaceOverlay RawImage, C# MonoBehaviour, Editor MenuItem tooling, Unity MCP (anklebreaker + UnityMCP servers), Python + Pillow for pixel verification.

---

## Diagnosis (measured, not guessed)

Two complaints, one root cause.

The v3 shader tints the smoke by progress: `lerp(_FogColor, _PanicColor, smoothstep(0.7, 1.0, _Progress))`. Because `_Progress` is the *fogCurve output*, it only crosses 0.7 with about 3.5 seconds left — so the entire colour shift is crammed into the last 3 seconds:

| clock | progress | lerp | colour |
|-------|----------|------|--------|
| 20s → 4s | 0.00 → 0.66 | **0.00** | RGB(199, 204, 214) — pale |
| 3s | 0.75 | 0.07 | RGB(195, 200, 211) |
| 2s | 0.83 | 0.41 | RGB(172, 178, 192) |
| 1s | 0.92 | 0.81 | RGB(145, 153, 171) |
| 0s | 1.00 | **1.00** | RGB(133, 140, 161) — dark |

That is the "last frame is a different colour" bug.

The second complaint ("too thick again") comes from the *same* line. Sampling the real background that the fog band actually covers, in a no-fog play-mode screenshot (`v3_act1b.png`, 130k pixels under `d < 0.26`):

| region | background | contrast vs PALE fog | contrast vs DARK fog |
|--------|-----------|---------------------|---------------------|
| top band (sky) | RGB(124,166,195) | 85.8 | 44.2 |
| side bands (village) | RGB(161,142,107) | 129.7 | 60.9 |
| bottom band (grass) | RGB(132,131,101) | 150.4 | 60.7 |

The background under the frame is a **mid-tone village**, not bright sky. So the pale fog is the *high-contrast* one — it shouts. Perceived weight ≈ `alpha × contrast`:

- **10s left** (pale colour, alpha 0.67): 0.67 × ~120 = **79**
- **0s left** (dark colour, alpha 0.90): 0.90 × ~55 = **49**

**The mid-game smoke is 60% louder than the finale.** That inversion is exactly why it reads as "too thick" in the middle and then "different" at the end. Making the whole countdown use the dark colour fixes both complaints at once — it drops the loud middle by ~2.3× and makes the finale the loudest moment, as it should be.

On top of that, `_Density = 0.9` means the core of the band replaces 90% of the background — the village literally vanishes. Dropping to 0.75 leaves a quarter of it showing through, which reads as *smoke* rather than *paint*.

**Invariant that must survive (from v3):** `_MaxReach + _Softness < 0.5`, so the four fronts can never meet and the screen centre stays mathematically clear. v4 keeps a wider margin: `0.23 + 0.15 = 0.38`.

---

## Starting state — READ THIS FIRST

The working tree already contains **uncommitted, uncompiled** edits from the previous session that do the colour half of this plan:

- `Assets/Scenes/region 1/EdgeFox.shader` — `_PanicColor` property + declaration + the `lerp` are already removed, and `_FogColor` default is already `(0.52, 0.55, 0.63, 1)`.
- `Assets/Editor/FogTune.cs` — `SetColor("_PanicColor", ...)` already removed, `_FogColor` already set to `(0.52f, 0.55f, 0.63f, 1f)`.

Verify with `git diff -- "Assets/Scenes/region 1/EdgeFox.shader" Assets/Editor/FogTune.cs` before starting. **If those edits are present, Task 1 is already done — confirm and move to Task 2.** If they are not, apply Task 1.

**`Assets/Scenes/region 1/reference_forest.unity` is also modified in the working tree. That is the user's own work. DO NOT stage it, commit it, revert it, or open it. Stage fog files by explicit path, never `git add -A` / `git add .`.**

## Global Constraints

- Do NOT remove `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` from the shader Properties block — UGUI needs it or Unity errors every frame and trips Error Pause.
- Shader stays `"WordFlow/EdgeFog"` at `Assets/Scenes/region 1/EdgeFox.shader` (the filename typo is intentional; the .mat references it by GUID).
- **`_MaxReach + _Softness` must stay `< 0.5`.** This is the guarantee that the screen centre never gets covered. Any retune that breaks it is wrong.
- **Unity MCP gotchas:**
  - `unity_execute_code` does not work. Use Editor `MenuItem` scripts + `unity_execute_menu_item`.
  - Re-run `unity_list_instances` → `unity_select_instance` **after every compile** — the bridge port hops on domain reload.
  - `unity_get_compilation_errors` covers **C# only**, not shader HLSL. Use `Tools/Quest/Fog Shader Check` for shader errors.
  - Game-view screenshots need `unity_screenshot_game` (framebuffer), not `unity_graphics_game_capture`. They land ~60-90s late — poll for the file. **If a screenshot never lands, run `unity_execute_menu_item "Window/General/Game"` to focus the Game view, then request it again** — an unfocused Game view does not render a frame.
  - `FogTune.ApplyScene()` refuses to run if the currently-open scene has unsaved changes (it has to `OpenScene(Single)` into CutScene_bear). If it errors out, save/close the open scene first.
- **Judge the fog by pixel values, not by eye.** Pale fog on a pale background is invisible to the eye but obvious in the numbers — that trap already cost an hour once in this project.

---

## File Structure

- **Modify** `Assets/Scenes/region 1/EdgeFox.shader` — remove the `_PanicColor` tint (likely already done — see Starting State).
- **Modify** `Assets/Editor/FogTune.cs` — one colour, plus the four re-tuned floats.
- **Modified as a side effect** `Assets/Scenes/region 1/EdgeFogMaterial.mat` — written by `Tools/Quest/Fog Apply Defaults`.

---

### Task 1: Shader — one colour, no progress tint

**Skip this task if `git diff` already shows these edits (see Starting State).**

**Files:**
- Modify: `Assets/Scenes/region 1/EdgeFox.shader`

**Interfaces:**
- Consumes: nothing.
- Produces: shader `"WordFlow/EdgeFog"` with **no** `_PanicColor` property. `_FogColor` is the single smoke colour for the whole countdown. Task 2 writes it by name.

- [ ] **Step 1: Remove the `_PanicColor` property**

In the `Properties` block, replace:

```hlsl
        _FogColor ("Fog Color", Color) = (0.78, 0.80, 0.84, 1)
        _PanicColor ("Panic Color (last 30%)", Color) = (0.52, 0.55, 0.63, 1)
```

with:

```hlsl
        _FogColor ("Fog Color", Color) = (0.52, 0.55, 0.63, 1)
```

- [ ] **Step 2: Remove the `_PanicColor` declaration**

In the CG block, replace:

```hlsl
            float4 _FogColor;
            float4 _PanicColor;
```

with:

```hlsl
            float4 _FogColor;
```

- [ ] **Step 3: Remove the tint from the fragment's return**

Replace:

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

with:

```hlsl
                // One colour for the whole countdown — the heavy storm-gray that used to only appear
                // in the last few seconds. A progress-driven tint was tried and cut: the shift had
                // to be crammed into the final ~3s to read as "panic", which made the last frames
                // look like a different smoke rather than the same smoke, thicker. Pressure comes
                // from reach, density and the beat pulse — not from a hue change.
                return float4(
                    _FogColor.rgb,
                    fog * _FogColor.a
                );
```

- [ ] **Step 4: Verify — C# AND shader**

```
unity_list_instances ; unity_select_instance <port>
unity_get_compilation_errors        # C# only, expect 0
unity_execute_menu_item "Tools/Quest/Fog Shader Check"
read_console filter "FogShaderCheck"
```
Expected: `isSupported=True` and `message count = 0`.

---

### Task 2: Retune the weight

`_Density 0.9` erases 90% of the background at the core. The frame is also wider than it needs to be. Bring both down so the smoke reads as smoke.

**Files:**
- Modify: `Assets/Editor/FogTune.cs`
- Modified by running it: `Assets/Scenes/region 1/EdgeFogMaterial.mat`

**Interfaces:**
- Consumes: shader property names from Task 1.
- Produces: `Tools/Quest/Fog Apply Defaults` writing the v4 values.

- [ ] **Step 1: Replace the whole `ApplyMaterial()` body in `Assets/Editor/FogTune.cs`**

```csharp
    static void ApplyMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogTune] material not found: {MatPath}"); return; }

        // ONE colour for the whole countdown — the heavy storm-gray the smoke used to only reach in
        // the final seconds. The background under the fog frame is a mid-tone village, not bright
        // sky, so this dark gray is the LOW-contrast choice: it reads as weather rather than paint.
        // (The old pale gray was ~2.3x the contrast against that background, which is why the
        // mid-game smoke used to shout louder than the finale.) The smoke thickens over time; it
        // never changes hue.
        mat.SetColor("_FogColor", new Color(0.52f, 0.55f, 0.63f, 1f));

        // Reach + softness are the readability guarantee: influence ends at 0.23 + 0.15 = 0.38,
        // comfortably < 0.5, so the four fronts can never meet and the screen centre stays clear.
        // NEVER let _MaxReach + _Softness reach 0.5.
        mat.SetFloat("_MaxReach", 0.23f);
        mat.SetFloat("_Softness", 0.15f);

        // 0.75 (was 0.90) leaves a quarter of the village showing through even at the core, which is
        // what makes it read as smoke instead of a painted border. 0.40 (was 0.45) shrinks the fully
        // solid part of the band.
        mat.SetFloat("_Density", 0.75f);
        mat.SetFloat("_CoreFrac", 0.40f);

        mat.SetFloat("_NoiseScale", 2.0f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.55f);
        mat.SetFloat("_WispStrength", 0.7f);

        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.16f);

        mat.SetFloat("_Pulse", 0f);
        mat.SetFloat("_PulseReach", 0.03f);

        // _Softness is a constant added to reach, so without this ramp the first non-zero progress
        // would already paint a full-density edge. 0.50 spreads the opacity climb across the whole
        // creep so the smoke materialises out of nothing instead of switching on.
        mat.SetFloat("_FadeIn", 0.50f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] material: smoke clock v4 values applied");
    }
```

- [ ] **Step 2: Compile, then run it**

```
unity_list_instances ; unity_select_instance <port>
unity_get_compilation_errors        # expect 0
unity_execute_menu_item "Tools/Quest/Fog Apply Defaults"
read_console filter "FogTune"
```
Expected two lines: `material: smoke clock v4 values applied` and `scene: three-act fogCurve + urgency=3 written to CutScene_bear`.

If it instead logs `has unsaved changes`, save/close the open scene in Unity and re-run.

- [ ] **Step 3: Confirm the material**

Read `Assets/Scenes/region 1/EdgeFogMaterial.mat`. It must show:
- `_MaxReach: 0.23`, `_Softness: 0.15`, `_Density: 0.75`, `_CoreFrac: 0.4`, `_FadeIn: 0.5`
- `_FogColor: {r: 0.52, g: 0.55, b: 0.63, a: 1}`
- **no `_PanicColor` entry** under `m_Colors` (the shader no longer declares it, so Unity drops it on reserialize). If it is still there, it is inert — but confirm the shader really lost the property.
- `_Progress: 0`

---

### Task 3: Verify the weight is actually down

**Files:** none (verification only).

- [ ] **Step 1: Prove the centre is still clear, and that the weight curve is now monotonic**

```bash
cd "C:/Users/NTP/Documents/NSC-Game" && PYTHONIOENCODING=utf-8 python -c "
import math
def ss(a,b,x):
    t=min(max((x-a)/(b-a),0.0),1.0); return t*t*(3-2*t)

MaxReach,Softness,CoreFrac,Density,FadeIn,PulseReach = 0.23,0.15,0.40,0.75,0.50,0.03
FOG=(133,140,161)                      # the single smoke colour, 0.52/0.55/0.63 * 255
BG =(143,141,122)                      # measured mean background under the fog band
CONTRAST=math.dist(FOG,BG)

keys=[(0.0,0.0),(0.33,0.0),(0.5,0.12),(0.67,0.30),(0.85,0.62),(1.0,1.0)]
def curve(x):
    for i in range(len(keys)-1):
        (t0,v0),(t1,v1)=keys[i],keys[i+1]
        if t0<=x<=t1:
            u=(x-t0)/(t1-t0); return v0+(v1-v0)*u
    return 1.0

def alpha(d, prog, pulse=0.0, n=0.0):
    reach = prog*MaxReach + pulse*PulseReach
    core  = reach*CoreFrac
    guard = min(max(d/max(reach,0.001),0.0),1.0)
    dd    = d + (n-0.5)*0.18*guard
    f     = 1.0 - ss(core, reach+Softness, dd)
    return min(max(f,0.0),1.0) * Density * ss(0.0, FadeIn, prog)

# 1. centre must stay clear
c_noBeep = alpha(0.5, 1.0, 0.0, 0.0)
c_onBeep = alpha(0.5, 1.0, 1.0, 0.0)
print(f'centre alpha, full progress, worst-case noise : {c_noBeep:.4f}')
print(f'centre alpha, full progress, ON a beep        : {c_onBeep:.4f}')
assert c_noBeep == 0.0,  'CENTRE NOT CLEAR'
assert c_onBeep < 0.05,  'CENTRE NOT CLEAR ON A BEEP'
assert MaxReach + Softness < 0.5, 'INVARIANT BROKEN'
print('PASS: screen centre stays clear\n')

# 2. perceived weight must rise monotonically to a peak at 0s (v3 peaked mid-game instead)
print('perceived weight = edge alpha x contrast-vs-background')
prev=-1; ok=True
for sec in [20,17,15,13,10,7,5,3,1,0]:
    prog=curve(1-sec/30)
    a=Density*ss(0.0,FadeIn,prog)      # alpha at the screen edge, where fog term == 1
    w=a*CONTRAST
    print(f'  {sec:>2}s  progress={prog:.2f}  alpha={a:.2f}  weight={w:5.1f}')
    if w+1e-9 < prev: ok=False
    prev=w
assert ok, 'WEIGHT NOT MONOTONIC -- the middle is louder than the end again'
print()
print(f'PASS: loudest moment is 0s (weight {prev:.1f}).')
print(f'For reference, v3 peaked at ~79 with 10s left and only reached ~49 at 0s.')
"
```

Expected: both `PASS` lines. If the monotonic assert fires, the tint or the fade-in got reintroduced.

- [ ] **Step 2: Eyeball the three checkpoints**

For each of `Tools/Quest/Fog Preview 15%`, `30%`, `100%`:
1. `unity_execute_menu_item`
2. `unity_screenshot_game` to `Assets/Screenshots/v4_<n>.png`
3. Poll for the file. If it never lands, `unity_execute_menu_item "Window/General/Game"` first, then request again.

Expected:
- **15%** — a faint dark haze just kissing the four edges. The barn, trees and grass under it are all still clearly readable.
- **30%** — a soft dark band on each edge; the village is dimmed but not gone. Book, stones and timer perfectly crisp on top.
- **100%** — a heavy dark frame. **The village must still be discernible through the core, not erased.** This is the check that `_Density 0.75` actually did its job: if the background is *gone*, density is still too high.

- [ ] **Step 3: Measure it, don't trust the eye**

```bash
cd "C:/Users/NTP/Documents/NSC-Game" && PYTHONIOENCODING=utf-8 python -c "
from PIL import Image
import math
fog=Image.open('Assets/Screenshots/v4_100.png').convert('RGB')
ref=Image.open('Assets/Screenshots/v3_act1b.png').convert('RGB')   # same scene, no fog
w,h=fog.size; pf,pr=fog.load(),ref.load()
for name,(x,y) in {
    'top edge  (deep core)':(int(0.5*w), 6),
    'left edge (deep core)':(6, int(0.5*h)),
    'top, 8% in':(int(0.5*w), int(0.08*h)),
    'top, 20% in':(int(0.5*w), int(0.20*h)),
}.items():
    a,b=pf[x,y],pr[x,y]
    print(f'{name:24s} fogged={a}  clean={b}  shift={math.dist(a,b):5.1f}')
print()
print('The deep core should shift a LOT but the clean colour must still be recognisable')
print('underneath -- a fully erased background means _Density is still too high.')
"
```

- [ ] **Step 4: Turn the preview off**

```
unity_execute_menu_item "Tools/Quest/Fog Preview Off"
```
The committed `.mat` must have `_Progress: 0`. Confirm by grepping the file.

- [ ] **Step 5: Commit — fog files by explicit path only**

```bash
cd "C:/Users/NTP/Documents/NSC-Game"
git add "Assets/Scenes/region 1/EdgeFox.shader" "Assets/Scenes/region 1/EdgeFogMaterial.mat" Assets/Editor/FogTune.cs
git status --short          # reference_forest.unity MUST still show as unstaged " M"
git commit -m "feat(fog): one smoke colour for the whole countdown, lighter weight

The progress tint (lerp to _PanicColor over the last 30% of progress) only
kicked in with ~3s left, so the final frames looked like a different smoke.
It also inverted the pressure curve: the pale mid-game colour is ~2.3x the
contrast against the village background, making 10s-left (weight ~79) louder
than 0s-left (~49).

One dark storm-gray throughout fixes both. Density 0.9 -> 0.75 and reach
0.26 -> 0.23 bring the overall weight down; the village now shows through the
core instead of being erased. Centre stays mathematically clear (0.23 + 0.15
= 0.38 < 0.5)."
```

---

## Tuning table (all on `EdgeFogMaterial`)

| Want | Knob | Note |
|---|---|---|
| Thinner / thicker smoke | `_Density` (0.75) | the main "how much does it hide" dial |
| Narrower / wider frame | `_MaxReach` (0.23) | **`_MaxReach + _Softness` must stay < 0.5** |
| Softer, foggier edge | `_Softness` (0.15) | same ceiling applies |
| Less / more of the band fully solid | `_CoreFrac` (0.40) | |
| Smoke appears earlier / later | `_FadeIn` (0.50) | higher = fades in more gradually |
| Different smoke colour | `_FogColor` (0.52, 0.55, 0.63) | one colour, no tint — keep it that way |
| Bigger lurch on each beep | `_PulseReach` (0.03) | |
| Grace period length | the `0.33` key in `FogTune.ApplyScene` | 0.33 = the first 10 of 30 seconds |

## Rollback

`git revert` the v4 commit. v3's final state is commit `f4db9b34d`.
