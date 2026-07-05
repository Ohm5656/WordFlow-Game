using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: reduce the owl's post-talk-round freeze (holdFrozenAfterRound) from 1s to 0.5s across
// every scene that has an OwlGreetingCutscene, so the cutscene moves on sooner after each round.
public static class SetOwlHoldAfterRound
{
    const float NewHold = 0.5f;

    static readonly string[] Scenes =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity",
        "Assets/Scenes/region 1/CutScene_ga.unity",
        "Assets/Scenes/region 1/CutScene_ta.unity",
        "Assets/Scenes/region 1/Success_pa.unity",
        "Assets/Scenes/region 1/Success_ga.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity",
        "Assets/Scenes/region 1/Success_ta_incorrect.unity",
    };

    [MenuItem("Tools/Fix/Owl Hold 0.5s")]
    public static void Run()
    {
        int changed = 0, scenesChanged = 0;
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            // Both owl-talk components carry a holdFrozenAfterRound (post-round freeze).
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb is not (OwlGreetingCutscene or SuccessPaOwlEpilogue)) continue;
                var so = new SerializedObject(mb);
                var prop = so.FindProperty("holdFrozenAfterRound");
                if (prop == null || Mathf.Approximately(prop.floatValue, NewHold)) continue;
                prop.floatValue = NewHold;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mb);
                dirty = true;
                changed++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesChanged++;
            }
        }
        Debug.Log($"[SetOwlHoldAfterRound] Set holdFrozenAfterRound={NewHold} on {changed} component(s) across {scenesChanged} scene(s).");
    }
}
