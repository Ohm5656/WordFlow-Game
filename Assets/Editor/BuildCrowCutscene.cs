using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot: builds the CutScene_ga crow entrance (replaces the bear intro).
//   1. Caps import settings on the anim frame folders (CapAnimFrames.Run).
//   2. Builds the looping ga_fly flight clip + the one-shot ga_stone petrify clip (frames
//      0332-0393 / 0394-0452 of ONE continuous sequence, cropped with a shared union bbox so
//      the switch is seamless; bound to Image.m_Sprite like BuildGaAssets), a 2-state
//      CrowController, and a Crow.prefab (Image + Animator + CanvasGroup + CrowEntranceCutscene).
//   3. In CutScene_ga: deletes the Bear prefab instance, drops the Crow in at the bear's
//      sibling slot, repoints OwlGreetingCutscene at the crow + the kaa_intro voice lines,
//      wires the gameplay TTS line ids into every MagicStonePuzzleController, saves scene.
// Scene edits are atomic in this one script + SaveScene (domain-reload safety).
public static class BuildCrowCutscene
{
    const string QuestDir = "Assets/Art/quest_map";
    const string GaDir = "Assets/Art/quest_map/quest_ga";
    const string ScenePath = "Assets/Scenes/region 1/CutScene_ga.unity";
    const float Fps = 30f;

    // Stale retry flags (set by Success_ta_incorrect / crow-scene returns during testing) make
    // the entrance skip straight to the puzzle — clear them to see the full flight again.
    [MenuItem("Tools/Crow/Clear Puzzle Retry Flags")]
    public static void ClearRetryFlags()
    {
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterCrow");
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterAlt");
        PlayerPrefs.Save();
        Debug.Log("[CrowCutscene] cleared puzzle retry PlayerPrefs");
    }

    [MenuItem("Tools/Crow/Build CutScene_ga")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        CapAnimFrames.Run(); // ensure all frame folders are Sprite-imported + capped for mobile

        // ga_fly loops while the crow slides between waypoints; ga_stone plays once and holds
        var fly = BuildClip($"{GaDir}/ga_fly_cropped", "ga_fly", true);
        var stone = BuildClip($"{GaDir}/ga_stone_cropped", "ga_stone", false);
        if (fly == null || stone == null) return;

        var ctrl = BuildController(fly, stone);
        var prefab = BuildPrefab(ctrl);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireScene(prefab);
        Debug.Log("[CrowCutscene] DONE");
    }

    // Sprite clip bound to Image.m_Sprite (UGUI), one keyframe per frame PNG.
    static AnimationClip BuildClip(string srcDir, string name, bool loop)
    {
        if (!AssetDatabase.IsValidFolder(srcDir)) { Debug.LogError($"[CrowCutscene] missing {srcDir}"); return null; }

        var sprites = AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png"))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null)
            .ToArray();
        if (sprites.Length == 0) { Debug.LogError($"[CrowCutscene] no sprites in {srcDir}"); return null; }

        var clip = new AnimationClip { frameRate = Fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { type = typeof(Image), path = "", propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string animDir = $"{QuestDir}/Animations";
        if (!AssetDatabase.IsValidFolder(animDir)) AssetDatabase.CreateFolder(QuestDir, "Animations");
        string clipPath = $"{animDir}/{name}.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        Debug.Log($"[CrowCutscene] clip {name}: {sprites.Length} frames, loop={loop}");
        return clip;
    }

    // One controller, two states — CrowEntranceCutscene switches with animator.Play(stateName).
    static AnimatorController BuildController(AnimationClip fly, AnimationClip stone)
    {
        string path = $"{QuestDir}/CrowController.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var stFly = sm.AddState("ga_fly"); stFly.motion = fly;
        var stStone = sm.AddState("ga_stone"); stStone.motion = stone;
        sm.defaultState = stFly;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static GameObject BuildPrefab(AnimatorController ctrl)
    {
        var firstFrame = AssetDatabase.LoadAssetAtPath<Sprite>($"{GaDir}/ga_fly_cropped/0332.png");
        if (firstFrame == null) Debug.LogWarning("[CrowCutscene] first fly frame 0332.png not found");

        var go = new GameObject("Crow", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Animator), typeof(CanvasGroup), typeof(CrowEntranceCutscene));
        var img = go.GetComponent<Image>();
        img.sprite = firstFrame;
        if (firstFrame != null) img.SetNativeSize();
        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;

        string prefabPath = $"{QuestDir}/Crow.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void WireScene(GameObject crowPrefab)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        var canvas = GameObject.Find("Canvas");
        var waypoints = GameObject.Find("waypoints");
        if (canvas == null || waypoints == null)
        { Debug.LogError("[CrowCutscene] Canvas or waypoints missing in CutScene_ga"); return; }

        // Bear out, Crow in (same sibling slot so it draws at the bear's depth)
        int siblingIndex = 1;
        var bear = Object.FindObjectOfType<BearCutscene>(true);
        if (bear != null)
        {
            siblingIndex = bear.transform.GetSiblingIndex();
            Object.DestroyImmediate(bear.gameObject);
        }
        var existing = canvas.transform.Find("Crow");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var crow = (GameObject)PrefabUtility.InstantiatePrefab(crowPrefab, canvas.transform);
        crow.name = "Crow";
        crow.transform.SetSiblingIndex(siblingIndex);

        var owl = Object.FindObjectOfType<OwlGreetingCutscene>(true);
        var cutscene = crow.GetComponent<CrowEntranceCutscene>();
        var cso = new SerializedObject(cutscene);
        cso.FindProperty("path").objectReferenceValue = waypoints.transform;
        cso.FindProperty("owlGreeting").objectReferenceValue = owl;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // start at wp_0 (world position, same convention as the runtime script)
        var wp0 = waypoints.transform.Find("wp_0");
        if (wp0 != null) crow.transform.position = wp0.position;

        // Owl: kaa_intro lines (already registered in the backend tts_lines.json), focus on the crow
        if (owl != null)
        {
            var oso = new SerializedObject(owl);
            oso.FindProperty("greetingPhrase1LineId").stringValue = "kaa_intro_owl_1";
            oso.FindProperty("greetingPhrase2LineId").stringValue = "";
            oso.FindProperty("greetingPhrase3LineId").stringValue = "";
            oso.FindProperty("greetingPhrase1Clip").objectReferenceValue = null;
            oso.FindProperty("greetingPhrase2Clip").objectReferenceValue = null;
            oso.FindProperty("greetingPhrase3Clip").objectReferenceValue = null;
            oso.FindProperty("bearFocusLookLineId").stringValue = "kaa_intro_owl_2";
            oso.FindProperty("bearFocusLookVoiceClip").objectReferenceValue = null; // old paa clip
            oso.FindProperty("bearRoot").objectReferenceValue = crow.GetComponent<RectTransform>();
            oso.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[CrowCutscene] no OwlGreetingCutscene in scene");

        // Word-build audio: gameplay TTS line ids on every puzzle controller copy
        // (existing wav clips stay assigned as offline fallback)
        int wired = WireGameplayTts.WireLoadedScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CrowCutscene] scene wired: Bear->Crow, kaa_intro lines, gameplay TTS on {wired} controllers, saved");
    }
}
