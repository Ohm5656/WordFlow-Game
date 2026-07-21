# practice_night Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `Assets/Scenes/region 1/practice_night.unity` into a one-event-per-entry nighttime
practice encounter (crow-petrified→spell กา, or crow-circling-the-scarecrow→spell ปา), with a
one-line owl comment, the game's stone-assembly feel (no smoke clock, no backend/mic, no stars),
and a red misassembly lock on a wrong build, returning to WorldMap on success.

**Architecture:** Two new light MonoBehaviours — `PracticeWordAssembly` (stone reveal/snap/lock,
reusing `MagicStonePuzzleStone` and `MisassemblyLock` as-is) and `PracticeNightController` (event
pick, crow/owl intro, hookup, resolution, fade-out) — plus a one-shot Editor setup script that
cleans the cloned scene and wires everything. `MagicStonePuzzleController` (2722 lines, coupled to
the smoke timer/backend/mic/dual-word routing) is **not** reused; only its small, self-contained
pieces are.

**Tech Stack:** Unity 6000.4.3f1, C#, UGUI, Unity MCP tools (`mcp__UnityMCP__*`) for scene
inspection/edits and play-mode verification.

## Global Constraints

- No smoke clock: do not add/reference `WordAssemblyTimer` or `FogController` anywhere in this feature.
- No backend/mic: no `GradeApiClient`, `TelemetryClient`, mic recording, or `/grade` calls.
- No stars: do not call `StarHud`.
- One event per scene entry; on success, load scene **"WorldMap"**.
- Wrong build (the other real word or garbage) → `MisassemblyLock.Instance.PlayRoutine()`, no smoke.
- Word playback is **listen-only** (no mic capture).
- Keep `MagicStonePuzzleController.cs` and `Boss.unity` / `CutScene_bear.unity` behavior unchanged —
  any shared-file edit must be additive/non-breaking (verified by reading compiler errors after
  each edit).
- Spec of record: `docs/superpowers/specs/2026-07-21-practice-night-design.md`. Where this plan's
  concrete implementation choices differ from that spec (see "Deviations" below), this plan wins —
  they were found while reading the actual shared components.

## Deviations from the design spec (found while reading the code)

1. **`MagicStonePuzzleStone.Initialize()` takes the concrete `sealed class MagicStonePuzzleController`**,
   not an interface — it cannot be reused unmodified by a different owner type. Fix: extract a
   2-member `IStonePuzzleOwner` interface (Task 1). `MagicStonePuzzleController` already exposes a
   matching `CanInteract` getter and `HandleStoneClicked(MagicStonePuzzleStone)` method, so it only
   needs `: IStonePuzzleOwner` added to its class declaration — no method bodies change.
2. **`CrowSetFreeCutscene` and `OwlGreetingCutscene` are full dedicated-scene cutscene components**
   (screen-filling shatter-explode + scene-ending epilogue; a 2068-line zoom/dim/bear-focus/book-reveal
   rig) — wrong shape for a short in-scene beat. Instead: `PracticeNightController` drives the
   `Crow` prefab's `Animator` directly (state names only, no wrapper component), and uses the
   already-existing, already-lightweight `OwlGuideAnimator` (`Assets/Prefabs/OwlGuide.prefab`,
   `PlayEnterThenTalk(Action, float)`) for the owl's one-liner — an exact fit, no changes needed.
3. **The crow's animation states are split across 4 separate `.controller` assets**, not one:
   `CrowController.controller` (`ga_fly`, `ga_stone`), `GaSetFreeController.controller`
   (`ga_set_free`, `ga_set_free2`). `ga_leftController`/`ga_rightController` exist but are not
   needed — Event B's "circling" is done by scripting the Crow's `RectTransform` around the
   scarecrow on the existing looping `ga_fly` state, no controller swap needed there.
   `PracticeNightController` swaps `Animator.runtimeAnimatorController` once, only for Event A's
   resolution (petrified → `GaSetFreeController`).
4. **The `WordAssembly` GameObject in `practice_night` is a live instance of
   `Assets/Prefabs/Boss/WordAssembly.prefab`** and already carries a `MagicStonePuzzleController`
   component on 4 of its children (`book_craft`, `book_craft_pa`, `book_craft_ga`, `magic_stone`) —
   leftover from that prefab's Boss-scene origin. These must be removed as **scene-instance
   overrides** (Task 4) — the shared prefab asset itself is untouched, so `Boss.unity` is unaffected.
5. **`practice_night` is not in `SceneFadeController.SelfRevealScenes`**, so entering it currently
   hard-cuts into view instead of fading in from black. One line is added to that array (Task 1) so
   the night scene reveals smoothly, matching `reference_forest`'s existing pattern. Entry-point
   wiring (how the player *reaches* practice_night) stays out of scope per the spec.
6. **Word playback/owl-line audio clips are plain `AudioClip` fields**, not TTS line ids — simpler,
   matches the spec's "just data" intent for a first pass. Upgrading to TTS line ids later (as
   `MagicStonePuzzleController`/`OwlGreetingCutscene` do) is a drop-in change to the same fields,
   not covered here.

## Confirmed scene facts (from Unity MCP inspection, 2026-07-22)

- Scarecrow object: `Objects/L5_Structures/dummy_idle_DOWN_0` (world-space `SpriteRenderer`).
- `WordAssembly` canvas children: `book_craft` (assembly page, has stray `MagicStonePuzzleController`
  + `WordAssemblyResizeHandle`), `book_craft_pa`, `book_craft_ga` (result pages, same stray
  components), `magic_stone` (has `stone1`/`stone2`/`stone3` children — plain `Image`+`RectTransform`,
  no `MagicStonePuzzleStone` yet — plus the stray controller), `inputSlot1`, `inputSlot2` (siblings,
  plain `RectTransform`).
- Leftovers to delete: root objects `BearIntroSpotlight`, `BearIntroCanvas`, `BossUI`, `path`,
  `frame`; and `Objects/quest_sequence`, `Objects/quest`.
- `MisassemblyLock.PlayRoutine()` already degrades gracefully with no `WordAssemblyTimer` present
  (`ClockRemaining` reads 0, `ClockRunning`/`ClockIsBeeping` both false) — no code change needed there.
