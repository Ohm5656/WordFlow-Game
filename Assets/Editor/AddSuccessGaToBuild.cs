using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-shot: ensure Success_ga is in the Build Settings (enabled), so the bear puzzle's กา branch
// can SceneManager.LoadScene("Assets/Scenes/region 1/Success_ga.unity"). Run via
// Tools/Scenes/Add Success_ga To Build.
public static class AddSuccessGaToBuild
{
    const string ScenePath = "Assets/Scenes/region 1/Success_ga.unity";

    [MenuItem("Tools/Scenes/Add Success_ga To Build")]
    public static void Run()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            // make sure it is enabled
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == ScenePath && !scenes[i].enabled)
                    scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Build] Success_ga already present — ensured enabled.");
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[Build] Added {ScenePath} (enabled) to Build Settings. Total scenes: {scenes.Count}");
    }
}
