using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// One-shot tool: turns the union-bbox-cropped character_run_* frame folders into
// 4 smooth looping run clips + a states-only controller + a UGUI Image prefab.
// Mirrors the proven bear pipeline (BearAnimationBuilder/BearPrefabBuilder).
// Frames were pre-cropped to ONE shared rect in PIL, so pivot + on-screen scale
// stay constant across directions => no jitter, no size-jump when swapping dir.
// A mover drives it via animator.Play("character_run_left") etc.
// Run via Tools/Character/Build Run Animations.
public static class CharacterRunBuilder
{
    const string CharDir = "Assets/Art/quest_map/character";
    const string OutDir = "Assets/Art/quest_map/character/Animations";
    const string ControllerPath = "Assets/Art/quest_map/character/CharacterRunController.controller";
    const string PrefabPath = "Assets/Art/quest_map/character/Character.prefab";
    const string DefaultState = "character_run_front";
    const float Fps = 30f;   // constant frame timing = smooth, no stutter
    const int MaxSize = 512; // mobile texture cap (ASTC ~cheap); bump if soft
    const float Ppu = 100f;  // fixed pixels-per-unit = same world height every direction
    static readonly Vector2 BoyPivot = new Vector2(0.480f, 0.082f); // boy feet anchor (frames pre-aligned to it)

    // cropped folder (under CharDir) -> clip name. all loop (run cycle).
    static readonly (string folder, string clip)[] Dirs =
    {
        ("character_run_front_cropped", "character_run_front"),
        ("character_run_left_cropped",  "character_run_left"),
        ("character_run_right_cropped", "character_run_right"),
        ("character_run_up_cropped",    "character_run_up"),
        // standing pose (front) — subtle 4-frame breathing loop, sampled slow
        ("character_stop_cropped",      "character_idle_front"),
    };
    const float IdleFps = 6f; // breathing loop pace for character_idle_front

    [MenuItem("Tools/Character/Build Run Animations")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        foreach (var (folder, clipName) in Dirs)
        {
            string dir = $"{CharDir}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir))
            { Debug.LogWarning($"[CharRun] missing {dir}, skipped"); continue; }

            string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png"))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0) { Debug.LogWarning($"[CharRun] no frames in {dir}"); continue; }

            foreach (var p in paths) ApplySpriteImport(p);
            AssetDatabase.Refresh();

            var sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                               .Where(s => s != null).ToArray();
            if (sprites.Length != paths.Length)
                Debug.LogWarning($"[CharRun] {clipName}: {paths.Length} frames but {sprites.Length} sprites");

            float fps = clipName.Contains("idle") ? IdleFps : Fps;
            var clip = new AnimationClip { frameRate = fps };
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

            // Bind BOTH Image (UGUI) and SpriteRenderer (world) sprite curves with the
            // same keys — the Animator drives whichever component the object actually has,
            // so one clip works under a Canvas AND in a world scene (reference_forest).
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { type = typeof(Image), path = "", propertyName = "m_Sprite" }, keys);
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" }, keys);

            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);

            string clipPath = $"{OutDir}/{clipName}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[CharRun] built {clipPath}  frames={sprites.Length} len={sprites.Length / fps:0.00}s");
        }

        BuildControllerAndPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharRun] DONE");
    }

    static void BuildControllerAndPrefab()
    {
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { OutDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".anim"))
            .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .Where(c => c != null).OrderBy(c => c.name).ToArray();
        if (clips.Length == 0) { Debug.LogError("[CharRun] no clips to wire"); return; }

        // states-only controller (no params); mover calls animator.Play(dir)
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState def = null;
        foreach (var c in clips)
        {
            var st = sm.AddState(c.name);
            st.motion = c;
            if (c.name == DefaultState) def = st;
        }
        if (def != null) sm.defaultState = def;
        EditorUtility.SetDirty(ctrl);

        var go = new GameObject("Character",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
        var img = go.GetComponent<Image>();
        var idle = clips.FirstOrDefault(c => c.name == DefaultState) ?? clips[0];
        var first = FirstSpriteOf(idle);
        if (first != null) { img.sprite = first; img.SetNativeSize(); }
        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[CharRun] controller+prefab: {clips.Length} states, default={DefaultState}");
    }

    static Sprite FirstSpriteOf(AnimationClip clip)
    {
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var k = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (k != null && k.Length > 0) return k[0].value as Sprite;
        }
        return null;
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
        // Force a fixed PPU so every direction renders at the same world height. Unity otherwise
        // rescales PPU when maxTextureSize downscales a frame, so different crop sizes per
        // direction (front 512 / up 687 tall) gave different on-screen sizes = size-jump on turn.
        if (Mathf.Abs(ti.spritePixelsPerUnit - Ppu) > 0.01f) { ti.spritePixelsPerUnit = Ppu; dirty = true; }

        // Custom pivot at the BOY's feet anchor (not the frame's bottom-centre): the frames
        // were aligned so the boy's feet sit at this exact normalized spot in EVERY direction,
        // so the boy stays put when the controller swaps direction (no turn jump), the owl+shadow
        // swing around it, and the feet sit on the transform (grounded).
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        if (settings.spriteAlignment != (int)SpriteAlignment.Custom ||
            (settings.spritePivot - BoyPivot).sqrMagnitude > 1e-6f)
        {
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = BoyPivot;
            ti.SetTextureSettings(settings);
            dirty = true;
        }
        if (dirty) ti.SaveAndReimport();
    }
}