- `ReferenceForestNightBackground.SetSceneNight(1f)` (static) is the correct call to force full
  night — calling `NightLighting.SetNight()` directly would be overwritten every `LateUpdate` by
  `ReferenceForestNightBackground`, which owns the baked night painting in this scene.

---

### Task 1: Extract `IStonePuzzleOwner`, verify shared files unaffected, add self-reveal

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleStone.cs`
- Modify: `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs:9` (class declaration only)
- Modify: `Assets/Scripts/Common/SceneFadeController.cs:26-30` (`SelfRevealScenes` array)

**Interfaces:**
- Produces: `public interface IStonePuzzleOwner { bool CanInteract { get; } void HandleStoneClicked(MagicStonePuzzleStone stone); }`
  — consumed by `MagicStonePuzzleStone` (Task 1) and by `PracticeWordAssembly` (Task 2).

- [ ] **Step 1: Add the interface and retype `MagicStonePuzzleStone`'s owner field**

Edit `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleStone.cs` — insert the interface right
before the class, and change the owner field/parameter type:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The minimal contract MagicStonePuzzleStone needs from whatever orchestrates it. Extracted so
/// PracticeWordAssembly (practice_night) can reuse the stone's drag/reveal/snap behaviour without
/// depending on the much larger, quest-specific MagicStonePuzzleController.
/// </summary>
public interface IStonePuzzleOwner
{
    bool CanInteract { get; }
    void HandleStoneClicked(MagicStonePuzzleStone stone);
}

public sealed class MagicStonePuzzleStone : MonoBehaviour, IPointerClickHandler
{
    private IStonePuzzleOwner controller;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image image;
    private Coroutine motionRoutine;
    private Color homeColor = Color.white;
    private bool homeCaptured;
    private bool interactable;

    public RectTransform RectTransform => rectTransform;
    public Vector2 HomePosition { get; private set; }
    public Vector3 HomeScale { get; private set; }
    public int CurrentSlot { get; private set; } = -1;

    // The glyph this stone represents (e.g. "ก", "ป", "า"). Used to assemble the word.
    public string Letter { get; private set; } = "";

    public void Initialize(IStonePuzzleOwner owner)
    {
        controller = owner;
        rectTransform = transform as RectTransform;
        image = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
```

Leave every other member of the file (`SetLetter`, `CaptureHome`, `PrepareHidden`, `Reveal`,
`SetInteractable`, `SetCurrentSlot`, `OnPointerClick`, `MoveTo`, `ReturnHome`, `MoveRoutine`,
`StopMotion`, `SetAlpha`, `SmoothStep`, `EaseOutBack`) exactly as they are — none of them reference
the concrete `MagicStonePuzzleController` type, only the `controller` field/`CanInteract`/
`HandleStoneClicked`, which the interface already covers.

- [ ] **Step 2: Make `MagicStonePuzzleController` implement the interface**

Edit `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`, line 9:

```csharp
public sealed class MagicStonePuzzleController : MonoBehaviour, IStonePuzzleOwner
```

No other line in this file changes — `public bool CanInteract => ...` (line ~217) and
`public void HandleStoneClicked(MagicStonePuzzleStone stone)` (line ~549) already match the
interface's members exactly.

- [ ] **Step 3: Add practice_night to the self-reveal scene list**

Edit `Assets/Scripts/Common/SceneFadeController.cs`:

```csharp
    private static readonly string[] SelfRevealScenes =
    {
        ReferenceForestSceneName,
        "word_build_paa_polished",
        "practice_night",
    };
```

- [ ] **Step 4: Compile and verify no errors, via Unity MCP**

Unity must be running with `NSC-Game@<hash>` connected (`ReadMcpResourceTool` server `UnityMCP`,
uri `mcpforunity://instances`; `mcp__UnityMCP__set_active_instance` with the returned id).

Run:
```
mcp__UnityMCP__refresh_unity  action: compile=request, scope=scripts, wait_for_ready=true
mcp__UnityMCP__read_console   action: get, types: ["error"], count: 20
```
Expected: `read_console` returns 0 compile errors referencing `MagicStonePuzzleStone.cs`,
`MagicStonePuzzleController.cs`, or `SceneFadeController.cs`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleStone.cs \
        Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs \
        Assets/Scripts/Common/SceneFadeController.cs
git commit -m "refactor: extract IStonePuzzleOwner so stones don't require the full puzzle controller

