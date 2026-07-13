using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires and normalizes the WordAssemblyTimer time_root board.
/// Keeps every scene that owns a time_root visually consistent, and stamps the timer to start at 30s.
/// </summary>
public static class WireWordAssemblyTimer
{
    private const string NumberPngPath = "Assets/Art/quest_map/number.png";
    private const string BeepClipPath = "Assets/Audio/countdown_beep.wav";
    private const string PrefabPath = "Assets/Prefabs/time_root.prefab";
    private const string ReferenceScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    private const float StandardTotalSeconds = 30f;
    private const float StandardCountdownAt = 10f;
    private const float StandardPopDuration = 0.5f;

    [MenuItem("Tools/Timer/Wire Word Assembly Timer")]
    public static void Wire()
    {
        GameObject timeRoot = GameObject.Find("time_root");
        if (timeRoot == null)
        {
            Debug.LogError("[WireTimer] No 'time_root' in the open scene.");
            return;
        }

        NormalizeTimer(timeRoot);

        EditorSceneManager.MarkSceneDirty(timeRoot.scene);
        EditorSceneManager.SaveScene(timeRoot.scene);
        Debug.Log($"[WireTimer] Wired {timeRoot.scene.name}: timer starts at {StandardTotalSeconds:0}s.");
    }

    [MenuItem("Tools/Timer/Normalize time_root In All Scenes")]
    public static void NormalizeAllTimeRoots()
    {
        string activeScenePath = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();

        TimeRootSnapshot snapshot = CaptureSnapshotFromReference();
        bool prefabUpdated = NormalizePrefab(snapshot);

        string[] scenePaths = FindScenePathsWithTimeRoot();
        int sceneCount = 0;
        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject timeRoot = GameObject.Find("time_root");
            if (timeRoot == null)
            {
                Debug.LogWarning($"[WireTimer] '{scenePath}' mentions time_root but no active GameObject named time_root was found.");
                continue;
            }

            ApplySnapshot(timeRoot, snapshot);
            NormalizeTimer(timeRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sceneCount++;
        }

        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        Debug.Log($"[WireTimer] Normalized time_root size and 30s timer in {sceneCount} scene(s). Prefab updated={prefabUpdated}. Reference={ReferenceScenePath}");
    }

