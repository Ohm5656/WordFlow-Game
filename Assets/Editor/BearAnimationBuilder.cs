using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// One-shot tool: configures import settings on the cropped bear frames and
// builds one AnimationClip per action. Run via Tools/Bear/Build Animations.
public static class BearAnimationBuilder
{
    const string BearDir = "Assets/Art/quest_map/bear";
    const string OutDir = "Assets/Art/quest_map/bear/Animations";
    const float Fps = 30f; // render assumed 30fps; tweak per clip in Animation window if timing is off
    const int MaxSize = 512; // mobile texture cap; bump to 768 if cutscene bear looks soft

    // action folder (under BearDir) -> loop?
    static readonly (string folder, bool loop)[] Actions =
    {
        ("bear_jump_cropped",       false),
        ("bear_run2_cropped",       true),
        ("bear_run_back_cropped",   false),
        ("Thow_cropped",            false),
        ("bear-breathing_cropped",  true),
    };

    [MenuItem("Tools/Bear/Build Animations")]
    public static void Build()
    {
        AssetDatabase.Refresh();

        int clips = 0;
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        foreach (var (folder, loop) in Actions)
        {
            string dir = $"{BearDir}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogWarning($"[BearAnim] missing folder {dir}, skipped");
                continue;
            }

            // 1. import settings on every frame, collect sprites in numeric order
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                             .Where(p => p.EndsWith(".png"))
                             .OrderBy(p => p, System.StringComparer.Ordinal)
                             .ToArray();
            if (paths.Length == 0) { Debug.LogWarning($"[BearAnim] no frames in {dir}"); continue; }

            foreach (var p in paths) ApplySpriteImport(p);
            AssetDatabase.Refresh();

            var sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                               .Where(s => s != null).ToArray();
            if (sprites.Length != paths.Length)
                Debug.LogWarning($"[BearAnim] {folder}: {paths.Length} frames but {sprites.Length} sprites loaded");

            // 2. build clip — one sprite keyframe per frame at Fps
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };

            // Cutscene renders the bear as a UGUI Image, so bind the sprite curve to Image.
            var binding = new EditorCurveBinding
            { type = typeof(Image), path = "", propertyName = "m_Sprite" };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string name = folder.Replace("_cropped", "").Replace("-", "_");
            string clipPath = $"{OutDir}/{name}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            clips++;
            Debug.Log($"[BearAnim] built {clipPath}  frames={sprites.Length} loop={loop} len={sprites.Length / Fps:0.00}s");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BearAnim] DONE — {clips} clips in {OutDir}");
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
        // ponytail: 512 cap = mobile sweet spot (~100MB VRAM all clips). Bump to 768 if the
        // cutscene bear shows large enough to look soft.
        if (ti.maxTextureSize != MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }
}
