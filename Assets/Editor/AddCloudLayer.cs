using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Add Cloud Layer
/// Adds the second smoke layer to CutScene_bear: a full-screen grey cloud-cluster overlay
/// (WordFlow/CloudFog) sitting directly above the purple FogOverlay and below the gameplay UI.
/// Reuses FogController as-is — it drives _Progress/_FogTime/_Pulse by name and reads the puzzle
/// clock through static providers, so the second instance syncs to the first for free.
/// Idempotent: re-running it updates the existing CloudOverlay instead of adding another.
public static class AddCloudLayer
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string ShaderName = "WordFlow/CloudFog";
    const string MatPath = "Assets/Scenes/region 1/CloudFogMaterial.mat";
    const string OverlayName = "CloudOverlay";

    [MenuItem("Tools/Quest/Add Cloud Layer")]
    public static void Run()
    {
        var mat = EnsureMaterial();
        if (mat == null) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // FogOverlay is the layer-1 smoke. Find it by its FogController, then walk to its parent
        // Canvas — the cloud layer must live in the same Canvas for sibling index to order them.
        var fog = Object.FindFirstObjectByType<FogController>(FindObjectsInactive.Include);
        if (fog == null)
        {
            Debug.LogError("[AddCloudLayer] no FogController (FogOverlay) found in CutScene_bear");
            return;
        }

        Transform fogT = fog.transform;
        Transform canvas = fogT.parent;
        if (canvas == null)
        {
            Debug.LogError("[AddCloudLayer] FogOverlay has no parent Canvas");
            return;
        }

        Transform existing = canvas.Find(OverlayName);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(OverlayName, typeof(RectTransform));

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Add Cloud Layer");
            go.transform.SetParent(canvas, false);
        }

        // Stretch to fill the Canvas.
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        var raw = go.GetComponent<RawImage>();
        if (raw == null) raw = go.AddComponent<RawImage>();
        raw.material = mat;
        raw.color = Color.white;
        raw.raycastTarget = false;

        var ctrl = go.GetComponent<FogController>();
        if (ctrl == null) ctrl = go.AddComponent<FogController>();

        var so = new SerializedObject(ctrl);
        var imgProp = so.FindProperty("fogImage");
        if (imgProp != null && imgProp.objectReferenceValue != raw)
        {
            imgProp.objectReferenceValue = raw;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Directly above the purple smoke, still below book_craft* / time_root / stones — one
        // ScreenSpaceOverlay Canvas, so sibling index is the draw order.
        go.transform.SetSiblingIndex(fogT.GetSiblingIndex() + 1);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[AddCloudLayer] DONE — {OverlayName} at sibling index " +
                  $"{go.transform.GetSiblingIndex()} (FogOverlay is {fogT.GetSiblingIndex()}), scene saved");
    }

    static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat != null) return mat;

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[AddCloudLayer] shader not found: {ShaderName} — is CloudFog.shader imported and compiling?");
            return null;
        }

        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AddCloudLayer] created {MatPath}");
        return mat;
    }
}
