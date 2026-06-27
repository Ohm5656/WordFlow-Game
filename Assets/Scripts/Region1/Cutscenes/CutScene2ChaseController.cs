using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CutScene2ChaseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform bearRoot;
    [SerializeField] private RectTransform calmBearFrame;
    [SerializeField] private RectTransform scaredBearFrame;
    [SerializeField] private RectTransform stoneRoot;

    [Header("Scene Entry")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool playWhiteFadeOutOnStart = true;
    [SerializeField] private float whiteFadeOutDuration = 1.05f;
    [SerializeField] private Color whiteFadeColor = Color.white;
    [SerializeField] private float startDelayAfterFade = 0.35f;

    [Header("Stone Throw")]
    [SerializeField] private bool hideStoneBeforeLaunch = true;
    [SerializeField] private float stoneRevealDuration = 0.2f;
    [SerializeField] private Vector2 stoneImpactPosition = new Vector2(-90f, -170f);
    [SerializeField] private float stoneLaunchDuration = 0.82f;
    [SerializeField] private float scareAtLaunchProgress = 0.62f;
    [SerializeField] private float stoneImpactScaleMultiplier = 1.06f;
    [SerializeField] private float stoneTiltDegrees = 8f;

    [Header("Run Away")]
    [SerializeField] private Vector2 bearExitPosition = new Vector2(-2100f, -115f);
    [SerializeField] private Vector2 stoneExitPosition = new Vector2(-2050f, -287f);
    [SerializeField] private float bearRunDelay = 0.12f;
    [SerializeField] private float bearRunDuration = 1.35f;
    [SerializeField] private float stoneChaseDuration = 1.25f;
    [SerializeField] private float runBobHeight = 42f;
    [SerializeField] private float runBobFrequency = 7f;
    [SerializeField] private float scaredShakeDuration = 0.28f;
    [SerializeField] private float scaredShakeStrength = 26f;

    [Header("Quest Complete")]
    [SerializeField] private bool loadSceneOnComplete = true;
    [SerializeField] private string nextSceneName = "success";
    [SerializeField] private bool playBlackFadeInBeforeLoad = true;
    [SerializeField] private float completeHoldDuration = 0.3f;
    [SerializeField] private float blackFadeInDuration = 0.85f;
    [SerializeField] private Color blackFadeColor = Color.black;

    private RectTransform ownRect;
    private CanvasGroup stoneCanvasGroup;
    private Coroutine flowRoutine;
    private Vector2 bearStartPosition;
    private Vector3 bearStartScale;
    private Vector2 stoneStartPosition;
    private Vector3 stoneStartScale;
    private Quaternion stoneStartRotation;

    private void Awake()
    {
        ownRect = transform as RectTransform;
        ResolveReferences();
        CapturePlacedState();
        PrepareInitialState();
    }

    private void OnEnable()
    {
        if (playOnStart)
        {
            PlayFlow();
        }
    }

    private void OnDisable()
    {
        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }
    }

    public void PlayFlow()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
        }

        ResolveReferences();
        CapturePlacedState();
        PrepareInitialState();
        flowRoutine = StartCoroutine(FlowRoutine());
    }

    private IEnumerator FlowRoutine()
    {
        if (playWhiteFadeOutOnStart)
        {
            yield return WhiteFadeOutRoutine();
        }

        if (startDelayAfterFade > 0f)
        {
            yield return new WaitForSeconds(startDelayAfterFade);
        }

        yield return RevealStoneRoutine();
        yield return StoneLaunchRoutine();

        if (bearRunDelay > 0f)
        {
            yield return new WaitForSeconds(bearRunDelay);
        }

        yield return RunAwayRoutine();

        if (completeHoldDuration > 0f)
        {
            yield return new WaitForSeconds(completeHoldDuration);
        }

        if (loadSceneOnComplete && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            if (playBlackFadeInBeforeLoad)
            {
                yield return BlackFadeInRoutine();
            }

            SceneManager.LoadScene(nextSceneName);
        }

        flowRoutine = null;
    }

    private void ResolveReferences()
    {
        if (bearRoot == null)
        {
            bearRoot = FindDescendantRect(transform, "BearRoot");
        }

        if (stoneRoot == null)
        {
            stoneRoot = FindDescendantRect(transform, "stone");
        }

        if (bearRoot != null)
        {
            if (calmBearFrame == null)
            {
                calmBearFrame = FindDescendantRect(bearRoot, "bear (1)");
            }

            if (scaredBearFrame == null)
            {
                scaredBearFrame = FindDescendantRect(bearRoot, "bear");
            }
        }
    }

    private void CapturePlacedState()
    {
        if (bearRoot != null)
        {
            bearStartPosition = bearRoot.anchoredPosition;
            bearStartScale = bearRoot.localScale;
        }

        if (stoneRoot != null)
        {
            stoneStartPosition = stoneRoot.anchoredPosition;
            stoneStartScale = stoneRoot.localScale;
            stoneStartRotation = stoneRoot.localRotation;
        }
    }

    private void PrepareInitialState()
    {
        SetBearScared(false);

        if (bearRoot != null)
        {
            bearRoot.anchoredPosition = bearStartPosition;
            bearRoot.localScale = bearStartScale;
        }

        if (stoneRoot != null)
        {
            stoneRoot.gameObject.SetActive(true);
            stoneRoot.anchoredPosition = stoneStartPosition;
            stoneRoot.localScale = stoneStartScale;
            stoneRoot.localRotation = stoneStartRotation;
            stoneCanvasGroup = EnsureCanvasGroup(stoneRoot);
            stoneCanvasGroup.alpha = hideStoneBeforeLaunch ? 0f : 1f;
            stoneCanvasGroup.interactable = false;
            stoneCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator RevealStoneRoutine()
    {
        if (stoneRoot == null || stoneCanvasGroup == null)
        {
            yield break;
        }

        if (!hideStoneBeforeLaunch)
        {
            stoneCanvasGroup.alpha = 1f;
            yield break;
        }

        Vector3 targetScale = stoneStartScale;
        Vector3 startScale = targetScale * 0.9f;
        float duration = Mathf.Max(0.01f, stoneRevealDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            stoneCanvasGroup.alpha = smooth;
            stoneRoot.localScale = Vector3.LerpUnclamped(startScale, targetScale, EaseOutBack(t));
            yield return null;
        }

        stoneCanvasGroup.alpha = 1f;
        stoneRoot.localScale = targetScale;
    }

    private IEnumerator StoneLaunchRoutine()
    {
        if (stoneRoot == null)
        {
            SetBearScared(true);
            yield break;
        }

        Vector2 fromPosition = stoneRoot.anchoredPosition;
        Vector3 targetScale = stoneStartScale * Mathf.Max(0.01f, stoneImpactScaleMultiplier);
        float duration = Mathf.Max(0.01f, stoneLaunchDuration);
        bool scared = false;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);

            if (!scared && t >= Mathf.Clamp01(scareAtLaunchProgress))
            {
                scared = true;
                SetBearScared(true);
                StartCoroutine(ScaredShakeRoutine());
            }

            stoneRoot.anchoredPosition = Vector2.LerpUnclamped(fromPosition, stoneImpactPosition, smooth);
            stoneRoot.localScale = Vector3.LerpUnclamped(stoneStartScale, targetScale, EaseOutBack(t));
            stoneRoot.localRotation = stoneStartRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * stoneTiltDegrees);
            yield return null;
        }

        SetBearScared(true);
        stoneRoot.anchoredPosition = stoneImpactPosition;
        stoneRoot.localScale = targetScale;
        stoneRoot.localRotation = stoneStartRotation;
    }

    private IEnumerator ScaredShakeRoutine()
    {
        if (bearRoot == null || scaredShakeDuration <= 0f)
        {
            yield break;
        }

        Vector2 startPosition = bearRoot.anchoredPosition;
        float duration = Mathf.Max(0.01f, scaredShakeDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float strength = Mathf.Lerp(scaredShakeStrength, 0f, SmoothStep(t));
            bearRoot.anchoredPosition = startPosition + Random.insideUnitCircle * strength;
            yield return null;
        }

        bearRoot.anchoredPosition = startPosition;
    }

    private IEnumerator RunAwayRoutine()
    {
        float duration = Mathf.Max(0.01f, Mathf.Max(bearRunDuration, stoneChaseDuration));
        Vector2 bearFrom = bearRoot != null ? bearRoot.anchoredPosition : Vector2.zero;
        Vector2 stoneFrom = stoneRoot != null ? stoneRoot.anchoredPosition : Vector2.zero;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float bearT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bearRunDuration));
            float stoneT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, stoneChaseDuration));

            if (bearRoot != null)
            {
                float bearSmooth = SmoothStep(bearT);
                Vector2 position = Vector2.LerpUnclamped(bearFrom, bearExitPosition, bearSmooth);
                position.y += Mathf.Sin(elapsed * runBobFrequency * Mathf.PI * 2f) * runBobHeight * Mathf.Sin(bearT * Mathf.PI);
                bearRoot.anchoredPosition = position;
            }

            if (stoneRoot != null)
            {
                float stoneSmooth = SmoothStep(stoneT);
                stoneRoot.anchoredPosition = Vector2.LerpUnclamped(stoneFrom, stoneExitPosition, stoneSmooth);
                stoneRoot.localRotation = stoneStartRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 18f) * stoneTiltDegrees);
            }

            yield return null;
        }

        if (bearRoot != null)
        {
            bearRoot.anchoredPosition = bearExitPosition;
        }

        if (stoneRoot != null)
        {
            stoneRoot.anchoredPosition = stoneExitPosition;
            stoneRoot.localRotation = stoneStartRotation;
        }
    }

    private void SetBearScared(bool scared)
    {
        if (calmBearFrame != null)
        {
            calmBearFrame.gameObject.SetActive(!scared);
        }

        if (scaredBearFrame != null)
        {
            scaredBearFrame.gameObject.SetActive(scared);
        }
    }

    private IEnumerator WhiteFadeOutRoutine()
    {
        Image fadeImage = CreateFadeImage("CutScene2WhiteFadeCanvas", "CutScene2WhiteFade");
        if (fadeImage == null)
        {
            yield break;
        }

        Color color = whiteFadeColor;
        color.a = 1f;
        fadeImage.color = color;

        float duration = Mathf.Max(0.01f, whiteFadeOutDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            color.a = Mathf.Lerp(1f, 0f, smooth);
            fadeImage.color = color;
            yield return null;
        }

        Destroy(fadeImage.transform.root.gameObject);
    }

    private IEnumerator BlackFadeInRoutine()
    {
        Image fadeImage = CreateFadeImage("CutScene2BlackFadeCanvas", "CutScene2BlackFade");
        if (fadeImage == null)
        {
            yield break;
        }

        Color color = blackFadeColor;
        color.a = 0f;
        fadeImage.color = color;

        float duration = Mathf.Max(0.01f, blackFadeInDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            color.a = Mathf.Lerp(0f, 1f, smooth);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    private Image CreateFadeImage(string canvasName, string imageName)
    {
        GameObject fadeCanvasObject = new GameObject(canvasName);
        Canvas fadeCanvas = fadeCanvasObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = fadeCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(3840f, 2160f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject(imageName);
        imageObject.transform.SetParent(fadeCanvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static RectTransform FindDescendantRect(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child as RectTransform;
            }

            RectTransform found = FindDescendantRect(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
