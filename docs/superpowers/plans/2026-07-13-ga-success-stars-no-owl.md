# Ga Success Stars And No-Owl Wrong Result

## Status And Intent

Implemented on 2026-07-13 after user approval. The result-only migration command was used so this
follow-up changed only the ga result scenes and their editor tooling.

This plan follows up `2026-07-13-cutscene-ga-parity.md` and supersedes only that plan's decision
to preserve the owl in `Success_ta_incorrect`. All previously approved `CutScene_ga` puzzle,
`Thow`, stone-crow, timer, smoke, lock, word-routing, and TTS-letter decisions remain unchanged.

## Final User-Visible Behaviour

### Correct word: `กา`

1. `CutScene_ga` awards and persists the normal stars before loading the result scene:
   - star 1 for assembling a valid word;
   - star 2 for the correct target after successful recording;
   - star 3 when the correct target was assembled before time expired.
2. `Success_ga_correct` shows a recap of the persisted 2 or 3 stars.
3. Each recapped star plays the existing `Assets/Audio/star.mp3` sound.
4. The stars stay visible, matching the successful `Success_pa` behaviour.
5. Preserve the existing crow-release sequence and sounds:
   - `GameAudio.PlayRockBreak()` at the first release animation;
   - `GameAudio.PlayCrowLoop()` for the flying sequence;
   - the existing owl line `kaa_voice_restored`;
   - `GameAudio.PlayWin()` when the owl epilogue begins;
   - the existing return to `reference_forest` at beat 3.

### Real but wrong word: `ปา`

1. `CutScene_ga` still awards exactly one valid-word star and routes to the legacy-named
   `Success_ta_incorrect` scene.
2. `Success_ta_incorrect` keeps the approved standalone `Thow` animation and petrified crow.
3. `paa.wav` plays once when `Thow` starts through `playPaaThrowSfxOnStart = true`.
4. The persisted one star appears after the standard 0.6-second recap delay and plays
   `Assets/Audio/star.mp3` once.
5. The one star holds, then fades away in wrong-result mode. The fade plays
   `Assets/Audio/lose.wav` once.
6. There is no owl, owl greeting, owl encouragement, owl TTS request, or second lose sound in
   `Success_ta_incorrect`.
7. After `Thow` finishes, `Thow` and the stone crow fade out together. The scene sets both puzzle
   retry flags and returns to `CutScene_ga` as before.

## Timing Contract

Do not add a new timing controller. Existing timings already leave enough room for the star recap.

- `Thow.anim` is non-looping and has `m_StopTime: 4.0333333` at 30 fps.
- `StarHud` wrong-result recap for one star takes about 2.8 seconds from scene load:
  - `recapStartDelay`: 0.60 seconds;
  - one `landPopDuration`: 0.25 seconds;
  - `recapGap`: 0.15 seconds;
  - `recapHoldBeforeFade`: 1.20 seconds;
  - `recapFadeDuration`: 0.60 seconds.
- `BearCutscene` then applies the existing 0.60-second end fade after the 4.03-second clip.
- Expected wrong-result transition time is approximately 4.63 seconds after scene start.
- The star and its lose sound therefore finish before the scene transition begins.

Do not add `SuccessGaReturn` to `Success_ta_incorrect`. Its fixed timer and crow-loop sound are
unnecessary here and would create competing transition ownership.

## Existing Systems To Reuse

Use the existing systems exactly as authored:

- `Assets/Prefabs/StarHud.prefab`
- `Assets/Scripts/Region1/Stars/StarHud.cs`
- `Assets/Scripts/Region1/Cutscenes/BearCutscene.cs`
- `Assets/Scripts/Common/GameAudio.cs`
- `Assets/Editor/AddStarHudToSuccessScenes.cs`
- `Assets/Editor/UpgradeCutSceneGaParity.cs`
- `Assets/Editor/BuildSuccessScenes.cs`
- `Assets/Editor/BuildTaIncorrectStoneCrow.cs`

