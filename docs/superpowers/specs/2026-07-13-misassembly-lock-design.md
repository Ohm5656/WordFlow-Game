# Mis-assembly Lock — red alert + 3-second lockout (CutScene_bear)

Date: 2026-07-13
Status: approved, ready for implementation

## Problem

`CutScene_bear` has three stones (ก, ป, า) and two slots, so the child can assemble six words. Two of
them are real — **ปา** (target) and **กา** (alt). The other four — **ปก, กป, าก, าป** — are garbage.

Today a garbage word costs nothing: the stones just sit there and the child taps them back out. There
is no pressure, so a child can brute-force combinations for free while the clock ticks quietly on.

We want garbage to **hurt**: a cartoon red damage-screen, a lock symbol, and a **3-second lockout**
with no countdown number. The clock keeps running through it — being frozen out is the punishment,
because the smoke closes in while you can do nothing.

## Requirements

1. **Trigger:** both slots filled with a word that is neither ปา nor กา.
2. **Red alert screen.** Cartoon "you took a hit" look. **The smoke underneath must stay visible.**
3. **Lock symbol** on screen for the duration. **No countdown digits.**
4. **3-second lockout.** Stones cannot be tapped. When it ends, the two mis-placed stones **spring
   back home by themselves** so the board is clear for the next attempt.
5. **Three beat-beeps** during the lock — the same beep the smoke clock plays in its last 10 seconds.
6. **The smoke visibly lurches toward the book on each beep** — but must **not actually advance any
   faster**. No extra difficulty; the lurch is a scare, not a speed-up.
7. **The last-10-seconds beep logic is not removed or changed.**
8. The clock **keeps running** during the lock (it is not paused).

## The blocker, and the answer: additive blending

A plain red overlay drawn over the smoke **would** hide it. This is the same alpha-over arithmetic
that buried the purple smoke under the grey coat: the top layer covers what is beneath it in
proportion to its own alpha, so a red screen at alpha 0.5 leaves only half the smoke.

**The alert is therefore drawn with `Blend SrcAlpha One` — additive.** Additive can only *add* red
light to what is already on screen. It is mathematically incapable of hiding anything beneath it. The
smoke keeps every one of its billows; it simply reads as *red-hot* smoke while the alert is up. That
is a better read for "danger" than a red sheet would have been anyway.

The same applies to the book and stones underneath — they tint red but stay perfectly legible.

## The smoke lurch: already built, and free

`EdgeFog` computes its front as:

```
reach = _Progress * _MaxReach + _Pulse * _PulseReach
```

`_Pulse` adds a **temporary** reach offset that snaps back — it never touches `_Progress`. That is
exactly requirement 6: the smoke jumps toward the book and settles, and **the clock is not advanced by
a single millisecond**. It is the same mechanism the countdown beep already uses in the last 10
seconds, so the lurch will feel identical — three of them, on the three lock beeps.

`FogController` currently owns `_Pulse` outright: it writes `ComputeBeatPulse()` to the material every
frame, so an outside caller cannot drive a lurch. It gets **one additive hook**:

```csharp
public static System.Func<float> ExtraPulseProvider;   // new
...
float pulse = ComputeBeatPulse();                       // unchanged
if (ExtraPulseProvider != null) pulse = Mathf.Max(pulse, ExtraPulseProvider());
fogMaterial.SetFloat(PulseID, pulse);
```

`Mathf.Max`, never replacement: **the last-10-seconds beep pulse keeps behaving exactly as it always
has** (requirement 7). This is the only change to `FogController`, and it removes nothing.

Because both `FogOverlay` and `CloudOverlay` run their own `FogController`, a single provider makes
**both** smoke layers lurch together.

## Design

### 1. `Assets/Scenes/region 1/LockVignette.shader` — `Shader "WordFlow/LockVignette"`

Full-screen procedural red vignette. Additive (`Blend SrcAlpha One`). No texture, no art asset.

- `[PerRendererData] _MainTex` — **must** be declared or UGUI logs a missing-`_MainTex` error every
  frame, which trips Error Pause. (This has bitten every shader in this scene.)
