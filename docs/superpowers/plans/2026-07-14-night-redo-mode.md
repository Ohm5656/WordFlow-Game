# Night Redo Mode — implementation plan (2026-07-14)

Goal: after the daytime run of `reference_forest` ends (night falls, hero enters the house), fade to
black → **WorldMap in night ambience** → a blinking **star button** → back into the island →
`reference_forest` **at night**, where only the quests that scored **2 stars** are shown (with a 2/3
star board next to them). Redo them with the *same* puzzle logic (smoke clock, stones, mic) — every
scene during a night quest is in night ambience. Quests already at 3 stars are hidden entirely.

Decisions locked with the user (2026-07-14):
- Night puzzle entry: **bear/crow entrance cutscene still plays**, but **no owl talking** — the craft
  book pops straight after the entrance animation. Scene is in night ambience.
- All night quests done → hero **walks into the house** → fade → **WorldMap (night)**.
- Quests already at 3⭐: **hidden at night**, hero walks past.
- WorldMap button: **blinking star icon, no text**.
- Smoke/fog + timer logic: **unchanged** (already correct — see §0.4).

---

## 0. Ground truth (verified in code — do not re-derive)

### 0.1 Only TWO quests have stars
`reference_forest` (`Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs`) has 6 beats, but
only two of them launch a puzzle scene:

| Beat | Actor | Scene | `targetWordId` |
|---|---|---|---|
| 1 | bear (field is named `villager`) at `wp_1` | `CutScene_bear` (ปา) | `paa` |
| 2 | two crows at `wp_2` | `CutScene_ga` (กา) | `kaa` |

Beats 3/4/5 (sick foxes, crows over crops, sick pigs) auto-complete after `questAutoHold` — no puzzle,
no stars. Beat 6 = nightfall walk + hero fades out at `wp_12`. **Only beats 1 and 2 can be redone.**

### 0.2 Star rules (`MagicStonePuzzleController`)
- ⭐1 — any valid word built (`WordResultRoutine`, ~line 943)
- ⭐2 — the word was the target word (`RecordingSuccessRoutine`, ~line 1734)
- ⭐3 — target word **and** `builtInTime` (`WordAssemblyTimer.SmokeRemaining > 0` at assembly, line 909)

A wrong-but-real word (กา in the ปา puzzle) routes to `Success_ga` → `SuccessGaReturn` → **forced retry
back into `CutScene_bear`**. So a quest can only be *left* with **2 or 3 stars** — exactly as the user
said. The one improvable case is **2⭐ → 3⭐ (beat the clock)**.

### 0.3 Stars are NOT stored per quest (the gap to fill)
`StarHud` (`Assets/Scripts/Region1/Stars/StarHud.cs`) persists a single global
`PlayerPrefs["StarEarnedCount"]`, and **both** `CutScene_bear` and `CutScene_ga` have
`resetOnAwake: 1`, so it is wiped on every puzzle entry. It is an *in-puzzle* counter, not a record.
→ We add a separate per-quest store (§1.2). `StarHud.cs` itself needs **no change**.

### 0.4 The clock already resets correctly for a redo
`WordAssemblyTimer.BeginFresh()` → full 30s. It is called from
`OwlGreetingCutscene.GreetingRoutine` when the book pops. `Resume()` (continue from persisted
remaining) is only used on the wrong-word retry path. Our night route calls `BeginFresh()` → every
night attempt gets a fresh 30s. **Fog/smoke needs no change at all** (`FogController` reads the timer
through static providers).

### 0.5 Scene flow that already exists (reuse it, don't rebuild)
```
reference_forest  --Beat1--> CutScene_bear --(ปา ok)--> Success_pa
                                                          └ BearCutscene{nextScene:reference_forest, resumeForestAtBeat2:1}
                  --Beat2--> CutScene_ga   --(กา ok)--> Success_ga_correct
                                                          └ CrowSetFreeCutscene{nextScene:reference_forest, resumeForestAtBeat3:1}
```
Hand-off flags live in `Assets/Scripts/Region1/Adventure/BearEncounterFlow.cs` (plain statics).
Black fades: `SceneFadeController.Cover(d)` on exit; `reference_forest` self-reveals; `WorldMap`
self-reveals via `WorldMapFadeIn`. **So `Cover()` → `LoadScene("WorldMap")` is already safe** (no
black-screen risk). `WorldMap` is in EditorBuildSettings (`enabled: 1`).

### 0.6 WorldMap facts
- No Canvas, no UI at all — only sprite islands + `WorldMapProblemIslands` + `WorldMapFadeIn` on
  Main Camera. **The night button must be built at runtime.**
