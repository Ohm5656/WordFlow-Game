using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Uses the authored night painting for the fixed portion of reference_forest while leaving the
/// quest actors, animals, and their existing animations above it. The daytime Tilemaps are never
/// altered in the editor and are restored whenever this component is at day amount zero.
/// </summary>
[DisallowMultipleComponent]
public sealed class ReferenceForestNightBackground : MonoBehaviour
{
    private const string GroundTilemapName = "L0_Ground";
    private const string WaterTilemapName = "L1_Water";
    private const string BridgeTilemapName = "bride";
    private const int BackgroundSortingOrder = 100;
    private const int NightEffectSortingOrder = 31900;

    private static ReferenceForestNightBackground activeInstance;

    [Header("Baked night scene")]
    [Tooltip("The authored, static night version of the forest. It intentionally excludes moving actors.")]
    [SerializeField] private Sprite nightBackground;
    [SerializeField] private int backgroundSortingOrder = BackgroundSortingOrder;

    [Header("Moving actor palette")]
    [Tooltip("Moonlight multiplier for people, animals, and quest markers that remain over the painted night map.")]
    [SerializeField] private Color moonlightMultiplier = new Color(0.46f, 0.62f, 0.88f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float warmLightRetention = 0.32f;

    private readonly List<TilemapRenderer> dayTilemapRenderers = new List<TilemapRenderer>();
    private readonly List<RendererState> staticSceneRenderers = new List<RendererState>();
    private readonly List<RendererState> movingRenderers = new List<RendererState>();

    private Camera sceneCamera;
    private NightLighting nightLighting;
    private SpriteRenderer backgroundRenderer;
    private float nightAmount;

    private sealed class RendererState
    {
        public SpriteRenderer Renderer;
        public Color OriginalColor;
        public bool OriginalEnabled;
        public int OriginalSortingOrder;
    }

    /// <summary>
    /// Called by QuestPathSequence at the same time as its old night-lighting fade. It is a no-op
    /// if the scene was opened without this presentation component.
    /// </summary>
    public static void SetSceneNight(float amount)
    {
        if (activeInstance != null)
        {
            activeInstance.SetNight(amount);
        }

        // The painting is already crossfading here. Keep only its living animal actors in the
        // matching relative pens, without changing this fade's duration or appearance.
        ReferenceForestNightActorCalibration.SetNightFadeAmount(amount);

        // The daytime grade is part of the same visual hand-off. Fading it away while the
        // authored night painting fades in keeps the original moonlit palette untouched.
        DaySceneColorGrade.SetReferenceForestDayAmount(1f - amount);
    }

    private void Awake()
    {
        activeInstance = this;
        sceneCamera = Camera.main;
        nightLighting = FindAnyObjectByType<NightLighting>();

        CreateBackgroundRenderer();
        CacheDayTilemaps();
        CacheSceneRenderers();

        // A night redo enters under the scene's black reveal, so it must already be on the
        // painted version before the first visible frame. A normal daytime run remains untouched.
        SetNight(NightMode.RedoActive ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    private void LateUpdate()
    {
        if (nightAmount <= 0f)
        {
            return;
        }

        // QuestPathSequence still owns the original lighting component. Keep its full-screen
        // overlay off in baked-background mode so its darkness and lamp glows are not painted a
        // second time over the authored night artwork.
        if (nightLighting != null)
        {
            nightLighting.SetNight(0f);
        }

        ApplyMovingRendererPalette(nightAmount);
    }

    private void SetNight(float amount)
    {
        nightAmount = Mathf.Clamp01(amount);

        if (backgroundRenderer != null)
        {
            backgroundRenderer.enabled = nightAmount > 0.0001f;
            Color color = Color.white;
            color.a = nightAmount;
            backgroundRenderer.color = color;
        }

        // The painting is rendered above the Tilemaps during the fade, producing a true
        // crossfade without changing any of the day Tilemap data. Once opaque, stop drawing the
        // covered day layers and props to avoid mixing the daytime art into the painted night map.
        bool hideDayLayers = nightAmount >= 0.999f;
        for (int i = 0; i < dayTilemapRenderers.Count; i++)
        {
            TilemapRenderer renderer = dayTilemapRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = !hideDayLayers;
            }
        }

        ApplyStaticScene(nightAmount, hideDayLayers);
        ApplyMovingRendererPalette(nightAmount);

        if (nightLighting != null)
        {
            nightLighting.SetNight(0f);
        }
    }

    private void CreateBackgroundRenderer()
    {
        if (nightBackground == null)
        {
            Debug.LogWarning("[ReferenceForestNightBackground] Night background sprite is missing.");
            return;
        }

        GameObject backgroundObject = new GameObject("Night Forest Background");
        backgroundObject.transform.SetParent(transform, false);
        backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = nightBackground;
        backgroundRenderer.sortingOrder = backgroundSortingOrder;
        backgroundRenderer.color = new Color(1f, 1f, 1f, 0f);

        FitBackgroundToCamera();
    }

    private void FitBackgroundToCamera()
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null || sceneCamera == null)
        {
            return;
        }

        float viewHeight = sceneCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * sceneCamera.aspect;
        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        // Cover rather than stretch: the source painting was authored at the game's 16:9 ratio.
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
        backgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        backgroundRenderer.transform.position = Vector3.zero;

        Bounds bounds = backgroundRenderer.bounds;
        Vector3 cameraCenter = sceneCamera.transform.position;
        backgroundRenderer.transform.position += new Vector3(
            cameraCenter.x - bounds.center.x,
            cameraCenter.y - bounds.center.y,
            -backgroundRenderer.transform.position.z);
    }

