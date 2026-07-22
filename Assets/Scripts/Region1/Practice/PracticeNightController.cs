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

    [Header("Hero")]
    [SerializeField] private Animator heroAnimator;

    [Header("Crow")]
    [SerializeField] private Animator crowAnimator;
    [SerializeField] private RectTransform crowRect;
    [SerializeField] private RuntimeAnimatorController crowFlyController;     // CrowController: ga_fly, ga_stone
    [SerializeField] private RuntimeAnimatorController crowSetFreeController; // GaSetFreeController: ga_set_free, ga_set_free2
    [SerializeField] private Transform scarecrowWorldTarget;                  // dummy_idle_DOWN_0
    [SerializeField] private Vector2 crowRestOffsetFromScarecrow = new Vector2(210f, -35f);
    [SerializeField, Range(0.1f, 1f)] private float crowScale = 0.65f;

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

    [Header("Book intro")]
    [SerializeField] private RectTransform bookPopRoot; // the WordAssembly book_craft (or its parent) to pop in
    [SerializeField] private float bookPopDuration = 0.5f;
    [SerializeField] private float bookPopStartScale = 0.08f;

    [Header("Scene fade gate")]
    [SerializeField] private float revealGateFallbackSeconds = 2f;

    [Header("Resolution / exit")]
    [SerializeField] private float resolutionHoldSeconds = 0.4f;
    [SerializeField] private float sceneFadeDuration = 0.6f;
    [SerializeField] private string returnSceneName = "WorldMap";

    private Vector2 crowRestPosition;
    private Canvas crowCanvas;
    private Vector3 crowNaturalScale = Vector3.one;
    private Vector3 bookNaturalScale = Vector3.one;

    private void Awake()
    {
        if (heroAnimator != null)
        {
            heroAnimator.SetInteger("orientation", 4);
            heroAnimator.SetFloat("speed", 0f);
        }

        if (crowRect != null)
        {
            crowRestPosition = crowRect.anchoredPosition;
            crowNaturalScale = crowRect.localScale;
            crowRect.localScale = crowNaturalScale * Mathf.Clamp(crowScale, 0.1f, 1f);
            crowCanvas = crowRect.GetComponentInParent<Canvas>();
            crowRect.gameObject.SetActive(false);
        }

        if (bookPopRoot != null)
        {
            bookNaturalScale = bookPopRoot.localScale;
            bookPopRoot.localScale = bookNaturalScale * Mathf.Max(0.01f, bookPopStartScale);
            bookPopRoot.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        ReferenceForestNightBackground.SetSceneNight(1f);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return WaitForSceneReveal();

        PracticeEvent active = PickEvent();
        if (active == null)
        {
            Debug.LogWarning("[PracticeNight] no event configured");
            yield break;
        }

        if (wordAssembly == null || crowAnimator == null || crowRect == null || owl == null)
        {
            Debug.LogWarning("[PracticeNight] essential references not wired; aborting Run()");
            yield break;
        }

        yield return PlayIntro(active);      // crow flies in (petrify for A / circle for B)
        yield return PopInBook();
        yield return PlayOwlLine(active);

        wordAssembly.Configure(active.targetWord, active.resultPage, active.wordClip, () => StartCoroutine(OnWordSuccess(active)));
        yield return wordAssembly.PlayReveal();
    }

    private IEnumerator WaitForSceneReveal()
    {
        if (SceneFadeController.RevealComplete) yield break;

        float deadline = Time.unscaledTime + Mathf.Max(0.1f, revealGateFallbackSeconds);
        while (!SceneFadeController.RevealComplete && Time.unscaledTime < deadline)
        {
            yield return null;
        }
    }

    private PracticeEvent PickEvent()
    {
        if (events == null || events.Length == 0) return null;
        return events[UnityEngine.Random.Range(0, events.Length)];
    }

    private IEnumerator PopInBook()
    {
        if (bookPopRoot == null) yield break;
        Vector3 target = bookNaturalScale;
        Vector3 start = target * Mathf.Max(0.01f, bookPopStartScale);
        bookPopRoot.gameObject.SetActive(true);
        bookPopRoot.localScale = start;
        float safe = Mathf.Max(0.01f, bookPopDuration);
        for (float t = 0f; t < safe; t += Time.deltaTime)
        {
            bookPopRoot.localScale = Vector3.LerpUnclamped(start, target, EaseOutBack(Mathf.Clamp01(t / safe)));
            yield return null;
        }
        bookPopRoot.localScale = target;
    }

    private IEnumerator PlayIntro(PracticeEvent activeEvent)
    {
        crowRestPosition = ResolveCrowRestPosition();
        crowRect.localScale = crowNaturalScale * Mathf.Clamp(crowScale, 0.1f, 1f);
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

    private Vector2 ResolveCrowRestPosition()
    {
        if (scarecrowWorldTarget == null || crowCanvas == null)
        {
            return crowRestPosition;
        }

        return ResolveScarecrowAnchoredPosition() + crowRestOffsetFromScarecrow;
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

    private static float EaseOutBack(float value)
    {
        value = Mathf.Clamp01(value);
        const float overshoot = 1.15f;
        float s = value - 1f;
        return 1f + s * s * ((overshoot + 1f) * s + overshoot);
    }
}
