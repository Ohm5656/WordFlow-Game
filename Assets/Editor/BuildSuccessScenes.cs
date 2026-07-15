using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot: rebuilds the Success_ga_correct + Success_ta_incorrect beats on the new frame anims.
//   Success_ga_correct: Bear -> ga_set_free (crow freed) chaining seamlessly into ga_set_free2
//   (consecutive frames of one render, shared union-bbox crop), Thow deleted, owl epilogue +
//   reference_forest hand-off kept.
//   Success_ta_incorrect: standalone Thow hands play over the petrified crow, then the
//   one-star no-owl retry return goes back to CutScene_ga.
// Scene edits are atomic per scene + SaveScene (domain-reload safety).
public static class BuildSuccessScenes
{
    const string QuestDir = "Assets/Art/quest_map";
    const string GaCorrectPath = "Assets/Scenes/region 1/Success_ga_correct.unity";
    const string TaIncorrectPath = "Assets/Scenes/region 1/Success_ta_incorrect.unity";

    [MenuItem("Tools/Success/Build Ga+Ta Success Scenes")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        CapAnimFrames.Run(); // mobile import caps on the new cropped folders

        var setFree = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free_cropped", "ga_set_free", false);
        var setFree2 = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free2_cropped", "ga_set_free2", false);
        if (setFree == null || setFree2 == null) return;

        var gaCtrl = BuildController($"{QuestDir}/GaSetFreeController.controller", setFree, setFree2);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireGaCorrect(gaCtrl);
        WireTaIncorrect();
        Debug.Log("[SuccessScenes] DONE");
    }

    // Plain state machine, first clip = default state; BearCutscene drives with animator.Play.
    static AnimatorController BuildController(string path, params AnimationClip[] clips)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var clip in clips)
        {
            var st = sm.AddState(clip.name);
            st.motion = clip;
            if (sm.defaultState == null || clip == clips[0]) sm.defaultState = st;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static Sprite FirstSprite(string dir) => AssetDatabase.FindAssets("t:Sprite", new[] { dir })
        .Select(AssetDatabase.GUIDToAssetPath)
        .OrderBy(p => p, System.StringComparer.Ordinal)
        .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
        .FirstOrDefault(s => s != null);

    static void SetStep(SerializedProperty step, string state)
    {
        step.FindPropertyRelative("state").stringValue = state;
        step.FindPropertyRelative("toWaypoint").intValue = -1;          // motion is baked into the frames
        step.FindPropertyRelative("timing").enumValueIndex = 1;         // PlayClip (once, real clip length)
        step.FindPropertyRelative("seconds").floatValue = 0f;
        step.FindPropertyRelative("speed").floatValue = 1f;
        step.FindPropertyRelative("speedEnd").floatValue = 0f;
        step.FindPropertyRelative("scale").floatValue = 1f;
        step.FindPropertyRelative("fadeSeconds").floatValue = 0f;
        step.FindPropertyRelative("moveDelay").floatValue = 0f;
    }

    static void SwapVisual(GameObject go, AnimatorController ctrl, string spriteDir)
    {
        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;
        var img = go.GetComponent<Image>();
        var first = FirstSprite(spriteDir);
        if (first != null) { img.sprite = first; img.SetNativeSize(); }
    }

    static void WireGaCorrect(AnimatorController gaCtrl)
    {
        var scene = EditorSceneManager.OpenScene(GaCorrectPath);
        var cutscene = Object.FindObjectOfType<BearCutscene>(true);
        if (cutscene == null) { Debug.LogError("[SuccessScenes] no BearCutscene in Success_ga_correct"); return; }

        SwapVisual(cutscene.gameObject, gaCtrl, $"{QuestDir}/quest_ga/ga_set_free_cropped");

        var so = new SerializedObject(cutscene);
        var seq = so.FindProperty("sequence");
        seq.arraySize = 2;
        SetStep(seq.GetArrayElementAtIndex(0), "ga_set_free");
        SetStep(seq.GetArrayElementAtIndex(1), "ga_set_free2");
        so.FindProperty("alsoFade").arraySize = 0; // held the Thow's CanvasGroup
        so.ApplyModifiedPropertiesWithoutUndo();
        // endFadeOutDuration / owlEpilogue / nextScene=reference_forest / resumeForestAtBeat2 kept as-is

        var thow = cutscene.transform.parent != null ? cutscene.transform.parent.Find("Thow") : null;
        if (thow != null) Object.DestroyImmediate(thow.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SuccessScenes] Success_ga_correct wired: ga_set_free -> ga_set_free2, Thow removed");
    }

    static void WireTaIncorrect()
    {
        var scene = EditorSceneManager.OpenScene(TaIncorrectPath);
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[SuccessScenes] no Canvas in Success_ta_incorrect"); return; }

        UpgradeCutSceneGaParity.WireThowIncorrectScene(canvas.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SuccessScenes] Success_ta_incorrect wired: Thow + stone crow + one-star no-owl retry return");
    }
}
