using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

public sealed class OwlGreetingCutscene : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private string frameNamePrefix = "owl";
    [SerializeField] private bool useSingleVisibleFrame = false;
    [SerializeField] private bool preservePlacedFrameTransforms = true;
    [SerializeField] private float startDelay = 0.1f;
    [SerializeField] private int loopCount = 2;
    [SerializeField] private float greetingDuration = 5.35f;
    [SerializeField, HideInInspector] private float frameDuration = 0.16f;
    [SerializeField] private float openingFrameDuration = 0.35f;
    [SerializeField] private float loopFrameDuration = 0.35f;
    [SerializeField] private bool playFirstFrameOnceThenLoopRest = true;
    [SerializeField] private bool holdLastFrame = true;

    [Header("Friendly Motion")]
    [SerializeField] private bool addGreetingBounce = false;
    [SerializeField] private float bounceHeight = 0f;
    [SerializeField] private float bounceScale = 1f;

    [Header("Zoom")]
    [SerializeField] private bool zoomBeforeGreeting = true;
    [SerializeField] private float zoomDuration = 0.75f;
    [SerializeField] private float zoomScale = 1.22f;
    [SerializeField] private Vector2 zoomFocusPosition = new Vector2(320f, -120f);
    [SerializeField] private float holdAfterZoom = 0.15f;
    [SerializeField] private bool restoreZoomAfterGreeting = true;
    [SerializeField] private float restoreZoomDuration = 0.55f;
    [SerializeField] private RectTransform zoomRoot;
    [SerializeField] private RectTransform backgroundBounds;
    [SerializeField] private bool clampZoomToBackground = true;
    [SerializeField] private Vector2 zoomViewportPadding = new Vector2(24f, 24f);

    [Header("Background Focus")]
    [SerializeField] private bool dimBackgroundBeforeZoom = true;
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private float dimFadeInDuration = 0.35f;
    [SerializeField] private float dimFadeOutDuration = 0.45f;

    [Header("Bear Focus Follow Up")]
    [SerializeField] private bool playBearFocusAfterGreeting = true;
    [SerializeField] private RectTransform bearRoot;
    [SerializeField] private float bearFocusDelay = 0.15f;
    [SerializeField] private float bearGrowDuration = 0.45f;
    [SerializeField] private float bearFocusScaleMultiplier = 1.18f;
    [SerializeField] private float bearFocusGreetingDuration = 9.35f;
    [SerializeField] private bool bearFocusUseHierarchyOrder = true;
    [SerializeField] private string bearFocusFrameSequence = "owl (1), owl (6), owl (2), owl (7), owl (9), owl (3)";
    [SerializeField] private string bearFocusExcludedFrames = "owl (5)";
    [SerializeField] private bool restoreBearScaleAfterFocus = true;
    [SerializeField] private bool restoreBackgroundAfterBearFocus = true;

    [Header("Voice — Greeting (3 วรรค)")]
    [Tooltip("วรรค1: ระวังนะ!")]
    [SerializeField] private AudioClip greetingPhrase1Clip;
    [Tooltip("วรรค2: เจ้าหมีตัวใหญ่บุกเข้ามาแล้ว")]
    [SerializeField] private AudioClip greetingPhrase2Clip;
    [Tooltip("วรรค3: เราต้องช่วยกันไล่มันไป")]
    [SerializeField] private AudioClip greetingPhrase3Clip;
    [HideInInspector] [SerializeField] private AudioClip greetingVoiceClip; // legacy fallback (วรรค1 ถ้าไม่ assign phrase clips)

    [Header("Voice — Bear Focus")]
    [SerializeField] private AudioSource voiceAudioSource;
    [SerializeField] private AudioClip bearFocusLookVoiceClip;
    [SerializeField] private AudioClip bearFocusMissionVoiceClip;

    [Header("Voice (TTS) — id wins over the baked clip; falls back to the clip on any failure")]
    [SerializeField] private TtsApiClient ttsClient;
    [Tooltip("/tts line for greeting วรรค1 (ระวังนะ!).")]
    [SerializeField] private string greetingPhrase1LineId;
    [Tooltip("/tts line for greeting วรรค2 (เจ้าหมีตัวใหญ่บุกเข้ามาแล้ว).")]
    [SerializeField] private string greetingPhrase2LineId;
    [Tooltip("/tts line for greeting วรรค3 (เราต้องช่วยกันไล่มันไป).")]
    [SerializeField] private string greetingPhrase3LineId;
    [HideInInspector] [SerializeField] private string greetingLineId; // legacy fallback TTS for วรรค1
    [Tooltip("/tts line for the second intro line (bear-focus 'look/mission' clip; e.g. paa_intro_owl_2).")]
    [SerializeField] private string bearFocusLookLineId;
    [SerializeField] private float voiceStartDelay = 0f;
    [SerializeField] private float bearFocusVoiceGap = 0.05f;
    [SerializeField] private bool useVoiceClipLengthForTalkDuration = true;

    [Header("Hello Animation — plays once before the first greeting phrase")]
    [Tooltip("Wave animation played once per scene before talking starts.")]
    [SerializeField] private OwlHelloSequence owlHello;

    [Header("Talking Prefab Animation")]
    [Tooltip("Animator on the owl prefab under OwlRoot. Empty = find the first child Animator (the Animator on OwlRoot itself is ignored).")]
    [SerializeField] private Animator talkingAnimator;
    [SerializeField] private string talkingStateName = "Owl";
    // Per-phrase (วรรค) owl mouth speed: Seconds Per Loop = how long one mouth loop takes for that
    // line (smaller = faster); >0 uses it, 0 falls back to that line's Animation Speed multiplier.
    [Tooltip("Greeting วรรค1 (ระวังนะ!) — Animation Speed multiplier (used when Seconds Per Loop is 0).")]
    [SerializeField, Min(0.01f)] private float talkingAnimationSpeed = 1f;
    [Tooltip("Greeting วรรค1 (ระวังนะ!) — Seconds per mouth loop (0 = use Animation Speed).")]
    [SerializeField, Min(0f)] private float talkingSecondsPerLoop = 0f;
    [Tooltip("Greeting วรรค2 (เจ้าหมีตัวใหญ่บุกเข้ามาแล้ว) — Animation Speed multiplier.")]
    [SerializeField, Min(0.01f)] private float greetingPhrase2AnimationSpeed = 1f;
    [Tooltip("Greeting วรรค2 — Seconds per mouth loop (0 = use Animation Speed).")]
    [SerializeField, Min(0f)] private float greetingPhrase2SecondsPerLoop = 0f;
    [Tooltip("Greeting วรรค3 (เราต้องช่วยกันไล่มันไป) — Animation Speed multiplier.")]
    [SerializeField, Min(0.01f)] private float greetingPhrase3AnimationSpeed = 1f;
    [Tooltip("Greeting วรรค3 — Seconds per mouth loop (0 = use Animation Speed).")]
    [SerializeField, Min(0f)] private float greetingPhrase3SecondsPerLoop = 0f;
    [Tooltip("Bear-focus 'look' line — Animation Speed multiplier.")]
    [SerializeField, Min(0.01f)] private float bearFocusLookAnimationSpeed = 1f;
    [Tooltip("Bear-focus 'look' line — Seconds per mouth loop (0 = use Animation Speed).")]
    [SerializeField, Min(0f)] private float bearFocusLookSecondsPerLoop = 0f;
    [Tooltip("Bear-focus 'mission' line — Animation Speed multiplier.")]
    [SerializeField, Min(0.01f)] private float bearFocusMissionAnimationSpeed = 1f;
    [Tooltip("Bear-focus 'mission' line — Seconds per mouth loop (0 = use Animation Speed).")]
    [SerializeField, Min(0f)] private float bearFocusMissionSecondsPerLoop = 0f;
    [Tooltip("Hide the owl prefab whenever no voice line is playing.")]
    [SerializeField] private bool hideTalkingPrefabWhenSilent = true;
    [Tooltip("Seconds to fade the talking owl in when it enters and out when it leaves. 0 = pop instantly.")]
    [SerializeField, Min(0f)] private float talkingFadeDuration = 0.35f;
    [Tooltip("After a whole talking round finishes (all its phrases), freeze the owl on its current frame for this long before fading out. 0 = no hold.")]
    [SerializeField, Min(0f)] private float holdFrozenAfterRound = 1f;

    [Header("Book Reveal")]
    [SerializeField] private bool playBookRevealAfterBearFocus = true;
    [SerializeField] private RectTransform bookCraftRoot;
    [SerializeField] private Image bookCraftImage;
    [SerializeField] private float bookRevealDelay = 0.5f;
    [SerializeField] private Vector2 bookRevealStartPosition = Vector2.zero;
    [SerializeField] private float bookRevealStartScale = 0.08f;
    [SerializeField] private float bookRevealDuration = 1.15f;
    [SerializeField] private bool fadeOwlBeforeBookReveal = true;
    [SerializeField] private float owlFadeOutBeforeBookDuration = 0.6f;
    [SerializeField] private bool moveOwlToBookRevealPosition = false;
    [SerializeField] private float owlBookRevealMoveDuration = 0.75f;
    [SerializeField] private bool usePlacedOwlPositionAsBookRevealTarget = true;
    [SerializeField] private Vector2 owlBookRevealStartPosition = new Vector2(0f, -115f);
    [SerializeField] private Vector2 owlBookRevealTargetPosition = new Vector2(180f, -115f);

    [Header("Magic Stone Puzzle")]
    [SerializeField] private bool playMagicStonePuzzleAfterBookReveal = true;
    [SerializeField] private MagicStonePuzzleController magicStonePuzzle;

    private readonly List<OwlFrame> frames = new List<OwlFrame>();
    private readonly List<ZoomTarget> zoomTargets = new List<ZoomTarget>();
    private Coroutine greetingRoutine;
    private Coroutine voiceRoutine;
    private RectTransform owlRootRect;
    private bool hasZoomState;
    private RectTransform dimOverlayRect;
    private Image dimOverlayImage;
    private bool hasBookRevealTarget;
    private Vector2 bookRevealTargetPosition;
    private Vector3 bookRevealTargetScale;
    private Color bookRevealTargetColor = Color.white;
    private Vector2 cachedOwlBookRevealTargetPosition;
    private RectTransform talkingRectTransform;
    private Vector2 talkingPlacedPosition;
    private Vector3 talkingPlacedScale;
    private Quaternion talkingPlacedRotation;
    private bool hasTalkingPlacedTransform;
    // The talking-speed knobs for the round currently playing (set per PlayTalkingSequence call so
    // each round — greeting vs bear-focus — uses its own inspector values).
    private float activeTalkingSpeed = 1f;
    private float activeTalkingSecondsPerLoop = 0f;

    private struct OwlFrame
    {
        public string Name;
        public Transform Target;
        public RectTransform RectTransform;
        public Image Image;
        public Sprite Sprite;
        public Color Color;
        public bool PreserveAspect;
        public Vector2 Position;
        public Vector2 SizeDelta;
        public Vector3 Scale;
    }

    private struct ZoomTarget
    {
        public RectTransform RectTransform;
        public Vector2 StartPosition;
        public Vector2 TargetPosition;
        public Vector3 StartScale;
        public Vector3 TargetScale;
    }

    private bool ShouldUseSingleVisibleFrame => useSingleVisibleFrame && !preservePlacedFrameTransforms;
    private bool UsesTalkingPrefabAnimator => talkingAnimator != null;

    private void OnValidate()
    {
        ApplyPreservePlacedFrameSettings();
    }

    private void Awake()
    {
        owlRootRect = transform as RectTransform;
        ResolveTalkingAnimator();
        CacheTalkingPlacedTransform();
        CacheOwlBookRevealTarget();
        PrepareOwlForBookRevealTarget();
        ApplyPreservePlacedFrameSettings();
        CacheFrames();
        CacheBookRevealTarget();
        PrepareBookForReveal();
        PrepareMagicStonePuzzle();
        PreloadVoiceClips();
        ResolveTtsLines();
        ShowFrame(0);
        if (UsesTalkingPrefabAnimator)
        {
            StopTalkingAnimation();
        }
        else
        {
            SetOwlFramesAlpha(0f); // legacy frame setup fades in before the greeting
        }
    }

    // TTS wins over the baked clip when a line id is set: fetched async + cached on the client, so it
    // is ready by the time the greeting plays (after the intro zoom). Any failure -> keep the clip.
    private void ResolveTtsLines()
    {
        if (ttsClient == null) ttsClient = FindObjectOfType<TtsApiClient>();
        if (ttsClient == null) return;
        if (!string.IsNullOrWhiteSpace(greetingPhrase1LineId))
            ttsClient.GetLine(greetingPhrase1LineId, c => { if (c != null) greetingPhrase1Clip = c; });
        else if (!string.IsNullOrWhiteSpace(greetingLineId))
            ttsClient.GetLine(greetingLineId, c => { if (c != null) { greetingPhrase1Clip = c; greetingVoiceClip = c; } });
        if (!string.IsNullOrWhiteSpace(greetingPhrase2LineId))
            ttsClient.GetLine(greetingPhrase2LineId, c => { if (c != null) greetingPhrase2Clip = c; });
        if (!string.IsNullOrWhiteSpace(greetingPhrase3LineId))
            ttsClient.GetLine(greetingPhrase3LineId, c => { if (c != null) greetingPhrase3Clip = c; });
        if (!string.IsNullOrWhiteSpace(bearFocusLookLineId))
            ttsClient.GetLine(bearFocusLookLineId, c => { if (c != null) bearFocusLookVoiceClip = c; });
    }

    private void OnDisable()
    {
        if (greetingRoutine != null)
        {
            StopCoroutine(greetingRoutine);
            greetingRoutine = null;
        }

        StopVoice();
    }

    public void PlayGreeting()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (greetingRoutine != null)
        {
            StopCoroutine(greetingRoutine);
        }

        StopVoice();
        if (MagicStonePuzzleController.ConsumeRetryAfterCrow())
        {
            greetingRoutine = StartCoroutine(RetryMagicStonePuzzleRoutine());
            return;
        }

        greetingRoutine = StartCoroutine(GreetingRoutine());
    }

    private IEnumerator RetryMagicStonePuzzleRoutine()
    {
        ApplyPreservePlacedFrameSettings();
        CacheFrames();
        // NOTE: do NOT re-cache the book reveal target here. Awake already cached the book's full
        // (authored) position/scale/color; by now PrepareBookForReveal has shrunk it and set alpha 0,
        // so re-caching would capture that hidden state as the "target" and the book would restore
        // invisible (alpha 0) on retry.

        SetOwlFramesAlpha(0f);
        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].Target.gameObject.SetActive(false);
        }

        RestoreOwlFrameColors();

        if (dimOverlayRect != null)
        {
            Destroy(dimOverlayRect.gameObject);
            dimOverlayRect = null;
            dimOverlayImage = null;
        }

        if (bookCraftRoot != null && hasBookRevealTarget)
        {
            bookCraftRoot.gameObject.SetActive(true);
            bookCraftRoot.anchoredPosition = bookRevealTargetPosition;
            bookCraftRoot.localScale = bookRevealTargetScale;
            SetBookRevealAlpha(1f);

            if (bookCraftImage != null)
            {
                bookCraftImage.raycastTarget = false;
            }
        }

        MagicStonePuzzleController puzzle = GetMagicStonePuzzle();
        if (puzzle != null)
        {
            bool simultaneous = MagicStonePuzzleController.ConsumeRetryAfterAlt();
            puzzle.PrepareForIntro();
            yield return null;
            yield return puzzle.PlayIntroReveal(simultaneous);
        }

        greetingRoutine = null;
    }

    private IEnumerator GreetingRoutine()
    {
        ApplyPreservePlacedFrameSettings();
        CacheFrames();

        if (frames.Count == 0)
        {
            greetingRoutine = null;
            yield break;
        }

        ShowFrame(0);
        if (UsesTalkingPrefabAnimator)
        {
            StopTalkingAnimation();
        }
        else
        {
            SetOwlFramesAlpha(0f); // legacy frame setup fades in before the greeting
        }

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

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

        if (holdAfterZoom > 0f)
        {
            yield return new WaitForSeconds(holdAfterZoom);
        }

        if (owlHello != null)
        {
            // Match the wave's frame rate to the talking owl's so the seam owl_hello->owl is continuous.
            float talkFps = GetTalkingDisplayFps(talkingAnimationSpeed, talkingSecondsPerLoop);
            if (talkFps > 0f) owlHello.SetPlaybackFps(talkFps);
            owlHello.SetHideOnComplete(false); // keep the last frame so we can crossfade it out
            yield return owlHello.Play();
            // Crossfade: owl_hello fades out while the talking owl (below it) fades in — smooth dissolve.
            StartCoroutine(owlHello.FadeOut(talkingFadeDuration));
        }

        // ใช้ greetingPhrase1Clip ก่อน ถ้าไม่ได้ assign ให้ fallback ไปที่ greetingVoiceClip (legacy)
        AudioClip phrase1 = greetingPhrase1Clip != null ? greetingPhrase1Clip : greetingVoiceClip;
        yield return PlayTalkingSequence(greetingDuration, null,
            new TalkLine(phrase1, talkingAnimationSpeed, talkingSecondsPerLoop),
            new TalkLine(greetingPhrase2Clip, greetingPhrase2AnimationSpeed, greetingPhrase2SecondsPerLoop),
            new TalkLine(greetingPhrase3Clip, greetingPhrase3AnimationSpeed, greetingPhrase3SecondsPerLoop));

        if (!UsesTalkingPrefabAnimator)
        {
            ShowFrame(holdLastFrame ? frames.Count - 1 : 0);
        }

        if (restoreZoomAfterGreeting && hasZoomState)
        {
            yield return RestoreZoomRoutine();
        }

        if (dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(false, -1);
        }

        if (playBearFocusAfterGreeting)
        {
            yield return BearFocusFollowUpRoutine();
        }

        if (playBookRevealAfterBearFocus)
        {
            yield return FadeOwlOutBeforeBookRevealRoutine();
            yield return PlayBookRevealRoutine();
        }

        if (playMagicStonePuzzleAfterBookReveal)
        {
            yield return PlayMagicStonePuzzleRevealRoutine();
        }

        greetingRoutine = null;
    }

    private IEnumerator BearFocusFollowUpRoutine()
    {
        RectTransform targetBearRoot = GetBearRoot();

        if (targetBearRoot == null)
        {
            yield break;
        }

        if (bearFocusDelay > 0f)
        {
            yield return new WaitForSeconds(bearFocusDelay);
        }

        if (dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(true, GetBearAndOwlDimSiblingIndex(targetBearRoot));
        }

        Vector3 bearStartScale = targetBearRoot.localScale;
        Vector3 bearTargetScale = bearStartScale * Mathf.Max(1f, bearFocusScaleMultiplier);
        yield return ScaleRectTransform(targetBearRoot, bearStartScale, bearTargetScale, bearGrowDuration);

        List<int> bearFocusSequence = bearFocusUseHierarchyOrder
            ? GetHierarchyFrameSequence(bearFocusExcludedFrames)
            : GetFrameSequence(bearFocusFrameSequence, bearFocusExcludedFrames);

        if (bearFocusSequence.Count == 0)
        {
            bearFocusSequence = GetDefaultBearFocusFrameSequence();
        }

        yield return PlayTalkingSequence(
            bearFocusGreetingDuration,
            bearFocusSequence,
            new TalkLine(bearFocusLookVoiceClip, bearFocusLookAnimationSpeed, bearFocusLookSecondsPerLoop),
            new TalkLine(bearFocusMissionVoiceClip, bearFocusMissionAnimationSpeed, bearFocusMissionSecondsPerLoop));

        if (!UsesTalkingPrefabAnimator)
        {
            ShowFrame(holdLastFrame ? GetLastSequenceFrameIndex(bearFocusSequence) : 0);
        }

        if (restoreBearScaleAfterFocus)
        {
            yield return ScaleRectTransform(targetBearRoot, bearTargetScale, bearStartScale, bearGrowDuration);
        }

        if (restoreBackgroundAfterBearFocus && dimBackgroundBeforeZoom)
        {
            yield return FadeDimOverlay(false, -1);
        }
    }

    private void CacheBookRevealTarget()
    {
        if (bookCraftRoot == null)
        {
            Transform bookTransform = transform.Find("book_craft");
            if (bookTransform == null && transform.parent != null)
            {
                bookTransform = transform.parent.Find("book_craft");
            }

            if (bookTransform == null)
            {
                bookTransform = FindDescendant(transform, "book_craft");
            }

            if (bookTransform == null && transform.parent != null)
            {
                bookTransform = FindDescendant(transform.parent, "book_craft");
            }

            bookCraftRoot = bookTransform as RectTransform;
        }

        if (bookCraftRoot == null)
        {
            hasBookRevealTarget = false;
            return;
        }

        if (bookCraftImage == null)
        {
            bookCraftImage = bookCraftRoot.GetComponent<Image>();
            if (bookCraftImage == null)
            {
                bookCraftImage = bookCraftRoot.GetComponentInChildren<Image>(true);
            }
        }

        bookRevealTargetPosition = bookCraftRoot.anchoredPosition;
        bookRevealTargetScale = bookCraftRoot.localScale;

        if (bookCraftImage != null)
        {
            bookRevealTargetColor = bookCraftImage.color;
        }

        hasBookRevealTarget = true;
    }

    private void PrepareBookForReveal()
    {
        if (!playBookRevealAfterBearFocus || !hasBookRevealTarget || bookCraftRoot == null)
        {
            return;
        }

        bookCraftRoot.gameObject.SetActive(true);
        bookCraftRoot.anchoredPosition = bookRevealStartPosition;
        bookCraftRoot.localScale = bookRevealTargetScale * Mathf.Max(0.01f, bookRevealStartScale);
        SetBookRevealAlpha(0f);

        if (bookCraftImage != null)
        {
            bookCraftImage.raycastTarget = false;
        }
    }

    private IEnumerator PlayBookRevealRoutine()
    {
        if (!hasBookRevealTarget || bookCraftRoot == null)
        {
            yield break;
        }

        if (moveOwlToBookRevealPosition && owlRootRect != null)
        {
            yield return MoveOwlToBookRevealTarget();
        }

        if (bookRevealDelay > 0f)
        {
            yield return new WaitForSeconds(bookRevealDelay);
        }

        bookCraftRoot.gameObject.SetActive(true);

        float duration = Mathf.Max(0.01f, bookRevealDuration);
        float startScale = Mathf.Max(0.01f, bookRevealStartScale);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float bookT = bookRevealDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / bookRevealDuration);

            float bookSmooth = SmoothStep(bookT);
            float bookScale = Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(bookT));
            bookCraftRoot.anchoredPosition = Vector2.LerpUnclamped(bookRevealStartPosition, bookRevealTargetPosition, bookSmooth);
            bookCraftRoot.localScale = bookRevealTargetScale * bookScale;
            SetBookRevealAlpha(bookSmooth);

            yield return null;
        }

        bookCraftRoot.anchoredPosition = bookRevealTargetPosition;
        bookCraftRoot.localScale = bookRevealTargetScale;
        SetBookRevealAlpha(1f);

        if (bookCraftImage != null)
        {
            bookCraftImage.raycastTarget = false;
        }

    }

    private IEnumerator FadeOwlInRoutine()
    {
        if (frames.Count == 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, owlFadeOutBeforeBookDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            SetOwlFramesAlpha(SmoothStep(Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetOwlFramesAlpha(1f);
    }

    private IEnumerator FadeOwlOutBeforeBookRevealRoutine()
    {
        if (!fadeOwlBeforeBookReveal || frames.Count == 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, owlFadeOutBeforeBookDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            SetOwlFramesAlpha(1f - smooth);
            yield return null;
        }

        SetOwlFramesAlpha(0f);

        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].Target.gameObject.SetActive(false);
        }

        RestoreOwlFrameColors();
    }

    private void SetOwlFramesAlpha(float alpha)
    {
        float safeAlpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < frames.Count; i++)
        {
            OwlFrame frame = frames[i];
            if (frame.Image == null || !frame.Target.gameObject.activeSelf)
            {
                continue;
            }

            Color color = frame.Color;
            color.a *= safeAlpha;
            frame.Image.color = color;
        }
    }

    private void RestoreOwlFrameColors()
    {
        for (int i = 0; i < frames.Count; i++)
        {
            OwlFrame frame = frames[i];
            if (frame.Image != null)
            {
                frame.Image.color = frame.Color;
            }
        }
    }

    private void CacheOwlBookRevealTarget()
    {
        cachedOwlBookRevealTargetPosition = owlBookRevealTargetPosition;

        if (!usePlacedOwlPositionAsBookRevealTarget || owlRootRect == null)
        {
            return;
        }

        Vector2 placedPosition = owlRootRect.anchoredPosition;
        if ((placedPosition - owlBookRevealStartPosition).sqrMagnitude > 0.01f)
        {
            cachedOwlBookRevealTargetPosition = placedPosition;
        }
    }

    private void PrepareOwlForBookRevealTarget()
    {
        if (!playBookRevealAfterBearFocus || owlRootRect == null)
        {
            return;
        }

        // Only pre-position the owl if we will actually animate it to the book reveal spot.
        // Otherwise keep it at the position the designer placed in the scene.
        if (!moveOwlToBookRevealPosition)
        {
            return;
        }

        owlRootRect.anchoredPosition = owlBookRevealStartPosition;
    }

    private IEnumerator MoveOwlToBookRevealTarget()
    {
        Vector2 owlStartPosition = owlRootRect.anchoredPosition;
        float duration = Mathf.Max(0.01f, owlBookRevealMoveDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            owlRootRect.anchoredPosition = Vector2.LerpUnclamped(
                owlStartPosition,
                cachedOwlBookRevealTargetPosition,
                smooth);
            yield return null;
        }

        owlRootRect.anchoredPosition = cachedOwlBookRevealTargetPosition;
    }

    private void SetBookRevealAlpha(float alpha)
    {
        if (bookCraftImage == null)
        {
            return;
        }

        Color color = bookRevealTargetColor;
        color.a *= Mathf.Clamp01(alpha);
        bookCraftImage.color = color;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform found = FindDescendant(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void PrepareMagicStonePuzzle()
    {
        MagicStonePuzzleController puzzle = GetMagicStonePuzzle();
        if (puzzle != null)
        {
            puzzle.PrepareForIntro();
        }
    }

    private IEnumerator PlayMagicStonePuzzleRevealRoutine()
    {
        MagicStonePuzzleController puzzle = GetMagicStonePuzzle();
        if (puzzle == null)
        {
            yield break;
        }

        yield return puzzle.PlayIntroReveal();
    }

    private MagicStonePuzzleController GetMagicStonePuzzle()
    {
        if (magicStonePuzzle != null)
        {
            return magicStonePuzzle;
        }

        Transform puzzleTransform = transform.parent != null ? transform.parent.Find("magic_stone") : null;
        if (puzzleTransform == null)
        {
            return null;
        }

        magicStonePuzzle = puzzleTransform.GetComponent<MagicStonePuzzleController>();
        if (magicStonePuzzle == null)
        {
            magicStonePuzzle = puzzleTransform.gameObject.AddComponent<MagicStonePuzzleController>();
        }

        return magicStonePuzzle;
    }

    // One spoken phrase (วรรค) = its voice clip + that line's own owl mouth speed.
    private struct TalkLine
    {
        public AudioClip clip;
        public float speed;
        public float secondsPerLoop;
        public TalkLine(AudioClip clip, float speed, float secondsPerLoop)
        {
            this.clip = clip;
            this.speed = speed;
            this.secondsPerLoop = secondsPerLoop;
        }
    }

    // Plays the round's phrases in order, each at its OWN mouth speed (the animator speed is re-set
    // per line). The owl fades in on the first phrase and out after the last; phrases in between
    // just swap speed while the owl keeps talking.
    private IEnumerator PlayTalkingSequence(
        float fallbackDuration,
        List<int> frameSequence,
        params TalkLine[] lines)
    {
        if (voiceStartDelay > 0f)
        {
            yield return new WaitForSeconds(voiceStartDelay);
        }

        StopVoice();
        AudioSource source = GetOrCreateVoiceAudioSource();
        StartTalkingAnimation();
        GameAudio.SetVoiceDucking(true); // drop the music under the owl's voice

        bool fade = UsesTalkingPrefabAnimator && talkingFadeDuration > 0f;
        int lastIndex = LastNonNullLineIndex(lines);
        bool fadedIn = false;
        bool playedAny = false;

        for (int i = 0; i < lines.Length; i++)
        {
            AudioClip clip = lines[i].clip;
            if (clip == null)
            {
                continue;
            }

            // This phrase's own mouth speed.
            activeTalkingSpeed = lines[i].speed;
            activeTalkingSecondsPerLoop = lines[i].secondsPerLoop;
            if (UsesTalkingPrefabAnimator)
            {
                talkingAnimator.speed = GetTalkingAnimatorSpeed();
            }

            if (playedAny && bearFocusVoiceGap > 0f)
            {
                yield return new WaitForSeconds(bearFocusVoiceGap);
            }

            LoadVoiceClip(clip);
            source.clip = clip;
            source.Play();
            playedAny = true;

            float hold = clip.length > 0f ? clip.length : Mathf.Max(0f, fallbackDuration);

            if (fade && !fadedIn)
            {
                SetOwlFramesAlpha(0f);
                yield return FadeTalkingAlpha(0f, 1f, talkingFadeDuration);
                hold = Mathf.Max(0f, hold - talkingFadeDuration);
                fadedIn = true;
            }

            // Talk visualization. On the last phrase, reserve the fade-out window.
            float talkPortion = (fade && i == lastIndex)
                ? Mathf.Max(0f, hold - talkingFadeDuration)
                : hold;
            yield return PlayGreetingFrames(talkPortion, frameSequence);

            // End of the whole round (last phrase): freeze on the current frame, hold, then fade out.
            if (i == lastIndex)
            {
                if (holdFrozenAfterRound > 0f)
                {
                    if (UsesTalkingPrefabAnimator) talkingAnimator.speed = 0f;
                    yield return new WaitForSeconds(holdFrozenAfterRound);
                }

                if (fade)
                {
                    yield return FadeTalkingAlpha(1f, 0f, talkingFadeDuration);
                }
            }
        }

        GameAudio.SetVoiceDucking(false); // owl finished this round — let the music back up
        StopTalkingAnimation();
    }

    private static int LastNonNullLineIndex(TalkLine[] lines)
    {
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].clip != null)
            {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator FadeTalkingAlpha(float from, float to, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            SetOwlFramesAlpha(Mathf.Lerp(from, to, SmoothStep(elapsed / safeDuration)));
            yield return null;
        }

        SetOwlFramesAlpha(to);
    }

    private AudioSource GetOrCreateVoiceAudioSource()
    {
        if (voiceAudioSource == null)
        {
            voiceAudioSource = GetComponent<AudioSource>();
        }

        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
        }

        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;
        voiceAudioSource.spatialBlend = 0f;

        return voiceAudioSource;
    }

    private void PreloadVoiceClips()
    {
        LoadVoiceClip(greetingPhrase1Clip);
        LoadVoiceClip(greetingPhrase2Clip);
        LoadVoiceClip(greetingPhrase3Clip);
        LoadVoiceClip(greetingVoiceClip);
        LoadVoiceClip(bearFocusLookVoiceClip);
        LoadVoiceClip(bearFocusMissionVoiceClip);
    }

    private static void LoadVoiceClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private void StopVoice()
    {
        GameAudio.SetVoiceDucking(false); // owl no longer speaking (interrupt / round end)
        if (voiceRoutine != null)
        {
            StopCoroutine(voiceRoutine);
            voiceRoutine = null;
        }

        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
        }

        StopTalkingAnimation();
    }

    private IEnumerator PlayGreetingFrames(float totalDuration, List<int> frameSequence = null)
    {
        float openingDuration = GetOpeningFrameDuration();
        float loopDuration = GetLoopFrameDuration();
        int frameCount = frameSequence != null && frameSequence.Count > 0 ? frameSequence.Count : frames.Count;
        bool useOpeningFrameOnce = playFirstFrameOnceThenLoopRest && frameCount > 1;

        if (totalDuration > 0f)
        {
            float playedTime = 0f;
            int frameIndex = 0;

            if (useOpeningFrameOnce)
            {
                int openingFrameIndex = GetSequenceFrameIndex(frameSequence, 0);
                float currentDuration = Mathf.Min(openingDuration, totalDuration);
                ShowFrame(openingFrameIndex);
                yield return AnimateFrameMotion(GetVisibleFrame(openingFrameIndex), currentDuration);
                playedTime += currentDuration;
            }

            while (playedTime < totalDuration)
            {
                int visibleIndex = useOpeningFrameOnce
                    ? GetLoopRestFrameIndex(frameSequence, frameIndex)
                    : GetSequenceFrameIndex(frameSequence, frameIndex);
                float remainingTime = totalDuration - playedTime;
                float currentDuration = Mathf.Min(loopDuration, remainingTime);

                ShowFrame(visibleIndex);
                yield return AnimateFrameMotion(GetVisibleFrame(visibleIndex), currentDuration);

                playedTime += currentDuration;
                frameIndex++;
            }

            yield break;
        }

        int loops = Mathf.Max(1, loopCount);

        if (useOpeningFrameOnce)
        {
            int openingFrameIndex = GetSequenceFrameIndex(frameSequence, 0);
            ShowFrame(openingFrameIndex);
            yield return AnimateFrameMotion(GetVisibleFrame(openingFrameIndex), openingDuration);
        }

        for (int loopIndex = 0; loopIndex < loops; loopIndex++)
        {
            int loopFrameCount = useOpeningFrameOnce ? frameCount - 1 : frameCount;

            for (int frameIndex = 0; frameIndex < loopFrameCount; frameIndex++)
            {
                int visibleIndex = useOpeningFrameOnce
                    ? GetLoopRestFrameIndex(frameSequence, frameIndex)
                    : GetSequenceFrameIndex(frameSequence, frameIndex);
                ShowFrame(visibleIndex);
                yield return AnimateFrameMotion(GetVisibleFrame(visibleIndex), loopDuration);
            }
        }
    }

    private float GetOpeningFrameDuration()
    {
        float fallback = frameDuration > 0f ? frameDuration : 0.16f;
        return Mathf.Max(0.01f, openingFrameDuration > 0f ? openingFrameDuration : fallback);
    }

    private float GetLoopFrameDuration()
    {
        float fallback = frameDuration > 0f ? frameDuration : 0.16f;
        return Mathf.Max(0.01f, loopFrameDuration > 0f ? loopFrameDuration : fallback);
    }

    private List<int> GetHierarchyFrameSequence(string excludedFrameText = null)
    {
        List<int> sequence = new List<int>();
        HashSet<string> excludedFrames = GetFrameNameSet(excludedFrameText);

        for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            if (!excludedFrames.Contains(frames[frameIndex].Name))
            {
                sequence.Add(frameIndex);
            }
        }

        return sequence;
    }

    private List<int> GetFrameSequence(string sequenceText, string excludedFrameText = null)
    {
        List<int> sequence = new List<int>();
        HashSet<string> excludedFrames = GetFrameNameSet(excludedFrameText);

        if (string.IsNullOrWhiteSpace(sequenceText))
        {
            return sequence;
        }

        string[] frameNames = sequenceText.Split(',');

        for (int nameIndex = 0; nameIndex < frameNames.Length; nameIndex++)
        {
            string frameName = frameNames[nameIndex].Trim();

            if (string.IsNullOrWhiteSpace(frameName))
            {
                continue;
            }

            if (excludedFrames.Contains(frameName))
            {
                continue;
            }

            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                if (string.Equals(frames[frameIndex].Name, frameName, StringComparison.OrdinalIgnoreCase))
                {
                    sequence.Add(frameIndex);
                    break;
                }
            }
        }

        return sequence;
    }

    private List<int> GetDefaultBearFocusFrameSequence()
    {
        return GetFrameSequence("owl (1), owl (6), owl (2), owl (7), owl (9), owl (3)", bearFocusExcludedFrames);
    }

    private static HashSet<string> GetFrameNameSet(string frameNameText)
    {
        HashSet<string> frameNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(frameNameText))
        {
            return frameNames;
        }

        string[] names = frameNameText.Split(',');

        for (int index = 0; index < names.Length; index++)
        {
            string frameName = names[index].Trim();

            if (!string.IsNullOrWhiteSpace(frameName))
            {
                frameNames.Add(frameName);
            }
        }

        return frameNames;
    }

    private int GetSequenceFrameIndex(List<int> frameSequence, int frameIndex)
    {
        if (frameSequence != null && frameSequence.Count > 0)
        {
            return frameSequence[frameIndex % frameSequence.Count];
        }

        return frameIndex % frames.Count;
    }

    private int GetLoopRestFrameIndex(List<int> frameSequence, int frameIndex)
    {
        int frameCount = frameSequence != null && frameSequence.Count > 0 ? frameSequence.Count : frames.Count;

        if (frameCount <= 1)
        {
            return GetSequenceFrameIndex(frameSequence, 0);
        }

        int loopIndex = 1 + frameIndex % (frameCount - 1);
        return GetSequenceFrameIndex(frameSequence, loopIndex);
    }

    private int GetLastSequenceFrameIndex(List<int> frameSequence)
    {
        if (frameSequence != null && frameSequence.Count > 0)
        {
            return frameSequence[frameSequence.Count - 1];
        }

        return frames.Count - 1;
    }

    private IEnumerator ScaleRectTransform(RectTransform target, Vector3 fromScale, Vector3 toScale, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / safeDuration));
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, smooth);
            yield return null;
        }

        target.localScale = toScale;
    }

    private void CacheFrames()
    {
        // CacheFrames is called again immediately before playback. At that point the owl may
        // already be hidden (alpha 0). Preserve the original authored colors so a later fade-in
        // does not multiply against a newly cached zero alpha forever.
        Dictionary<Transform, Color> authoredColors = new Dictionary<Transform, Color>();
        for (int index = 0; index < frames.Count; index++)
        {
            OwlFrame cachedFrame = frames[index];
            if (cachedFrame.Target != null && cachedFrame.Image != null)
            {
                authoredColors[cachedFrame.Target] = cachedFrame.Color;
            }
        }

        frames.Clear();

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);

            if (!string.IsNullOrWhiteSpace(frameNamePrefix)
                && !child.name.StartsWith(frameNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Image image = child.GetComponent<Image>();
            RectTransform rect = child as RectTransform;

            if (image == null && rect == null)
            {
                continue;
            }

            Color authoredColor = image != null ? image.color : Color.white;
            if (authoredColors.TryGetValue(child, out Color cachedColor))
            {
                authoredColor = cachedColor;
            }

            frames.Add(new OwlFrame
            {
                Name = child.name,
                Target = child,
                RectTransform = rect,
                Image = image,
                Sprite = image != null ? image.sprite : null,
                Color = authoredColor,
                PreserveAspect = image == null || image.preserveAspect,
                Position = rect != null ? rect.anchoredPosition : new Vector2(child.localPosition.x, child.localPosition.y),
                SizeDelta = rect != null ? rect.sizeDelta : Vector2.zero,
                Scale = child.localScale
            });
        }
    }

    private void ResolveTalkingAnimator()
    {
        if (talkingAnimator != null && talkingAnimator.transform != transform)
        {
            return;
        }

        talkingAnimator = null;
        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        for (int index = 0; index < childAnimators.Length; index++)
        {
            Animator candidate = childAnimators[index];
            if (candidate != null && candidate.transform != transform)
            {
                talkingAnimator = candidate;
                break;
            }
        }
    }

    private void CacheTalkingPlacedTransform()
    {
        if (!UsesTalkingPrefabAnimator)
        {
            return;
        }

        talkingRectTransform = talkingAnimator.transform as RectTransform;
        if (talkingRectTransform == null)
        {
            return;
        }

        talkingPlacedPosition = talkingRectTransform.anchoredPosition;
        talkingPlacedScale = talkingRectTransform.localScale;
        talkingPlacedRotation = talkingRectTransform.localRotation;
        hasTalkingPlacedTransform = true;
    }

    private void StartTalkingAnimation()
    {
        ResolveTalkingAnimator();
        if (!UsesTalkingPrefabAnimator)
        {
            return;
        }

        GameObject talkingObject = talkingAnimator.gameObject;
        talkingObject.SetActive(true);
        RestoreTalkingPlacedTransform();
        SetOwlFramesAlpha(1f);

        talkingAnimator.speed = GetTalkingAnimatorSpeed();
        if (!string.IsNullOrWhiteSpace(talkingStateName))
        {
            int stateHash = Animator.StringToHash(talkingStateName.Trim());
            if (talkingAnimator.HasState(0, stateHash))
            {
                talkingAnimator.Play(stateHash, 0, 0f);
            }
        }
    }

    private void StopTalkingAnimation()
    {
        if (!UsesTalkingPrefabAnimator)
        {
            return;
        }

        talkingAnimator.speed = 0f;
        RestoreTalkingPlacedTransform();

        if (hideTalkingPrefabWhenSilent)
        {
            talkingAnimator.gameObject.SetActive(false);
        }
    }

    private void RestoreTalkingPlacedTransform()
    {
        if (!hasTalkingPlacedTransform || talkingRectTransform == null)
        {
            return;
        }

        talkingRectTransform.anchoredPosition = talkingPlacedPosition;
        talkingRectTransform.localScale = talkingPlacedScale;
        talkingRectTransform.localRotation = talkingPlacedRotation;
    }

    // Effective sprite-swap rate (frames/sec) the talking owl runs at for the given line, so the
    // owl_hello wave can match it and the seam has no cadence jump. 0 = unknown (caller keeps default).
    private float GetTalkingDisplayFps(float speed, float secondsPerLoop)
    {
        if (!UsesTalkingPrefabAnimator) return 0f;
        RuntimeAnimatorController controller = talkingAnimator.runtimeAnimatorController;
        if (controller == null || controller.animationClips == null) return 0f;

        AnimationClip clip = null;
        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (clip == null) clip = clips[i];
            if (string.Equals(clips[i].name, talkingStateName, StringComparison.OrdinalIgnoreCase))
            {
                clip = clips[i];
                break;
            }
        }

        if (clip == null || clip.length <= 0f || clip.frameRate <= 0f) return 0f;
        float animSpeed = secondsPerLoop > 0f
            ? clip.length / Mathf.Max(0.01f, secondsPerLoop)
            : Mathf.Max(0.01f, speed);
        return clip.frameRate * animSpeed;
    }

    private float GetTalkingAnimatorSpeed()
    {
        if (activeTalkingSecondsPerLoop <= 0f)
        {
            return Mathf.Max(0.01f, activeTalkingSpeed);
        }

        RuntimeAnimatorController controller = talkingAnimator.runtimeAnimatorController;
        if (controller == null || controller.animationClips == null)
        {
            return Mathf.Max(0.01f, activeTalkingSpeed);
        }

        AnimationClip[] clips = controller.animationClips;
        AnimationClip selectedClip = null;
        for (int index = 0; index < clips.Length; index++)
        {
            AnimationClip clip = clips[index];
            if (clip == null)
            {
                continue;
            }

            if (selectedClip == null)
            {
                selectedClip = clip;
            }

            if (string.Equals(clip.name, talkingStateName, StringComparison.OrdinalIgnoreCase))
            {
                selectedClip = clip;
                break;
            }
        }

        if (selectedClip == null || selectedClip.length <= 0f)
        {
            return Mathf.Max(0.01f, activeTalkingSpeed);
        }

        return selectedClip.length / Mathf.Max(0.01f, activeTalkingSecondsPerLoop);
    }

    private void ShowFrame(int visibleIndex)
    {
        if (frames.Count == 0)
        {
            return;
        }

        visibleIndex = Mathf.Clamp(visibleIndex, 0, frames.Count - 1);

        if (ShouldUseSingleVisibleFrame)
        {
            OwlFrame visualFrame = frames[0];
            OwlFrame sourceFrame = frames[visibleIndex];

            for (int index = 0; index < frames.Count; index++)
            {
                frames[index].Target.gameObject.SetActive(index == 0);
            }

            ApplyVisualAppearance(visualFrame, sourceFrame);
            ApplyFrameMotion(visualFrame, visualFrame.Position, visualFrame.Scale);
            return;
        }

        for (int index = 0; index < frames.Count; index++)
        {
            OwlFrame frame = frames[index];
            bool visible = index == visibleIndex;
            frame.Target.gameObject.SetActive(visible);

            if (visible)
            {
                ApplyFrameMotion(frame, frame.Position, frame.Scale);
            }
        }
    }

    private OwlFrame GetVisibleFrame(int frameIndex)
    {
        if (ShouldUseSingleVisibleFrame)
        {
            return frames[0];
        }

        return frames[Mathf.Clamp(frameIndex, 0, frames.Count - 1)];
    }

    private IEnumerator AnimateFrameMotion(OwlFrame frame, float duration)
    {
        if (preservePlacedFrameTransforms || !addGreetingBounce || duration <= 0f)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector2 basePosition = frame.Position;
        Vector3 baseScale = frame.Scale;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float lift = Mathf.Sin(t * Mathf.PI) * bounceHeight;
            float scale = Mathf.Lerp(1f, bounceScale, Mathf.Sin(t * Mathf.PI));

            ApplyFrameMotion(
                frame,
                basePosition + Vector2.up * lift,
                new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z));

            yield return null;
        }

        ApplyFrameMotion(frame, basePosition, baseScale);
    }

    private IEnumerator ZoomInRoutine()
    {
        PrepareZoomTargets();

        if (zoomTargets.Count == 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, zoomDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            ApplyZoomTargets(smooth, false);
            yield return null;
        }

        ApplyZoomTargets(1f, false);
        hasZoomState = true;
    }

    private IEnumerator RestoreZoomRoutine()
    {
        float duration = Mathf.Max(0.01f, restoreZoomDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            ApplyZoomTargets(smooth, true);
            yield return null;
        }

        ApplyZoomTargets(1f, true);
        hasZoomState = false;
    }

    private void PrepareZoomTargets()
    {
        zoomTargets.Clear();

        RectTransform parent = GetZoomParent();
        RectTransform focusFrame = GetFocusFrame();

        if (parent == null || focusFrame == null)
        {
            return;
        }

        Bounds focusBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, focusFrame);
        Vector2 focusPoint = focusBounds.center;
        float targetScale = Mathf.Max(1f, zoomScale);

        if (zoomRoot != null)
        {
            AddZoomTarget(zoomRoot, focusPoint, targetScale);
            ClampZoomTargetsToBackground(parent);
            return;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            RectTransform child = parent.GetChild(index) as RectTransform;

            if (child == null)
            {
                continue;
            }

            if (dimOverlayRect != null && child == dimOverlayRect)
            {
                continue;
            }

            AddZoomTarget(child, focusPoint, targetScale);
        }

        ClampZoomTargetsToBackground(parent);
    }

    private void AddZoomTarget(RectTransform target, Vector2 focusPoint, float targetScale)
    {
        Vector2 startPosition = target.anchoredPosition;
        Vector2 scaledOffset = (startPosition - focusPoint) * targetScale;
        Vector2 targetPosition = zoomFocusPosition + scaledOffset;

        zoomTargets.Add(new ZoomTarget
        {
            RectTransform = target,
            StartPosition = startPosition,
            TargetPosition = targetPosition,
            StartScale = target.localScale,
            TargetScale = target.localScale * targetScale
        });
    }

    private void ClampZoomTargetsToBackground(RectTransform parent)
    {
        if (!clampZoomToBackground || parent == null)
        {
            return;
        }

        RectTransform background = GetBackgroundBounds(parent);

        if (background == null)
        {
            return;
        }

        int backgroundTargetIndex = -1;

        for (int index = 0; index < zoomTargets.Count; index++)
        {
            if (zoomTargets[index].RectTransform == background)
            {
                backgroundTargetIndex = index;
                break;
            }
        }

        if (backgroundTargetIndex < 0)
        {
            return;
        }

        ZoomTarget backgroundTarget = zoomTargets[backgroundTargetIndex];
        Vector2 viewportHalfSize = parent.rect.size * 0.5f - zoomViewportPadding;
        Vector2 backgroundHalfSize = new Vector2(
            background.rect.width * Mathf.Abs(backgroundTarget.TargetScale.x),
            background.rect.height * Mathf.Abs(backgroundTarget.TargetScale.y)) * 0.5f;

        if (viewportHalfSize.x <= 0f || viewportHalfSize.y <= 0f)
        {
            return;
        }

        Vector2 clampedPosition = backgroundTarget.TargetPosition;
        clampedPosition.x = ClampBackgroundCenter(clampedPosition.x, backgroundHalfSize.x, viewportHalfSize.x);
        clampedPosition.y = ClampBackgroundCenter(clampedPosition.y, backgroundHalfSize.y, viewportHalfSize.y);

        Vector2 correction = clampedPosition - backgroundTarget.TargetPosition;

        if (correction == Vector2.zero)
        {
            return;
        }

        for (int index = 0; index < zoomTargets.Count; index++)
        {
            ZoomTarget target = zoomTargets[index];
            target.TargetPosition += correction;
            zoomTargets[index] = target;
        }
    }

    private RectTransform GetBackgroundBounds(RectTransform parent)
    {
        if (backgroundBounds != null)
        {
            return backgroundBounds;
        }

        Transform background = parent.Find("background");
        return background as RectTransform;
    }

    private static float ClampBackgroundCenter(float center, float backgroundHalfSize, float viewportHalfSize)
    {
        if (backgroundHalfSize <= viewportHalfSize)
        {
            return 0f;
        }

        float minCenter = viewportHalfSize - backgroundHalfSize;
        float maxCenter = backgroundHalfSize - viewportHalfSize;
        return Mathf.Clamp(center, minCenter, maxCenter);
    }

    private RectTransform GetZoomParent()
    {
        if (zoomRoot != null)
        {
            return zoomRoot.parent as RectTransform;
        }

        RectTransform ownRect = transform as RectTransform;
        return ownRect != null ? ownRect.parent as RectTransform : null;
    }

    private RectTransform GetFocusFrame()
    {
        if (frames.Count > 0 && frames[0].RectTransform != null)
        {
            return frames[0].RectTransform;
        }

        return transform as RectTransform;
    }

    private IEnumerator FadeDimOverlay(bool fadeIn, int siblingIndex)
    {
        Image overlay = GetOrCreateDimOverlay(siblingIndex);

        if (overlay == null)
        {
            yield break;
        }

        float fromAlpha = fadeIn ? 0f : overlay.color.a;
        float toAlpha = fadeIn ? Mathf.Clamp01(dimColor.a) : 0f;
        float duration = Mathf.Max(0.01f, fadeIn ? dimFadeInDuration : dimFadeOutDuration);
        Color color = dimColor;
        color.a = fromAlpha;
        overlay.color = color;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            color.a = Mathf.Lerp(fromAlpha, toAlpha, smooth);
            overlay.color = color;
            yield return null;
        }

        color.a = toAlpha;
        overlay.color = color;

        if (!fadeIn && dimOverlayRect != null)
        {
            Destroy(dimOverlayRect.gameObject);
            dimOverlayRect = null;
            dimOverlayImage = null;
        }
    }

    private Image GetOrCreateDimOverlay(int siblingIndex)
    {
        if (dimOverlayImage != null)
        {
            if (dimOverlayRect != null && siblingIndex >= 0)
            {
                dimOverlayRect.SetSiblingIndex(siblingIndex);
            }

            return dimOverlayImage;
        }

        RectTransform parent = transform.parent as RectTransform;

        if (parent == null)
        {
            return null;
        }

        GameObject overlayObject = new GameObject("OwlFocusDimOverlay");
        overlayObject.transform.SetParent(parent, false);
        dimOverlayRect = overlayObject.AddComponent<RectTransform>();
        dimOverlayRect.anchorMin = Vector2.zero;
        dimOverlayRect.anchorMax = Vector2.one;
        dimOverlayRect.offsetMin = Vector2.zero;
        dimOverlayRect.offsetMax = Vector2.zero;
        dimOverlayRect.localScale = Vector3.one;

        if (siblingIndex >= 0)
        {
            dimOverlayRect.SetSiblingIndex(siblingIndex);
        }

        dimOverlayImage = overlayObject.AddComponent<Image>();
        dimOverlayImage.raycastTarget = false;
        Color color = dimColor;
        color.a = 0f;
        dimOverlayImage.color = color;
        return dimOverlayImage;
    }

    private int GetOwlOnlyDimSiblingIndex()
    {
        return transform.GetSiblingIndex();
    }

    private int GetBearAndOwlDimSiblingIndex(RectTransform targetBearRoot)
    {
        return Mathf.Min(targetBearRoot.GetSiblingIndex(), transform.GetSiblingIndex());
    }

    private RectTransform GetBearRoot()
    {
        if (bearRoot != null)
        {
            return bearRoot;
        }

        RectTransform parent = transform.parent as RectTransform;

        if (parent == null)
        {
            return null;
        }

        Transform foundBearRoot = parent.Find("BearRoot");
        bearRoot = foundBearRoot as RectTransform;
        return bearRoot;
    }

    private void ApplyZoomTargets(float t, bool reverse)
    {
        RectTransform parent = GetZoomParent();

        for (int index = 0; index < zoomTargets.Count; index++)
        {
            ZoomTarget target = zoomTargets[index];

            Vector2 fromPosition = reverse ? target.TargetPosition : target.StartPosition;
            Vector2 toPosition = reverse ? target.StartPosition : target.TargetPosition;
            Vector3 fromScale = reverse ? target.TargetScale : target.StartScale;
            Vector3 toScale = reverse ? target.StartScale : target.TargetScale;

            target.RectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
            target.RectTransform.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
        }

        ClampAppliedZoomToBackground(parent);
    }

    private void ClampAppliedZoomToBackground(RectTransform parent)
    {
        if (!clampZoomToBackground || parent == null || zoomTargets.Count == 0)
        {
            return;
        }

        RectTransform background = GetBackgroundBounds(parent);

        if (background == null)
        {
            return;
        }

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, background);
        Rect viewport = parent.rect;
        Vector2 padding = new Vector2(
            Mathf.Max(0f, zoomViewportPadding.x),
            Mathf.Max(0f, zoomViewportPadding.y));
        Vector2 correction = Vector2.zero;

        correction.x = GetBoundsCorrection(
            bounds.min.x,
            bounds.max.x,
            viewport.xMin - padding.x,
            viewport.xMax + padding.x,
            viewport.center.x,
            bounds.center.x);

        correction.y = GetBoundsCorrection(
            bounds.min.y,
            bounds.max.y,
            viewport.yMin - padding.y,
            viewport.yMax + padding.y,
            viewport.center.y,
            bounds.center.y);

        if (correction == Vector2.zero)
        {
            return;
        }

        for (int index = 0; index < zoomTargets.Count; index++)
        {
            RectTransform target = zoomTargets[index].RectTransform;
            target.anchoredPosition += correction;
        }
    }

    private static float GetBoundsCorrection(
        float boundsMin,
        float boundsMax,
        float viewportMin,
        float viewportMax,
        float viewportCenter,
        float boundsCenter)
    {
        float boundsSize = boundsMax - boundsMin;
        float viewportSize = viewportMax - viewportMin;

        if (boundsSize <= viewportSize)
        {
            return viewportCenter - boundsCenter;
        }

        if (boundsMin > viewportMin)
        {
            return viewportMin - boundsMin;
        }

        if (boundsMax < viewportMax)
        {
            return viewportMax - boundsMax;
        }

        return 0f;
    }

    private static void ApplyVisualAppearance(OwlFrame visualFrame, OwlFrame sourceFrame)
    {
        if (visualFrame.Image == null || sourceFrame.Image == null)
        {
            return;
        }

        visualFrame.Image.sprite = sourceFrame.Sprite;
        visualFrame.Image.color = sourceFrame.Color;
        visualFrame.Image.preserveAspect = sourceFrame.PreserveAspect;
    }

    private static void ApplyFrameMotion(OwlFrame frame, Vector2 position, Vector3 scale)
    {
        if (frame.RectTransform != null)
        {
            frame.RectTransform.anchoredPosition = position;
            frame.RectTransform.sizeDelta = frame.SizeDelta;
        }
        else
        {
            Vector3 localPosition = frame.Target.localPosition;
            frame.Target.localPosition = new Vector3(position.x, position.y, localPosition.z);
        }

        frame.Target.localScale = scale;
    }

    private void ApplyPreservePlacedFrameSettings()
    {
        if (!preservePlacedFrameTransforms)
        {
            return;
        }

        useSingleVisibleFrame = false;
        addGreetingBounce = false;
        bounceHeight = 0f;
        bounceScale = 1f;
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
}
