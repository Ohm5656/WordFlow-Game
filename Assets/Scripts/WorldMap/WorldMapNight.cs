using System;
using System.Collections;
using System.Collections.Generic;
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
/// extra dim. The left choice opens night practice; the right choice starts the incomplete-quest
/// redo route.
/// </summary>
public sealed class WorldMapNight : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string PracticeNightSceneName = "practice_night";
    private const string SetupObjectName = "WorldMap Night";
    private const string ChoiceRootName = "ui";
    private const string DayBackgroundName = "BG_Map";
    private const string NightBackgroundName = "BG_Map_Night";
    private static readonly string[] IslandNames = { "Island1", "Island2", "Island3", "Island4", "Island5" };

    // Existing map orders: BG = 0, islands/locks = 3..9, placed choice sprites = 20.
    private const int ChoiceDimOrder = 19;

    [Header("Night island palette")]
    [Tooltip("Cool moonlight applied only to island objects; the background itself uses the authored night painting.")]
    [SerializeField] private Color islandMoonlightMultiplier = new Color(0.47f, 0.60f, 0.86f, 1f);
    [Range(0f, 1f)] [SerializeField] private float islandWarmLightRetention = 0.30f;
    [SerializeField] private float overscan = 1.1f;

    [Header("Night choices")]
    [SerializeField, Range(0f, 1f)] private float choiceDimAlpha = 0.58f;
    [SerializeField] private float choiceRevealDuration = 0.28f;
    [SerializeField] private float choiceDismissDuration = 0.24f;
    [SerializeField] private float pressDownDuration = 0.08f;
    [SerializeField] private float pressReleaseDuration = 0.13f;
    [SerializeField] private float practiceSceneCoverDuration = 0.55f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;

    public static bool BlocksIslandInput { get; private set; }

    private SpriteRenderer choiceDim;
    private SpriteRenderer practiceChoice;
    private SpriteRenderer incompleteChoice;
    private SpriteRenderer dayBackground;
    private SpriteRenderer nightBackground;
    private Color dayBackgroundBaseColor;
    private Color nightBackgroundBaseColor;
    private Material backgroundUnlitMaterial;
    private readonly List<IslandRendererState> islandRenderers = new List<IslandRendererState>();
    private Vector3 practiceBaseScale;
    private Vector3 incompleteBaseScale;
    private Color practiceBaseColor;
    private Color incompleteBaseColor;
    private bool choiceReady;
    private bool choicePressing;

    private sealed class IslandRendererState
    {
        public SpriteRenderer Renderer;
        public Color OriginalColor;
    }

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
        ResolveBackgrounds();
        CacheIslandRenderers();
        ResolveChoiceSprites();
        SetChoiceVisible(false);

        SetMapPresentation(NightMode.NightPhase ? 1f : 0f);

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
        if (!choiceReady || choicePressing || !HasBothChoices)
        {
            return;
        }

        if (TryGetPointerPress(out Vector2 screenPosition)
            && TryScreenToWorld(screenPosition, out Vector3 worldPosition))
        {
            if (IsInsideChoice(practiceChoice, worldPosition))
            {
                StartCoroutine(PressPracticeChoice());
            }
            else if (IsInsideChoice(incompleteChoice, worldPosition))
            {
                StartCoroutine(PressIncompleteChoice());
            }
        }
    }

    private void LateUpdate()
    {
        if (NightMode.NightPhase)
        {
            ApplyIslandPalette(1f);
        }
    }

    private void OnDestroy()
    {
        BlocksIslandInput = false;
        if (backgroundUnlitMaterial != null)
        {
            Destroy(backgroundUnlitMaterial);
        }
    }

    private IEnumerator Run()
    {
        if (!NightMode.NightPhase)
        {
            SetMapPresentation(0f);
            BlocksIslandInput = false;
            yield break;
        }

        // WorldMapFadeIn is black while this scene initializes, so the map can start directly in
        // its painted night state. This prevents the previous day background from flashing for a
        // frame before a dark overlay caught up.
        SetMapPresentation(1f);

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
        yield return PressAndDismissChoice(incompleteChoice, incompleteBaseScale);
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

    private IEnumerator PressPracticeChoice()
    {
        yield return PressAndDismissChoice(practiceChoice, practiceBaseScale);

        // Practice is a separate night training path, so clear any stale redo flags before loading.
        NightMode.ForceNightPhaseForCurrentSession();
        NightMode.EndSession();

        yield return SceneFadeController.Cover(practiceSceneCoverDuration);
        SceneManager.LoadScene(PracticeNightSceneName);
    }

    private IEnumerator PressAndDismissChoice(SpriteRenderer pressedChoice, Vector3 baseScale)
    {
        choicePressing = true;
        choiceReady = false;
        GameAudio.PlayClick();

        Vector3 downScale = baseScale * pressedScale;
        float downDuration = Mathf.Max(0.01f, pressDownDuration);
        for (float elapsed = 0f; elapsed < downDuration; elapsed += Time.deltaTime)
        {
            float t = Smooth(elapsed / downDuration);
            pressedChoice.transform.localScale = Vector3.Lerp(baseScale, downScale, t);
            yield return null;
        }
        pressedChoice.transform.localScale = downScale;

        float releaseDuration = Mathf.Max(0.01f, pressReleaseDuration);
        for (float elapsed = 0f; elapsed < releaseDuration; elapsed += Time.deltaTime)
        {
            float t = EaseOutBack(elapsed / releaseDuration);
            pressedChoice.transform.localScale = Vector3.LerpUnclamped(downScale, baseScale, t);
            yield return null;
        }
        pressedChoice.transform.localScale = baseScale;

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

    private void ResolveBackgrounds()
    {
        // BG_Map_Night is intentionally inactive in the authored daytime scene. GameObject.Find
        // ignores inactive objects, so search the loaded scene hierarchy explicitly instead.
        GameObject dayObject = FindSceneObject(DayBackgroundName);
        GameObject nightObject = FindSceneObject(NightBackgroundName);
        dayBackground = dayObject != null ? dayObject.GetComponent<SpriteRenderer>() : null;
        nightBackground = nightObject != null ? nightObject.GetComponent<SpriteRenderer>() : null;

        if (dayBackground == null || nightBackground == null)
        {
            Debug.LogWarning("[WorldMapNight] expected BG_Map and BG_Map_Night SpriteRenderers.");
            return;
        }

        dayBackgroundBaseColor = dayBackground.color;
        nightBackgroundBaseColor = nightBackground.color;
        ApplyUnlitBackgroundMaterial();
    }

    private void CacheIslandRenderers()
    {
        islandRenderers.Clear();
        HashSet<SpriteRenderer> seen = new HashSet<SpriteRenderer>();
        for (int islandIndex = 0; islandIndex < IslandNames.Length; islandIndex++)
        {
            GameObject island = GameObject.Find(IslandNames[islandIndex]);
            if (island == null)
            {
                continue;
            }

            SpriteRenderer[] renderers = island.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !seen.Add(renderer))
                {
                    continue;
                }

                islandRenderers.Add(new IslandRendererState
                {
                    Renderer = renderer,
                    OriginalColor = renderer.color
                });
            }
        }
    }

    private void SetMapPresentation(float amount)
    {
        bool night = amount > 0.0001f;
        if (dayBackground != null)
        {
            dayBackground.gameObject.SetActive(!night);
            dayBackground.enabled = !night;
            dayBackground.color = dayBackgroundBaseColor;
        }
        if (nightBackground != null)
        {
            nightBackground.gameObject.SetActive(night);
            nightBackground.enabled = night;
            nightBackground.color = nightBackgroundBaseColor;
        }

        ApplyIslandPalette(amount);
    }

    private void ApplyUnlitBackgroundMaterial()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null)
        {
            // Projects opened with a non-URP renderer can still display the painting safely.
            unlitShader = Shader.Find("Sprites/Default");
        }
        if (unlitShader == null)
        {
            Debug.LogWarning("[WorldMapNight] could not find an unlit sprite shader for map backgrounds.");
            return;
        }

        backgroundUnlitMaterial = new Material(unlitShader)
        {
            name = "World Map Background Unlit (Runtime)"
        };
        dayBackground.sharedMaterial = backgroundUnlitMaterial;
        nightBackground.sharedMaterial = backgroundUnlitMaterial;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    return transforms[i].gameObject;
                }
            }
        }

        // Unity can restore an unsaved scene from its backup while the hierarchy is being rebuilt.
        // This fallback still finds disabled SpriteRenderers that have already been deserialized.
        SpriteRenderer[] allRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
        for (int i = 0; i < allRenderers.Length; i++)
        {
            SpriteRenderer renderer = allRenderers[i];
            if (renderer != null
                && renderer.gameObject.scene == activeScene
                && renderer.name == objectName)
            {
                return renderer.gameObject;
            }
        }

        return null;
    }

    private void ApplyIslandPalette(float amount)
    {
        amount = Mathf.Clamp01(amount);
        for (int i = 0; i < islandRenderers.Count; i++)
        {
            IslandRendererState state = islandRenderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            // WorldMapProblemIslands animates lock/unlock alpha. Preserve that live alpha while
            // applying only an RGB palette, so its current animation and click bounds stay intact.
            Color current = state.Renderer.color;
            Color color = Color.Lerp(state.OriginalColor, GradeIslandForNight(state.OriginalColor), amount);
            color.a = current.a;
            state.Renderer.color = color;
        }
    }

    private Color GradeIslandForNight(Color source)
    {
        float luminance = source.r * 0.2126f + source.g * 0.7152f + source.b * 0.0722f;
        float warmth = Mathf.Clamp01((source.r - source.b) * 1.65f);
        Color cool = new Color(
            source.r * islandMoonlightMultiplier.r,
            source.g * islandMoonlightMultiplier.g,
            source.b * islandMoonlightMultiplier.b,
            source.a);
        Color warm = new Color(source.r * 0.84f, source.g * 0.66f, source.b * 0.42f, source.a);
        float retainWarmth = warmth * Mathf.Lerp(islandWarmLightRetention * 0.45f, islandWarmLightRetention, luminance);
        return Color.Lerp(cool, warm, retainWarmth);
    }

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

    private static bool IsInsideChoice(SpriteRenderer choice, Vector3 worldPosition)
    {
        if (choice == null)
        {
            return false;
        }

        return choice.bounds.Contains(new Vector3(
            worldPosition.x,
            worldPosition.y,
            choice.bounds.center.z));
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