- `MarkRegionCompleted` is only called from `SuccessSceneIntro`, which lives *only* in the dead
  `success.unity` scene → in the live flow `WorldMapHighestUnlockedRegion` stays **1** → the playable
  island stays **Island2 → `reference_forest`**. The night button can just reuse the playable island's
  `SceneName`.
- `WorldMapProblemIslands.EnsureInitialProgress` re-arms `PendingUnlockRegion` every entry
  (`replayFirstUnlockUntilNextRegion`), so the Island2 lock→unlock animation replays on *every*
  WorldMap entry. At night we suppress it (§4.3).

### 0.7 The bear entrance → owl → book chain
`BearCutscene` (in `CutScene_bear`, `nextScene` empty) plays the bear walk-in, then calls
`owlGreeting.PlayGreeting()`. `OwlGreetingCutscene.PlayGreeting()` (line 314) branches:
- `MagicStonePuzzleController.ConsumeRetryAfterCrow()` → `RetryMagicStonePuzzleRoutine` (no owl, book
  snapped on, `WordAssemblyTimer.Resume()`)
- otherwise → `GreetingRoutine` (zoom + dim + owl talks + bear focus + book reveal + `BeginFresh` +
  stone reveal)

`CutScene_ga` has the same `OwlGreetingCutscene`; its crow entrance lives in `Crow.prefab`.
→ **One new branch in `PlayGreeting()` covers both scenes** (§5.2).

---

## 1. New data model

### 1.1 `Assets/Scripts/Common/NightMode.cs` (new)
Static, PlayerPrefs-backed (survives scene loads *and* editor play-stop, matching the project's
existing `MagicStonePuzzleRetryAfterCrow` pattern).

```csharp
using UnityEngine;

/// The night pass: after the daytime forest run ends, the world is in NIGHT PHASE and any quest that
/// scored <3 stars can be redone once per night session.
///  - NightPhase  : the world has turned to night (set when the day forest run ends). Drives the
///                  WorldMap night look + star button.
///  - RedoActive  : we are inside a night redo run (forest + its puzzle scenes render at night,
///                  the owl intro is skipped). Set by the WorldMap star button.
///  - ActiveQuest : the quest id currently being redone (empty = not inside a puzzle). Set by the
///                  forest right before it loads the puzzle scene; consumed when it comes back.
///  - DoneThisNight(id) : that quest was attempted this night session — do not offer it again, even
///                  if it is still at 2 stars. WITHOUT THIS THE NIGHT ROUTE LOOPS FOREVER on a quest
///                  the child fails to improve.
public static class NightMode
{
    private const string PhaseKey = "NightPhase";
    private const string RedoKey = "NightRedoActive";
    private const string ActiveQuestKey = "NightActiveQuest";
    private const string DonePrefix = "NightDone_";

    public static bool NightPhase
    {
        get => PlayerPrefs.GetInt(PhaseKey, 0) == 1;
        set { PlayerPrefs.SetInt(PhaseKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool RedoActive
    {
        get => PlayerPrefs.GetInt(RedoKey, 0) == 1;
        set { PlayerPrefs.SetInt(RedoKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string ActiveQuest
    {
        get => PlayerPrefs.GetString(ActiveQuestKey, string.Empty);
        set { PlayerPrefs.SetString(ActiveQuestKey, value ?? string.Empty); PlayerPrefs.Save(); }
    }

    public static bool IsDoneThisNight(string questId) =>
        !string.IsNullOrEmpty(questId) && PlayerPrefs.GetInt(DonePrefix + questId, 0) == 1;

    public static void MarkDoneThisNight(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        PlayerPrefs.SetInt(DonePrefix + questId, 1);
        PlayerPrefs.Save();
    }

    /// Pressed the star button: a brand new night session — every quest is offerable again.
    public static void BeginSession(string[] questIds)
    {
        if (questIds != null)
            foreach (string id in questIds) PlayerPrefs.DeleteKey(DonePrefix + id);
        ActiveQuest = string.Empty;
        RedoActive = true;
    }

    /// Back on the WorldMap: the run is over (the night phase itself stays on).
    public static void EndSession()
    {
        ActiveQuest = string.Empty;
        RedoActive = false;
    }

    public static void ResetAll(string[] questIds)
    {
        PlayerPrefs.DeleteKey(PhaseKey);
        PlayerPrefs.DeleteKey(RedoKey);
        PlayerPrefs.DeleteKey(ActiveQuestKey);
        if (questIds != null)
            foreach (string id in questIds) PlayerPrefs.DeleteKey(DonePrefix + id);
        PlayerPrefs.Save();
    }
}
```

