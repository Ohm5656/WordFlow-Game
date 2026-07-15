using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Build Misassembly Lock  — builds the garbage-word red alert into CutScene_bear.
/// Tools/Quest/Lock Preview On | Off   — eyeball the alert without playing to a garbage word.
///
/// The lock_overlay (additive red vignette) goes near the top of the Canvas so the alert covers the
/// smoke, the book and the star board — safe, because additive can only add red light, never hide
/// what is beneath it. key_root (the padlock art, authored by hand) is then pushed ABOVE it, so the
/// padlock draws over the red rather than being washed by it.
///
/// Idempotent: re-running re-wires the existing overlay instead of building a second one.
public static class BuildMisassemblyLock
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string ShaderName = "WordFlow/LockVignette";
    const string MatPath = "Assets/Scenes/region 1/LockVignetteMaterial.mat";
    const string BeepPath = "Assets/Audio/countdown_beep.wav";
    const string UnlockSfxPath = "Assets/Audio/unlock.wav";
    const string OverlayName = "lock_overlay";
    const string KeyRootName = "key_root";

    [MenuItem("Tools/Quest/Build Misassembly Lock")]
    public static void Run()
    {
        Material mat = EnsureMaterial();
        if (mat == null) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[BuildMisassemblyLock] no Canvas in CutScene_bear"); return; }

        Transform keyRoot = canvas.transform.Find(KeyRootName);
        if (keyRoot == null)
        {
            Debug.LogError($"[BuildMisassemblyLock] {KeyRootName} not found under the Canvas — the " +
                           "padlock art (lock / unlock / effect_star1..4) is authored there and is required");
            return;
        }

        // --- lock_overlay: the full-screen additive vignette ---------------------------------------
        Transform existing = canvas.transform.Find(OverlayName);
        GameObject overlay = existing != null
            ? existing.gameObject
            : new GameObject(OverlayName, typeof(RectTransform));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(overlay, "Build Misassembly Lock");
            overlay.transform.SetParent(canvas.transform, false);
        }

        Stretch((RectTransform)overlay.transform);
        overlay.layer = keyRoot.gameObject.layer;

        var raw = overlay.GetComponent<RawImage>();
        if (raw == null) raw = overlay.AddComponent<RawImage>();
        raw.material = mat;
        raw.color = Color.white;
        raw.raycastTarget = false;   // MisassemblyLock switches this on only while the lock is up

        var src = overlay.GetComponent<AudioSource>();
        if (src == null) src = overlay.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;

        // --- wire MisassemblyLock to the authored key_root art ---------------------------------------
        var lockComp = overlay.GetComponent<MisassemblyLock>();
        if (lockComp == null) lockComp = Undo.AddComponent<MisassemblyLock>(overlay);

        var art = BuildNameMap(keyRoot);

        var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(BeepPath);
        if (beep == null) Debug.LogWarning($"[BuildMisassemblyLock] beep clip not found: {BeepPath}");

        var unlockSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(UnlockSfxPath);
        if (unlockSfx == null) Debug.LogWarning($"[BuildMisassemblyLock] unlock sfx not found: {UnlockSfxPath}");

        var so = new SerializedObject(lockComp);
        so.FindProperty("vignette").objectReferenceValue = raw;
        so.FindProperty("lockIcon").objectReferenceValue = Get(art, "lock");
        so.FindProperty("unlockIcon").objectReferenceValue = Get(art, "unlock");
        so.FindProperty("audioSource").objectReferenceValue = src;
        if (beep != null) so.FindProperty("beepClip").objectReferenceValue = beep;
        if (unlockSfx != null) so.FindProperty("unlockSfx").objectReferenceValue = unlockSfx;

        // effect_star1..4 — the unlock burst. Their authored positions are the scatter destinations.
        var burst = so.FindProperty("burst");
        burst.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            burst.GetArrayElementAtIndex(i).objectReferenceValue = Get(art, $"effect_star{i + 1}");
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // --- draw order: red under the padlock, both above everything else --------------------------
        overlay.transform.SetSiblingIndex(canvas.transform.childCount - 1);
        keyRoot.SetSiblingIndex(canvas.transform.childCount - 1);   // key_root ends up on top

        // Both roots MUST stay active: lock_overlay's Awake registers FogController.ExtraPulseProvider
        // and hides the art itself. Shipping either inactive would silently kill the effect.
        overlay.SetActive(true);
        keyRoot.gameObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BuildMisassemblyLock] DONE — {OverlayName} at sibling index " +
                  $"{overlay.transform.GetSiblingIndex()}, {KeyRootName} at {keyRoot.GetSiblingIndex()} (on top), " +
                  $"beep={(beep != null ? beep.name : "MISSING")}, unlock={(unlockSfx != null ? unlockSfx.name : "MISSING")}, " +
                  "scene saved");
    }

    [MenuItem("Tools/Quest/Lock Preview On")]
    public static void PreviewOn() => SetPreview(1f);

    [MenuItem("Tools/Quest/Lock Preview Off")]
    public static void PreviewOff() => SetPreview(0f);

    static void SetPreview(float intensity)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[LockPreview] material not found: {MatPath}"); return; }

        mat.SetFloat("_Intensity", intensity);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LockPreview] _Intensity = {intensity}");
    }

    /// Key every child of key_root by its name with all whitespace stripped, so a stray space the
    /// designer left in ("effect_star1 ") never breaks the lookup. Same guard the star board needed.
    static Dictionary<string, Image> BuildNameMap(Transform root)
    {
        var map = new Dictionary<string, Image>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            var img = c.GetComponent<Image>();
            if (img == null) continue;
            map[Regex.Replace(c.name, @"\s+", "")] = img;
        }
        return map;
    }

    static Image Get(Dictionary<string, Image> map, string normalisedName)
    {
        if (map.TryGetValue(normalisedName, out Image img) && img != null) return img;
        Debug.LogError($"[BuildMisassemblyLock] key_root is missing an Image named '{normalisedName}'");
        return null;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.localScale = Vector3.one;
    }

    static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[BuildMisassemblyLock] shader not found: {ShaderName} — is LockVignette.shader importing?");
            return null;
        }

        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildMisassemblyLock] created {MatPath}");
        return mat;
    }
}
