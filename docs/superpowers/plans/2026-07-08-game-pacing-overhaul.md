# Game Pacing Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut hands-off waiting across the whole game so a young, short-attention-span player reaches interaction fast — CutScene_bear from ~27s to ≤13s before the first stone tap, and every ceremony (unlocks, transitions, success screens) roughly halved.

**Architecture:** Three levers, no content cuts and no skip button (per user decision): (1) trim dead silence from all baked TTS wavs — the game holds on `clip.length` everywhere, so every trimmed second is a free win; (2) restructure `OwlGreetingCutscene` coroutines so transitions run in parallel instead of serially; (3) one editor menu script that stamps aggressive timing values into every scene's serialized component fields (scene values override code defaults, so hand-editing defaults is not enough).

**Tech Stack:** Unity 6 (6000.4.3f1), C# coroutines, Python stdlib `wave` for audio trim, Unity Editor scripts (`SerializedObject`) for scene value stamping.

## Global Constraints

- No skip/tap-to-advance system — user explicitly declined. Speed comes from trims + parallelization only.
- Never cut teaching audio content: phoneme placement clips, word echoes, and owl lines all still play in full (minus dead silence).
- `blockPlacementWhileVoicePlaying` stays `true` (pedagogy gate); it becomes fast because the clips get shorter.
- Scene-serialized values override C# defaults — all per-scene timing changes go through the `ApplyFastPacing` editor script (Task 4/7), not hand edits of `.unity` files.
- Editor scene edits must be atomic: modify + `EditorSceneManager.SaveScene` in one editor-script run (project convention; scene edits done piecemeal have vanished on domain reload before).
- After every C# change, verify compilation via Unity console / `unity_get_compilation_errors` before running any menu item.
- The Unity MCP bridge port hops between 7890/7891 after domain reloads — re-run `unity_list_instances` after any recompile.
- `WordAssemblyTimer` (the 30s countdown, added 2026-07-08) must keep working: `BeginFresh()` at book pop, `Pause()` at word result, `Resume()` on retry. Do not move those call sites.
- Commit after each task with the message given in the task.

## Measured baseline (why these tasks exist)

| Segment | Today | Target |
|---|---|---|
| CutScene_bear: load → first stone tap | ~27s | ≤13s |
| — of which actual owl voice | ~12.3s | ~9.3s (silence-trimmed) |
| Word built → mic button usable | ~6s (incl. 4.85s echo that is ~2.6s silence) | ~2.5s |
| Mic stop → next scene visible | ~4.8s (2s shake + flash) | ~2.2s |
| Every TTS wav | 0.45–1.40s dead silence each (`gameplay_aa`: 1.40s of 2.04s) | ≤0.16s pad total |
| reference_forest scripted walk | ~30s+ (questAutoHold 1.5×5, faceHold 2, moveSpeed 1) | ~60% of today |

---

## Phase 1 — Core loop (CutScene_bear/ga/ta + Success_*)

### Task 1: Trim silence from all baked TTS wavs

**Files:**
- Create: `Assets/Editor/tools/trim_tts_silence.py` (checked in so it can be re-run when new lines are baked)
- Modify (in place): all 20 `Assets/Resources/TTS/*.wav`

**Interfaces:**
- Produces: same wav filenames, same format (24kHz mono 16-bit — `MagicStonePuzzleController.ConcatClips` requires all clips share one format), shorter durations. No code consumes anything new.

- [ ] **Step 1: Write the trim script**

```python
# Assets/Editor/tools/trim_tts_silence.py
# Trims leading/trailing silence from every baked TTS wav, keeping an 80ms pad.
# The game holds on clip.length everywhere, so trimmed silence = faster gameplay.
# Re-run whenever new lines are baked into Resources/TTS.
import wave, os, struct, contextlib

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Resources", "TTS")
THRESHOLD = 0.02   # 2% of peak amplitude counts as sound
PAD_S = 0.08       # keep 80ms of natural silence on each side

for f in sorted(os.listdir(ROOT)):
    if not f.lower().endswith(".wav"):
        continue
    p = os.path.join(ROOT, f)
    with contextlib.closing(wave.open(p)) as w:
        n, sr, ch, sw = w.getnframes(), w.getframerate(), w.getnchannels(), w.getsampwidth()
        raw = w.readframes(n)
    assert sw == 2, f"{f}: expected 16-bit"
    samples = struct.unpack(f"<{n*ch}h", raw)
    peak = max(1, max(abs(s) for s in samples))
    th = peak * THRESHOLD
    first = next((i for i in range(0, len(samples), ch) if abs(samples[i]) > th), 0) // ch
    last = next((i for i in range(len(samples)-ch, -1, -ch) if abs(samples[i]) > th), len(samples)-1) // ch
    pad = int(PAD_S * sr)
    start = max(0, first - pad)
    end = min(n, last + 1 + pad)
    if start == 0 and end == n:
        print(f"skip  {f}")
        continue
    body = raw[start*ch*2 : end*ch*2]
    with wave.open(p, "w") as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(body)
    print(f"trim  {f}: {n/sr:.2f}s -> {(end-start)/sr:.2f}s")
```

