using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: rename cut_scene1 -> CutScene_bear, create Success_pa (copy of success),
// add Success_pa to Build Settings, and fix cut_scene3's CrowCutsceneController return
// target. Atomic in a single menu run so nothing is lost to a domain reload.
public static class RenameBearScenes
{
    const string Dir = "Assets/Scenes/region 1";
    const string OldScene = Dir + "/cut_scene1.unity";
    const string NewScene = Dir + "/CutScene_bear.unity";
    const string SuccessScene = Dir + "/success.unity";
    const string SuccessPaScene = Dir + "/Success_pa.unity";

    [MenuItem("Tools/Scenes/Rename + Create Bear Scenes")]
    public static void Run()
    {
        EditorSceneManager.SaveOpenScenes();

        // switch away so the open scene's asset can be renamed
        EditorSceneManager.OpenScene(SuccessScene, OpenSceneMode.Single);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldScene) != null)
        {
            string err = AssetDatabase.RenameAsset(OldScene, "CutScene_bear");
            Debug.Log(string.IsNullOrEmpty(err)
                ? "[RenameBearScenes] renamed cut_scene1 -> CutScene_bear"
                : "[RenameBearScenes] rename FAILED: " + err);
        }
        else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NewScene) != null)
        {
            Debug.Log("[RenameBearScenes] already renamed (CutScene_bear exists)");
        }

        // create Success_pa as a copy of success (working base to tweak)
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SuccessPaScene) == null)
        {
            bool ok = AssetDatabase.CopyAsset(SuccessScene, SuccessPaScene);
            Debug.Log("[RenameBearScenes] create Success_pa: " + ok);
        }
        else Debug.Log("[RenameBearScenes] Success_pa already exists");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Build Settings: ensure both scenes are listed so LoadScene-by-name works
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EnsureInBuild(list, NewScene);
        EnsureInBuild(list, SuccessPaScene);
        EditorBuildSettings.scenes = list.ToArray();

        // cut_scene3 returns to the renamed scene after the crow beat
        var s3 = EditorSceneManager.OpenScene(Dir + "/cut_scene3.unity", OpenSceneMode.Single);
        int fixedCount = 0;
        foreach (var crow in Object.FindObjectsByType<CrowCutsceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(crow);
            var prop = so.FindProperty("returnSceneName");
            if (prop != null) { prop.stringValue = "CutScene_bear"; so.ApplyModifiedPropertiesWithoutUndo(); fixedCount++; }
        }
        EditorSceneManager.MarkSceneDirty(s3);
        EditorSceneManager.SaveScene(s3);
        Debug.Log($"[RenameBearScenes] cut_scene3 CrowCutsceneController returnSceneName updated x{fixedCount}");

        // leave the user on the renamed scene
        EditorSceneManager.OpenScene(NewScene, OpenSceneMode.Single);
        Debug.Log("[RenameBearScenes] DONE");
    }

    static void EnsureInBuild(List<EditorBuildSettingsScene> list, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
        if (!list.Exists(s => s.path == path))
            list.Add(new EditorBuildSettingsScene(path, true));
    }
}
