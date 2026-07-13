using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Preview 15%|30%|60%|100%|Off
/// Sets the shared EdgeFogMaterial's _Progress so you can see the smoke shape in the Game view
/// without entering play mode / running the whole puzzle. "Off" clears it back to 0.
/// The values map to points on the three-act fogCurve — 15% is mid-act-2 (the "is it creeping in
/// too hard?" checkpoint), 30% is where the countdown beep starts, 100% is time-up.
/// (At runtime FogController instances the material and drives _Progress from the clock, so this
/// preview value never affects an actual play session.)
public static class FogPreview
{
    [MenuItem("Tools/Quest/Fog Preview 15%")]
    public static void P15() => Set(0.15f);

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
        SetOn("Assets/Scenes/region 1/EdgeFogMaterial.mat", progress);
        SetOn("Assets/Scenes/region 1/CloudFogMaterial.mat", progress);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FogPreview] _Progress = {progress}");
    }

    static void SetOn(string path, float progress)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { Debug.LogError($"[FogPreview] material not found: {path}"); return; }
        mat.SetFloat("_Progress", progress);
        EditorUtility.SetDirty(mat);
    }
}
