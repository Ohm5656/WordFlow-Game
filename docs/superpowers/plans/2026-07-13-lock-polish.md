# Mis-assembly Lock Polish - fixed red hold, one countdown rhythm, no key art

**Goal:** Finish the garbage-word lock without changing any unrelated puzzle behavior.

When the player assembles a garbage word, the screen becomes dark blood-red for one fixed
three-second lockout. The red does not flash or throb. A short fade-in and fade-out are included
inside those three seconds. Outside the countdown window the lock keeps its own three beeps and
smoke lurches. In the last 10 seconds it uses `WordAssemblyTimer`'s existing beep and smoke rhythm
instead of playing a second copy. The `key_root` lock/unlock/burst art and `unlock.wav` are not shown
or played.

Previous spec: `docs/superpowers/specs/2026-07-13-misassembly-lock-design.md`

## Confirmed decisions

- The reported duplicate count is duplicate countdown audio, not the timer losing two seconds at a
  time.
- The alert is a fixed red hold, not a pulse. Fade-in and fade-out remain for polish, but both are
  part of the exact three-second lockout.
- Do not display `key_root/lock`, `key_root/unlock`, or `key_root/effect_star1..4`.
- Do not play `unlock.wav` when the lock ends.
- Keep `key_root` and its serialized references in the scene but inactive. This avoids an unrelated
  scene hierarchy migration and preserves the authored assets.

## Scope and safety constraints

- Modify only:
  - `docs/superpowers/plans/2026-07-13-lock-polish.md`
  - `Assets/Scenes/region 1/LockVignette.shader`
  - `Assets/Scenes/region 1/LockVignetteMaterial.mat`
  - `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`
- Never modify `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`.
- Do not modify `Assets/Scripts/Common/FogController.cs`, `MagicStonePuzzleController.cs`, fog
  shaders/materials, star logic, success routing, stone return behavior, or scene layout.
- Preserve `[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}` in the vignette shader.
- Preserve the multiply blend (`Blend DstColor Zero`) so the alert darkens and reddens the scene
  while the smoke remains visible.
- Existing unrelated worktree changes must remain untouched.

## Task 1: Make the red alert fixed instead of pulsing

**Files:**

- `Assets/Scenes/region 1/LockVignette.shader`
- `Assets/Scenes/region 1/LockVignetteMaterial.mat`

- [x] Keep the dark blood-red multiply tint and `_Strength` ceiling.
- [x] Make the fragment strength depend only on `_Intensity`; `_Pulse` must not affect the red.
- [x] Remove `_BaseLevel`, because there is no between-beat visual level anymore.
- [x] Keep `_Pulse` at zero in the material for compatibility with the existing material interface.
- [x] Reset the material's authored `_Intensity` to zero so editor preview state does not ship.

Expected result: during a lock, the screen fades into one stable dark-red state, stays stable, then
fades out. Beeps and smoke movement do not change the red intensity.

## Task 2: Make the lockout exactly three seconds

**File:** `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`

- [x] Add an explicit `lockoutDuration` with a default of `3f`.
- [x] Run fade-in, steady hold, and fade-out inside that one duration. Do not append padlock,
  unlock, burst, hold, or alignment waits after it.
- [x] Keep input blocked and `IsLocked` true for the same three-second routine.
- [x] Keep `key_root` images inactive for the whole routine.
- [x] Do not play `unlockSfx`.
- [x] Leave the serialized key-art fields and scene objects intact to minimize migration risk.

Expected result: the stones spring home and input unlocks after approximately 3.0 seconds, allowing
only normal frame-level coroutine tolerance.

## Task 3: Use one clean beat source

**File:** `Assets/Scripts/Region1/Cutscenes/MisassemblyLock.cs`

- [x] If the final-10-seconds countdown is active or will begin during this lockout, schedule beats
  on the clock's next integer-second boundaries without delaying the start or extending the end of
  the three-second lockout.
- [x] If more than `beepWindow + lockoutDuration` remains, preserve the original local beat timing
  at 0, 1, and 2 seconds. Do not phase-shift the normal early-round feedback.
- [x] Outside the last 10 seconds, play the lock's own beep and drive its existing extra smoke lurch.
- [x] At or below `beepWindow` (default 10 seconds), do not play the lock beep and do not add a
  second smoke lurch. Let `WordAssemblyTimer` and `FogController` provide the existing countdown
  beep/lurch on that same boundary.
- [x] Re-evaluate the remaining time at each beat so crossing into the last 10 seconds during the
  lock cannot create an overlapping beep.
- [x] When no puzzle clock is running, retain three local beats at 0, 1, and 2 seconds.

Expected result: a garbage word in the last 10 seconds produces one clean countdown beep per second,
with no second copy and no doubled lurch. A garbage word earlier still produces three lock beeps.

## Task 4: Verification

- [x] Refresh/compile in Unity and confirm zero new shader or C# compilation errors.
- [x] Check the Unity console for new errors after compilation.
- [x] Verify the diff touches only the four scoped files listed above; pre-existing unrelated dirty
  files may remain dirty but must not receive new edits.
- [ ] Play case A: assemble a garbage word with more than 10 seconds left.
  - dark red is stable, not pulsing;
  - three clean local beeps and smoke lurches occur;
  - no key, unlock, burst art, or unlock sound appears;
  - input returns and stones spring home after three seconds.
- [ ] Play case B: assemble a garbage word with about 8 seconds left.
  - only the countdown track is audible;
  - no duplicate beep or duplicate smoke lurch occurs;
  - red remains stable for the same three-second lockout;
  - countdown continues normally afterward.
- [ ] Regression check: the countdown beep/lurch works by itself, and the valid-word success routes
  remain unchanged.

## Done when

- The alert is dark blood-red and visually stable for a total three-second lockout, including fades.
- The final-10-seconds case has one clean countdown rhythm with no duplicated sound or lurch.
- No `key_root` art and no unlock sound is presented during the lock.
- The smoke, book, stones, timer, success routing, and stone return behavior remain otherwise
  unchanged.
