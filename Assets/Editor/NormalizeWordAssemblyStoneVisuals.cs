using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes every currently shipped word-assembly board use the authored CutScene_bear
/// stone set.  Positions remain scene-specific; the sprite, rendering settings,
/// RectTransform size and scale are copied exactly from the bear reference.
/// </summary>
public static class NormalizeWordAssemblyStoneVisuals
{
    private const string BearScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    private const string GaScenePath = "Assets/Scenes/region 1/CutScene_ga.unity";
    private const string WordAssemblyPrefabPath = "Assets/Prefabs/Boss/WordAssembly.prefab";

    [MenuItem("Tools/Word Assembly/Normalize Stone Visuals")]
    public static void Normalize()
    {
        if (HasDirtyScenes())
        {
            Debug.LogError("[WordAssemblyVisuals] Save or discard open scene changes before normalizing stone visuals.");
            return;
        }

        string returnScenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrWhiteSpace(returnScenePath))
        {
            Debug.LogError("[WordAssemblyVisuals] Open a saved scene before normalizing stone visuals.");
            return;
        }

        try
        {
            Scene bearScene = EditorSceneManager.OpenScene(BearScenePath, OpenSceneMode.Single);
            StoneSet reference = CaptureAndBrightenBearReference(bearScene);
            EditorSceneManager.MarkSceneDirty(bearScene);
            EditorSceneManager.SaveScene(bearScene);

            Scene gaScene = EditorSceneManager.OpenScene(GaScenePath, OpenSceneMode.Single);
            ApplyStoneSet(FindCanvasRoot(gaScene), reference, "stone (1)");
            EditorSceneManager.MarkSceneDirty(gaScene);
            EditorSceneManager.SaveScene(gaScene);

            ApplyStoneSetToPrefab(reference);
            AssetDatabase.SaveAssets();
            Debug.Log("[WordAssemblyVisuals] CutScene_bear brightness fixed; CutScene_ga and WordAssembly now match its stone assets and sizes.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[WordAssemblyVisuals] " + exception.Message);
            throw;
        }
        finally
        {
            EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
        }
    }

    private static bool HasDirtyScenes()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            if (SceneManager.GetSceneAt(index).isDirty)
            {
                return true;
            }
        }

        return false;
    }

    private static StoneSet CaptureAndBrightenBearReference(Scene scene)
    {
        Transform canvas = FindCanvasRoot(scene);
        StoneSet reference = new StoneSet
        {
            Stone1 = CaptureVisual(FindVisual(canvas, "magic_stone/stone1")),
            Stone2 = CaptureVisual(FindVisual(canvas, "magic_stone/stone2")),
            Stone3 = CaptureVisual(FindVisual(canvas, "magic_stone/stone3")),
            PaResult = CaptureVisual(FindVisual(canvas, "book_craft_pa/stone")),
            GaResult = CaptureVisual(FindVisual(canvas, "book_craft_ga/stone"))
        };

        // The source Pa result was intentionally dimmed in the scene file.  It is a UI Image
        // tint rather than scene lighting, so restore it to its authored sprite brightness.
        Image paImage = FindVisual(canvas, "book_craft_pa/stone").Image;
        paImage.color = Color.white;
        reference.PaResult.Color = Color.white;
        return reference;
    }

    private static void ApplyStoneSetToPrefab(StoneSet reference)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WordAssemblyPrefabPath);
        try
        {
            Transform canvas = prefabRoot.transform;
            if (canvas.Find("magic_stone") == null)
            {
                canvas = prefabRoot.transform.Find("Canvas");
            }

            if (canvas == null)
            {
                throw new MissingReferenceException("Canvas is missing from WordAssembly prefab.");
            }

            ApplyStoneSet(canvas, reference, "stone");
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, WordAssemblyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ApplyStoneSet(Transform canvas, StoneSet reference, string paResultName)
    {
        ApplyVisual(FindVisual(canvas, "magic_stone/stone1"), reference.Stone1);
        ApplyVisual(FindVisual(canvas, "magic_stone/stone2"), reference.Stone2);
        ApplyVisual(FindVisual(canvas, "magic_stone/stone3"), reference.Stone3);
        ApplyVisual(FindVisual(canvas, "book_craft_pa/" + paResultName), reference.PaResult);
        ApplyVisual(FindVisual(canvas, "book_craft_ga/stone"), reference.GaResult);
    }

    private static Transform FindCanvasRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Canvas")
            {
                return root.transform;
            }
        }

        throw new MissingReferenceException("Canvas is missing from " + scene.path + ".");
    }

    private static VisualTarget FindVisual(Transform canvas, string path)
    {
        Transform transform = canvas.Find(path);
        if (transform == null)
        {
            throw new MissingReferenceException(path + " is missing beneath " + canvas.name + ".");
        }

        Image image = transform.GetComponent<Image>();
        RectTransform rect = transform as RectTransform;
        if (image == null || rect == null)
        {
            throw new MissingReferenceException(path + " must have both Image and RectTransform components.");
        }

        return new VisualTarget(image, rect);
    }

    private static StoneVisual CaptureVisual(VisualTarget source)
    {
        return new StoneVisual
        {
            Sprite = source.Image.sprite,
            Material = source.Image.material,
            Color = source.Image.color,
            Type = source.Image.type,
            PreserveAspect = source.Image.preserveAspect,
            PixelsPerUnitMultiplier = source.Image.pixelsPerUnitMultiplier,
            SizeDelta = source.Rect.sizeDelta,
            LocalScale = source.Rect.localScale
        };
    }

    private static void ApplyVisual(VisualTarget target, StoneVisual source)
    {
        target.Image.sprite = source.Sprite;
        target.Image.material = source.Material;
        target.Image.color = source.Color;
        target.Image.type = source.Type;
        target.Image.preserveAspect = source.PreserveAspect;
        target.Image.pixelsPerUnitMultiplier = source.PixelsPerUnitMultiplier;
        target.Rect.sizeDelta = source.SizeDelta;
        target.Rect.localScale = source.LocalScale;
        EditorUtility.SetDirty(target.Image);
        EditorUtility.SetDirty(target.Rect);
    }

    private sealed class StoneSet
    {
        public StoneVisual Stone1;
        public StoneVisual Stone2;
        public StoneVisual Stone3;
        public StoneVisual PaResult;
        public StoneVisual GaResult;
    }

    private sealed class StoneVisual
    {
        public Sprite Sprite;
        public Material Material;
        public Color Color;
        public Image.Type Type;
        public bool PreserveAspect;
        public float PixelsPerUnitMultiplier;
        public Vector2 SizeDelta;
        public Vector3 LocalScale;
    }

    private readonly struct VisualTarget
    {
        public readonly Image Image;
        public readonly RectTransform Rect;

        public VisualTarget(Image image, RectTransform rect)
        {
            Image = image;
            Rect = rect;
        }
    }
}
