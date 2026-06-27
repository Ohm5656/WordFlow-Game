using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CrowCutsceneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform crowRoot;
    [SerializeField] private AudioClip crowCallClip;

    [Header("Scene Entry")]
    [SerializeField] private bool playBlackFadeOutOnStart = true;
    [SerializeField] private float blackFadeOutDuration = 0.55f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float startDelayAfterFade = 0.1f;

    [Header("Crow Motion")]
    [SerializeField] private Vector2 flyEndOffset = new Vector2(760f, 120f);
    [SerializeField] private float flyDuration = 2.25f;
    [SerializeField] private float bobHeight = 34f;
    [SerializeField] private float bobFrequency = 4.5f;
    [SerializeField] private float tiltDegrees = 6f;
    [SerializeField] private float exitHoldDuration = 0.25f;

    [Header("Return")]
    [SerializeField] private string returnSceneName = "cut_scene1";
    [SerializeField] private bool playBlackFadeInBeforeReturn = true;
    [SerializeField] private float blackFadeInDuration = 0.5f;

    private Coroutine routine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        routine = StartCoroutine(FlowRoutine());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator FlowRoutine()
    {
        ResolveReferences();

        if (playBlackFadeOutOnStart)
        {
            yield return FadeRoutine(1f, 0f, blackFadeOutDuration, true);
        }

        if (startDelayAfterFade > 0f)
        {
            yield return new WaitForSeconds(startDelayAfterFade);
        }

        PlayCrowCall();
        yield return FlyCrowRoutine();

        if (exitHoldDuration > 0f)
        {
            yield return new WaitForSeconds(exitHoldDuration);
        }

        MagicStonePuzzleController.RequestRetryAfterCrow();

        if (playBlackFadeInBeforeReturn)
        {
            yield return FadeRoutine(0f, 1f, blackFadeInDuration, false);
        }

        SceneManager.LoadScene(string.IsNullOrWhiteSpace(returnSceneName) ? "cut_scene1" : returnSceneName.Trim());
    }

    private void ResolveReferences()
    {
        if (crowRoot == null)
        {
            crowRoot = FindDescendantRect(transform, "crow");
        }
    }

    private void PlayCrowCall()
    {
        if (crowCallClip == null)
        {
            return;
        }

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = crowCallClip;
        audioSource.volume = 1f;
        audioSource.Play();
    }

    private IEnumerator FlyCrowRoutine()
    {
        if (crowRoot == null)
        {
            yield break;
        }

        Vector2 startPosition = crowRoot.anchoredPosition;
        Vector2 targetPosition = startPosition + flyEndOffset;
        Quaternion startRotation = crowRoot.localRotation;
        float duration = Mathf.Max(0.01f, flyDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            Vector2 position = Vector2.LerpUnclamped(startPosition, targetPosition, smooth);
            position.y += Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f) * bobHeight * Mathf.Sin(t * Mathf.PI);
            crowRoot.anchoredPosition = position;
            crowRoot.localRotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * tiltDegrees);
            yield return null;
        }

        crowRoot.anchoredPosition = targetPosition;
        crowRoot.localRotation = startRotation;
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, bool destroyAfterFade)
    {
        Image fadeImage = CreateFadeImage();
        if (fadeImage == null)
        {
            yield break;
        }

        Color color = fadeColor;
        color.a = Mathf.Clamp01(fromAlpha);
        fadeImage.color = color;

        float safeDuration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            color.a = Mathf.Lerp(fromAlpha, toAlpha, SmoothStep(t));
            fadeImage.color = color;
            yield return null;
        }

        color.a = Mathf.Clamp01(toAlpha);
        fadeImage.color = color;

        if (destroyAfterFade)
        {
            Destroy(fadeImage.transform.root.gameObject);
        }
    }

    private static Image CreateFadeImage()
    {
        GameObject fadeCanvasObject = new GameObject("CrowCutsceneFadeCanvas");
        Canvas fadeCanvas = fadeCanvasObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = fadeCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(3840f, 2160f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject fadeObject = new GameObject("CrowCutsceneFade");
        fadeObject.transform.SetParent(fadeCanvasObject.transform, false);

        RectTransform fadeRect = fadeObject.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        Image fadeImage = fadeObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        return fadeImage;
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
}
