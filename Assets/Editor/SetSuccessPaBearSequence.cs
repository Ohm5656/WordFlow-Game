using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: set the Success_pa bear's BearCutscene sequence to play bear_run_back then Thow
// (in place, each as a full clip). The bear already has BearController (all 5 states), so no
// new prefab/clip is needed — only the per-instance sequence.
public static class SetSuccessPaBearSequence
{
    [MenuItem("Tools/Scenes/Set Success_pa Bear Sequence")]
    public static void Run()
    {
        var canvas = GameObject.Find("Canvas");
        Transform bear = canvas != null ? canvas.transform.Find("Bear") : null;
        if (bear == null) { Debug.LogError("[SuccessPaBear] Canvas/Bear not found (open Success_pa first)"); return; }

        var bc = bear.GetComponent<BearCutscene>();
        if (bc == null) { Debug.LogError("[SuccessPaBear] BearCutscene missing on Bear"); return; }

        var so = new SerializedObject(bc);
        var seq = so.FindProperty("sequence");
        seq.arraySize = 2;
        // Timing enum: 0=LoopUntilArrive, 1=PlayClip, 2=Hold
        SetStep(seq.GetArrayElementAtIndex(0), "bear_run_back", -1, 1, 0f, 1f, 0f, 1f, 0f);
        SetStep(seq.GetArrayElementAtIndex(1), "Thow",         -1, 1, 0f, 1f, 0f, 1f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[SuccessPaBear] sequence set to [bear_run_back -> Thow], saved");
    }

    static void SetStep(SerializedProperty el, string state, int toWaypoint, int timing,
        float seconds, float speed, float speedEnd, float scale, float fadeSeconds)
    {
        el.FindPropertyRelative("state").stringValue = state;
        el.FindPropertyRelative("toWaypoint").intValue = toWaypoint;
        el.FindPropertyRelative("timing").enumValueIndex = timing;
        el.FindPropertyRelative("seconds").floatValue = seconds;
        el.FindPropertyRelative("speed").floatValue = speed;
        el.FindPropertyRelative("speedEnd").floatValue = speedEnd;
        el.FindPropertyRelative("scale").floatValue = scale;
        el.FindPropertyRelative("fadeSeconds").floatValue = fadeSeconds;
    }
}
