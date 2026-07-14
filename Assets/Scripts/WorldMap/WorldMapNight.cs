using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Self-bootstrapping WorldMap night presentation. The existing night overlay remains unchanged:
/// the backdrop darkens while the islands stay lit. If at least one quest is below three stars, the
/// two choice sprites already placed under the scene's "ui" root are revealed above one temporary
/// extra dim. Only the incomplete-quest choice is active for this pass.
/// </summary>
public sealed class WorldMapNight : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string SetupObjectName = "WorldMap Night";
    private const string ChoiceRootName = "ui";

    // Existing map orders: BG = 0, islands/locks = 3..9, placed choice sprites = 20.
    private const int DarkOverlayOrder = 1;
    private const int ChoiceDimOrder = 19;

    [Header("Darkness")]
    [SerializeField] private Color darkColor = new Color(0.04f, 0.09f, 0.20f, 1f);
    [SerializeField] private float maxDarkAlpha = 0.82f;
    [SerializeField] private float fadeStartDelay = 1.7f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float overscan = 1.1f;

    [Header("Night choices")]
    [SerializeField, Range(0f, 1f)] private float choiceDimAlpha = 0.58f;
    [SerializeField] private float choiceRevealDuration = 0.28f;
    [SerializeField] private float choiceDismissDuration = 0.24f;
    [SerializeField] private float pressDownDuration = 0.08f;
    [SerializeField] private float pressReleaseDuration = 0.13f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;

    public static bool BlocksIslandInput { get; private set; }

    private SpriteRenderer overlay;
    private SpriteRenderer choiceDim;
    private SpriteRenderer practiceChoice;
    private SpriteRenderer incompleteChoice;
    private Vector3 practiceBaseScale;
    private Vector3 incompleteBaseScale;
    private Color practiceBaseColor;
    private Color incompleteBaseColor;
    private bool choiceReady;
    private bool choicePressing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryStartForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryStartForActiveScene();

    private static void TryStartForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != WorldMapSceneName) return;
        if (GameObject.Find(SetupObjectName) != null) return;

        GameObject host = new GameObject(SetupObjectName);
        host.AddComponent<WorldMapNight>();
    }

    private void Awake()
    {
        ResolveChoiceSprites();
        SetChoiceVisible(false);

        BlocksIslandInput = NightMode.NightPhase
            && QuestStars.AnyNeedsRedo(NightMode.QuestIds)
            && HasBothChoices;
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private void Update()
    {
        if (!choiceReady || choicePressing || incompleteChoice == null)
        {
            return;
        }

        if (TryGetPointerPress(out Vector2 screenPosition)
            && TryScreenToWorld(screenPosition, out Vector3 worldPosition)
            && incompleteChoice.bounds.Contains(new Vector3(
                worldPosition.x,
                worldPosition.y,
                incompleteChoice.bounds.center.z)))
        {
            StartCoroutine(PressIncompleteChoice());
        }
    }

    private void OnDestroy()
    {
        BlocksIslandInput = false;
    }

    private IEnumerator Run()
    {
        if (!NightMode.NightPhase)
        {
            BlocksIslandInput = false;
            yield break;
        }

        overlay = BuildFullscreenOverlay("Night Overlay", DarkOverlayOrder, darkColor, 0f);
        if (overlay == null)
        {
            BlocksIslandInput = false;
            yield break;
        }

        if (fadeStartDelay > 0f)
        {
            yield return new WaitForSeconds(fadeStartDelay);
        }

        float duration = Mathf.Max(0.01f, fadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            SetAlpha(overlay, Mathf.Lerp(0f, maxDarkAlpha, elapsed / duration));
            yield return null;
        }
        SetAlpha(overlay, maxDarkAlpha);

        // The normal night must be fully visible first; only then dim the whole map and reveal the
        // choices above it. This keeps the user's existing night transition intact.
        if (QuestStars.AnyNeedsRedo(NightMode.QuestIds) && HasBothChoices)
        {
            yield return RevealChoices();
        }
        else
        {
            BlocksIslandInput = false;
        }
    }

    private IEnumerator RevealChoices()
    {
        choiceDim = choiceDim != null
            ? choiceDim
            : BuildFullscreenOverlay("Night Choice Dim", ChoiceDimOrder, Color.black, 0f);
        if (choiceDim == null)
        {
            BlocksIslandInput = false;
            yield break;
        }

        BlocksIslandInput = true;
        choiceReady = false;
        SetChoiceVisible(true);
        SetAlpha(practiceChoice, 0f);
        SetAlpha(incompleteChoice, 0f);
        practiceChoice.transform.localScale = practiceBaseScale * 0.9f;
        incompleteChoice.transform.localScale = incompleteBaseScale * 0.9f;
        SetAlpha(choiceDim, 0f);

        float duration = Mathf.Max(0.01f, choiceRevealDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = Smooth(t);
            float pop = EaseOutBack(t);
            SetAlpha(choiceDim, choiceDimAlpha * smooth);
            SetAlpha(practiceChoice, practiceBaseColor.a * smooth);
            SetAlpha(incompleteChoice, incompleteBaseColor.a * smooth);
            practiceChoice.transform.localScale = Vector3.LerpUnclamped(
                practiceBaseScale * 0.9f, practiceBaseScale, pop);
            incompleteChoice.transform.localScale = Vector3.LerpUnclamped(
                incompleteBaseScale * 0.9f, incompleteBaseScale, pop);
            yield return null;
        }

        SetAlpha(choiceDim, choiceDimAlpha);
        RestoreChoiceVisuals();
        choiceReady = true;
    }

    private IEnumerator PressIncompleteChoice()
    {
        choicePressing = true;
        choiceReady = false;
        GameAudio.PlayClick();

        Vector3 downScale = incompleteBaseScale * pressedScale;
        float downDuration = Mathf.Max(0.01f, pressDownDuration);
        for (float elapsed = 0f; elapsed < downDuration; elapsed += Time.deltaTime)
        {
            float t = Smooth(elapsed / downDuration);
            incompleteChoice.transform.localScale = Vector3.Lerp(incompleteBaseScale, downScale, t);
            yield return null;
        }
        incompleteChoice.transform.localScale = downScale;

        float releaseDuration = Mathf.Max(0.01f, pressReleaseDuration);
        for (float elapsed = 0f; elapsed < releaseDuration; elapsed += Time.deltaTime)
        {
            float t = EaseOutBack(elapsed / releaseDuration);
            incompleteChoice.transform.localScale = Vector3.LerpUnclamped(downScale, incompleteBaseScale, t);
            yield return null;
        }
        incompleteChoice.transform.localScale = incompleteBaseScale;

        float dismissDuration = Mathf.Max(0.01f, choiceDismissDuration);
        for (float elapsed = 0f; elapsed < dismissDuration; elapsed += Time.deltaTime)
        {
            float t = Smooth(elapsed / dismissDuration);
            SetAlpha(choiceDim, Mathf.Lerp(choiceDimAlpha, 0f, t));
            SetAlpha(practiceChoice, Mathf.Lerp(practiceBaseColor.a, 0f, t));
            SetAlpha(incompleteChoice, Mathf.Lerp(incompleteBaseColor.a, 0f, t));
            yield return null;
        }

        SetAlpha(choiceDim, 0f);
        RestoreChoiceVisuals();
        SetChoiceVisible(false);
        BlocksIslandInput = false;

        WorldMapProblemIslands map = FindObjectOfType<WorldMapProblemIslands>();
        if (map != null && map.StartNightRedoFromChoice())
        {
            yield break;
        }

        // Do not strand the player if the map controller was unexpectedly not ready.
        Debug.LogWarning("[WorldMapNight] incomplete-quest choice could not start the playable island");
        choicePressing = false;
        yield return RevealChoices();
    }

    private void ResolveChoiceSprites()
    {
        GameObject root = GameObject.Find(ChoiceRootName);
        if (root == null)
        {
            return;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.name.EndsWith("_0", StringComparison.Ordinal)) practiceChoice = renderer;
            else if (renderer.name.EndsWith("_1", StringComparison.Ordinal)) incompleteChoice = renderer;
        }

        // Fallback keeps the scene resilient if the imported sprite names are later cleaned up:
        // the practice sign is the left placement and the incomplete-quest sign is the right one.
        if ((practiceChoice == null || incompleteChoice == null) && renderers.Length >= 2)
        {
            SpriteRenderer left = renderers[0];
            SpriteRenderer right = renderers[0];
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i].transform.position.x < left.transform.position.x) left = renderers[i];
                if (renderers[i].transform.position.x > right.transform.position.x) right = renderers[i];
            }
            practiceChoice = left;
            incompleteChoice = right;
        }

        if (!HasBothChoices)
        {
            Debug.LogWarning("[WorldMapNight] expected two choice sprites under WorldMap/ui");
            return;
        }

        practiceBaseScale = practiceChoice.transform.localScale;
        incompleteBaseScale = incompleteChoice.transform.localScale;
        practiceBaseColor = practiceChoice.color;
        incompleteBaseColor = incompleteChoice.color;
    }

    private bool HasBothChoices => practiceChoice != null && incompleteChoice != null;

    private void SetChoiceVisible(bool visible)
    {
        if (practiceChoice != null) practiceChoice.gameObject.SetActive(visible);
        if (incompleteChoice != null) incompleteChoice.gameObject.SetActive(visible);
    }

    private void RestoreChoiceVisuals()
    {
        if (practiceChoice != null)
        {
            practiceChoice.color = practiceBaseColor;
            practiceChoice.transform.localScale = practiceBaseScale;
        }
        if (incompleteChoice != null)
        {
            incompleteChoice.color = incompleteBaseColor;
            incompleteChoice.transform.localScale = incompleteBaseScale;
        }
    }

    private SpriteRenderer BuildFullscreenOverlay(string objectName, int sortingOrder, Color color, float alpha)
    {
        Sprite whiteSprite = Resources.Load<Sprite>("Night/night_white");
        Material material = Resources.Load<Material>("Night/NightCutoutOverlay");
        Camera cam = Camera.main;
        if (whiteSprite == null || material == null || cam == null)
        {
            Debug.LogWarning("[WorldMapNight] missing night overlay resource or Camera.main");
            return null;
        }

        GameObject overlayObject = new GameObject(objectName);
        overlayObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = overlayObject.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        color.a = alpha;
        renderer.color = color;

        float height = 2f * cam.orthographicSize * overscan;
        float width = height * cam.aspect;
        float spriteWidth = Mathf.Max(0.0001f, whiteSprite.bounds.size.x);
        float spriteHeight = Mathf.Max(0.0001f, whiteSprite.bounds.size.y);
        Vector3 camPosition = cam.transform.position;
        overlayObject.transform.position = new Vector3(camPosition.x, camPosition.y, 0f);
        overlayObject.transform.localScale = new Vector3(width / spriteWidth, height / spriteHeight, 1f);
        return renderer;
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null) return;
        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    private static bool TryScreenToWorld(Vector2 screenPosition, out Vector3 worldPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = cam.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            -cam.transform.position.z));
        return true;
    }

    private static bool TryGetPointerPress(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
