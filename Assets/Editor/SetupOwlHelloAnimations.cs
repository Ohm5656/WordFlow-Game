using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Setup/Setup Owl Hello — All Scenes
/// Places hello_sprite as a direct Canvas child and matches it to the talking owl's
/// world position and displayed size.
public static class SetupOwlHelloAnimations
{
    private const string HelloFolder = "Assets/Art/quest_map/owl_hello";

    [MenuItem("Tools/Setup/Setup Owl Hello — All Scenes")]
    private static void Run()
    {
        NormalizeHelloTextureImporters();
        Sprite[] frames = LoadFrames();
        if (frames.Length == 0) { Debug.LogError("[OwlHello] No sprites found in " + HelloFolder); return; }

        SetupCutSceneBear(frames);
        SetupSuccessPa(frames);

        Debug.Log($"[OwlHello] Done — {frames.Length} frames loaded.");
    }

    // ────────────────────────── CutScene_bear ──────────────────────────

    private static void SetupCutSceneBear(Sprite[] frames)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/region 1/CutScene_bear.unity", OpenSceneMode.Single);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[OwlHello] Canvas not found in CutScene_bear"); return; }

        var owlRoot = canvas.transform.Find("OwlRoot");
        if (owlRoot == null) { Debug.LogError("[OwlHello] OwlRoot not found in CutScene_bear/Canvas"); return; }
        var owlRect = owlRoot.Find("owl") as RectTransform;
        if (owlRect == null) { Debug.LogError("[OwlHello] Talking owl not found in CutScene_bear/Canvas/OwlRoot"); return; }

        // Clean up stale GOs — check both Canvas and OwlRoot
        RemoveStale(canvas.transform);
        RemoveStale(owlRoot);

        // Create hello_sprite directly under Canvas, matched to the talking owl.
        var helloGo = new GameObject("hello_sprite");
        helloGo.transform.SetParent(canvas.transform, false);
        helloGo.layer = LayerMask.NameToLayer("UI");
        var helloRect = helloGo.AddComponent<RectTransform>();
        MatchRect(helloRect, owlRect, canvas.transform as RectTransform);

        var img = helloGo.AddComponent<Image>();
        img.sprite = frames[0];
        img.raycastTarget = false;
        img.preserveAspect = false;

        // Last sibling = renders on top of everything in Canvas
        helloGo.transform.SetAsLastSibling();

        var seq = helloGo.AddComponent<OwlHelloSequence>();
        var seqSo = new SerializedObject(seq);
        seqSo.FindProperty("helloImage").objectReferenceValue = img;
        seqSo.FindProperty("matchTarget").objectReferenceValue = owlRect;
        seqSo.FindProperty("zoomTarget").objectReferenceValue = helloRect;
        seqSo.FindProperty("playbackSpeed").floatValue = 1f;
        seqSo.FindProperty("zoomScale").floatValue = 1f;
        SetSprites(seqSo, frames);
        seqSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire OwlGreetingCutscene.owlHello
        var greeting = owlRoot.GetComponent<OwlGreetingCutscene>();
        if (greeting != null)
        {
            var greetSo = new SerializedObject(greeting);
            greetSo.FindProperty("owlHello").objectReferenceValue = seq;

            // Clear legacy greetingVoiceClip (baked clip that overrides TTS)
            var voiceClipProp = greetSo.FindProperty("greetingVoiceClip");
            if (voiceClipProp != null) voiceClipProp.objectReferenceValue = null;

            var lineIdProp = greetSo.FindProperty("greetingLineId");
            if (lineIdProp != null) lineIdProp.stringValue = "";

            greetSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[OwlHello] CutScene_bear: wired + cleared legacy greetingVoiceClip");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[OwlHello] CutScene_bear saved.");
    }

    // ────────────────────────── Success_pa ──────────────────────────

    private static void SetupSuccessPa(Sprite[] frames)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/region 1/Success_pa.unity", OpenSceneMode.Single);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[OwlHello] Canvas not found in Success_pa"); return; }

        var owlEpilogue = canvas.transform.Find("OwlEpilogue");
        if (owlEpilogue == null) { Debug.LogError("[OwlHello] OwlEpilogue not found in Success_pa/Canvas"); return; }
        var owlRect = owlEpilogue.Find("owl") as RectTransform;
        if (owlRect == null) { Debug.LogError("[OwlHello] Talking owl not found in Success_pa/Canvas/OwlEpilogue"); return; }

        // Clean up stale GOs from both Canvas and OwlEpilogue
        RemoveStale(canvas.transform);
        RemoveStale(owlEpilogue);

        // Create hello_sprite directly under Canvas, matched to the talking owl.
        var helloGo = new GameObject("hello_sprite");
        helloGo.transform.SetParent(canvas.transform, false);
        helloGo.layer = LayerMask.NameToLayer("UI");
        var helloRect = helloGo.AddComponent<RectTransform>();
        MatchRect(helloRect, owlRect, canvas.transform as RectTransform);

        var img = helloGo.AddComponent<Image>();
        img.sprite = frames[0];
        img.raycastTarget = false;
        img.preserveAspect = false;

        helloGo.transform.SetAsLastSibling();

        var seq = helloGo.AddComponent<OwlHelloSequence>();
        var seqSo = new SerializedObject(seq);
        seqSo.FindProperty("helloImage").objectReferenceValue = img;
        seqSo.FindProperty("matchTarget").objectReferenceValue = owlRect;
        seqSo.FindProperty("zoomTarget").objectReferenceValue = helloRect;
        seqSo.FindProperty("playbackSpeed").floatValue = 1f;
        seqSo.FindProperty("zoomScale").floatValue = 1f;
        SetSprites(seqSo, frames);
        seqSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire SuccessPaOwlEpilogue.owlHello
        var epilogue = owlEpilogue.GetComponent<SuccessPaOwlEpilogue>();
        if (epilogue != null)
        {
            var epSo = new SerializedObject(epilogue);
            epSo.FindProperty("owlHello").objectReferenceValue = seq;
            epSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[OwlHello] Success_pa: wired OwlHelloSequence");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[OwlHello] Success_pa saved.");
    }

    // ────────────────────────── Helpers ──────────────────────────

    private static void RemoveStale(Transform parent)
    {
        foreach (string name in new[] { "owl_hello", "hello_sprite" })
        {
            var s = parent.Find(name);
            if (s != null) Object.DestroyImmediate(s.gameObject);
        }
    }

    private static void MatchRect(RectTransform rt, RectTransform target, RectTransform canvas)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = target.pivot;
        rt.sizeDelta = target.rect.size;
        rt.position = target.position;
        rt.rotation = target.rotation;
        Vector3 parentScale = canvas != null ? canvas.lossyScale : Vector3.one;
        Vector3 targetScale = target.lossyScale;
        rt.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? targetScale.x : targetScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? targetScale.y : targetScale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? targetScale.z : targetScale.z / parentScale.z);
    }

    private static Sprite[] LoadFrames()
    {
        return Directory.GetFiles(HelloFolder, "*.png")
            .OrderBy(f => f)
            .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/')))
            .Where(s => s != null)
            .ToArray();
    }

    // These source frames were accidentally imported as Multiple sprites. Unity then returned
    // the first auto-sliced fragment (sometimes only 28x28 pixels) instead of the full owl frame.
    // Match the working owl animation: one full-frame sprite per PNG.
    private static void NormalizeHelloTextureImporters()
    {
        foreach (string file in Directory.GetFiles(HelloFolder, "*.png").OrderBy(f => f))
        {
            string path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static void SetSprites(SerializedObject so, Sprite[] frames)
    {
        var arr = so.FindProperty("frames");
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }
}
