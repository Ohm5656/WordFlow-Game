# Music — two-track BGM (IMPLEMENTED)

All code is done in `Assets/Scripts/Common/GameAudio.cs`,
`Assets/Editor/BuildGameAudioPrefab.cs`, and `Assets/Resources/GameAudio.prefab`.
What is left is supplying the two audio files.

---

## 1. What you still have to do

Put two music files in `Assets/Audio/` with these exact names:

| Slot | File |
|------|------|
| Main / world theme | `Assets/Audio/main_theme.ogg` |
| Quest theme | `Assets/Audio/quest_theme.ogg` (already trimmed to the excerpt you want) |

Then in Unity: **Tools → Audio → Wire Music Tracks**. That only touches the two music
slots on the prefab; every other clip and hand-tuned value stays as it is.

(You can also just select `Assets/Resources/GameAudio.prefab` and drag the clips onto
the `Main Theme` / `Quest Theme` fields — same result.)

Until the files exist, the game runs fine with no music at all — no errors, no spam.

Recommended import settings for both clips (Inspector, then Apply):
`Load Type = Streaming`, `Compression Format = Vorbis`, `Preload Audio Data = off`.

`Golden Gleam.ogg` is left in `Assets/Audio/` untouched as a fallback. **No SFX were
removed** — only the old single-BGM wiring.

---

## 2. Scene → track mapping

Main theme (`mainThemeScenes` on the prefab, editable in the Inspector):

```
first_page, Login, ForgotPassword, Register, WorldMap, reference_forest
```

WorldMap covers day *and* night — night is a lighting mode inside the same scene.

Quest theme: **everything else** — `quest_map1`, `word_build_*`, `Success_*`, `success`,
`CutScene_*`, `cut_scene2/3`, `boss`, `before_Boss`, `practice_night`.

To move a scene between tracks, edit the `Main Theme Scenes` list on the prefab.
Nothing else needs to change.

---

## 3. Inspector knobs (`Assets/Resources/GameAudio.prefab`)

| Field | Default | What it does |
|-------|---------|--------------|
| `Main Theme Volume` | 0.85 | loudness of the world theme — **live-adjustable in Play mode** |
| `Quest Theme Volume` | 0.85 | loudness of the quest theme — live-adjustable |
| `Crossfade Seconds` | 2.0 | overlap at the loop seam (end blends into the restart) |
| `Track Switch Fade Seconds` | 1.2 | fade-out/fade-in when the scene changes track |
| `Music Fade In Seconds` | 3.2 | first fade-in of the session |
| `Voice Duck` | 0.2 | how far music drops while the owl speaks |
| `Duck Lerp Speed` | 2.5 | how fast that duck moves |

The old `otherSceneDuck` (a hidden ×0.7 outside WorldMap) is gone — it made the volume
sliders lie. Each track's volume is now the single source of truth for its loudness.

---

## 4. How it behaves

- **Same track across scenes** → keeps playing, no restart, no re-fade.
- **Different track** → old one fades out over `Track Switch Fade Seconds`, its playhead
  is saved, the new one fades in **from wherever it left off** (0 the first time).
- **Loop** → two `AudioSource`s on the DSP clock; the second starts
  `Crossfade Seconds` before the first ends and they crossfade. No gap, no click.
- **Owl speaking** → music ducks to `Voice Duck` and comes back smoothly.
- **Login now has music** (the old code deliberately skipped that scene).

---

## 5. Verification — run these in Play mode once the clips are in

1. **Login → Register → ForgotPassword → first_page** — main theme plays continuously,
   no restart, no gap, no click at any load.
2. **WorldMap** — same theme, still continuous. Toggle night — unaffected.
3. **WorldMap → quest_map1** — main theme fades out (~1.2 s), quest theme fades in.
4. **quest_map1 → CutScene_bear → Success_ga** — quest theme keeps playing
   uninterrupted across all three (same track).
5. **Resume test (the important one):** note where the quest theme is, go to `WorldMap`
   (main theme resumes where *it* left off), come back to `quest_map1` — the quest theme
   must continue from roughly where it stopped, not from the top.
6. **Loop seam:** sit in `WorldMap` for the full length of the track and listen at the
   wrap — smooth crossfade, no silence, no double-hit.
7. **Owl duck:** `reference_forest` bear intro / `Success_pa` epilogue — music dips under
   the voice and recovers.
8. **Live volume:** in Play mode, select `GameAudio` in the Hierarchy and drag
   `Main Theme Volume` — loudness responds immediately.

If step 5 restarts from 0 instead of resuming, the likely cause is the resume clamp in
`GameAudio.StartTrack` — a playhead within `crossfadeSeconds` of the end is deliberately
reset to 0 because the loop scheduling has no room left.

---

## 6. Not done (deliberately)

- No settings-menu volume slider — Inspector control only, as asked.
- No `AudioMixer` groups. Two `AudioSource`s and a float already do the job.
- Resume is **in-session only**; it does not survive quitting the game.
