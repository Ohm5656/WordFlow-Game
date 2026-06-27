using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot builders for the region1 quest-area island objects.
//  - Build Island Object Prefabs: turns the 3 PNGs into simple SpriteRenderer prefabs.
//  - Build Quest Marker (Animated): rebuilds the "!" marker as Shadow + Mark, where the
//    Mark bobs up/down (looped) and the Shadow stays put.
public static class IslandObjectPrefabBuilder
{
    const string PrefabDir = "Assets/Prefabs/region1/object_on_island";
    const string AnimDir = "Assets/Animations/region1/quest";
    const float TargetHeight = 2.5f;   // world units tall (props in this map are ~1.5)
    const int SortingOrder = 5000;     // match the crop/prop sorting used in this scene

    struct Item
    {
        public string spritePath;
        public string objName;
        public Vector3 pos;
    }

    [MenuItem("Tools/Quest/Build Island Object Prefabs")]
    public static void Build()
    {
        var items = new[]
        {
            new Item { spritePath = "Assets/Art/region1/object_on_island/ChatGPT Image Jun 19, 2026, 12_54_56 AM.png", objName = "IslandObject_1", pos = new Vector3(14f, 9f, 0f) },
            new Item { spritePath = "Assets/Art/region1/object_on_island/ChatGPT Image Jun 19, 2026, 01_07_56 PM.png", objName = "IslandObject_2", pos = new Vector3(18f, 9f, 0f) },
            new Item { spritePath = "Assets/Art/region1/object_on_island/ChatGPT Image Jun 19, 2026, 01_08_14 PM.png", objName = "IslandObject_3", pos = new Vector3(22f, 9f, 0f) },
        };

        Directory.CreateDirectory(PrefabDir);
        AssetDatabase.Refresh();

        var parentGo = GameObject.Find("Objects");
        Transform parent = parentGo != null ? parentGo.transform : null;

        var log = new System.Text.StringBuilder("IslandObjectPrefabBuilder: ");
        int made = 0;

        foreach (var it in items)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(it.spritePath);
            if (sprite == null) { log.Append($"[MISSING SPRITE {it.spritePath}] || "); continue; }

            var go = new GameObject(it.objName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrder;

            float h = sprite.bounds.size.y;
            float s = h > 0.0001f ? TargetHeight / h : 1f;
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.position = it.pos;
            if (parent != null) go.transform.SetParent(parent, true);

            string prefabPath = $"{PrefabDir}/{it.objName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            log.Append($"{it.objName} -> {(prefab != null ? prefabPath : "FAILED")} (scale {s:0.000}) || ");
            made++;
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.Append($"DONE made={made} scene={scene.name}");
        Debug.Log(log.ToString());
    }

    // ---- Animated quest marker ("!" bobs, shadow stays) ----------------------------

    const string MarkerPngPath = "Assets/Art/region1/object_on_island/ChatGPT Image Jun 19, 2026, 12_54_56 AM.png";
    const string MarkerName = "QuestMarker_Exclamation";
    const float MarkerScale = 0.244f;   // keeps the "!" ~1 unit tall, consistent with the other props
    const float BobWorld = 0.28f;       // how far up the "!" rises, in world units
    const float BobPeriod = 1.8f;       // seconds per up+down cycle (higher = slower)
    const float ShadowMinScale = 0.78f; // shadow shrinks to this when the "!" is at the top
    const float ShadowMinAlpha = 0.55f; // shadow fades to this when the "!" is at the top

    [MenuItem("Tools/Quest/Build Quest Marker (Animated)")]
    public static void BuildAnimatedMarker()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(MarkerPngPath).OfType<Sprite>().ToList();
        if (sprites.Count < 2)
        {
            Debug.LogError($"QuestMarker: expected 2 sub-sprites in '{MarkerPngPath}', found {sprites.Count}. " +
                           "Make sure Sprite Mode = Multiple with the '!' and the shadow sliced.");
            return;
        }

        // Classify: the taller slice is the "!", the flatter/shorter one is the shadow.
        sprites.Sort((a, b) => b.rect.height.CompareTo(a.rect.height));
        Sprite markSprite = sprites[0];
        Sprite shadowSprite = sprites[1];

        float ppu = markSprite.pixelsPerUnit;
        var tex = markSprite.texture;
        Vector2 texCenter = new Vector2(tex.width / 2f, tex.height / 2f);

        // Local offset that reproduces each slice's position in the original image.
        System.Func<Sprite, Vector3> localOffset = sp =>
        {
            Vector2 pivotTex = sp.rect.position + sp.pivot; // pivot is px from the slice's bottom-left
            Vector2 d = pivotTex - texCenter;
            return new Vector3(d.x / ppu, d.y / ppu, 0f);
        };

        Vector3 markOffset = localOffset(markSprite);
        Vector3 shadowOffset = localOffset(shadowSprite);

        // Preserve where the existing instance sat in the scene (default (14,9,0) under Objects).
        Vector3 worldPos = new Vector3(14f, 9f, 0f);
        Transform parent = null;
        var existing = GameObject.Find(MarkerName);
        if (existing != null)
        {
            worldPos = existing.transform.position;
            parent = existing.transform.parent;
        }
        if (parent == null)
        {
            var objs = GameObject.Find("Objects");
            if (objs != null) parent = objs.transform;
        }

        // Build hierarchy.
        var root = new GameObject(MarkerName);
        root.transform.position = worldPos;
        root.transform.localScale = new Vector3(MarkerScale, MarkerScale, 1f);

        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(root.transform, false);
        shadow.transform.localPosition = shadowOffset;
        var shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = shadowSprite;
        shadowSr.sortingOrder = SortingOrder - 1;   // under the mark

        var mark = new GameObject("Mark");
        mark.transform.SetParent(root.transform, false);
        mark.transform.localPosition = markOffset;
        var markSr = mark.AddComponent<SpriteRenderer>();
        markSr.sprite = markSprite;
        markSr.sortingOrder = SortingOrder + 1;      // above the shadow

        // Animation clip: the "!" bobs up/down; the shadow shrinks + fades in sync
        // (smallest/faintest when the "!" is highest) so it reads as a floating marker.
        Directory.CreateDirectory(AnimDir);
        float ampLocal = BobWorld / MarkerScale;     // child is inside the scaled root
        float baseY = markOffset.y;

        // Smooth 3-key curve (start -> mid at the top of the bob -> back), eased.
        System.Func<float, float, float, AnimationCurve> tri = (v0, vMid, v1) =>
        {
            var c = new AnimationCurve(
                new Keyframe(0f, v0),
                new Keyframe(BobPeriod * 0.5f, vMid),
                new Keyframe(BobPeriod, v1));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        };

        var clip = new AnimationClip { name = "QuestMarker_Bob" };
        // Mark: rise then settle.
        clip.SetCurve("Mark", typeof(Transform), "localPosition.y", tri(baseY, baseY + ampLocal, baseY));
        // Shadow: shrink (x/y) while the "!" is up, keep z = 1, and fade alpha.
        clip.SetCurve("Shadow", typeof(Transform), "localScale.x", tri(1f, ShadowMinScale, 1f));
        clip.SetCurve("Shadow", typeof(Transform), "localScale.y", tri(1f, ShadowMinScale, 1f));
        clip.SetCurve("Shadow", typeof(Transform), "localScale.z",
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(BobPeriod, 1f)));
        clip.SetCurve("Shadow", typeof(SpriteRenderer), "m_Color.a", tri(1f, ShadowMinAlpha, 1f));

        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        string clipPath = $"{AnimDir}/QuestMarker_Bob.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);

        string controllerPath = $"{AnimDir}/{MarkerName}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        // Save prefab (overwrite the old single-sprite one) and reconnect the scene instance.
        Directory.CreateDirectory(PrefabDir);
        string prefabPath = $"{PrefabDir}/{MarkerName}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);

        if (existing != null && existing != root)
            Object.DestroyImmediate(existing);
        if (parent != null)
            root.transform.SetParent(parent, true);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"BuildAnimatedMarker: prefab={(prefab != null ? prefabPath : "FAILED")} || " +
                  $"mark={markSprite.name} ({markSprite.rect.width}x{markSprite.rect.height}) offset={markOffset} || " +
                  $"shadow={shadowSprite.name} ({shadowSprite.rect.width}x{shadowSprite.rect.height}) offset={shadowOffset} || " +
                  $"bobWorld={BobWorld} ampLocal={ampLocal:0.00} period={BobPeriod} pos={worldPos}");
    }
}
