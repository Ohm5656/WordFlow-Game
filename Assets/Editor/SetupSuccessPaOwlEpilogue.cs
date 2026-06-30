using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

public static class SetupSuccessPaOwlEpilogue
{
    [MenuItem("Tools/Success/Setup Success_pa Owl Epilogue")]
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/region 1/Success_pa.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Find Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[SetupOwlEpilogue] No Canvas found"); return; }

        // Remove existing OwlEpilogue root if re-running
        var existing = canvas.transform.Find("OwlEpilogue");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // --- Create OwlEpilogue root GO (holds the SuccessPaOwlEpilogue component) ---
        var rootGo = new GameObject("OwlEpilogue");
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRect = rootGo.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;

        var epilogue = rootGo.AddComponent<SuccessPaOwlEpilogue>();

        // --- Create child "owl" --- (clone of owl in CutScene_bear)
        var owlGo = new GameObject("owl");
        owlGo.transform.SetParent(rootGo.transform, false);
        owlGo.layer = LayerMask.NameToLayer("UI");

        var owlRect = owlGo.AddComponent<RectTransform>();
        owlRect.anchorMin = new Vector2(0.5f, 0.5f);
        owlRect.anchorMax = new Vector2(0.5f, 0.5f);
        owlRect.pivot = new Vector2(0.5f, 0.5f);
        owlRect.anchoredPosition = new Vector2(0f, -89f); // user-requested position
        owlRect.sizeDelta = new Vector2(1920f, 1080f);

        owlGo.AddComponent<CanvasRenderer>();

        var img = owlGo.AddComponent<Image>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/quest_map/owl/0151.png");
        if (sprite != null) img.sprite = sprite;
        img.raycastTarget = false;

        var owlAnimator = owlGo.AddComponent<Animator>();
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Art/quest_map/owl/owlController.controller");
        if (controller != null) owlAnimator.runtimeAnimatorController = controller;
        owlAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // CanvasGroup for fade
        var cg = owlGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        owlGo.SetActive(false); // hidden until epilogue starts

        // Add TtsApiClient so ResolveTts() can find it via FindObjectOfType
        var ttsClient = rootGo.AddComponent<TtsApiClient>();
        var ttsClientSo = new SerializedObject(ttsClient);
        ttsClientSo.FindProperty("ttsUrl").stringValue = "http://127.0.0.1:8001/api/v1/tts";
        ttsClientSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire SuccessPaOwlEpilogue fields via SerializedObject
        var so = new SerializedObject(epilogue);
        so.FindProperty("owlAnimator").objectReferenceValue = owlAnimator;
        so.FindProperty("owlPosition").vector2Value = new Vector2(0f, -89f);
        so.FindProperty("ttsClient").objectReferenceValue = ttsClient;
        so.FindProperty("phrase1LineId").stringValue = "paa_outro_owl_1";
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Wire BearCutscene.owlEpilogue ---
        var bear = canvas.transform.Find("Bear");
        if (bear != null)
        {
            var bearCutscene = bear.GetComponent<BearCutscene>();
            if (bearCutscene != null)
            {
                var bearSo = new SerializedObject(bearCutscene);
                bearSo.FindProperty("owlEpilogue").objectReferenceValue = epilogue;
                bearSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[SetupOwlEpilogue] Wired BearCutscene.owlEpilogue");
            }
            else Debug.LogWarning("[SetupOwlEpilogue] No BearCutscene found on Bear GO");
        }
        else Debug.LogWarning("[SetupOwlEpilogue] Bear GO not found in Canvas");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupOwlEpilogue] Done — Success_pa saved.");
    }
}