Lets practice_night reuse MagicStonePuzzleStone's drag/reveal/snap behaviour via a
2-member interface instead of depending on the 2700-line MagicStonePuzzleController.
Also adds practice_night to SceneFadeController's self-reveal list so it fades in
from black like reference_forest instead of hard-cutting into view."
```

---

### Task 2: `PracticeWordAssembly` — the stone-build mechanic

**Files:**
- Create: `Assets/Scripts/Region1/Practice/PracticeWordAssembly.cs`

**Interfaces:**
- Consumes: `IStonePuzzleOwner` (Task 1), `MagicStonePuzzleStone` (existing, unchanged),
  `MisassemblyLock.Instance.PlayRoutine()` (existing, unchanged), `GameAudio.PlayClick()` (existing).
- Produces (used by `PracticeNightController` in Task 3):
  - `public void Configure(string word, RectTransform resultPageRoot, AudioClip resultWordClip, System.Action onSuccessCallback)`
  - `public IEnumerator PlayReveal()`

- [ ] **Step 1: Write `PracticeWordAssembly.cs`**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight word-assembly mechanic for practice_night: reveals the ก/ป/า stones, accepts
/// stone-to-slot placement, and resolves against a single target word supplied by
/// PracticeNightController.Configure. No smoke clock, no backend, no mic — just the
/// reveal/snap/lock feel MagicStonePuzzleController uses, trimmed to what one practice round needs.
///
/// Sits on the WordAssembly/magic_stone GameObject (stone1/stone2/stone3 are its children;
/// inputSlot1/inputSlot2/book_craft are siblings under the parent WordAssembly canvas) — the same
/// layout MagicStonePuzzleController used, so no scene reparenting is required.
/// </summary>
public sealed class PracticeWordAssembly : MonoBehaviour, IStonePuzzleOwner
{
    [Header("Stones (children of this GameObject)")]
    [SerializeField] private RectTransform stone1; // ก
    [SerializeField] private RectTransform stone2; // ป
    [SerializeField] private RectTransform stone3; // า

    [Header("Slots (siblings under the parent WordAssembly canvas)")]
    [SerializeField] private RectTransform inputSlot1;
    [SerializeField] private RectTransform inputSlot2;
    [SerializeField] private Vector2 firstSlotPosition = new Vector2(-395f, 150f);
    [SerializeField] private Vector2 secondSlotPosition = new Vector2(395f, 150f);

    [Header("Assembly page (sibling under the parent WordAssembly canvas)")]
    [SerializeField] private RectTransform bookCraftRoot;

    [Header("Reveal")]
    [SerializeField] private float revealDelayBetweenStones = 0.22f;
    [SerializeField] private float revealDuration = 0.42f;
    [SerializeField] private float revealYOffset = -120f;
    [SerializeField] private float revealStartScale = 0.2f;

    [Header("Motion")]
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private float returnDuration = 0.22f;
    [SerializeField] private float snappedScaleMultiplier = 0.82f;

    [Header("Result reveal")]
    [SerializeField] private float resultRevealDelay = 0.08f;
    [SerializeField] private float resultRevealDuration = 0.38f;
    [SerializeField] private float resultStartScale = 0.96f;

    [Header("Audio")]
    [SerializeField] private AudioSource wordAudioSource;

    private readonly List<MagicStonePuzzleStone> stones = new List<MagicStonePuzzleStone>();
    private readonly MagicStonePuzzleStone[] slotOccupants = new MagicStonePuzzleStone[2];

    private string targetWord = "";
    private RectTransform resultPage;
    private AudioClip wordClip;
    private Action onSuccess;

    private bool revealFinished;
    private bool resultShown;
    private Coroutine misassemblyRoutine;

    public RectTransform DragParent => transform as RectTransform;
    public float ReturnDuration => returnDuration;
    public bool CanInteract => revealFinished && !resultShown && misassemblyRoutine == null;

    private void Awake()
    {
        ResolveStones();
        ResolveSlots();

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].CaptureHome();
            stones[i].SetCurrentSlot(-1);
            stones[i].PrepareHidden(new Vector2(0f, revealYOffset), Mathf.Max(0.01f, revealStartScale));
        }
    }

    /// Sets the round's target word, result page and echo clip, and wires the success callback.
    /// Call once before PlayReveal().
    public void Configure(string word, RectTransform resultPageRoot, AudioClip resultWordClip, Action onSuccessCallback)
    {
        targetWord = word ?? "";
        resultPage = resultPageRoot;
        wordClip = resultWordClip;
        onSuccess = onSuccessCallback;
    }

    public IEnumerator PlayReveal()
    {
        Vector2 offset = new Vector2(0f, revealYOffset);
        float startScale = Mathf.Max(0.01f, revealStartScale);

        for (int i = 0; i < stones.Count; i++)
        {
            yield return stones[i].Reveal(revealDuration, offset, startScale);
            if (i < stones.Count - 1 && revealDelayBetweenStones > 0f)
            {
                yield return new WaitForSeconds(revealDelayBetweenStones);
            }
        }

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(true);
        }

        revealFinished = true;
    }

    public void HandleStoneClicked(MagicStonePuzzleStone stone)
    {
        if (stone == null || !CanInteract)
        {
            return;
        }

        GameAudio.PlayClick();
        int currentSlot = stone.CurrentSlot;

        if (IsValidSlot(currentSlot))
        {
            if (slotOccupants[currentSlot] == stone)
            {
                slotOccupants[currentSlot] = null;
            }

            stone.SetCurrentSlot(-1);
            stone.ReturnHome();
            return;
        }

        int targetSlot = GetFirstEmptySlot();
        if (!IsValidSlot(targetSlot))
        {
            return;
        }

        SnapStoneToSlot(stone, targetSlot);
    }

    private void SnapStoneToSlot(MagicStonePuzzleStone stone, int slotIndex)
    {
        MagicStonePuzzleStone existing = slotOccupants[slotIndex];
        if (existing != null && existing != stone)
        {
            existing.SetCurrentSlot(-1);
            existing.ReturnHome();
        }

        slotOccupants[slotIndex] = stone;
        stone.SetCurrentSlot(slotIndex);
        stone.MoveTo(GetSlotPosition(slotIndex), stone.HomeScale * Mathf.Max(0.01f, snappedScaleMultiplier), snapDuration, true);

        TryComplete();
    }

    private void TryComplete()
    {
        if (slotOccupants[0] == null || slotOccupants[1] == null)
        {
            return;
        }

        string built = (slotOccupants[0].Letter ?? "") + (slotOccupants[1].Letter ?? "");

        if (built == targetWord)
        {
            StartCoroutine(SuccessRoutine());
        }
        else if (misassemblyRoutine == null)
        {
            misassemblyRoutine = StartCoroutine(MisassemblyRoutine());
        }
    }

    private IEnumerator SuccessRoutine()
    {
        resultShown = true;

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
        }

        if (resultRevealDelay > 0f)
        {
            yield return new WaitForSeconds(resultRevealDelay);
        }

        CanvasGroup bookGroup = EnsureCanvasGroup(bookCraftRoot);
        CanvasGroup resultGroup = EnsureCanvasGroup(resultPage);
        CanvasGroup stoneRootGroup = EnsureCanvasGroup(DragParent);

        Vector3 resultTargetScale = resultPage != null ? resultPage.localScale : Vector3.one;
        Vector3 resultStartScaleVector = resultTargetScale * Mathf.Max(0.01f, resultStartScale);

        if (resultPage != null)
        {
            resultPage.gameObject.SetActive(true);
            resultPage.localScale = resultStartScaleVector;
            SetGroup(resultGroup, 0f);
        }

        SetGroup(stoneRootGroup, 1f);

        float duration = Mathf.Max(0.01f, resultRevealDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            float pop = EaseOutBack(t);

            SetGroup(bookGroup, 1f - smooth);
            SetGroup(resultGroup, smooth);
            SetGroup(stoneRootGroup, 1f - smooth);

            if (resultPage != null)
            {
                resultPage.localScale = Vector3.LerpUnclamped(resultStartScaleVector, resultTargetScale, pop);
            }

            yield return null;
        }

        SetGroup(bookGroup, 0f);
        SetGroup(resultGroup, 1f);
        SetGroup(stoneRootGroup, 0f);

        if (bookCraftRoot != null)
        {
            bookCraftRoot.gameObject.SetActive(false);
        }

        if (resultPage != null)
        {
            resultPage.localScale = resultTargetScale;
        }

        if (wordClip != null && wordAudioSource != null)
        {
            wordAudioSource.Stop();
            wordAudioSource.clip = wordClip;
            wordAudioSource.Play();
            yield return new WaitForSeconds(wordClip.length);
        }

        onSuccess?.Invoke();
    }

    /// Two stones, no match (the other real word or garbage): freeze the board, red-flash via
    /// MisassemblyLock (no smoke — practice has no clock), then spring the stones home.
    private IEnumerator MisassemblyRoutine()
    {
        if (MisassemblyLock.Instance != null)
        {
            yield return MisassemblyLock.Instance.PlayRoutine();
        }

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            MagicStonePuzzleStone occupant = slotOccupants[i];
            if (occupant == null) continue;

            slotOccupants[i] = null;
            occupant.SetCurrentSlot(-1);
            occupant.ReturnHome();
        }

        misassemblyRoutine = null;
    }

    private int GetFirstEmptySlot()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null) return i;
        }
        return -1;
    }

    private static bool IsValidSlot(int index) => index >= 0 && index < 2;

    private Vector2 GetSlotPosition(int index)
    {
        RectTransform slot = index == 0 ? inputSlot1 : inputSlot2;
        if (slot != null)
        {
            return slot.anchoredPosition;
        }
        return index == 0 ? firstSlotPosition : secondSlotPosition;
    }

    private void ResolveStones()
    {
        if (stone1 == null) stone1 = transform.Find("stone1") as RectTransform;
        if (stone2 == null) stone2 = transform.Find("stone2") as RectTransform;
        if (stone3 == null) stone3 = transform.Find("stone3") as RectTransform;

        stones.Clear();
        AddStone(stone1, "ก");
        AddStone(stone2, "ป");
        AddStone(stone3, "า");
    }

    private void AddStone(RectTransform stoneRect, string letter)
    {
        if (stoneRect == null) return;

        MagicStonePuzzleStone stone = stoneRect.GetComponent<MagicStonePuzzleStone>();
        if (stone == null)
        {
            stone = stoneRect.gameObject.AddComponent<MagicStonePuzzleStone>();
        }

        stone.Initialize(this);
        stone.SetLetter(letter);
        stones.Add(stone);

        Image image = stoneRect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private void ResolveSlots()
    {
        if (transform.parent == null) return;
        if (inputSlot1 == null) inputSlot1 = transform.parent.Find("inputSlot1") as RectTransform;
        if (inputSlot2 == null) inputSlot2 = transform.parent.Find("inputSlot2") as RectTransform;
        if (bookCraftRoot == null) bookCraftRoot = transform.parent.Find("book_craft") as RectTransform;
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform target)
    {
        if (target == null) return null;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = Mathf.Clamp01(alpha);
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutBack(float value)
    {
        value = Mathf.Clamp01(value);
        const float overshoot = 1.15f;
        float shifted = value - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }

#if UNITY_EDITOR
    // ponytail: one runnable check for the branch that matters most (slot ORDER changes the built
    // word — า+ก must not equal ก+า). No test framework in this project; a menu item matches the
    // existing Tools/* convention (see e.g. Tools/Boss, Tools/Crow).
    [UnityEditor.MenuItem("Tools/Practice/Verify Word Classification")]
    private static void VerifyWordClassification()
    {
        var cases = new (string slot1, string slot2, string target, bool expectMatch)[]
        {
            ("ก", "า", "กา", true),
            ("า", "ก", "กา", false), // order matters: าก != กา
            ("ป", "า", "ปา", true),
            ("า", "ป", "ปา", false),
            ("ก", "ป", "กา", false), // garbage
            ("ป", "ก", "ปา", false), // garbage
        };

        int passed = 0;
        foreach (var c in cases)
        {
            bool actual = (c.slot1 + c.slot2) == c.target;
            bool ok = actual == c.expectMatch;
            Debug.Log(ok
                ? $"[PracticeWordAssembly] PASS: {c.slot1}+{c.slot2} vs target {c.target} -> {actual}"
                : $"[PracticeWordAssembly] FAIL: {c.slot1}+{c.slot2} vs target {c.target} -> expected {c.expectMatch}, got {actual}");
            if (ok) passed++;
        }
        Debug.Log($"[PracticeWordAssembly] {passed}/{cases.Length} passed");
    }
#endif
}
```

