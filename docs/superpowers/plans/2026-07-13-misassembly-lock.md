# Mis-assembly Lock — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A garbage word (ปก / กป / าก / าป) triggers a cartoon red damage-screen, a lock symbol, three beat-beeps that each make the smoke lurch at the book, and a 3-second lockout — after which the stones spring home. The clock keeps running through it.

**Architecture:** A full-screen **additive** red vignette (a new procedural shader) drawn above everything, driven by one new component (`MisassemblyLock`). Additive blending is the load-bearing choice: it can only *add* red light, so it is mathematically incapable of hiding the smoke underneath. The smoke lurch reuses `EdgeFog`'s existing `_Pulse` (a temporary reach offset that never touches `_Progress`), reached through one **additive** `Mathf.Max` hook on `FogController`. The lockout itself is a single `&& !misassemblyLocked` term on `MagicStonePuzzleController.CanInteract`.

**Tech Stack:** Unity (built-in RP), UGUI ScreenSpaceOverlay Canvas, CG/HLSL shader, coroutines, Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`).

Spec: `docs/superpowers/specs/2026-07-13-misassembly-lock-design.md`

## Global Constraints

- **Never modify** `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`, `Assets/Scenes/region 1/EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat`, or anything in `Assets/Scripts/Region1/Stars/`.
- **`FogController.cs` gets exactly one additive change** (Task 2): a new static `ExtraPulseProvider`, `Mathf.Max`'d with the existing `ComputeBeatPulse()`. **Nothing is removed.** The last-10-seconds countdown beep and its smoke lurch must behave exactly as they do today — that is an explicit user requirement, and Task 6 checks it.
- **The clock is never paused** in the mis-assembly path. Being locked out while the smoke closes in *is* the punishment. Do not add a `WordAssemblyTimer.Instance?.Pause()` anywhere in this work.
- The alert must be **additive** (`Blend SrcAlpha One`). Do not "simplify" it to a normal alpha overlay — that would hide the smoke, which is the one thing the user explicitly asked us to avoid.
- Every new shader **must** declare `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}`. Without it, UGUI logs a missing-`_MainTex` error every frame and trips Error Pause. This has bitten every shader in this scene.
- `lock_overlay`'s root GameObject **stays active** in the scene. Its `Awake` must run to register the fog hook; the component hides *itself* (intensity 0, icon off, `raycastTarget` off). Do not ship it with `SetActive(false)`.
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
- Consumes: `FogController.ExtraPulseProvider` (Task 2); `WordFlow/LockVignette` (Task 1).
- Produces:
  - `static MisassemblyLock Instance { get; }`
  - `IEnumerator PlayRoutine()` — the whole alert: fade in, icon, three beeps + three smoke lurches, fade out. Yield on it; it returns when the lock should lift.
  - Serialized: `vignette` (RawImage), `lockIcon` (Image — **its Sprite is left empty for the user to drop their lock PNG in**), `audioSource`, `beepClip`, and the timing knobs.

- [ ] **Step 1: Write the component**

Create `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// The garbage-word punishment: a cartoon red damage-screen, a lock symbol, and three beat-beeps that
/// each yank the smoke toward the book — for three seconds, during which the stones are dead.
///
/// The clock is NOT paused while this plays. That is the whole point: being frozen out costs real
/// seconds of smoke, so brute-forcing combinations has a price.
///
/// The alert is ADDITIVE (WordFlow/LockVignette, Blend SrcAlpha One). It can only add red light, so
/// it cannot hide the smoke underneath — the smoke just reads as red-hot while the alert is up.
public sealed class MisassemblyLock : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Full-screen RawImage running the WordFlow/LockVignette material.")]
    [SerializeField] private RawImage vignette;

    [Tooltip("The lock symbol. Drop the lock PNG into its Sprite — the effect works without one, it " +
             "just has no icon.")]
    [SerializeField] private Image lockIcon;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("countdown_beep.wav — the same beep the smoke clock plays in its last 10 seconds. The " +
             "clip is one long track of once-a-second beeps, so we play it from 0 and cut it after " +
             "beepAudibleSeconds to get exactly one beep.")]
    [SerializeField] private AudioClip beepClip;

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

    [Header("Lock icon pop")]
    [SerializeField, Min(0.01f)] private float iconPopDuration = 0.28f;
    [SerializeField] private float iconOvershoot = 1.8f;

    public static MisassemblyLock Instance { get; private set; }

    public bool IsLocked { get; private set; }

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int PulseID = Shader.PropertyToID("_Pulse");

    private Material vignetteMaterial;
    private Vector3 iconBaseScale = Vector3.one;
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

        if (lockIcon != null)
        {
            iconBaseScale = lockIcon.rectTransform.localScale;
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

        if (lockIcon != null)
        {
            lockIcon.gameObject.SetActive(true);
            SetIconAlpha(0f);
            lockIcon.rectTransform.localScale = Vector3.zero;
        }

        // --- flash in ---------------------------------------------------------------------------
        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetIntensity(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetIntensity(1f);

        // --- lock icon pops in --------------------------------------------------------------------
        for (float t = 0f; t < iconPopDuration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / iconPopDuration);
            if (lockIcon != null)
            {
                lockIcon.rectTransform.localScale =
                    Vector3.LerpUnclamped(Vector3.zero, iconBaseScale, EaseOutBack(k, iconOvershoot));
                SetIconAlpha(Mathf.Clamp01(k * 2f));
            }
            yield return null;
        }
        if (lockIcon != null)
        {
            lockIcon.rectTransform.localScale = iconBaseScale;
            SetIconAlpha(1f);
        }

        // --- three beats: beep + smoke lurch -------------------------------------------------------
        for (int i = 0; i < beepCount; i++)
        {
            PlayBeep();

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

        // --- fade out -----------------------------------------------------------------------------
        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Clamp01(t / fadeOutDuration);
            SetIntensity(k);
            if (lockIcon != null) SetIconAlpha(k);
            yield return null;
        }

        Hide();
        IsLocked = false;
    }

    private void PlayBeep()
    {
        if (audioSource == null || beepClip == null) return;

        // countdown_beep.wav is a long track of once-a-second beeps. Play from the top and cut it
        // after one beep's worth, so three calls = three beeps (not thirty).
        audioSource.Stop();
        audioSource.clip = beepClip;
        audioSource.loop = false;
        audioSource.time = 0f;
        audioSource.Play();
        StartCoroutine(StopBeepRoutine());
    }

    private IEnumerator StopBeepRoutine()
    {
        yield return new WaitForSeconds(beepAudibleSeconds);
        if (audioSource != null && audioSource.clip == beepClip && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void Hide()
    {
        pulse01 = 0f;
        SetIntensity(0f);
        SetPulse(0f);

        if (vignette != null) vignette.raycastTarget = false;
        if (lockIcon != null) lockIcon.gameObject.SetActive(false);
    }

    private void SetIntensity(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(IntensityID, Mathf.Clamp01(v));
    }

    private void SetPulse(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(PulseID, Mathf.Clamp01(v));
    }

    private void SetIconAlpha(float a)
    {
        if (lockIcon == null) return;
        Color c = lockIcon.color;
        c.a = Mathf.Clamp01(a);
        lockIcon.color = c;
    }

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
git commit -m "feat(lock): add MisassemblyLock — red alert, lock icon, three beat-beeps + smoke lurches"
```

---

### Task 4: Build `lock_overlay` into the scene, plus the preview menu

**Files:**
- Create: `Assets/Editor/BuildMisassemblyLock.cs`
- Creates as a side effect: `Assets/Scenes/region 1/LockVignetteMaterial.mat`
- Modifies (via the tool): `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Produces: menu items `Tools/Quest/Build Misassembly Lock` and `Tools/Quest/Lock Preview On` / `Tools/Quest/Lock Preview Off`; a wired `lock_overlay` (RawImage + AudioSource + `MisassemblyLock`, with a `lock_icon` child) as the **top sibling** of the `CutScene_bear` Canvas.

`Lock Preview On/Off` is what makes the main check automatable — a garbage word needs a human at the controls, but the preview lets us screenshot the red over the smoke without playing.

- [ ] **Step 1: Write the editor tool**

Create `Assets/Editor/BuildMisassemblyLock.cs`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Build Misassembly Lock  — builds the garbage-word red alert into CutScene_bear.
/// Tools/Quest/Lock Preview On | Off   — eyeball the alert without playing to a garbage word.
///
/// The lock_overlay sits at the TOP of the Canvas so the alert covers the smoke, the book and the
/// star board. That is safe because the vignette is ADDITIVE — it can only add red light, never hide
/// what is beneath it. Idempotent: re-running re-wires the existing overlay.
public static class BuildMisassemblyLock
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string ShaderName = "WordFlow/LockVignette";
    const string MatPath = "Assets/Scenes/region 1/LockVignetteMaterial.mat";
    const string BeepPath = "Assets/Audio/countdown_beep.wav";
    const string OverlayName = "lock_overlay";
    const string IconName = "lock_icon";

    [MenuItem("Tools/Quest/Build Misassembly Lock")]
    public static void Run()
    {
        Material mat = EnsureMaterial();
        if (mat == null) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[BuildMisassemblyLock] no Canvas in CutScene_bear"); return; }

        // --- lock_overlay: full-screen additive vignette ------------------------------------------
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

        var raw = overlay.GetComponent<RawImage>();
        if (raw == null) raw = overlay.AddComponent<RawImage>();
        raw.material = mat;
        raw.color = Color.white;
        raw.raycastTarget = false;   // MisassemblyLock switches this on only while the lock is up

        var src = overlay.GetComponent<AudioSource>();
        if (src == null) src = overlay.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;

        // --- lock_icon: the symbol (Sprite left empty — the user drops their PNG in) --------------
        Transform iconT = overlay.transform.Find(IconName);
        GameObject icon = iconT != null ? iconT.gameObject : new GameObject(IconName, typeof(RectTransform));
        if (iconT == null) icon.transform.SetParent(overlay.transform, false);

        var iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(420f, 420f);
        iconRect.localScale = Vector3.one;

        var iconImage = icon.GetComponent<Image>();
        if (iconImage == null) iconImage = icon.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        // iconImage.sprite is deliberately left as-is: the user drops the lock PNG in themselves.

        // --- wire MisassemblyLock ------------------------------------------------------------------
        var lockComp = overlay.GetComponent<MisassemblyLock>();
        if (lockComp == null) lockComp = Undo.AddComponent<MisassemblyLock>(overlay);

        var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepPath);
        if (beep == null) Debug.LogWarning($"[BuildMisassemblyLock] beep clip not found: {BeepPath}");

        var so = new SerializedObject(lockComp);
        so.FindProperty("vignette").objectReferenceValue = raw;
        so.FindProperty("lockIcon").objectReferenceValue = iconImage;
        so.FindProperty("audioSource").objectReferenceValue = src;
        if (beep != null) so.FindProperty("beepClip").objectReferenceValue = beep;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Top of the Canvas: the alert covers everything. Safe — it is additive.
        overlay.transform.SetSiblingIndex(canvas.transform.childCount - 1);

        // The root MUST stay active: its Awake registers FogController.ExtraPulseProvider and hides
        // the visuals itself. Shipping it inactive would silently kill the smoke lurch.
        overlay.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildMisassemblyLock] DONE — {OverlayName} at sibling index " +
                  $"{overlay.transform.GetSiblingIndex()} (top of Canvas), beep={(beep != null ? beep.name : "MISSING")}, " +
                  $"lock icon sprite = {(iconImage.sprite != null ? iconImage.sprite.name : "(empty — drop the PNG in)")}, scene saved");
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

