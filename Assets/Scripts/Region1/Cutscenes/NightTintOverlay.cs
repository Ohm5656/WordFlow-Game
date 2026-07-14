using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Self-bootstrapping night dressing for the puzzle/success scenes during a night redo.
///
/// The dark wash is inserted INTO the scene's own Canvas, directly above the `background` image and
/// below everything else. That is what makes the night read correctly: the background goes dark
/// while the actors on top of it — the bear, the crow, the craft book, the stones, the smoke, the
/// star board — keep their full daylight brightness, so the quest object is the lit thing in a dark
/// scene. (A screen-space overlay ON TOP of the canvas would have dimmed the book and stones too.)
///
/// Only appears while NightMode.RedoActive is true; does nothing during the day.
public sealed class NightTintOverlay : MonoBehaviour
{
    private const string OverlayObjectName = "Night Background Tint";
    private const string BackgroundObjectName = "background";

    private static readonly string[] NightScenes =
    {
        "CutScene_bear", "CutScene_ga",
        "Success_pa", "Success_ga", "Success_ga_correct", "Success_ta_incorrect",
    };

    // Moonlit blue, matching reference_forest's night. Alpha is the knob: higher = darker night.
    private static readonly Color TintColor = new Color(0.04f, 0.09f, 0.20f, 0.72f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

    private static void TryCreateForActiveScene()
    {
        if (!NightMode.RedoActive) return;
        if (!IsNightScene(SceneManager.GetActiveScene().name)) return;
        if (GameObject.Find(OverlayObjectName) != null) return;

        Canvas canvas = FindSceneCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[NightTintOverlay] no root Canvas in this scene — night tint skipped");
            return;
        }

        // Sit directly above the background image; everything authored after it (actors, book,
        // stones, fog, star board) draws on top of the tint and therefore stays bright.
        Transform background = canvas.transform.Find(BackgroundObjectName);
        int siblingIndex = background != null ? background.GetSiblingIndex() + 1 : 1;

        GameObject tint = new GameObject(OverlayObjectName);
        tint.transform.SetParent(canvas.transform, false);
        tint.transform.SetSiblingIndex(siblingIndex);

        RectTransform rect = tint.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = tint.AddComponent<Image>();
        image.color = TintColor;
        image.raycastTarget = false; // never eat a tap meant for a stone
    }

    private static Canvas FindSceneCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null || c.transform.parent != null) continue;        // root canvases only
            if (best == null || c.sortingOrder < best.sortingOrder) best = c; // the scene's own (order 0)
        }

        return best;
    }

    private static bool IsNightScene(string sceneName)
    {
        for (int i = 0; i < NightScenes.Length; i++)
        {
            if (NightScenes[i] == sceneName) return true;
        }

        return false;
    }
}
