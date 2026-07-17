using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// The garbage-word punishment: a short red edge-light for three seconds, during which the stones
/// are dead. A supported phone vibrates once at the start; no gameplay UI is screen-shaken.
/// Outside the countdown window it sounds three local beat-beeps that yank the smoke;
/// inside the last 10 seconds it rides the clock's existing beep and smoke rhythm instead.
///
/// The clock is NOT paused while this plays. That is the whole point: being frozen out costs real
/// seconds of smoke, so brute-forcing combinations has a price.
///
/// The alert uses an edge-only red vignette. Its short fades are included in the three-second duration;
/// beep pulses affect the smoke only, never the red intensity. The legacy key_root art stays wired
/// for scene compatibility but remains hidden throughout, and no unlock sound is played.
public sealed class MisassemblyLock : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Full-screen RawImage running the WordFlow/LockVignette material.")]
    [SerializeField] private RawImage vignette;

    [Tooltip("Legacy key_root/lock reference. Kept serialized but never shown.")]
    [SerializeField] private Image lockIcon;

    [Tooltip("Legacy key_root/unlock reference. Kept serialized but never shown.")]
    [SerializeField] private Image unlockIcon;

    [Tooltip("Legacy key_root/effect_star1..4 references. Kept serialized but never shown.")]
    [SerializeField] private Image[] burst;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("countdown_beep.wav — the same beep the smoke clock plays in its last 10 seconds. The " +
             "clip is one long track of once-a-second beeps, so we play it from 0 and cut it after " +
             "beepAudibleSeconds to get exactly one beep.")]
    [SerializeField] private AudioClip beepClip;

    [Tooltip("Legacy unlock.wav reference. Kept serialized but deliberately not played.")]
    [SerializeField] private AudioClip unlockSfx;

    [Header("Device haptics")]
    [Tooltip("Vibrate once on supported phones when a non-word is assembled. This never shakes the game UI.")]
    [SerializeField] private bool vibrateDeviceOnMisassembly = true;

    [Header("Beats")]
    [SerializeField, Min(1)] private int beepCount = 3;
    [SerializeField, Min(0.1f)] private float beepInterval = 1f;
    [SerializeField, Min(0.05f)] private float beepAudibleSeconds = 0.4f;

    [Tooltip("Seconds left at which WordAssemblyTimer's own countdown track starts beeping — must " +
             "match its countdownAt (10). Inside this window the clock is already sounding a beep " +
             "every second, so the lock rides on it instead of playing a second, out-of-phase copy " +
             "of the same clip on top.")]
    [SerializeField, Min(0f)] private float beepWindow = 10f;

    [Tooltip("How fast each smoke lurch settles back. 6 matches FogController's countdown beat, so " +
             "the lurch feels identical to a countdown beep's.")]
    [SerializeField, Range(1f, 12f)] private float pulseDecay = 6f;

    [Header("Lockout")]
    [Tooltip("Total input lock duration, including the fade-in and fade-out.")]
    [SerializeField, Min(0.1f)] private float lockoutDuration = 3f;

    [Header("Fades")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;

    [Header("Padlock pop")]
    [SerializeField, Min(0.01f)] private float iconPopDuration = 0.28f;
    [SerializeField] private float iconOvershoot = 1.8f;

    [Header("Unlock burst")]
    [SerializeField, Min(0.01f)] private float unlockPopDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float burstOutDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float burstFadeDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float burstStartScale = 0.3f;
    [SerializeField, Min(0.01f)] private float burstEndScale = 1.2f;
    [SerializeField, Min(0f)] private float unlockHold = 0.25f;

    public static MisassemblyLock Instance { get; private set; }

    public bool IsLocked { get; private set; }

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");

    private Material vignetteMaterial;
    private Vector3 lockBaseScale = Vector3.one;
    private Vector3 unlockBaseScale = Vector3.one;

    // The scene layout IS the burst's target data — cache it before anything moves.
    private Vector2[] burstPos;
    private Vector3[] burstScale;

    private float pulse01;

    private void Awake()
    {
        Instance = this;

        if (vignette != null && vignette.material != null)
        {
            // Instance the material so an editor preview value never leaks into a play session.
            vignetteMaterial = new Material(vignette.material);
            vignette.material = vignetteMaterial;
        }

        if (lockIcon != null) lockBaseScale = lockIcon.rectTransform.localScale;
        if (unlockIcon != null) unlockBaseScale = unlockIcon.rectTransform.localScale;

        int n = burst != null ? burst.Length : 0;
        burstPos = new Vector2[n];
        burstScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            RectTransform r = Rect(burst[i]);
            if (r == null) continue;
            burstPos[i] = r.anchoredPosition;
            burstScale[i] = r.localScale;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // The smoke reads this every frame. Max'd with the countdown beat inside FogController, so
        // the last-10-seconds beep pulse is untouched.
        FogController.ExtraPulseProvider = () => pulse01;

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (FogController.ExtraPulseProvider != null) FogController.ExtraPulseProvider = null;
        if (vignetteMaterial != null) Destroy(vignetteMaterial);
    }

    /// The full alert. Yield on this — it returns when the lock should lift.
    public IEnumerator PlayRoutine()
    {
        IsLocked = true;

        if (vibrateDeviceOnMisassembly && Application.isMobilePlatform)
        {
            Handheld.Vibrate();
        }

        // raycastTarget on the full-screen vignette eats every tap on its own, belt-and-braces with
        // the CanInteract gate in MagicStonePuzzleController.
        if (vignette != null) vignette.raycastTarget = true;

        HideKeyArt();

        float duration = Mathf.Max(0.1f, lockoutDuration);
        float fadeIn = Mathf.Min(fadeInDuration, duration);
        float fadeOut = Mathf.Min(fadeOutDuration, Mathf.Max(0f, duration - fadeIn));

        // Keep one local beat per second through the three-second lockout when the countdown is not
        // involved. Only align to clock boundaries when the 10-second rhythm is active or will begin
        // during this lockout; this prevents a near-boundary local beep from colliding with the countdown track.
        bool useClockRhythm = ClockRunning && ClockRemaining <= beepWindow + duration;
        float nextBeatAt = 0f;
        float beatSpacing = beepInterval;
        if (useClockRhythm)
        {
            nextBeatAt = ClockRemaining % 1f;
            if (nextBeatAt <= 0.03f) nextBeatAt = 0f;
            beatSpacing = 1f;
        }

        int beatsHandled = 0;
        float localPulseStartedAt = float.NegativeInfinity;
        bool countdownOwnsRhythm = ClockIsBeeping;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            // Latch once the countdown starts. This remains true if the timer reaches zero during
            // the lock, so a local beep never starts after the countdown track has taken ownership.
            if (ClockIsBeeping)
            {
                countdownOwnsRhythm = true;
                localPulseStartedAt = float.NegativeInfinity;
                pulse01 = 0f;
            }

            while (beatsHandled < beepCount && elapsed + 0.001f >= nextBeatAt)
            {
                if (!countdownOwnsRhythm)
                {
                    PlayClip(beepClip, cutAfter: beepAudibleSeconds);
                    localPulseStartedAt = elapsed;
                }

                beatsHandled++;
                nextBeatAt += beatSpacing;
            }

            if (!countdownOwnsRhythm && !float.IsNegativeInfinity(localPulseStartedAt))
            {
                pulse01 = Mathf.Exp(-(elapsed - localPulseStartedAt) * pulseDecay);
            }

            SetIntensity(EvaluateIntensity(elapsed, duration, fadeIn, fadeOut));
            yield return null;
        }

        Hide();
        IsLocked = false;
    }

    private IEnumerator PopIconRoutine(Image icon, Vector3 baseScale, float duration)
    {
        if (icon == null) yield break;

        SetActive(icon, true);
        SetAlpha(icon, 0f);
        icon.rectTransform.localScale = Vector3.zero;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            icon.rectTransform.localScale =
                Vector3.LerpUnclamped(Vector3.zero, baseScale, EaseOutBack(k, iconOvershoot));
            SetAlpha(icon, Mathf.Clamp01(k * 2f));
            yield return null;
        }

        icon.rectTransform.localScale = baseScale;
        SetAlpha(icon, 1f);
    }

    /// effect_star1..4 fly out from the padlock to their authored positions and fade — the same
    /// scatter the star award uses.
    private IEnumerator BurstRoutine()
    {
        if (burst == null || burst.Length == 0) yield break;

        Vector2 origin = unlockIcon != null
            ? unlockIcon.rectTransform.anchoredPosition
            : (lockIcon != null ? lockIcon.rectTransform.anchoredPosition : Vector2.zero);

        for (int i = 0; i < burst.Length; i++)
        {
            RectTransform r = Rect(burst[i]);
            if (r == null) continue;
            r.anchoredPosition = origin;
            r.localScale = burstScale[i] * burstStartScale;
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
                r.anchoredPosition = Vector2.LerpUnclamped(origin, burstPos[i], k);
                r.localScale = Vector3.LerpUnclamped(burstScale[i] * burstStartScale,
                                                     burstScale[i] * burstEndScale, k);
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
            if (r != null) r.localScale = burstScale[i];
        }
    }

    /// countdown_beep.wav is a long track of once-a-second beeps, so `cutAfter` stops it after one
    /// beep's worth.
    private void PlayClip(AudioClip clip, float cutAfter)
    {
        if (audioSource == null || clip == null) return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.time = 0f;
        audioSource.Play();

        if (cutAfter > 0f) StartCoroutine(CutClipRoutine(clip, cutAfter));
    }

    private IEnumerator CutClipRoutine(AudioClip clip, float after)
    {
        yield return new WaitForSeconds(after);
        if (audioSource != null && audioSource.clip == clip && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// The authored key_root can stay visible while editing, but it is never part of runtime lock
    /// feedback. Force every child off both at startup and when a lock ends.
    private void Hide()
    {
        pulse01 = 0f;
        SetIntensity(0f);

        if (vignette != null) vignette.raycastTarget = false;

        HideKeyArt();
    }

    private void HideKeyArt()
    {
        SetActive(lockIcon, false);
        SetActive(unlockIcon, false);

        if (burst == null) return;
        for (int i = 0; i < burst.Length; i++) SetActive(burst[i], false);
    }

    private void SetIntensity(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(IntensityID, Mathf.Clamp01(v));
    }

    private static float EvaluateIntensity(float elapsed, float duration, float fadeIn, float fadeOut)
    {
        if (fadeIn > 0f && elapsed < fadeIn)
        {
            return Mathf.Clamp01(elapsed / fadeIn);
        }

        float fadeOutStart = duration - fadeOut;
        if (fadeOut > 0f && elapsed > fadeOutStart)
        {
            return Mathf.Clamp01((duration - elapsed) / fadeOut);
        }

        return 1f;
    }

    /// Seconds left on the puzzle clock, or 0 when there is no clock (the other cutscene scenes).
    private float ClockRemaining =>
        WordAssemblyTimer.Instance != null ? WordAssemblyTimer.Instance.SmokeRemaining : 0f;

    private bool ClockRunning => ClockRemaining > 0.01f;

    /// A small frame allowance prevents a local beep from starting just before the timer crosses
    /// 10.000 seconds and launches its continuous countdown track on the same rendered frame.
    private bool ClockIsBeeping =>
        ClockRunning && ClockRemaining <= beepWindow + Mathf.Max(0.03f, Time.deltaTime * 2f);

    private static RectTransform Rect(Component c) => c != null ? c.transform as RectTransform : null;

    private static void SetActive(Component c, bool on)
    {
        if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = Mathf.Clamp01(a);
        g.color = c;
    }

    private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

    private static float EaseOutBack(float t, float overshoot)
    {
        float s = t - 1f;
        return 1f + s * s * ((overshoot + 1f) * s + overshoot);
    }
}
