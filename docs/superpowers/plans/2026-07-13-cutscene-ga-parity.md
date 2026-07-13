# CutScene_ga Parity - stars, smoke, audio, paa branch

> Follow-up decision: the `Success_ta_incorrect` owl/encouragement portion of this plan is
> superseded by `2026-07-13-ga-success-stars-no-owl.md`. Keep every other completed decision here.

> Smoke correction (confirmed after comparing the clean materials with `HEAD`): keep the two
> authored layers visually unchanged. `FogOverlay` remains the original dark purple and
> `CloudOverlay` remains the original grey. Do not lighten the purple, darken the grey, or retune
> density/reach. Only the timer-driven start/resume logic is brought in line with `CutScene_bear`.

## Goal

Bring `CutScene_ga` up to the same puzzle-feedback standard as `CutScene_bear` without changing
the crow entrance, the correct `gaa` story outcome, or the owl's existing encouragement.

## Confirmed behaviour

- The tray contains stone letters `ก`, `ป`, `า`.
- `กา` remains the correct word (`kaa`) and loads `Success_ga_correct`.
- `ปา` is a real but incorrect word (`paa`) and still loads the legacy-named
  `Success_ta_incorrect` scene.
- `Success_ta_incorrect` replaces the old eye animation with the standalone `Thow` hands only.
- The petrified crow remains visible during `Thow` and fades with it before the owl epilogue.
- `paa.wav` plays when `Thow` starts; the existing lose sting and `kaa_try_again` owl line remain.
- Non-word assemblies use the same fixed-red three-second lock as `CutScene_bear`.

## Scope

### CutScene_ga puzzle

1. Clone `Canvas/star_root` from `CutScene_bear` as the sole `WordAssemblyTimer` holder, with the
   bear scene's 30-second timer and last-10-seconds countdown audio. `CutScene_ga` must contain no
   `time_root`.
2. Replace the `ต` stone's visual and dimensions with the bear scene's `ป` stone while keeping
   the existing `ก` and `า` stones.
3. Update every scene-local `MagicStonePuzzleController` consistently:
   - target: `กา`, id `kaa`, scene `Success_ga_correct`;
   - alternate real word: `ปา`, id `paa`, scene `Success_ta_incorrect`;
   - letters and TTS: `ก/ป/า`, `gameplay_ko/gameplay_po/gameplay_aa`;
   - target echo: `gameplay_ko/gameplay_aa/gameplay_kaa`;
   - alternate echo: `gameplay_po/gameplay_aa/gameplay_paa`.
4. Install the existing `StarHud` prefab in live-award mode with `resetOnAwake = true`.
5. Install two smoke overlays using the shared edge/cloud materials and the bear scene's tuned
   countdown curve, ordering them above the background and below all puzzle UI. Preserve the
   committed material appearance exactly:
   - `EdgeFogMaterial`: purple `(0.48170567, 0.27901027, 0.5622641, 1)`, density `0.75`, max reach
     `0.23`, core fraction `0.40`, fade-in `0.50`, authored progress `0.60`;
   - `CloudFogMaterial`: grey `(0.72, 0.73, 0.77, 1)`, density `0.55`, max reach `0.30`, core
     fraction `0.35`, fade-in `0.50`, authored progress `0.60`.
   Runtime `FogController` still resets its material instance to progress `0` before assembly.
6. Install the current `MisassemblyLock` overlay using the fixed-red material and the same beep
   wiring as the bear scene. Legacy key art remains inactive.

### Success_ta_incorrect

1. Remove the old `Eye`/eye-animation object.
2. Instantiate `Thow.prefab`, add a `CanvasGroup`, and let `BearCutscene` own one `Thow` PlayClip
   step, the end fade, owl hand-off, retry flags, and return to `CutScene_ga`.
3. Enable `playPaaThrowSfxOnStart`.
4. Keep the static final-frame stone crow and include its `CanvasGroup` in `alsoFade`.
5. Preserve `OwlEpilogue`, `playLoseSting`, `kaa_try_again`, and all owl timing/visual references.

### Durable editor tooling

- Add one idempotent editor command that applies the `CutScene_ga` puzzle parity changes.
- Update the existing success-scene builders so rerunning them cannot restore the eye/`ตา` flow.
- Do not modify `MagicStonePuzzleController`, `WordAssemblyTimer`, `StarHud`, `FogController`,
  `MisassemblyLock`, shared materials, or either correct-success scene unless verification exposes
  a genuine blocker.

## Verification

- [x] Unity scripts compile with zero errors.
- [x] `CutScene_ga` contains exactly one timer, star HUD, red-lock overlay, and two fog overlays.
- [x] `CutScene_ga` contains one `Canvas/star_root`, no `time_root`, and smoke stays at zero until
      word assembly begins.
- [x] Purple and grey smoke materials match their committed pre-change values exactly.
- [x] `กา` awards the valid-word star, uses `kaa` audio/data, and reaches `Success_ga_correct`.
- [x] `ปา` awards only the valid-word star, uses `paa` audio/data, and reaches
      `Success_ta_incorrect`.
- [x] A non-word holds the fixed red lock for three seconds while the clock continues.
- [x] Countdown audio and lock audio do not duplicate during the last ten seconds.
- [x] `Success_ta_incorrect` shows `Thow` with the stone crow, plays `paa.wav`, then preserves the
      lose sting and `kaa_try_again` owl encouragement before returning in retry mode.
- [x] No unrelated scene, fog material, gameplay controller, or correct-success behaviour changes.
