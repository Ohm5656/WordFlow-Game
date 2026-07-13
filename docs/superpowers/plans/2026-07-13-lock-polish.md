# Mis-assembly Lock Polish — a dangerous red, and beeps that don't collide

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two fixes to the garbage-word lock, both found by playing it:

1. **The alert reads as candy pink, not danger.** It is additive, which *brightens* — and this scene is bright (cream book, pale sky), so adding red gives sweet pink. Danger reads as **darker + red**, not brighter. Switch the blend from additive to **multiply**.
2. **The alert sits still.** It should **throb on the beat** — the red should surge on each beep and sink back between them, so the alarm pulses instead of holding a flat tint. Today `_Pulse` only *adds* to an already-saturated base, so nothing visibly moves. Rework it so the pulse drives the alert's **amplitude from a floor up to full**.
3. **In the last 10 seconds the beeps collide.** `WordAssemblyTimer` is already playing the ~10s `countdown_beep.wav` track continuously (it seeks into it once and lets it run), and the lock plays *the same clip* from `t = 0` in three short bursts at an arbitrary phase. Two copies of one beep, out of phase. Fix by **phase-locking the lock's beats to the clock's second boundary** and **not re-playing a beep the clock is already sounding**.

**Nothing else about the lock changes.** The padlock, the burst, the 3-second lockout, the stones springing home, the smoke lurch — all stay exactly as they are and were signed off.

Previous spec: `docs/superpowers/specs/2026-07-13-misassembly-lock-design.md`

## Why multiply is still safe for the smoke (the user's original worry)

Additive was chosen because it can only *add* light and therefore cannot hide the smoke. Multiply keeps that guarantee by a different route: `result = dst × tint`. It **scales** every pixel; it never covers one. The smoke's contrast against the background is preserved proportionally — the whole screen just gets darker and redder. So the smoke is still fully visible; it now reads as smoke in a blood-red room instead of smoke in a pink one.

What multiply *does* crush is the green and blue channels (that is what makes it feel dangerous). To stop the purple smoke losing all of its blue, the tint keeps a floor in G and B (`0.12`, `0.15`) rather than going to pure red, and a `_Strength` ceiling caps how dark it can ever get.

## Global Constraints

