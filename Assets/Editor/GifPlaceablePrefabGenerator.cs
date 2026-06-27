using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GifPlaceablePrefabGenerator
{
    private const string OutputRoot = "Assets/Prefabs/GifPlaceables";
    private const string ReportPath = OutputRoot + "/_generation_report.txt";

    private static readonly string[] ObjectSourceRoots =
    {
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs_with_behavior",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters",
        "Assets/Gif/Super_Retro_Collection/Resources/Battlers",
        "Assets/Gif/Super_Retro_Collection/Resources/Animations",
        "Assets/Gif/Super_Retro_Collection/Resources/Hero",
        "Assets/Gif/Super_Retro_Collection/Resources/ARPG",
        "Assets/Gif/Super_Retro_Collection/Resources/Backgrounds",
    };

    private static readonly string[] TileOrPalettePathParts =
    {
        "/Environments/TilePalette/",
        "/Environments/TilePalette_updates/",
        "/Samples/",
        "/Ruccho/",
    };

    [MenuItem("Tools/NSC/GIF Placeables/Generate Object Prefabs")]
    public static void GenerateObjectPrefabs()
    {
        Generate(ObjectSourceRoots, includeTilesAndPalettes: false);
    }

    [MenuItem("Tools/NSC/GIF Placeables/Generate Every Sprite Prefab (Huge)")]
    public static void GenerateEverySpritePrefab()
    {
        Generate(new[] { "Assets/Gif" }, includeTilesAndPalettes: true);
    }

    private static void Generate(IReadOnlyList<string> roots, bool includeTilesAndPalettes)
    {
        EnsureFolder(OutputRoot);

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", roots.ToArray());
        var usedPrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoryCounts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int textureCount = 0;
        int spriteCount = 0;
        int prefabCount = 0;
        int skippedTextureCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in textureGuids)
            {
                string texturePath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (!includeTilesAndPalettes && IsTileOrPaletteAsset(texturePath))
                {
                    continue;
                }

                textureCount++;
                List<Sprite> sprites = LoadSprites(texturePath);
                if (sprites.Count == 0)
                {
                    skippedTextureCount++;
                    continue;
                }

                foreach (Sprite sprite in sprites)
                {
                    spriteCount++;
                    string prefabPath = BuildPrefabPath(texturePath, sprite, usedPrefabPaths);
                    EnsureFolder(Path.GetDirectoryName(prefabPath)?.Replace("\\", "/"));
                    CreatePrefab(sprite, texturePath, prefabPath);
                    prefabCount++;

                    string category = GetOutputCategory(texturePath);
                    categoryCounts[category] = categoryCounts.TryGetValue(category, out int count) ? count + 1 : 1;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        WriteReport(roots, includeTilesAndPalettes, textureCount, spriteCount, prefabCount, skippedTextureCount, categoryCounts);
        AssetDatabase.Refresh();

        Debug.Log($"GIF placeable prefabs generated: {prefabCount} prefabs in {OutputRoot}");
    }

    private static List<Sprite> LoadSprites(string texturePath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        return assets
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CreatePrefab(Sprite sprite, string sourcePath, string prefabPath)
    {
        var go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = GetSortingOrder(sourcePath);

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static string BuildPrefabPath(string sourcePath, Sprite sprite, HashSet<string> usedPrefabPaths)
    {
        string category = GetOutputCategory(sourcePath);
        string spriteName = string.IsNullOrWhiteSpace(sprite.name)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : sprite.name;

        string safeName = MakeSafePathPart(spriteName);
        string prefabPath = $"{OutputRoot}/{category}/{safeName}.prefab";

        if (usedPrefabPaths.Add(prefabPath))
        {
            return prefabPath;
        }

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId);
        prefabPath = $"{OutputRoot}/{category}/{safeName}_{Math.Abs(localId)}.prefab";
        usedPrefabPaths.Add(prefabPath);
        return prefabPath;
    }

    private static string GetOutputCategory(string sourcePath)
    {
        string relative = sourcePath.StartsWith("Assets/Gif/", StringComparison.OrdinalIgnoreCase)
            ? sourcePath.Substring("Assets/Gif/".Length)
            : sourcePath;

        string directory = Path.GetDirectoryName(relative)?.Replace("\\", "/") ?? "Unsorted";
        directory = directory
            .Replace("/Sprites", string.Empty)
            .Replace("Resources/", string.Empty)
            .Replace("Super_Retro_Collection/", "SuperRetro/")
            .Trim('/');

        return MakeSafeCategoryPath(directory);
    }

    private static int GetSortingOrder(string sourcePath)
    {
        string lower = sourcePath.ToLowerInvariant();
        if (lower.Contains("/backgrounds/"))
        {
            return -20;
        }

        if (lower.Contains("/characters/") || lower.Contains("/hero/") || lower.Contains("/arpg/") || lower.Contains("/battlers/"))
        {
            return 20;
        }

        if (lower.Contains("/prefabs/trees/") || lower.Contains("/prefabs/houses/"))
        {
            return 10;
        }

        return 5;
    }

    private static bool IsTileOrPaletteAsset(string path)
    {
        string normalized = NormalizeAssetPath(path);
        foreach (string part in TileOrPalettePathParts)
        {
            if (normalized.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteReport(
        IReadOnlyList<string> roots,
        bool includeTilesAndPalettes,
        int textureCount,
        int spriteCount,
        int prefabCount,
        int skippedTextureCount,
        IReadOnlyDictionary<string, int> categoryCounts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("GIF Placeable Prefab Generation Report");
        builder.AppendLine($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Output: {OutputRoot}");
        builder.AppendLine($"Include tile/palette sprites: {includeTilesAndPalettes}");
        builder.AppendLine("Source roots:");
        foreach (string root in roots)
        {
            builder.AppendLine($"- {root}");
        }

        builder.AppendLine();
        builder.AppendLine($"Textures scanned: {textureCount}");
        builder.AppendLine($"Sprites found: {spriteCount}");
        builder.AppendLine($"Prefabs generated: {prefabCount}");
        builder.AppendLine($"Textures skipped because no Sprite sub-assets were found: {skippedTextureCount}");
        builder.AppendLine();
        builder.AppendLine("Generated by category:");
        foreach (KeyValuePair<string, int> pair in categoryCounts)
        {
            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        }

        File.WriteAllText(ReportPath, builder.ToString());
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string normalized = folderPath.Replace("\\", "/").TrimEnd('/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string MakeSafeCategoryPath(string value)
    {
        return string.Join("/", value
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(MakeSafePathPart));
    }

    private static string MakeSafePathPart(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        string safe = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Unnamed" : safe;
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace("\\", "/");
    }
}