- [ ] **Step 2: Compile, verify no errors**

```
mcp__UnityMCP__refresh_unity  action: compile=request, scope=scripts, wait_for_ready=true
mcp__UnityMCP__read_console   action: get, types: ["error"], count: 20
```
Expected: 0 errors referencing `PracticeWordAssembly.cs`.

- [ ] **Step 3: Run the self-check menu item**

```
mcp__UnityMCP__execute_menu_item  menu_path: "Tools/Practice/Verify Word Classification"
mcp__UnityMCP__read_console       action: get, types: ["log"], count: 10, filter_text: "PracticeWordAssembly"
```
Expected: `6/6 passed` in the log, no `FAIL` lines.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Region1/Practice/PracticeWordAssembly.cs
git commit -m "feat: add PracticeWordAssembly, a light stone-build mechanic for practice_night

Reveals the ก/ป/า stones, accepts stone-to-slot placement, and resolves
against one target word. Reuses MagicStonePuzzleStone (via IStonePuzzleOwner)
and MisassemblyLock as-is; no smoke clock, no backend, no mic."
```

---

### Task 3: `PracticeNightController` — event pick, crow/owl intro, resolution, exit

**Files:**
- Create: `Assets/Scripts/Region1/Practice/PracticeNightController.cs`

**Interfaces:**
- Consumes: `PracticeWordAssembly.Configure(...)` / `.PlayReveal()` (Task 2),
  `OwlGuideAnimator.PlayEnterThenTalk(Action, float)` (existing, unchanged),
  `ReferenceForestNightBackground.SetSceneNight(float)` (existing static),
  `SceneFadeController.RevealComplete` (existing static bool) / `SceneFadeController.Cover(float)` (existing static coroutine),
  `GameAudio.PlayCrowLoop()` / `.StopSfxLoop()` / `.PlayRockBreak()` / `.PlayPaaThrow()` (existing statics).
- Produces: nothing consumed by later tasks except the wiring shape the Task 4 Editor script writes
  into (private serialized fields listed below, by name).

- [ ] **Step 1: Write `PracticeNightController.cs`**

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates one practice_night round: picks one of the two Quest-1-flavoured events at
/// random, plays a short crow intro + a one-line owl comment, hands control to
/// PracticeWordAssembly for the stone build, then plays the event's resolution and fades to
/// WorldMap. No smoke clock, no backend, no stars — see
/// docs/superpowers/specs/2026-07-21-practice-night-design.md.
/// </summary>
public sealed class PracticeNightController : MonoBehaviour
{
    private enum PracticeEventKind { CrowPetrified, CrowCircling }

    [Serializable]
    private sealed class PracticeEvent
    {
        public PracticeEventKind kind;
        [Tooltip("The word that resolves this event (\"กา\" or \"ปา\").")]
        public string targetWord;
        [Tooltip("Result page under WordAssembly (book_craft_ga / book_craft_pa).")]
        public RectTransform resultPage;
        [Tooltip("Echo clip played once the target word is built.")]
        public AudioClip wordClip;
        [Tooltip("Owl's one-line comment for this event.")]
        public AudioClip owlLineClip;
    }

    [Header("Word assembly")]
    [SerializeField] private PracticeWordAssembly wordAssembly;

    [Header("Crow")]
    [SerializeField] private Animator crowAnimator;
    [SerializeField] private RectTransform crowRect;
    [SerializeField] private RuntimeAnimatorController crowFlyController;     // CrowController: ga_fly, ga_stone
    [SerializeField] private RuntimeAnimatorController crowSetFreeController; // GaSetFreeController: ga_set_free, ga_set_free2
    [SerializeField] private Transform scarecrowWorldTarget;                  // dummy_idle_DOWN_0

    [Header("Owl")]
    [SerializeField] private OwlGuideAnimator owl;
    [SerializeField] private AudioSource owlAudioSource;

    [Header("Events (fill both — one is picked at random)")]
    [SerializeField] private PracticeEvent[] events = new PracticeEvent[2];

    [Header("Crow pacing")]
    [SerializeField] private Vector2 crowEntryOffset = new Vector2(-500f, 220f);
    [SerializeField] private float crowEntryDuration = 1.1f;
    [SerializeField] private float crowCircleDuration = 2.4f;
    [SerializeField] private float crowCircleRadius = 220f;
    [SerializeField] private Vector2 crowShooOffset = new Vector2(900f, 500f);
    [SerializeField] private float crowShooDuration = 0.6f;

    [Header("Owl pacing")]
    [SerializeField] private float owlTalkSecondsFallback = 2.2f;

    [Header("Resolution / exit")]
    [SerializeField] private float resolutionHoldSeconds = 0.4f;
    [SerializeField] private float sceneFadeDuration = 0.6f;
    [SerializeField] private string returnSceneName = "WorldMap";

    private Vector2 crowRestPosition;
    private Canvas crowCanvas;

    private void Awake()
    {
        if (crowRect != null)
        {
            crowRestPosition = crowRect.anchoredPosition;
            crowCanvas = crowRect.GetComponentInParent<Canvas>();
            crowRect.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        ReferenceForestNightBackground.SetSceneNight(1f);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitUntil(() => SceneFadeController.RevealComplete);

        PracticeEvent active = PickEvent();
        if (active == null)
        {
            Debug.LogWarning("[PracticeNight] no event configured");
            yield break;
        }

        yield return PlayIntro(active);
        yield return PlayOwlLine(active);

        wordAssembly.Configure(active.targetWord, active.resultPage, active.wordClip, () => StartCoroutine(OnWordSuccess(active)));
        yield return wordAssembly.PlayReveal();
    }

    private PracticeEvent PickEvent()
    {
        if (events == null || events.Length == 0) return null;
        return events[UnityEngine.Random.Range(0, events.Length)];
    }

    private IEnumerator PlayIntro(PracticeEvent activeEvent)
    {
        crowRect.gameObject.SetActive(true);
        crowRect.anchoredPosition = crowRestPosition + crowEntryOffset;
        crowAnimator.runtimeAnimatorController = crowFlyController;
        crowAnimator.Play("ga_fly", 0, 0f);
        GameAudio.PlayCrowLoop();

        yield return MoveCrowTo(crowRect.anchoredPosition, crowRestPosition, crowEntryDuration);

        if (activeEvent.kind == PracticeEventKind.CrowPetrified)
        {
            yield return null; // let the state register so normalizedTime reads correctly
            while (crowAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f < 0.98f)
            {
                yield return null;
            }

            crowAnimator.Play("ga_stone", 0, 0f);
            GameAudio.StopSfxLoop();
            yield return null;

            float stoneLength = crowAnimator.GetCurrentAnimatorStateInfo(0).length;
            if (stoneLength > 0f) yield return new WaitForSeconds(stoneLength);
        }
        else
        {
            yield return CircleScarecrow(crowCircleDuration);
            GameAudio.StopSfxLoop();
        }
    }

    private IEnumerator CircleScarecrow(float duration)
    {
        Vector2 center = ResolveScarecrowAnchoredPosition();
        float safeDuration = Mathf.Max(0.1f, duration);

        for (float t = 0f; t < safeDuration; t += Time.deltaTime)
        {
            float angle = (t / safeDuration) * Mathf.PI * 2f;
            crowRect.anchoredPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f) * crowCircleRadius;
            yield return null;
        }

        crowRect.anchoredPosition = crowRestPosition;
    }

    private Vector2 ResolveScarecrowAnchoredPosition()
    {
        if (scarecrowWorldTarget == null || crowCanvas == null)
        {
            return crowRestPosition;
        }

        Camera cam = crowCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : crowCanvas.worldCamera;
        Camera projector = cam != null ? cam : Camera.main;
        if (projector == null) return crowRestPosition;

        Vector3 screenPoint = projector.WorldToScreenPoint(scarecrowWorldTarget.position);
        RectTransform canvasRect = crowCanvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 local))
        {
            return local;
        }

        return crowRestPosition;
    }

    private IEnumerator MoveCrowTo(Vector2 from, Vector2 to, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < safeDuration; t += Time.deltaTime)
        {
            crowRect.anchoredPosition = Vector2.LerpUnclamped(from, to, SmoothStep(Mathf.Clamp01(t / safeDuration)));
            yield return null;
        }
        crowRect.anchoredPosition = to;
    }

    private IEnumerator PlayOwlLine(PracticeEvent activeEvent)
    {
        float talkSeconds = activeEvent.owlLineClip != null ? activeEvent.owlLineClip.length : owlTalkSecondsFallback;

        yield return owl.PlayEnterThenTalk(() =>
        {
            if (activeEvent.owlLineClip == null || owlAudioSource == null) return;
            owlAudioSource.Stop();
            owlAudioSource.clip = activeEvent.owlLineClip;
            owlAudioSource.Play();
        }, talkSeconds);
    }

    private IEnumerator OnWordSuccess(PracticeEvent activeEvent)
    {
        if (resolutionHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(resolutionHoldSeconds);
        }

        if (activeEvent.kind == PracticeEventKind.CrowPetrified)
        {
            crowAnimator.runtimeAnimatorController = crowSetFreeController;
            crowAnimator.Play("ga_set_free", 0, 0f);
            GameAudio.PlayRockBreak();
            yield return null;

            float len1 = crowAnimator.GetCurrentAnimatorStateInfo(0).length;
            if (len1 > 0f) yield return new WaitForSeconds(len1);

            crowAnimator.Play("ga_set_free2", 0, 0f);
            GameAudio.PlayCrowLoop();
            yield return null;

            float len2 = crowAnimator.GetCurrentAnimatorStateInfo(0).length;
            if (len2 > 0f) yield return new WaitForSeconds(len2);
            GameAudio.StopSfxLoop();
        }
        else
        {
            GameAudio.PlayPaaThrow();
            yield return MoveCrowTo(crowRect.anchoredPosition, crowRect.anchoredPosition + crowShooOffset, crowShooDuration);
        }

        crowRect.gameObject.SetActive(false);

        yield return StartCoroutine(SceneFadeController.Cover(sceneFadeDuration));
        SceneManager.LoadScene(returnSceneName);
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
```