### 1.2 `Assets/Scripts/Common/QuestStars.cs` (new)
Dumb per-quest best-score store. **Quest id = the puzzle's existing `targetWordId`** (`"paa"` for the
bear quest, `"kaa"` for the crow quest) — both are already serialized in the scenes, so **no scene
edit is needed to identify a quest**.

```csharp
using UnityEngine;

/// Best star count (0-3) ever earned per quest. Written by MagicStonePuzzleController when a quest is
/// completed with the correct word; read by the night route to decide what can be redone.
public static class QuestStars
{
    private const string Prefix = "QuestStars_";
    public const int Max = 3;

    public static int Get(string questId) =>
        string.IsNullOrEmpty(questId) ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(Prefix + questId, 0), 0, Max);

    /// Best-of: a night redo that goes worse never lowers the record.
    public static void RecordBest(string questId, int stars)
    {
        if (string.IsNullOrEmpty(questId)) return;
        int best = Mathf.Max(Get(questId), Mathf.Clamp(stars, 0, Max));
        PlayerPrefs.SetInt(Prefix + questId, best);
        PlayerPrefs.Save();
    }

    /// Completed but not perfect -> redoable at night.
    public static bool NeedsRedo(string questId)
    {
        int s = Get(questId);
        return s > 0 && s < Max;
    }

    public static bool AnyNeedsRedo(string[] questIds)
    {
        if (questIds == null) return false;
        foreach (string id in questIds)
            if (NeedsRedo(id) && !NightMode.IsDoneThisNight(id)) return true;
        return false;
    }

    public static void Clear(string[] questIds)
    {
        if (questIds == null) return;
        foreach (string id in questIds) PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.Save();
    }
}
```

---

## 2. Phase A — record per-quest stars during the day

**File:** `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`

In `RecordingSuccessRoutine()` (~line 1734) the award block already computes `correct`. Extend it —
`StarHud.EarnedCount` re-reads PlayerPrefs, which `AwardRoutine` has just written, so it is the final
total for this attempt:

```csharp
        if (StarHud.Instance != null)
        {
            bool correct = !string.IsNullOrEmpty(activeResultWord)
                           && activeResultWord == (string.IsNullOrEmpty(targetWord) ? "ปา" : targetWord);

            int award = correct ? (builtInTime ? 2 : 1) : 0;
            if (award > 0)
            {
                yield return StarHud.Instance.AwardRoutine(award);
            }

            // The quest is only ever LEFT on the correct word (a wrong word forces a retry), so this
            // is the one place a quest's final score exists. Keyed by targetWordId (paa / kaa) so no
            // scene wiring is needed. Best-of: a night redo can raise it, never lower it.
            if (correct)
            {
                QuestStars.RecordBest(targetWordId, StarHud.EarnedCount);
            }
        }
```

Nothing else changes here. Do **not** touch `StarHud.cs`.

---

## 3. Phase B — day run ends → WorldMap at night

**File:** `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs`

Add serialized config (defaults mean **zero scene edits** — Unity keeps a C# field initializer for a
field that is missing from the scene YAML):

```csharp
    [Header("Night redo")]
    [Tooltip("Quest ids that own a puzzle, in walk order. Must equal each puzzle's targetWordId.")]
    [SerializeField] private string[] nightQuestIds = { "paa", "kaa" };
    [Tooltip("Puzzle scene per night quest id, index-for-index with nightQuestIds.")]
    [SerializeField] private string[] nightQuestScenes = { "CutScene_bear", "CutScene_ga" };
    [Tooltip("Scene loaded when the hero enters the house (day end, and after the last night quest).")]
    [SerializeField] private string worldMapSceneName = "WorldMap";
    [Tooltip("Star board shown next to a redoable quest at night: world offset above the actor.")]
    [SerializeField] private Vector3 nightBadgeOffset = new Vector3(0f, 1.6f, 0f);
```

At the very end of `RunSequence()` — right after `yield return FadeOutBody();` (line ~587) — the hero
has entered the house and the map is already night. Hand off to the WorldMap:

```csharp
            // Reached wp_12: the hero fades out and disappears.
            yield return FadeOutBody();

            // The day is over: the world stays in night phase, and the map offers a redo of every
            // quest that is not yet perfect.
            NightMode.NightPhase = true;
            yield return SceneFadeController.Cover(sceneExitCoverDuration);
            SceneManager.LoadScene(worldMapSceneName);
```

(`WorldMapFadeIn` self-bootstraps the black→clear reveal on the WorldMap side — verified.)

---

## 4. Phase C — WorldMap night

