using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot builder: turns the two boss PNGs into UI prefabs and duplicates the
// CutScene_bear word-assembly mechanic (stones + slots + result pages) into a
// self-contained prefab, then drops all three into the Boss scene for hand layout.
public static class BuildBossAssets
{
    const string SpiritPng = "Assets/Art/boss/boss_spirit_transparent.png";
    const string HealthPng = "Assets/Art/boss/boss_health_bar_transparent.png";
    const string PrefabDir = "Assets/Prefabs/Boss";
    const string SpiritPrefab = PrefabDir + "/boss_spirit.prefab";
    const string HealthPrefab = PrefabDir + "/boss_health_bar.prefab";
    const string WordAsmPrefab = PrefabDir + "/WordAssembly.prefab";
    const string BearScene = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string BossScene = "Assets/Scenes/region 1/Boss.unity";

    // Top-level children of CutScene_bear's Canvas that make up the pure mechanic.
    static readonly HashSet<string> MechanicKeep = new HashSet<string>
    {
        "magic_stone", "inputSlot1", "inputSlot2", "book_craft", "book_craft_pa", "book_craft_ga"
    };

    [MenuItem("Tools/Boss/Build Boss Assets")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
        }

        // --- Phase A: PNGs -> Single sprite (they came in as auto-sliced/empty Multiple) ---
        MakeSingleSprite(SpiritPng);
        MakeSingleSprite(HealthPng);
        AssetDatabase.Refresh();

        var spiritSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpiritPng);
        var healthSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HealthPng);
        if (spiritSprite == null || healthSprite == null)
        {
            Debug.LogError($"[BuildBossAssets] sprite load failed: spirit={spiritSprite} health={healthSprite}");
            return;
        }

        // --- Phase B: UI Image prefabs ---
        MakeImagePrefab("boss_spirit", spiritSprite, SpiritPrefab);
        MakeImagePrefab("boss_health_bar", healthSprite, HealthPrefab);

        // --- Phase C: word-assembly mechanic prefab from CutScene_bear ---
        EditorSceneManager.OpenScene(BearScene, OpenSceneMode.Single);
        GameObject canvas = null;
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Canvas") { canvas = go; break; }
        if (canvas == null) { Debug.LogError("[BuildBossAssets] Canvas root not found in CutScene_bear"); return; }

        var dup = Object.Instantiate(canvas);
        dup.name = "WordAssembly";
        // Strip everything but the mechanic children.
        var toKill = new List<GameObject>();
        foreach (Transform child in dup.transform)
            if (!MechanicKeep.Contains(child.name)) toKill.Add(child.gameObject);
        foreach (var go in toKill) Object.DestroyImmediate(go);

        PrefabUtility.SaveAsPrefabAsset(dup, WordAsmPrefab);
        Object.DestroyImmediate(dup);

        // --- Phase D: drop all three prefabs into the Boss scene ---
        var boss = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);

        var uiRoot = new GameObject("BossUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager_MoveToScene(uiRoot, boss);
        var canv = uiRoot.GetComponent<Canvas>();
        canv.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        InstantiatePrefabUnder(SpiritPrefab, uiRoot.transform);
        InstantiatePrefabUnder(HealthPrefab, uiRoot.transform);

        var wordAsm = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(WordAsmPrefab), boss);
        wordAsm.name = "WordAssembly";

        EditorSceneManager.MarkSceneDirty(boss);
        EditorSceneManager.SaveScene(boss);
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildBossAssets] DONE: boss_spirit, boss_health_bar, WordAssembly placed in Boss.unity");
    }

    static void MakeSingleSprite(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError($"[BuildBossAssets] no importer at {path}"); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
    }

    static void MakeImagePrefab(string name, Sprite sprite, string path)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.SetNativeSize();
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void InstantiatePrefabUnder(string prefabPath, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var rt = inst.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = Vector2.zero;
    }

    static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
