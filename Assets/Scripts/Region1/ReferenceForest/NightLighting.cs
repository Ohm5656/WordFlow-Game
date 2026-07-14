using UnityEngine;

/// <summary>
/// Stylised night lighting for the reference_forest map: a full-screen dark overlay
/// with soft transparent openings around the light fixtures, plus warm glow pools.
/// Everything is world-space SpriteRenderers so the glows anchor to their fixtures and scale
/// with the orthographic camera. <see cref="SetNight"/> drives the whole thing from a single
/// 0..1 amount, which QuestPathSequence fades in when the player triggers nightfall.
/// </summary>
public sealed class NightLighting : MonoBehaviour
{
    private static readonly int[] LightDataIds =
    {
        Shader.PropertyToID("_LightData0"),
        Shader.PropertyToID("_LightData1"),
        Shader.PropertyToID("_LightData2"),
        Shader.PropertyToID("_LightData3"),
        Shader.PropertyToID("_LightData4"),
        Shader.PropertyToID("_LightData5"),
        Shader.PropertyToID("_LightData6"),
        Shader.PropertyToID("_LightData7")
    };

    [Header("Darkening overlay")]
    [Tooltip("Big sprite covering the camera view; tinted to this colour, alpha driven by the night amount.")]
    [SerializeField] private SpriteRenderer darkOverlay;
    [SerializeField] private Color darkColor = new Color(0.035f, 0.028f, 0.060f, 1f);
    [Range(0f, 1f)] [SerializeField] private float maxDarkAlpha = 0.69f;

    [Header("Warm glow pools")]
    [Tooltip("Radial glow sprites placed on the light fixtures.")]
    [SerializeField] private SpriteRenderer[] glows;
    [Tooltip("Per-light multiplier used to keep the glow warm over differently coloured terrain.")]
    [SerializeField] private float[] glowStrengths;
    [SerializeField] private Color glowColor = Color.white;
    [Range(0f, 1f)] [SerializeField] private float maxGlowAlpha = 0.42f;

    [Header("Lit fixtures")]
    [Tooltip("Original campfire/lamp renderers used as the source for the bright night copies.")]
    [SerializeField] private SpriteRenderer[] lightSources;
    [Tooltip("Radius of the soft opening cut into the darkness around each source.")]
    [SerializeField] private float[] lightRevealRadii;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float clearCenterFraction = 0.01f;
    [Tooltip("How much of the night overlay remains at the brightest point, preventing a harsh daylight cutout.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumDarknessInLight = 0.58f;
    [Tooltip("Copies rendered above the dark overlay so the fixtures themselves stay illuminated.")]
    [SerializeField] private SpriteRenderer[] lightHighlights;
    [Range(0f, 1f)] [SerializeField] private float maxLightSourceAlpha = 0.46f;

    private float nightAmount;
    private MaterialPropertyBlock overlayProperties;

    // Runtime lights (shader slots 4-7): hero + night-quest pools. Zeroed = off.
    // Slot 0 = hero, slots 1-2 = the two night quests (paa/kaa), slot 3 spare.
    private readonly Vector4[] dynamicLights = new Vector4[4];

    private void Awake()
    {
        // Start as day; the quest fades night in.
        SetNight(0f);
    }

    /// <summary>Sets/clears one of the 4 runtime light slots (shader slots 4-7). Pass
    /// outerRadius &lt;= 0 to switch the slot off. Used for the hero's follow-light and the
    /// night-quest light pools; the 4 fixture lights (slots 0-3) are unaffected.</summary>
    public void SetDynamicLight(int slot, Vector2 worldPos, float innerRadius, float outerRadius)
    {
        if (slot < 0 || slot >= dynamicLights.Length)
        {
            return;
        }

        dynamicLights[slot] = outerRadius > 0f
            ? new Vector4(worldPos.x, worldPos.y, innerRadius, outerRadius)
            : Vector4.zero;
        SyncOverlayOpenings();
    }

    private void OnValidate()
    {
        SyncOverlayOpenings();
    }

    private void LateUpdate()
    {
        // The campfire is animated, so keep its bright copy in sync with the live sprite.
        SyncLightHighlights();
    }

    /// <summary>0 = full day (clear), 1 = full night (dark + glows lit).</summary>
    public void SetNight(float amount)
    {
        amount = Mathf.Clamp01(amount);
        nightAmount = amount;

        if (darkOverlay != null)
        {
            Color c = darkColor;
            c.a = maxDarkAlpha * amount;
            darkOverlay.color = c;
            SyncOverlayOpenings();
        }

        if (glows != null)
        {
            Color g = glowColor;
            g.a = maxGlowAlpha * amount;
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] != null)
                {
                    float strength = glowStrengths != null && i < glowStrengths.Length
                        ? Mathf.Max(0f, glowStrengths[i])
                        : 1f;
                    g.a = Mathf.Clamp01(maxGlowAlpha * amount * strength);
                    glows[i].color = g;
                }
            }
        }

        SyncLightHighlights();
    }

    private void SyncOverlayOpenings()
    {
        if (darkOverlay == null || lightSources == null)
        {
            return;
        }

        if (overlayProperties == null)
        {
            overlayProperties = new MaterialPropertyBlock();
        }

        darkOverlay.GetPropertyBlock(overlayProperties);
        for (int i = 0; i < 4; i++)
        {
            Vector4 data = Vector4.zero;
            if (i < lightSources.Length && lightSources[i] != null)
            {
                Vector3 center = lightSources[i].bounds.center;
                float outerRadius = lightRevealRadii != null && i < lightRevealRadii.Length
                    ? Mathf.Max(0.01f, lightRevealRadii[i])
                    : 2.5f;
                float innerRadius = outerRadius * Mathf.Clamp01(clearCenterFraction);
                data = new Vector4(center.x, center.y, innerRadius, outerRadius);
            }

            overlayProperties.SetVector(LightDataIds[i], data);
        }

        // Slots 4-7: runtime lights (hero + night quests). Written every sync alongside the fixture
        // slots — the property block is rebuilt from the renderer each call, so a partial write
        // here would drop whichever dynamic lights aren't re-set this frame.
        for (int i = 0; i < dynamicLights.Length; i++)
        {
            overlayProperties.SetVector(LightDataIds[4 + i], dynamicLights[i]);
        }

        overlayProperties.SetFloat("_MinimumDarkness", minimumDarknessInLight);
        darkOverlay.SetPropertyBlock(overlayProperties);
    }

    private void SyncLightHighlights()
    {
        if (lightSources == null || lightHighlights == null)
        {
            return;
        }

        int count = Mathf.Min(lightSources.Length, lightHighlights.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer source = lightSources[i];
            SpriteRenderer highlight = lightHighlights[i];
            if (source == null || highlight == null)
            {
                continue;
            }

            bool visible = nightAmount > 0.001f && source.enabled && source.gameObject.activeInHierarchy;
            highlight.enabled = visible;
            if (!visible)
            {
                continue;
            }

            Transform sourceTransform = source.transform;
            Transform highlightTransform = highlight.transform;
            highlightTransform.position = sourceTransform.position;
            highlightTransform.rotation = sourceTransform.rotation;
            highlightTransform.localScale = sourceTransform.lossyScale;

            highlight.sprite = source.sprite;
            highlight.sharedMaterial = source.sharedMaterial;
            highlight.flipX = source.flipX;
            highlight.flipY = source.flipY;
            highlight.drawMode = source.drawMode;
            highlight.size = source.size;

            Color color = source.color;
            color.a *= maxLightSourceAlpha * nightAmount;
            highlight.color = color;
        }
    }
}