Expected: `[BuildMisassemblyLock] DONE — lock_overlay at sibling index N (top of Canvas), beep=countdown_beep, lock icon sprite = (empty — drop the PNG in), scene saved`. No errors.

- [ ] **Step 3: Verify the wiring**

```
mcp__anklebreaker__unity_scene_hierarchy            parentPath: "Canvas"   maxDepth: 1   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "lock_overlay"   componentType: "MisassemblyLock"   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "lock_overlay"   componentType: "RawImage"        port: <port>
```

Expected:
- `lock_overlay` is the **last** child of the Canvas (after `star_hud`), and is **active**.
- `MisassemblyLock`: `vignette`, `lockIcon`, `audioSource` all wired; `beepClip` = `countdown_beep`.
- `RawImage`: material = `LockVignetteMaterial`, `raycastTarget` = **false** (it flips on only during a lock).

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/BuildMisassemblyLock.cs Assets/Editor/BuildMisassemblyLock.cs.meta \
        "Assets/Scenes/region 1/LockVignetteMaterial.mat" "Assets/Scenes/region 1/LockVignetteMaterial.mat.meta" \
        "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(lock): build the lock_overlay alert into CutScene_bear, plus a preview menu"
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

- [ ] **Step 3: Ask the user to play the garbage-word path**