    private void CacheDayTilemaps()
    {
        CacheTilemap(GroundTilemapName);
        CacheTilemap(WaterTilemapName);
        CacheTilemap(BridgeTilemapName);
    }

    private void CacheTilemap(string objectName)
    {
        GameObject tilemapObject = GameObject.Find(objectName);
        TilemapRenderer renderer = tilemapObject != null ? tilemapObject.GetComponent<TilemapRenderer>() : null;
        if (renderer == null)
        {
            Debug.LogWarning($"[ReferenceForestNightBackground] Day Tilemap '{objectName}' was not found.");
            return;
        }

        dayTilemapRenderers.Add(renderer);
    }

    private void CacheSceneRenderers()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        HashSet<SpriteRenderer> seen = new HashSet<SpriteRenderer>();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            SpriteRenderer[] renderers = roots[rootIndex].GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null
                    || renderer == backgroundRenderer
                    || renderer.sortingOrder >= NightEffectSortingOrder
                    || (nightLighting != null && renderer.transform.IsChildOf(nightLighting.transform))
                    || !seen.Add(renderer))
                {
                    continue;
                }

                RendererState state = new RendererState
                {
                    Renderer = renderer,
                    OriginalColor = renderer.color,
                    OriginalEnabled = renderer.enabled,
                    OriginalSortingOrder = renderer.sortingOrder
                };

                // The painted image already contains every static prop. Keep only the original
                // moving people, animals, and quest actors in front, so they retain their own
                // walk/patrol animation without bringing the daytime lamps along with them.
                if (IsMovingActor(renderer))
                {
                    movingRenderers.Add(state);
                }
                else
                {
                    staticSceneRenderers.Add(state);
                }
            }
        }
    }

    private static bool IsMovingActor(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Transform transform = renderer.transform;
        for (Transform current = transform; current != null; current = current.parent)
        {
            // These are the two ambient roots that own the normal walking people and animals.
            if (current.name == "animal" || current.name == "character")
            {
                return true;
            }

            // All quest actors that still need to move/fade are children of this controller.
            // Their existing behaviour continues untouched; only their draw order is calibrated.
            if (current.GetComponent<QuestPathSequence>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyStaticScene(float amount, bool hideDayLayers)
    {
        for (int i = 0; i < staticSceneRenderers.Count; i++)
        {
            RendererState state = staticSceneRenderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.enabled = state.OriginalEnabled && !hideDayLayers;
            state.Renderer.sortingOrder = state.OriginalSortingOrder;
            Color color = state.OriginalColor;
            color.a *= 1f - amount;
            state.Renderer.color = color;
        }
    }

    private void ApplyMovingRendererPalette(float amount)
    {
        for (int i = 0; i < movingRenderers.Count; i++)
        {
            RendererState state = movingRenderers[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.enabled = state.OriginalEnabled;
            // The static night painting sits at 100. Most living actors were authored at order
            // zero, so lift only those lower-order actors just above it; their relative order and
            // all existing movement/patrol logic remain unchanged.
            state.Renderer.sortingOrder = amount > 0.0001f
                ? Mathf.Max(state.OriginalSortingOrder, backgroundSortingOrder + 1)
                : state.OriginalSortingOrder;

            // Keep the alpha currently owned by the actor's existing fade/animation routine;
            // only grade RGB so quest reveals and sprite swaps continue to work unchanged.
            Color current = state.Renderer.color;
            Color moonlit = GradeForMoonlight(state.OriginalColor);
            Color color = Color.Lerp(state.OriginalColor, moonlit, amount);
            color.a = current.a;
            state.Renderer.color = color;
        }
    }

    private Color GradeForMoonlight(Color source)
    {
        float luminance = source.r * 0.2126f + source.g * 0.7152f + source.b * 0.0722f;
        float warmth = Mathf.Clamp01((source.r - source.b) * 1.65f);

        Color cool = new Color(
            source.r * moonlightMultiplier.r,
            source.g * moonlightMultiplier.g,
            source.b * moonlightMultiplier.b,
            source.a);
        Color retainedWarmth = new Color(
            source.r * 0.84f,
            source.g * 0.66f,
            source.b * 0.42f,
            source.a);
        float keepWarm = warmth * Mathf.Lerp(warmLightRetention * 0.45f, warmLightRetention, luminance);
        return Color.Lerp(cool, retainedWarmth, keepWarm);
    }
}
