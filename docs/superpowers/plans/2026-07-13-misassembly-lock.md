# Mis-assembly Lock — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A garbage word (ปก / กป / าก / าป) triggers a cartoon red damage-screen, a padlock, three beat-beeps that each make the smoke lurch at the book, and a 3-second lockout. The lock then **bursts open** (the star board's scatter-and-fade) and the stones spring home. The clock keeps running through it.

**The lock art already exists in the scene** under `Canvas/key_root`:

| Object | Role |
|---|---|
| `lock` | closed padlock — shown for the 3 seconds of the lockout |
| `unlock` | opened padlock — swapped in at the moment the lock lifts |
| `effect_star1`..`effect_star4` | the burst. **Their authored positions are the scatter destinations** — the same convention `StarHud` already uses. The animation flies them out from the padlock to where they already sit, then fades them. |

All five are **hidden in `Awake`** (forced by code, not trusted from scene state) and only appear on their cue.

**Architecture:** A full-screen **additive** red vignette (a new procedural shader) drawn above everything, driven by one new component (`MisassemblyLock`). Additive blending is the load-bearing choice: it can only *add* red light, so it is mathematically incapable of hiding the smoke underneath. The smoke lurch reuses `EdgeFog`'s existing `_Pulse` (a temporary reach offset that never touches `_Progress`), reached through one **additive** `Mathf.Max` hook on `FogController`. The lockout itself is a single `&& !misassemblyLocked` term on `MagicStonePuzzleController.CanInteract`.

**Tech Stack:** Unity (built-in RP), UGUI ScreenSpaceOverlay Canvas, CG/HLSL shader, coroutines, Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`).

Spec: `docs/superpowers/specs/2026-07-13-misassembly-lock-design.md`

## Global Constraints

- **Never modify** `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`, `Assets/Scenes/region 1/EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat`, or anything in `Assets/Scripts/Region1/Stars/`.
- **`FogController.cs` gets exactly one additive change** (Task 2): a new static `ExtraPulseProvider`, `Mathf.Max`'d with the existing `ComputeBeatPulse()`. **Nothing is removed.** The last-10-seconds countdown beep and its smoke lurch must behave exactly as they do today — that is an explicit user requirement, and Task 6 checks it.
- **The clock is never paused** in the mis-assembly path. Being locked out while the smoke closes in *is* the punishment. Do not add a `WordAssemblyTimer.Instance?.Pause()` anywhere in this work.
- The alert must be **additive** (`Blend SrcAlpha One`). Do not "simplify" it to a normal alpha overlay — that would hide the smoke, which is the one thing the user explicitly asked us to avoid.
- Every new shader **must** declare `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}`. Without it, UGUI logs a missing-`_MainTex` error every frame and trips Error Pause. This has bitten every shader in this scene.
- `lock_overlay`'s root GameObject **stays active** in the scene. Its `Awake` must run to register the fog hook; the component hides *itself* (intensity 0, `key_root` art off, `raycastTarget` off). Do not ship it with `SetActive(false)`. Same for `key_root` — the root stays active, its *children* get hidden by code.
- **Do not create a new lock icon.** The art is already authored in `Canvas/key_root` (`lock`, `unlock`, `effect_star1..4`). Wire those; do not add sprites of your own.
- Every scene edit happens atomically inside **one** editor script ending in `EditorSceneManager.SaveScene`.
- `mcp__anklebreaker__unity_execute_code` does not work here. Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- **Run `Assets/Refresh`, then poll `unity_get_compilation_errors` until `isCompiling: false`, before invoking a menu item you just wrote.** Otherwise Unity runs the *old* compiled code and the menu silently does the wrong thing.
- Before any MCP call: `unity_list_instances` then `unity_select_instance`. The port hops between 7890 and 7891 on domain reload. Pass `port:` on every call.
- **Open the Game view (`Window/General/Game`) before every screenshot**, or `unity_screenshot_game` silently returns a flat ~100KB image with no UI. Screenshots land ~60-75s later — poll for the file and check it is multi-MB.
- The `[Telemetry] 404` / `[Session] open failed` ngrok warnings and the `EditorStyles.get_toolbarButtonRight` NullReferenceException (an Editor UI redraw quirk) are all pre-existing. Ignore them.
- Commit after each task.

---

### Task 1: The additive red-alert shader

**Files:**
- Create: `Assets/Scenes/region 1/LockVignette.shader`

**Interfaces:**
- Produces: `Shader "WordFlow/LockVignette"`. Driven properties: `_Intensity` (0..1, the fade), `_Pulse` (0..1, the throb). Tunables: `_Color`, `_EdgeStart`, `_EdgeEnd`, `_CoreGlow`, `_PulseGain`.

- [ ] **Step 1: Write the shader**

Create `Assets/Scenes/region 1/LockVignette.shader`:

```hlsl
Shader "WordFlow/LockVignette"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it silences the
        // per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause).
        // The fragment ignores it — the vignette is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Alert Color", Color) = (0.95, 0.15, 0.12, 1)

        _Intensity ("Intensity (driven)", Range(0, 1)) = 0
        _Pulse ("Pulse (driven)", Range(0, 1)) = 0

        _EdgeStart ("Vignette Inner Edge", Range(0, 1.5)) = 0.35
        _EdgeEnd ("Vignette Outer Edge", Range(0, 1.5)) = 1.15
        _CoreGlow ("Centre Glow", Range(0, 1)) = 0.08
        _PulseGain ("Pulse Gain", Range(0, 2)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        // ADDITIVE, not alpha-over. This is the whole point of the effect: additive can only ADD red
        // light to what is already on screen, so it is mathematically incapable of hiding the smoke
        // underneath. A normal alpha overlay at any useful strength would bury it — the same
        // arithmetic that buried the purple smoke under the grey coat. Do not change this line.
        Blend SrcAlpha One
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

            float4 _Color;
            float _Intensity;
            float _Pulse;
            float _EdgeStart;
            float _EdgeEnd;
            float _CoreGlow;
            float _PulseGain;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0 at the centre, 1 at the edge midpoints, ~1.41 in the corners — so the alert is
                // hottest where a hit would sting and stays clear of the book in the middle.
                float d = length((i.uv - 0.5) * 2.0);

                float vignette = smoothstep(_EdgeStart, _EdgeEnd, d);

                // A little red even dead centre, so the whole screen reads "you took a hit" rather
                // than "there is a red frame around the screen".
                float a = (vignette + _CoreGlow) * _Intensity * (1.0 + _Pulse * _PulseGain);

                return float4(_Color.rgb, saturate(a) * _Color.a);
            }

            ENDCG
        }
    }
}
```

- [ ] **Step 2: Confirm it compiles**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance          port: <from the list>
mcp__anklebreaker__unity_console_clear            port: <port>
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
mcp__anklebreaker__unity_console_log              type: "error"     port: <port>
```

