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

    /// Play the award animation for `count` stars and persist the new total. Yield on this.
    /// A single star (star 1) flies alone. Two at once (star 2 + the star-3 speed bonus) pop
    /// together at centre, then split and fly to their slots side by side, landing together.
    public IEnumerator AwardRoutine(int count)
    {
        int start = shown;
        int end = Mathf.Min(start + count, MaxStars, slots.Length);
        if (end <= start) yield break;

        int n = end - start;
        yield return n == 1 ? AwardOneRoutine(start) : AwardBatchRoutine(start, n);

        shown = end;
        PlayerPrefs.SetInt(EarnedKey, shown);
        PlayerPrefs.Save();
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

    /// n stars awarded in the same breath (the correct + in-time case): one shared pop + burst at
    /// centre — they are born at the same point at the same instant, so a single pop reads correctly
    /// for all of them — then the star splits into `n` clones that arc out to their own slots and
    /// land in parallel. starStart itself can't play two flights at once (it's one object in the
    /// scene), so each flight gets a throwaway clone, destroyed on landing.
    private IEnumerator AwardBatchRoutine(int firstSlotIndex, int n)
    {
        if (starStart == null) yield break;

        PlaySfx();

        // --- 1. pop in at centre (shared) + centre burst ------------------------------------------
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

        // --- 2. split: the shared star hands off to n clones, one per slot ------------------------
        SetActive(starStart, false);

        int pending = n;
        for (int i = 0; i < n; i++)
        {
            int slotIndex = firstSlotIndex + i;
            RectTransform clone = CloneStar();
            StartCoroutine(FlyCloneToSlotRoutine(clone, slotIndex, () => pending--));
        }

        while (pending > 0) yield return null;
    }

    /// A throwaway copy of starStart, popped in and ready to fly — used when several stars launch
    /// from the same point at once and each needs its own transform to animate independently.
    private RectTransform CloneStar()
    {
        GameObject go = Instantiate(starStart.gameObject, starStart.parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = starStart.anchorMin;
        rt.anchorMax = starStart.anchorMax;
        rt.pivot = starStart.pivot;
        rt.anchoredPosition = starStart.anchoredPosition;
        rt.sizeDelta = starStart.sizeDelta;
        rt.localScale = starStart.localScale;
        rt.localRotation = starStart.localRotation;
        SetActive(rt, true);
        SetAlpha(rt, 1f);
        return rt;
    }

    /// Anticipation dip + arc flight + landing stamp for one cloned star. Mirrors steps 2-4 of
    /// AwardOneRoutine, operating on a clone instead of the shared starStart so several of these can
    /// run at once. The clone is destroyed once it reaches the slot — the slot's own star_end takes
    /// over from there, exactly as it does for a solo award.
    private IEnumerator FlyCloneToSlotRoutine(RectTransform star, int slotIndex, System.Action onDone)
    {
        Slot slot = slots[slotIndex];
        if (slot == null || slot.end == null)
        {
            Destroy(star.gameObject);
            onDone();
            yield break;
        }

        for (float t = 0f; t < anticipationDuration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, anticipationDuration));
            star.localScale = Vector3.LerpUnclamped(startScale, startScale * anticipationScale, k);
            yield return null;
        }

        Vector2 endPos = slot.end.anchoredPosition;
        Vector2 endSize = slot.end.sizeDelta;
        Vector3 endScale = slot.end.localScale;

        Vector2 control = (startPos + endPos) * 0.5f + Vector2.up * flyArcHeight;

        for (float t = 0f; t < flyDuration; t += Time.deltaTime)
        {
            float k = SmoothStep(Mathf.Clamp01(t / flyDuration));
            star.anchoredPosition = Bezier(startPos, control, endPos, k);
            star.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, k);
            star.localScale = Vector3.LerpUnclamped(startScale * anticipationScale, endScale, k);
            star.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(0f, flySpinDegrees, k));
            yield return null;
        }

        Destroy(star.gameObject);

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

        onDone();
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
