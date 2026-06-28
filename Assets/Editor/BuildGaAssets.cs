using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the Success_ga art: ga_left / ga_right throw prefabs (like the Thow hands) and a
// bear_question clip + controller, all from the union-bbox cropped frame folders. Then, if
// Success_ga is the open scene, drops the two ga prefabs in, swaps the Bear to play bear_question
// (no walk), and adds SuccessGaReturn (fade out -> retry CutScene_bear). Run via the menu item.
public static class BuildGaAssets
{
    const string QuestDir = "Assets/Art/quest_map";
    const string BearDir = "Assets/Art/quest_map/bear";
    const float Fps = 30f;
    const int MaxSize = 512;

    [MenuItem("Tools/Bear/Build Ga Assets + Place")]
    public static void Build()
    {
        AssetDatabase.Refresh();

        // ga_left / ga_right: clip + single-state controller + Image prefab (auto-play)
        var gaLeft = BuildSpriteAnim($"{QuestDir}/ga_left_cropped", "ga_left", QuestDir, true);
        var gaRight = BuildSpriteAnim($"{QuestDir}/ga_right_cropped", "ga_right", QuestDir, true);

        // bear_question: clip + single-state controller only (the existing Bear prefab plays it)
        var bearQ = BuildSpriteAnim($"{BearDir}/bear_question_cropped", "bear_question", BearDir, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (EditorSceneManager.GetActiveScene().name == "Success_ga")
        {
            PlaceInSuccessGa(gaLeft.prefab, gaRight.prefab, bearQ.controller);
        }
        else
        {
            Debug.LogWarning("[GaAssets] Success_ga not open — built assets only, no scene placement.");
        }

        Debug.Log("[GaAssets] DONE");
    }

    struct Built { public AnimationClip clip; public AnimatorController controller; public GameObject prefab; }

    // Imports the cropped frames, builds a sprite clip bound to Image.m_Sprite, a single-state
    // controller that auto-plays it, and (if makePrefab) an Image+Animator prefab.
    static Built BuildSpriteAnim(string srcDir, string name, string outDir, bool makePrefab)
    {
        var result = new Built();
        if (!AssetDatabase.IsValidFolder(srcDir)) { Debug.LogError($"[GaAssets] missing {srcDir}"); return result; }

        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png"))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToArray();
        foreach (var p in paths) ApplySpriteImport(p);
        AssetDatabase.Refresh();

        var sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        if (sprites.Length == 0) { Debug.LogError($"[GaAssets] no sprites in {srcDir}"); return result; }

        var clip = new AnimationClip { frameRate = Fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { type = typeof(Image), path = "", propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false; // play through once, then "animations finished"
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string animDir = $"{outDir}/Animations";
        if (!AssetDatabase.IsValidFolder(animDir)) AssetDatabase.CreateFolder(outDir, "Animations");
        string clipPath = $"{animDir}/{name}.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        result.clip = clip;

        string ctrlPath = $"{outDir}/{name}Controller.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var st = ctrl.layers[0].stateMachine.AddState(name);
        st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;
        EditorUtility.SetDirty(ctrl);
        result.controller = ctrl;

        if (makePrefab)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
            var img = go.GetComponent<Image>();
            img.sprite = sprites[0];
            img.SetNativeSize();
            go.GetComponent<Animator>().runtimeAnimatorController = ctrl;
            string prefabPath = $"{outDir}/{name}.prefab";
            result.prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
        }

        Debug.Log($"[GaAssets] built {name} ({sprites.Length} frames, {sprites.Length / Fps:0.00}s)");
        return result;
    }

    static void PlaceInSuccessGa(GameObject gaLeftPrefab, GameObject gaRightPrefab, AnimatorController bearQuestion)
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[GaAssets] no Canvas in Success_ga"); return; }

        // ga_left / ga_right placeholders — reposition to taste
        PlaceGa(canvas.transform, gaLeftPrefab, "ga_left", new Vector3(1400f, 900f, 0f));
        PlaceGa(canvas.transform, gaRightPrefab, "ga_right", new Vector3(2440f, 900f, 0f));

        // Bear: play bear_question, no walking / owl trigger
        var bear = canvas.transform.Find("Bear");
        if (bear != null)
        {
            var cutscene = bear.GetComponent("BearCutscene") as MonoBehaviour;
            if (cutscene != null) Object.DestroyImmediate(cutscene);
            var animator = bear.GetComponent<Animator>();
            if (animator != null && bearQuestion != null) animator.runtimeAnimatorController = bearQuestion;
        }
        else Debug.LogWarning("[GaAssets] no Bear under Canvas");

        // fade-out -> retry CutScene_bear
        if (canvas.GetComponent<SuccessGaReturn>() == null) canvas.AddComponent<SuccessGaReturn>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[GaAssets] placed ga_left/ga_right, set Bear=bear_question, added SuccessGaReturn, saved Success_ga");
    }

    static void PlaceGa(Transform parent, GameObject prefab, string name, Vector3 worldPos)
    {
        if (prefab == null) { Debug.LogWarning($"[GaAssets] no prefab for {name}"); return; }
        var existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.name = name;
        inst.transform.position = worldPos;
    }

    static void ApplySpriteImport(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
        if (ti.textureCompression != TextureImporterCompression.Compressed)
        { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
        if (ti.maxTextureSize != MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }
}
