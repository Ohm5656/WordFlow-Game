# Star Scoring & Award Animation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A 0-3 star verdict for the word-build round, with a juicy pop → burst → fly → stamp award animation, shown live in `CutScene_bear` and recapped in `Success_pa` / `Success_ga`.

**Architecture:** One new runtime component (`StarHud`) drives everything. The star visuals move out from under `star_root` (which still hosts the untouchable `WordAssemblyTimer`) into a new sibling `star_hud`, because the timer blanks its own GameObject's `CanvasGroup` on `Pause()` — which fires exactly when the first star is earned. `MagicStonePuzzleController` gains two `StarHud.Instance?.…` calls. Star count crosses scenes in one `PlayerPrefs` int.

**Tech Stack:** Unity (built-in RP), UGUI ScreenSpaceOverlay Canvas, coroutine tweens, Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`).

Spec: `docs/superpowers/specs/2026-07-13-star-scoring-design.md`

## Scoring rules (the whole logic, in one table)

| Star | Condition | Awarded at |
|---|---|---|
| 1 | any valid word assembled (ปา **or** กา). **No time condition.** | `WordResultRoutine` |
| 2 | the word was **correct** (ปา) | `RecordingSuccessRoutine` |
| 3 | correct **and** assembled while the clock still had time (`SmokeRemaining > 0`) | same moment as star 2 |

Totals: **1** = wrong word · **2** = correct but late · **3** = correct and in time.
Stars **reset** on a retry (`CutScene_bear` clears the count in `Awake`). The *clock* still resumes — that is existing behaviour, do not touch it.

## Global Constraints

- **Never modify** `Assets/Scripts/Region1/Cutscenes/WordAssemblyTimer.cs`, `Assets/Scripts/Common/FogController.cs`, `EdgeFox.shader`, `EdgeFogMaterial.mat`, `CloudFogMaterial.mat`. The smoke clock is off-limits. Everything the stars need from the timer is already public: `WordAssemblyTimer.Instance.SmokeRemaining`.
- **Do not delete or reparent `star_root` itself**, and do not touch the `WordAssemblyTimer` / `AudioSource` components on it. Only its *children* move.
- `StarHud` must be **null-safe from the call site**: `CutScene_ga` and `CutScene_ta` share `MagicStonePuzzleController` but have no star board. Every hook is `StarHud.Instance?.…`.
- **Object names in `star_root` have irregular internal spaces** — `effect_panel3 `, `effect_panel3  (1)`, `effect_panel4  (2)`. Never match names raw. Normalise by stripping **all** whitespace first (`"effect_panel3  (1)"` → `"effect_panel3(1)"`), then match.
- Every scene edit happens atomically inside **one** editor script ending in `EditorSceneManager.SaveScene`. Scene objects created across separate MCP calls vanish on domain reload.
- `mcp__anklebreaker__unity_execute_code` does not work in this project. Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- **Run `Assets/Refresh` and wait for `isCompiling: false` before invoking a menu item you just wrote** — otherwise Unity runs the *old* compiled code and the menu silently does the wrong thing. (This bit us last session.)
- Before any MCP call: `unity_list_instances` then `unity_select_instance`. **The port hops between 7890 and 7891 on domain reload.** Pass `port:` on every call.
- **Open the Game view (`Window/General/Game`) before every screenshot batch** or `unity_screenshot_game` silently returns a flat ~100KB image with no UI. Screenshots land ~60-75s later — poll for the file and check it is multi-MB.
- The `[Telemetry] 404` / `[Session] open failed` ngrok warnings are pre-existing. Ignore them.
- Commit after each task.

---

### Task 1: The `StarHud` runtime component

**Files:**
- Create: `Assets/Scripts/Region1/Stars/StarHud.cs`

**Interfaces:**
- Consumes: nothing at runtime (pure UI + `PlayerPrefs`).
- Produces — the API Tasks 3 and 4 call:
  - `static StarHud Instance { get; }`
  - `IEnumerator AwardRoutine(int count)` — plays `count` award animations back to back and persists the new total. Yield on it.
  - `static int EarnedCount { get; }` — reads the persisted total.
  - Serialized flags used by the Success scenes: `resetOnAwake`, `recapOnStart`, `fadeOutAfterRecap`.

- [ ] **Step 1: Write the component**

Create `Assets/Scripts/Region1/Stars/StarHud.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// The 0-3 star board for the word-build round.
///
/// Lives on `star_hud` — a sibling of `star_root`, NOT a child of it. star_root still carries
/// WordAssemblyTimer, and the timer blanks its own GameObject's CanvasGroup on Pause() — which fires
/// the instant a word is built. Parented under it, every star would vanish the moment it was earned.
///
/// Three usages, all the same component:
///  - CutScene_bear : resetOnAwake = true               -> awards play live via AwardRoutine()
///  - Success_pa    : recapOnStart = true               -> the earned stars pop in while the owl praises
///  - Success_ga    : recapOnStart + fadeOutAfterRecap  -> the single star shows, then fades = "reset"
public sealed class StarHud : MonoBehaviour
{
    private const string EarnedKey = "StarEarnedCount";
    public const int MaxStars = 3;

    [System.Serializable]
    public sealed class Slot
    {
        [Tooltip("star_end / star_end (1) / star_end (2) — the resting place. Position AND size are read from this.")]
        public RectTransform end;

        [Tooltip("effect_panel1 for this slot — the impact stamp.")]
        public Image stamp;

