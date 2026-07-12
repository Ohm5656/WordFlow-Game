using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Tune Voice Pacing
/// The gameplay_* TTS clips shipped with 0.24-0.81s of leading and 0.21-0.58s of trailing silence
/// baked in — an artifact of the TTS bake, not intentional pacing. That dead air, not the configured
/// gaps, was what made the example read-aloud and the word assembly drag: the real pause between two
/// syllables was clip.tail + gap + nextClip.lead = ~1.6s, so turning the gap knob down barely moved it.
///
/// The clips are trimmed now (silence removed, speech untouched), which means the configured gap is
/// finally the gap the child actually hears. These values re-tune for the trimmed clips:
/// a ~0.46s pause between syllables and ~0.61s before the whole word — clear enough to follow,
/// without the dead air.
///
/// Nothing about "wait for the voice to finish" changes: PlacementVoiceRoutine still waits
/// clip.length + postGap, and blockPlacementWhileVoicePlaying still gates the next stone.
public static class TuneVoicePacing
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    const float EchoGap = 0.40f;        // was 0.45 — but the real pause was ~1.61s
    const float EchoFinalWordGap = 0.55f; // was 0.70 — real pause was ~1.54s
    const float PlacementPostGap = 0.15f; // was 0.25

    [MenuItem("Tools/Quest/Tune Voice Pacing")]
    public static void Tune()
    {
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty)
        {
            Debug.LogError($"[VoicePacing] '{open.name}' has unsaved changes. Save or discard it first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var controllers = Object.FindObjectsByType<MagicStonePuzzleController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (controllers.Length == 0)
        {
            Debug.LogError("[VoicePacing] no MagicStonePuzzleController in CutScene_bear");
            return;
        }

        foreach (var c in controllers)
        {
            var so = new SerializedObject(c);
            so.FindProperty("ttsEchoGapSeconds").floatValue = EchoGap;
            so.FindProperty("ttsEchoFinalWordGapSeconds").floatValue = EchoFinalWordGap;
            so.FindProperty("placementVoicePostGapSeconds").floatValue = PlacementPostGap;

            // The child must always hear a phoneme out before the next stone lands. Non-negotiable.
            so.FindProperty("blockPlacementWhileVoicePlaying").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[VoicePacing] {controllers.Length} controller(s): echoGap={EchoGap}s " +
                  $"finalWordGap={EchoFinalWordGap}s placementPostGap={PlacementPostGap}s, " +
                  "blockPlacementWhileVoicePlaying=true");
    }
}