- [ ] **Step 2: Run it and verify output**

Run: `python Assets/Editor/tools/trim_tts_silence.py`
Expected: `trim` lines for all 20 files; e.g. `gameplay_aa.wav: 2.04s -> ~0.80s`, `paa_intro_owl_1a.wav: 1.84s -> ~1.30s`. No file should print `skip`.

- [ ] **Step 3: Reimport in Unity and listen-check one line**

In Unity: Assets → Refresh (or via MCP `Assets/Refresh` menu item). Select `Assets/Resources/TTS/gameplay_paa.wav` in the Project window and play it in the Inspector — the word must be intact, no clipped consonant at the start.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/tools/trim_tts_silence.py Assets/Resources/TTS
git commit -m "perf(audio): trim dead silence from baked TTS wavs (~0.5-1.4s each)"
```

### Task 2: Parallelize OwlGreetingCutscene transitions

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/OwlGreetingCutscene.cs`

**Interfaces:**
- Consumes: nothing new. Public API (`PlayGreeting()`) unchanged.
- Produces: same coroutine flow, but dim/zoom/fades overlap instead of running serially. Downstream tasks rely on the method names staying as-is: `GreetingRoutine`, `BearFocusFollowUpRoutine`, `FadeOwlOutBeforeBookRevealRoutine`, `PlayBookRevealRoutine`.

All four edits below are in this one file. The `WordAssemblyTimer.Instance?.BeginFresh()` line already present in `GreetingRoutine` must stay exactly where it is.

- [ ] **Step 1: Overlap dim + legacy owl fade with the zoom (in `GreetingRoutine`)**

Replace:

```csharp
        if (dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(true, GetOwlOnlyDimSiblingIndex());
        }

        if (!UsesTalkingPrefabAnimator)
        {
            yield return FadeOwlInRoutine();
        }

        if (zoomBeforeGreeting && zoomDuration > 0f)
        {
            yield return ZoomInRoutine();
        }
```

with:

```csharp
        // Pacing: dim, legacy owl fade-in and zoom all run together (they used to run serially).
        Coroutine dimIn = null;
        if (dimBackgroundBeforeZoom)
        {
            dimIn = StartCoroutine(FadeDimOverlay(true, GetOwlOnlyDimSiblingIndex()));
        }

        if (!UsesTalkingPrefabAnimator)
        {
            StartCoroutine(FadeOwlInRoutine());
        }

        if (zoomBeforeGreeting && zoomDuration > 0f)
        {
            yield return ZoomInRoutine();
        }

        if (dimIn != null)
        {
            yield return dimIn; // zoom is usually the longer of the two; this is a no-op then
        }
```

- [ ] **Step 2: Overlap the post-greeting dim-out with the zoom restore (in `GreetingRoutine`)**

Replace:

```csharp
        if (restoreZoomAfterGreeting && hasZoomState)
        {
            yield return RestoreZoomRoutine();
        }

        if (dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(false, -1);
        }
```

with:

```csharp
        Coroutine dimOut = null;
        if (dimBackgroundBeforeZoom)
        {
            dimOut = StartCoroutine(FadeDimOverlay(false, -1));
        }

        if (restoreZoomAfterGreeting && hasZoomState)
        {
            yield return RestoreZoomRoutine();
        }

        if (dimOut != null)
        {
            yield return dimOut;
        }
```

- [ ] **Step 3: Overlap the book reveal with the owl fade-out (in `GreetingRoutine`)**

Replace:

```csharp
        if (playBookRevealAfterBearFocus)
        {
            yield return FadeOwlOutBeforeBookRevealRoutine();
            WordAssemblyTimer.Instance?.BeginFresh(); // start the 30s clock as the craft book pops
            yield return PlayBookRevealRoutine();
        }
```

with:

```csharp
        if (playBookRevealAfterBearFocus)
        {
            // Pacing: the owl fades while the book flies in — no dead frame between them.
            StartCoroutine(FadeOwlOutBeforeBookRevealRoutine());
            WordAssemblyTimer.Instance?.BeginFresh(); // start the 30s clock as the craft book pops
            yield return PlayBookRevealRoutine();
        }
```

- [ ] **Step 4: Overlap bear-focus dim transitions with the bear grow/shrink (in `BearFocusFollowUpRoutine`)**

Replace:

```csharp
        if (dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(true, GetBearAndOwlDimSiblingIndex(targetBearRoot));
        }

        Vector3 bearStartScale = targetBearRoot.localScale;
```

with:

```csharp
        if (dimBackgroundBeforeZoom)
        {
            StartCoroutine(FadeDimOverlay(true, GetBearAndOwlDimSiblingIndex(targetBearRoot)));
        }

        Vector3 bearStartScale = targetBearRoot.localScale;
```

and, at the end of the same method, replace:

```csharp
        if (restoreBearScaleAfterFocus)
        {
            yield return ScaleRectTransform(targetBearRoot, bearTargetScale, bearStartScale, bearGrowDuration);
        }

        if (restoreBackgroundAfterBearFocus && dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(false, -1);
        }
```

with:

```csharp
        if (restoreBackgroundAfterBearFocus && dimBackgroundBeforeZoom)
        {
            StartCoroutine(FadeDimOverlay(false, -1));
        }

        if (restoreBearScaleAfterFocus)
        {
            yield return ScaleRectTransform(targetBearRoot, bearTargetScale, bearStartScale, bearGrowDuration);
        }
```

- [ ] **Step 5: Verify compilation**

Check Unity console / `unity_get_compilation_errors`: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/OwlGreetingCutscene.cs
git commit -m "perf(cutscene): run owl cutscene transitions in parallel instead of serially"
```

### Task 3: Trim hardcoded ceremony literals + add pacing log

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`

**Interfaces:**
- Produces: a `[Pacing] stones interactive at X.Xs` console log used by verification Tasks 5 and 8.

- [ ] **Step 1: Shorten the recording-success sfx hold cap**

In `ShowRecordingSuccessMarkRoutine`, replace:

```csharp
            holdDuration = Mathf.Max(holdDuration, Mathf.Min(recordingSuccessSfx.length, 1.1f));
```

with:

```csharp
            holdDuration = Mathf.Max(holdDuration, Mathf.Min(recordingSuccessSfx.length, 0.6f));
```

- [ ] **Step 2: Add the time-to-interactive log**

In `PlayIntroReveal`, replace:

```csharp
        _latency.Start(Time.realtimeSinceStartupAsDouble);
        revealFinished = true;
```

with:

```csharp
        _latency.Start(Time.realtimeSinceStartupAsDouble);
        revealFinished = true;
        Debug.Log($"[Pacing] stones interactive at {Time.timeSinceLevelLoad:0.0}s");
```

- [ ] **Step 3: Verify compilation**