### 4.1 `Assets/Resources/Stars/star.png` (new asset)
Runtime-built UI cannot use `AssetDatabase`, and `Assets/Art/quest_map/star.png` is not under a
`Resources/` folder. **Copy** it (file copy, keep the original where it is):

```
Assets/Art/quest_map/star.png  ->  Assets/Resources/Stars/star.png
```
Delete the copied `.meta` if you copy it too, and let Unity reimport (it must get a NEW guid).
Import settings: Texture Type **Sprite (2D and UI)**, Sprite Mode **Single** (same as the source).
Load at runtime with `Resources.Load<Sprite>("Stars/star")`.

### 4.2 `Assets/Scripts/WorldMap/WorldMapNight.cs` (new)
Self-bootstrapping, exactly like `WorldMapFadeIn` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` +
`SceneManager.sceneLoaded`, guard on `GameObject.Find(name)`, active scene name == `"WorldMap"`).
Runs only when `NightMode.NightPhase` is true. It builds, at runtime:

1. **Night tint** — a `ScreenSpaceOverlay` Canvas, `sortingOrder = 9000` (under the 10000 fade
   canvases so the black fade still covers it), one full-screen `Image`,
   `color = new Color(0.04f, 0.09f, 0.20f, 0.62f)` (the moonlit blue used by `NightLighting`),
   `raycastTarget = false`.
2. **Star button** — on the SAME canvas, an `Image` with `Resources.Load<Sprite>("Stars/star")`,
   ~200x200 at the reference resolution, anchored bottom-centre (`anchorMin = anchorMax = (0.5f, 0f)`,
   `anchoredPosition = (0, 220)`), `raycastTarget = true`.
   Only created when `QuestStars.AnyNeedsRedo(questIds)` — otherwise the night map has no button.
   - Blink: in `Update`, `scale = Lerp(1f, 1.12f, s)` and `alpha = Lerp(0.55f, 1f, s)` where
     `s = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f)`.
   - Click: pointer press inside `RectTransformUtility.RectangleContainsScreenPoint` (there is no
     `EventSystem` in the scene — do NOT rely on `Button.onClick`; read the pointer the same way
     `WorldMapProblemIslands.TryGetPointerPress` does, and copy that method).
   - On click: block re-entry, then
     ```csharp
     GameAudio.PlayClick();
     NightMode.BeginSession(questIds);          // clears the per-night done flags, RedoActive = true
     MagicStonePuzzleController.ConsumeRetryAfterCrow(); // defensive: drop stale retry flags so the
     MagicStonePuzzleController.ConsumeRetryAfterAlt();  // puzzle takes the fresh (BeginFresh) path
     yield return SceneFadeController.Cover(0.7f);
     SceneManager.LoadScene(islandScene);       // see below
     ```
   - `islandScene`: read it off the map instead of hardcoding — `WorldMapProblemIslands` knows the
     playable island. Add a tiny public accessor to that script:
     ```csharp
     public string PlayableSceneName =>
         playableIsland != null && playableIsland.Progress != null ? playableIsland.Progress.SceneName : string.Empty;
     ```
     and in `WorldMapNight`, `FindObjectOfType<WorldMapProblemIslands>()?.PlayableSceneName`, falling
     back to `"reference_forest"` if empty/null.
   - The quest id list: `WorldMapNight` needs `{"paa","kaa"}`. Keep it as a `static readonly string[]`
     const on `WorldMapNight` — one place, matching `QuestPathSequence.nightQuestIds`. (Two literals
     is acceptable; do not build a ScriptableObject for it.)
3. Give the tint + button canvas ~`0.9s` of delay before showing the button so it appears after the
   WorldMap fade-in (which takes `0.08 + 1.6s`); a simple fade-in over 0.5s starting at t=1.7s is
   fine. Tunable serialized fields on the component are welcome; it is created at runtime, so give
   them sane C# defaults.

### 4.3 Suppress the lock-unlock replay at night
**File:** `Assets/Scripts/WorldMap/WorldMapProblemIslands.cs`, in `EnsureInitialProgress()`:

```csharp
            if (replayFirstUnlockUntilNextRegion
                && !NightMode.NightPhase          // night: the island is long unlocked, do not replay it
                && highestUnlockedRegion == firstRegion
                && PlayerPrefs.GetInt(PendingUnlockRegionKey, 0) == 0)
