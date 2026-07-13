using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Tune Owl Opening
/// Drops the owl_hello wave from the scene opening and tightens the owl's speech pacing.
///
/// The wave: nulling the owlHello reference is enough to remove it. OwlGreetingCutscene already
/// guards `if (owlHello != null)`, and OwlHelloSequence.Awake() disables its Image until Play() is
/// called — so with the reference gone the sprite never appears. The talking owl is unaffected:
/// PlayTalkingSequence fades it in itself (FadeTalkingAlpha), it never depended on the wave.
///
/// The pacing: the owl's paa_intro_* clips carried 0.2-0.5s of TTS silence at each end, so the pause
/// a child actually heard between two phrases was clip.tail + bearFocusVoiceGap + nextClip.lead
/// ≈ 1.04s. The clips are trimmed now, so the gap knob finally means what it says — retuned to 0.22
/// for a ~0.28s pause between phrases.
public static class TuneOwlOpening
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    const float BearFocusVoiceGap = 0.22f;   // was 0.35 — but the real pause was ~1.04s

    [MenuItem("Tools/Quest/Tune Owl Opening")]
    public static void Tune()
    {
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty)
        {
            Debug.LogError($"[OwlOpening] '{open.name}' has unsaved changes. Save or discard it first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var owl = Object.FindAnyObjectByType<OwlGreetingCutscene>(FindObjectsInactive.Include);
        if (owl == null) { Debug.LogError("[OwlOpening] no OwlGreetingCutscene in CutScene_bear"); return; }

        var so = new SerializedObject(owl);

        var hello = so.FindProperty("owlHello");
        string had = hello.objectReferenceValue != null ? hello.objectReferenceValue.name : "(already none)";
        hello.objectReferenceValue = null;

        so.FindProperty("bearFocusVoiceGap").floatValue = BearFocusVoiceGap;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owl);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[OwlOpening] wave removed (owlHello was '{had}'), bearFocusVoiceGap={BearFocusVoiceGap}s");
    }
}