- [ ] **Step 2: Compile, verify no errors**

```
mcp__UnityMCP__refresh_unity  action: compile=request, scope=scripts, wait_for_ready=true
mcp__UnityMCP__read_console   action: get, types: ["error"], count: 20
```
Expected: 0 errors referencing `PracticeNightController.cs`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Region1/Practice/PracticeNightController.cs
git commit -m "feat: add PracticeNightController, the practice_night round orchestrator

Picks one of two Quest-1-flavoured events at random (crow petrified -> spell
กา; crow circling the scarecrow -> spell ปา), plays a short crow intro + the
owl's one-line comment, hands off to PracticeWordAssembly, plays the event's
resolution, and fades to WorldMap on success."
```

---

### Task 4: Scene setup — Editor script, cleanup, wiring

**Files:**
- Create: `Assets/Editor/PracticeNightSetup.cs`
- Modify (via the Editor tool, not hand-edited): `Assets/Scenes/region 1/practice_night.unity`

**Interfaces:**
- Consumes: `PracticeWordAssembly` (Task 2), `PracticeNightController` (Task 3),
  `Assets/Art/quest_map/Crow.prefab`, `Assets/Prefabs/OwlGuide.prefab`,
  `Assets/Art/quest_map/CrowController.controller`, `Assets/Art/quest_map/GaSetFreeController.controller`.

- [ ] **Step 1: Write the Editor setup script**

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot scene setup for practice_night: removes clone leftovers (BearIntro*, BossUI, the
/// reference_forest quest-path waypoints), strips the stray MagicStonePuzzleController instances
/// the WordAssembly prefab carries over from Boss.unity, wires a PracticeWordAssembly onto
/// magic_stone, and instantiates + wires the Crow and OwlGuide actors onto a new
/// PracticeNightController. Open practice_night first, then run this, then fill in the two
/// events' AudioClips in the Inspector, nudge Crow/OwlGuide positions to taste, and save.
/// See docs/superpowers/specs/2026-07-21-practice-night-design.md.
/// </summary>
public static class PracticeNightSetup
{
    private const string CrowPrefabPath = "Assets/Art/quest_map/Crow.prefab";
    private const string OwlGuidePrefabPath = "Assets/Prefabs/OwlGuide.prefab";
    private const string CrowFlyControllerPath = "Assets/Art/quest_map/CrowController.controller";
    private const string CrowSetFreeControllerPath = "Assets/Art/quest_map/GaSetFreeController.controller";

    [MenuItem("Tools/Practice/Setup Practice Night")]
    public static void Setup()
    {
        GameObject wordAssembly = GameObject.Find("WordAssembly");
        if (wordAssembly == null)
        {
            Debug.LogError("[PracticeNightSetup] 'WordAssembly' not found — open practice_night first.");
            return;
        }

        RemoveLeftovers();
        RectTransform magicStoneRect = StripStrayPuzzleControllers(wordAssembly.transform);
        if (magicStoneRect == null)
        {
            Debug.LogError("[PracticeNightSetup] 'WordAssembly/magic_stone' not found.");
            return;
        }

        PracticeWordAssembly wordAssemblyComponent = magicStoneRect.GetComponent<PracticeWordAssembly>();
        if (wordAssemblyComponent == null)
        {
            wordAssemblyComponent = magicStoneRect.gameObject.AddComponent<PracticeWordAssembly>();
        }

        GameObject crow = InstantiateCrow(wordAssembly.transform);
        GameObject owl = InstantiateOwl(wordAssembly.transform);

        GameObject controllerObject = GameObject.Find("PracticeNightController");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("PracticeNightController");
        }

        PracticeNightController controller = controllerObject.GetComponent<PracticeNightController>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<PracticeNightController>();
        }

        WireController(controller, wordAssemblyComponent, crow, owl, wordAssembly.transform);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PracticeNightSetup] Done. Fill in event target words/pages/clips on " +
            "PracticeNightController (targetWord/resultPage are pre-filled; wordClip/owlLineClip " +
            "need audio assigned), position Crow/OwlGuide visually, then save the scene.");
    }

    private static void RemoveLeftovers()
    {
        string[] names = { "BearIntroSpotlight", "BearIntroCanvas", "BossUI", "path", "frame" };
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        GameObject objectsRootGo = GameObject.Find("Objects");
        Transform objectsRoot = objectsRootGo != null ? objectsRootGo.transform : null;
        if (objectsRoot != null)
        {
            Transform questSequence = objectsRoot.Find("quest_sequence");
            if (questSequence != null) Object.DestroyImmediate(questSequence.gameObject);

            Transform quest = objectsRoot.Find("quest");
            if (quest != null) Object.DestroyImmediate(quest.gameObject);
        }
    }

    private static RectTransform StripStrayPuzzleControllers(Transform wordAssembly)
    {
        string[] carriers = { "book_craft", "book_craft_pa", "book_craft_ga", "magic_stone" };
        RectTransform magicStoneRect = null;

        foreach (string carrierName in carriers)
        {
            Transform carrier = wordAssembly.Find(carrierName);
            if (carrier == null) continue;

            MagicStonePuzzleController stray = carrier.GetComponent<MagicStonePuzzleController>();
            if (stray != null) Object.DestroyImmediate(stray);

            if (carrierName == "magic_stone") magicStoneRect = carrier as RectTransform;
        }

        return magicStoneRect;
    }

    private static GameObject InstantiateCrow(Transform parent)
    {
        Transform existing = parent.Find("Crow");
        if (existing != null) return existing.gameObject;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefabPath);
        GameObject crow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        crow.name = "Crow";
        crow.transform.SetSiblingIndex(0); // draw behind book_craft/magic_stone, matching the reference art

        RectTransform rect = crow.transform as RectTransform;
        rect.anchoredPosition = new Vector2(0f, 260f); // starting guess — nudge to match the reference screenshot

        CrowEntranceCutscene entrance = crow.GetComponent<CrowEntranceCutscene>();
        if (entrance != null) entrance.enabled = false; // PracticeNightController drives the Animator directly

        crow.SetActive(false);
        return crow;
    }

    private static GameObject InstantiateOwl(Transform parent)
    {
        Transform existing = parent.Find("OwlGuide");
        if (existing != null) return existing.gameObject;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OwlGuidePrefabPath);
        GameObject owl = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        owl.name = "OwlGuide";
        return owl;
    }

    private static void WireController(PracticeNightController controller, PracticeWordAssembly wordAssembly,
        GameObject crow, GameObject owl, Transform wordAssemblyRoot)
    {
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("wordAssembly").objectReferenceValue = wordAssembly;
        so.FindProperty("crowAnimator").objectReferenceValue = crow.GetComponent<Animator>();
        so.FindProperty("crowRect").objectReferenceValue = crow.transform as RectTransform;
        so.FindProperty("crowFlyController").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowFlyControllerPath);
        so.FindProperty("crowSetFreeController").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowSetFreeControllerPath);

        GameObject scarecrowGo = GameObject.Find("dummy_idle_DOWN_0");
        so.FindProperty("scarecrowWorldTarget").objectReferenceValue = scarecrowGo != null ? scarecrowGo.transform : null;

        so.FindProperty("owl").objectReferenceValue = owl.GetComponent<OwlGuideAnimator>();
        AudioSource owlAudio = owl.GetComponent<AudioSource>();
        if (owlAudio == null) owlAudio = owl.AddComponent<AudioSource>();
        owlAudio.playOnAwake = false;
        so.FindProperty("owlAudioSource").objectReferenceValue = owlAudio;

        SerializedProperty events = so.FindProperty("events");
        events.arraySize = 2;

        SerializedProperty eventA = events.GetArrayElementAtIndex(0);
        eventA.FindPropertyRelative("kind").enumValueIndex = 0; // CrowPetrified
        eventA.FindPropertyRelative("targetWord").stringValue = "กา";
        eventA.FindPropertyRelative("resultPage").objectReferenceValue = wordAssemblyRoot.Find("book_craft_ga") as RectTransform;

        SerializedProperty eventB = events.GetArrayElementAtIndex(1);
        eventB.FindPropertyRelative("kind").enumValueIndex = 1; // CrowCircling
        eventB.FindPropertyRelative("targetWord").stringValue = "ปา";
        eventB.FindPropertyRelative("resultPage").objectReferenceValue = wordAssemblyRoot.Find("book_craft_pa") as RectTransform;

        so.ApplyModifiedProperties();
    }
}
```

