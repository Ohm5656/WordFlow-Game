using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Build Star Hud
/// Moves the star visuals out from under star_root (which still hosts WordAssemblyTimer — the timer
/// blanks its own CanvasGroup on Pause(), which fires the instant a word is built, so a star parented
/// under it would vanish the moment it was earned) into a sibling `star_hud`, and wires up StarHud.
/// Idempotent: re-running re-wires the existing star_hud instead of building a second one.
public static class BuildStarHud
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string HudName = "star_hud";

    [MenuItem("Tools/Quest/Build Star Hud")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform starRoot = FindInScene("star_root");
        if (starRoot == null) { Debug.LogError("[BuildStarHud] star_root not found"); return; }

        Transform canvas = starRoot.parent;
        if (canvas == null) { Debug.LogError("[BuildStarHud] star_root has no parent Canvas"); return; }

        // --- the star_hud object: same RectTransform as star_root, same sibling slot -------------
        Transform hudT = canvas.Find(HudName);
        GameObject hud = hudT != null ? hudT.gameObject : new GameObject(HudName, typeof(RectTransform));
        if (hudT == null)
        {
            Undo.RegisterCreatedObjectUndo(hud, "Build Star Hud");
            hud.transform.SetParent(canvas, false);
        }

        var srcRect = (RectTransform)starRoot;
        var hudRect = (RectTransform)hud.transform;
        hudRect.anchorMin = srcRect.anchorMin;
        hudRect.anchorMax = srcRect.anchorMax;
        hudRect.pivot = srcRect.pivot;
        hudRect.anchoredPosition = srcRect.anchoredPosition;
        hudRect.sizeDelta = srcRect.sizeDelta;
        hudRect.localScale = srcRect.localScale;
        hudRect.localRotation = srcRect.localRotation;
        hud.layer = starRoot.gameObject.layer;

        // Draw on top of everything else in the Canvas, like the timer board did.
        hud.transform.SetSiblingIndex(canvas.childCount - 1);

        // --- move the children across, preserving their authored local layout --------------------
        // star_hud's RectTransform is identical to star_root's, so the local values map 1:1 — copy
        // them out, reparent without world-position math, copy them back.
        var kids = new List<RectTransform>();
        for (int i = starRoot.childCount - 1; i >= 0; i--)
        {
            var r = starRoot.GetChild(i) as RectTransform;
            if (r != null) kids.Add(r);
        }
        kids.Reverse();

        foreach (var k in kids)
        {
            Vector2 aMin = k.anchorMin, aMax = k.anchorMax, piv = k.pivot;
            Vector2 pos = k.anchoredPosition, size = k.sizeDelta;
            Vector3 scale = k.localScale;
            Quaternion rot = k.localRotation;

            Undo.SetTransformParent(k, hud.transform, "Build Star Hud");
            k.SetParent(hud.transform, false);

            k.anchorMin = aMin; k.anchorMax = aMax; k.pivot = piv;
            k.anchoredPosition = pos; k.sizeDelta = size;
            k.localScale = scale; k.localRotation = rot;
        }

        // --- wire StarHud ------------------------------------------------------------------------
        var star = hud.GetComponent<StarHud>();
        if (star == null) star = Undo.AddComponent<StarHud>(hud);

        var map = BuildNameMap(hud.transform);

        var so = new SerializedObject(star);
        so.FindProperty("starFrame").objectReferenceValue = Get<RectTransform>(map, "star_frame");
        so.FindProperty("starStart").objectReferenceValue = Get<RectTransform>(map, "star_start");
        so.FindProperty("resetOnAwake").boolValue = true;   // CutScene_bear: stars never survive a retry
        so.FindProperty("recapOnStart").boolValue = false;
        so.FindProperty("fadeOutAfterRecap").boolValue = false;

        // effect_star1..4 — the centre burst
        var centre = so.FindProperty("centreBurst");
        centre.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            centre.GetArrayElementAtIndex(i).objectReferenceValue = Get<Image>(map, $"effect_star{i + 1}");
        }

        // slots 0,1,2: star_end / star_end(1) / star_end(2), stamp = effect_panel1(i), burst = panels 2..5
        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            string suffix = i == 0 ? "" : $"({i})";

            var slot = slotsProp.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("end").objectReferenceValue = Get<RectTransform>(map, $"star_end{suffix}");
            slot.FindPropertyRelative("stamp").objectReferenceValue = Get<Image>(map, $"effect_panel1{suffix}");

            var burst = slot.FindPropertyRelative("burst");
            burst.arraySize = 4;
            for (int p = 2; p <= 5; p++)
            {
                burst.GetArrayElementAtIndex(p - 2).objectReferenceValue = Get<Image>(map, $"effect_panel{p}{suffix}");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildStarHud] DONE — {HudName} holds {hud.transform.childCount} children, " +
                  $"star_root kept {starRoot.childCount} (should be 0), scene saved");
    }

    /// The authored names have irregular internal spacing — "effect_panel3 ", "effect_panel3  (1)",
    /// "effect_panel4  (2)". Key every child by its name with ALL whitespace stripped, so a lookup of
    /// "effect_panel3(1)" finds it no matter how many spaces the designer left in.
    static Dictionary<string, Transform> BuildNameMap(Transform root)
    {
        var map = new Dictionary<string, Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            string key = Regex.Replace(c.name, @"\s+", "");
            map[key] = c;   // last one wins; names are unique after normalising
        }
        return map;
    }

    static T Get<T>(Dictionary<string, Transform> map, string normalisedName) where T : Component
    {
        if (!map.TryGetValue(normalisedName, out Transform t) || t == null)
        {
            Debug.LogError($"[BuildStarHud] missing child: {normalisedName}");
            return null;
        }

        T c = typeof(T) == typeof(RectTransform) ? t as T : t.GetComponent<T>();
        if (c == null) Debug.LogError($"[BuildStarHud] {normalisedName} has no {typeof(T).Name}");
        return c;
    }

    static Transform FindInScene(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name) return go.transform;
        }
        return null;
    }
}
