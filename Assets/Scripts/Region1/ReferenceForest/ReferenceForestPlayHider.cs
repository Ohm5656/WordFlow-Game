using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hides a fixed set of objects at runtime when the <c>reference_forest</c> scene is entered, so
/// they stay visible while authoring in the editor but disappear on Play.
///
/// Self-bootstraps from the scene name (like the fade scripts) so no in-scene wiring is needed.
/// Runs at AfterSceneLoad, before the first frame renders; the fade reveal overlay also covers any
/// momentary flash.
/// </summary>
public static class ReferenceForestPlayHider
{
    private const string ReferenceForestSceneName = "reference_forest";

    private static readonly string[] ObjectsToHide =
    {
        "bird6_16x20_6",
        "ExhaustedVillager_Lying",
        "QuestMarker_Exclamation (1)",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HideForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideForActiveScene();
    }

    private static void HideForActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != ReferenceForestSceneName)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < ObjectsToHide.Length; i++)
        {
            Transform found = FindByExactName(roots, ObjectsToHide[i]);
            if (found == null)
            {
                Debug.LogWarning($"ReferenceForestPlayHider: '{ObjectsToHide[i]}' not found in {ReferenceForestSceneName}.");
                continue;
            }

            // If a QuestProximityReveal owns this item it would re-reveal it on approach, so
            // exclude it there; otherwise just deactivate it.
            QuestProximityReveal reveal = found.GetComponentInParent<QuestProximityReveal>(true);
            if (reveal != null)
            {
                reveal.ExcludePermanently(found);
                Debug.Log($"ReferenceForestPlayHider: excluded '{ObjectsToHide[i]}' from QuestProximityReveal (stays hidden).");
            }
            else
            {
                found.gameObject.SetActive(false);
                Debug.Log($"ReferenceForestPlayHider: deactivated '{ObjectsToHide[i]}'.");
            }
        }
    }

    // Depth-first search over all transforms (including inactive) for an exact name match.
    private static Transform FindByExactName(GameObject[] roots, string targetName)
    {
        if (roots == null)
        {
            return null;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name == targetName)
                {
                    return all[j];
                }
            }
        }

        return null;
    }
}
