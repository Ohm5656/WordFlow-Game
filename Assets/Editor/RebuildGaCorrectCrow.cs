using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot: replaces the Bear prefab instance in Success_ga_correct with a plain Crow Image
// driven by CrowSetFreeCutscene (ga_set_free shatter -> ga_set_free2 free, owl praise ->
// reference_forest). Fixes the "nothing plays" bug: the old BearCutscene skipped the whole
// celebration whenever a puzzle retry flag was left set; the celebration must always play.
// Atomic scene edit + SaveScene (domain-reload safety).
public static class RebuildGaCorrectCrow
{
    const string QuestDir = "Assets/Art/quest_map";
    const string ScenePath = "Assets/Scenes/region 1/Success_ga_correct.unity";
    const string ControllerPath = "Assets/Art/quest_map/GaSetFreeController.controller";

    [MenuItem("Tools/Success/Rebuild Ga Correct Crow")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        CapAnimFrames.Run();

        // Reuse the clips/controller built by BuildSuccessScenes; rebuild if missing.
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            var setFree = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free_cropped", "ga_set_free", false);
            var setFree2 = BuildCrowCutscene.BuildClip($"{QuestDir}/quest_ga/ga_set_free2_cropped", "ga_set_free2", false);
            if (setFree == null || setFree2 == null) return;
            AssetDatabase.DeleteAsset(ControllerPath);
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            var s0 = sm.AddState("ga_set_free"); s0.motion = setFree; sm.defaultState = s0;
            var s1 = sm.AddState("ga_set_free2"); s1.motion = setFree2;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[GaCrow] no Canvas"); return; }

        // Capture the bear's placed RectTransform (the "scene size" the crow must end at) then remove it.
        var bear = Object.FindObjectOfType<BearCutscene>(true);
        var epilogue = Object.FindObjectOfType<SuccessPaOwlEpilogue>(true);
        int sibling = 1;
        var parent = canvas.transform;
        RectTransform src = null;
        if (bear != null)
        {
            src = bear.transform as RectTransform;
            parent = bear.transform.parent;
            sibling = bear.transform.GetSiblingIndex();
        }

        // Snapshot values before destroying the source
        Vector2 anchorMin = src ? src.anchorMin : new Vector2(0.5f, 0.5f);
        Vector2 anchorMax = src ? src.anchorMax : new Vector2(0.5f, 0.5f);
        Vector2 pivot = src ? src.pivot : new Vector2(0.5f, 0.5f);
        Vector2 anchoredPos = src ? src.anchoredPosition : Vector2.zero;
        Vector2 sizeDelta = src ? src.sizeDelta : new Vector2(1618f, 1080f);
        Vector3 localScale = src ? src.localScale : Vector3.one;

        if (bear != null) Object.DestroyImmediate(bear.gameObject);
        var thow = parent.Find("Thow");
        if (thow != null) Object.DestroyImmediate(thow.gameObject);
        var stale = parent.Find("Crow");
        if (stale != null) Object.DestroyImmediate(stale.gameObject);

        var go = new GameObject("Crow", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Animator), typeof(CanvasGroup), typeof(CrowSetFreeCutscene));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta; rt.localScale = localScale;
        rt.SetSiblingIndex(sibling);

        var img = go.GetComponent<Image>();
        var first = AssetDatabase.FindAssets("t:Sprite", new[] { $"{QuestDir}/quest_ga/ga_set_free_cropped" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>).FirstOrDefault(s => s != null);
        img.sprite = first;
        img.raycastTarget = false;

        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;

        var so = new SerializedObject(go.GetComponent<CrowSetFreeCutscene>());
        so.FindProperty("owlEpilogue").objectReferenceValue = epilogue;
        so.FindProperty("nextScene").stringValue = "reference_forest";
        so.FindProperty("resumeForestAtBeat2").boolValue = true;
        so.FindProperty("expandStartScale").floatValue = 1f;    // start at scene size
        so.FindProperty("expandEndScale").floatValue = 3.5f;    // bloom well past the 16:9 screen
        so.FindProperty("fadeOutDuration").floatValue = 1f;     // gentle dissolve, no white flash
        so.FindProperty("clip2FadeInDuration").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GaCrow] DONE — Bear replaced with Crow (ga_set_free -> ga_set_free2), scene saved");
    }
}