Do not modify `StarHud`, `BearCutscene`, `GameAudio`, `MagicStonePuzzleController`,
`CrowSetFreeCutscene`, or `SuccessPaOwlEpilogue`. The required behaviour is achievable through
scene wiring and editor builders only.

## Files Allowed To Change

Implementation should be limited to these files unless Unity creates the matching `.meta` for a
new asset:

1. `Assets/Editor/AddStarHudToSuccessScenes.cs`
2. `Assets/Editor/UpgradeCutSceneGaParity.cs`
3. `Assets/Editor/BuildSuccessScenes.cs`
4. `Assets/Editor/BuildTaIncorrectStoneCrow.cs`
5. `Assets/Scenes/region 1/Success_ga_correct.unity`
6. `Assets/Scenes/region 1/Success_ta_incorrect.unity`
7. This plan and the superseding note in the earlier plan

Do not modify `CutScene_ga.unity`, either old success scene (`Success_pa`, `Success_ga`), shared
audio assets, animation clips/controllers, materials, prefabs, correct-result logic, or build
settings unless verification proves a real blocker and the user approves that additional scope.

## Implementation Steps

### 1. Establish A Clean Baseline

1. Read `git status -sb` and preserve all current uncommitted work.
2. Confirm Unity is not compiling, not in Play Mode, and has no dirty unsaved scene.
3. Validate the current `Success_ga_correct` and `Success_ta_incorrect` scenes before mutation.
4. Record the pre-change hierarchy and component counts for both scenes.
5. Do not revert, overwrite, or reformat unrelated changes.

### 2. Extend The Canonical Success-Star Editor Tool

Edit `AddStarHudToSuccessScenes.cs` so its existing idempotent installer can also target the two new
result scenes without changing or reopening the two old scenes during this task.

Required scene modes:

| Scene | `resetOnAwake` | `recapOnStart` | `fadeOutAfterRecap` | Meaning |
| --- | ---: | ---: | ---: | --- |
| `Success_pa` | false | true | false | Existing correct-result recap |
| `Success_ga` | false | true | true | Existing wrong-result recap/reset |
| `Success_ga_correct` | false | true | false | New correct `กา` recap |
| `Success_ta_incorrect` | false | true | true | New wrong `ปา` recap/reset |

Implementation constraints:

1. Continue using `Assets/Prefabs/StarHud.prefab` and the existing authored references.
2. Keep installation idempotent by deleting only an existing direct child named `star_hud` under
   the target Canvas before instantiating one replacement.
3. Place `star_hud` at the final Canvas sibling index so it renders over result art.
4. Do not reset `StarEarnedCount` in a result scene.
5. Preserve `starEarnedSfx = Assets/Audio/star.mp3`.
6. Preserve `fadeOutSfx = Assets/Audio/lose.wav` for both wrong-result scenes. In the new wrong
   scene this is the only lose sound because the owl is removed.
7. Keep the existing menu command's `Success_pa` and `Success_ga` behaviour unchanged.
8. Add a separate result-only entry point such as `InstallGaFlow()` and a menu item such as
   `Tools/Quest/Add Star Hud To Ga Success Scenes`. It must install only `Success_ga_correct` and
   `Success_ta_incorrect`.
9. Expose only the minimum internal helper needed by the Ga success builders. Do not duplicate the
   prefab-instantiation logic in several files.
10. Log each scene path and its recap/fade mode after saving.

### 3. Remove The Owl From The New Wrong-Result Builder

Refactor `UpgradeCutSceneGaParity.WireThowIncorrectScene` to own the final no-owl setup.

Required hierarchy after wiring `Success_ta_incorrect`:

- `Canvas/background`
- `Canvas/Crow`
- `Canvas/Thow`
- `Canvas/star_hud`
- no `Canvas/OwlRoot`
- no `Canvas/OwlEpilogue`
- no `SuccessPaOwlEpilogue` component anywhere in the scene
- no `SuccessGaReturn` component anywhere in the scene
- no `Eye`, `Bear`, `ga_left`, or `ga_right` object