Expected: no `Shader error in 'WordFlow/LockVignette'`. A shader that fails to compile renders magenta.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scenes/region 1/LockVignette.shader" "Assets/Scenes/region 1/LockVignette.shader.meta"
git commit -m "feat(lock): add the WordFlow/LockVignette additive red-alert shader"
```

---

### Task 2: Let something outside the clock lurch the smoke

**Files:**
- Modify: `Assets/Scripts/Common/FogController.cs` (add a static field; change 1 statement in `Update`)

**Interfaces:**
- Produces: `FogController.ExtraPulseProvider` — a `static Func<float>` returning 0..1. Whatever it returns is `Mathf.Max`'d with the countdown beat pulse, so it can lurch the smoke at any point in the clock **without changing how the last-10-seconds beep behaves**.
- Both `FogOverlay` and `CloudOverlay` run their own `FogController`, so one provider lurches **both** smoke layers together.

**Why this is safe:** `_Pulse` feeds `reach = _Progress * _MaxReach + _Pulse * _PulseReach` in `EdgeFog`. It is a **temporary** reach offset that snaps back — it never touches `_Progress`, so the smoke jumps toward the book and settles **without the clock advancing any faster**. That is exactly what the user asked for.

- [ ] **Step 1: Add the provider**

In `Assets/Scripts/Common/FogController.cs`, find the existing provider block:

```csharp
    /// Seconds left on the puzzle clock. Used to land a visual pulse on each countdown beep.
    public static System.Func<float> SmokeRemainingProvider;
```

and add below it:

```csharp
    /// An extra smoke lurch driven from outside the countdown — the mis-assembly lock beeps use it.
    /// Max'd with the countdown beat pulse in Update, never replacing it: the last-10-seconds beep
    /// lurch keeps behaving exactly as it always has. Returns 0..1; null means "no external lurch".
    public static System.Func<float> ExtraPulseProvider;
```

- [ ] **Step 2: Fold it into the pulse**

In the same file, in `Update()`, find:

```csharp
        fogMaterial.SetFloat(
            PulseID,
            ComputeBeatPulse()
        );
```

and replace it with:

```csharp
        // The countdown beat pulse, raised by any external lurch (the mis-assembly lock). Max, never
        // replacement — ComputeBeatPulse() and the 10-second beep it serves are untouched.
        float pulse = ComputeBeatPulse();

        if (ExtraPulseProvider != null)
        {
            pulse = Mathf.Max(pulse, ExtraPulseProvider());
        }

        fogMaterial.SetFloat(
            PulseID,
            pulse
        );
```

Do not touch `ComputeBeatPulse()` itself, `pulseWindow`, `pulseDecay`, or anything else in the file.

- [ ] **Step 3: Compile**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Common/FogController.cs
git commit -m "feat(fog): allow an external smoke lurch, max'd with the countdown beat

Additive only: ComputeBeatPulse() and the last-10-seconds beep pulse it drives
are unchanged. _Pulse is a temporary reach offset that never touches _Progress,
so an external lurch scares without advancing the clock."
```

