using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-shot: make sure every scene the reference_forest -> CutScene_bear flow loads by name is
// ENABLED in the build list. A scene that is present but enabled:0 still fails LoadScene with
// "couldn't be loaded because it has not been added to the active build profile". Run via menu.
public static class EnableBearFlowScenes
{
    static readonly string[] Paths =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity", // reference_forest beat 1 -> here
        "Assets/Scenes/region 1/cut_scene3.unity",    // CutScene_bear wrong-order (crow) path
        "Assets/Scenes/region 1/Success_pa.unity",
        "Assets/Scenes/region 1/Success_ga.unity",
    };

    [MenuItem("Tools/Scenes/Enable Bear Flow Scenes")]
    public static void Run()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var want = new HashSet<string>(Paths);

        for (int i = 0; i < scenes.Count; i++)
            if (want.Contains(scenes[i].path) && !scenes[i].enabled)
                scenes[i] = new EditorBuildSettingsScene(scenes[i].path, true);

        foreach (var p in Paths)
            if (!scenes.Exists(s => s.path == p))
                scenes.Add(new EditorBuildSettingsScene(p, true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[Build] Enabled bear-flow scenes: " + string.Join(", ", Paths));
    }
}
