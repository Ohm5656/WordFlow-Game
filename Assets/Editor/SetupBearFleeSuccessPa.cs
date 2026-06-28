using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-shot: in Success_pa, make the bear run away (bear_run_back) from where it stands to wp_1,
// then fade the bear + the throw hands (Thow) out and load reference_forest.
// Configures the existing BearCutscene's sequence + new ending fields. Run via Tools/Bear/Setup Flee (Success_pa).
public static class SetupBearFleeSuccessPa
{
    const string ScenePath = "Assets/Scenes/region 1/Success_pa.unity";
    const string NextScene = "reference_forest";

    [MenuItem("Tools/Bear/Setup Flee (Success_pa)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Success_pa")
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var bearGo = GameObject.Find("Canvas/Bear");
        var bear = bearGo != null ? bearGo.GetComponent<BearCutscene>() : null;
        if (bear == null) { Debug.LogError("[Flee] Canvas/Bear with BearCutscene not found"); return; }

        // Hands group: add a CanvasGroup to Canvas/Thow so the bear's ending can fade it out too.
        var thow = GameObject.Find("Canvas/Thow");
        CanvasGroup thowCg = null;
        if (thow != null)
        {
            thowCg = thow.GetComponent<CanvasGroup>();
            if (thowCg == null) thowCg = thow.AddComponent<CanvasGroup>();
        }
        else Debug.LogWarning("[Flee] Canvas/Thow not found — hands won't fade (set alsoFade later)");

        var so = new SerializedObject(bear);
        so.FindProperty("startWaypoint").intValue = -1;      // start where it stands (no teleport)
        so.FindProperty("fadeInDuration").floatValue = 0f;   // already visible — appear instantly
        so.FindProperty("endFadeOutDuration").floatValue = 0.6f;
        so.FindProperty("nextScene").stringValue = NextScene;
        so.FindProperty("resumeForestAtBeat2").boolValue = true;  // land at the crow quest, not the bear intro

        // sequence = one run leg to wp_1
        var seq = so.FindProperty("sequence");
        seq.arraySize = 1;
        var s0 = seq.GetArrayElementAtIndex(0);
        s0.FindPropertyRelative("state").stringValue = "bear_run_back";
        s0.FindPropertyRelative("toWaypoint").intValue = 1;          // wp_1
        s0.FindPropertyRelative("timing").enumValueIndex = 1;        // PlayClip — play bear_run_back fully
        s0.FindPropertyRelative("seconds").floatValue = 0f;          // ignored for PlayClip (uses clip length)
        s0.FindPropertyRelative("speed").floatValue = 1f;
        s0.FindPropertyRelative("speedEnd").floatValue = 0f;
        s0.FindPropertyRelative("scale").floatValue = 1f;
        s0.FindPropertyRelative("fadeSeconds").floatValue = 0f;
        s0.FindPropertyRelative("moveDelay").floatValue = 2f;        // turn-around in place ~2s, then run to wp_1

        // alsoFade = [ Thow CanvasGroup ]
        var also = so.FindProperty("alsoFade");
        also.arraySize = thowCg != null ? 1 : 0;
        if (thowCg != null) also.GetArrayElementAtIndex(0).objectReferenceValue = thowCg;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool inBuild = System.Array.Exists(EditorBuildSettings.scenes,
            s => s.enabled && s.path.EndsWith($"/{NextScene}.unity"));
        Debug.Log($"[Flee] DONE — bear_run_back -> wp_1, fade out (bear+hands), load '{NextScene}'. " +
                  (inBuild ? "scene in build settings ✓" : $"WARNING: '{NextScene}' not enabled in Build Settings — LoadScene will fail"));
    }
}