Required `BearCutscene` fields on `Canvas/Thow`:

| Field | Required value |
| --- | --- |
| `bear` | Animator on `Thow` |
| `playOnStart` | true |
| `playPaaThrowSfxOnStart` | true |
| `sequence` | one PlayClip step with state `Thow` |
| `startWaypoint` | -1 |
| `fadeInDuration` | 0 |
| `alsoFade` | one entry: petrified crow `CanvasGroup` |
| `endFadeOutDuration` | 0.6 |
| `owlEpilogue` | null |
| `nextScene` | `CutScene_ga` |
| `setPuzzleRetryFlags` | true |
| `resumeForestAtBeat2` | false |

Inside the builder:

1. Destroy old direct children named `Bear`, `Eye`, `Thow`, `ga_left`, and `ga_right` before
   rebuilding the approved objects.
2. Destroy `OwlRoot` and `OwlEpilogue` direct children.
3. Remove any scene-local `SuccessPaOwlEpilogue` and `SuccessGaReturn` components that survive
   under another object.
4. Keep `EnsureStoneCrow` behaviour unchanged: frozen final `ga_stone` frame, animator and entrance
   disabled, alpha 1, raycasts off, and included in `alsoFade`.
5. Keep the approved `Thow.prefab`, 1920x1080 RectTransform, centered position, scale `(2,2,1)`,
   `ThowController`, and non-raycast Image.
6. Remove the `SuccessPaOwlEpilogue` parameter from `WireThowIncorrectScene`; it must no longer be
   possible for a caller to wire the owl back accidentally.
7. Delete the now-unused `ConfigureEncouragement` helper from this editor tool.

### 4. Prevent Legacy Builders From Restoring The Owl

Update all callers of `WireThowIncorrectScene`.

In `BuildSuccessScenes.cs`:

1. Keep `WireGaCorrect` behaviour unchanged.
2. Remove the temporary owl-epilogue prefab workflow used only to clone the correct owl into the
   wrong scene:
   - remove `TempEpiloguePrefab`;
   - stop snapshotting the correct scene's epilogue;
   - stop cloning/configuring an epilogue in `WireTaIncorrect`;
   - stop deleting the temporary prefab at the end.
3. Call the no-owl `WireThowIncorrectScene(canvas.transform)` overload.
4. Update comments and logs from “owl retry return” to “one-star no-owl retry return”.
5. Do not alter the existing correct crow-release animation/controller creation.

In `BuildTaIncorrectStoneCrow.cs`:

1. Stop searching for `SuccessPaOwlEpilogue`.
2. Call `WireThowIncorrectScene(canvas.transform)`.
3. Update comments and logs to describe `Thow + stone crow + one-star no-owl return`.

### 5. Add A Result-Only Migration Command

The existing `Tools/Crow/Upgrade CutScene_ga Parity` command also rewrites `CutScene_ga`. Do not run
that full command for this follow-up task.

In `UpgradeCutSceneGaParity.cs`:

1. Add a separate menu command such as `Tools/Crow/Upgrade Ga Success Results`.
2. The new command must perform the clean-scene guard, rebuild only `Success_ta_incorrect`, install
   stars into only `Success_ga_correct` and `Success_ta_incorrect`, save assets, and log completion.
3. Refactor the existing full command to reuse the same internal result-migration method so the
   full command remains durable, but do not execute the full command during this task.
4. Do not call `UpgradePuzzleScene()` from the result-only command.
5. The result-only command must not open, save, or dirty `CutScene_ga`, `Success_pa`, or
   `Success_ga`.

### 6. Apply The Scene Migration In Unity

After the editor scripts compile successfully:

1. Run only the new `Tools/Crow/Upgrade Ga Success Results` command.
2. Do not run `Tools/Crow/Upgrade CutScene_ga Parity`.
3. Do not run the old all-purpose `Tools/Quest/Add Star Hud To Success Scenes` command.
4. The result-only command should invoke the new Ga-flow star installer internally so one command
   produces both final scenes.
