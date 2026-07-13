using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Add Star Hud To Success Scenes
/// Copies the wired star_hud out of CutScene_bear into Success_pa and Success_ga, in recap mode:
///  - Success_pa : the 2-3 earned stars pop into the board while the owl praises.
///  - Success_ga : the single earned star shows, holds, then fades out — "your stars reset, try again".
/// Idempotent: replaces an existing star_hud in the target scene rather than stacking a second one.
public static class AddStarHudToSuccessScenes
{
    const string BearScene = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string PaScene = "Assets/Scenes/region 1/Success_pa.unity";
    const string GaScene = "Assets/Scenes/region 1/Success_ga.unity";
    const string PrefabPath = "Assets/Prefabs/StarHud.prefab";
    const string HudName = "star_hud";

    [MenuItem("Tools/Quest/Add Star Hud To Success Scenes")]
    public static void Run()
    {
        // 1. Snapshot the wired star_hud out of CutScene_bear as a prefab.
        EditorSceneManager.OpenScene(BearScene, OpenSceneMode.Single);

        GameObject source = GameObject.Find(HudName);
        if (source == null) { Debug.LogError("[AddStarHud] star_hud not found in CutScene_bear — run Tools/Quest/Build Star Hud first"); return; }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        if (prefab == null) { Debug.LogError($"[AddStarHud] could not write {PrefabPath}"); return; }
        Debug.Log($"[AddStarHud] snapshotted {PrefabPath}");

        // 2. Drop it into each Success scene in the right recap mode.
        Install(PaScene, recapOnStart: true, fadeOutAfterRecap: false, prefab);
        Install(GaScene, recapOnStart: true, fadeOutAfterRecap: true, prefab);

        Debug.Log("[AddStarHud] DONE — Success_pa (recap) + Success_ga (recap + fade-out) saved");
    }

    static void Install(string scenePath, bool recapOnStart, bool fadeOutAfterRecap, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError($"[AddStarHud] no Canvas in {scenePath}"); return; }

        // Idempotent: blow away a previous copy so re-running never stacks two boards.
        Transform existing = canvas.transform.Find(HudName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        hud.name = HudName;
        hud.transform.SetSiblingIndex(canvas.transform.childCount - 1); // on top of the scene's art

        var star = hud.GetComponent<StarHud>();
        if (star == null) { Debug.LogError($"[AddStarHud] prefab has no StarHud in {scenePath}"); return; }

        var so = new SerializedObject(star);
        so.FindProperty("resetOnAwake").boolValue = false;          // only CutScene_bear clears the count
        so.FindProperty("recapOnStart").boolValue = recapOnStart;
        so.FindProperty("fadeOutAfterRecap").boolValue = fadeOutAfterRecap;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AddStarHud] {scenePath}: recap={recapOnStart} fadeOut={fadeOutAfterRecap}");
    }
}
