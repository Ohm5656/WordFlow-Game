using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// The garbage-word punishment: a cartoon red damage-screen, a padlock, and three beat-beeps that
/// each yank the smoke toward the book — for three seconds, during which the stones are dead. Then
/// the padlock springs open and bursts apart, and the board is playable again.
///
/// The clock is NOT paused while this plays. That is the whole point: being frozen out costs real
/// seconds of smoke, so brute-forcing combinations has a price.
///
/// The alert is ADDITIVE (WordFlow/LockVignette, Blend SrcAlpha One). It can only add red light, so
/// it cannot hide the smoke underneath — the smoke just reads as red-hot while the alert is up.
///
/// The padlock art lives in Canvas/key_root and is authored by hand: `lock`, `unlock`, and
/// `effect_star1..4`, whose authored positions ARE the burst's scatter destinations — the same
/// convention StarHud uses. Everything there is hidden in Awake and only shows on its cue.
public sealed class MisassemblyLock : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Full-screen RawImage running the WordFlow/LockVignette material.")]
    [SerializeField] private RawImage vignette;

    [Tooltip("key_root/lock — the closed padlock, up for the whole lockout.")]
    [SerializeField] private Image lockIcon;

    [Tooltip("key_root/unlock — the opened padlock, swapped in when the lock lifts.")]
    [SerializeField] private Image unlockIcon;

    [Tooltip("key_root/effect_star1..4 — the unlock burst. Their authored positions are the scatter " +
             "destinations; the burst flies them out from the padlock to where they already sit.")]
    [SerializeField] private Image[] burst;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("countdown_beep.wav — the same beep the smoke clock plays in its last 10 seconds. The " +
             "clip is one long track of once-a-second beeps, so we play it from 0 and cut it after " +
             "beepAudibleSeconds to get exactly one beep.")]
    [SerializeField] private AudioClip beepClip;

    [Tooltip("unlock.wav — plays as the padlock springs open.")]
    [SerializeField] private AudioClip unlockSfx;

    [Header("Beats")]
    [SerializeField, Min(1)] private int beepCount = 3;
    [SerializeField, Min(0.1f)] private float beepInterval = 1f;
    [SerializeField, Min(0.05f)] private float beepAudibleSeconds = 0.4f;

    [Tooltip("How fast each smoke lurch settles back. 6 matches FogController's countdown beat, so " +
             "the lurch feels identical to a countdown beep's.")]
    [SerializeField, Range(1f, 12f)] private float pulseDecay = 6f;

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
    private static readonly int PulseID = Shader.PropertyToID("_Pulse");

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

        // raycastTarget on the full-screen vignette eats every tap on its own, belt-and-braces with
        // the CanInteract gate in MagicStonePuzzleController.
        if (vignette != null) vignette.raycastTarget = true;

        // --- flash in -----------------------------------------------------------------------------
        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetIntensity(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetIntensity(1f);

        // --- the closed padlock pops in -------------------------------------------------------------
        yield return PopIconRoutine(lockIcon, lockBaseScale, iconPopDuration);

        // --- three beats: beep + smoke lurch ---------------------------------------------------------
        for (int i = 0; i < beepCount; i++)
        {
            PlayClip(beepClip, cutAfter: beepAudibleSeconds);

            // pulse01 drives BOTH the smoke's inward lurch (via FogController.ExtraPulseProvider) and
            // the vignette's throb, so the sound, the red and the smoke all land on the same beat.
            for (float t = 0f; t < beepInterval; t += Time.deltaTime)
            {
                pulse01 = Mathf.Exp(-t * pulseDecay);
                SetPulse(pulse01);
                yield return null;
            }
        }

        pulse01 = 0f;
        SetPulse(0f);

        // --- the unlock beat: swap to the open padlock and burst it apart -----------------------------
        SetActive(lockIcon, false);

        PlayClip(unlockSfx, cutAfter: 0f);

        StartCoroutine(BurstRoutine());
        yield return PopIconRoutine(unlockIcon, unlockBaseScale, unlockPopDuration);

        if (unlockHold > 0f) yield return new WaitForSeconds(unlockHold);

        // --- fade out ---------------------------------------------------------------------------------
        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Clamp01(t / fadeOutDuration);
            SetIntensity(k);
            SetAlpha(unlockIcon, k);
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
    /// beep's worth. Pass 0 to let a clip (unlock.wav) play out in full.
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

    /// Everything from key_root is hidden until its cue — forced here rather than trusted from the
    /// scene, which the designer leaves visible while authoring.
    private void Hide()
    {
        pulse01 = 0f;
        SetIntensity(0f);
        SetPulse(0f);

        if (vignette != null) vignette.raycastTarget = false;

        SetActive(lockIcon, false);
        SetActive(unlockIcon, false);

        if (burst == null) return;
        for (int i = 0; i < burst.Length; i++) SetActive(burst[i], false);
    }

    private void SetIntensity(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(IntensityID, Mathf.Clamp01(v));
    }

    private void SetPulse(float v)
    {
        if (vignetteMaterial != null) vignetteMaterial.SetFloat(PulseID, Mathf.Clamp01(v));
    }

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