---

### Task 3: The `MisassemblyLock` component

**Files:**
- Create: `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`

**Interfaces:**
- Consumes: `FogController.ExtraPulseProvider` (Task 2); `WordFlow/LockVignette` (Task 1); the authored `key_root` art (`lock`, `unlock`, `effect_star1..4`).
- Produces:
  - `static MisassemblyLock Instance { get; }`
  - `IEnumerator PlayRoutine()` — the whole alert: fade in, padlock, three beeps + three smoke lurches, the unlock burst, fade out. Yield on it; it returns when the lock should lift.
  - Serialized: `vignette` (RawImage), `lockIcon` / `unlockIcon` (Image — the `lock` and `unlock` objects in `key_root`), `burst` (Image[4] — `effect_star1..4`), `audioSource`, `beepClip`, `unlockSfx`, and the timing knobs.

- [ ] **Step 1: Write the component**

Create `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// The garbage-word punishment: a cartoon red damage-screen, a padlock, and three beat-beeps that
/// each yank the smoke toward the book — for three seconds, during which the stones are dead. Then
/// the padlock springs open and bursts apart, and the board is playable again.
///
/// The clock is NOT paused while this plays. That is the whole point: being frozen out costs real
/// seconds of smoke, so brute-forcing combinations has a price.
///
/// The alert is ADDITIVE (WordFlow/LockVignette, Blend SrcAlpha One). It can only add red light, so
/// it cannot hide the smoke underneath — the smoke just reads as red-hot while the alert is up.
///
/// The padlock art lives in Canvas/key_root and is authored by hand: `lock`, `unlock`, and
/// `effect_star1..4`, whose authored positions ARE the burst's scatter destinations — the same
/// convention StarHud uses. Everything there is hidden in Awake and only shows on its cue.
public sealed class MisassemblyLock : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Full-screen RawImage running the WordFlow/LockVignette material.")]
    [SerializeField] private RawImage vignette;

    [Tooltip("key_root/lock — the closed padlock, up for the whole lockout.")]
    [SerializeField] private Image lockIcon;

    [Tooltip("key_root/unlock — the opened padlock, swapped in when the lock lifts.")]
    [SerializeField] private Image unlockIcon;

    [Tooltip("key_root/effect_star1..4 — the unlock burst. Their authored positions are the scatter " +
             "destinations; the burst flies them out from the padlock to where they already sit.")]
    [SerializeField] private Image[] burst;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("countdown_beep.wav — the same beep the smoke clock plays in its last 10 seconds. The " +
             "clip is one long track of once-a-second beeps, so we play it from 0 and cut it after " +
             "beepAudibleSeconds to get exactly one beep.")]
    [SerializeField] private AudioClip beepClip;

    [Tooltip("unlock.wav — plays as the padlock springs open.")]
    [SerializeField] private AudioClip unlockSfx;

    [Header("Beats")]
    [SerializeField, Min(1)] private int beepCount = 3;
    [SerializeField, Min(0.1f)] private float beepInterval = 1f;
    [SerializeField, Min(0.05f)] private float beepAudibleSeconds = 0.4f;

    [Tooltip("How fast each smoke lurch settles back. 6 matches FogController's countdown beat, so " +
             "the lurch feels identical to a countdown beep's.")]
    [SerializeField, Range(1f, 12f)] private float pulseDecay = 6f;

    [Header("Fades")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;

    [Header("Padlock pop")]
    [SerializeField, Min(0.01f)] private float iconPopDuration = 0.28f;
    [SerializeField] private float iconOvershoot = 1.8f;

    [Header("Unlock burst")]
    [SerializeField, Min(0.01f)] private float unlockPopDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float burstOutDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float burstFadeDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float burstStartScale = 0.3f;
    [SerializeField, Min(0.01f)] private float burstEndScale = 1.2f;
    [SerializeField, Min(0f)] private float unlockHold = 0.25f;

    public static MisassemblyLock Instance { get; private set; }

    public bool IsLocked { get; private set; }

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int PulseID = Shader.PropertyToID("_Pulse");

    private Material vignetteMaterial;
    private Vector3 lockBaseScale = Vector3.one;
    private Vector3 unlockBaseScale = Vector3.one;

    // The scene layout IS the burst's target data — cache it before anything moves.
    private Vector2[] burstPos;
    private Vector3[] burstScale;

    private float pulse01;

    private void Awake()
    {
        Instance = this;

        if (vignette != null && vignette.material != null)
        {
            // Instance the material so an editor preview value never leaks into a play session.
            vignetteMaterial = new Material(vignette.material);
            vignette.material = vignetteMaterial;
        }

        if (lockIcon != null) lockBaseScale = lockIcon.rectTransform.localScale;
        if (unlockIcon != null) unlockBaseScale = unlockIcon.rectTransform.localScale;

        int n = burst != null ? burst.Length : 0;
        burstPos = new Vector2[n];
        burstScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            RectTransform r = Rect(burst[i]);
            if (r == null) continue;
            burstPos[i] = r.anchoredPosition;
            burstScale[i] = r.localScale;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // The smoke reads this every frame. Max'd with the countdown beat inside FogController, so
        // the last-10-seconds beep pulse is untouched.
        FogController.ExtraPulseProvider = () => pulse01;

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (FogController.ExtraPulseProvider != null) FogController.ExtraPulseProvider = null;
        if (vignetteMaterial != null) Destroy(vignetteMaterial);
    }

    /// The full alert. Yield on this — it returns when the lock should lift.
    public IEnumerator PlayRoutine()
    {
        IsLocked = true;

        // raycastTarget on the full-screen vignette eats every tap on its own, belt-and-braces with
        // the CanInteract gate in MagicStonePuzzleController.
        if (vignette != null) vignette.raycastTarget = true;

        // --- flash in -----------------------------------------------------------------------------
        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetIntensity(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetIntensity(1f);

        // --- the closed padlock pops in -------------------------------------------------------------
        yield return PopIconRoutine(lockIcon, lockBaseScale, iconPopDuration);

        // --- three beats: beep + smoke lurch ---------------------------------------------------------
        for (int i = 0; i < beepCount; i++)
        {
            PlayClip(beepClip, cutAfter: beepAudibleSeconds);

            // pulse01 drives BOTH the smoke's inward lurch (via FogController.ExtraPulseProvider) and
            // the vignette's throb, so the sound, the red and the smoke all land on the same beat.
            for (float t = 0f; t < beepInterval; t += Time.deltaTime)
            {
                pulse01 = Mathf.Exp(-t * pulseDecay);
                SetPulse(pulse01);
                yield return null;
            }
        }

        pulse01 = 0f;
        SetPulse(0f);

        // --- the unlock beat: swap to the open padlock and burst it apart -----------------------------
        SetActive(lockIcon, false);

        PlayClip(unlockSfx, cutAfter: 0f);

        StartCoroutine(BurstRoutine());
        yield return PopIconRoutine(unlockIcon, unlockBaseScale, unlockPopDuration);

        if (unlockHold > 0f) yield return new WaitForSeconds(unlockHold);

        // --- fade out ---------------------------------------------------------------------------------
        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Clamp01(t / fadeOutDuration);
            SetIntensity(k);
            SetAlpha(unlockIcon, k);
            yield return null;
        }

        Hide();
        IsLocked = false;
    }

    private IEnumerator PopIconRoutine(Image icon, Vector3 baseScale, float duration)
    {
        if (icon == null) yield break;

        SetActive(icon, true);
        SetAlpha(icon, 0f);
        icon.rectTransform.localScale = Vector3.zero;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            icon.rectTransform.localScale =
                Vector3.LerpUnclamped(Vector3.zero, baseScale, EaseOutBack(k, iconOvershoot));
            SetAlpha(icon, Mathf.Clamp01(k * 2f));
            yield return null;
        }

        icon.rectTransform.localScale = baseScale;
        SetAlpha(icon, 1f);
    }

    /// effect_star1..4 fly out from the padlock to their authored positions and fade — the same
    /// scatter the star award uses.
    private IEnumerator BurstRoutine()
    {
        if (burst == null || burst.Length == 0) yield break;

        Vector2 origin = unlockIcon != null
            ? unlockIcon.rectTransform.anchoredPosition
            : (lockIcon != null ? lockIcon.rectTransform.anchoredPosition : Vector2.zero);

        for (int i = 0; i < burst.Length; i++)
        {
            RectTransform r = Rect(burst[i]);
            if (r == null) continue;
            r.anchoredPosition = origin;
            r.localScale = burstScale[i] * burstStartScale;
            SetActive(burst[i], true);
            SetAlpha(burst[i], 1f);
        }

        for (float t = 0f; t < burstOutDuration; t += Time.deltaTime)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / burstOutDuration));
            for (int i = 0; i < burst.Length; i++)
            {
                RectTransform r = Rect(burst[i]);
                if (r == null) continue;
                r.anchoredPosition = Vector2.LerpUnclamped(origin, burstPos[i], k);
                r.localScale = Vector3.LerpUnclamped(burstScale[i] * burstStartScale,
                                                     burstScale[i] * burstEndScale, k);
            }
            yield return null;
        }

        for (float t = 0f; t < burstFadeDuration; t += Time.deltaTime)
        {
            float a = 1f - Mathf.Clamp01(t / burstFadeDuration);
            for (int i = 0; i < burst.Length; i++) SetAlpha(burst[i], a);
            yield return null;
        }

        for (int i = 0; i < burst.Length; i++)
        {
            SetAlpha(burst[i], 1f);
            SetActive(burst[i], false);
            RectTransform r = Rect(burst[i]);
            if (r != null) r.localScale = burstScale[i];
        }
    }

    /// countdown_beep.wav is a long track of once-a-second beeps, so `cutAfter` stops it after one
    /// beep's worth. Pass 0 to let a clip (unlock.wav) play out in full.
    private void PlayClip(AudioClip clip, float cutAfter)
    {
        if (audioSource == null || clip == null) return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.time = 0f;
        audioSource.Play();

        if (cutAfter > 0f) StartCoroutine(CutClipRoutine(clip, cutAfter));
    }

    private IEnumerator CutClipRoutine(AudioClip clip, float after)
    {
        yield return new WaitForSeconds(after);
        if (audioSource != null && audioSource.clip == clip && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// Everything from key_root is hidden until its cue — forced here rather than trusted from the
    /// scene, which the designer leaves visible while authoring.
    private void Hide()
    {
        pulse01 = 0f;
        SetIntensity(0f);
        SetPulse(0f);

        if (vignette != null) vignette.raycastTarget = false;

        SetActive(lockIcon, false);
        SetActive(unlockIcon, false);

        if (burst == null) return;
        for (int i = 0; i < burst.Length; i++) SetActive(burst[i], false);
    }

    private void SetIntensity(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(IntensityID, Mathf.Clamp01(v));
    }

    private void SetPulse(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(PulseID, Mathf.Clamp01(v));
    }

    private static RectTransform Rect(Component c) => c != null ? c.transform as RectTransform : null;

    private static void SetActive(Component c, bool on)
    {
        if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = Mathf.Clamp01(a);
        g.color = c;
    }

    private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

    private static float EaseOutBack(float t, float overshoot)
    {
        float s = t - 1f;
        return 1f + s * s * ((overshoot + 1f) * s + overshoot);
    }
}
```

