using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

// One-shot: in CutScene_bear, replace OwlRoot's swapped sprite frames with the single animated
// Owl (Owl.prefab, owl.anim via Animator) and switch the two intro greeting voices to TTS
// (paa_intro_owl_1 / paa_intro_owl_2). Stone/repeat audio stays as baked AudioClips.
// Run via Tools/Owl/Swap Owl Anim + TTS (CutScene_bear).
public static class SetupOwlAnimTtsBear
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string OwlPrefab = "Assets/Art/quest_map/owl/Owl.prefab";

    [MenuItem("Tools/Owl/Swap Owl Anim + TTS (CutScene_bear)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "CutScene_bear")
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var owlRootGo = GameObject.Find("Canvas/OwlRoot");
        if (owlRootGo == null) { Debug.LogError("[OwlAnim] Canvas/OwlRoot not found"); return; }
        var owlRoot = (RectTransform)owlRootGo.transform;
        var greeting = owlRootGo.GetComponent<OwlGreetingCutscene>();
        if (greeting == null) { Debug.LogError("[OwlAnim] OwlGreetingCutscene missing"); return; }

        // capture the placed owl spot from the first existing frame, then remove all old frames
        Vector2 placedPos = Vector2.zero;
        var firstFrame = FindOwlFrame(owlRoot);
        if (firstFrame != null) placedPos = firstFrame.anchoredPosition;
        for (int i = owlRoot.childCount - 1; i >= 0; i--)
        {
            var c = owlRoot.GetChild(i);
            if (c.name.StartsWith("owl", System.StringComparison.OrdinalIgnoreCase))
                Object.DestroyImmediate(c.gameObject);
        }

        // add the single animated owl (prefix "owl" so OwlGreetingCutscene caches it as its 1 frame)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OwlPrefab);
        if (prefab == null) { Debug.LogError($"[OwlAnim] missing {OwlPrefab}"); return; }
        var owl = (GameObject)PrefabUtility.InstantiatePrefab(prefab, owlRoot);
        owl.name = "owl";
        var owlRect = (RectTransform)owl.transform;
        owlRect.anchorMin = owlRect.anchorMax = new Vector2(0.5f, 0.5f);
        owlRect.pivot = new Vector2(0.5f, 0.5f);
        owlRect.anchoredPosition = placedPos;
        owlRect.localScale = Vector3.one;            // tune in inspector / via screenshot
        var img = owl.GetComponent<Image>();
        if (img != null) img.SetNativeSize();

        // TTS client (match the scene's 8001 backend like Session/Grade/Telemetry)
        var tts = Object.FindObjectOfType<TtsApiClient>();
        if (tts == null) tts = owlRootGo.AddComponent<TtsApiClient>();
        var tso = new SerializedObject(tts);
        tso.FindProperty("ttsUrl").stringValue = "http://127.0.0.1:8001/api/v1/tts";
        tso.FindProperty("authorization").stringValue = "Bearer demo-token";
        tso.ApplyModifiedPropertiesWithoutUndo();

        // wire OwlGreetingCutscene: TTS lines + collapse the frame sequence to the single owl
        var go = new SerializedObject(greeting);
        go.FindProperty("ttsClient").objectReferenceValue = tts;
        go.FindProperty("greetingLineId").stringValue = "paa_intro_owl_1";
        go.FindProperty("bearFocusLookLineId").stringValue = "paa_intro_owl_2";
        go.FindProperty("bearFocusFrameSequence").stringValue = "owl";
        go.FindProperty("bearFocusExcludedFrames").stringValue = "";
        go.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[OwlAnim] DONE — single animated owl at {placedPos}, TTS paa_intro_owl_1/2 wired (8001). Tune owl scale/pos via screenshot.");
    }

    static RectTransform FindOwlFrame(RectTransform owlRoot)
    {
        for (int i = 0; i < owlRoot.childCount; i++)
        {
            var c = owlRoot.GetChild(i);
            if (c.name.StartsWith("owl", System.StringComparison.OrdinalIgnoreCase))
                return (RectTransform)c;
        }
        return null;
    }
}