A garbage word needs stones tapped and cannot be driven from here. Ask the user to play `CutScene_bear`
and assemble **ปก** (or any of กป / าก / าป), then report:

1. the screen flashes red the moment the second stone lands — **and the smoke is still visible through it**;
2. the lock icon appears (or, if they have not dropped the PNG in yet, no icon but everything else works);
3. **three** beeps land, ~1s apart;
4. the smoke **visibly lurches in toward the book on each beep and settles back**;
5. the stones **cannot be tapped** for those ~3 seconds;
6. then the stones **spring home by themselves** and the board is playable again;
7. the clock kept ticking the whole time (the smoke is further in than when the lock started) — but
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

- A garbage word (ปก / กป / าก / าป) flashes the screen red, shows the lock, plays three beeps, lurches the smoke inward on each, and freezes the stones for ~3 seconds — after which they spring home.
- **The smoke stays visible through the red** (additive blend).
- The clock keeps running through the lock, and the lurch does **not** make it run any faster.
- The last-10-seconds countdown beep and its lurch are **unchanged**.
- ปา and กา behave exactly as they did.
- The lock icon Sprite slot is exposed and empty, ready for the user's PNG.
- `git diff` never touches `WordAssemblyTimer.cs`, `EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat` or `Assets/Scripts/Region1/Stars/`, and `FogController.cs` shows only the additive `ExtraPulseProvider` change.