- **Never modify** `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`. The last-10-seconds beep and its `beepPlaying` guard stay exactly as they are — **this fix works by reading the clock, not by changing it.**
- Do not change `Assets/Scripts/Common/FogController.cs` (its `ExtraPulseProvider` hook is already in and correct), `EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat`, or anything in `Assets/Scripts/Region1/Stars/`.
- Do not change the lockout duration, the padlock/burst animation, the stone spring-home, or `MagicStonePuzzleController`. This plan touches exactly two files (plus the lock's material).
- The shader **must** keep `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` — without it UGUI logs a missing-`_MainTex` error every frame and trips Error Pause.
- **The material asset holds stale values.** `LockVignetteMaterial.mat` was written with the *old* shader's defaults (`_Color` = bright red `0.95, 0.15, 0.12`, `_CoreGlow` = `0.08`). Changing the shader's defaults does **not** update a material that already has those keys saved — this bit us on `CloudFogMaterial`. Task 1 edits the `.mat` explicitly.
- **Run `Assets/Refresh`, then poll `unity_get_compilation_errors` until `isCompiling: false`, before invoking a menu item you just wrote.**
- Before any MCP call: `unity_list_instances` then `unity_select_instance`. The port hops between 7890 and 7891 on domain reload. Pass `port:` on every call.
- **Open the Game view (`Window/General/Game`) before every screenshot**, or `unity_screenshot_game` silently returns a flat ~100KB image with no UI. Screenshots land ~60-75s later — poll for the file and check it is multi-MB.
- The `[Telemetry] 404` / `[Session] open failed` ngrok warnings and the `EditorStyles.get_toolbarButtonRight` NullReferenceException are pre-existing. Ignore them.
- Commit after each task.

---

### Task 1: Make the alert dark and dangerous (multiply, not additive)

**Files:**
- Modify: `Assets/Scenes/region 1/LockVignette.shader`
- Modify: `Assets/Scenes/region 1/LockVignetteMaterial.mat`

**Interfaces:**
- The driven property names (`_Intensity`, `_Pulse`) are **unchanged**, so `MisassemblyLock` and the `Tools/Quest/Lock Preview On|Off` menu keep working untouched.
- New tunable: `_Strength` — a ceiling on how dark the alert can ever get.

- [ ] **Step 1: Rewrite the shader as a multiply**

Replace the whole of `Assets/Scenes/region 1/LockVignette.shader` with:

```hlsl
Shader "WordFlow/LockVignette"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it silences the
        // per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause).
        // The fragment ignores it — the vignette is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // Deep blood red, not a bright signal red. Multiply drives everything toward this colour, so
        // a bright tint would wash the screen out instead of darkening it. The green and blue floors
        // (0.12 / 0.15) stop the purple smoke from losing its blue entirely.
        _Color ("Alert Tint", Color) = (0.55, 0.12, 0.15, 1)

        _Intensity ("Intensity (driven)", Range(0, 1)) = 0
        _Pulse ("Pulse (driven)", Range(0, 1)) = 0

        _EdgeStart ("Vignette Inner Edge", Range(0, 1.5)) = 0.15
        _EdgeEnd ("Vignette Outer Edge", Range(0, 1.5)) = 1.1
        _CoreGlow ("Centre Tint", Range(0, 1)) = 0.3

        // How much red is left BETWEEN beats. The alert throbs from this floor up to full on every
        // beep and sinks back — an alarm that pulses, not a flat sheet of red.
        //  0.35 = the danger state persists between beats (recommended)
        //  0    = a hard strobe: full red on the beat, completely clear between them
        _BaseLevel ("Between-Beat Level", Range(0, 1)) = 0.35

        // Ceiling on the darkening, so the book and stones never fall into unreadable shadow.
        _Strength ("Max Strength", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        // MULTIPLY, not additive. Additive brightens — and on this bright scene (cream book, pale
        // sky) adding red gave candy pink, which reads as sweet, not dangerous. Multiply darkens and
        // reddens instead: result = dst * tint.
        //
        // It keeps the guarantee additive gave us: multiply SCALES every pixel, it never covers one,
        // so the smoke's contrast against the background survives proportionally and the smoke stays
        // fully visible. It just reads as smoke in a blood-red room now. Do not change this line.
        Blend DstColor Zero
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
            float _BaseLevel;
            float _Strength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0 at the centre, 1 at the edge midpoints, ~1.41 in the corners — the alert closes
                // in hardest from the edges, while the book in the middle stays readable.
                float d = length((i.uv - 0.5) * 2.0);

                float vignette = smoothstep(_EdgeStart, _EdgeEnd, d);

                // _CoreGlow keeps a wash of red even dead centre, so the whole screen reads "you took
                // a hit" rather than "there is a red frame around the screen".
                float shape = saturate(vignette + _CoreGlow);

                // THE THROB. _Pulse spikes to 1 on each beep and decays back before the next, so the
                // alert surges to full and sinks to _BaseLevel between beats — an alarm that pulses.
                // The old formula had _Pulse merely ADD to an already-saturated base, so nothing
                // visibly moved; here it drives the amplitude, which is what makes it read as alive.
                // _BaseLevel = 0 turns this into a hard strobe (full red on the beat, clear between).
                float level = lerp(_BaseLevel, 1.0, saturate(_Pulse)) * _Intensity;

                // lerp from white (multiply by 1 = untouched) toward the tint. _Strength caps how far
                // it can ever go, so nothing is ever crushed to black.
                float3 tint = lerp(float3(1, 1, 1), _Color.rgb, saturate(shape * level) * _Strength);

                return float4(tint, 1.0);
            }

            ENDCG
        }
    }
}
```

Two things changed and both matter: `Blend SrcAlpha One` → **`Blend DstColor Zero`**, and the fragment now returns a **multiplier** (white = untouched) instead of a colour-plus-alpha.

- [ ] **Step 2: Refresh the material's stale values**

The material already has the *old* shader's defaults baked in. New properties (`_Strength`) will pick up the shader default automatically, but the keys that already exist will not — they are stale and must be edited by hand.

In `Assets/Scenes/region 1/LockVignetteMaterial.mat`, replace the whole `m_Floats` / `m_Colors` block:

```yaml
    m_Floats:
    - _CoreGlow: 0.08
    - _EdgeEnd: 1.15
    - _EdgeStart: 0.35
    - _Intensity: 0
    - _Pulse: 0
    - _PulseGain: 0.75
    m_Colors:
    - _Color: {r: 0.95, g: 0.14999998, b: 0.11999995, a: 1}
```

with:

```yaml
    m_Floats:
    - _BaseLevel: 0.35
    - _CoreGlow: 0.3
    - _EdgeEnd: 1.1
    - _EdgeStart: 0.15
    - _Intensity: 0
    - _Pulse: 0
    - _Strength: 0.85
    m_Colors:
    - _Color: {r: 0.55, g: 0.12, b: 0.15, a: 1}
```

`_PulseGain` is gone — the pulse now drives the amplitude (`_BaseLevel` → 1) rather than adding gain
on top. `_Intensity` and `_Pulse` stay at `0`; they are driven at runtime.

- [ ] **Step 3: Compile and shoot the gate — is it dark, dangerous, and is the smoke still there?**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance          port: <from the list>
mcp__anklebreaker__unity_console_clear            port: <port>
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
mcp__anklebreaker__unity_console_log              type: "error"     port: <port>
```

Expected: no `Shader error in 'WordFlow/LockVignette'`.

Then:

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"          port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 60%"   port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Lock Preview On"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/Lock_multiply.png"   port: <port>
```

Poll for the file (~60-75s, multi-MB), then read it.

Note the preview menu holds `_Pulse` at 0, so this screenshot shows the alert at its **between-beat
floor** (`_BaseLevel` 0.35) — the quietest it ever gets during a lock. On the beat it surges to full.

**Expected — the pass/fail criteria, both must hold:**
1. the screen is **darkened and red**, closing in from the edges — heavy and threatening, **not pink and not bright**;
2. the smoke is **still clearly visible** through it, and the book / stones / padlock are still legible.

Compare against `Assets/Screenshots/Lock_over_smoke.png` (the pink version) to be sure it actually changed.

**If it went pink again**, the blend line did not take — check for `Blend DstColor Zero`.
**If the book is unreadably dark**, lower `_Strength` (0.85 → 0.7) or `_CoreGlow` (0.3 → 0.18) in the material. Do not touch the blend.

Then clean up:

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Lock Preview Off"   port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview Off"    port: <port>
```

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scenes/region 1/LockVignette.shader" "Assets/Scenes/region 1/LockVignetteMaterial.mat"
git commit -m "fix(lock): darken the alert with a multiply, and make it throb on the beat

Additive adds light, and this scene is bright, so a red alert came out candy
pink - sweet, not dangerous. Multiply reddens by darkening instead. It keeps
the guarantee additive gave us: it scales every pixel rather than covering
one, so the smoke stays fully visible underneath.

_Pulse used to merely add gain on top of an already-saturated base, so the
alert sat still. It now drives the amplitude from _BaseLevel up to full, so
the red surges on every beep and sinks back - an alarm that pulses. Set
_BaseLevel to 0 for a hard strobe."
```

---

### Task 2: Stop the beeps colliding in the last 10 seconds

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`

**Interfaces:**
- Consumes: `WordAssemblyTimer.Instance.SmokeRemaining` — public, read-only. `MisassemblyLock` sits in the same folder and assembly, so it can reference the timer directly. **The timer itself is not touched.**
- Produces: a new serialized `beepWindow` (default 10, must match the timer's `countdownAt`).

**The bug, precisely.** `WordAssemblyTimer.HandleBeep()` fires once when `remaining` first drops below `countdownAt` (10s): it seeks `countdown_beep.wav` to `countdownAt - remaining` and plays **the whole ~10-second track**, which is a beep every second. Its `beepPlaying` guard means it never restarts. So for the last 10 seconds, that track is *already sounding a beep every second*. The lock then plays **the same clip** from `t = 0`, in three 0.4s bursts, starting whenever the garbage word happened to land — an arbitrary phase against the track. Two copies of one beep, offset. Mush.

**The fix, two halves:**
- **Phase-lock:** wait for the clock's next second boundary before each beat. The clock's beeps land when `remaining` crosses an integer, so the wait is `remaining % 1`. Now the lock's beat, its smoke lurch, and the clock's own beep + lurch all land on the same instant instead of fighting at different phases.
- **Don't double the sound:** if the clock's countdown track is already audible (`remaining <= beepWindow`), the lock **plays no beep of its own** — the clock is already beeping on exactly that boundary, once a second, which *is* the three beats. The lock still lurches the smoke. Outside the window (`remaining > 10`), the clock is silent, so the lock plays its own beep as before.

- [ ] **Step 1: Add the clock hooks**

In `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`, in the `[Header("Beats")]` block, after `beepAudibleSeconds`, add:

```csharp
    [Tooltip("Seconds left at which WordAssemblyTimer's own countdown track starts beeping — must " +
             "match its countdownAt (10). Inside this window the clock is already sounding a beep " +
             "every second, so the lock rides on it instead of playing a second, out-of-phase copy " +
             "of the same clip on top.")]
    [SerializeField, Min(0f)] private float beepWindow = 10f;
```

Then, next to the other private helpers near the bottom of the class (just above `private static RectTransform Rect(...)`), add:

```csharp
    /// Seconds left on the puzzle clock, or 0 when there is no clock (the other cutscene scenes).
    private float ClockRemaining =>
        WordAssemblyTimer.Instance != null ? WordAssemblyTimer.Instance.SmokeRemaining : 0f;

    private bool ClockRunning => ClockRemaining > 0.01f;

    /// True when WordAssemblyTimer's countdown track is already sounding a beep every second. The
    /// lock must NOT play its own copy of the same clip on top of it — that is what made the last ten
    /// seconds sound like mush.
    private bool ClockIsBeeping => ClockRunning && ClockRemaining <= beepWindow;

    /// Wait until the clock's next second boundary. Its beeps land when `remaining` crosses an
    /// integer, so the time to that crossing is the fractional part. Landing our beat there puts our
    /// lurch, the clock's beep and the clock's own lurch on the same instant instead of three
    /// different phases. No-op when there is no clock running.
    private IEnumerator AlignToClockBeatRoutine()
    {
        if (!ClockRunning) yield break;

        float wait = ClockRemaining % 1f;
        if (wait > 0.03f) yield return new WaitForSeconds(wait);
    }
```

- [ ] **Step 2: Rewrite the beat loop to use them**

In `PlayRoutine()`, find the beat loop:

```csharp
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
```

and replace it with:

```csharp
        // --- three beats: beep + smoke lurch ---------------------------------------------------------
        for (int i = 0; i < beepCount; i++)
        {
            // Land the beat on the clock's own second boundary, so our lurch, the clock's beep and the
            // clock's lurch all hit the same instant rather than three different phases.
            yield return AlignToClockBeatRoutine();

            // Inside the last 10 seconds the clock's countdown track is ALREADY beeping on this exact
            // boundary, once a second. Playing our own copy of the same clip on top of it is what made
            // the audio a mush — so we stay silent and let the clock's beep be the beat. Outside the
            // window the clock is quiet, so we sound it ourselves.
            if (!ClockIsBeeping)
            {
                PlayClip(beepClip, cutAfter: beepAudibleSeconds);
            }

            // pulse01 drives BOTH the smoke's inward lurch (via FogController.ExtraPulseProvider) and
            // the vignette's throb, so the sound, the red and the smoke all land on the same beat.
            for (float t = 0f; t < beepInterval; t += Time.deltaTime)
            {
                pulse01 = Mathf.Exp(-t * pulseDecay);
                SetPulse(pulse01);
                yield return null;
            }
        }
```

Nothing else in the file changes. The lockout is still `beepCount × beepInterval` ≈ 3 seconds (the alignment wait is under one second on the first beat and ~0 on the rest, since the beats are already a second apart).

- [ ] **Step 3: Compile**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs
git commit -m "fix(lock): phase-lock the lock beats to the clock, and never double its beep

WordAssemblyTimer plays the whole 10-second countdown track in the last ten
seconds - a beep every second. The lock was playing the SAME clip from t=0 at
whatever phase the garbage word landed on, so two copies collided.

Now each beat waits for the clock's next second boundary (remaining % 1), and
inside the beep window the lock stays silent and lets the clock's beep BE the
beat. Outside the window it sounds its own. The timer is not touched."
```

---

### Task 3: Verification

- [ ] **Step 1: Ask the user to play the two cases**

Both need stones tapped and cannot be driven from here.

**Case A — garbage word with lots of time left (clock silent):**
Assemble **ปก** early in the round. Expect:
1. the screen goes **dark blood-red**, closing in from the edges — heavy, dangerous, **not pink**;
2. **the red throbs on the beat** — it surges to full on each beep and sinks back between them, three
   times. It should read as an alarm pulsing, not a flat sheet of red;
3. **the smoke is still clearly visible** through it;
4. **three beeps** from the lock itself, one a second, each landing with a smoke lurch **and** a red surge;
5. the padlock, the unlock burst, the stones springing home — all exactly as before.

**Case B — garbage word inside the last 10 seconds (clock already beeping):**
Let the clock run down to ~8 seconds, *then* assemble **ปก**. Expect:
1. **the beeps do not collide** — a single clean beep per second, not two overlapping;
2. the smoke lurches land **on** those beeps, not between them;
3. the countdown's own beeping carries on normally after the lock lifts.

- [ ] **Step 2: Ask the user to confirm nothing else regressed**

1. The last-10-seconds countdown beep and its smoke lurch still work on their own (no garbage word involved).
2. ปา still awards the stars and goes to `Success_pa`; กา still awards one and goes to `Success_ga`.

- [ ] **Step 3: Tune the red if asked**

All on `LockVignetteMaterial` — no code edit:

| Want | Knob |
|---|---|
| Darker / more oppressive | raise `_Strength` (0.85 → 0.95) |
| Book too dark to read | lower `_Strength` (0.85 → 0.7) or `_CoreGlow` (0.3 → 0.18) |
| More red, less black | raise `_Color`'s R, or lift the G/B floors |
| Red band too thick | raise `_EdgeStart` (0.15 → 0.4) |
| Whole screen too evenly red | lower `_CoreGlow` (0.3 → 0.12) — the edges keep their weight |
| **Throb too subtle** — want a bigger swing per beep | **lower `_BaseLevel` (0.35 → 0.2)** — the red sinks further between beats, so the surge reads bigger |
| **Want a hard strobe** (full red on the beat, screen completely clear between) | **`_BaseLevel` = 0** |
| Throb too flickery / want the danger to sit heavier | raise `_BaseLevel` (0.35 → 0.55) |
| Throb snaps back too fast / too slow | `pulseDecay` on the `MisassemblyLock` component (6 = the countdown beat's own decay) |

- [ ] **Step 4: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/LockVignetteMaterial.mat"
git commit -m "feat(lock): tune the alert red"
```

(Skip if nothing was tuned.)

---

## Done when

- The garbage-word alert is **dark and blood-red**, not bright pink — and the smoke is still plainly visible through it.
- The alert **throbs on the beat**: it surges to full on each beep and sinks back to `_BaseLevel` between them. `_BaseLevel = 0` gives a hard strobe if that is preferred.
- A garbage word in the last 10 seconds produces **one clean beep per second**, not two overlapping ones, with the smoke lurches landing on the beat.
- A garbage word outside that window still sounds its own three beeps, as before.
- Everything else about the lock — the padlock, the unlock burst, the 3-second lockout, the stones springing home, the smoke lurch — is unchanged.
- `git diff` touches only `LockVignette.shader`, `LockVignetteMaterial.mat` and `MisassemblyLock.cs`. `WordAssemblyTimer.cs`, `FogController.cs`, `MagicStonePuzzleController.cs`, the fog shaders/materials and `Stars/` are all untouched.
