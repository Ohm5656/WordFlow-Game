using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class OwlGuideAnimator : MonoBehaviour
{
    [SerializeField] private Image owlImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Vector2 targetAnchoredPosition = new Vector2(250f, -190f);
    [SerializeField] private Vector2 sizeDelta = new Vector2(270f, 270f);
    [SerializeField] private float enterDuration = 0.42f;
    [SerializeField] private float speechDelay = 0.18f;
    [SerializeField] private float frameDuration = 0.35f;
    [SerializeField] private float openingFrameDuration = 0.35f;
    [SerializeField] private float loopFrameDuration = 0.35f;
    [SerializeField] private bool playFirstFrameOnceThenLoopRest = true;
    [SerializeField] private bool holdLastFrame = true;

    private RectTransform rectTransform;

    private void Awake()
    {
        EnsureReferences();
        SetFrame(0);
    }

    public void Configure(
        Sprite[] frameOverride,
        Vector2 targetPosition,
        Vector2 targetSize,
        float enterSeconds,
        float delayBeforeSpeech,
        float secondsPerFrame)
    {
        EnsureReferences();

        if (frameOverride != null && frameOverride.Length > 0)
        {
            frames = frameOverride;
        }

        targetAnchoredPosition = targetPosition;
        sizeDelta = new Vector2(Mathf.Max(1f, targetSize.x), Mathf.Max(1f, targetSize.y));
        enterDuration = Mathf.Max(0.01f, enterSeconds);
        speechDelay = Mathf.Max(0f, delayBeforeSpeech);
        frameDuration = Mathf.Max(0.03f, secondsPerFrame);
        openingFrameDuration = Mathf.Max(0.03f, secondsPerFrame);
        loopFrameDuration = Mathf.Max(0.03f, secondsPerFrame);

        if (rectTransform != null)
        {
            rectTransform.sizeDelta = sizeDelta;
        }
    }

    public IEnumerator PlayEnterThenTalk(Action onTalkStarted, float talkingSeconds)
    {
        EnsureReferences();

        if (rectTransform == null || canvasGroup == null)
        {
            yield break;
        }

        gameObject.SetActive(true);

        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = targetAnchoredPosition;
        rectTransform.localScale = Vector3.one;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        SetFrame(0);

        float safeEnterDuration = Mathf.Max(0.01f, enterDuration);
        for (float elapsed = 0f; elapsed < safeEnterDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeEnterDuration);
            float eased = SmootherStep(t);
            canvasGroup.alpha = eased;
            rectTransform.anchoredPosition = targetAnchoredPosition;
            rectTransform.localScale = Vector3.one;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = targetAnchoredPosition;
        rectTransform.localScale = Vector3.one;

        if (speechDelay > 0f)
        {
            yield return new WaitForSeconds(speechDelay);
        }

        onTalkStarted?.Invoke();

        yield return PlayCutsceneStyleFrames(Mathf.Max(0f, talkingSeconds));
        SetFrame(holdLastFrame ? GetLastFrameIndex() : 0);
    }

    private void EnsureReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (owlImage == null)
        {
            owlImage = GetComponentInChildren<Image>(true);
        }
    }

    private IEnumerator PlayCutsceneStyleFrames(float totalDuration)
    {
        if (frames == null || frames.Length == 0)
        {
            yield break;
        }

        int frameCount = frames.Length;
        bool useOpeningFrameOnce = playFirstFrameOnceThenLoopRest && frameCount > 1;
        float playedTime = 0f;
        int frameIndex = 0;

        if (useOpeningFrameOnce)
        {
            float currentDuration = totalDuration > 0f
                ? Mathf.Min(GetOpeningFrameDuration(), totalDuration)
                : GetOpeningFrameDuration();

            SetFrame(0);
            yield return new WaitForSeconds(currentDuration);
            playedTime += currentDuration;
        }

        while (playedTime < totalDuration)
        {
            int visibleIndex = useOpeningFrameOnce
                ? GetLoopRestFrameIndex(frameIndex)
                : GetFrameIndex(frameIndex);
            float remainingTime = totalDuration - playedTime;
            float currentDuration = Mathf.Min(GetLoopFrameDuration(), remainingTime);

            SetFrame(visibleIndex);
            yield return new WaitForSeconds(currentDuration);

            playedTime += currentDuration;
            frameIndex++;
        }
    }

    private void SetFrame(float elapsed)
    {
        float safeFrameDuration = Mathf.Max(0.03f, frameDuration);
        SetFrame(Mathf.FloorToInt(elapsed / safeFrameDuration));
    }

    private void SetFrame(int frameIndex)
    {
        if (owlImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        int count = frames.Length;
        int safeIndex = Mathf.Abs(frameIndex) % count;
        for (int i = 0; i < count; i++)
        {
            Sprite frame = frames[(safeIndex + i) % count];
            if (frame != null)
            {
                owlImage.sprite = frame;
                return;
            }
        }
    }

    private int GetFrameIndex(int frameIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return 0;
        }

        return Mathf.Abs(frameIndex) % frames.Length;
    }

    private int GetLoopRestFrameIndex(int frameIndex)
    {
        if (frames == null || frames.Length <= 1)
        {
            return GetFrameIndex(0);
        }

        return 1 + Mathf.Abs(frameIndex) % (frames.Length - 1);
    }

    private int GetLastFrameIndex()
    {
        return frames != null && frames.Length > 0 ? frames.Length - 1 : 0;
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

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