```

---

## 5. Phase D/E — night ambience + no-owl entry in the puzzle scenes

### 5.1 `Assets/Scripts/Common/NightTintOverlay.cs` (new)
Self-bootstrapping (same pattern as `SceneFadeController`), and it does nothing unless
`NightMode.RedoActive`. On `sceneLoaded`, if the active scene name is in

```csharp
private static readonly string[] NightScenes =
{
    "CutScene_bear", "CutScene_ga",
    "Success_pa", "Success_ga", "Success_ga_correct", "Success_ta_incorrect",
};
```

create one `ScreenSpaceOverlay` Canvas named `"Night Tint"`, `sortingOrder = 9000`, one full-screen
`Image`, `color = new Color(0.04f, 0.09f, 0.20f, 0.38f)`, `raycastTarget = false`.

Why an overlay and not a per-object tint: `CutScene_bear` / `CutScene_ga` have exactly **one** Canvas
(sortingOrder 0) and everything (background, book, stones, fog, star HUD) is a sibling `Image` inside
it, so a whole-screen wash at 0.38 alpha is the only change that touches nothing else. It sits under
the 10000 fade canvases, so scene transitions still read correctly.
**`0.38f` is the tuning knob** — expose it as a `[SerializeField] private float tintAlpha = 0.38f;`
and let the user dial it in; too dark and the stones stop reading.
Do NOT add it to `reference_forest` (that scene has its own `NightLighting` rig).

### 5.2 No owl on a night redo, book straight after the entrance
**File:** `Assets/Scripts/Region1/Cutscenes/OwlGreetingCutscene.cs`, `PlayGreeting()` (line ~314).

Branch order matters: the **wrong-word retry must still win** (it needs `Resume()`, not `BeginFresh()`
— it is the same 30s clock continuing), so put the night branch *after* it:

```csharp
    public void PlayGreeting()
    {
        if (!isActiveAndEnabled) return;
        if (greetingRoutine != null) StopCoroutine(greetingRoutine);
        StopVoice();

        if (MagicStonePuzzleController.ConsumeRetryAfterCrow())
        {
            greetingRoutine = StartCoroutine(RetryMagicStonePuzzleRoutine());
            return;
        }

        // Night redo: the bear/crow entrance has just played; skip the owl (zoom, dim, talking,
        // bear-focus) entirely and open the craft book straight away, on a fresh 30s clock.
        if (NightMode.RedoActive)
        {
            greetingRoutine = StartCoroutine(NightRedoRoutine());
            return;
        }

        greetingRoutine = StartCoroutine(GreetingRoutine());
    }

    private IEnumerator NightRedoRoutine()
    {
        ApplyPreservePlacedFrameSettings();
        CacheFrames();

        // The owl never appears tonight.
        SetOwlFramesAlpha(0f);
        for (int i = 0; i < frames.Count; i++) frames[i].Target.gameObject.SetActive(false);
        if (UsesTalkingPrefabAnimator) StopTalkingAnimation();

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        if (playBookRevealAfterBearFocus)
        {
            WordAssemblyTimer.Instance?.BeginFresh();   // fresh 30s — a night redo is a new attempt
            yield return PlayBookRevealRoutine();       // same pop-in the day flow uses
        }

        if (playMagicStonePuzzleAfterBookReveal)
        {
            yield return PlayMagicStonePuzzleRevealRoutine();  // stones reveal one by one, as in day
        }

        greetingRoutine = null;
    }
```

**Do not** touch `GreetingRoutine`, `RetryMagicStonePuzzleRoutine`, the fog, the timer, the stones, or
`MisassemblyLock`. The gameplay of the puzzle is byte-for-byte the daytime one.

Note: `CutScene_ga`'s crow entrance lives in `Crow.prefab`; it calls the same `PlayGreeting()`, so
this one branch covers both scenes. Verify at implementation time by reading the crow prefab's
controller (`Assets/Scripts/Region1/Cutscenes/CrowCutsceneController.cs` /
`CrowEntranceCutscene.cs`) — if it does **not** route through `OwlGreetingCutscene.PlayGreeting()`,
report back before improvising.

---

## 6. Phase F — the night route in `reference_forest`

**File:** `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs`

### 6.1 `Awake()` — the map is already night, and the day's fixes already happened
```csharp
        if (nightLighting != null)
        {
            nightLighting.SetNight(NightMode.RedoActive ? 1f : 0f);
        }

        if (NightMode.RedoActive)
        {
            // The day run already healed the crops; the scene reloads with the blighted set active.
            SwapCrops();
        }
```
Everything else in `Awake()` already hides every quest actor (bear, crows, sick foxes, sick pigs,
marks) — at night they stay hidden unless the night route reveals one. Nothing to add.

### 6.2 `Start()`
```csharp
    private void Start()
    {
        StartCoroutine(NightMode.RedoActive ? RunNightRedo() : RunSequence());
    }