    private static string[] FindScenePathsWithTimeRoot()
    {
        return AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => File.Exists(path) && File.ReadAllText(path).Contains("time_root"))
            .OrderBy(path => path)
            .ToArray();
    }

    private static TimeRootSnapshot CaptureSnapshotFromReference()
    {
        if (!File.Exists(ReferenceScenePath))
        {
            throw new FileNotFoundException($"Reference scene not found: {ReferenceScenePath}");
        }

        EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Single);
        GameObject timeRoot = GameObject.Find("time_root");
        if (timeRoot == null)
        {
            throw new MissingReferenceException($"Reference scene has no GameObject named time_root: {ReferenceScenePath}");
        }

        return TimeRootSnapshot.Capture(timeRoot);
    }

    private static bool NormalizePrefab(TimeRootSnapshot snapshot)
    {
        if (!File.Exists(PrefabPath))
        {
            Debug.LogWarning($"[WireTimer] No prefab at {PrefabPath}; scene time_roots will still be normalized.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplySnapshot(prefabRoot, snapshot);
            NormalizeTimer(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath, out bool ok);
            return ok;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void NormalizeTimer(GameObject timeRoot)
    {
        WordAssemblyTimer timer = timeRoot.GetComponent<WordAssemblyTimer>();
        if (timer == null) timer = timeRoot.AddComponent<WordAssemblyTimer>();

        AudioSource audio = timeRoot.GetComponent<AudioSource>();
        if (audio == null) audio = timeRoot.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        Image[] digits = FindDigitImages(timeRoot);
        if (digits.Length != 4)
        {
            Debug.LogWarning($"[WireTimer] Expected 4 digit Images under {timeRoot.scene.name}/time_root, found {digits.Length}.");
        }

        TimerAssets assets = LoadTimerAssets();

        SerializedObject so = new SerializedObject(timer);
        if (digits.Length == 4) AssignArray(so, "digits", digits);
        AssignArray(so, "numberSprites", assets.NumberSprites);
        SetObject(so, "audioSource", audio);
        SetObject(so, "countdownClip", assets.Beep);
        SetObject(so, "popRoot", timeRoot.GetComponent<RectTransform>());
        SetFloat(so, "totalSeconds", StandardTotalSeconds);
        SetFloat(so, "countdownAt", StandardCountdownAt);
        SetFloat(so, "popDuration", StandardPopDuration);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(timer);
        EditorUtility.SetDirty(audio);
        EditorUtility.SetDirty(timeRoot);
    }

    private static Image[] FindDigitImages(GameObject timeRoot)
    {
        List<Image> digitImages = new List<Image>();
        foreach (Transform child in timeRoot.transform)
        {
            string n = child.name;
            if (n != "0" && !n.StartsWith("0 (")) continue;
            Image img = child.GetComponent<Image>();
            if (img != null) digitImages.Add(img);
        }

        return digitImages
            .OrderBy(i => ((RectTransform)i.transform).anchoredPosition.x)
            .Take(4)
            .ToArray();
    }

    private static TimerAssets LoadTimerAssets()
    {
        TimerAssets assets = new TimerAssets();
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(NumberPngPath))
        {
            if (o is Sprite s)
            {
                int idx = ParseTrailingIndex(s.name);
                if (idx >= 0 && idx < 10) assets.NumberSprites[idx] = s;
            }
        }

        assets.MissingSpriteCount = assets.NumberSprites.Count(s => s == null);
        if (assets.MissingSpriteCount > 0)
        {
            Debug.LogWarning($"[WireTimer] {assets.MissingSpriteCount} number sprite(s) unresolved from {NumberPngPath} (need number_0..number_9).");
        }

        assets.Beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepClipPath);
        if (assets.Beep == null) Debug.LogWarning($"[WireTimer] No beep clip at {BeepClipPath}.");

        return assets;
    }

    private static void AssignArray(SerializedObject so, string prop, Object[] values)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) return;

        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetFloat(SerializedObject so, string prop, float value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }

    private static void SetObject(SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    private static int ParseTrailingIndex(string name)
    {
        int u = name.LastIndexOf('_');
        if (u < 0 || u == name.Length - 1) return -1;
        return int.TryParse(name.Substring(u + 1), out int v) ? v : -1;
    }

    // Run with CutScene_bear open (after Wire) to snapshot its fully-wired time_root as a prefab.
    [MenuItem("Tools/Timer/Create time_root Prefab From Open Scene")]
    public static void CreatePrefab()
    {
        GameObject timeRoot = GameObject.Find("time_root");
        if (timeRoot == null) { Debug.LogError("[WireTimer] No 'time_root' to snapshot."); return; }

        NormalizeTimer(timeRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        // SaveAsPrefabAsset (not ...AndConnect): create the asset but leave the open scene untouched.
        PrefabUtility.SaveAsPrefabAsset(timeRoot, PrefabPath, out bool ok);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WireTimer] Prefab {(ok ? "saved" : "FAILED")} -> {PrefabPath}");
    }

    // Run with a scene that lacks the timer board to drop in the wired prefab.
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

        NormalizeTimer(instance);
        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Debug.Log($"[WireTimer] Added time_root prefab to {instance.scene.name}.");
    }

    private static void ApplySnapshot(GameObject timeRoot, TimeRootSnapshot snapshot)
    {
        foreach (KeyValuePair<string, RectTransformState> pair in snapshot.Rects)
        {
            Transform target = string.IsNullOrEmpty(pair.Key) ? timeRoot.transform : timeRoot.transform.Find(pair.Key);
            if (target == null)
            {
                Debug.LogWarning($"[WireTimer] Missing RectTransform path '{pair.Key}' under {timeRoot.name}; skipped.");
                continue;
            }

            RectTransform rt = target as RectTransform;
            if (rt == null) continue;

            pair.Value.Apply(rt);
            EditorUtility.SetDirty(rt);
        }
    }

    private sealed class TimerAssets
    {
        public readonly Sprite[] NumberSprites = new Sprite[10];
        public AudioClip Beep;
        public int MissingSpriteCount;
    }

    private sealed class TimeRootSnapshot
    {
        public readonly Dictionary<string, RectTransformState> Rects = new Dictionary<string, RectTransformState>();

        public static TimeRootSnapshot Capture(GameObject timeRoot)
        {
            TimeRootSnapshot snapshot = new TimeRootSnapshot();
            CaptureRecursive(timeRoot.transform, timeRoot.transform, snapshot);
            return snapshot;
        }

        private static void CaptureRecursive(Transform root, Transform current, TimeRootSnapshot snapshot)
        {
            RectTransform rt = current as RectTransform;
            if (rt != null)
            {
                snapshot.Rects[RelativePath(root, current)] = new RectTransformState(rt);
            }

            foreach (Transform child in current)
            {
                CaptureRecursive(root, child, snapshot);
            }
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;

            List<string> names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }

    private sealed class RectTransformState
    {
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 pivot;
        private readonly Vector2 sizeDelta;
        private readonly Vector3 anchoredPosition3D;
        private readonly Vector3 localScale;
        private readonly Quaternion localRotation;

        public RectTransformState(RectTransform rt)
        {
            anchorMin = rt.anchorMin;
            anchorMax = rt.anchorMax;
            pivot = rt.pivot;
            sizeDelta = rt.sizeDelta;
            anchoredPosition3D = rt.anchoredPosition3D;
            localScale = rt.localScale;
            localRotation = rt.localRotation;
        }

        public void Apply(RectTransform rt)
        {
            rt.localRotation = localRotation;
            rt.localScale = localScale;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition3D = anchoredPosition3D;
        }
    }
}
