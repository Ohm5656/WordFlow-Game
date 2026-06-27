using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class WorldMapFadeIn : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string FadeSetupName = "WorldMap Fade In";
    private const string FadeCanvasName = "WorldMap Fade Canvas";
    private const float DefaultFadeInDuration = 3.2f;
    private const float DefaultFadeStartDelay = 0.12f;

    private static bool fadeExpected;
    private static bool fadeRunning;
    private static float expectedFadeFinishedRealtime;

    public static bool IsFadeBlocking
    {
        get
        {
            return fadeExpected
                || fadeRunning
                || Time.realtimeSinceStartup < expectedFadeFinishedRealtime
                || GameObject.Find(FadeSetupName) != null
                || GameObject.Find(FadeCanvasName) != null;
        }
    }

    [SerializeField]
    private float fadeInDuration = DefaultFadeInDuration;

    [SerializeField]
    private float fadeStartDelay = DefaultFadeStartDelay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryStartForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryStartForActiveScene();
    }

    private static void TryStartForActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != WorldMapSceneName)
        {
            fadeExpected = false;
            fadeRunning = false;
            expectedFadeFinishedRealtime = 0f;
            return;
        }

        if (GameObject.Find(FadeSetupName) != null)
        {
            return;
        }

        fadeExpected = true;
        expectedFadeFinishedRealtime = Time.realtimeSinceStartup + DefaultFadeStartDelay + DefaultFadeInDuration;

        GameObject setupObject = new GameObject(FadeSetupName);
        setupObject.AddComponent<WorldMapFadeIn>();
    }

    private void Start()
    {
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        fadeExpected = true;
        fadeRunning = true;
        expectedFadeFinishedRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, fadeStartDelay) + Mathf.Max(0.01f, fadeInDuration);

        CanvasGroup fadeCanvas = CreateFadeCanvas();
        fadeCanvas.alpha = 1f;
        fadeCanvas.blocksRaycasts = true;

        yield return null;

        if (fadeStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(fadeStartDelay);
        }

        float duration = Mathf.Max(0.01f, fadeInDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvas.alpha = 1f - EaseInOutSine(t);
            yield return null;
        }

        fadeCanvas.alpha = 0f;
        fadeCanvas.blocksRaycasts = false;
        fadeRunning = false;
        fadeExpected = false;
        expectedFadeFinishedRealtime = 0f;
        Destroy(fadeCanvas.gameObject);
        Destroy(gameObject);
    }

    private static CanvasGroup CreateFadeCanvas()
    {
        GameObject canvasObject = new GameObject(FadeCanvasName);
        RectTransform rectTransform = canvasObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        GameObject panelObject = new GameObject("Fade Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image fadeImage = panelObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        return canvasGroup;
    }

    private static float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return 0.5f - Mathf.Cos(t * Mathf.PI) * 0.5f;
    }
}
