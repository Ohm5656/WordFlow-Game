using System.Linq;
using UnityEditor;
using UnityEngine;

// One-shot: cap import settings on full-screen anim frame folders so they don't blow up VRAM.
// These frames fill the canvas (faint full-frame overlays / motion spanning the screen), so
// cropping gives nothing — the only lever is maxTextureSize + compression. Mirrors
// BearAnimationBuilder.ApplySpriteImport (512 cap). Run via Tools/Optimize/Cap Anim Frames 512.
public static class CapAnimFrames
{
    const int MaxSize = 512;

    static readonly string[] Folders =
    {
        "Assets/Art/quest_map/owl",
        "Assets/Art/quest_map/owl_hello",
        "Assets/Art/quest_map/bear/Thow_cropped",
        "Assets/Art/quest_map/quest_2/eye_cropped",
        "Assets/Art/quest_map/quest_ga/ga_set_free_cropped",
        "Assets/Art/quest_map/quest_ga/ga_set_free2_cropped",
        "Assets/Art/quest_map/quest_ga/ga_fly_cropped",
        "Assets/Art/quest_map/quest_ga/ga_stone_cropped",
        "Assets/Art/quest_map/ga_left_cropped",
        "Assets/Art/quest_map/ga_right_cropped",
    };

    [MenuItem("Tools/Optimize/Cap Anim Frames 512")]
    public static void Run()
    {
        int changed = 0, total = 0;
        foreach (var dir in Folders)
        {
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning($"[CapAnim] missing {dir}"); continue; }
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png")).ToArray();
            foreach (var p in paths) { total++; if (Apply(p)) changed++; }
            Debug.Log($"[CapAnim] {dir}: {paths.Length} frames");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[CapAnim] DONE — reimported {changed}/{total} frames at {MaxSize} cap");
    }

    static bool Apply(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return false;
        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
        if (ti.textureCompression != TextureImporterCompression.Compressed)
        { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
        if (ti.maxTextureSize != MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }
        if (dirty) ti.SaveAndReimport();
        return dirty;
    }
}
