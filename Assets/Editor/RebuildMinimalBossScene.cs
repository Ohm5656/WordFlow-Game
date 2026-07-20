using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Rebuilds Boss as a focused word-assembly scene with a video-only background.
/// </summary>
public static class RebuildMinimalBossScene
{
    private const string BossScenePath = "Assets/Scenes/region 1/boss.unity";
    private const string WordAssemblyPrefabPath = "Assets/Prefabs/Boss/WordAssembly.prefab";
    private const string VideoPath = "Assets/Art/boss/boss_background_pingpong.mp4";
    private const string RenderTexturePath = "Assets/Art/boss/BossBackground.renderTexture";
    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;

    [MenuItem("Tools/Boss/Rebuild Minimal Video Scene")]
    public static void Rebuild()
    {
        AssetDatabase.ImportAsset(VideoPath, ImportAssetOptions.ForceSynchronousImport);

        VideoClip videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
        if (videoClip == null)
        {
            Debug.LogError($"[RebuildMinimalBossScene] Video clip is unavailable at {VideoPath}.");
            return;
        }

        GameObject wordAssemblyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WordAssemblyPrefabPath);
        if (wordAssemblyPrefab == null)
        {
            Debug.LogError($"[RebuildMinimalBossScene] WordAssembly prefab is unavailable at {WordAssemblyPrefabPath}.");
            return;
        }

        RenderTexture renderTexture = GetOrCreateRenderTexture();
        if (renderTexture == null)
        {
            Debug.LogError($"[RebuildMinimalBossScene] RenderTexture could not be created at {RenderTexturePath}.");
            return;
        }

        Scene bossScene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        GameObject eventSystem = FindRootEventSystem(bossScene);
        ClearSceneExcept(bossScene, eventSystem);

        if (eventSystem == null)
        {
            eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, bossScene);
        }

        CreateCamera(bossScene);
        CreateVideoBackground(bossScene, videoClip, renderTexture);

        GameObject wordAssembly = (GameObject)PrefabUtility.InstantiatePrefab(wordAssemblyPrefab, bossScene);
        wordAssembly.name = "WordAssembly";
        wordAssembly.AddComponent<BossWordAssemblyStarter>();

        EditorSceneManager.MarkSceneDirty(bossScene);
        EditorSceneManager.SaveScene(bossScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RebuildMinimalBossScene] Boss now contains only EventSystem, video background, and WordAssembly.");
    }

    private static GameObject FindRootEventSystem(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponent<EventSystem>() != null)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static void ClearSceneExcept(Scene scene, GameObject preservedRoot)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != preservedRoot)
            {
                Object.DestroyImmediate(roots[i]);
            }
        }
    }

    private static RenderTexture GetOrCreateRenderTexture()
    {
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (renderTexture != null)
        {
            return renderTexture;
        }

        renderTexture = new RenderTexture(ReferenceWidth, ReferenceHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "BossBackground"
        };
        AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
        return renderTexture;
    }

    private static void CreateCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject(
            "Boss Camera",
            typeof(Camera),
            typeof(AudioListener));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
    }

    private static void CreateVideoBackground(Scene scene, VideoClip videoClip, RenderTexture renderTexture)
    {
        GameObject background = new GameObject(
            "Boss Video Background",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(background, scene);

        Canvas canvas = background.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -1000;

        CanvasScaler scaler = background.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject(
            "Video Image",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        imageObject.transform.SetParent(background.transform, false);

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = renderTexture;
        image.raycastTarget = false;

        AspectRatioFitter aspectRatioFitter = imageObject.GetComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectRatioFitter.aspectRatio = (float)videoClip.width / videoClip.height;

        VideoPlayer videoPlayer = background.AddComponent<VideoPlayer>();
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.playOnAwake = true;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }
}
