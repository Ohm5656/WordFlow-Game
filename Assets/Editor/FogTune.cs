using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Apply Defaults
/// Writes the tuned smoke-clock v2 values onto the shared EdgeFogMaterial. Kept as a MenuItem
/// because unity_execute_code does not work in this project — this is how an agent can set the
/// material without hand-editing the .mat YAML.
public static class FogTune
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";

    [MenuItem("Tools/Quest/Fog Apply Defaults")]
    public static void ApplyDefaults()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogTune] material not found: {MatPath}"); return; }

        mat.SetColor("_FogColor", Color.white);
        mat.SetFloat("_Density", 1f);
        mat.SetFloat("_MaxReach", 0.34f);
        mat.SetFloat("_CoreFrac", 0.35f);
        mat.SetFloat("_Softness", 0.22f);
        mat.SetFloat("_NoiseScale", 2.5f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.4f);
        mat.SetFloat("_WispStrength", 0.6f);
        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.12f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] smoke clock v2 defaults applied");
    }
}
