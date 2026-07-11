using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Setup Bear Intro
/// One-shot: imports the cropped owl frames cheaply (compressed, small), then builds the
/// bear-intro objects in reference_forest — a spotlight dark overlay + a corner owl UI +
/// the BearIntroSequence orchestrator — and wires it into QuestPathSequence.bearIntro.
/// Idempotent: re-run after tweaking anything; it rebuilds the two GOs and re-wires.
public static class SetupBearIntro
{
    const string ScenePath = "Assets/Scenes/region 1/reference_forest.unity";
    const string WowDir = "Assets/Art/quest_map/owl_wow_cropped";
    const string TalkDir = "Assets/Art/quest_map/owl_talk_cropped";
    const string OverlayMat = "Assets/Scenes/region 1/adventure/Shaders/NightDarkOverlay.mat";
    const string OverlaySprite = "Assets/Scenes/region 1/adventure/Shaders/NightOverlayWhite.png";
    const int OwlMaxSize = 256; // small corner element -> cheap on mobile

    [MenuItem("Tools/Quest/Setup Bear Intro")]
    public static void Run()
    {
        ImportOwlFrames(WowDir);
        ImportOwlFrames(TalkDir);
        AssetDatabase.Refresh();

        Sprite[] wow = LoadFrames(WowDir);
        Sprite[] talk = LoadFrames(TalkDir);
        if (wow.Length == 0 || talk.Length == 0)
        {
            Debug.LogError($"[BearIntro] missing owl frames (wow={wow.Length}, talk={talk.Length})");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var qps = Object.FindFirstObjectByType<QuestPathSequence>(FindObjectsInactive.Include);
        if (qps == null) { Debug.LogError("[BearIntro] QuestPathSequence not found in scene"); return; }

        var qpsSo = new SerializedObject(qps);
        var bearSprite = qpsSo.FindProperty("villager").objectReferenceValue as SpriteRenderer;
        var markSprite = qpsSo.FindProperty("villagerMarkerSprite").objectReferenceValue as SpriteRenderer;
        if (bearSprite == null) { Debug.LogError("[BearIntro] QuestPathSequence.villager (bear) not assigned"); return; }

        // --- fresh build: remove any previous intro GOs ---
        foreach (var name in new[] { "BearIntroSpotlight", "BearIntroCanvas", "BearIntro" })
        {
            var stale = GameObject.Find(name);
            if (stale != null) Object.DestroyImmediate(stale);
        }

        // --- spotlight dark overlay (world SpriteRenderer using NightOverlayCutout material) ---
        var overlayGo = new GameObject("BearIntroSpotlight");
        overlayGo.transform.position = new Vector3(18f, 10f, 0f);
        overlayGo.transform.localScale = new Vector3(48f, 30f, 1f);
        var osr = overlayGo.AddComponent<SpriteRenderer>();
        osr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OverlaySprite);
        osr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(OverlayMat);
        osr.sortingOrder = 31950; // above world sprites, below the night overlay (32000)
        osr.color = new Color(0.03f, 0.03f, 0.06f, 0f);

        // --- owl UI: its own screen-space canvas + a corner Image with a CanvasGroup ---
        var canvasGo = new GameObject("BearIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var owlGo = new GameObject("Owl", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        owlGo.transform.SetParent(canvasGo.transform, false);
        var owlRect = owlGo.GetComponent<RectTransform>();
        owlRect.anchorMin = new Vector2(0f, 0f);   // bottom-left corner
        owlRect.anchorMax = new Vector2(0f, 0f);
        owlRect.pivot = new Vector2(0f, 0f);
        owlRect.sizeDelta = new Vector2(460f, 372f); // owl aspect ~1.24
        owlRect.anchoredPosition = new Vector2(40f, 24f);
        var owlGroup = owlGo.GetComponent<CanvasGroup>();
        owlGroup.alpha = 1f; // visible in the scene for tuning; PlayIntro drives the runtime fade
        var owlImage = owlGo.GetComponent<Image>();
        owlImage.sprite = wow[0];
        owlImage.raycastTarget = false;
        owlImage.preserveAspect = true;

        // --- orchestrator ---
        var seq = overlayGo.AddComponent<BearIntroSequence>();
        var so = new SerializedObject(seq);
        so.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        so.FindProperty("zoomFocus").objectReferenceValue = bearSprite.transform;
        so.FindProperty("spotlightOverlay").objectReferenceValue = osr;
        var targets = so.FindProperty("spotlightTargets");
        targets.arraySize = markSprite != null ? 2 : 1;
        targets.GetArrayElementAtIndex(0).objectReferenceValue = bearSprite.transform;
        if (markSprite != null) targets.GetArrayElementAtIndex(1).objectReferenceValue = markSprite.transform;
        so.FindProperty("owlGroup").objectReferenceValue = owlGroup;
        so.FindProperty("owlImage").objectReferenceValue = owlImage;
        SetSprites(so.FindProperty("wowFrames"), wow);
        SetSprites(so.FindProperty("talkFrames"), talk);
        so.FindProperty("voiceClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/TTS/bear_village_help_owl_1.wav");
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- wire into QuestPathSequence ---
        qpsSo.FindProperty("bearIntro").objectReferenceValue = seq;
        qpsSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BearIntro] DONE — wow={wow.Length} talk={talk.Length}, wired into QuestPathSequence, scene saved");
    }

    static void SetSprites(SerializedProperty arr, Sprite[] frames)
    {
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    static Sprite[] LoadFrames(string dir)
    {
        return Directory.GetFiles(dir, "*.png")
            .OrderBy(f => f, System.StringComparer.Ordinal)
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
            .Where(s => s != null)
            .ToArray();
    }

    static void ImportOwlFrames(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (ti.textureCompression != TextureImporterCompression.Compressed)
            { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
            if (ti.maxTextureSize != OwlMaxSize) { ti.maxTextureSize = OwlMaxSize; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
    }
}