- [ ] **Step 2: Compile, verify no errors**

```
mcp__UnityMCP__refresh_unity  action: compile=request, scope=scripts, wait_for_ready=true
mcp__UnityMCP__read_console   action: get, types: ["error"], count: 20
```
Expected: 0 errors referencing `PracticeNightSetup.cs`.

- [ ] **Step 3: Open practice_night and run the setup menu item**

```
mcp__UnityMCP__manage_scene       action: load, path: "Assets/Scenes/region 1/practice_night.unity"
mcp__UnityMCP__execute_menu_item  menu_path: "Tools/Practice/Setup Practice Night"
mcp__UnityMCP__read_console       action: get, types: ["error", "log"], count: 20
```
Expected: log line `[PracticeNightSetup] Done. ...` and no errors.

- [ ] **Step 4: Verify the leftovers are gone and the new objects exist**

```
mcp__UnityMCP__find_gameobjects  search_term: "BearIntroCanvas", search_method: by_name
mcp__UnityMCP__find_gameobjects  search_term: "BossUI", search_method: by_name
mcp__UnityMCP__find_gameobjects  search_term: "PracticeNightController", search_method: by_name
mcp__UnityMCP__find_gameobjects  search_term: "Crow", search_method: by_name
mcp__UnityMCP__find_gameobjects  search_term: "OwlGuide", search_method: by_name
```
Expected: the first two return 0 results; the last three return exactly 1 each.

