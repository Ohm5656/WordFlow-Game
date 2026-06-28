using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds two separate hand prefabs from the split Thow frames (Thow_left / Thow_right):
// each gets a clip + a single-state controller + an Image+Animator prefab that auto-plays.
// Then (if Success_pa is open) removes the old single Thow and drops the two hands in, apart.
public static class BuildThowHands
{
    const string Dir = "Assets/Art/quest_map/bear";
    const float Fps = 30f;
    const int MaxSize = 512;

    // (sourceFramesFolder, asset base name, placed world X in Success_pa)
    static readonly (string folder, string name, float worldX)[] Hands =
    {
        ("Thow_left",  "ThowLeft",  1400f),
        ("Thow_right", "ThowRight", 2440f),
    };

    [MenuItem("Tools/Bear/Build Thow Hands + Place")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var builtPrefabs = new System.Collections.Generic.Dictionary<string, GameObject>();

        foreach (var (folder, name, worldX) in Hands)
        {
            string dir = $"{Dir}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogError($"[ThowHands] missing {dir}"); continue; }

            string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png"))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();
            foreach (var p in paths) ApplySpriteImport(p);
            AssetDatabase.Refresh();

            var sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
            if (sprites.Length == 0) { Debug.LogError($"[ThowHands] no sprites in {dir}"); continue; }

            // clip
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { type = typeof(Image), path = "", propertyName = "m_Sprite" }, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = $"{Dir}/Animations/{name}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);

            // controller (single default state)
            string ctrlPath = $"{Dir}/{name}Controller.controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var st = ctrl.layers[0].stateMachine.AddState(name);
            st.motion = clip;
            ctrl.layers[0].stateMachine.defaultState = st;
            EditorUtility.SetDirty(ctrl);

            // prefab
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
            var img = go.GetComponent<Image>();
            img.sprite = sprites[0];
            img.SetNativeSize();
            go.GetComponent<Animator>().runtimeAnimatorController = ctrl;
            string prefabPath = $"{Dir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            builtPrefabs[name] = prefab;
            Debug.Log($"[ThowHands] built {prefabPath} ({sprites.Length} frames)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // place in Success_pa
        if (EditorSceneManager.GetActiveScene().name == "Success_pa")
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform oldThow = canvas.transform.Find("Thow");
                if (oldThow != null) Object.DestroyImmediate(oldThow.gameObject);

                foreach (var (folder, name, worldX) in Hands)
                {
                    if (!builtPrefabs.TryGetValue(name, out var prefab) || prefab == null) continue;
                    if (canvas.transform.Find(name) != null) continue;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                    inst.name = name;
                    inst.transform.position = new Vector3(worldX, 700f, 0f);
                }
                Debug.Log("[ThowHands] placed ThowLeft + ThowRight in Success_pa (reposition as needed)");
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("[ThowHands] DONE");
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
        if (ti.textureCompression != TextureImporterCompression.Compressed) { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
        if (ti.maxTextureSize != MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }
}