- [ ] **Step 2: Compile**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs.meta
git commit -m "feat(lock): add MisassemblyLock — red alert, padlock, three beat-beeps, unlock burst"
```

---

### Task 4: Build `lock_overlay` into the scene and wire the `key_root` art

**Files:**
- Create: `Assets/Editor/BuildMisassemblyLock.cs`
- Creates as a side effect: `Assets/Scenes/region 1/LockVignetteMaterial.mat`
- Modifies (via the tool): `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Consumes: `WordFlow/LockVignette` (Task 1), `MisassemblyLock` (Task 3), and the authored art under `Canvas/key_root` (`lock`, `unlock`, `effect_star1`..`effect_star4`).
- Produces: menu items `Tools/Quest/Build Misassembly Lock` and `Tools/Quest/Lock Preview On` / `Tools/Quest/Lock Preview Off`; a wired `lock_overlay` (RawImage + AudioSource + `MisassemblyLock`) sitting just under `key_root` at the top of the Canvas.

**Draw order matters here.** Final Canvas order: … `star_hud`, **`lock_overlay`** (the additive red), **`key_root`** (the padlock + burst) — so the padlock draws *over* the red and reads crisply instead of being washed by it.

`Lock Preview On/Off` is what makes the main check automatable — a garbage word needs a human at the controls, but the preview lets us screenshot the red over the smoke without playing.