```

### 6.3 `RunNightRedo()`
```csharp
    private IEnumerator RunNightRedo()
    {
        // Coming back from a night puzzle? Pre-place the hero at that quest's spot and re-show its
        // actor BEFORE the reveal finishes, so it is on screen as the black fades in — the same trick
        // the day flow uses for resuming2/resuming3.
        string returned = NightMode.ActiveQuest;
        int startIndex = 0;
        if (!string.IsNullOrEmpty(returned))
        {
            startIndex = IndexOfQuest(returned) + 1;   // continue AFTER the quest we just played
            PlaceHeroAtQuest(returned);
            ShowQuestActors(returned);
            NightMode.MarkDoneThisNight(returned);     // attempted tonight: never offer it again
            NightMode.ActiveQuest = string.Empty;
        }

        // The forest never uses the day resume flags at night; drop whatever the success scene set.
        BearEncounterFlow.ResumeAtBeat2 = false;
        BearEncounterFlow.ResumeAtBeat3 = false;

        yield return new WaitUntil(() => SceneFadeController.RevealComplete);
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        if (!string.IsNullOrEmpty(returned))
        {
            // The quest is solved: its actor + "!" fade off, exactly like the day flow's return.
            GameAudio.PlayAfterQuest();
            yield return new WaitForSeconds(questAutoHold);
            yield return FadeOutQuestActors(returned);
        }

        // Walk the remaining redoable quests in order.
        for (int i = startIndex; i < nightQuestIds.Length; i++)
        {
            string id = nightQuestIds[i];
            if (!QuestStars.NeedsRedo(id) || NightMode.IsDoneThisNight(id)) continue;   // 3-star / already tried: walk past

            yield return WalkToQuest(id);        // MoveTo the quest's waypoint, face the actor
            Vibrate();
            yield return RevealQuestActors(id);  // same fade-in the day beat uses
            yield return ShowStarBadge(id);      // 2/3 board next to the actor
            yield return new WaitForSeconds(questAutoHold);

            NightMode.ActiveQuest = id;
            MagicStonePuzzleController.ConsumeRetryAfterCrow();   // defensive: fresh puzzle, not retry
            MagicStonePuzzleController.ConsumeRetryAfterAlt();
            yield return PlayQuestEnterThenCover();
            SceneManager.LoadScene(nightQuestScenes[i]);
            yield break;
        }

        // Nothing left to redo: walk home along the authored road and enter the house.
        yield return WalkHome();
        yield return FadeOutBody();

        NightMode.EndSession();
        yield return SceneFadeController.Cover(sceneExitCoverDuration);
        SceneManager.LoadScene(worldMapSceneName);
    }
```

Helpers (all built from methods that already exist in this file):

- `IndexOfQuest(id)` — index in `nightQuestIds`, `-1` if absent.
- `PlaceHeroAtQuest(id)` — `body.position = <quest waypoint>` (keep `body.position.z`) and
  `facingLock = OrientationFor(<actor> - body.position)`.
  - `"paa"` → `wp1`, actor `villager`
  - `"kaa"` → `wp2`, actor `crow1Sprite`
- `WalkToQuest(id)`:
  - `"paa"` — `facingLock = -1; yield return MoveTo(wp1.position);` then the existing approach step:
    `MoveTo(Vector3.Lerp(body.position, villager.transform.position, questApproachFraction))` and
    `facingLock = OrientationFor(bearPos - body.position)`. (Copy Beat 1, lines 341-361.)
  - `"kaa"` — `MoveTo(wp2.position)`, then `facingLock = OrientationFor(wp3.position - wp2.position)`.
    (Copy Beat 2, lines 389-400.)
- `RevealQuestActors(id)`:
  - `"paa"` — `Reveal(villagerMarkerRoot, villagerMarkerSprite, villager.gameObject, villager)`.
  - `"kaa"` — `ActivateForFade(crow1Root, crow1Sprite); ActivateForFade(crow2Root, crow2Sprite);
    StartCrowFlight(); yield return Fade(new[]{crow1Sprite, crow2Sprite}, 0f, 1f);
    yield return Reveal(crowMarkRoot, crowMarkSprite, null, null);`
- `ShowQuestActors(id)` — the already-visible variants: reuse `ShowQuest1Actor()` for `"paa"` and
  `ShowResumeCrows()` for `"kaa"` (both already exist, lines 242-268).
- `FadeOutQuestActors(id)`:
  - `"paa"` — `FadeOutAndHide(new[]{villager, villagerMarkerSprite}, new[]{villager.gameObject, villagerMarkerRoot})`
  - `"kaa"` — `crowPatrolActive = false; FadeOutAndHide(new[]{crow1Sprite, crow2Sprite}, new[]{crow1Root, crow2Root});`
    plus hide `crowMarkRoot`.
- `WalkHome()` — walk the authored road with **no quest beats**, from wherever the hero is:
  `WalkFacing(null, wp3) … WalkFacing(wp8, wp9), WalkFacing(wp9, wp10), WalkFacing(wp10, wp11),
  WalkFacing(wp11, wp12)`. Skip any waypoint the hero is already past — simplest correct version:
  walk **every** wp from `startIndex`'s next waypoint through `wp12` in order, using the existing
  `WalkFacing(from, to)`. (`WalkFacing` already handles `from == null` by facing from `body`.)
  It is the same road the day run walks, just with nothing happening on it.

### 6.4 `Assets/Scripts/Region1/ReferenceForest/QuestStarBadge.cs` (new)
The "2⭐ board" that hovers next to the quest actor at night. World-space, runtime-built, no prefab:

```csharp
using System.Collections;
using UnityEngine;

