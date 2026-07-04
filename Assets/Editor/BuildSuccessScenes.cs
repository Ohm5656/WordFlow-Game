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
//   Success_ta_incorrect: leftover ga_left/ga_right + SuccessGaReturn removed; Bear -> eye
//   (plays once, fades out), then the owl epilogue (copied from Success_ga_correct, retry line),
//   then back to CutScene_ga with the puzzle retry flags (BearCutscene.setPuzzleRetryFlags).
// Scene edits are atomic per scene + SaveScene (domain-reload safety).
public static class BuildSuccessScenes
{
    const string QuestDir = "Assets/Art/quest_map";
    const string GaCorrectPath = "Assets/Scenes/region 1/Success_ga_correct.unity";
    const string TaIncorrectPath = "Assets/Scenes/region 1/Success_ta_incorrect.unity";
    const string TempEpiloguePrefab = "Assets/Art/quest_map/__TempOwlEpilogue.prefab";

    [MenuItem("Tools/Success/Build Ga+Ta Success Scenes")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        CapAnimFrames.Run(); // mobile import caps on the new cropped folders

        var setFree = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free_cropped", "ga_set_free", false);
        var setFree2 = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free2_cropped", "ga_set_free2", false);
        var eye = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_2/eye_cropped", "eye", false);
        if (setFree == null || setFree2 == null || eye == null) return;

        var gaCtrl = BuildController($"{QuestDir}/GaSetFreeController.controller", setFree, setFree2);
        var eyeCtrl = BuildController($"{QuestDir}/EyeController.controller", eye);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireGaCorrect(gaCtrl);
        WireTaIncorrect(eyeCtrl);
        AssetDatabase.DeleteAsset(TempEpiloguePrefab);
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

        // Snapshot the owl epilogue (SuccessPaOwlEpilogue + talking owl child + TtsApiClient) so
        // WireTaIncorrect can clone the identical setup. Scene-external refs (owlHello) go null
        // in the asset, which is exactly what the ta scene wants (it has no hello_sprite).
        var epilogue = Object.FindObjectOfType<SuccessPaOwlEpilogue>(true);
        if (epilogue != null) PrefabUtility.SaveAsPrefabAsset(epilogue.gameObject, TempEpiloguePrefab);
        else Debug.LogWarning("[SuccessScenes] no SuccessPaOwlEpilogue in Success_ga_correct");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SuccessScenes] Success_ga_correct wired: ga_set_free -> ga_set_free2, Thow removed");
    }

    static void WireTaIncorrect(AnimatorController eyeCtrl)
    {
        var scene = EditorSceneManager.OpenScene(TaIncorrectPath);
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[SuccessScenes] no Canvas in Success_ta_incorrect"); return; }

        // Leftovers from the Success_ga copy this scene started as
        foreach (var name in new[] { "ga_left", "ga_right" })
        {
            var t = canvas.transform.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        var oldReturn = Object.FindObjectOfType<SuccessGaReturn>(true);
        if (oldReturn != null) Object.DestroyImmediate(oldReturn); // BearCutscene owns the return now (no timer race)

        var bear = canvas.transform.Find("Bear");
        if (bear == null) { Debug.LogError("[SuccessScenes] no Bear in Success_ta_incorrect"); return; }
        SwapVisual(bear.gameObject, eyeCtrl, $"{QuestDir}/quest_2/eye_cropped");

        // Owl epilogue cloned from Success_ga_correct (talking owl + TtsApiClient ride along)
        SuccessPaOwlEpilogue epilogue = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TempEpiloguePrefab);
        if (prefab != null)
        {
            var existing = canvas.transform.Find("OwlEpilogue");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = Object.Instantiate(prefab); // plain clone, no prefab link
            go.name = "OwlEpilogue";
            go.transform.SetParent(canvas.transform, false);
            epilogue = go.GetComponent<SuccessPaOwlEpilogue>();
            var eso = new SerializedObject(epilogue);
            eso.FindProperty("phrase1LineId").stringValue = "kaa_intro_owl_2"; // "ลองเสกคำว่า 'กา' ดูสิจ๊ะ..."
            eso.FindProperty("phrase2LineId").stringValue = "";
            eso.FindProperty("phrase3LineId").stringValue = "";
            eso.FindProperty("phrase1Clip").objectReferenceValue = null; // was the paa outro wav
            eso.FindProperty("phrase2Clip").objectReferenceValue = null;
            eso.FindProperty("phrase3Clip").objectReferenceValue = null;
            eso.FindProperty("owlHello").objectReferenceValue = null; // no hello_sprite in this scene
            eso.ApplyModifiedPropertiesWithoutUndo();
        }

        var cutscene = bear.GetComponent<BearCutscene>();
        if (cutscene == null) cutscene = bear.gameObject.AddComponent<BearCutscene>();
        var so = new SerializedObject(cutscene);
        so.FindProperty("startWaypoint").intValue = -1;   // play where the designer placed it
        so.FindProperty("fadeInDuration").floatValue = 0.4f;
        var seq = so.FindProperty("sequence");
        seq.arraySize = 1;
        SetStep(seq.GetArrayElementAtIndex(0), "eye");
        so.FindProperty("alsoFade").arraySize = 0;
        so.FindProperty("endFadeOutDuration").floatValue = 0.6f;
        so.FindProperty("nextScene").stringValue = "CutScene_ga";
        so.FindProperty("resumeForestAtBeat2").boolValue = false;
        so.FindProperty("setPuzzleRetryFlags").boolValue = true; // wrong word: reassemble in retry mode
        so.FindProperty("owlEpilogue").objectReferenceValue = epilogue;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SuccessScenes] Success_ta_incorrect wired: eye + owl epilogue + retry return to CutScene_ga");
    }
}