- [ ] **Step 1: Write the editor tool**

Create `Assets/Editor/BuildMisassemblyLock.cs`:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Build Misassembly Lock  — builds the garbage-word red alert into CutScene_bear.
/// Tools/Quest/Lock Preview On | Off   — eyeball the alert without playing to a garbage word.
///
/// The lock_overlay (additive red vignette) goes near the top of the Canvas so the alert covers the
/// smoke, the book and the star board — safe, because additive can only add red light, never hide
/// what is beneath it. key_root (the padlock art, authored by hand) is then pushed ABOVE it, so the
/// padlock draws over the red rather than being washed by it.
///
/// Idempotent: re-running re-wires the existing overlay instead of building a second one.
public static class BuildMisassemblyLock
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string ShaderName = "WordFlow/LockVignette";
    const string MatPath = "Assets/Scenes/region 1/LockVignetteMaterial.mat";
    const string BeepPath = "Assets/Audio/countdown_beep.wav";
    const string UnlockSfxPath = "Assets/Audio/unlock.wav";
    const string OverlayName = "lock_overlay";
    const string KeyRootName = "key_root";

    [MenuItem("Tools/Quest/Build Misassembly Lock")]
    public static void Run()
    {
        Material mat = EnsureMaterial();
        if (mat == null) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[BuildMisassemblyLock] no Canvas in CutScene_bear"); return; }

        Transform keyRoot = canvas.transform.Find(KeyRootName);
        if (keyRoot == null)
        {
            Debug.LogError($"[BuildMisassemblyLock] {KeyRootName} not found under the Canvas — the " +
                           "padlock art (lock / unlock / effect_star1..4) is authored there and is required");
            return;
        }

        // --- lock_overlay: the full-screen additive vignette ---------------------------------------
        Transform existing = canvas.transform.Find(OverlayName);
        GameObject overlay = existing != null
            ? existing.gameObject
            : new GameObject(OverlayName, typeof(RectTransform));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(overlay, "Build Misassembly Lock");
            overlay.transform.SetParent(canvas.transform, false);
        }

        Stretch((RectTransform)overlay.transform);
        overlay.layer = keyRoot.gameObject.layer;

        var raw = overlay.GetComponent<RawImage>();
        if (raw == null) raw = overlay.AddComponent<RawImage>();
        raw.material = mat;
        raw.color = Color.white;
        raw.raycastTarget = false;   // MisassemblyLock switches this on only while the lock is up

        var src = overlay.GetComponent<AudioSource>();
        if (src == null) src = overlay.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;

        // --- wire MisassemblyLock to the authored key_root art ---------------------------------------
        var lockComp = overlay.GetComponent<MisassemblyLock>();
        if (lockComp == null) lockComp = Undo.AddComponent<MisassemblyLock>(overlay);

        var art = BuildNameMap(keyRoot);

        var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepPath);
        if (beep == null) Debug.LogWarning($"[BuildMisassemblyLock] beep clip not found: {BeepPath}");

        var unlockSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(UnlockSfxPath);
        if (unlockSfx == null) Debug.LogWarning($"[BuildMisassemblyLock] unlock sfx not found: {UnlockSfxPath}");

        var so = new SerializedObject(lockComp);
        so.FindProperty("vignette").objectReferenceValue = raw;
        so.FindProperty("lockIcon").objectReferenceValue = Get(art, "lock");
        so.FindProperty("unlockIcon").objectReferenceValue = Get(art, "unlock");
        so.FindProperty("audioSource").objectReferenceValue = src;
        if (beep != null) so.FindProperty("beepClip").objectReferenceValue = beep;
        if (unlockSfx != null) so.FindProperty("unlockSfx").objectReferenceValue = unlockSfx;

        // effect_star1..4 — the unlock burst. Their authored positions are the scatter destinations.
        var burst = so.FindProperty("burst");
        burst.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            burst.GetArrayElementAtIndex(i).objectReferenceValue = Get(art, $"effect_star{i + 1}");
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // --- draw order: red under the padlock, both above everything else --------------------------
        overlay.transform.SetSiblingIndex(canvas.transform.childCount - 1);
        keyRoot.SetSiblingIndex(canvas.transform.childCount - 1);   // key_root ends up on top

        // Both roots MUST stay active: lock_overlay's Awake registers FogController.ExtraPulseProvider
        // and hides the art itself. Shipping either inactive would silently kill the effect.
        overlay.SetActive(true);
        keyRoot.gameObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildMisassemblyLock] DONE — {OverlayName} at sibling index " +
                  $"{overlay.transform.GetSiblingIndex()}, {KeyRootName} at {keyRoot.GetSiblingIndex()} (on top), " +
                  $"beep={(beep != null ? beep.name : "MISSING")}, unlock={(unlockSfx != null ? unlockSfx.name : "MISSING")}, " +
                  "scene saved");
    }

    [MenuItem("Tools/Quest/Lock Preview On")]
    public static void PreviewOn() => SetPreview(1f);

    [MenuItem("Tools/Quest/Lock Preview Off")]
    public static void PreviewOff() => SetPreview(0f);

    static void SetPreview(float intensity)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[LockPreview] material not found: {MatPath}"); return; }

        mat.SetFloat("_Intensity", intensity);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LockPreview] _Intensity = {intensity}");
    }

    /// Key every child of key_root by its name with all whitespace stripped, so a stray space the
    /// designer left in ("effect_star1 ") never breaks the lookup. Same guard the star board needed.
    static Dictionary<string, Image> BuildNameMap(Transform root)
    {
        var map = new Dictionary<string, Image>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            var img = c.GetComponent<Image>();
            if (img == null) continue;
            map[Regex.Replace(c.name, @"\s+", "")] = img;
        }
        return map;
    }

    static Image Get(Dictionary<string, Image> map, string normalisedName)
    {
        if (map.TryGetValue(normalisedName, out Image img) && img != null) return img;
        Debug.LogError($"[BuildMisassemblyLock] key_root is missing an Image named '{normalisedName}'");
        return null;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.localScale = Vector3.one;
    }

    static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[BuildMisassemblyLock] shader not found: {ShaderName} — is LockVignette.shader importing?");
            return null;
        }

        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildMisassemblyLock] created {MatPath}");
        return mat;
    }
}
```

- [ ] **Step 2: Compile, then run it**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Wait for `isCompiling: false` and zero errors, then:

```
mcp__anklebreaker__unity_console_clear       port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Build Misassembly Lock"   port: <port>
mcp__anklebreaker__unity_console_log         port: <port>
```

Expected: `[BuildMisassemblyLock] DONE — lock_overlay at sibling index N, key_root at N (on top), beep=countdown_beep, unlock=unlock, scene saved`, and **no `key_root is missing an Image named …` errors**. If one appears, the child is named differently than expected — list `key_root`'s children and fix the lookup key before continuing.

- [ ] **Step 3: Verify the wiring and the draw order**

```
mcp__anklebreaker__unity_scene_hierarchy            parentPath: "Canvas"   maxDepth: 1   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "lock_overlay"   componentType: "MisassemblyLock"   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "lock_overlay"   componentType: "RawImage"          port: <port>
```

Expected:
- Canvas order ends `… star_hud`, **`lock_overlay`**, **`key_root`** — the padlock art is the very last child, so it draws over the red.
- Both `lock_overlay` and `key_root` are **active**.
- `MisassemblyLock`: `vignette`, `lockIcon`, `unlockIcon`, `audioSource` all wired; `burst` has 4 entries; `beepClip` = `countdown_beep`; `unlockSfx` = `unlock`. **No nulls.**
- `RawImage`: material = `LockVignetteMaterial`, `raycastTarget` = **false** (it flips on only during a lock).

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/BuildMisassemblyLock.cs Assets/Editor/BuildMisassemblyLock.cs.meta \
        "Assets/Scenes/region 1/LockVignetteMaterial.mat" "Assets/Scenes/region 1/LockVignetteMaterial.mat.meta" \
        "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(lock): build the lock_overlay alert and wire the key_root padlock art"
```

