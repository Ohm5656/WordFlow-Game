using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reusable full-screen black fade overlay.
///
/// Two uses:
///  - <see cref="Cover"/> (static coroutine): eases a fresh black overlay from clear to opaque and
///    leaves it up, so a caller can fade the screen out before loading the next scene.
///  - A self-bootstrapping <b>reveal</b> for the <c>reference_forest</c> scene: on entering that
///    scene a black overlay is created at full opacity and eased back to clear, mirroring the
///    existing WorldMapFadeIn fade-in.
///
/// <see cref="RevealComplete"/> lets gameplay (e.g. QuestPathSequence) wait until the reveal has
/// fully finished. It defaults to true so gameplay is never permanently blocked when no fade runs,
/// and is set false the instant a reference_forest reveal begins.
/// </summary>
public sealed class SceneFadeController : MonoBehaviour
{
    private const string ReferenceForestSceneName = "reference_forest";

    // Scenes that self-reveal (ease a black overlay from opaque to clear) on load, so
    // entering them after a Cover() fade-out is a smooth fade-in rather than a hard pop.
    private static readonly string[] SelfRevealScenes =
    {
        ReferenceForestSceneName,
        "word_build_paa_polished",
        "practice_night",
    };

    private const string CoverObjectName = "Scene Fade Cover";
    private const string RevealObjectName = "Scene Fade Reveal";
    private const string CanvasName = "Scene Fade Canvas";

    private const float DefaultRevealDuration = 0.65f;

    /// <summary>True once any in-progress reference_forest reveal has finished (or none is running).</summary>
    public static bool RevealComplete { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryStartRevealForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryStartRevealForActiveScene();
    }

    private static void TryStartRevealForActiveScene()
    {
        if (!ShouldSelfReveal(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (GameObject.Find(RevealObjectName) != null)
        {
            return;
        }

        // Arm the gate immediately so gameplay (QuestPathSequence.Start, which runs after this
        // sceneLoaded callback) waits for the reveal to finish.
        RevealComplete = false;

        GameObject host = new GameObject(RevealObjectName);
        SceneFadeController controller = host.AddComponent<SceneFadeController>();
        controller.StartCoroutine(controller.RevealRoutine(DefaultRevealDuration));
    }

    /// <summary>
    /// Creates a black overlay and eases it from clear to opaque over <paramref name="duration"/>
    /// seconds, leaving it in place. Call before loading the next scene to fade the screen out.
    /// </summary>
    public static IEnumerator Cover(float duration)
    {
        GameObject host = new GameObject(CoverObjectName);
        SceneFadeController controller = host.AddComponent<SceneFadeController>();
        yield return controller.CoverRoutine(duration);
    }

    private IEnumerator CoverRoutine(float duration)
    {
        CanvasGroup overlay = CreateOverlay();
        overlay.alpha = 0f;
        overlay.blocksRaycasts = true;

        float total = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < total; elapsed += Time.unscaledDeltaTime)
        {
            overlay.alpha = EaseInOutSine(Mathf.Clamp01(elapsed / total));
            yield return null;
        }

        overlay.alpha = 1f;
        // Intentionally left up: the imminent scene load destroys it, and the next scene's reveal
        // starts black, so there is no flash.
    }

    private IEnumerator RevealRoutine(float duration)
    {
        CanvasGroup overlay = CreateOverlay();
        overlay.alpha = 1f;
        overlay.blocksRaycasts = true;

        yield return null;

        float total = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < total; elapsed += Time.unscaledDeltaTime)
        {
            overlay.alpha = 1f - EaseInOutSine(Mathf.Clamp01(elapsed / total));
            yield return null;
        }

        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
        RevealComplete = true;
        Destroy(overlay.gameObject);
        Destroy(gameObject);
    }

    private static CanvasGroup CreateOverlay()
    {
        GameObject canvasObject = new GameObject(CanvasName);
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

    private static bool ShouldSelfReveal(string sceneName)
    {
        for (int i = 0; i < SelfRevealScenes.Length; i++)
        {
            if (SelfRevealScenes[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private static float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return 0.5f - Mathf.Cos(t * Mathf.PI) * 0.5f;
    }
}