/// A 3-slot star board that pops in above a night quest, showing how many stars it already has
/// (filled) and how many are still missing (dim). Built at runtime — the forest has no UI canvas.
public sealed class QuestStarBadge : MonoBehaviour
{
    public static IEnumerator Show(Transform anchor, Vector3 offset, int filled, float fadeDuration)
    { /* spawn a root at anchor.position + offset, 3 SpriteRenderers spaced 0.55f apart,
         sprite = Resources.Load<Sprite>("Stars/star"),
         filled  -> Color.white,
         empty   -> new Color(1f, 1f, 1f, 0.22f) (a dim ghost star = the slot still to win),
         sortingOrder = 30000 (above the NightDark overlay, which is 32000 -> use 32100 so the board
         is not swallowed by the night overlay — VERIFY against NightLighting's darkOverlay order and
         go one above it),
         fade the whole board 0 -> 1 over fadeDuration, and leave it up. */ }

    public static void HideAll() { /* destroy every spawned board */ }
}
```
Called by `QuestPathSequence.ShowStarBadge(id)` with the quest actor's transform and
`nightBadgeOffset`, `filled = QuestStars.Get(id)`. Destroy it in `FadeOutQuestActors` /
right before `LoadScene`.
**Sorting-order gotcha:** `reference_forest`'s `NightDark` overlay sits at `sortingOrder 32000`
(see `NightLighting`); anything below it gets darkened. The badge must be **above** it
(`32100`) or it will be a barely-visible smudge at night. Verify in-editor.

---

## 7. Editor tools — `Assets/Editor/NightModeTools.cs` (new)

There is no way to reach night without a full playthrough, so ship the test menus:

- `Tools/Night/Simulate Day Done (2★ + 2★)` — `QuestStars.RecordBest("paa",2); RecordBest("kaa",2);
  NightMode.NightPhase = true; NightMode.RedoActive = false;` + `Debug.Log` the resulting state.
- `Tools/Night/Simulate Day Done (2★ + 3★)` — same but `kaa = 3` (proves a 3★ quest is walked past).
- `Tools/Night/Reset Night + Stars` — `QuestStars.Clear(ids); NightMode.ResetAll(ids);` and also
  `PlayerPrefs.DeleteKey("StarEarnedCount")` + `"WordAssemblyTimerRemaining"` +
  `"MagicStonePuzzleRetryAfterCrow"` + `"MagicStonePuzzleRetryAfterAlt"` (a clean slate for a fresh
  day run) — mirror the style of `Assets/Editor/ResetWorldMapProgress.cs`.
- `Tools/Night/Log State` — print `NightPhase`, `RedoActive`, `ActiveQuest`, both quest star counts,
  both done-this-night flags.

---

## 8. Scene edits

**None required.** Every new field has a C# default and every new object is built at runtime.
The only asset added is `Assets/Resources/Stars/star.png`.

If, after playtesting, the user wants different star-board placement or tint darkness, tune the
serialized fields on `QuestPathSequence` (`nightBadgeOffset`) and the `tintAlpha` on
`NightTintOverlay` / `WorldMapNight` — do not hardcode new values.

---

## 9. Test plan (run in this order)

1. `Tools/Night/Reset Night + Stars`, play from `WorldMap` → full day run. After the bear puzzle,
   check the console/`Tools/Night/Log State`: `QuestStars_paa` should be **2** if the child was slow,
   **3** if fast. Same for `kaa` after the crow puzzle. **This is the whole feature's foundation — if
   the stars are not recorded, stop and fix Phase A before continuing.**
2. Finish the day run to `wp_12`. Expect: night falls, hero fades at the house, black fade,
   **WorldMap loads in night tint with a blinking star icon** and **no lock-unlock replay**.
3. `Tools/Night/Simulate Day Done (2★ + 3★)` → enter `WorldMap` directly. Expect: night, button
   present. Click → black fade → `reference_forest` **already dark at start** (no day-to-night fade),
   hero walks to the bear, bear + "!" fade in, a **2-of-3 star board** pops next to it, then the scene
   fades to `CutScene_bear`.
4. In `CutScene_bear`: the bear entrance animation plays, **no owl**, the book pops immediately, the
   clock starts at **30s**, the smoke behaves exactly as in the day, and the whole scene is tinted
   night.
5. Build ปา fast → 3⭐ → `Success_pa` (night tinted) → back in `reference_forest` (night): the bear
   fades off, and because `kaa` is at 3★ the hero **walks straight home** past the crow spot, enters
   the house, and returns to the night WorldMap — **with the star button gone** (nothing left to redo).
6. `Tools/Night/Simulate Day Done (2★ + 2★)` → night run must chain **bear → crow → home**.
7. Failure path: during a night redo of ปา, build **กา** on purpose → `Success_ga` → forced retry back
   into `CutScene_bear`. Expect the clock to **resume** (not reset) and the owl to stay away is *not*
   required here — the retry path is the existing one; just confirm it does not crash and that on
   finally building ปา slowly the recorded star count stays **2** (best-of never lowers it) and that
   the quest is **not offered again this night** (`NightDone_paa`).

---

## 10. Gotchas (each one has bitten this codebase before)

1. **Infinite night loop.** Without `NightMode.MarkDoneThisNight`, a quest that stays at 2★ after a
   redo is still `NeedsRedo` → the route re-enters it forever. The mark is set **on return**, not on
   entry, so a mid-quest quit still lets the child retry.
2. **Stale retry flags.** `MagicStonePuzzleRetryAfterCrow` / `...AfterAlt` are PlayerPrefs and survive
   a killed play session. If one is set when a night redo starts, `PlayGreeting` takes the
   *retry* branch → `WordAssemblyTimer.Resume()` → the clock resumes at ~0s and ⭐3 is unwinnable.
   Consume both before every night `LoadScene` (§4.2, §6.3).
3. **Branch order in `PlayGreeting`.** Retry check first, night check second. Reversed, the wrong-word
   retry inside a night redo would restart the clock and hand out a free ⭐3.
4. **`StarHud.resetOnAwake` is `1` in both puzzle scenes** — leave it. The in-puzzle board must start
   empty on a redo; the *record* lives in `QuestStars`, which is written at the end.
5. **Night badge vs the night overlay.** `NightDark` renders at `sortingOrder 32000`. Anything below
   it is darkened into the mud. Put the badge above it.
6. **Scene edits vanish on domain reload** (see the project memory) — but this plan needs none. If you
   find yourself hand-editing a scene, stop and check whether a C# default would do.
7. **`reference_forest` at night must not re-run `FadeNight()`** — `Awake` sets night to 1 directly;
   the night route never calls `FadeNight`.
8. **No `EventSystem` in `WorldMap`** — the star button cannot use `Button.onClick`. Poll the pointer
   the way `WorldMapProblemIslands.TryGetPointerPress` does.
9. `Handheld.Vibrate()` is guarded by `#if UNITY_ANDROID || UNITY_IOS` — keep using the existing
   `Vibrate()` helper, do not call it directly.

---

## 11. File summary

**New**
- `Assets/Scripts/Common/NightMode.cs`
- `Assets/Scripts/Common/QuestStars.cs`
- `Assets/Scripts/Common/NightTintOverlay.cs`
- `Assets/Scripts/WorldMap/WorldMapNight.cs`
- `Assets/Scripts/Region1/ReferenceForest/QuestStarBadge.cs`
- `Assets/Editor/NightModeTools.cs`
- `Assets/Resources/Stars/star.png` (copy of `Assets/Art/quest_map/star.png`)

**Modified**
- `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs` — record the quest's stars (§2)
- `Assets/Scripts/Region1/Cutscenes/OwlGreetingCutscene.cs` — `NightRedoRoutine` branch (§5.2)
- `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs` — night config, night Awake/Start,
  `RunNightRedo`, day-end → WorldMap (§3, §6)
- `Assets/Scripts/WorldMap/WorldMapProblemIslands.cs` — `PlayableSceneName` accessor + suppress the
  unlock replay at night (§4.2, §4.3)

**Untouched on purpose**: `StarHud.cs`, `WordAssemblyTimer.cs`, `FogController.cs`,
`MisassemblyLock.cs`, `NightLighting.cs`, `SceneFadeController.cs`, every Success scene script.
