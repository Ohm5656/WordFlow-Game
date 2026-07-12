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

        // Pale cool gray, not pure white: the puzzle's book page / canvas backdrop is itself
        // near-white, so white-on-white fog is invisible.
        mat.SetColor("_FogColor", new Color(0.78f, 0.80f, 0.84f, 1f));

        // Act 3 darkens toward a heavy storm-gray. Not red — this is a children's game.
        mat.SetColor("_PanicColor", new Color(0.52f, 0.55f, 0.63f, 1f));

        // Reach + softness are the readability guarantee: influence ends at 0.26 + 0.15 = 0.41,
        // which is < 0.5, so the four fronts can never meet and the centre alpha is exactly 0 even
        // at full progress. The smoke is a frame, never a blindfold.
        mat.SetFloat("_MaxReach", 0.26f);
        mat.SetFloat("_Softness", 0.15f);
        mat.SetFloat("_CoreFrac", 0.45f);
        mat.SetFloat("_Density", 0.9f);

        mat.SetFloat("_NoiseScale", 2.0f);
        mat.SetFloat("_NoiseStrength", 0.18f);
        mat.SetFloat("_WarpAmount", 0.55f);
        mat.SetFloat("_WispStrength", 0.7f);

        mat.SetFloat("_Speed", 0.05f);
        mat.SetFloat("_BoilSpeed", 0.16f);

        mat.SetFloat("_Pulse", 0f);
        mat.SetFloat("_PulseReach", 0.03f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[FogTune] material: smoke clock v3 values applied");
    }

    static void ApplyScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var fog = Object.FindAnyObjectByType<FogController>();
        if (fog == null) { Debug.LogError("[FogTune] FogController not found in CutScene_bear"); return; }

        // Three acts over the 30s clock. Flat 0 for the first third so the player gets a clean
        // screen to read the puzzle when the book pops in, then a creep, then a rush that lands
        // exactly where WordAssemblyTimer.countdownAt (10s) starts the beep.
        var curve = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),   // 30s left — book pops in, screen clean
            new Keyframe(0.33f, 0.00f),   // 20s left — still clean
            new Keyframe(0.50f, 0.12f),   // 15s left — first wisps find the edges
            new Keyframe(0.67f, 0.30f),   // 10s left — beep starts, smoke clearly present
            new Keyframe(0.85f, 0.62f),   //  4.5s left
            new Keyframe(1.00f, 1.00f)    //  0s left — full frame
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