        [Tooltip("effect_panel2..5 for this slot. Their authored positions are the scatter destinations.")]
        public Image[] burst;
    }

    [Header("Board")]
    [SerializeField] private RectTransform starFrame;

    [Tooltip("The star that pops in at screen centre and then flies to a slot.")]
    [SerializeField] private RectTransform starStart;

    [Tooltip("effect_star1..4 — the centre burst. Authored positions are the scatter destinations.")]
    [SerializeField] private Image[] centreBurst;

    [Tooltip("Slots 0,1,2 left to right.")]
    [SerializeField] private Slot[] slots = new Slot[MaxStars];

    [Header("Mode")]
    [Tooltip("CutScene_bear: clear the star count on entry, so stars never survive a retry.")]
    [SerializeField] private bool resetOnAwake;

    [Tooltip("Success scenes: pop the already-earned stars straight into their slots (no fly).")]
    [SerializeField] private bool recapOnStart;

    [Tooltip("Success_ga: after the recap, fade the whole board out — the 'your stars reset' beat.")]
    [SerializeField] private bool fadeOutAfterRecap;

    [Header("Timing — pop")]
    [SerializeField, Min(0.01f)] private float popDuration = 0.35f;
    [SerializeField] private float popOvershoot = 1.7f;
    [SerializeField, Min(0f)] private float holdAfterPop = 0.3f;

    [Header("Timing — burst")]
    [SerializeField, Min(0.01f)] private float burstOutDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float burstFadeDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float burstStartScale = 0.3f;
    [SerializeField, Min(0.01f)] private float burstEndScale = 1.15f;

    [Header("Timing — fly")]
    [SerializeField, Min(0.01f)] private float flyDuration = 0.6f;
    [SerializeField] private float flyArcHeight = 260f;
    [SerializeField] private float flySpinDegrees = 360f;
    [SerializeField, Min(0f)] private float anticipationDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float anticipationScale = 0.85f;

    [Header("Timing — stamp")]
    [SerializeField, Min(0.01f)] private float stampDuration = 0.32f;
    [SerializeField, Min(0.01f)] private float stampStartScale = 0.55f;
    [SerializeField, Min(0.01f)] private float stampEndScale = 1.5f;
    [SerializeField, Min(0.01f)] private float landPopDuration = 0.25f;
    [SerializeField] private float landOvershoot = 1.9f;

    [Header("Sequencing")]
    [Tooltip("Gap between two stars awarded in the same breath (the correct + in-time case).")]
    [SerializeField, Min(0f)] private float gapBetweenStars = 0.25f;

    [Header("Recap (Success scenes)")]
    [SerializeField, Min(0f)] private float recapStartDelay = 0.6f;
    [SerializeField, Min(0f)] private float recapGap = 0.15f;
    [SerializeField, Min(0f)] private float recapHoldBeforeFade = 1.2f;
    [SerializeField, Min(0.01f)] private float recapFadeDuration = 0.6f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip starEarnedSfx;

    public static StarHud Instance { get; private set; }

    public static int EarnedCount => Mathf.Clamp(PlayerPrefs.GetInt(EarnedKey, 0), 0, MaxStars);

    // Cached authored transforms — the scene layout IS the animation's target data.
    private Vector2 startPos;
    private Vector2 startSize;
    private Vector3 startScale;
    private Vector2[] centreBurstPos;
    private Vector3[] centreBurstScale;
    private Vector2[][] slotBurstPos;
    private Vector3[][] slotBurstScale;

    private int shown; // stars currently sitting in slots

    private void Awake()
    {
        Instance = this;
        CacheAuthoredLayout();
        HideAllEffects();

        if (resetOnAwake)
        {
            // Stars never survive a retry: entering CutScene_bear (fresh or returning from Success_ga)
            // always starts the board empty. The CLOCK still resumes — that is the timer's business.
            PlayerPrefs.SetInt(EarnedKey, 0);
            PlayerPrefs.Save();
        }

        shown = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (recapOnStart) StartCoroutine(RecapRoutine());
    }

    private void CacheAuthoredLayout()
    {
        if (starStart != null)
        {
            startPos = starStart.anchoredPosition;
            startSize = starStart.sizeDelta;
            startScale = starStart.localScale;
        }

        int n = centreBurst != null ? centreBurst.Length : 0;
        centreBurstPos = new Vector2[n];
        centreBurstScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            RectTransform r = Rect(centreBurst[i]);
            if (r == null) continue;
            centreBurstPos[i] = r.anchoredPosition;
            centreBurstScale[i] = r.localScale;
        }

