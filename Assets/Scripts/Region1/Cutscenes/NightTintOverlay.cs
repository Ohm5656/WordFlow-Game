using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Chooses the authored day/night background in quest cutscenes and gives foreground
/// sprites a moonlit presentation during a night redo.
///
/// This intentionally does not change reference_forest. That scene owns its own
/// NightLighting system, while the scenes listed here use pre-painted night backgrounds.
/// </summary>
public sealed class NightTintOverlay : MonoBehaviour
{
    private const string DayBackgroundName = "background";
    private const string NightBackgroundName = "background_night";

    private static readonly string[] SupportedScenes =
    {
        "CutScene_bear", "CutScene_ga",
        "Success_pa", "Success_ga", "Success_ga_correct", "Success_ta_incorrect",
    };

    // HUD remains legible instead of inheriting the world-space moonlight treatment.
    private static readonly string[] UiRootsToKeepBright =
    {
        "star_hud", "star_root", "lock_overlay",
    };

    private static Material nightSpriteMaterial;
    private static Material nightSpriteMatteCleanupMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForActiveScene();
    }

    private static void ApplyForActiveScene()
    {
        if (!IsSupportedScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        Canvas canvas = FindSceneCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[NightSceneVisuals] no root Canvas in this scene — visual switch skipped");
            return;
        }

        bool isNight = NightMode.RedoActive;
        SwitchBackground(canvas.transform, isNight);

        if (isNight)
        {
            ApplyNightSpriteMaterial(canvas.transform);
        }
    }

    private static void SwitchBackground(Transform canvas, bool isNight)
    {
        Graphic dayBackground = FindGraphic(canvas, DayBackgroundName);
        Graphic nightBackground = FindGraphic(canvas, NightBackgroundName);

        if (dayBackground == null || nightBackground == null)
        {
            Debug.LogWarning("[NightSceneVisuals] expected both background and background_night under the scene Canvas");
            return;
        }

        // Leave the day object's RectTransform alive: OwlGreetingCutscene uses it as the zoom bound.
        // Only its Graphic is switched off. The two authored backgrounds share the same placement.
        dayBackground.enabled = !isNight;
        nightBackground.gameObject.SetActive(isNight);
        nightBackground.enabled = isNight;
    }

    private static Graphic FindGraphic(Transform canvas, string objectName)
    {
        Transform target = canvas.Find(objectName);
        return target != null ? target.GetComponent<Graphic>() : null;
    }

    private static void ApplyNightSpriteMaterial(Transform canvas)
    {
        Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
        for (int index = 0; index < graphics.Length; index++)
        {
            Graphic graphic = graphics[index];
            if (!ShouldApplyNightMaterial(graphic))
            {
                continue;
            }

            // Graphic.material falls back to defaultMaterial when no authored material is assigned.
            // Preserve specialised UI materials (for example, the lock vignette shader), but allow
            // an older instance of our own runtime material to be replaced after a script reload.
            if (graphic.material != graphic.defaultMaterial && !IsOurNightMaterial(graphic.material))
            {
                continue;
            }

            Image image = (Image)graphic;
            bool needsMatteCleanup = IsGreenMatteActor(image.transform);
            Material material = GetNightSpriteMaterial(needsMatteCleanup);
            if (material != null)
            {
                graphic.material = material;
            }
        }
    }

    private static bool ShouldApplyNightMaterial(Graphic graphic)
    {
        if (graphic == null)
        {
            return false;
        }

        string name = graphic.gameObject.name;
        if (name == DayBackgroundName || name == NightBackgroundName)
        {
            return false;
        }

        for (Transform current = graphic.transform; current != null; current = current.parent)
        {
            for (int index = 0; index < UiRootsToKeepBright.Length; index++)
            {
                if (current.name == UiRootsToKeepBright[index])
                {
                    return false;
                }
            }
        }

        // Empty Images are intentional UI layout/panel elements. Do not turn Unity's white default
        // texture into a coloured rectangle; only actual sprite artwork needs the night presentation.
        return graphic is Image image && image.sprite != null;
    }

    private static bool IsOurNightMaterial(Material material)
    {
        return material != null && material.shader != null && material.shader.name == "UI/NightSpritePalette";
    }

    private static bool IsGreenMatteActor(Transform transform)
    {
        // The source frame sequences for these two animated actors contain a teal/green
        // background-removal matte in the pixels themselves. This is independent of
        // Unity's optional alpha-split import setting, so checking associatedAlphaSplitTexture
        // would miss the desktop import used in the editor.
        //
        // Keep this narrow: scenery has legitimate green foliage and must never be chroma-keyed.
        for (Transform current = transform; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("crow") || name.Contains("bear"))
            {
                return true;
            }
        }

        return false;
    }

    private static Material GetNightSpriteMaterial(bool cleanGreenMatte)
    {
        Material existingMaterial = cleanGreenMatte ? nightSpriteMatteCleanupMaterial : nightSpriteMaterial;
        if (existingMaterial != null)
        {
            return existingMaterial;
        }

        Shader shader = Resources.Load<Shader>("NightSpritePalette");
        if (shader == null)
        {
            Debug.LogError("[NightSceneVisuals] Resources/NightSpritePalette.shader is missing");
            return null;
        }

        Material material = new Material(shader)
        {
            name = cleanGreenMatte ? "Night Sprite Palette + Matte Cleanup (Runtime)" : "Night Sprite Palette (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        material.SetFloat("_ChromaKeyStrength", cleanGreenMatte ? 1f : 0f);
        material.SetFloat("_MatteAlphaFloor", cleanGreenMatte ? 0.94f : 0f);

        if (cleanGreenMatte)
        {
            nightSpriteMatteCleanupMaterial = material;
        }
        else
        {
            nightSpriteMaterial = material;
        }

        return material;
    }

    private static Canvas FindSceneCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];
            if (canvas == null || canvas.transform.parent != null)
            {
                continue;
            }

            if (best == null || canvas.sortingOrder < best.sortingOrder)
            {
                best = canvas;
            }
        }

        return best;
    }

    private static bool IsSupportedScene(string sceneName)
    {
        for (int index = 0; index < SupportedScenes.Length; index++)
        {
            if (SupportedScenes[index] == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}
