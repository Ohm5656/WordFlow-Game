using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Preview 30%|60%|100%|Off
/// Sets the shared EdgeFogMaterial's _Progress so you can see the smoke shape in the Game view
/// without entering play mode / running the whole puzzle. "Off" clears it back to 0.
/// (At runtime FogController instances the material and drives _Progress from the clock, so this
/// preview value never affects an actual play session.)
public static class FogPreview
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";

    [MenuItem("Tools/Quest/Fog Preview 30%")]
    public static void P30() => Set(0.3f);

    [MenuItem("Tools/Quest/Fog Preview 60%")]
    public static void P60() => Set(0.6f);

    [MenuItem("Tools/Quest/Fog Preview 100%")]
    public static void P100() => Set(1f);

    [MenuItem("Tools/Quest/Fog Preview Off")]
    public static void Off() => Set(0f);

    static void Set(float progress)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogPreview] material not found: {MatPath}"); return; }
        mat.SetFloat("_Progress", progress);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FogPreview] _Progress = {progress}");
    }
}
