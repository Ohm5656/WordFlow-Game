using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using WordFlow.Adventure.Net;

/// <summary>
/// One-click builder for the first_page title screen scene. Idempotent:
/// re-running rebuilds the scene from scratch and re-registers it in
/// EditorBuildSettings at index 0. Run via Tools > FirstPage > Build Scene.
/// </summary>
public static class BuildFirstPageScene
{
    private const string ScenePath = "Assets/Scenes/first_page.unity";
    private const string ThaiFontPath = "Assets/Fonts/LeelawUI SDF.asset";
    private const string LogoFramesDir = "Assets/Art/login/WordFlow_logo_cropped";
    private const string LoginBtnPath = "Assets/Art/login/login_btn.png";
    private const string SignupBtnPath = "Assets/Art/login/signup_btn.png";

    [MenuItem("Tools/FirstPage/Build Scene")]
    public static void Build()
    {
        VideoClip introReverseClip = LoadClip("Assets/Video/intro_rev.mp4");
        VideoClip idleClip = LoadClip("Assets/Video/idle.mp4");
        VideoClip idleReverseClip = LoadClip("Assets/Video/idle_rev.mp4");
        RenderTexture rtA = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroA.renderTexture");
        RenderTexture rtB = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroB.renderTexture");
        TMP_FontAsset thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontPath);

        Sprite loginSprite = ImportSprite(LoginBtnPath, 1024);
        Sprite signupSprite = ImportSprite(SignupBtnPath, 1024);
        Sprite[] logoFrames = ImportLogoFrames();

        if (introReverseClip == null || idleClip == null || idleReverseClip == null
            || rtA == null || rtB == null || thaiFont == null
            || loginSprite == null || signupSprite == null || logoFrames.Length == 0)
        {
            Debug.LogError("[FirstPage] Missing a required asset — aborting.");
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
        (CanvasGroup logoGroup, RectTransform logoRect, UISpriteLoop spriteLoop, Image logoImage) =
            CreateLogo(canvas.transform, logoFrames[0]);
        (CanvasGroup authGroup, RectTransform authRect, Button loginBtn, Button signupBtn) =
            CreateAuthButtons(canvas.transform, loginSprite, signupSprite);
        CanvasGroup pressToStartGroup = CreatePressToStart(canvas.transform, thaiFont);
        (CanvasGroup logoutGroup, Button logoutBtn) = CreateLogoutButton(canvas.transform, thaiFont);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var authGO = new GameObject("Auth");
        var firebase = authGO.AddComponent<FirebaseAuthClient>();
        var session = authGO.AddComponent<AuthSession>();
        var sessionSo = new SerializedObject(session);
        SetRef(sessionSo, "auth", firebase);
        sessionSo.ApplyModifiedPropertiesWithoutUndo();

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
        SetRef(so, "introReverseClip", introReverseClip);
        SetRef(so, "idleClip", idleClip);
        SetRef(so, "idleReverseClip", idleReverseClip);
        SetRef(so, "logoGroup", logoGroup);
        SetRef(so, "logoRect", logoRect);
        SetRef(so, "pressToStartGroup", pressToStartGroup);
        SetRef(so, "pressToStartRect", pressToStartGroup.GetComponent<RectTransform>());
        SetRef(so, "logoutGroup", logoutGroup);
        SetRef(so, "logoutButton", logoutBtn);
        SetRef(so, "authButtonsGroup", authGroup);
        SetRef(so, "authButtonsRect", authRect);
        SetRef(so, "loginButton", loginBtn);
        SetRef(so, "signupButton", signupBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        var loopSo = new SerializedObject(spriteLoop);
        SetRef(loopSo, "target", logoImage);
        var framesProp = loopSo.FindProperty("frames");
        framesProp.arraySize = logoFrames.Length;
        for (int i = 0; i < logoFrames.Length; i++)
        {
            framesProp.GetArrayElementAtIndex(i).objectReferenceValue = logoFrames[i];
        }
        loopSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        // TMP resets a freshly AddComponent-ed text back to the default font when it
        // first initializes, so the font only sticks once the scene has been written
        // and reopened. Same post-pass ThaiTMPSetup uses for the Login scene.
        ApplyThaiFont(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single), thaiFont);

        RegisterAtIndexZero(ScenePath);

        Debug.Log("[FirstPage] Scene built and registered at build index 0.");
    }

    // ---- element builders ----

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

    private static (CanvasGroup, RectTransform, UISpriteLoop, Image) CreateLogo(Transform canvasTransform, Sprite firstFrame)
    {
        var go = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UISpriteLoop));
        go.transform.SetParent(canvasTransform, false);

        float aspect = firstFrame.rect.width / firstFrame.rect.height;
        const float width = 780f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -235f);
        rt.sizeDelta = new Vector2(width, width / aspect);

        var image = go.GetComponent<Image>();
        image.sprite = firstFrame;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        return (group, rt, go.GetComponent<UISpriteLoop>(), image);
    }

    private static (CanvasGroup, RectTransform, Button, Button) CreateAuthButtons(
        Transform canvasTransform, Sprite loginSprite, Sprite signupSprite)
    {
        var container = new GameObject("AuthButtons", typeof(RectTransform), typeof(CanvasGroup));
        container.transform.SetParent(canvasTransform, false);

        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 95f);
        rt.sizeDelta = new Vector2(900, 130);

        var group = container.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Button loginBtn = CreateSpriteButton(container.transform, "LoginButton", loginSprite, new Vector2(-185f, 0f));
        Button signupBtn = CreateSpriteButton(container.transform, "SignupButton", signupSprite, new Vector2(185f, 0f));

        return (group, rt, loginBtn, signupBtn);
    }

    private static Button CreateSpriteButton(Transform parent, string name, Sprite sprite, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        float aspect = sprite.rect.width / sprite.rect.height;
        const float height = 110f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(height * aspect, height);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        return button;
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

    private static (CanvasGroup, Button) CreateLogoutButton(Transform canvasTransform, TMP_FontAsset font)
    {
        var go = new GameObject("LogoutButton", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(190, 58);

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = "ออกจากระบบ";
        text.font = font;
        text.fontSize = 26;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return (group, go.GetComponent<Button>());
    }

    // ---- asset helpers ----

    private static Sprite ImportSprite(string path, int maxSize)
    {
        ApplySpriteImport(path, maxSize);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] ImportLogoFrames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LogoFramesDir });
        var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".png"))
                         .OrderBy(p => p, System.StringComparer.Ordinal)
                         .ToArray();

        foreach (var p in paths)
        {
            ApplySpriteImport(p, 1024);
        }

        return paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
    }

    private static void ApplySpriteImport(string path, int maxSize)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            return;
        }

        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (dirty)
        {
            ti.SaveAndReimport();
        }
    }

    private static void ApplyThaiFont(Scene scene, TMP_FontAsset font)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.font = font;
                EditorUtility.SetDirty(tmp);
            }
        }

        EditorSceneManager.SaveScene(scene);
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