---

### Task 5: Fire the lock on a garbage word

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`

**Interfaces:**
- Consumes: `MisassemblyLock.Instance.PlayRoutine()` (Task 3).
- Produces: nothing new. This is what makes ปก / กป / าก / าป actually cost something.

Null-guard the call: `CutScene_ga` and `CutScene_ta` share this controller and have no lock overlay.

- [ ] **Step 1: Add the lock state**

In `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`, next to `private bool builtInTime;` (~line 187), add:

```csharp
    // Garbage word (ปก / กป / าก / าป): the board is frozen while the red alert plays. The clock is
    // NOT paused — the seconds it costs are the punishment.
    private bool misassemblyLocked;
    private Coroutine misassemblyRoutine;
```

- [ ] **Step 2: Gate every stone tap on it**

Find `CanInteract` (~line 199):

```csharp
    public bool CanInteract => revealFinished && gameplayAudioReady
        && !ritualPlaying && !ritualCompleted && !crowFeedbackPlaying;
```

and add the lock term:

```csharp
    public bool CanInteract => revealFinished && gameplayAudioReady
        && !ritualPlaying && !ritualCompleted && !crowFeedbackPlaying
        && !misassemblyLocked;
```

This single term is the entire lockout — every stone tap already funnels through `CanInteract`.

- [ ] **Step 3: Trigger the alert from the garbage-word branch**

In `TryStartCompletionRitual()` (~line 808), find the `else` branch:

```csharp
        else
        {
            // Not a valid word: leave the stones in place so the player can tap them back out and
            // retry. No backend send here — like word_build_paa_polished, the build-attempt (and
            // /grade) fire together later at mic-stop. Just restart the build timer.
            _latency.Start(Time.realtimeSinceStartupAsDouble);
            return;
        }