5. Save scenes through Unity APIs only. Do not manually edit Unity YAML.
6. Confirm `CutScene_ga`, `Success_pa`, and `Success_ga` have no new diff. If one changes
   unexpectedly, stop and inspect before proceeding; do not hide the change in a broad rewrite.

### 7. Verify Correct-Result Behaviour

For `Success_ga_correct` verify:

1. Exactly one `star_hud` and one `StarHud` component.
2. `resetOnAwake = false`, `recapOnStart = true`, `fadeOutAfterRecap = false`.
3. `starEarnedSfx` references `Assets/Audio/star.mp3`.
4. No `fadeOutSfx` is played because fade mode is false.
5. Existing `CrowSetFreeCutscene` fields and references are unchanged.
6. Existing rock-break, crow-loop, `kaa_voice_restored`, win sting, and forest beat-3 transition
   remain intact.
7. With `StarEarnedCount = 2`, exactly two stars recap; with count 3, exactly three recap.

### 8. Verify Wrong-Result Behaviour

For `Success_ta_incorrect` verify:

1. Exactly one `Thow`, one petrified `Crow`, one `star_hud`, and one `BearCutscene`.
2. Zero `OwlRoot`, `OwlEpilogue`, `SuccessPaOwlEpilogue`, `Eye`, `Bear`, and `SuccessGaReturn`.
3. Star mode is `resetOnAwake = false`, `recapOnStart = true`,
   `fadeOutAfterRecap = true`.
4. `starEarnedSfx` references `star.mp3`; `fadeOutSfx` references `lose.wav`.
5. With `StarEarnedCount = 1`, one star appears and one star sound plays.
6. The star fades once and `lose.wav` plays exactly once.
7. `paa.wav` plays exactly once at `Thow` start.
8. No owl TTS request, owl animation, win sting, or `GameAudio.PlayLose()` call occurs.
9. `Thow` completes before its 0.6-second end fade starts.
10. The crow fades with `Thow` through `alsoFade`.
11. Both retry flags are set and `CutScene_ga` loads once.
12. Returning to `CutScene_ga` clears the old star through its existing `resetOnAwake = true` and
    resumes the established retry puzzle flow.

### 9. Final Safety Checks

1. Unity compiles with zero errors.
2. Validate both modified scenes with `manage_scene(action="validate", auto_repair=false)`.
3. Confirm both scenes remain enabled in Build Settings.
4. Run a Play Mode pass through both `กา` and `ปา` branches.
5. Watch the Console for missing references, duplicate AudioListeners, missing TTS lines, scene-load
   errors, or duplicate transition requests.
6. Check `git status -sb`, `git diff --name-only`, and focused diffs.
7. Confirm no shared runtime script, audio asset, animation asset, old result scene, or
   `CutScene_ga.unity` changed.
8. Do not commit or push unless the user explicitly requests it after verification.

## Acceptance Checklist

- [x] `Success_ga_correct` recaps 2-3 stars with `star.mp3` and keeps them visible.
- [x] Correct crow release, crow audio, owl praise, win sting, and forest transition are unchanged.
- [x] `Success_ta_incorrect` shows `Thow`, petrified crow, and exactly one recapped star.
- [x] Wrong-result audio order is `paa.wav` -> `star.mp3` -> one `lose.wav`.
- [x] The wrong-result star fades before `Thow` completes.
- [x] No owl object, owl TTS, owl encouragement, or owl-driven sting remains in the wrong scene.
- [x] `Thow` and crow fade together, then return once to `CutScene_ga` in retry mode.
- [x] Rerunning either relevant editor builder does not restore the owl or duplicate the star HUD.
- [x] The old `Success_pa` and `Success_ga` behaviours remain unchanged.
- [x] Unity reports zero compile errors, missing scripts, and broken prefab references.
