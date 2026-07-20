using UnityEditor;
using UnityEngine;

/// <summary>
/// Fits the WordAssembly grouping RectTransforms around their children and adds edit-mode
/// resize handles, so changing Width/Height on the group scales the visible contents.
/// </summary>
public static class RepairWordAssemblyResizeHandles
{
    private const string WordAssemblyPrefabPath = "Assets/Prefabs/Boss/WordAssembly.prefab";

    private static readonly string[] ResizableGroups =
    {
        "book_craft",
        "book_craft_pa",
        "book_craft_ga",
        "magic_stone"
    };

    [MenuItem("Tools/Word Assembly/Repair Resize Handles")]
    public static void Repair()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WordAssemblyPrefabPath);
        try
        {
            for (int i = 0; i < ResizableGroups.Length; i++)
            {
                RectTransform group = FindDirectChild(prefabRoot.transform, ResizableGroups[i]);
                if (group == null)
                {
                    Debug.LogWarning($"[WordAssemblyResize] Missing group '{ResizableGroups[i]}' in {WordAssemblyPrefabPath}.");
                    continue;
                }

                WordAssemblyResizeHandle oldHandle = group.GetComponent<WordAssemblyResizeHandle>();
                if (oldHandle != null)
                {
                    Object.DestroyImmediate(oldHandle, true);
                }

                FitAroundDirectChildren(group);
                WordAssemblyResizeHandle handle = group.gameObject.AddComponent<WordAssemblyResizeHandle>();
                handle.CaptureCurrentLayoutAsReference();
                EditorUtility.SetDirty(group);
                EditorUtility.SetDirty(handle);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, WordAssemblyPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[WordAssemblyResize] WordAssembly resize handles repaired.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RectTransform FindDirectChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child as RectTransform;
    }

    private static void FitAroundDirectChildren(RectTransform group)
    {
        if (group == null || group.childCount == 0)
        {
            return;
        }

        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < group.childCount; i++)
        {
            if (group.GetChild(i) is RectTransform child)
            {
                Vector2 childScale = new Vector2(child.localScale.x, child.localScale.y);
                EncapsulateRectTree(child, child.anchoredPosition, childScale, ref bounds, ref hasBounds);
            }
        }

        if (!hasBounds || bounds.size.x <= 0.01f || bounds.size.y <= 0.01f)
        {
            return;
        }

        Vector2 centerOffset = new Vector2(bounds.center.x, bounds.center.y);
        group.anchoredPosition += centerOffset;
        group.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
        group.pivot = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < group.childCount; i++)
        {
            if (group.GetChild(i) is RectTransform child)
            {
                child.anchoredPosition -= centerOffset;
                EditorUtility.SetDirty(child);
            }
        }
    }

    private static void EncapsulateRectTree(
        RectTransform target,
        Vector2 rootSpacePosition,
        Vector2 rootSpaceScale,
        ref Bounds bounds,
        ref bool hasBounds)
    {
        Vector2 size = target.sizeDelta;
        if (size.x <= 0.01f || size.y <= 0.01f)
        {
            size = target.rect.size;
        }

        Vector2 scaledSize = new Vector2(
            Mathf.Abs(size.x * rootSpaceScale.x),
            Mathf.Abs(size.y * rootSpaceScale.y));

        EncapsulatePoint(rootSpacePosition + new Vector2(-target.pivot.x * scaledSize.x, -target.pivot.y * scaledSize.y), ref bounds, ref hasBounds);
        EncapsulatePoint(rootSpacePosition + new Vector2(-target.pivot.x * scaledSize.x, (1f - target.pivot.y) * scaledSize.y), ref bounds, ref hasBounds);
        EncapsulatePoint(rootSpacePosition + new Vector2((1f - target.pivot.x) * scaledSize.x, (1f - target.pivot.y) * scaledSize.y), ref bounds, ref hasBounds);
        EncapsulatePoint(rootSpacePosition + new Vector2((1f - target.pivot.x) * scaledSize.x, -target.pivot.y * scaledSize.y), ref bounds, ref hasBounds);

        for (int i = 0; i < target.childCount; i++)
        {
            if (target.GetChild(i) is RectTransform child)
            {
                Vector2 childScale = new Vector2(
                    rootSpaceScale.x * child.localScale.x,
                    rootSpaceScale.y * child.localScale.y);
                Vector2 childPosition = rootSpacePosition + new Vector2(
                    child.anchoredPosition.x * rootSpaceScale.x,
                    child.anchoredPosition.y * rootSpaceScale.y);
                EncapsulateRectTree(child, childPosition, childScale, ref bounds, ref hasBounds);
            }
        }
    }

    private static void EncapsulatePoint(Vector2 point, ref Bounds bounds, ref bool hasBounds)
    {
        Vector3 point3 = new Vector3(point.x, point.y, 0f);
        if (!hasBounds)
        {
            bounds = new Bounds(point3, Vector3.zero);
            hasBounds = true;
        }
        else
        {
            bounds.Encapsulate(point3);
        }
    }
}
