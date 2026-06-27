using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BearCutsceneEntrance : MonoBehaviour
{
    [Header("Placed Frame Waypoints")]
    [SerializeField] private bool useChildSpritesAsWaypoints = true;
    [SerializeField] private string waypointNamePrefix = "BearSprite";
    [SerializeField] private bool useSingleVisibleSprite = true;
    [SerializeField] private bool useCinematicPath = true;
    [SerializeField] private bool growFromFirstToLastFrame = true;
    [SerializeField] [Range(0f, 1f)] private float poseSwitchPoint = 0.45f;
    [SerializeField] private bool addJumpArcBetweenWaypoints = true;
    [SerializeField] private bool impactEveryWaypoint = false;

    [Header("Path")]
    [SerializeField] private Vector2 startPosition = new Vector2(-7.2f, -1.55f);
    [SerializeField] private Vector2 endPosition = new Vector2(0f, -1.55f);
    [SerializeField] private Vector3 startScale = new Vector3(0.92f, 0.92f, 1f);
    [SerializeField] private Vector3 endScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private float moveDuration = 2.4f;
    [SerializeField] private float startDelay = 0.35f;

    [Header("Weight")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobFrequency = 2.15f;
    [SerializeField] private float impactDuration = 0.34f;
    [SerializeField] private Vector3 squashScale = new Vector3(1.08f, 0.9f, 1f);
    [SerializeField] private Vector3 stretchScale = new Vector3(0.97f, 1.04f, 1f);

    [Header("Optional Shield")]
    [SerializeField] private GameObject pinkShield;
    [SerializeField] private float shieldDelayAfterLanding = 0.25f;

    [Header("Scene Entry")]
    [SerializeField] private bool playWhiteFadeOutOnStart = true;
    [SerializeField] private float whiteFadeOutDuration = 0.45f;
    [SerializeField] private Color whiteFadeColor = Color.black;

    [Header("After Entrance")]
    [SerializeField] private GameObject revealAfterEntrance;
    [SerializeField] private float revealFadeDuration = 0.55f;

    private RectTransform rectTransform;
    private readonly List<BearFrame> waypointFrames = new List<BearFrame>();
    private Coroutine entranceRoutine;
    private float localZ;
    private CanvasGroup revealCanvasGroup;

    private struct BearFrame
    {
        public Transform Target;
        public RectTransform RectTransform;
        public Image Image;
        public Sprite Sprite;
        public Color Color;
        public bool PreserveAspect;
        public Vector2 Position;
        public Vector3 Scale;
        public Vector2 SizeDelta;
        public float LocalZ;
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        localZ = transform.localPosition.z;
        CacheWaypointFrames();

        if (pinkShield != null)
        {
            pinkShield.SetActive(false);
        }

        PrepareRevealAfterEntrance();
    }

    private void OnEnable()
    {
        if (playWhiteFadeOutOnStart)
        {
            StartCoroutine(WhiteFadeOutRoutine());
        }

        if (MagicStonePuzzleController.IsRetryAfterCrowRequested)
        {
            ShowRevealAfterEntranceImmediately();
            StartCoroutine(PlayRevealGreetingNextFrame());
            return;
        }

        PlayEntrance();
    }

    private void OnDisable()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }
    }

    public void PlayEntrance()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
        }

        entranceRoutine = StartCoroutine(EntranceRoutine());
    }

    public void PlayRoarPlaceholder()
    {
        // Hook a future roar Animator trigger, Timeline signal, or sound cue here.
    }

    private IEnumerator EntranceRoutine()
    {
        if (pinkShield != null)
        {
            pinkShield.SetActive(false);
        }

        if (useChildSpritesAsWaypoints && waypointFrames.Count > 0)
        {
            yield return PlayWaypointEntrance();
        }
        else
        {
            yield return PlayDirectEntrance();
        }

        PlayRoarPlaceholder();

        if (revealAfterEntrance != null)
        {
            yield return RevealAfterEntranceRoutine();
            PlayRevealGreeting();
        }

        if (pinkShield != null)
        {
            if (shieldDelayAfterLanding > 0f)
            {
                yield return new WaitForSeconds(shieldDelayAfterLanding);
            }

            pinkShield.SetActive(true);
        }

        entranceRoutine = null;
    }

    private void PrepareRevealAfterEntrance()
    {
        if (revealAfterEntrance == null)
        {
            return;
        }

        revealAfterEntrance.SetActive(true);
        revealCanvasGroup = revealAfterEntrance.GetComponent<CanvasGroup>();

        if (revealCanvasGroup == null)
        {
            revealCanvasGroup = revealAfterEntrance.AddComponent<CanvasGroup>();
        }

        revealCanvasGroup.alpha = 0f;
        revealCanvasGroup.interactable = false;
        revealCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator RevealAfterEntranceRoutine()
    {
        if (revealAfterEntrance == null)
        {
            yield break;
        }

        if (revealCanvasGroup == null)
        {
            revealCanvasGroup = revealAfterEntrance.GetComponent<CanvasGroup>();
        }

        if (revealCanvasGroup == null)
        {
            yield break;
        }

        revealAfterEntrance.SetActive(true);
        float duration = Mathf.Max(0.01f, revealFadeDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            revealCanvasGroup.alpha = smooth;
            yield return null;
        }

        revealCanvasGroup.alpha = 1f;
    }

    private void ShowRevealAfterEntranceImmediately()
    {
        if (revealAfterEntrance == null)
        {
            return;
        }

        if (revealCanvasGroup == null)
        {
            revealCanvasGroup = revealAfterEntrance.GetComponent<CanvasGroup>();
        }

        if (revealCanvasGroup == null)
        {
            revealCanvasGroup = revealAfterEntrance.AddComponent<CanvasGroup>();
        }

        revealAfterEntrance.SetActive(true);
        revealCanvasGroup.alpha = 1f;
        revealCanvasGroup.interactable = false;
        revealCanvasGroup.blocksRaycasts = false;
    }

    private void PlayRevealGreeting()
    {
        if (revealAfterEntrance == null)
        {
            return;
        }

        OwlGreetingCutscene greeting = revealAfterEntrance.GetComponent<OwlGreetingCutscene>();

        if (greeting == null)
        {
            greeting = revealAfterEntrance.AddComponent<OwlGreetingCutscene>();
        }

        greeting.PlayGreeting();
    }

    private IEnumerator PlayRevealGreetingNextFrame()
    {
        yield return null;
        PlayRevealGreeting();
    }

    private IEnumerator WhiteFadeOutRoutine()
    {
        Image flashImage = CreateWhiteFadeImage();

        if (flashImage == null)
        {
            yield break;
        }

        Color color = whiteFadeColor;
        color.a = 1f;
        flashImage.color = color;

        float duration = Mathf.Max(0.01f, whiteFadeOutDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            color.a = Mathf.Lerp(1f, 0f, smooth);
            flashImage.color = color;
            yield return null;
        }

        Destroy(flashImage.transform.parent.gameObject);
    }

    private Image CreateWhiteFadeImage()
    {
        GameObject flashCanvasObject = new GameObject("CutsceneWhiteFadeCanvas");
        Canvas flashCanvas = flashCanvasObject.AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = flashCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject flashImageObject = new GameObject("WhiteFadeOut");
        flashImageObject.transform.SetParent(flashCanvasObject.transform, false);

        RectTransform flashRect = flashImageObject.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Image flashImage = flashImageObject.AddComponent<Image>();
        flashImage.raycastTarget = false;
        return flashImage;
    }

    private IEnumerator PlayDirectEntrance()
    {
        SetPosition(startPosition);
        transform.localScale = startScale;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        float duration = Mathf.Max(0.01f, moveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float smoothTime = SmoothStep(normalizedTime);

            Vector2 position = Vector2.LerpUnclamped(startPosition, endPosition, smoothTime);
            float bobEnvelope = Mathf.Sin(normalizedTime * Mathf.PI);
            position.y += Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f) * bobHeight * bobEnvelope;

            SetPosition(position);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, smoothTime);

            yield return null;
        }

        SetPosition(endPosition);
        transform.localScale = endScale;

        if (impactDuration > 0f)
        {
            yield return PlayLandingImpact(transform, endScale, impactDuration);
        }
    }

    private IEnumerator PlayWaypointEntrance()
    {
        if (useSingleVisibleSprite)
        {
            yield return PlaySingleVisibleSpriteWaypointEntrance();
            yield break;
        }

        ResetWaypointFrames();

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (waypointFrames.Count == 1)
        {
            SetWaypointFrameVisible(0);
            BearFrame onlyFrame = waypointFrames[0];

            if (impactDuration > 0f)
            {
                yield return PlayLandingImpact(onlyFrame.Target, onlyFrame.Scale, impactDuration);
            }

            yield break;
        }

        float segmentDuration = Mathf.Max(0.01f, moveDuration / (waypointFrames.Count - 1));

        for (int index = 0; index < waypointFrames.Count - 1; index++)
        {
            yield return MoveBetweenWaypointFrames(index, index + 1, segmentDuration);

            SetWaypointFrameVisible(index + 1);

            BearFrame landedFrame = waypointFrames[index + 1];
            ApplyFrameState(landedFrame, landedFrame.Position, landedFrame.Scale, landedFrame.SizeDelta);

            float landingImpactDuration = index == waypointFrames.Count - 2
                ? impactDuration
                : impactDuration * 0.45f;

            if ((impactEveryWaypoint || index == waypointFrames.Count - 2) && landingImpactDuration > 0f)
            {
                yield return PlayLandingImpact(landedFrame.Target, landedFrame.Scale, landingImpactDuration);
            }
        }
    }

    private IEnumerator PlaySingleVisibleSpriteWaypointEntrance()
    {
        BearFrame visualFrame = waypointFrames[0];

        for (int index = 0; index < waypointFrames.Count; index++)
        {
            waypointFrames[index].Target.gameObject.SetActive(index == 0);
        }

        ApplyVisualAppearance(visualFrame, waypointFrames[0]);
        GetMotionStateForFrameIndex(0, out Vector2 firstPosition, out Vector3 firstScale, out Vector2 firstSizeDelta);
        ApplyFrameState(visualFrame, firstPosition, firstScale, firstSizeDelta);

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (waypointFrames.Count == 1)
        {
            if (impactDuration > 0f)
            {
                yield return PlayLandingImpact(visualFrame.Target, waypointFrames[0].Scale, impactDuration);
            }

            yield break;
        }

        int totalSegments = waypointFrames.Count - 1;
        float segmentDuration = Mathf.Max(0.01f, moveDuration / totalSegments);

        for (int index = 0; index < totalSegments; index++)
        {
            yield return MoveSingleVisibleSpriteBetweenFrames(visualFrame, index, index + 1, segmentDuration, totalSegments);

            BearFrame landedFrame = waypointFrames[index + 1];
            ApplyVisualAppearance(visualFrame, landedFrame);
            GetMotionStateForFrameIndex(index + 1, out Vector2 landedPosition, out Vector3 landedScale, out Vector2 landedSizeDelta);
            ApplyFrameState(visualFrame, landedPosition, landedScale, landedSizeDelta);

            bool isFinalFrame = index == totalSegments - 1;
            float landingImpactDuration = isFinalFrame ? impactDuration : impactDuration * 0.35f;

            if ((impactEveryWaypoint || isFinalFrame) && landingImpactDuration > 0f)
            {
                yield return PlayLandingImpact(visualFrame.Target, landedScale, landingImpactDuration);
            }
        }
    }

    private IEnumerator MoveSingleVisibleSpriteBetweenFrames(
        BearFrame visualFrame,
        int fromIndex,
        int toIndex,
        float duration,
        int totalSegments)
    {
        BearFrame fromFrame = waypointFrames[fromIndex];
        BearFrame toFrame = waypointFrames[toIndex];
        bool switchedPose = false;

        ApplyVisualAppearance(visualFrame, fromFrame);
        GetMotionStateForFrameIndex(fromIndex, out Vector2 startPosition, out Vector3 startScale, out Vector2 startSizeDelta);
        ApplyFrameState(visualFrame, startPosition, startScale, startSizeDelta);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float smoothTime = SmoothStep(normalizedTime);

            if (!switchedPose && normalizedTime >= poseSwitchPoint)
            {
                ApplyVisualAppearance(visualFrame, toFrame);
                switchedPose = true;
            }

            float globalTime = Mathf.Lerp(
                (float)fromIndex / totalSegments,
                (float)toIndex / totalSegments,
                normalizedTime);

            GetMotionStateForGlobalTime(
                globalTime,
                smoothTime,
                fromFrame,
                toFrame,
                out Vector2 position,
                out Vector3 scale,
                out Vector2 sizeDelta);

            if (addJumpArcBetweenWaypoints)
            {
                position.y += Mathf.Sin(normalizedTime * Mathf.PI) * bobHeight;
            }

            ApplyFrameState(visualFrame, position, scale, sizeDelta);
            yield return null;
        }

        ApplyVisualAppearance(visualFrame, toFrame);
        GetMotionStateForFrameIndex(toIndex, out Vector2 endPosition, out Vector3 endScale, out Vector2 endSizeDelta);
        ApplyFrameState(visualFrame, endPosition, endScale, endSizeDelta);
    }

    private IEnumerator MoveBetweenWaypointFrames(int fromIndex, int toIndex, float duration)
    {
        BearFrame fromFrame = waypointFrames[fromIndex];
        BearFrame toFrame = waypointFrames[toIndex];

        SetWaypointFrameVisible(fromIndex);
        ApplyFrameState(fromFrame, fromFrame.Position, fromFrame.Scale, fromFrame.SizeDelta);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float smoothTime = SmoothStep(normalizedTime);

            Vector2 position = Vector2.LerpUnclamped(fromFrame.Position, toFrame.Position, smoothTime);

            if (addJumpArcBetweenWaypoints)
            {
                position.y += Mathf.Sin(normalizedTime * Mathf.PI) * bobHeight;
            }

            Vector3 scale = Vector3.LerpUnclamped(fromFrame.Scale, toFrame.Scale, smoothTime);
            Vector2 sizeDelta = Vector2.LerpUnclamped(fromFrame.SizeDelta, toFrame.SizeDelta, smoothTime);

            ApplyFrameState(fromFrame, position, scale, sizeDelta);
            yield return null;
        }

        ApplyFrameState(fromFrame, toFrame.Position, toFrame.Scale, toFrame.SizeDelta);
    }

    private IEnumerator PlayLandingImpact(Transform target, Vector3 baseScale, float duration)
    {
        Vector3 squashTarget = Vector3.Scale(baseScale, squashScale);
        Vector3 stretchTarget = Vector3.Scale(baseScale, stretchScale);

        yield return ScaleOverTime(target, baseScale, squashTarget, duration * 0.38f);
        yield return ScaleOverTime(target, squashTarget, stretchTarget, duration * 0.24f);
        yield return ScaleOverTime(target, stretchTarget, baseScale, duration * 0.38f);
    }

    private IEnumerator ScaleOverTime(Transform target, Vector3 fromScale, Vector3 toScale, float duration)
    {
        if (duration <= 0f)
        {
            target.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float smoothTime = SmoothStep(Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, smoothTime);
            yield return null;
        }

        target.localScale = toScale;
    }

    private void CacheWaypointFrames()
    {
        waypointFrames.Clear();

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);

            if (pinkShield != null && child.gameObject == pinkShield)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(waypointNamePrefix)
                && !child.name.StartsWith(waypointNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Image childImage = child.GetComponent<Image>();
            RectTransform childRectTransform = child as RectTransform;
            waypointFrames.Add(new BearFrame
            {
                Target = child,
                RectTransform = childRectTransform,
                Image = childImage,
                Sprite = childImage != null ? childImage.sprite : null,
                Color = childImage != null ? childImage.color : Color.white,
                PreserveAspect = childImage == null || childImage.preserveAspect,
                Position = GetFramePosition(child, childRectTransform),
                Scale = child.localScale,
                SizeDelta = childRectTransform != null ? childRectTransform.sizeDelta : Vector2.zero,
                LocalZ = child.localPosition.z
            });
        }
    }

    private static void ApplyVisualAppearance(BearFrame visualFrame, BearFrame sourceFrame)
    {
        if (visualFrame.Image == null || sourceFrame.Image == null)
        {
            return;
        }

        visualFrame.Image.sprite = sourceFrame.Sprite;
        visualFrame.Image.color = sourceFrame.Color;
        visualFrame.Image.preserveAspect = sourceFrame.PreserveAspect;
    }

    private void GetMotionStateForFrameIndex(
        int frameIndex,
        out Vector2 position,
        out Vector3 scale,
        out Vector2 sizeDelta)
    {
        int totalSegments = Mathf.Max(1, waypointFrames.Count - 1);
        float globalTime = Mathf.Clamp01((float)frameIndex / totalSegments);
        BearFrame frame = waypointFrames[Mathf.Clamp(frameIndex, 0, waypointFrames.Count - 1)];

        GetMotionStateForGlobalTime(globalTime, SmoothStep(globalTime), frame, frame, out position, out scale, out sizeDelta);
    }

    private void GetMotionStateForGlobalTime(
        float globalTime,
        float segmentSmoothTime,
        BearFrame fromFrame,
        BearFrame toFrame,
        out Vector2 position,
        out Vector3 scale,
        out Vector2 sizeDelta)
    {
        BearFrame pathStartFrame = waypointFrames[0];
        BearFrame pathEndFrame = waypointFrames[waypointFrames.Count - 1];
        float pathTime = SmoothStep(Mathf.Clamp01(globalTime));

        position = useCinematicPath
            ? Vector2.LerpUnclamped(pathStartFrame.Position, pathEndFrame.Position, pathTime)
            : Vector2.LerpUnclamped(fromFrame.Position, toFrame.Position, segmentSmoothTime);

        scale = growFromFirstToLastFrame
            ? Vector3.LerpUnclamped(pathStartFrame.Scale, pathEndFrame.Scale, pathTime)
            : Vector3.LerpUnclamped(fromFrame.Scale, toFrame.Scale, segmentSmoothTime);

        sizeDelta = growFromFirstToLastFrame
            ? Vector2.LerpUnclamped(pathStartFrame.SizeDelta, pathEndFrame.SizeDelta, pathTime)
            : Vector2.LerpUnclamped(fromFrame.SizeDelta, toFrame.SizeDelta, segmentSmoothTime);
    }

    private void ResetWaypointFrames()
    {
        for (int index = 0; index < waypointFrames.Count; index++)
        {
            BearFrame frame = waypointFrames[index];
            frame.Target.gameObject.SetActive(index == 0);
            ApplyFrameState(frame, frame.Position, frame.Scale, frame.SizeDelta);
        }
    }

    private void SetWaypointFrameVisible(int visibleIndex)
    {
        for (int index = 0; index < waypointFrames.Count; index++)
        {
            waypointFrames[index].Target.gameObject.SetActive(index == visibleIndex);
        }
    }

    private void ApplyFrameState(BearFrame frame, Vector2 position, Vector3 scale, Vector2 sizeDelta)
    {
        if (frame.RectTransform != null)
        {
            frame.RectTransform.anchoredPosition = position;
            frame.RectTransform.sizeDelta = sizeDelta;
        }
        else
        {
            frame.Target.localPosition = new Vector3(position.x, position.y, frame.LocalZ);
        }

        frame.Target.localScale = scale;
    }

    private static Vector2 GetFramePosition(Transform target, RectTransform targetRectTransform)
    {
        if (targetRectTransform != null)
        {
            return targetRectTransform.anchoredPosition;
        }

        Vector3 localPosition = target.localPosition;
        return new Vector2(localPosition.x, localPosition.y);
    }

    private void SetPosition(Vector2 position)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = position;
            return;
        }

        transform.localPosition = new Vector3(position.x, position.y, localZ);
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
