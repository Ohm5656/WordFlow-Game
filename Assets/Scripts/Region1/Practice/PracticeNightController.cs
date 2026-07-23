using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the practice_night redo as one continuous night-context sequence:
/// crow enters and petrifies, the child frees it by building/saying "กา", then builds/says
/// "ปา" to play the throw result and send the crow away.
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
    }

    private const string GaWord = "\u0E01\u0E32";
    private const string PaWord = "\u0E1B\u0E32";

    [Header("Word assembly")]
    [SerializeField] private PracticeWordAssembly wordAssembly;

    [Header("Hero")]
    [SerializeField] private Animator heroAnimator;

    [Header("Crow")]
    [SerializeField] private Animator crowAnimator;
    [SerializeField] private RectTransform crowRect;
    [SerializeField] private RuntimeAnimatorController crowFlyController;     // CrowController: ga_fly, ga_stone
    [SerializeField] private RuntimeAnimatorController crowSetFreeController; // GaSetFreeController: ga_set_free, ga_set_free2
    [SerializeField] private Transform scarecrowWorldTarget;                  // legacy scene hook; kept for existing serialized data
    [SerializeField] private Transform crowWaypointPath;
    [SerializeField] private string[] crowIntroWaypoints = { "wp_0", "wp_1", "wp_2" };
    [SerializeField, Range(0.1f, 1f)] private float crowScale = 0.48f;
    [SerializeField] private float crowFadeInDuration = 0.4f;
    [SerializeField] private float crowWaypointLegDuration = 1.35f;
    [SerializeField] private string crowFreedWaypointName = "wp_3";
    [SerializeField] private float crowFreedWaypointLegDuration = 1.35f;
    [SerializeField] private float crowStoneEdgeCropPixels = 4f;
    [SerializeField] private float crowHoverAmplitude = 24f;
    [SerializeField] private float crowHoverFrequency = 1.3f;

    [Header("Events (กา then ปา)")]
    [SerializeField] private PracticeEvent[] events = new PracticeEvent[2];

    [Header("Crow escape after ปา")]
    [SerializeField] private Vector2 crowShooOffset = new Vector2(900f, 500f);
    [SerializeField] private float crowShooDuration = 0.6f;

    [Header("Set-free result")]
    [SerializeField] private float setFreeFadeOutDuration = 0f;
    [SerializeField] private float setFree2FadeInDuration = 0.35f;
    [SerializeField] private string setFreeState = "ga_set_free";
    [SerializeField] private string setFree2State = "ga_set_free2";
    [SerializeField] private int setFreeExpandStartFrame = 37;
    [SerializeField] private float setFreeClipFps = 30f;
    [Tooltip("Base scale for the shatter clip before its bloom. Keep separate from crowScale so the white/pink burst can leave the frame cleanly.")]
    [SerializeField, Min(0.01f)] private float setFreeShatterBaseScale = 1f;
    [SerializeField, Min(0.01f)] private float setFreeExpandStartScale = 1f;
    [SerializeField, Min(0.01f)] private float setFreeExpandEndScale = 3.5f;

    [Header("Throw result")]
    [SerializeField] private RuntimeAnimatorController throwController;
    [SerializeField] private RectTransform throwVisualRoot;
    [Tooltip("If Throw Visual Root is assigned to a scene object, keep its authored position and size so it can be resized visually.")]
    [SerializeField] private bool useAuthoredThrowTransform = true;
    [SerializeField] private Vector2 throwVisualAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 throwVisualSize = new Vector2(1920f, 1080f);
    [SerializeField, Min(0.01f)] private float throwVisualScale = 1f;
    [SerializeField] private string throwStateName = "Thow";
    [SerializeField, Min(0)] private int throwFirstFrameNumber = 37;
    [SerializeField, Min(0)] private int crowEscapeThrowFrameNumber = 108;
    [SerializeField, Min(0.01f)] private float throwClipFps = 30f;
    [SerializeField] private float throwFadeOutDuration = 0.25f;

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

    private Canvas crowCanvas;
    private CanvasGroup crowFade;
    private Vector3 crowNaturalScale = Vector3.one;
    private Vector3 bookNaturalScale = Vector3.one;
    private Coroutine crowHoverRoutine;
    private Animator throwAnimator;
    private CanvasGroup throwFade;
    private Vector3 throwVisualNaturalScale = Vector3.one;
    private bool throwVisualNaturalScaleCaptured;
    private bool throwVisualCreatedAtRuntime;

    private void Awake()
    {
        if (heroAnimator != null)
        {
            heroAnimator.SetInteger("orientation", 4);
            heroAnimator.SetFloat("speed", 0f);
        }

        ResolveWaypointPath();

        if (crowRect != null)
        {
            crowNaturalScale = crowRect.localScale;
            crowCanvas = crowRect.GetComponentInParent<Canvas>();
            crowFade = EnsureCanvasGroup(crowRect.gameObject);
            crowFade.alpha = 0f;
            ConfigureCrowImage();
            crowRect.gameObject.SetActive(false);
        }

        if (bookPopRoot != null)
        {
            bookNaturalScale = bookPopRoot.localScale;
            bookPopRoot.localScale = bookNaturalScale * Mathf.Max(0.01f, bookPopStartScale);
            bookPopRoot.gameObject.SetActive(false);
        }

        if (throwVisualRoot != null)
        {
            CaptureThrowVisualNaturalScale();
            throwVisualRoot.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        ReferenceForestNightBackground.SetSceneNight(1f);
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopCrowHover();
        GameAudio.StopSfxLoop();
    }

    private IEnumerator Run()
    {
        yield return WaitForSceneReveal();

        PracticeEvent gaEvent = FindEvent(GaWord);
        PracticeEvent paEvent = FindEvent(PaWord);
        if (gaEvent == null || paEvent == null)
        {
            Debug.LogWarning("[PracticeNight] expected both กา and ปา events to be configured.");
            yield break;
        }

        if (wordAssembly == null || crowAnimator == null || crowRect == null)
        {
            Debug.LogWarning("[PracticeNight] essential references not wired; aborting Run().");
            yield break;
        }

        yield return PlayCrowWaypointIntro();
        yield return PlayWordRound(gaEvent);
        yield return PlayCrowSetFreeThenHover();
        yield return PlayWordRound(paEvent);
        yield return PlayThrowAndCrowEscape();

        yield return StartCoroutine(SceneFadeController.Cover(sceneFadeDuration));
        NightMode.ForceNightPhaseForCurrentSession();
        NightMode.EndSession();
        SceneManager.LoadScene(returnSceneName);
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

    private PracticeEvent FindEvent(string word)
    {
        if (events == null) return null;

        for (int i = 0; i < events.Length; i++)
        {
            PracticeEvent practiceEvent = events[i];
            if (practiceEvent != null && string.Equals(practiceEvent.targetWord, word, StringComparison.Ordinal))
            {
                return practiceEvent;
            }
        }

        return null;
    }

    private IEnumerator PlayWordRound(PracticeEvent practiceEvent)
    {
        bool roundComplete = false;
        wordAssembly.Configure(
            practiceEvent.targetWord,
            practiceEvent.resultPage,
            practiceEvent.wordClip,
            () => roundComplete = true);

        yield return PopInBook();
        yield return wordAssembly.PlayReveal();

        while (!roundComplete)
        {
            yield return null;
        }

        if (resolutionHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(resolutionHoldSeconds);
        }
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

    private IEnumerator PlayCrowWaypointIntro()
    {
        ResolveWaypointPath();

        crowRect.gameObject.SetActive(true);
        crowRect.localScale = crowNaturalScale * Mathf.Clamp(crowScale, 0.1f, 1f);
        crowRect.position = WaypointPosition(GetCrowWaypointName(0), crowRect.position);

        crowFade = EnsureCanvasGroup(crowRect.gameObject);
        crowFade.alpha = 0f;

        crowAnimator.runtimeAnimatorController = crowFlyController;
        crowAnimator.speed = 1f;
        crowAnimator.Play("ga_fly", 0, 0f);
        GameAudio.PlayCrowLoop();

        yield return FadeCanvasGroup(crowFade, 1f, crowFadeInDuration);

        int waypointCount = crowIntroWaypoints != null ? crowIntroWaypoints.Length : 0;
        for (int i = 1; i < waypointCount; i++)
        {
            yield return MoveCrowToWorld(WaypointPosition(GetCrowWaypointName(i), crowRect.position), crowWaypointLegDuration);
        }

        yield return WaitForCrowFlyLoopBoundary();
        crowAnimator.Play("ga_stone", 0, 0f);
        GameAudio.StopSfxLoop();
        yield return WaitForCurrentAnimatorState(crowAnimator);
    }

    private IEnumerator PlayCrowSetFreeThenHover()
    {
        StopCrowHover();
        crowRect.gameObject.SetActive(true);
        crowFade = EnsureCanvasGroup(crowRect.gameObject);
        crowFade.alpha = 1f;

        Vector3 flyScale = crowNaturalScale * Mathf.Clamp(crowScale, 0.1f, 1f);
        Vector3 shatterBaseScale = crowNaturalScale * Mathf.Max(0.01f, setFreeShatterBaseScale);
        crowRect.localScale = shatterBaseScale * Mathf.Max(0.01f, setFreeExpandStartScale);

        crowAnimator.runtimeAnimatorController = crowSetFreeController;
        crowAnimator.speed = 1f;
        crowAnimator.Play(setFreeState, 0, 0f);
        GameAudio.PlayRockBreak();
        yield return null;

        float clipLength = Mathf.Max(0.01f, crowAnimator.GetCurrentAnimatorStateInfo(0).length);
        float expandStart = setFreeExpandStartFrame / Mathf.Max(1f, setFreeClipFps);
        float guard = 0f;
        while (guard < clipLength + 1f)
        {
            AnimatorStateInfo info = crowAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime >= 1f) break;

            float clipSeconds = info.normalizedTime * clipLength;
            float expandT = Mathf.InverseLerp(expandStart, clipLength, clipSeconds);
            crowRect.localScale = shatterBaseScale * Mathf.Lerp(
                Mathf.Max(0.01f, setFreeExpandStartScale),
                Mathf.Max(0.01f, setFreeExpandEndScale),
                EaseOut(expandT));

            guard += Time.deltaTime;
            yield return null;
        }

        crowRect.localScale = shatterBaseScale * Mathf.Max(0.01f, setFreeExpandEndScale);
        if (setFreeFadeOutDuration > 0f)
        {
            yield return FadeCanvasGroup(crowFade, 0f, setFreeFadeOutDuration);
        }
        else
        {
            crowFade.alpha = 0f;
        }

        crowRect.localScale = flyScale;
        crowAnimator.Play(setFree2State, 0, 0f);
        GameAudio.PlayCrowLoop();
        yield return null;

        float clip2Length = Mathf.Max(0.01f, crowAnimator.GetCurrentAnimatorStateInfo(0).length);
        yield return FadeCanvasGroup(crowFade, 1f, setFree2FadeInDuration);
        float remain = clip2Length - Mathf.Max(0f, setFree2FadeInDuration);
        if (remain > 0f)
        {
            yield return new WaitForSeconds(remain);
        }

        crowAnimator.runtimeAnimatorController = crowFlyController;
        crowAnimator.Play("ga_fly", 0, 0f);
        yield return MoveCrowToWorld(
            WaypointPosition(crowFreedWaypointName, crowRect.position),
            crowFreedWaypointLegDuration > 0f ? crowFreedWaypointLegDuration : crowWaypointLegDuration);
        StartCrowHover();
    }

    private IEnumerator PlayThrowAndCrowEscape()
    {
        crowRect.gameObject.SetActive(true);
        crowFade = EnsureCanvasGroup(crowRect.gameObject);
        crowFade.alpha = 1f;
        crowAnimator.runtimeAnimatorController = crowFlyController;
        crowAnimator.Play("ga_fly", 0, 0f);
        if (crowHoverRoutine == null)
        {
            StartCrowHover();
        }
        GameAudio.PlayCrowLoop();
        GameAudio.PlayPaaThrow();

        bool crowCanEscape = false;
        Coroutine throwRoutine = StartCoroutine(PlayThrowVisual(() => crowCanEscape = true));
        while (!crowCanEscape)
        {
            yield return null;
        }

        StopCrowHover();
        yield return MoveCrowAwayAndFade();

        if (throwRoutine != null)
        {
            yield return throwRoutine;
        }

        GameAudio.StopSfxLoop();
        crowRect.gameObject.SetActive(false);
    }

    private IEnumerator PlayThrowVisual(Action onCrowEscapeFrame)
    {
        if (throwController == null)
        {
            Debug.LogWarning("[PracticeNight] throwController is not assigned; playing audio/crow escape only.");
            onCrowEscapeFrame?.Invoke();
            yield break;
        }

        ResolveThrowVisual();
        if (throwVisualRoot == null || throwAnimator == null || throwFade == null)
        {
            onCrowEscapeFrame?.Invoke();
            yield break;
        }

        throwVisualRoot.gameObject.SetActive(true);
        throwVisualRoot.SetAsLastSibling();
        if (throwVisualCreatedAtRuntime || !useAuthoredThrowTransform)
        {
            throwVisualRoot.anchoredPosition = throwVisualAnchoredPosition;
            throwVisualRoot.sizeDelta = throwVisualSize;
        }
        throwVisualRoot.localScale = throwVisualNaturalScale * Mathf.Max(0.01f, throwVisualScale);
        throwFade.alpha = 1f;
        throwAnimator.runtimeAnimatorController = throwController;
        throwAnimator.speed = 1f;
        throwAnimator.Play(throwStateName, 0, 0f);
        yield return WaitForCurrentAnimatorState(throwAnimator, GetCrowEscapeThrowSeconds(), onCrowEscapeFrame);
        yield return FadeCanvasGroup(throwFade, 0f, throwFadeOutDuration);
        throwVisualRoot.gameObject.SetActive(false);
    }

    private float GetCrowEscapeThrowSeconds()
    {
        int frameOffset = Mathf.Max(0, crowEscapeThrowFrameNumber - throwFirstFrameNumber);
        return frameOffset / Mathf.Max(0.01f, throwClipFps);
    }

    private IEnumerator MoveCrowAwayAndFade()
    {
        Vector2 startPosition = crowRect.anchoredPosition;
        Vector2 targetPosition = startPosition + crowShooOffset;
        float safeDuration = Mathf.Max(0.01f, crowShooDuration);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smooth = SmoothStep(t);
            crowRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, smooth);
            if (crowFade != null)
            {
                crowFade.alpha = Mathf.Lerp(1f, 0f, smooth);
            }
            yield return null;
        }

        crowRect.anchoredPosition = targetPosition;
        if (crowFade != null) crowFade.alpha = 0f;
    }

    private void StartCrowHover()
    {
        StopCrowHover();
        crowHoverRoutine = StartCoroutine(CrowHoverRoutine(crowRect.anchoredPosition));
    }

    private void StopCrowHover()
    {
        if (crowHoverRoutine != null)
        {
            StopCoroutine(crowHoverRoutine);
            crowHoverRoutine = null;
        }
    }

    private IEnumerator CrowHoverRoutine(Vector2 origin)
    {
        float frequency = Mathf.Max(0.01f, crowHoverFrequency);
        while (true)
        {
            Vector2 position = origin;
            position.y += Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * Mathf.Max(0f, crowHoverAmplitude);
            crowRect.anchoredPosition = position;
            yield return null;
        }
    }

    private IEnumerator MoveCrowToWorld(Vector3 target, float duration)
    {
        Vector3 start = crowRect.position;
        float safeDuration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            crowRect.position = Vector3.LerpUnclamped(start, target, SmoothStep(Mathf.Clamp01(elapsed / safeDuration)));
            yield return null;
        }
        crowRect.position = target;
    }

    private IEnumerator WaitForCrowFlyLoopBoundary()
    {
        yield return null;

        float guard = 0f;
        while (guard < 2f)
        {
            float normalized = crowAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
            if (normalized >= 0.98f)
            {
                yield break;
            }

            guard += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForCurrentAnimatorState(Animator animator)
    {
        yield return WaitForCurrentAnimatorState(animator, -1f, null);
    }

    private IEnumerator WaitForCurrentAnimatorState(Animator animator, float signalSeconds, Action onSignal)
    {
        if (animator == null) yield break;

        yield return null;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float length = Mathf.Max(0.01f, info.length);
        float guard = 0f;
        bool signaled = false;
        while (guard < length + 1f)
        {
            info = animator.GetCurrentAnimatorStateInfo(0);
            float stateSeconds = Mathf.Max(0f, info.normalizedTime) * length;
            if (!signaled && signalSeconds >= 0f && stateSeconds >= signalSeconds)
            {
                signaled = true;
                onSignal?.Invoke();
            }

            if (info.normalizedTime >= 1f)
            {
                break;
            }

            guard += Time.deltaTime;
            yield return null;
        }

        if (!signaled)
        {
            onSignal?.Invoke();
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;

        float start = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, SmoothStep(Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        group.alpha = target;
    }

    private void ConfigureCrowImage()
    {
        Image crowImage = crowRect.GetComponent<Image>();
        if (crowImage == null)
        {
            return;
        }

        crowImage.preserveAspect = true;
        crowImage.raycastTarget = false;
        crowImage.color = Color.white;

        if (crowStoneEdgeCropPixels > 0f)
        {
            CrowStoneEdgeCrop crop = crowRect.GetComponent<CrowStoneEdgeCrop>();
            if (crop == null) crop = crowRect.gameObject.AddComponent<CrowStoneEdgeCrop>();
            crop.Configure(crowAnimator, crowStoneEdgeCropPixels);
        }
    }

    private void ResolveThrowVisual()
    {
        if (throwVisualRoot == null)
        {
            Canvas parentCanvas = crowCanvas != null ? crowCanvas : FindObjectOfType<Canvas>();
            if (parentCanvas == null)
            {
                return;
            }

            GameObject visual = new GameObject("PracticeThrowVisual", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Animator));
            throwVisualRoot = visual.GetComponent<RectTransform>();
            throwVisualCreatedAtRuntime = true;
            throwVisualRoot.SetParent(parentCanvas.transform, false);
            throwVisualRoot.anchorMin = new Vector2(0.5f, 0.5f);
            throwVisualRoot.anchorMax = new Vector2(0.5f, 0.5f);
            throwVisualRoot.pivot = new Vector2(0.5f, 0.5f);
            throwVisualRoot.localScale = Vector3.one;

            Image image = visual.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
        }

        throwFade = EnsureCanvasGroup(throwVisualRoot.gameObject);
        CaptureThrowVisualNaturalScale();
        throwAnimator = throwVisualRoot.GetComponent<Animator>();
        if (throwAnimator == null)
        {
            throwAnimator = throwVisualRoot.gameObject.AddComponent<Animator>();
        }

        Image throwImage = throwVisualRoot.GetComponent<Image>();
        if (throwImage == null)
        {
            throwImage = throwVisualRoot.gameObject.AddComponent<Image>();
        }
        throwImage.raycastTarget = false;
        throwImage.preserveAspect = true;
    }

    private void CaptureThrowVisualNaturalScale()
    {
        if (throwVisualRoot == null || throwVisualNaturalScaleCaptured)
        {
            return;
        }

        throwVisualNaturalScale = throwVisualRoot.localScale;
        throwVisualNaturalScaleCaptured = true;
    }

    private void ResolveWaypointPath()
    {
        if (crowWaypointPath != null)
        {
            return;
        }

        GameObject pathObject = GameObject.Find("waypoints");
        if (pathObject != null)
        {
            crowWaypointPath = pathObject.transform;
        }
    }

    private string GetCrowWaypointName(int index)
    {
        if (crowIntroWaypoints == null || index < 0 || index >= crowIntroWaypoints.Length)
        {
            return "";
        }

        return crowIntroWaypoints[index];
    }

    private Vector3 WaypointPosition(string waypointName, Vector3 fallback)
    {
        if (crowWaypointPath == null || string.IsNullOrEmpty(waypointName))
        {
            return fallback;
        }

        Transform waypoint = crowWaypointPath.Find(waypointName);
        if (waypoint == null)
        {
            Debug.LogWarning($"[PracticeNight] waypoint '{waypointName}' not found under {crowWaypointPath.name}.");
            return fallback;
        }

        return waypoint.position;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        return group;
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

    private static float EaseOut(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - (1f - value) * (1f - value);
    }
}