```

and replace it with:

```csharp
        else
        {
            // Garbage word (ปก / กป / าก / าป): red alert + a 3-second lockout, then the stones
            // spring home. No backend send here — like word_build_paa_polished, the build-attempt
            // (and /grade) fire together later at mic-stop.
            if (misassemblyRoutine == null)
            {
                misassemblyRoutine = StartCoroutine(MisassemblyRoutine());
            }

            return;
        }
```

- [ ] **Step 4: Write the routine**

Add this method immediately after `TryStartCompletionRitual()` (i.e. before `SetActiveResultVariant`):

```csharp
    /// Two stones, no word. Freeze the board, flash the red alert with its three beat-beeps (the smoke
    /// lurches inward on each, exactly as it does on a countdown beep), then spit both stones back
    /// home so the next attempt starts clean.
    ///
    /// The clock is deliberately NOT paused: the ~3 seconds this costs are seconds of smoke closing
    /// in, which is what stops the child brute-forcing combinations for free.
    private IEnumerator MisassemblyRoutine()
    {
        misassemblyLocked = true;

        if (MisassemblyLock.Instance != null)
        {
            yield return MisassemblyLock.Instance.PlayRoutine();
        }

        // Spring the mis-placed stones home — the same three calls a manual tap-out makes.
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            MagicStonePuzzleStone occupant = slotOccupants[i];
            if (occupant == null) continue;

            slotOccupants[i] = null;
            occupant.SetCurrentSlot(-1);
            occupant.ReturnHome();
        }

        misassemblyLocked = false;
        misassemblyRoutine = null;

        // Restart the build timer for the next attempt (this is what the old else-branch did).
        _latency.Start(Time.realtimeSinceStartupAsDouble);
    }