Unity console: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs
git commit -m "perf(puzzle): shorten success-sfx hold cap, log time-to-interactive"
```

### Task 4: ApplyFastPacing editor script — stamp core-loop scene values

**Files:**
- Create: `Assets/Editor/ApplyFastPacing.cs`

**Interfaces:**
- Produces: menu item `Tools/Pacing/Apply Fast Pacing (Core Loop)` and `Tools/Pacing/Apply Fast Pacing (World)` (world list filled in Task 7 — the scaffolding for both lives here so Task 7 only appends table rows).
- Sets serialized fields on **every instance** of a component type in each scene (CutScene_* scenes have 4-5 `MagicStonePuzzleController` instances that must all match).

- [ ] **Step 1: Write the editor script**

```csharp
// Assets/Editor/ApplyFastPacing.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stamps the fast-pacing timing values into scene-serialized component fields.
/// Scene values override C# defaults, so this (not code-default edits) is how the
/// game actually speeds up. Idempotent — safe to re-run after any scene edit.
/// </summary>
public static class ApplyFastPacing
{
    // component type name -> (field, value)
    private static readonly Dictionary<string, (string field, float value)[]> CoreValues =
        new Dictionary<string, (string, float)[]>
    {
        ["OwlGreetingCutscene"] = new (string, float)[]
        {
            ("startDelay", 0.05f),
            ("dimFadeInDuration", 0.2f),
            ("dimFadeOutDuration", 0.25f),
            ("zoomDuration", 0.4f),
            ("holdAfterZoom", 0f),
            ("restoreZoomDuration", 0.35f),
            ("talkingFadeDuration", 0.2f),
            ("holdFrozenAfterRound", 0.15f),
            ("bearFocusDelay", 0f),
            ("bearGrowDuration", 0.3f),
            ("bookRevealDelay", 0.1f),
            ("bookRevealDuration", 0.7f),
            ("owlFadeOutBeforeBookDuration", 0.35f),
        },
        ["OwlHelloSequence"] = new (string, float)[]
        {
            ("playbackSpeed", 1.8f),
            ("holdAfterPlay", 0.1f),
            ("zoomInDuration", 0.15f),
            ("zoomOutDuration", 0.15f),
        },
        ["MagicStonePuzzleController"] = new (string, float)[]
        {
            ("revealDuration", 0.3f),
            ("revealDelayBetweenStones", 0.08f),
            ("craftResultFadeDuration", 0.25f),
            ("crowCraftHoldDuration", 0.2f),
            ("actionIconFadeDelay", 0f),
            ("actionIconFadeDuration", 0.2f),
            ("ritualShakeDuration", 0.8f),
            ("whiteFlashFadeInDuration", 0.18f),
            ("whiteFlashHoldDuration", 0.1f),
            ("whiteFlashFadeOutDuration", 0.45f),
            ("recordingSuccessPopDuration", 0.3f),
            ("recordingSuccessHoldDuration", 0.25f),
        },
        ["SuccessPaOwlEpilogue"] = new (string, float)[]
        {
            ("fallbackHoldSeconds", 2f),
            ("holdFrozenAfterRound", 0.15f),
        },
        ["SuccessGaReturn"] = new (string, float)[]
        {
            ("playSeconds", 4f), // ga clip is ~4s; 4.5 held on 0.5s of nothing
        },
        ["CrowEntranceCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.25f),
            ("leg1Duration", 2f),
            ("leg2Duration", 1.6f),
        },
    };

