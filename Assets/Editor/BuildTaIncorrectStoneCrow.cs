using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Legacy menu entry kept for designers. The wrong real word is now paa: standalone Thow hands play
// over the petrified crow, then the one-star no-owl retry return goes back to CutScene_ga.
// Atomic scene edit + SaveScene (domain-reload safety).
public static class BuildTaIncorrectStoneCrow
{
    const string ScenePath = "Assets/Scenes/region 1/Success_ta_incorrect.unity";

    [MenuItem("Tools/Success/Build Ta Incorrect Stone Crow")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[TaStoneCrow] no Canvas"); return; }

        UpgradeCutSceneGaParity.WireThowIncorrectScene(canvas.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TaStoneCrow] DONE: Thow + petrified crow + one-star no-owl return wired");
    }
}
