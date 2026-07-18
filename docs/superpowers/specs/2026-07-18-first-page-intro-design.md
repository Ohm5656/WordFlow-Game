# first_page — Title Screen Video Intro

**Date:** 2026-07-18
**Scene:** `Assets/Scenes/first_page.unity` (currently empty — camera only, not in Build Settings)

## Goal

Turn `first_page` into the game's title screen: a speed-ramped intro video that
plays forward then backward, then an idle ping-pong loop that waits for the
player to tap into the game (WorldMap).

## Source Material

Both source clips are identical in format:

| Property | Value |
|---|---|
| Resolution | 1918 x 1080 |
| Frame rate | 24 fps |
| Duration | 5.042 s (121 frames) |
| Video codec | h264 |
| Audio codec | aac (discarded — see Audio) |

- Intro: `C:\Users\NTP\Downloads\b2d52762-3aa6-42a4-8e30-ca1d8d6830b4.mp4`
- Idle loop: `C:\Users\NTP\Downloads\c2288469-5694-438a-b88a-89109b82c3b6.mp4`

## Key Constraint

Unity's `VideoPlayer` does not support reverse playback. `playbackSpeed` is
clamped to a non-negative range on effectively every platform, and stepping
backwards by seeking frame-by-frame stutters badly. Reversed footage must
therefore be baked ahead of time.

## Approach

Bake **reversed copies only** with ffmpeg; do all speed ramping at runtime.
This keeps the ramp tunable without re-encoding, at the cost of two extra
video assets.

Rejected alternatives:
- Bake ramp + reverse into two finished mp4s (zero runtime logic, but every
  timing tweak needs a re-encode).
- Pure runtime frame-seeking (no ffmpeg dependency, but visibly janky).

## Assets

ffmpeg (installed via `winget install Gyan.FFmpeg`) produces four clips in
`Assets/Video/`:

| File | Source | ffmpeg |
|---|---|---|
| `intro.mp4` | b2d52762 | `-c:v copy -an` |
| `intro_rev.mp4` | b2d52762 | `-vf reverse -an` |
| `idle.mp4` | c2288469 | `-c:v copy -an` |
| `idle_rev.mp4` | c2288469 | `-vf reverse -an` |

`-an` strips audio (see Audio). Baking is a one-time manual step; the resulting
mp4s are committed as project assets.

## Scene Structure

```
first_page
├── Main Camera
├── Canvas (Screen Space - Overlay)
│   ├── VideoSurface (RawImage, stretched full-screen)
│   │   └── AspectRatioFitter — mode: EnvelopeParent   ← "cover", crops overflow
│   └── PressToStart (TMP_Text, CanvasGroup alpha 0)
│       "กดเพื่อเข้าเกม", font LeelawUI SDF, centred, lower third
└── FirstPageIntro (VideoPlayer x2 + FirstPageIntro.cs)
```

- A single `RenderTexture` (1920x1080) is the render target of both VideoPlayers
  and the texture of the RawImage.
- **Two VideoPlayers** alternate: while one plays, the other `Prepare()`s the
  next clip, so clip swaps have no black frame.
- `AspectRatioFitter` in `EnvelopeParent` mode gives cover-fit behaviour with no
  custom code.

## Runtime Behaviour — `FirstPageIntro.cs`

Single MonoBehaviour, one file, a three-phase state machine.

| Phase | Clip | playbackSpeed | Input |
|---|---|---|---|
| 1. IntroForward | `intro` | `Lerp(1, 2, time/length)` | ignored |
| 2. IntroReverse | `intro_rev` | `Lerp(2, 1, time/length)` | ignored |
| 3. Idle (repeats forever) | `idle` → `idle_rev` → repeat | `1` | accepted |

- Speed is updated every frame from the active player's normalized `time`.
- Because of the ramp, each intro pass takes `5 * ln(2)` ≈ 3.5 s, so the intro
  runs ~7 s total rather than 10 s.
- Phase transitions fire on `VideoPlayer.loopPointReached`.
- On entering phase 3, the `PressToStart` CanvasGroup fades 0→1 over 0.6 s and
  input becomes live.
- Input = any key, mouse click, or touch. On input: start
  `SceneFadeController.Cover(0.6f)`, then `SceneManager.LoadScene("WorldMap")`.
  Input is latched so a second press cannot double-load.

## Audio

All audio is stripped at bake time (`-an`) and both VideoPlayers use
`AudioOutputMode.None`. Reversed and pitch-shifted source audio sounds wrong,
and a silent title screen leaves room for a proper BGM track later.

## Build Settings

`Assets/Scenes/first_page.unity` is added to Build Settings as scene index 0,
displacing `Login.unity`. Login / auto-login UI is explicitly out of scope for
this spec and will be layered onto this screen in follow-up work.

## Out of Scope

- Login / signup / auto-login UI and its routing (next task).
- BGM or SFX.
- Settings, credits, or any other title-screen menu item.

## Verification

An editor menu item `Tools/FirstPage/Validate` asserts:

1. All four clips exist in `Assets/Video/` and import as `VideoClip`.
2. `intro.mp4` and `intro_rev.mp4` have equal frame counts; likewise the idle
   pair. (A mismatch means the reverse bake failed or was re-run on a stale
   source.)
3. The scene's `FirstPageIntro` component has all four clip references, both
   VideoPlayers, the RenderTexture, and the text CanvasGroup wired.
4. `first_page` is present and enabled in Build Settings.

Manual check: enter Play Mode on `first_page`, confirm the intro accelerates
forward, decelerates backward, the text fades in, and a click fades to WorldMap.
