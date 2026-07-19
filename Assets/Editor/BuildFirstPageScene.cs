using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// One-click builder for the first_page title screen scene. Idempotent:
/// re-running rebuilds the scene from scratch and re-registers it in
/// EditorBuildSettings at index 0. Run via Tools > FirstPage > Build Scene.
/// </summary>
public static class BuildFirstPageScene
{
    private const string ScenePath = "Assets/Scenes/first_page.unity";
    private const string ThaiFontPath = "Assets/Fonts/LeelawUI SDF.asset";

    [MenuItem("Tools/FirstPage/Build Scene")]
    public static void Build()
    {
        VideoClip introClip = LoadClip("Assets/Video/intro.mp4");
        VideoClip introReverseClip = LoadClip("Assets/Video/intro_rev.mp4");
        VideoClip idleClip = LoadClip("Assets/Video/idle.mp4");
        VideoClip idleReverseClip = LoadClip("Assets/Video/idle_rev.mp4");
        RenderTexture rtA = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroA.renderTexture");
        RenderTexture rtB = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroB.renderTexture");
        TMP_FontAsset thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontPath);

        if (introClip == null || introReverseClip == null || idleClip == null || idleReverseClip == null
            || rtA == null || rtB == null || thaiFont == null)
        {
            Debug.LogError("[FirstPage] Missing a required asset — run Task 1-3 first. Aborting.");
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RawImage videoSurface = CreateVideoSurface(canvas.transform);
        CanvasGroup pressToStartGroup = CreatePressToStart(canvas.transform, thaiFont);

        var playersGO = new GameObject("FirstPageIntro");
        var playerA = playersGO.AddComponent<VideoPlayer>();
        var playerB = playersGO.AddComponent<VideoPlayer>();
        playerA.targetTexture = rtA;
        playerB.targetTexture = rtB;

        var intro = playersGO.AddComponent<FirstPageIntro>();
        var so = new SerializedObject(intro);
        SetRef(so, "playerA", playerA);
        SetRef(so, "playerB", playerB);
        SetRef(so, "videoSurface", videoSurface);
        SetRef(so, "pressToStartGroup", pressToStartGroup);
        SetRef(so, "introClip", introClip);
        SetRef(so, "introReverseClip", introReverseClip);
        SetRef(so, "idleClip", idleClip);
        SetRef(so, "idleReverseClip", idleReverseClip);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        RegisterAtIndexZero(ScenePath);

        Debug.Log("[FirstPage] Scene built and registered at build index 0.");
    }

    private static RawImage CreateVideoSurface(Transform canvasTransform)
    {
        var go = new GameObject("VideoSurface", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1920, 1080);

        var fitter = go.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1920f / 1080f;

        var image = go.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;

        return image;
    }

    private static CanvasGroup CreatePressToStart(Transform canvasTransform, TMP_FontAsset font)
    {
        var go = new GameObject("PressToStart", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 90f);
        rt.sizeDelta = new Vector2(700, 70);

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = "กดเพื่อเข้าเกม";
        text.font = font;
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return group;
    }

    private static VideoClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        if (clip == null)
        {
            Debug.LogError("[FirstPage] Missing VideoClip at " + path);
        }

        return clip;
    }

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null)
        {
            Debug.LogWarning("[FirstPage] missing serialized property: " + prop);
            return;
        }

        p.objectReferenceValue = value;
    }

    private static void RegisterAtIndexZero(string path)
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        existing.RemoveAll(s => s.path == path);
        existing.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = existing.ToArray();
    }
}