```

- [ ] **Step 5: Compile**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs
git commit -m "feat(lock): a garbage word now costs a red-alert lockout and springs the stones home"
```

---

### Task 6: Verification

- [ ] **Step 1: The gate — does the smoke survive the red?**

This is the check the whole design hangs on, and it is automatable via the preview menus.

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"          port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 60%"   port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Lock Preview On"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/Lock_over_smoke.png"   port: <port>
```

Poll for the file (~60-75s, must be multi-MB), then read it.

**Expected — the pass/fail criterion:** the screen is red and dangerous, **and the smoke is still
plainly visible through it** — every billow still readable, just tinted red-hot. The book and stones
are also tinted but perfectly legible.

**If the smoke is gone**, the blend mode is wrong — check `Blend SrcAlpha One` in `LockVignette.shader`.
Do not "fix" it by lowering the alpha; fix the blend.

Then clean up:

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Lock Preview Off"   port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview Off"    port: <port>
```

- [ ] **Step 2: Tune the alert, if needed**

Every knob is on `LockVignetteMaterial` (Inspector) — no code edit:

| Want | Knob |
|---|---|
| Red too weak / too strong | `_Color` alpha, or `_CoreGlow` (0.08) |
| Red frame too thick | raise `_EdgeStart` (0.35 → 0.55) |
| Red creeping too far into the middle | raise `_EdgeStart` and `_EdgeEnd` together |
| Throb on each beep too subtle | `_PulseGain` (0.75 → 1.2) |

The unlock burst's knobs are on the `MisassemblyLock` component (`lock_overlay` in the Inspector):
`burstOutDuration`, `burstEndScale`, `unlockHold`. The burst's *directions* are the authored positions
of `effect_star1..4` in `key_root` — to change where the pieces fly, drag them in the scene.

- [ ] **Step 3: Ask the user to play the garbage-word path**

A garbage word needs stones tapped and cannot be driven from here. Ask the user to play `CutScene_bear`
and assemble **ปก** (or any of กป / าก / าป), then report:

1. **before the garbage word, nothing from `key_root` is on screen** — no padlock, no burst pieces;
2. the screen flashes red the moment the second stone lands — **and the smoke is still visible through it**;
3. the **closed padlock** pops in, drawn crisply *over* the red (not washed out by it);
4. **three** beeps land, ~1s apart;
5. the smoke **visibly lurches in toward the book on each beep and settles back**;
6. the stones **cannot be tapped** for those ~3 seconds;
7. then the padlock **swaps to the opened one and bursts apart** (scatter + fade, like a star award),
   `unlock.wav` plays, the stones **spring home by themselves**, and the board is playable again;
8. afterwards `key_root` is fully hidden again — nothing lingers on screen;
9. the clock kept ticking the whole time (the smoke is further in than when the lock started) — but
   **no faster than normal**: the lurch snapped back each time, it did not accumulate.

- [ ] **Step 4: Ask the user to confirm the untouched paths**

These are the regressions this work could plausibly cause:

1. **The last-10-seconds countdown beep still works** — same beep, same smoke lurch, unchanged. (This
   is a hard user requirement: the lock's pulse is `Mathf.Max`'d with it, never replacing it.)
2. Assembling **ปา** still awards the stars and moves to `Success_pa`.
3. Assembling **กา** still awards one star and moves to `Success_ga`.

- [ ] **Step 5: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/LockVignetteMaterial.mat"
git commit -m "feat(lock): tune the red alert"
```

(Skip if nothing was tuned.)

---

## Done when

- A garbage word (ปก / กป / าก / าป) flashes the screen red, pops the **closed padlock**, plays three beeps, lurches the smoke inward on each, and freezes the stones for ~3 seconds.
- The lock then **springs open and bursts apart** (`unlock` + `effect_star1..4` scatter and fade, like a star award), and the stones spring home.
- **The smoke stays visible through the red** (additive blend), and the padlock stays crisp over it (`key_root` is the top sibling).
- Nothing from `key_root` is visible before its cue or after the alert ends.
- The clock keeps running through the lock, and the lurch does **not** make it run any faster.
- The last-10-seconds countdown beep and its lurch are **unchanged**.
- ปา and กา behave exactly as they did.
- `git diff` never touches `WordAssemblyTimer.cs`, `EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat` or `Assets/Scripts/Region1/Stars/`, and `FogController.cs` shows only the additive `ExtraPulseProvider` change.
