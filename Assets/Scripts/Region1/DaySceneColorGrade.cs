using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// A small, runtime-only art-direction pass for the daytime quest flow. It grades the camera's
/// rendered world (tilemaps and SpriteRenderers together) while leaving Screen Space Overlay UI
/// untouched. Night redo scenes and WorldMap deliberately never receive this profile.
/// </summary>
public sealed class DaySceneColorGrade : MonoBehaviour
{
    private static readonly HashSet<string> DaySceneNames = new HashSet<string>
    {
        "CutScene_bear",
        "CutScene_ga",
        "CutScene_ta",
        "Success_pa",
        "Success_ga",
        "Success_ga_correct",
        "Success_ta_incorrect",
        "word_build_paa_polished",
    };

    private static DaySceneColorGrade referenceForestGrade;

    private Volume volume;

    // Subscribe before the first scene starts loading. The scene-loaded callback is early enough
    // for every listed daytime scene, including a direct Play from reference_forest.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // The initial scene can already be open in the Editor when Play is pressed.  Keep a second,
    // idempotent check here so it receives the exact same daytime profile as scenes loaded later
    // through SceneManager (CutScene_*/Success_* included).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInitialSceneIsGraded()
    {
        AddToSceneIfEligible(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AddToSceneIfEligible(scene);
    }

    /// <summary>
    /// The forest crossfades this value down while its baked night artwork crossfades in. Other
    /// scenes are either fully daytime or enter with <see cref="NightMode.RedoActive"/> already set.
    /// </summary>
    public static void SetReferenceForestDayAmount(float amount)
    {
        if (referenceForestGrade != null)
        {
            referenceForestGrade.SetAmount(amount);
        }
    }

    /// <summary>
    /// Ensures the daytime profile is present when a scene is entered through a runtime-only
    /// gateway such as the forest's adventure card.
    /// </summary>
    public static void EnsureForScene(Scene scene)
    {
        AddToSceneIfEligible(scene);
    }

    private static void AddToSceneIfEligible(Scene scene)
    {
        if (!scene.isLoaded || NightMode.RedoActive || !DaySceneNames.Contains(scene.name))
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponent<DaySceneColorGrade>() != null)
            {
                return;
            }
        }

        GameObject host = new GameObject("Day Scene Color Grade");
        SceneManager.MoveGameObjectToScene(host, scene);
        DaySceneColorGrade grade = host.AddComponent<DaySceneColorGrade>();
        grade.Configure(scene.name);
    }

    private void Configure(string sceneName)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindAnyObjectByType<Camera>();
        }

        if (camera == null)
        {
            Debug.LogWarning($"[DaySceneColorGrade] No camera found for {sceneName}; daytime grading was skipped.");
            Destroy(gameObject);
            return;
        }

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
        }
        else
        {
            Debug.LogWarning($"[DaySceneColorGrade] {camera.name} has no UniversalAdditionalCameraData; daytime grading was skipped.");
            Destroy(gameObject);
            return;
        }

        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 200f;
        volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        ColorAdjustments adjustments = volume.sharedProfile.Add<ColorAdjustments>(true);
        // Keep the daytime maps slightly muted, but warm the low and mid tones rather than
        // pushing them toward blue. Exposure stays neutral so this reads as art direction,
        // not a day-to-night darkening pass.
        adjustments.postExposure.Override(0f);
        adjustments.contrast.Override(5f);
        adjustments.hueShift.Override(-3f);
        adjustments.saturation.Override(-15f);
        adjustments.colorFilter.Override(new Color(1.03f, 0.96f, 0.82f, 1f));

        SetAmount(1f);
        if (sceneName == "reference_forest")
        {
            referenceForestGrade = this;
        }
    }

    private void OnDestroy()
    {
        if (referenceForestGrade == this)
        {
            referenceForestGrade = null;
        }
    }

    private void SetAmount(float amount)
    {
        if (volume != null)
        {
            volume.weight = Mathf.Clamp01(amount);
        }
    }
}