        int s = slots != null ? slots.Length : 0;
        slotBurstPos = new Vector2[s][];
        slotBurstScale = new Vector3[s][];
        for (int i = 0; i < s; i++)
        {
            Image[] burst = slots[i] != null ? slots[i].burst : null;
            int b = burst != null ? burst.Length : 0;
            slotBurstPos[i] = new Vector2[b];
            slotBurstScale[i] = new Vector3[b];
            for (int j = 0; j < b; j++)
            {
                RectTransform r = Rect(burst[j]);
                if (r == null) continue;
                slotBurstPos[i][j] = r.anchoredPosition;
                slotBurstScale[i][j] = r.localScale;
            }
        }
    }

    private void HideAllEffects()
    {
        // star_frame is the only thing visible before a star is earned. Force it here rather than
        // trusting scene state — the designer leaves these active while authoring.
        SetActive(starStart, false);

        if (centreBurst != null)
        {
            for (int i = 0; i < centreBurst.Length; i++) SetActive(centreBurst[i], false);
        }

        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            SetActive(slots[i].end, false);
            SetActive(slots[i].stamp, false);
            if (slots[i].burst == null) continue;
            for (int j = 0; j < slots[i].burst.Length; j++) SetActive(slots[i].burst[j], false);
        }
    }

    /// Play `count` award animations back to back and persist the new total. Yield on this.
    public IEnumerator AwardRoutine(int count)
    {
        for (int k = 0; k < count; k++)
        {
            if (shown >= MaxStars || shown >= slots.Length) yield break;

            yield return AwardOneRoutine(shown);

            shown++;
            PlayerPrefs.SetInt(EarnedKey, shown);
            PlayerPrefs.Save();

            if (k < count - 1 && gapBetweenStars > 0f)
            {
                yield return new WaitForSeconds(gapBetweenStars);
            }
        }
    }

    private IEnumerator AwardOneRoutine(int slotIndex)
    {
        Slot slot = slots[slotIndex];
        if (slot == null || slot.end == null || starStart == null) yield break;

        PlaySfx();

        // --- 1. pop in at centre + centre burst -------------------------------------------------
        starStart.anchoredPosition = startPos;
        starStart.sizeDelta = startSize;
        starStart.localRotation = Quaternion.identity;
        SetActive(starStart, true);
        SetAlpha(starStart, 0f);

        StartCoroutine(CentreBurstRoutine());

        for (float t = 0f; t < popDuration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / popDuration);
            starStart.localScale = Vector3.LerpUnclamped(Vector3.zero, startScale, EaseOutBack(k, popOvershoot));
            SetAlpha(starStart, Mathf.Clamp01(k * 2f));
            yield return null;
        }
        starStart.localScale = startScale;
        SetAlpha(starStart, 1f);

        if (holdAfterPop > 0f) yield return new WaitForSeconds(holdAfterPop);

        // --- 2. anticipation dip ----------------------------------------------------------------
        for (float t = 0f; t < anticipationDuration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, anticipationDuration));
            starStart.localScale = Vector3.LerpUnclamped(startScale, startScale * anticipationScale, k);
            yield return null;
        }

        // --- 3. fly to the slot on an arc -------------------------------------------------------
        Vector2 endPos = slot.end.anchoredPosition;
        Vector2 endSize = slot.end.sizeDelta;
        Vector3 endScale = slot.end.localScale;

        // Control point lifted above the midpoint: a straight lerp reads as a UI tween, an arc reads
        // as a thing being thrown.
        Vector2 control = (startPos + endPos) * 0.5f + Vector2.up * flyArcHeight;

        for (float t = 0f; t < flyDuration; t += Time.deltaTime)
        {
            float k = SmoothStep(Mathf.Clamp01(t / flyDuration));
            starStart.anchoredPosition = Bezier(startPos, control, endPos, k);
            starStart.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, k);
            starStart.localScale = Vector3.LerpUnclamped(startScale * anticipationScale, endScale, k);
            starStart.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(0f, flySpinDegrees, k));
            yield return null;
        }

        starStart.localRotation = Quaternion.identity;
        SetActive(starStart, false);

        // --- 4. land: stamp the slot star + slot effects -----------------------------------------
        SetActive(slot.end, true);
        SetAlpha(slot.end, 1f);

        StartCoroutine(StampRoutine(slot, slotIndex));

        for (float t = 0f; t < landPopDuration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / landPopDuration);
            slot.end.localScale = Vector3.LerpUnclamped(endScale * 1.25f, endScale, EaseOutBack(k, landOvershoot));
            yield return null;
        }
        slot.end.localScale = endScale;
    }

    private IEnumerator CentreBurstRoutine()
    {
        if (centreBurst == null) yield break;

        for (int i = 0; i < centreBurst.Length; i++)
        {
            RectTransform r = Rect(centreBurst[i]);
            if (r == null) continue;
            r.anchoredPosition = startPos;
            r.localScale = centreBurstScale[i] * burstStartScale;
            SetActive(centreBurst[i], true);
            SetAlpha(centreBurst[i], 1f);
        }

        // Fly out from the star to the authored scatter positions.
        for (float t = 0f; t < burstOutDuration; t += Time.deltaTime)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / burstOutDuration));
            for (int i = 0; i < centreBurst.Length; i++)
            {
                RectTransform r = Rect(centreBurst[i]);
                if (r == null) continue;
                r.anchoredPosition = Vector2.LerpUnclamped(startPos, centreBurstPos[i], k);
                r.localScale = Vector3.LerpUnclamped(centreBurstScale[i] * burstStartScale,
                                                     centreBurstScale[i] * burstEndScale, k);
            }
            yield return null;
        }

        // Then fade out where they land.
        for (float t = 0f; t < burstFadeDuration; t += Time.deltaTime)
        {
            float a = 1f - Mathf.Clamp01(t / burstFadeDuration);
            for (int i = 0; i < centreBurst.Length; i++) SetAlpha(centreBurst[i], a);
            yield return null;
        }

        for (int i = 0; i < centreBurst.Length; i++)
        {
            SetAlpha(centreBurst[i], 1f);
            SetActive(centreBurst[i], false);
        }
    }

    private IEnumerator StampRoutine(Slot slot, int slotIndex)
    {
        // effect_panel1 = the impact stamp: a ring that expands out of the landing and fades.
        if (slot.stamp != null)
        {
            RectTransform sr = Rect(slot.stamp);
            Vector3 baseScale = sr != null ? sr.localScale : Vector3.one;
            if (sr != null)
            {
                sr.localScale = baseScale * stampStartScale;
                SetActive(slot.stamp, true);
                SetAlpha(slot.stamp, 1f);
            }

            StartCoroutine(SlotBurstRoutine(slot, slotIndex));

            for (float t = 0f; t < stampDuration; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / stampDuration);
                if (sr != null)
                {
                    sr.localScale = Vector3.LerpUnclamped(baseScale * stampStartScale, baseScale * stampEndScale, EaseOutCubic(k));
                }
                SetAlpha(slot.stamp, 1f - k);
                yield return null;
            }

            SetAlpha(slot.stamp, 1f);
            SetActive(slot.stamp, false);
            if (sr != null) sr.localScale = baseScale;
        }
        else
        {
            yield return SlotBurstRoutine(slot, slotIndex);
        }
    }

    private IEnumerator SlotBurstRoutine(Slot slot, int slotIndex)
    {
        Image[] burst = slot.burst;
        if (burst == null || burst.Length == 0) yield break;

        Vector2 origin = slot.end != null ? slot.end.anchoredPosition : Vector2.zero;
        Vector2[] dest = slotBurstPos[slotIndex];
        Vector3[] destScale = slotBurstScale[slotIndex];

        for (int i = 0; i < burst.Length; i++)
        {
            RectTransform r = Rect(burst[i]);
            if (r == null) continue;
            r.anchoredPosition = origin;
            r.localScale = destScale[i] * burstStartScale;
            SetActive(burst[i], true);
            SetAlpha(burst[i], 1f);
        }

        for (float t = 0f; t < burstOutDuration; t += Time.deltaTime)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / burstOutDuration));
            for (int i = 0; i < burst.Length; i++)
            {
                RectTransform r = Rect(burst[i]);
                if (r == null) continue;
                r.anchoredPosition = Vector2.LerpUnclamped(origin, dest[i], k);
                r.localScale = Vector3.LerpUnclamped(destScale[i] * burstStartScale, destScale[i] * burstEndScale, k);
            }
            yield return null;
        }

        for (float t = 0f; t < burstFadeDuration; t += Time.deltaTime)
        {
            float a = 1f - Mathf.Clamp01(t / burstFadeDuration);
            for (int i = 0; i < burst.Length; i++) SetAlpha(burst[i], a);
            yield return null;
        }

        for (int i = 0; i < burst.Length; i++)
        {
            SetAlpha(burst[i], 1f);
            SetActive(burst[i], false);
            RectTransform r = Rect(burst[i]);
            if (r != null) r.localScale = destScale[i];
        }
    }

    /// Success scenes: the stars were already flown in CutScene_bear, so here they just pop into
    /// their slots as a recap while the owl/crow does its beat.
    private IEnumerator RecapRoutine()
    {
        int earned = EarnedCount;
        if (earned <= 0)
        {
            if (fadeOutAfterRecap) yield break;
            yield break;
        }

        if (recapStartDelay > 0f) yield return new WaitForSeconds(recapStartDelay);

        for (int i = 0; i < earned && i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.end == null) continue;

            PlaySfx();

            Vector3 endScale = slot.end.localScale;
            SetActive(slot.end, true);
            SetAlpha(slot.end, 1f);
            StartCoroutine(StampRoutine(slot, i));

            for (float t = 0f; t < landPopDuration; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / landPopDuration);
                slot.end.localScale = Vector3.LerpUnclamped(Vector3.zero, endScale, EaseOutBack(k, landOvershoot));
                yield return null;
            }
            slot.end.localScale = endScale;
            shown = i + 1;

            if (recapGap > 0f) yield return new WaitForSeconds(recapGap);
        }

        if (!fadeOutAfterRecap) yield break;

        // Success_ga: the single star fades away — the "your stars reset, try again" beat.
        if (recapHoldBeforeFade > 0f) yield return new WaitForSeconds(recapHoldBeforeFade);

        for (float t = 0f; t < recapFadeDuration; t += Time.deltaTime)
        {
            float a = 1f - Mathf.Clamp01(t / recapFadeDuration);
            for (int i = 0; i < earned && i < slots.Length; i++)
            {
                if (slots[i] != null) SetAlpha(slots[i].end, a);
            }
            yield return null;
        }

        for (int i = 0; i < earned && i < slots.Length; i++)
        {
            if (slots[i] != null) SetActive(slots[i].end, false);
        }
    }

    private void PlaySfx()
    {
        if (starEarnedSfx == null) return;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null) audioSource.PlayOneShot(starEarnedSfx);
    }

    // --- small helpers -------------------------------------------------------------------------

    private static RectTransform Rect(Component c) => c != null ? c.transform as RectTransform : null;

    private static void SetActive(Component c, bool on)
    {
        if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void SetAlpha(Component c, float a)
    {
        if (c == null) return;
        Graphic g = c.GetComponent<Graphic>();
        if (g == null) return;
        Color col = g.color;
        col.a = Mathf.Clamp01(a);
        g.color = col;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

    private static float EaseOutBack(float t, float overshoot)
    {
        float s = t - 1f;
        return 1f + s * s * ((overshoot + 1f) * s + overshoot);
    }
}
```

- [ ] **Step 2: Confirm it compiles**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance          port: <from the list>
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors, `isCompiling: false`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Region1/Stars/StarHud.cs Assets/Scripts/Region1/Stars/StarHud.cs.meta
git commit -m "feat(stars): add the StarHud board — pop, burst, fly, stamp"
```

---

### Task 2: Move the star visuals out of `star_root` into `star_hud`

**Files:**
- Create: `Assets/Editor/BuildStarHud.cs`
- Modifies (via the tool, not by hand): `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Consumes: `StarHud` from Task 1; the authored children of `star_root`.
- Produces: menu item `Tools/Quest/Build Star Hud`; a `star_hud` GameObject under the same Canvas, holding every star visual and a fully-wired `StarHud` component with `resetOnAwake = true`. `star_root` keeps `WordAssemblyTimer` + `AudioSource` and is left otherwise empty.

**Why:** `WordAssemblyTimer.Pause()` (fired the instant a word is built) calls `HideBoard()` → `CanvasGroup.alpha = 0` **on its own GameObject**, `star_root`. Any star parented under it would be awarded and instantly blanked.

- [ ] **Step 1: Write the editor tool**

Create `Assets/Editor/BuildStarHud.cs`:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Build Star Hud
/// Moves the star visuals out from under star_root (which still hosts WordAssemblyTimer — the timer
/// blanks its own CanvasGroup on Pause(), which fires the instant a word is built, so a star parented
/// under it would vanish the moment it was earned) into a sibling `star_hud`, and wires up StarHud.
/// Idempotent: re-running re-wires the existing star_hud instead of building a second one.
public static class BuildStarHud
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string HudName = "star_hud";

    [MenuItem("Tools/Quest/Build Star Hud")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform starRoot = FindInScene("star_root");
        if (starRoot == null) { Debug.LogError("[BuildStarHud] star_root not found"); return; }

        Transform canvas = starRoot.parent;
        if (canvas == null) { Debug.LogError("[BuildStarHud] star_root has no parent Canvas"); return; }

        // --- the star_hud object: same RectTransform as star_root, same sibling slot -------------
        Transform hudT = canvas.Find(HudName);
        GameObject hud = hudT != null ? hudT.gameObject : new GameObject(HudName, typeof(RectTransform));
        if (hudT == null)
        {
            Undo.RegisterCreatedObjectUndo(hud, "Build Star Hud");
            hud.transform.SetParent(canvas, false);
        }

        var srcRect = (RectTransform)starRoot;
        var hudRect = (RectTransform)hud.transform;
        hudRect.anchorMin = srcRect.anchorMin;
        hudRect.anchorMax = srcRect.anchorMax;
        hudRect.pivot = srcRect.pivot;
        hudRect.anchoredPosition = srcRect.anchoredPosition;
        hudRect.sizeDelta = srcRect.sizeDelta;
        hudRect.localScale = srcRect.localScale;
        hudRect.localRotation = srcRect.localRotation;
        hud.layer = starRoot.gameObject.layer;

        // Draw on top of everything else in the Canvas, like the timer board did.
        hud.transform.SetSiblingIndex(canvas.childCount - 1);

        // --- move the children across, preserving their authored local layout --------------------
        // star_hud's RectTransform is identical to star_root's, so the local values map 1:1 — copy
        // them out, reparent without world-position math, copy them back.
        var kids = new List<RectTransform>();
        for (int i = starRoot.childCount - 1; i >= 0; i--)
        {
            var r = starRoot.GetChild(i) as RectTransform;
            if (r != null) kids.Add(r);
        }
        kids.Reverse();

        foreach (var k in kids)
        {
            Vector2 aMin = k.anchorMin, aMax = k.anchorMax, piv = k.pivot;
            Vector2 pos = k.anchoredPosition, size = k.sizeDelta;
            Vector3 scale = k.localScale;
            Quaternion rot = k.localRotation;

            Undo.SetTransformParent(k, hud.transform, "Build Star Hud");
            k.SetParent(hud.transform, false);

            k.anchorMin = aMin; k.anchorMax = aMax; k.pivot = piv;
            k.anchoredPosition = pos; k.sizeDelta = size;
            k.localScale = scale; k.localRotation = rot;
        }

        // --- wire StarHud ------------------------------------------------------------------------
        var star = hud.GetComponent<StarHud>();
        if (star == null) star = Undo.AddComponent<StarHud>(hud);

        var map = BuildNameMap(hud.transform);

        var so = new SerializedObject(star);
        so.FindProperty("starFrame").objectReferenceValue = Get<RectTransform>(map, "star_frame");
        so.FindProperty("starStart").objectReferenceValue = Get<RectTransform>(map, "star_start");
        so.FindProperty("resetOnAwake").boolValue = true;   // CutScene_bear: stars never survive a retry
        so.FindProperty("recapOnStart").boolValue = false;
        so.FindProperty("fadeOutAfterRecap").boolValue = false;

        // effect_star1..4 — the centre burst
        var centre = so.FindProperty("centreBurst");
        centre.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            centre.GetArrayElementAtIndex(i).objectReferenceValue = Get<Image>(map, $"effect_star{i + 1}");
        }

        // slots 0,1,2: star_end / star_end(1) / star_end(2), stamp = effect_panel1(i), burst = panels 2..5
        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            string suffix = i == 0 ? "" : $"({i})";

            var slot = slotsProp.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("end").objectReferenceValue = Get<RectTransform>(map, $"star_end{suffix}");
            slot.FindPropertyRelative("stamp").objectReferenceValue = Get<Image>(map, $"effect_panel1{suffix}");

            var burst = slot.FindPropertyRelative("burst");
            burst.arraySize = 4;
            for (int p = 2; p <= 5; p++)
            {
                burst.GetArrayElementAtIndex(p - 2).objectReferenceValue = Get<Image>(map, $"effect_panel{p}{suffix}");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildStarHud] DONE — {HudName} holds {hud.transform.childCount} children, " +
                  $"star_root kept {starRoot.childCount} (should be 0), scene saved");
    }

    /// The authored names have irregular internal spacing — "effect_panel3 ", "effect_panel3  (1)",
    /// "effect_panel4  (2)". Key every child by its name with ALL whitespace stripped, so a lookup of
    /// "effect_panel3(1)" finds it no matter how many spaces the designer left in.
    static Dictionary<string, Transform> BuildNameMap(Transform root)
    {
        var map = new Dictionary<string, Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            string key = Regex.Replace(c.name, @"\s+", "");
            map[key] = c;   // last one wins; names are unique after normalising
        }
        return map;
    }

    static T Get<T>(Dictionary<string, Transform> map, string normalisedName) where T : Component
    {
        if (!map.TryGetValue(normalisedName, out Transform t) || t == null)
        {
            Debug.LogError($"[BuildStarHud] missing child: {normalisedName}");
            return null;
        }

        T c = typeof(T) == typeof(RectTransform) ? t as T : t.GetComponent<T>();
        if (c == null) Debug.LogError($"[BuildStarHud] {normalisedName} has no {typeof(T).Name}");
        return c;
    }

    static Transform FindInScene(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name) return go.transform;
        }
        return null;
    }
}
```

- [ ] **Step 2: Compile, then run the tool**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Wait for `isCompiling: false` and zero errors **before** the next call — running a menu item while the old assembly is still loaded executes the old code.

```
mcp__anklebreaker__unity_console_clear       port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Build Star Hud"   port: <port>
mcp__anklebreaker__unity_console_log         port: <port>
```

Expected: `[BuildStarHud] DONE — star_hud holds 24 children, star_root kept 0 (should be 0), scene saved`, and **no `missing child:` errors**. If any appear, the name normalisation missed one — print the actual child names and fix the lookup key before continuing.

- [ ] **Step 3: Verify the hierarchy and the wiring**

```
mcp__anklebreaker__unity_scene_hierarchy            parentPath: "Canvas"   maxDepth: 1   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "star_hud"   componentType: "StarHud"   port: <port>
mcp__anklebreaker__unity_component_get_properties   gameObjectPath: "star_root"  componentType: "WordAssemblyTimer"   port: <port>
```

Expected:
- `Canvas` now has both `star_root` (empty, still carrying `WordAssemblyTimer` + `AudioSource`) and `star_hud` (24 children, last sibling).
- `StarHud`: `starStart`, `starFrame`, 4 `centreBurst` entries and 3 slots each with `end` + `stamp` + 4 `burst` — **no nulls**.
- `WordAssemblyTimer` still present and untouched on `star_root`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/BuildStarHud.cs Assets/Editor/BuildStarHud.cs.meta "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(stars): lift the star visuals into star_hud, clear of the timer's CanvasGroup"
```

---

### Task 3: Award the stars from the puzzle

**Files:**
- Modify: `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs` (one field + two hooks)

**Interfaces:**
- Consumes: `StarHud.Instance`, `StarHud.AwardRoutine(int)` from Task 1; `WordAssemblyTimer.Instance.SmokeRemaining` (read-only, existing).
- Produces: nothing new. This is what makes the stars actually appear.

- [ ] **Step 1: Add the in-time field**

In `Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs`, next to the other private state (near `private string activeResultWord;`, line ~185), add:

```csharp
    // Star 3 is the speed bonus: was the clock still running when the word landed? Captured at
    // assembly time because by the time the child finishes pronouncing, the clock has been paused.
    private bool builtInTime;
```

- [ ] **Step 2: Award star 1 when the word is built**

In `WordResultRoutine()` (line ~856), the method currently opens:

```csharp
    private IEnumerator WordResultRoutine()
    {
        ritualPlaying = true;
        WordAssemblyTimer.Instance?.Pause(); // word built -> craft/sound/mic: stop timing (resumes only if they come back to fix a wrong word)
```

Insert the capture immediately **before** that `Pause()` call (the clock must be read while it is still running):

```csharp
    private IEnumerator WordResultRoutine()
    {
        ritualPlaying = true;

        // Read the clock BEFORE pausing it: star 3 is "assembled before the smoke closed".
        builtInTime = WordAssemblyTimer.Instance != null && WordAssemblyTimer.Instance.SmokeRemaining > 0f;

        WordAssemblyTimer.Instance?.Pause(); // word built -> craft/sound/mic: stop timing (resumes only if they come back to fix a wrong word)
```

Then, further down the same method, after the craft page has been revealed and before the action icons fade in — i.e. immediately after this existing line:

```csharp
        HideSlotFrames();
```

insert:

```csharp
        // Star 1: any valid word — right or wrong — earns it. No time condition.
        if (StarHud.Instance != null)
        {
            yield return StarHud.Instance.AwardRoutine(1);
        }
```

- [ ] **Step 3: Award stars 2 and 3 when the pronunciation lands**

In `RecordingSuccessRoutine()` (line ~1654), find:

```csharp
        yield return ShowRecordingSuccessMarkRoutine();

        Image flashImage = null;
```

and insert the award between them:

```csharp
        yield return ShowRecordingSuccessMarkRoutine();

        // Star 2 = the word was correct. Star 3 = it was also assembled before the smoke closed.
        // Both fly up here, one after the other. A wrong word (กา) earns neither and stops at 1.
        if (StarHud.Instance != null)
        {
            bool correct = !string.IsNullOrEmpty(activeResultWord)
                           && activeResultWord == (string.IsNullOrEmpty(targetWord) ? "ปา" : targetWord);

            int award = correct ? (builtInTime ? 2 : 1) : 0;
            if (award > 0)
            {
                yield return StarHud.Instance.AwardRoutine(award);
            }
        }

        Image flashImage = null;
```

- [ ] **Step 4: Compile**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero errors.

- [ ] **Step 5: Play-mode check — the whole 3-star path**

Open the Game view first, then play `CutScene_bear` and **assemble ปา (the correct word) quickly**, then hit the mic and speak.

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"   port: <port>
mcp__anklebreaker__unity_play_mode           action: "play"   port: <port>
```

Confirm `isPlaying: true` via `unity_editor_state` (the first `play` after a domain reload sometimes does not take — call it again). This path needs a human at the controls; if you cannot drive it, ask the user to play it and report, and use the screenshots to check what you can.

Expected:
- one star pops at centre, bursts, arcs up and stamps into slot 1 **the moment the word completes** — and it **stays visible** (this is the `star_root` CanvasGroup trap: if the star blinks out instantly, Task 2 did not take);
- after the recording succeeds, **two more** stars fly up in sequence into slots 2 and 3;
- no console errors.

```
mcp__anklebreaker__unity_console_log   type: "error"   port: <port>
mcp__anklebreaker__unity_play_mode     action: "stop"   port: <port>
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Region1/Cutscenes/MagicStonePuzzleController.cs
git commit -m "feat(stars): award star 1 on assembly, stars 2-3 on a correct pronunciation"
```

---

### Task 4: Show the stars in the Success scenes

**Files:**
- Create: `Assets/Editor/AddStarHudToSuccessScenes.cs`
- Modifies (via the tool): `Assets/Scenes/region 1/Success_pa.unity`, `Assets/Scenes/region 1/Success_ga.unity`

**Interfaces:**
- Consumes: the wired `star_hud` in `CutScene_bear` (Task 2) as the source to copy; `StarHud.EarnedCount`.
- Produces: a `star_hud` in each Success scene, in recap mode. `Success_pa` shows the 2-3 earned stars while the owl praises. `Success_ga` shows the single star, holds, then fades it out — the "stars reset" beat.

Neither `SuccessPaOwlEpilogue` nor `SuccessGaReturn` is modified. The board simply lives alongside them and drives itself from `Start()`.

- [ ] **Step 1: Write the editor tool**

Create `Assets/Editor/AddStarHudToSuccessScenes.cs`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Add Star Hud To Success Scenes
/// Copies the wired star_hud out of CutScene_bear into Success_pa and Success_ga, in recap mode:
///  - Success_pa : the 2-3 earned stars pop into the board while the owl praises.
///  - Success_ga : the single earned star shows, holds, then fades out — "your stars reset, try again".
/// Idempotent: replaces an existing star_hud in the target scene rather than stacking a second one.
public static class AddStarHudToSuccessScenes
{
    const string BearScene = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string PaScene = "Assets/Scenes/region 1/Success_pa.unity";
    const string GaScene = "Assets/Scenes/region 1/Success_ga.unity";
    const string PrefabPath = "Assets/Prefabs/StarHud.prefab";
    const string HudName = "star_hud";

    [MenuItem("Tools/Quest/Add Star Hud To Success Scenes")]
    public static void Run()
    {
        // 1. Snapshot the wired star_hud out of CutScene_bear as a prefab.
        EditorSceneManager.OpenScene(BearScene, OpenSceneMode.Single);

        GameObject source = GameObject.Find(HudName);
        if (source == null) { Debug.LogError("[AddStarHud] star_hud not found in CutScene_bear — run Tools/Quest/Build Star Hud first"); return; }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        if (prefab == null) { Debug.LogError($"[AddStarHud] could not write {PrefabPath}"); return; }
        Debug.Log($"[AddStarHud] snapshotted {PrefabPath}");

        // 2. Drop it into each Success scene in the right recap mode.
        Install(PaScene, recapOnStart: true, fadeOutAfterRecap: false, prefab);
        Install(GaScene, recapOnStart: true, fadeOutAfterRecap: true, prefab);

        Debug.Log("[AddStarHud] DONE — Success_pa (recap) + Success_ga (recap + fade-out) saved");
    }

    static void Install(string scenePath, bool recapOnStart, bool fadeOutAfterRecap, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError($"[AddStarHud] no Canvas in {scenePath}"); return; }

        // Idempotent: blow away a previous copy so re-running never stacks two boards.
        Transform existing = canvas.transform.Find(HudName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        hud.name = HudName;
        hud.transform.SetSiblingIndex(canvas.transform.childCount - 1); // on top of the scene's art

        var star = hud.GetComponent<StarHud>();
        if (star == null) { Debug.LogError($"[AddStarHud] prefab has no StarHud in {scenePath}"); return; }

        var so = new SerializedObject(star);
        so.FindProperty("resetOnAwake").boolValue = false;          // only CutScene_bear clears the count
        so.FindProperty("recapOnStart").boolValue = recapOnStart;
        so.FindProperty("fadeOutAfterRecap").boolValue = fadeOutAfterRecap;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AddStarHud] {scenePath}: recap={recapOnStart} fadeOut={fadeOutAfterRecap}");
    }
}
```

- [ ] **Step 2: Compile, then run it**

```
mcp__anklebreaker__unity_execute_menu_item        menuPath: "Assets/Refresh"   port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Wait for `isCompiling: false` and zero errors, then:

```
mcp__anklebreaker__unity_console_clear       port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Add Star Hud To Success Scenes"   port: <port>
mcp__anklebreaker__unity_console_log         port: <port>
```

Expected: the snapshot line, one line per scene, then `DONE`. No errors.

- [ ] **Step 3: Verify both scenes**

```
mcp__anklebreaker__unity_scene_open                path: "Assets/Scenes/region 1/Success_pa.unity"   port: <port>
mcp__anklebreaker__unity_component_get_properties  gameObjectPath: "star_hud"   componentType: "StarHud"   port: <port>
```

Expected on `Success_pa`: `recapOnStart` true, `fadeOutAfterRecap` false, `resetOnAwake` false, and all the object references still wired (they came across with the prefab).

Repeat for `Success_ga.unity` — same, but `fadeOutAfterRecap` **true**.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/AddStarHudToSuccessScenes.cs Assets/Editor/AddStarHudToSuccessScenes.cs.meta \
        Assets/Prefabs/StarHud.prefab Assets/Prefabs/StarHud.prefab.meta \
        "Assets/Scenes/region 1/Success_pa.unity" "Assets/Scenes/region 1/Success_ga.unity" \
        "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "feat(stars): recap the star board in Success_pa and Success_ga"
```

---

### Task 5: End-to-end verification

**Files:** none (tuning only, if the animation needs it — every knob is serialized on `StarHud`).

- [ ] **Step 1: The wrong-word path (1 star → reset)**

Play `CutScene_bear`, assemble **กา** (the wrong word), pronounce it.

Expected:
1. one star flies up and stamps into slot 1 at assembly;
2. **no** further stars after the recording succeeds;
3. `Success_ga` opens: the crow celebrates, the board shows **one** star, holds, then the star **fades out**;
4. back in `CutScene_bear`, the board is **empty again** (stars do not survive a retry) while the *clock* picks up where it left off;
5. assembling ปา now and pronouncing it awards star 1, then stars 2+3 — a full board.

- [ ] **Step 2: The correct-but-slow path (2 stars)**

Play again, let the clock run to zero (the smoke fully closes), *then* assemble ปา and pronounce it.

Expected: star 1 at assembly, **one** more star at pronunciation (star 2, no speed bonus) → 2 stars. `Success_pa` recaps two stars while the owl praises.

- [ ] **Step 3: Screenshot the good case**

With the Game view open, capture the moment the board is full:

```
mcp__anklebreaker__unity_screenshot_game   path: "Assets/Screenshots/Stars_full.png"   port: <port>
```

Poll for the file (~60-75s, multi-MB), read it, and check the three stars sit inside `star_frame` at the authored slots — not offset, not stretched.

- [ ] **Step 4: Tune the feel, if needed**

Every timing is a serialized field on `StarHud` (`star_hud` in the Inspector). No code edit for any of this:

| Want | Knob |
|---|---|
| Star hangs at centre longer | `holdAfterPop` (0.3 → 0.5) |
| Flight too floaty / too fast | `flyDuration` (0.6) |
| Higher, more thrown-feeling arc | `flyArcHeight` (260 → 400) |
| Burst too tight / too wide | `burstEndScale`, and re-position the `effect_*` objects in the scene (their authored positions **are** the destinations) |
| Two stars feel simultaneous | `gapBetweenStars` (0.25 → 0.4) |
| Success-scene recap too quick | `recapStartDelay`, `recapGap` |
| `Success_ga` star fades too soon | `recapHoldBeforeFade` (1.2) |

- [ ] **Step 5: Commit any tuning**

```bash
git add "Assets/Scenes/region 1/CutScene_bear.unity" "Assets/Scenes/region 1/Success_pa.unity" "Assets/Scenes/region 1/Success_ga.unity" Assets/Prefabs/StarHud.prefab
git commit -m "feat(stars): tune the award animation timing"
```

(Skip if you tuned nothing.)

---

## Done when

- Assembling any valid word awards **1 star**, live, with pop → burst → arc → stamp — and it **stays on the board** (does not blink out when the timer pauses).
- A **correct** word awards **2 more** stars at pronunciation when the clock had time left, **1 more** when it did not, and **none** when the word was wrong.
- `Success_pa` recaps 2-3 stars during the owl's praise; `Success_ga` shows 1 star and fades it out.
- Returning to `CutScene_bear` after `Success_ga` starts the board **empty** while the clock resumes.
- `CutScene_ga` / `CutScene_ta` still run (they share the puzzle controller and have no board — the `StarHud.Instance?` guards make the hooks no-ops).
- `git diff` never touches `WordAssemblyTimer.cs`, `FogController.cs`, `EdgeFox.shader`, `EdgeFogMaterial.mat` or `CloudFogMaterial.mat`.