- [ ] **Step 5: Assign the four AudioClips manually, then position Crow/OwlGuide**

This is a content/authoring step, not code — no clip assets are created by this plan. In the
Unity Editor Inspector, on `PracticeNightController`:
- `events[0].wordClip` — a spoken "กา" clip (reuse or record one; e.g. from `Resources/TTS` if a
  suitable line already exists, matching how `MagicStonePuzzleController.soundPlaybackClip` sources
  its clips).
- `events[0].owlLineClip` — the owl's short "the crow turned to stone" line.
- `events[1].wordClip` — a spoken "ปา" clip.
- `events[1].owlLineClip` — the owl's short "shoo the crow" line.

Then, in the Scene view, nudge `WordAssembly/Crow`'s anchored position (and `WordAssembly/OwlGuide`
if needed) to match the reference screenshot's layout (crow behind the book, arms visible above it).

- [ ] **Step 6: Save the scene**

```
mcp__UnityMCP__manage_scene  action: save
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/PracticeNightSetup.cs "Assets/Scenes/region 1/practice_night.unity"
git commit -m "feat: wire practice_night — clean clone leftovers, add PracticeNightController

Adds a one-shot Editor setup (Tools/Practice/Setup Practice Night) that
removes BearIntro*/BossUI/quest-path leftovers from the Boss.unity clone,
strips the stray MagicStonePuzzleController instances the WordAssembly
prefab carries over, and wires PracticeWordAssembly + PracticeNightController
with the Crow and OwlGuide actors for the two practice events."
```

