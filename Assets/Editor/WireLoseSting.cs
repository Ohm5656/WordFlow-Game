using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: Success_ta_incorrect is a wrong-word scene -> its owl plays lose.wav.
// Correct scenes keep the default (false = win.wav), nothing to wire there.
public static class WireLoseSting
{
    [MenuItem("Tools/Audio/Wire Lose Sting (ta incorrect)")]
    public static void Wire()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/region 1/Success_ta_incorrect.unity");
        var epilogue = Object.FindObjectOfType<SuccessPaOwlEpilogue>(true);
        if (epilogue == null) { Debug.LogError("[LoseSting] no SuccessPaOwlEpilogue"); return; }
        var so = new SerializedObject(epilogue);
        so.FindProperty("playLoseSting").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LoseSting] DONE: Success_ta_incorrect plays lose.wav");
    }
}