    private static readonly string[] CoreScenes =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity",
        "Assets/Scenes/region 1/CutScene_ga.unity",
        "Assets/Scenes/region 1/CutScene_ta.unity",
        "Assets/Scenes/region 1/Success_pa.unity",
        "Assets/Scenes/region 1/Success_ga.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity",
        "Assets/Scenes/region 1/Success_ta_incorrect.unity",
    };

    // Filled by Task 7 (Phase 2).
    private static readonly Dictionary<string, (string field, float value)[]> WorldValues =
        new Dictionary<string, (string, float)[]>();
    private static readonly string[] WorldScenes = Array.Empty<string>();

    [MenuItem("Tools/Pacing/Apply Fast Pacing (Core Loop)")]
    public static void ApplyCore() => Apply(CoreScenes, CoreValues);

    [MenuItem("Tools/Pacing/Apply Fast Pacing (World)")]
    public static void ApplyWorld() => Apply(WorldScenes, WorldValues);

    private static void Apply(string[] scenes, Dictionary<string, (string field, float value)[]> table)
    {
        foreach (string scenePath in scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int touched = 0;

            foreach (var kv in table)
            {
                Type type = FindType(kv.Key);
                if (type == null)
                {
                    Debug.LogError($"[FastPacing] Unknown component type '{kv.Key}'");
                    continue;
                }

                // Include inactive objects — several book/icon roots start disabled.
                foreach (var comp in UnityEngine.Object.FindObjectsByType(
                    type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(comp);
                    bool changed = false;
                    foreach (var (field, value) in kv.Value)
                    {
                        SerializedProperty p = so.FindProperty(field);
                        if (p == null)
                        {
                            Debug.LogError($"[FastPacing] {kv.Key}.{field} not found in {scenePath}");
                            continue;
                        }
                        if (!Mathf.Approximately(p.floatValue, value))
                        {
                            p.floatValue = value;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty((UnityEngine.Object)comp);
                        touched++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FastPacing] {scene.name}: updated {touched} component(s)");
        }
    }

    private static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
```

- [ ] **Step 2: Compile + run**

Verify 0 compile errors, then run menu `Tools/Pacing/Apply Fast Pacing (Core Loop)`.
Expected console output: one `[FastPacing] <scene>: updated N component(s)` line per scene, N ≥ 1 in all seven, **no `[FastPacing] ... not found` errors**. (A `not found` error means a field name typo — fix the table, re-run.)

Note: `SuccessGaReturn` and `SuccessPaOwlEpilogue` only exist in some Success scenes, and `CrowEntranceCutscene` only in CutScene_ga — components simply not found in a scene are silently skipped by `FindObjectsByType`, which is correct.

- [ ] **Step 3: Spot-check one value**

Open CutScene_bear, select the `OwlRoot` object, confirm Inspector shows `Zoom Duration = 0.4`, `Hold After Zoom = 0`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/ApplyFastPacing.cs "Assets/Scenes/region 1"
git commit -m "perf(pacing): stamp fast timing values into all core-loop scenes"
```

### Task 5: Verify the core loop end-to-end

**Files:** none (play-mode verification).

- [ ] **Step 1: CutScene_bear time-to-interactive**

Enter play mode in CutScene_bear. Watch the console for `[Pacing] stones interactive at X.Xs`.
Expected: **X ≤ 13.0** (was ~27). If over budget, the remaining time is owl voice — that is the floor, do not cut further.

- [ ] **Step 2: Build the correct word (ปา)**

Drag ก out, place ป + า. Confirm: placement voices play but feel snappy (~0.6s each), book_craft_pa page pops in fast, echo autoplays and finishes in ~2s, mic usable right after. Record → success mark → shake (~0.8s) → white flash → Success_pa loads. Total mic-stop → scene visible ≤ 2.5s.

- [ ] **Step 3: Wrong word + timer resume**

Fresh run (Tools/Scenes/Reset WorldMap Progress not needed; just replay CutScene_bear): build กา, let it route to Success_ga, return. Confirm the 30s `WordAssemblyTimer` resumes from where it paused (not reset to 00:30), stones reveal all-at-once, and the retry path still skips the owl intro.

- [ ] **Step 4: One Success scene sanity**

Let Success_pa play through: owl epilogue voices play back-to-back with ~0.15s holds, no long frozen owl at the end.

- [ ] **Step 5: Commit any fixes; otherwise nothing to commit**

---

## Phase 2 — World scenes (forest walk, quest map, world map, chase/crow)

### Task 6: QuestPathSequence hardcoded turn holds

**Files:**
- Modify: `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs`

**Interfaces:** none new.

- [ ] **Step 1: Shorten the six hardcoded turn beats**

There are six occurrences of `yield return new WaitForSeconds(0.4f);` in this file (approx. lines 423, 452, 494, 523, 545, 591 — some with a `// brief turn before moving` comment). Replace **all six** with:

```csharp
            yield return new WaitForSeconds(0.15f);
```

Keep any trailing comment on the line intact.

- [ ] **Step 2: Verify compilation** — 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs
git commit -m "perf(forest): shorten hardcoded turn beats in quest path walk"
```

### Task 7: Extend ApplyFastPacing with the world-scene table

**Files:**
- Modify: `Assets/Editor/ApplyFastPacing.cs`

**Interfaces:**
- Consumes: the `WorldValues` / `WorldScenes` placeholders created in Task 4.

- [ ] **Step 1: Fill in the world table**

Replace:

```csharp
    // Filled by Task 7 (Phase 2).
    private static readonly Dictionary<string, (string field, float value)[]> WorldValues =
        new Dictionary<string, (string, float)[]>();
    private static readonly string[] WorldScenes = Array.Empty<string>();
```

with:

```csharp
    private static readonly Dictionary<string, (string field, float value)[]> WorldValues =
        new Dictionary<string, (string, float)[]>
    {
        ["QuestPathSequence"] = new (string, float)[]
        {
            ("startDelay", 0.15f),
            ("moveSpeed", 1.5f),           // was 1
            ("walkAnimSpeedParam", 1.5f),  // keep feet in sync with the faster walk
            ("questAutoHold", 0.6f),       // was 1.5, hit 5 times per walk
            ("faceHoldBeforeQuest", 0.7f), // was 2
            ("sceneExitCoverDuration", 0.6f),
        },
        ["QuestMapIntroFlow"] = new (string, float)[]
        {
            ("startDelay", 0.15f),
            ("mapViewHoldDuration", 0.8f),               // was 2
            ("foxTalkDuration", 2.5f),                   // was 4
            ("foxCameraFullMapHoldDuration", 0.2f),
            ("delayBeforeRestoringFullBrightness", 0.8f),// was 2
            ("delayAfterAllStonesBeforeSigns", 0.2f),
            ("delayBetweenQuestPointReveals", 0.15f),
            ("selectedQuestHoldBeforeZoom", 0.15f),
            ("selectedQuestHoldAfterZoom", 0.2f),
        },
        ["WorldMapProblemIslands"] = new (string, float)[]
        {
            ("unlockAnimationDelay", 0.2f),      // was 0.45
            ("unlockAnimationDuration", 1f),     // was 1.8
            ("unlockHoldDuration", 0.5f),        // was 1.1
            ("unlockFadeOutDuration", 0.5f),
            ("playableIslandPromptDuration", 1.2f), // was 2.1
            ("sceneExitCoverDuration", 0.5f),
        },
        ["CutScene2ChaseController"] = new (string, float)[]
        {
            ("startDelayAfterFade", 0.1f),   // was 0.35
            ("whiteFadeOutDuration", 0.6f),  // was 1.05
            ("bearRunDuration", 1f),         // was 1.35
            ("stoneChaseDuration", 0.9f),    // was 1.25
            ("completeHoldDuration", 0.15f), // was 0.3
            ("blackFadeInDuration", 0.5f),   // was 0.85
        },
        ["CrowCutsceneController"] = new (string, float)[]
        {
            ("blackFadeOutDuration", 0.35f),
            ("flyDuration", 1.5f),           // was 2.25
            ("blackFadeInDuration", 0.35f),
        },
        ["CrowSetFreeCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.25f),
            ("fadeOutDuration", 0.6f),       // was 1
            ("clip2FadeInDuration", 0.4f),
        },
    };

    private static readonly string[] WorldScenes =
    {
        "Assets/Scenes/region 1/reference_forest.unity",
        "Assets/Scenes/region 1/quest_map1.unity",
        "Assets/Scenes/WorldMap.unity",
        "Assets/Scenes/region 1/cut_scene2.unity",
        "Assets/Scenes/region 1/cut_scene3.unity",
        "Assets/Scenes/region 1/Success_ga.unity",   // CrowSetFreeCutscene lives here
        "Assets/Scenes/region 1/Success_pa.unity",
    };
```

- [ ] **Step 2: Compile + run**

0 compile errors, then run `Tools/Pacing/Apply Fast Pacing (World)`.
Expected: `[FastPacing] <scene>: updated N` per scene, no `not found` errors. If a field name errors, check the component's actual field with the Inspector (Debug mode) and correct the table — the names above were verified against the source on 2026-07-08.

- [ ] **Step 3: Commit**

```bash
git add Assets/Editor/ApplyFastPacing.cs "Assets/Scenes"
git commit -m "perf(pacing): stamp fast timing values into world scenes"
```

### Task 8: Verify the full first-session flow

**Files:** none (play-mode verification).

- [ ] **Step 1: Reset progress**

Run menu `Tools/Scenes/Reset WorldMap Progress` so the run behaves like a first session.

- [ ] **Step 2: Full run**

Play from WorldMap: island unlock ceremony ≤ ~2.5s total → quest map intro (fox talk ~2.5s, map hold 0.8s) → reference_forest walk (noticeably brisker walk, short beats at quest points) → cut_scene2 chase → CutScene_bear (`[Pacing] stones interactive` ≤ 13s) → build ปา → Success_pa.

- [ ] **Step 3: Regression checks**

- Crow wrong-word path: build าป in CutScene_bear → crow scene (cut_scene3) fly-in ~1.5s → back to assembly with intro skipped.
- `WordAssemblyTimer`: fresh 00:30 at book pop; beep still starts at 10s; pause/resume across the wrong-word round trip.
- Audio: no clipped speech starts anywhere (if a line sounds clipped, raise `PAD_S` to 0.1 in `trim_tts_silence.py`, re-run, reimport).

- [ ] **Step 4: Final commit if fixes were needed**

```bash
git add -A
git commit -m "perf(pacing): full-flow verification fixes"
```

---

## Self-review notes

- Silence trim (Task 1) is the only change that touches audio content; PAD_S 80ms protects consonant onsets, and Task 5/8 include listen checks.
- All scene-serialized values go through one idempotent editor script; re-running it after future scene edits is the maintenance story.
- Parallelization edits (Task 2) only convert `yield return X()` to `StartCoroutine(X())` where the two animations touch disjoint state (dim overlay vs zoom targets vs owl frames vs book) — no logic changes.
- Voice remains the pacing floor (~9.3s of owl teaching in CutScene_bear). Going lower requires re-baking shorter lines — out of scope per user decision (no content cuts).
