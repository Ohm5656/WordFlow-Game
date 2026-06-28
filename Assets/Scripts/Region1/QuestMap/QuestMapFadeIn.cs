using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class QuestMapFadeIn : MonoBehaviour
{
    private const string QuestMap1SceneName = "quest_map1";
    private const string FadeSetupName = "QuestMapFadeInSetup";
    private const string FadeCanvasName = "QuestMap Fade Canvas";

    [SerializeField]
    private float fadeInDuration = 2.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SetupCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupCurrentScene();
    }

    private static void SetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != QuestMap1SceneName)
        {
            return;
        }

        if (GameObject.Find(FadeSetupName) != null)
        {
            return;
        }

        GameObject setupObj = new GameObject(FadeSetupName);
        QuestMapFadeIn fadeIn = setupObj.AddComponent<QuestMapFadeIn>();
        fadeIn.StartFadeIn();
    }

    private void StartFadeIn()
    {
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        CanvasGroup fadeCanvas = GetOrCreateFadeCanvas();
        if (fadeCanvas == null)
        {
            yield break;
        }

        fadeCanvas.alpha = 1f;
        fadeCanvas.blocksRaycasts = true;

        float duration = Mathf.Max(0.01f, fadeInDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvas.alpha = 1f - SmootherStep(t);
            yield return null;
        }

        fadeCanvas.alpha = 0f;
        fadeCanvas.blocksRaycasts = false;
        Destroy(fadeCanvas.gameObject);
        Destroy(gameObject);
    }

    private static CanvasGroup GetOrCreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject(FadeCanvasName);
        RectTransform rectTransform = canvasObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 10000;

        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        GameObject panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(fadeCanvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image fadeImage = panelObj.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        return canvasGroup;
    }

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
