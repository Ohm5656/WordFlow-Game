using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires the WordAssemblyTimer on the currently open scene's time_root: assigns the 4 digit Images
/// (ordered left->right by x = MM:SS), the number_0..9 sprites sliced from number.png, and the
/// countdown_beep clip. Run once per scene that has the timer board (CutScene_bear, CutScene_ga).
/// Atomic (memory: do scene edits in one editor script + save).
/// </summary>
public static class WireWordAssemblyTimer
{
    private const string NumberPngPath = "Assets/Art/quest_map/number.png";
    private const string BeepClipPath = "Assets/Audio/countdown_beep.wav";

    [MenuItem("Tools/Timer/Wire Word Assembly Timer")]
    public static void Wire()
    {
        GameObject timeRoot = GameObject.Find("time_root");
        if (timeRoot == null)
        {
            Debug.LogError("[WireTimer] No 'time_root' in the open scene.");
            return;
        }

        WordAssemblyTimer timer = timeRoot.GetComponent<WordAssemblyTimer>();
        if (timer == null) timer = timeRoot.AddComponent<WordAssemblyTimer>();

        AudioSource audio = timeRoot.GetComponent<AudioSource>();
        if (audio == null) audio = timeRoot.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // Digits: children with an Image whose name is "0" or "0 (n)", ordered left -> right by x.
        List<Image> digitImages = new List<Image>();
        foreach (Transform child in timeRoot.transform)
        {
            string n = child.name;
            if (n != "0" && !n.StartsWith("0 (")) continue;
            Image img = child.GetComponent<Image>();
            if (img != null) digitImages.Add(img);
        }
        Image[] digits = digitImages
            .OrderBy(i => ((RectTransform)i.transform).anchoredPosition.x)
            .Take(4)
            .ToArray();

        if (digits.Length != 4)
        {
            Debug.LogError($"[WireTimer] Expected 4 digit Images under time_root, found {digits.Length}.");
            return;
        }

        // number_0..number_9 sub-sprites.
        Sprite[] sprites = new Sprite[10];
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(NumberPngPath))
        {
            if (o is Sprite s)
            {
                int idx = ParseTrailingIndex(s.name);
                if (idx >= 0 && idx < 10) sprites[idx] = s;
            }
        }
        // also try the main asset in case it's a single sprite (defensive; not expected)
        int missing = sprites.Count(s => s == null);
        if (missing > 0)
            Debug.LogWarning($"[WireTimer] {missing} number sprite(s) unresolved from {NumberPngPath} (need number_0..number_9).");

        AudioClip beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepClipPath);
        if (beep == null) Debug.LogWarning($"[WireTimer] No beep clip at {BeepClipPath}.");

        SerializedObject so = new SerializedObject(timer);
        AssignArray(so, "digits", digits);
        AssignArray(so, "numberSprites", sprites);
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("countdownClip").objectReferenceValue = beep;
        so.FindProperty("popRoot").objectReferenceValue = timeRoot.GetComponent<RectTransform>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(timer);
        EditorSceneManager.MarkSceneDirty(timeRoot.scene);
        EditorSceneManager.SaveScene(timeRoot.scene);
        Debug.Log($"[WireTimer] Wired {timeRoot.scene.name}: 4 digits, {10 - missing}/10 sprites, beep={(beep != null)}.");
    }

    private static void AssignArray(SerializedObject so, string prop, Object[] values)
    {
        SerializedProperty p = so.FindProperty(prop);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static int ParseTrailingIndex(string name)
    {
        int u = name.LastIndexOf('_');
        if (u < 0 || u == name.Length - 1) return -1;
        return int.TryParse(name.Substring(u + 1), out int v) ? v : -1;
    }

    private const string PrefabPath = "Assets/Prefabs/time_root.prefab";

    // Run with CutScene_bear open (after Wire) to snapshot its fully-wired time_root as a prefab.
    [MenuItem("Tools/Timer/Create time_root Prefab From Open Scene")]
    public static void CreatePrefab()
    {
        GameObject timeRoot = GameObject.Find("time_root");
        if (timeRoot == null) { Debug.LogError("[WireTimer] No 'time_root' to snapshot."); return; }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        // SaveAsPrefabAsset (not ...AndConnect): create the asset but leave the open scene untouched.
        PrefabUtility.SaveAsPrefabAsset(timeRoot, PrefabPath, out bool ok);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WireTimer] Prefab {(ok ? "saved" : "FAILED")} -> {PrefabPath}");
    }

    // Run with a scene that lacks the timer board (e.g. CutScene_ga) to drop in the wired prefab.
    [MenuItem("Tools/Timer/Add time_root To Open Scene")]
    public static void AddToScene()
    {
        if (GameObject.Find("time_root") != null) { Debug.LogWarning("[WireTimer] Scene already has time_root."); return; }
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[WireTimer] No 'Canvas' in the open scene."); return; }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError($"[WireTimer] No prefab at {PrefabPath}. Run 'Create time_root Prefab' first."); return; }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(canvas.transform, false);
        RectTransform rt = instance.transform as RectTransform;
        RectTransform prefabRt = prefab.transform as RectTransform;
        if (rt != null && prefabRt != null)
        {
            rt.anchorMin = prefabRt.anchorMin;
            rt.anchorMax = prefabRt.anchorMax;
            rt.pivot = prefabRt.pivot;
            rt.anchoredPosition3D = prefabRt.anchoredPosition3D;
            rt.sizeDelta = prefabRt.sizeDelta;
            rt.localScale = prefabRt.localScale;
        }

        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Debug.Log($"[WireTimer] Added time_root prefab to {instance.scene.name}.");
    }
}
