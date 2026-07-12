using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Fog Apply Defaults
/// Writes the smoke-clock v3 tuning: material values + the three-act fogCurve on the FogController
/// in CutScene_bear. Kept as a MenuItem because unity_execute_code does not work in this project.
/// Scene edits are done atomically and saved immediately — scene mutations are lost on domain reload.
public static class FogTune
{
    const string MatPath = "Assets/Scenes/region 1/EdgeFogMaterial.mat";
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    [MenuItem("Tools/Quest/Fog Apply Defaults")]
    public static void ApplyDefaults()
    {
        ApplyMaterial();
        ApplyScene();
    }

    static void ApplyMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError($"[FogTune] material not found: {MatPath}"); return; }

        // _FogColor is deliberately NOT written here. It is the one knob meant to be tuned by hand in
        // the Inspector (EdgeFogMaterial -> Fog Color), and this tool would clobber that choice every
        // time it ran. One colour for the whole countdown — the smoke thickens, it never changes hue.

        // Reach + softness are the readability guarantee: influence ends at 0.23 + 0.15 = 0.38,
        // comfortably < 0.5, so the four fronts can never meet and the screen centre stays clear.
        // NEVER let _MaxReach + _Softness reach 0.5.
        mat.SetFloat("_MaxReach", 0.23f);
        mat.SetFloat("_Softness", 0.15f);

        // 0.75 (was 0.90) leaves a quarter of the village showing through even at the core, which is
        // what makes it read as smoke instead of a painted border. 0.40 (was 0.45) shrinks the fully
        // solid part of the band.
        mat.SetFloat("_Density", 0.75f);
        mat.SetFloat("_CoreFrac", 0.40f);

        mat.SetFloat("_NoiseScale", 2.0f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.55f);
        mat.SetFloat("_WispStrength", 0.7f);

        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.16f);

        mat.SetFloat("_Pulse", 0f);
        mat.SetFloat("_PulseReach", 0.03f);

        // _Softness is a constant added to reach, so without this ramp the first non-zero progress
        // would already paint a full-density edge. 0.50 spreads the opacity climb across the whole
        // creep so the smoke materialises out of nothing instead of switching on.
        mat.SetFloat("_FadeIn", 0.50f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] material: smoke clock v4 values applied");
    }

    static void ApplyScene()
    {
        // OpenScene(Single) closes whatever scene is currently open. If that scene has unsaved work
        // in it, running this menu item would throw it away — so refuse rather than destroy.
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty)
        {
            Debug.LogError(
                $"[FogTune] '{open.name}' has unsaved changes. Save or discard it first — this tool " +
                "has to open CutScene_bear, which would close it and lose that work.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var fog = Object.FindAnyObjectByType<FogController>();
        if (fog == null) { Debug.LogError("[FogTune] FogController not found in CutScene_bear"); return; }

        // The smoke starts at 25s and still hits full reach exactly at 0s. These keys are the old
        // 20s curve remapped along its own ramp — the VALUE at every key is untouched (0, .12, .30,
        // .62, 1), so the entrance is the identical shape, just stretched over 25s instead of 20s.
        // Do not "tidy" these numbers: they are the remap, not round figures.
        var curve = new AnimationCurve(
            new Keyframe(0.00000f, 0.00f),   // 30s left — book pops in, screen clean
            new Keyframe(0.16667f, 0.00f),   // 25s left — smoke starts
            new Keyframe(0.37811f, 0.12f),   // 18.7s left — first wisps find the edges
            new Keyframe(0.58955f, 0.30f),   // 12.3s left — smoke clearly present
            new Keyframe(0.81343f, 0.62f),   //  5.6s left
            new Keyframe(1.00000f, 1.00f)    //  0s left — full frame
        );

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        var so = new SerializedObject(fog);
        so.FindProperty("fogCurve").animationCurveValue = curve;
        so.FindProperty("urgency").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(fog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[FogTune] scene: three-act fogCurve + urgency=3 written to CutScene_bear");
    }
}