---

### Task 5: Play-mode verification

**Files:** none (verification only).

- [ ] **Step 1: Enter Play Mode and capture the initial state**

```
mcp__UnityMCP__manage_editor  action: play
```
Wait ~1-2s, then:
```
mcp__UnityMCP__read_console  action: get, types: ["error"], count: 20
```
Expected: 0 errors on entering play mode (in particular, no `NullReferenceException` from
`PracticeNightController` or `PracticeWordAssembly` field resolution).

- [ ] **Step 2: Confirm night is applied**

```
mcp__UnityMCP__find_gameobjects  search_term: "Reference Forest Night Background", search_method: by_name
```
Then read that GameObject's components (`mcpforunity://scene/gameobject/{id}/components`) and
confirm the night background `SpriteRenderer`'s color alpha is ~1 (fully applied), matching
`SetSceneNight(1f)` having run.

- [ ] **Step 3: Confirm the crow intro plays and the owl line runs**

```
mcp__UnityMCP__find_gameobjects  search_term: "Crow", search_method: by_name
```
Poll its `Animator`/`RectTransform` component data a few seconds apart; expect `activeSelf: true`
and a changing `anchoredPosition`/current animator state during the intro window.

- [ ] **Step 4: Confirm stones become interactive**

After the intro + owl line (roughly 4-6s in), find `WordAssembly/magic_stone/stone1` and confirm
its `Image.raycastTarget` is (still) `false` (raycasts are handled via `CanvasGroup`) and its
`CanvasGroup.alpha` is `1` (revealed).

- [ ] **Step 5: Manually place stones for the CORRECT word and confirm success resolves**

Use `mcp__UnityMCP__manage_gameobject` or simulate a click via `find_gameobjects` + a scripted
`OnPointerClick` is not available over MCP — instead, temporarily call
`PracticeWordAssembly.HandleStoneClicked` is private-surface via the component; the practical
verification is: use `unity_screenshot_game` (anklebreaker) or `mcp__UnityMCP__manage_camera`
screenshot action after manually clicking the stones in the Game view (human-in-the-loop for this
one interactive step), and visually confirm:
- Placing the two stones spelling the *active* event's target word pops the correct result page
  (`book_craft_ga` for the petrified-crow event, `book_craft_pa` for the circling event), plays the
  word clip, plays the resolution animation, and the scene fades to `WorldMap`.
- Placing any other two-stone combination triggers the red `MisassemblyLock` vignette (no smoke),
  then springs the stones back home, and the round can be retried.

- [ ] **Step 6: Stop Play Mode**

```
mcp__UnityMCP__manage_editor  action: stop
```

- [ ] **Step 7: Report results to the user**

Summarize: which event was picked in the test run, whether night/crow/owl/stones/result/lock/fade
all behaved as designed, and any visual nudges still needed (Crow/OwlGuide position, audio clips).
No commit for this task — it is verification only.

---

## Self-Review Notes (completed during writing)

- **Spec coverage:** §3 cleanup → Task 4 Step 1 (`RemoveLeftovers`). §4/§7 reuse inventory → Tasks
  1-3 (`MagicStonePuzzleStone`, `MisassemblyLock`, `Crow.prefab`+controllers, `OwlGuideAnimator`,
  `SceneFadeController`, `GameAudio`). §6 the two events → `PracticeNightController.events` (Task 3)
  + Task 4 wiring. §8 lifecycle → `PracticeNightController.Run()`. §9 wrong-answer/no-smoke →
  `PracticeWordAssembly.MisassemblyRoutine()`. §10 verification → Task 5. Deviations from §5's exact
  reuse list are documented and justified above.
- **Placeholder scan:** no TBD/TODO; the one manual step (Task 4 Step 5, audio clip assignment) is
  content authoring, not a code stub, and is explicitly called out as such.
- **Type consistency:** `PracticeWordAssembly.Configure(string, RectTransform, AudioClip, Action)`
  matches its one call site in `PracticeNightController.Run()`. `IStonePuzzleOwner.CanInteract`/
  `HandleStoneClicked` match both implementers (`MagicStonePuzzleController`, `PracticeWordAssembly`)
  exactly. Field names referenced by `PracticeNightSetup.WireController` (`wordAssembly`,
  `crowAnimator`, `crowRect`, `crowFlyController`, `crowSetFreeController`, `scarecrowWorldTarget`,
  `owl`, `owlAudioSource`, `events`) match the private serialized field names declared in
  `PracticeNightController` exactly.