- `_Color` — alert red, default `(0.95, 0.15, 0.12)`.
- `_Intensity` (0..1, driven) — the fade in/out.
- `_EdgeStart` / `_EdgeEnd` — radial falloff: clear-ish in the middle, hot at the edges and corners.
- `_CoreGlow` — a faint red wash even at centre, so the whole screen reads "hit", not just a frame.
- `_Pulse` (0..1, driven) — throbs the intensity on each beep, in step with the smoke's lurch.

### 2. `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`

One component on a new `lock_overlay` GameObject (top sibling of the `CutScene_bear` Canvas — above
the smoke, the book and the star board, so the alert covers the whole screen).

- The root GameObject **stays active** (its `Awake` has to run to register the static hooks); the
  component hides itself by driving `_Intensity` to 0, hiding the icon, and switching the RawImage's
  `raycastTarget` off when idle.
- `PlayRoutine()`:
  1. fade `_Intensity` 0 → 1 (~0.12s), `raycastTarget = true` (this alone eats every tap),
  2. pop the lock icon in with an ease-out-back overshoot,
  3. three times, one second apart: play the beep, and drive `pulse01` from 1 down through
     `Mathf.Exp(-t * pulseDecay)` — **the same decay curve `FogController.ComputeBeatPulse` uses**, so
     the lurch is indistinguishable from a countdown beat,
  4. fade out, hide, `raycastTarget = false`.
- Registers `FogController.ExtraPulseProvider = () => pulse01` in `Awake`, clears it in `OnDestroy`.
- **The beep:** `countdown_beep.wav` is one ~10-second track of once-a-second beeps (the clock seeks
  into it). To get exactly three, the lock plays it from `t = 0` and stops it after
  `beepAudibleSeconds` (~0.4s) — one clean beep — three times, one second apart. It uses its **own**
  `AudioSource`, so it never interferes with the clock's.
- The lock icon `Sprite` is a serialized field, left empty for the user to drop their PNG in.

### 3. `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`

- `CanInteract` — one gate for every stone tap — gains `&& !misassemblyLocked`. That single term is
  the entire lockout.
- The garbage-word `else` branch (line ~808) starts `MisassemblyRoutine()`, which plays the alert,
  then **springs both stones home** (`slotOccupants[i] = null; stone.SetCurrentSlot(-1); stone.ReturnHome();`
  — the same calls a manual tap-out already makes) and unlocks.
- The clock is **not** paused anywhere in this path, so it keeps ticking. That is the design.

### 4. `Assets/Editor/BuildMisassemblyLock.cs` — `Tools/Quest/Build Misassembly Lock`

Creates the material, builds and wires `lock_overlay` in `CutScene_bear`, saves the scene. Idempotent.

### 5. `Tools/Quest/Lock Preview On | Off`

Sets `_Intensity` on the shared material so the alert can be eyeballed **without playing to a garbage
word** (which needs a human at the controls). This is what makes the key check automatable: turn on
`Fog Preview 60%` *and* `Lock Preview On`, screenshot, and confirm the smoke is still clearly visible
through the red.

## Scoring: unaffected

A garbage word never reaches `WordResultRoutine`, so no star is awarded and no telemetry fires — same
as today. The stars, the smoke clock, and the grade/backend path are all untouched.

## Verification

1. `Fog Preview 60%` + `Lock Preview On` → screenshot. **The smoke must still be clearly visible
   through the red.** This is the requirement-2 gate.
2. `Lock Preview Off` → the screen is clean.
3. Play, assemble **ปก** (garbage): red flashes up, lock icon appears, three beeps land, the smoke
   lurches inward on each and settles back, stones are dead to the touch, after ~3s they spring home
   and the board is playable again.
4. During all of it the clock keeps counting down (the smoke's `_Progress` keeps growing) — and it
   grows at exactly the normal rate: the lurch is temporary and does not accumulate.
5. Let the clock reach its last 10 seconds: **the countdown beep and its smoke lurch still work
   exactly as before** (requirement 7).
6. Assembling ปา or กา still behaves exactly as it did.

## Out of scope

- Any change to `WordAssemblyTimer`, `EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat`,
  or the star system.
- Removing or altering the last-10-seconds beep logic.
- A countdown number on the lock (explicitly not wanted).
- Scaling the lurch bigger than a countdown beat's. If more punch is wanted later, `_PulseReach` on
  the fog materials is the knob — but note it also scales the countdown beep's lurch.
