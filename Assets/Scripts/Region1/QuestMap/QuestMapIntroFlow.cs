using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class QuestMapIntroFlow : MonoBehaviour
{
    private const string QuestMap1SceneName = "quest_map1";
    private const string SetupName = "QuestMapIntroFlowSetup";
    private const string FadeCanvasName = "QuestMap Fade Canvas";
    private const string BearQuestSceneName = "cut_scene1";

    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float delayAfterFade = 0.12f;
    [SerializeField] private float dimFadeDuration = 0.75f;
    [SerializeField] private Color dimTint = new Color(0.02f, 0.03f, 0.06f, 0.72f);
    [SerializeField] private int dimOverlaySortingOrder = 9000;
    [SerializeField] private Vector2Int dimOverlayTextureSize = new Vector2Int(960, 540);
    [SerializeField] [Range(0.02f, 0.45f)] private float cutoutFeather = 0.22f;
    [SerializeField] private float stoneRevealDuration = 0.65f;
    [SerializeField] private float signRevealDuration = 0.55f;
    [SerializeField] private float delayBetweenQuestPointReveals = 0.35f;
    [SerializeField] private float delayAfterAllStonesBeforeSigns = 0.4f;
    [SerializeField] private float delayBeforeRestoringFullBrightness = 2f;
    [SerializeField] private float dimRestoreDuration = 0.45f;
    [SerializeField] private float revealStartScale = 0.72f;
    [SerializeField] private float questionBobHeight = 0.11f;
    [SerializeField] private float questionBobDuration = 1.45f;
    [SerializeField] private float questClickBoundsPadding = 0.12f;
    [SerializeField] private float delayAfterQuestClickBeforeFocus = 0.24f;
    [SerializeField] private float questClickBounceDuration = 0.38f;
    [SerializeField] private float questClickBounceScale = 1.12f;
    [SerializeField] private float selectedQuestDimFadeDuration = 0.32f;
    [SerializeField] private float selectedQuestHoldBeforeZoom = 0.3f;
    [SerializeField] private float selectedQuestZoomDuration = 1f;
    [SerializeField] private float selectedQuestZoomOrthographicSize = 2.75f;
    [SerializeField] private Vector2 selectedQuestCameraOffset = new Vector2(0f, -0.15f);
    [SerializeField] private float selectedQuestHoldAfterZoom = 0.35f;
    [SerializeField] private int selectedQuestWorldOverlayTextureWidth = 640;
    [SerializeField] private float sceneFadeInDuration = 0.55f;
    [SerializeField] private float sceneFadeHoldDuration = 0.08f;
    [SerializeField] private Color sceneFadeColor = Color.black;

    [Header("Map View Shift")]
    [SerializeField] private bool playMapViewShift = true;
    [SerializeField] private bool showQuestPointsAfterViewShift = false;
    [SerializeField] private string isometricBackgroundName = "background";
    [SerializeField] private string topViewBackgroundName = "background (1)";
    [SerializeField] private float mapViewHoldDuration = 2f;
    [SerializeField] private float mapViewShiftDuration = 1.7f;
    [SerializeField] private float mapViewStartOrthographicSize = 0f;
    [SerializeField] private float mapViewEndOrthographicSize = 0f;
    [SerializeField] private float mapViewFrameBleed = 0.04f;
    [SerializeField] private Vector2 mapViewStartCameraOffset = Vector2.zero;
    [SerializeField] private Vector2 mapViewEndCameraOffset = Vector2.zero;
    [SerializeField] private float isometricStartScaleMultiplier = 1.035f;
    [SerializeField] private float topViewStartScaleMultiplier = 1.06f;
    [SerializeField] private Vector2 isometricStartLocalOffset = new Vector2(0f, -0.16f);
    [SerializeField] private Vector2 topViewStartLocalOffset = new Vector2(0f, 0.16f);
    [SerializeField] private Color mapViewVeilColor = new Color(0.82f, 0.94f, 1f, 1f);
    [SerializeField] [Range(0f, 0.45f)] private float mapViewVeilMaxAlpha = 0.22f;
    [SerializeField] private int topViewSortingOffset = 1;

    [Header("Fox Intro Character")]
    [SerializeField] private bool spawnFoxAfterViewShift = false;
    [SerializeField] private string foxPrefabResourcesPath = "QuestMap/Fox";
    [SerializeField] private string foxPrefabAssetPath = "Assets/TeddySuitKid/Assets/Prefabs/Fox.prefab";
    [SerializeField] private Vector3 foxStartWorldPosition = new Vector3(0.18f, -2.35f, -0.55f);
    [SerializeField] private Vector3 foxEndWorldPosition = new Vector3(0.18f, -0.85f, -0.55f);
    [SerializeField] private Vector3 foxRotationEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private float foxWorldScale = 0.48f;
    [SerializeField] private float foxWalkDuration = 2.65f;
    [SerializeField] private float foxSpawnPopDuration = 0.3f;
    [SerializeField] private float foxIntroIdleDuration = 0.45f;
    [SerializeField] private float foxCameraZoomDuration = 1.25f;
    [SerializeField] private float foxCameraZoomOrthographicSize = 2.5f;
    [SerializeField] private float foxTalkTurnDuration = 0.55f;
    [SerializeField] private Vector3 foxTalkRotationEuler = Vector3.zero;
    [SerializeField] private float foxTalkDuration = 4f;
    [SerializeField] private string foxTalkText = "Let's go!";
    [SerializeField] private Vector2 foxTalkBubbleSize = new Vector2(330f, 108f);
    [SerializeField] private Vector2 foxTalkBubbleScreenOffset = new Vector2(0f, 118f);
    [SerializeField] private float foxTalkBubblePopDuration = 0.24f;
    [SerializeField] private float foxCameraFullMapZoomOutDuration = 1.05f;
    [SerializeField] private float foxCameraFullMapHoldDuration = 0.35f;
    [SerializeField] private float foxCameraGameplayZoomInDuration = 0.9f;
    [SerializeField] private float foxCameraGameplayOrthographicSize = 3.65f;
    [SerializeField] private Vector2 foxCameraFocusOffset = new Vector2(0f, 0.72f);
    [SerializeField] private Vector2 foxCameraFollowOffset = new Vector2(0f, 0.72f);
    [SerializeField] private float foxCameraFollowSharpness = 10f;
    [SerializeField] private bool enableGameplayCameraControls = true;
    [SerializeField] private float gameplayCameraMinOrthographicSize = 2.35f;
    [SerializeField] private float gameplayCameraMaxOrthographicSize = 0f;
    [SerializeField] private float gameplayCameraCloseOrthographicSize = 2.75f;
    [SerializeField] private float gameplayCameraOverviewOrthographicSize = 4.55f;
    [SerializeField] private float gameplayCameraScrollZoomStep = 0.35f;
    [SerializeField] private float gameplayCameraKeyboardZoomSpeed = 1.8f;
    [SerializeField] private float gameplayCameraPinchZoomSensitivity = 0.006f;
    [SerializeField] private float gameplayCameraZoomSharpness = 12f;
    [SerializeField] private Vector2 foxShadowSize = new Vector2(0.82f, 0.24f);
    [SerializeField] private Color foxShadowColor = new Color(0f, 0f, 0f, 0.24f);
    [SerializeField] private int foxShadowSortingOrder = 35;
    [SerializeField] private int foxRenderLayer = 8;
    [SerializeField] private float foxOverlayCameraDepthOffset = 10f;
    [SerializeField] private Vector3 foxLightRotationEuler = new Vector3(46f, -28f, 0f);
    [SerializeField] private float foxLightIntensity = 0.55f;
    [SerializeField] private bool forceFoxQuestMapMaterials = true;
    [SerializeField] private Color foxMaterialTint = Color.white;

    private readonly List<QuestIntroPoint> questPoints = new List<QuestIntroPoint>();
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> originalLocalPositions = new Dictionary<Transform, Vector3>();
    private bool canSelectQuest;
    private bool isTransitioningToQuest;
    private Bounds mapBackgroundBounds;
    private bool hasMapBackgroundBounds;
    private SpriteRenderer isometricBackgroundRenderer;
    private SpriteRenderer topViewBackgroundRenderer;
    private Color isometricBackgroundColor = Color.white;
    private Color topViewBackgroundColor = Color.white;
    private Vector3 isometricBackgroundLocalScale = Vector3.one;
    private Vector3 topViewBackgroundLocalScale = Vector3.one;
    private Vector3 isometricBackgroundLocalPosition = Vector3.zero;
    private Vector3 topViewBackgroundLocalPosition = Vector3.zero;
    private Vector3 mapViewBaseCameraPosition = new Vector3(0f, 0f, -10f);
    private float mapViewBaseOrthographicSize = 5f;
    private bool hasCachedMapViewState;
    private Camera foxOverlayCamera;
    private Coroutine foxOverlayCameraSyncRoutine;
    private Transform gameplayCameraTarget;
    private float gameplayCameraTargetOrthographicSize = 3.65f;
    private bool gameplayCameraControlsEnabled;
    private float lastTouchPinchDistance = -1f;
    private static Sprite sharedFoxShadowSprite;
    private static Sprite sharedFoxTalkBubbleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SetupCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupCurrentScene();
    }

    private static void SetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != QuestMap1SceneName || GameObject.Find(SetupName) != null)
        {
            return;
        }

        GameObject setupObject = new GameObject(SetupName);
        setupObject.AddComponent<QuestMapIntroFlow>();
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartCoroutine(IntroRoutine());
        }
    }

    private void Update()
    {
        HandleGameplayCameraControls();

        if (!canSelectQuest || isTransitioningToQuest)
        {
            return;
        }

        if (!TryGetPointerPress(out Vector2 screenPosition))
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        QuestIntroPoint selectedPoint = FindQuestPointAt(worldPosition, out SpriteRenderer selectedMarker);
        if (selectedPoint == null || string.IsNullOrWhiteSpace(selectedPoint.NextSceneName))
        {
            return;
        }

        StartCoroutine(OpenQuestRoutine(selectedPoint, selectedMarker));
    }

    private IEnumerator IntroRoutine()
    {
        yield return null;

        CacheSceneState();
        BuildQuestPoints();
        PrepareQuestPointsHidden();
        PrepareMapViewShiftStartState();

        yield return WaitForFadeInToFinish();

        if (delayAfterFade > 0f)
        {
            yield return new WaitForSeconds(delayAfterFade);
        }

        yield return PlayMapViewShift();
        yield return SpawnFoxAfterViewShiftRoutine();

        if (!showQuestPointsAfterViewShift)
        {
            canSelectQuest = false;
            yield break;
        }

        Image dimOverlay = CreateDimCutoutOverlay();
        yield return FadeDimOverlay(dimOverlay);
        yield return RevealMagicStonesSequentially();

        if (delayAfterAllStonesBeforeSigns > 0f)
        {
            yield return new WaitForSeconds(delayAfterAllStonesBeforeSigns);
        }

        yield return RevealQuestionSignsTogether();

        if (delayBeforeRestoringFullBrightness > 0f)
        {
            yield return new WaitForSeconds(delayBeforeRestoringFullBrightness);
        }

        yield return FadeDimOverlay(dimOverlay, 0f, dimRestoreDuration);
        DestroyDimOverlay(dimOverlay);
        canSelectQuest = true;
    }

    private void CacheSceneState()
    {
        originalColors.Clear();
        originalScales.Clear();
        originalLocalPositions.Clear();
        CacheMapViewState();
        CacheMapBackgroundBounds();

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            originalColors[renderer] = renderer.color;
            Transform transform = renderer.transform;
            if (transform != null)
            {
                originalScales[transform] = transform.localScale;
                originalLocalPositions[transform] = transform.localPosition;
            }
        }
    }

    private void BuildQuestPoints()
    {
        questPoints.Clear();

        QuestIntroPoint bearPoint = new QuestIntroPoint
        {
            Name = "Bear Quest",
            NextSceneName = BearQuestSceneName,
            Sign = FindRendererNear(new Vector2(-5.72f, -0.14f), 0.35f),
            Stone = FindRendererByName("ChatGPT Image Jun 2, 2026, 04_02_27 PM"),
            ExtraFocusRenderer = FindRendererByName("bear_01_0"),
            FocusCenter = new Vector2(-5.35f, -1.35f),
            FocusSize = new Vector2(5.95f, 4.85f)
        };

        QuestIntroPoint potionPoint = new QuestIntroPoint
        {
            Name = "Potion Quest",
            Sign = FindRendererNear(new Vector2(-1.33f, -0.91f), 0.35f),
            Stone = FindRendererByName("ChatGPT Image Jun 2, 2026, 04_03_04 PM"),
            ExtraFocusRenderer = FindRendererByName("ChatGPT Image Jun 2, 2026, 04_24_00 PM"),
            FocusCenter = new Vector2(0.05f, -1.35f),
            FocusSize = new Vector2(4.95f, 4.55f)
        };

        QuestIntroPoint plantPoint = new QuestIntroPoint
        {
            Name = "Plant Quest",
            Sign = FindRendererNear(new Vector2(4.73f, -2.44f), 0.35f),
            Stone = FindRendererByName("ChatGPT Image Jun 2, 2026, 04_02_48 PM"),
            FocusCenter = new Vector2(5.35f, -1.65f),
            FocusSize = new Vector2(5.45f, 4.55f)
        };

        AddQuestPoint(bearPoint);
        AddQuestPoint(potionPoint);
        AddQuestPoint(plantPoint);
    }

    private void AddQuestPoint(QuestIntroPoint point)
    {
        if (point.Sign == null && point.Stone == null)
        {
            return;
        }

        point.SignBaseScale = GetOriginalScale(point.Sign != null ? point.Sign.transform : null);
        point.StoneBaseScale = GetOriginalScale(point.Stone != null ? point.Stone.transform : null);
        point.SignBaseLocalPosition = GetOriginalLocalPosition(point.Sign != null ? point.Sign.transform : null);
        questPoints.Add(point);
    }

    private void PrepareQuestPointsHidden()
    {
        for (int i = 0; i < questPoints.Count; i++)
        {
            QuestIntroPoint point = questPoints[i];
            HideRendererForReveal(point.Stone, point.StoneBaseScale);
            HideRendererForReveal(point.Sign, point.SignBaseScale);
        }
    }

    private IEnumerator WaitForFadeInToFinish()
    {
        yield return null;

        while (GameObject.Find(FadeCanvasName) != null)
        {
            yield return null;
        }
    }

    private void PrepareMapViewShiftStartState()
    {
        if (!playMapViewShift)
        {
            return;
        }

        if (!hasCachedMapViewState)
        {
            CacheMapViewState();
            CacheMapBackgroundBounds();
        }

        if (isometricBackgroundRenderer == null || topViewBackgroundRenderer == null)
        {
            return;
        }

        isometricBackgroundRenderer.gameObject.SetActive(true);
        topViewBackgroundRenderer.gameObject.SetActive(true);
        topViewBackgroundRenderer.sortingLayerID = isometricBackgroundRenderer.sortingLayerID;
        topViewBackgroundRenderer.sortingOrder = isometricBackgroundRenderer.sortingOrder + topViewSortingOffset;

        isometricBackgroundRenderer.transform.localPosition = GetOffsetLocalPosition(isometricBackgroundLocalPosition, isometricStartLocalOffset);
        topViewBackgroundRenderer.transform.localPosition = GetOffsetLocalPosition(topViewBackgroundLocalPosition, topViewStartLocalOffset);
        isometricBackgroundRenderer.transform.localScale = isometricBackgroundLocalScale * Mathf.Max(0.01f, isometricStartScaleMultiplier);
        topViewBackgroundRenderer.transform.localScale = topViewBackgroundLocalScale * Mathf.Max(0.01f, topViewStartScaleMultiplier);
        SetRendererAlpha(isometricBackgroundRenderer, isometricBackgroundColor, isometricBackgroundColor.a);
        SetRendererAlpha(topViewBackgroundRenderer, topViewBackgroundColor, 0f);

        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            return;
        }

        float endSize = ResolveMapViewOrthographicSize(camera, mapViewEndOrthographicSize);
        float startSize = ResolveMapViewOrthographicSize(camera, mapViewStartOrthographicSize);

        camera.orthographicSize = startSize;
        camera.transform.position = ClampCameraPositionToBackground(
            camera,
            GetMapViewCameraPosition(mapViewStartCameraOffset),
            startSize);
    }

    private void CacheMapViewState()
    {
        if (hasCachedMapViewState)
        {
            return;
        }

        isometricBackgroundRenderer = FindMapRendererByName(isometricBackgroundName);
        topViewBackgroundRenderer = FindMapRendererByName(topViewBackgroundName);

        if (isometricBackgroundRenderer != null)
        {
            isometricBackgroundColor = isometricBackgroundRenderer.color;
            isometricBackgroundLocalScale = isometricBackgroundRenderer.transform.localScale;
            isometricBackgroundLocalPosition = isometricBackgroundRenderer.transform.localPosition;
        }

        if (topViewBackgroundRenderer != null)
        {
            topViewBackgroundColor = topViewBackgroundRenderer.color;
            topViewBackgroundLocalScale = topViewBackgroundRenderer.transform.localScale;
            topViewBackgroundLocalPosition = topViewBackgroundRenderer.transform.localPosition;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            mapViewBaseCameraPosition = camera.transform.position;
            mapViewBaseOrthographicSize = camera.orthographicSize;
        }

        hasCachedMapViewState = true;
    }

    private IEnumerator PlayMapViewShift()
    {
        if (!playMapViewShift)
        {
            yield break;
        }

        if (!hasCachedMapViewState)
        {
            CacheMapViewState();
            CacheMapBackgroundBounds();
        }

        if (isometricBackgroundRenderer == null || topViewBackgroundRenderer == null)
        {
            yield break;
        }

        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            SetMapViewFinalState();
            yield break;
        }

        topViewBackgroundRenderer.gameObject.SetActive(true);
        isometricBackgroundRenderer.gameObject.SetActive(true);
        topViewBackgroundRenderer.sortingLayerID = isometricBackgroundRenderer.sortingLayerID;
        topViewBackgroundRenderer.sortingOrder = isometricBackgroundRenderer.sortingOrder + topViewSortingOffset;

        Vector3 startPosition = GetMapViewCameraPosition(mapViewStartCameraOffset);
        Vector3 endPosition = GetMapViewCameraPosition(mapViewEndCameraOffset);
        float endSize = ResolveMapViewOrthographicSize(camera, mapViewEndOrthographicSize);
        float startSize = ResolveMapViewOrthographicSize(camera, mapViewStartOrthographicSize);

        camera.orthographicSize = startSize;
        camera.transform.position = ClampCameraPositionToBackground(camera, startPosition, startSize);

        Vector3 isometricStartLocalPosition = GetOffsetLocalPosition(isometricBackgroundLocalPosition, isometricStartLocalOffset);
        Vector3 topViewStartLocalPosition = GetOffsetLocalPosition(topViewBackgroundLocalPosition, topViewStartLocalOffset);

        isometricBackgroundRenderer.transform.localPosition = isometricStartLocalPosition;
        topViewBackgroundRenderer.transform.localPosition = topViewStartLocalPosition;
        isometricBackgroundRenderer.transform.localScale = isometricBackgroundLocalScale * Mathf.Max(0.01f, isometricStartScaleMultiplier);
        topViewBackgroundRenderer.transform.localScale = topViewBackgroundLocalScale * Mathf.Max(0.01f, topViewStartScaleMultiplier);
        SetRendererAlpha(isometricBackgroundRenderer, isometricBackgroundColor, isometricBackgroundColor.a);
        SetRendererAlpha(topViewBackgroundRenderer, topViewBackgroundColor, 0f);

        if (mapViewHoldDuration > 0f)
        {
            yield return new WaitForSeconds(mapViewHoldDuration);
        }

        Image veil = CreateMapViewVeil();
        float duration = Mathf.Max(0.01f, mapViewShiftDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = SmootherStep(normalized);
            float blend = SmootherStep(Mathf.InverseLerp(0.16f, 0.86f, normalized));

            float currentSize = Mathf.LerpUnclamped(startSize, endSize, eased);
            camera.orthographicSize = currentSize;
            camera.transform.position = ClampCameraPositionToBackground(
                camera,
                Vector3.LerpUnclamped(startPosition, endPosition, eased),
                currentSize);

            isometricBackgroundRenderer.transform.localScale = Vector3.LerpUnclamped(
                isometricBackgroundLocalScale * Mathf.Max(0.01f, isometricStartScaleMultiplier),
                isometricBackgroundLocalScale * 0.99f,
                eased);
            topViewBackgroundRenderer.transform.localScale = Vector3.LerpUnclamped(
                topViewBackgroundLocalScale * Mathf.Max(0.01f, topViewStartScaleMultiplier),
                topViewBackgroundLocalScale,
                eased);
            isometricBackgroundRenderer.transform.localPosition = Vector3.LerpUnclamped(
                isometricStartLocalPosition,
                isometricBackgroundLocalPosition,
                eased);
            topViewBackgroundRenderer.transform.localPosition = Vector3.LerpUnclamped(
                topViewStartLocalPosition,
                topViewBackgroundLocalPosition,
                eased);

            SetRendererAlpha(isometricBackgroundRenderer, isometricBackgroundColor, isometricBackgroundColor.a * (1f - blend));
            SetRendererAlpha(topViewBackgroundRenderer, topViewBackgroundColor, topViewBackgroundColor.a * blend);

            if (veil != null)
            {
                float veilPulse = Mathf.Sin(normalized * Mathf.PI);
                veil.color = new Color(mapViewVeilColor.r, mapViewVeilColor.g, mapViewVeilColor.b, mapViewVeilMaxAlpha * veilPulse);
            }

            yield return null;
        }

        camera.orthographicSize = endSize;
        camera.transform.position = ClampCameraPositionToBackground(camera, endPosition, endSize);
        SetMapViewFinalState();
        DestroyDimOverlay(veil);
        CacheMapBackgroundBounds();
    }

    private void SetMapViewFinalState()
    {
        if (isometricBackgroundRenderer != null)
        {
            isometricBackgroundRenderer.transform.localPosition = isometricBackgroundLocalPosition;
            isometricBackgroundRenderer.transform.localScale = isometricBackgroundLocalScale;
            SetRendererAlpha(isometricBackgroundRenderer, isometricBackgroundColor, 0f);
        }

        if (topViewBackgroundRenderer != null)
        {
            topViewBackgroundRenderer.gameObject.SetActive(true);
            topViewBackgroundRenderer.transform.localPosition = topViewBackgroundLocalPosition;
            topViewBackgroundRenderer.transform.localScale = topViewBackgroundLocalScale;
            SetRendererAlpha(topViewBackgroundRenderer, topViewBackgroundColor, topViewBackgroundColor.a);
        }
    }

    private Vector3 GetMapViewCameraPosition(Vector2 offset)
    {
        Vector3 basePosition = mapViewBaseCameraPosition;
        if (hasMapBackgroundBounds && mapBackgroundBounds.size.sqrMagnitude > 0f)
        {
            basePosition.x = mapBackgroundBounds.center.x;
            basePosition.y = mapBackgroundBounds.center.y;
        }

        basePosition.x += offset.x;
        basePosition.y += offset.y;
        return basePosition;
    }

    private float ResolveMapViewOrthographicSize(Camera camera, float requestedSize)
    {
        float size = requestedSize > 0f ? requestedSize : mapViewBaseOrthographicSize;
        if (camera == null || !hasMapBackgroundBounds || mapBackgroundBounds.size.sqrMagnitude <= 0f)
        {
            return Mathf.Max(0.01f, size);
        }

        float aspect = Mathf.Max(0.01f, camera.aspect);
        float fillHeight = mapBackgroundBounds.extents.y;
        float fillWidth = mapBackgroundBounds.extents.x / aspect;
        float frameFillSize = Mathf.Min(fillHeight, fillWidth) - Mathf.Max(0f, mapViewFrameBleed);
        return requestedSize > 0f
            ? Mathf.Max(0.01f, requestedSize)
            : Mathf.Max(0.01f, frameFillSize);
    }

    private Image CreateMapViewVeil()
    {
        if (mapViewVeilMaxAlpha <= 0f)
        {
            return null;
        }

        GameObject canvasObject = new GameObject("QuestMap View Shift Veil", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(0, dimOverlaySortingOrder - 25);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new GameObject("View Shift Glow", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(mapViewVeilColor.r, mapViewVeilColor.g, mapViewVeilColor.b, 0f);
        return image;
    }

    private SpriteRenderer FindMapRendererByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null && activeObject.TryGetComponent(out SpriteRenderer activeRenderer))
        {
            return activeRenderer;
        }

        Transform mapRoot = FindMapRoot();
        Transform found = mapRoot != null ? mapRoot.Find(objectName) : null;
        return found != null ? found.GetComponent<SpriteRenderer>() : null;
    }

    private Transform FindMapRoot()
    {
        GameObject mapRoot = GameObject.Find("map_root");
        return mapRoot != null ? mapRoot.transform : null;
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, Color baseColor, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(alpha));
    }

    private static Vector3 GetOffsetLocalPosition(Vector3 basePosition, Vector2 offset)
    {
        return new Vector3(basePosition.x + offset.x, basePosition.y + offset.y, basePosition.z);
    }

    private IEnumerator SpawnFoxAfterViewShiftRoutine()
    {
        if (!spawnFoxAfterViewShift)
        {
            yield break;
        }

        GameObject foxPrefab = LoadFoxPrefab();
        if (foxPrefab == null)
        {
            Debug.LogWarning("Quest map fox intro could not find Fox.prefab.");
            yield break;
        }

        Camera overlayCamera = EnsureFoxOverlayCamera();
        EnsureFoxLight();

        Transform parent = FindMapRoot();
        GameObject fox = Instantiate(foxPrefab, foxStartWorldPosition, Quaternion.Euler(foxRotationEuler), parent);
        fox.name = "QuestMap Fox Intro";
        SetLayerRecursively(fox, GetSafeFoxRenderLayer());
        fox.transform.position = foxStartWorldPosition;
        fox.transform.rotation = Quaternion.Euler(foxRotationEuler);

        Vector3 targetScale = Vector3.one * Mathf.Max(0.01f, foxWorldScale);
        Vector3 spawnScale = targetScale * 0.72f;
        fox.transform.localScale = spawnScale;

        PrepareFoxAnimators(fox, false);
        PrepareFoxRenderers(fox);
        DisableFoxDemoBehaviours(fox);

        if (overlayCamera != null)
        {
            CopyFoxOverlayCameraFromMain(overlayCamera);
        }

        SpriteRenderer shadow = CreateFoxShadow(parent, foxStartWorldPosition);
        yield return PopFoxIntoScene(fox.transform, shadow, spawnScale, targetScale);
        yield return HoldFoxIdle(fox, foxIntroIdleDuration);
        yield return ZoomCameraToFox(fox.transform);
        yield return TurnFoxForTalk(fox.transform);
        yield return ShowFoxTalkMoment(fox.transform);
        yield return TurnFoxBackForWalk(fox.transform);
        yield return ZoomCameraOutToGameplayMap(fox.transform);
        BeginGameplayCameraControls(fox.transform);
        yield return WalkFoxWithCameraFollow(fox.transform, shadow);
    }

    private void PrepareFoxAnimators(GameObject fox, bool isWalking)
    {
        if (fox == null)
        {
            return;
        }

        Animator[] animators = fox.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            SetFoxAnimatorWalking(animator, isWalking);
        }
    }

    private void PrepareFoxRenderers(GameObject fox)
    {
        if (fox == null)
        {
            return;
        }

        int layer = GetSafeFoxRenderLayer();
        Renderer[] renderers = fox.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Quest map fox intro spawned, but no renderers were found on the prefab.");
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.gameObject.SetActive(true);
            renderer.gameObject.layer = layer;
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = foxShadowSortingOrder + 1;

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                skinnedMeshRenderer.updateWhenOffscreen = true;
                skinnedMeshRenderer.localBounds = ExpandFoxLocalBounds(skinnedMeshRenderer.localBounds);
            }

            if (forceFoxQuestMapMaterials)
            {
                ApplyQuestMapVisibleMaterials(renderer);
            }
        }
    }

    private void DisableFoxDemoBehaviours(GameObject fox)
    {
        if (fox == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = fox.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !IsFoxDemoBehaviour(behaviour))
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private static bool IsFoxDemoBehaviour(MonoBehaviour behaviour)
    {
        string typeName = behaviour.GetType().Name;
        return typeName == "WovenKidExample" || typeName == "BackPackExample";
    }

    private static Bounds ExpandFoxLocalBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x, 2.5f);
        size.y = Mathf.Max(size.y, 2.5f);
        size.z = Mathf.Max(size.z, 2.5f);
        return new Bounds(bounds.center, size);
    }

    private void ApplyQuestMapVisibleMaterials(Renderer renderer)
    {
        Material[] sourceMaterials = renderer.sharedMaterials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            return;
        }

        Material[] visibleMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            visibleMaterials[i] = CreateQuestMapVisibleMaterial(sourceMaterials[i]);
        }

        renderer.materials = visibleMaterials;
    }

    private Material CreateQuestMapVisibleMaterial(Material sourceMaterial)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return sourceMaterial;
        }

        Material visibleMaterial = new Material(shader);
        visibleMaterial.name = sourceMaterial != null
            ? sourceMaterial.name + " QuestMap Visible"
            : "QuestMap Fox Visible";

        Texture mainTexture = FindMainTexture(sourceMaterial);
        if (mainTexture != null)
        {
            if (visibleMaterial.HasProperty("_BaseMap"))
            {
                visibleMaterial.SetTexture("_BaseMap", mainTexture);
            }

            if (visibleMaterial.HasProperty("_MainTex"))
            {
                visibleMaterial.SetTexture("_MainTex", mainTexture);
            }
        }

        Color tint = ResolveFoxMaterialTint(sourceMaterial);
        if (visibleMaterial.HasProperty("_BaseColor"))
        {
            visibleMaterial.SetColor("_BaseColor", tint);
        }

        if (visibleMaterial.HasProperty("_Color"))
        {
            visibleMaterial.SetColor("_Color", tint);
        }

        return visibleMaterial;
    }

    private Color ResolveFoxMaterialTint(Material sourceMaterial)
    {
        Color tint = foxMaterialTint;
        if (sourceMaterial != null && sourceMaterial.HasProperty("_Color"))
        {
            Color sourceTint = sourceMaterial.GetColor("_Color");
            tint = new Color(
                sourceTint.r * foxMaterialTint.r,
                sourceTint.g * foxMaterialTint.g,
                sourceTint.b * foxMaterialTint.b,
                sourceTint.a * foxMaterialTint.a);
        }

        return tint;
    }

    private static Texture FindMainTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseMap"))
        {
            Texture texture = material.GetTexture("_BaseMap");
            if (texture != null)
            {
                return texture;
            }
        }

        if (material.HasProperty("_MainTex"))
        {
            Texture texture = material.GetTexture("_MainTex");
            if (texture != null)
            {
                return texture;
            }
        }

        return material.mainTexture;
    }

    private Camera EnsureFoxOverlayCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return null;
        }

        if (foxOverlayCamera == null)
        {
            GameObject existing = GameObject.Find("QuestMap Fox Overlay Camera");
            if (existing != null)
            {
                foxOverlayCamera = existing.GetComponent<Camera>();
            }
        }

        if (foxOverlayCamera == null)
        {
            GameObject cameraObject = new GameObject("QuestMap Fox Overlay Camera");
            foxOverlayCamera = cameraObject.AddComponent<Camera>();
        }

        foxOverlayCamera.enabled = true;
        foxOverlayCamera.orthographic = true;
        foxOverlayCamera.clearFlags = CameraClearFlags.Nothing;
        foxOverlayCamera.cullingMask = 1 << GetSafeFoxRenderLayer();
        foxOverlayCamera.depth = mainCamera.depth + foxOverlayCameraDepthOffset;
        CopyFoxOverlayCameraFromMain(foxOverlayCamera);
        ConfigureFoxOverlayCameraForUrp(mainCamera, foxOverlayCamera);

        if (foxOverlayCameraSyncRoutine == null)
        {
            foxOverlayCameraSyncRoutine = StartCoroutine(SyncFoxOverlayCameraRoutine());
        }

        return foxOverlayCamera;
    }

    private void ConfigureFoxOverlayCameraForUrp(Camera mainCamera, Camera overlayCamera)
    {
        if (mainCamera == null || overlayCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData mainData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (mainData == null)
        {
            return;
        }

        UniversalAdditionalCameraData overlayData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();
        if (overlayData == null)
        {
            overlayData = overlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        mainData.renderType = CameraRenderType.Base;
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;

        if (!mainData.cameraStack.Contains(overlayCamera))
        {
            mainData.cameraStack.Add(overlayCamera);
        }
    }

    private IEnumerator SyncFoxOverlayCameraRoutine()
    {
        while (foxOverlayCamera != null)
        {
            CopyFoxOverlayCameraFromMain(foxOverlayCamera);
            yield return null;
        }

        foxOverlayCameraSyncRoutine = null;
    }

    private void CopyFoxOverlayCameraFromMain(Camera overlayCamera)
    {
        Camera mainCamera = Camera.main;
        if (overlayCamera == null || mainCamera == null)
        {
            return;
        }

        overlayCamera.transform.position = mainCamera.transform.position;
        overlayCamera.transform.rotation = mainCamera.transform.rotation;
        overlayCamera.orthographic = mainCamera.orthographic;
        overlayCamera.orthographicSize = mainCamera.orthographicSize;
        overlayCamera.fieldOfView = mainCamera.fieldOfView;
        overlayCamera.nearClipPlane = mainCamera.nearClipPlane;
        overlayCamera.farClipPlane = mainCamera.farClipPlane;
        overlayCamera.backgroundColor = Color.clear;
        overlayCamera.allowHDR = mainCamera.allowHDR;
        overlayCamera.allowMSAA = mainCamera.allowMSAA;
    }

    private void EnsureFoxLight()
    {
        GameObject lightObject = GameObject.Find("QuestMap Fox Key Light");
        Light keyLight = lightObject != null ? lightObject.GetComponent<Light>() : null;
        if (keyLight == null)
        {
            lightObject = new GameObject("QuestMap Fox Key Light");
            keyLight = lightObject.AddComponent<Light>();
        }

        keyLight.type = LightType.Directional;
        keyLight.intensity = Mathf.Max(0f, foxLightIntensity);
        keyLight.color = Color.white;
        keyLight.cullingMask = 1 << GetSafeFoxRenderLayer();
        keyLight.transform.rotation = Quaternion.Euler(foxLightRotationEuler);
    }

    private int GetSafeFoxRenderLayer()
    {
        return Mathf.Clamp(foxRenderLayer, 0, 31);
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        Transform transform = target.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }

    private GameObject LoadFoxPrefab()
    {
        GameObject prefab = null;
        if (!string.IsNullOrWhiteSpace(foxPrefabResourcesPath))
        {
            prefab = Resources.Load<GameObject>(foxPrefabResourcesPath);
        }

#if UNITY_EDITOR
        if (prefab == null && !string.IsNullOrWhiteSpace(foxPrefabAssetPath))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(foxPrefabAssetPath);
        }
#endif

        return prefab;
    }

    private void SetFoxAnimatorWalking(Animator animator, bool isWalking)
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorBoolIfExists(animator, "Walk", isWalking);
        SetAnimatorBoolIfExists(animator, "Run", false);
        SetAnimatorBoolIfExists(animator, "Idle", !isWalking);
        SetAnimatorBoolIfExists(animator, "tailSwagWalk", isWalking);
        SetAnimatorBoolIfExists(animator, "tailSwagRun", false);
        SetAnimatorBoolIfExists(animator, "tailSwagIdle", !isWalking);

        int stateHash = Animator.StringToHash(isWalking ? "Walk" : "Idle");
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFade(stateHash, 0.08f);
        }
        else
        {
            int backpackStateHash = Animator.StringToHash(isWalking ? "tailSwagWalk" : "tailSwagIdle");
            if (animator.HasState(0, backpackStateHash))
            {
                animator.CrossFade(backpackStateHash, 0.08f);
            }
        }
    }

    private static void SetAnimatorBoolIfExists(Animator animator, string parameterName, bool value)
    {
        if (!HasAnimatorBool(animator, parameterName))
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    private static bool HasAnimatorBool(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private SpriteRenderer CreateFoxShadow(Transform parent, Vector3 worldPosition)
    {
        Sprite shadowSprite = GetFoxShadowSprite();
        if (shadowSprite == null)
        {
            return null;
        }

        GameObject shadowObject = new GameObject("QuestMap Fox Shadow");
        shadowObject.transform.SetParent(parent, true);
        shadowObject.layer = GetSafeFoxRenderLayer();
        shadowObject.transform.position = new Vector3(worldPosition.x, worldPosition.y - 0.12f, -0.48f);
        shadowObject.transform.localScale = new Vector3(foxShadowSize.x, foxShadowSize.y, 1f);

        SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
        shadow.sprite = shadowSprite;
        shadow.color = new Color(foxShadowColor.r, foxShadowColor.g, foxShadowColor.b, 0f);
        shadow.sortingOrder = foxShadowSortingOrder;
        return shadow;
    }

    private IEnumerator PopFoxIntoScene(Transform fox, SpriteRenderer shadow, Vector3 startScale, Vector3 targetScale)
    {
        if (fox == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, foxSpawnPopDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            fox.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            if (shadow != null)
            {
                shadow.color = new Color(foxShadowColor.r, foxShadowColor.g, foxShadowColor.b, foxShadowColor.a * t);
            }

            yield return null;
        }

        fox.localScale = targetScale;
        if (shadow != null)
        {
            shadow.color = foxShadowColor;
        }
    }

    private IEnumerator HoldFoxIdle(GameObject fox, float durationSeconds)
    {
        if (fox == null)
        {
            yield break;
        }

        SetFoxAnimatorsWalking(fox, false);
        float duration = Mathf.Max(0f, durationSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            yield return null;
        }
    }

    private IEnumerator ZoomCameraToFox(Transform fox)
    {
        Camera camera = Camera.main;
        if (fox == null || camera == null || !camera.orthographic)
        {
            yield break;
        }

        if (!hasMapBackgroundBounds)
        {
            CacheMapBackgroundBounds();
        }

        Transform cameraTransform = camera.transform;
        Vector3 startPosition = cameraTransform.position;
        float startSize = camera.orthographicSize;
        float targetSize = foxCameraZoomOrthographicSize > 0f
            ? Mathf.Min(startSize, foxCameraZoomOrthographicSize)
            : startSize;
        float duration = Mathf.Max(0.01f, foxCameraZoomDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            float currentSize = Mathf.LerpUnclamped(startSize, targetSize, t);
            Vector3 targetPosition = GetCameraPositionForFox(camera, fox.position, currentSize, foxCameraFocusOffset);

            camera.orthographicSize = currentSize;
            cameraTransform.position = ClampCameraPositionToBackground(
                camera,
                Vector3.LerpUnclamped(startPosition, targetPosition, t),
                currentSize);

            yield return null;
        }

        camera.orthographicSize = targetSize;
        cameraTransform.position = GetCameraPositionForFox(camera, fox.position, targetSize, foxCameraFocusOffset);
    }

    private IEnumerator TurnFoxForTalk(Transform fox)
    {
        return TurnFoxToRotation(fox, Quaternion.Euler(foxTalkRotationEuler), foxTalkTurnDuration);
    }

    private IEnumerator TurnFoxBackForWalk(Transform fox)
    {
        return TurnFoxToRotation(fox, Quaternion.Euler(foxRotationEuler), foxTalkTurnDuration);
    }

    private IEnumerator TurnFoxToRotation(Transform fox, Quaternion targetRotation, float durationSeconds)
    {
        if (fox == null)
        {
            yield break;
        }

        Quaternion startRotation = fox.rotation;
        float duration = Mathf.Max(0.01f, durationSeconds);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            fox.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        fox.rotation = targetRotation;
    }

    private IEnumerator ShowFoxTalkMoment(Transform fox)
    {
        if (fox == null)
        {
            yield break;
        }

        SetFoxAnimatorsWalking(fox.gameObject, false);
        GameObject canvasObject = CreateFoxTalkBubble(out RectTransform bubbleRect, out CanvasGroup canvasGroup);
        if (canvasObject == null || bubbleRect == null || canvasGroup == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, foxTalkDuration));
            yield break;
        }

        float duration = Mathf.Max(0.01f, foxTalkDuration);
        float popDuration = Mathf.Clamp(foxTalkBubblePopDuration, 0.01f, duration * 0.5f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float popIn = Mathf.Clamp01(elapsed / popDuration);
            float popOut = Mathf.Clamp01((duration - elapsed) / popDuration);
            float visibility = SmootherStep(Mathf.Min(popIn, popOut));
            float pulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 2.5f) * 0.018f * visibility;

            canvasGroup.alpha = visibility;
            bubbleRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, pulse, visibility);
            PositionTalkBubbleNearFox(bubbleRect, fox);
            yield return null;
        }

        Destroy(canvasObject);
    }

    private GameObject CreateFoxTalkBubble(out RectTransform bubbleRect, out CanvasGroup canvasGroup)
    {
        GameObject canvasObject = new GameObject("QuestMap Fox Talk Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject bubbleObject = new GameObject("Fox Talk Bubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        bubbleObject.transform.SetParent(canvasObject.transform, false);

        bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = foxTalkBubbleSize;
        bubbleRect.localScale = Vector3.one * 0.86f;

        canvasGroup = bubbleObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Image bubbleImage = bubbleObject.GetComponent<Image>();
        bubbleImage.raycastTarget = false;
        bubbleImage.sprite = GetFoxTalkBubbleSprite();
        bubbleImage.type = Image.Type.Sliced;
        bubbleImage.color = new Color(1f, 0.98f, 0.9f, 0.96f);

        GameObject textObject = new GameObject("Fox Talk Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(bubbleObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(28f, 20f);
        textRect.offsetMax = new Vector2(-28f, -20f);

        Text text = textObject.GetComponent<Text>();
        text.raycastTarget = false;
        text.text = foxTalkText;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.18f, 0.13f, 0.08f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font != null)
        {
            text.font = font;
        }

        return canvasObject;
    }

    private void PositionTalkBubbleNearFox(RectTransform bubbleRect, Transform fox)
    {
        Camera camera = Camera.main;
        if (bubbleRect == null || fox == null || camera == null)
        {
            return;
        }

        Canvas canvas = bubbleRect.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            return;
        }

        Vector3 screenPosition = camera.WorldToScreenPoint(fox.position);
        screenPosition.x += foxTalkBubbleScreenOffset.x;
        screenPosition.y += foxTalkBubbleScreenOffset.y;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
        {
            bubbleRect.anchoredPosition = localPoint;
        }
    }

    private IEnumerator ZoomCameraOutToGameplayMap(Transform fox)
    {
        Camera camera = Camera.main;
        if (fox == null || camera == null || !camera.orthographic)
        {
            yield break;
        }

        if (!hasMapBackgroundBounds)
        {
            CacheMapBackgroundBounds();
        }

        Transform cameraTransform = camera.transform;
        Vector3 startPosition = cameraTransform.position;
        float startSize = camera.orthographicSize;
        float fullMapSize = ResolveMapViewOrthographicSize(camera, mapViewEndOrthographicSize);
        Vector3 fullMapPosition = ClampCameraPositionToBackground(
            camera,
            GetMapViewCameraPosition(mapViewEndCameraOffset),
            fullMapSize);
        float requestedGameplaySize = foxCameraGameplayOrthographicSize > 0f
            ? foxCameraGameplayOrthographicSize
            : fullMapSize;
        float targetSize = Mathf.Clamp(requestedGameplaySize, startSize, fullMapSize);
        float zoomOutDuration = Mathf.Max(0.01f, foxCameraFullMapZoomOutDuration);

        for (float elapsed = 0f; elapsed < zoomOutDuration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / zoomOutDuration));
            float currentSize = Mathf.LerpUnclamped(startSize, fullMapSize, t);

            camera.orthographicSize = currentSize;
            cameraTransform.position = ClampCameraPositionToBackground(
                camera,
                Vector3.LerpUnclamped(startPosition, fullMapPosition, t),
                currentSize);

            yield return null;
        }

        camera.orthographicSize = fullMapSize;
        cameraTransform.position = fullMapPosition;

        if (foxCameraFullMapHoldDuration > 0f)
        {
            yield return new WaitForSeconds(foxCameraFullMapHoldDuration);
        }

        Vector3 gameplayStartPosition = cameraTransform.position;
        float gameplayStartSize = camera.orthographicSize;
        float zoomInDuration = Mathf.Max(0.01f, foxCameraGameplayZoomInDuration);
        for (float elapsed = 0f; elapsed < zoomInDuration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / zoomInDuration));
            float currentSize = Mathf.LerpUnclamped(gameplayStartSize, targetSize, t);
            Vector3 gameplayPosition = GetCameraPositionForFox(camera, fox.position, currentSize, foxCameraFollowOffset);

            camera.orthographicSize = currentSize;
            cameraTransform.position = ClampCameraPositionToBackground(
                camera,
                Vector3.LerpUnclamped(gameplayStartPosition, gameplayPosition, t),
                currentSize);

            yield return null;
        }

        camera.orthographicSize = targetSize;
        cameraTransform.position = GetCameraPositionForFox(camera, fox.position, targetSize, foxCameraFollowOffset);
    }

    private void BeginGameplayCameraControls(Transform target)
    {
        gameplayCameraTarget = target;
        Camera camera = Camera.main;
        gameplayCameraTargetOrthographicSize = camera != null
            ? camera.orthographicSize
            : Mathf.Max(0.01f, foxCameraGameplayOrthographicSize);
        gameplayCameraControlsEnabled = enableGameplayCameraControls && target != null;
        lastTouchPinchDistance = -1f;
    }

    private void HandleGameplayCameraControls()
    {
        if (!gameplayCameraControlsEnabled || gameplayCameraTarget == null || isTransitioningToQuest)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        if (!camera.orthographic)
        {
            return;
        }

        if (!hasMapBackgroundBounds)
        {
            CacheMapBackgroundBounds();
        }

        float minSize = Mathf.Max(0.1f, gameplayCameraMinOrthographicSize);
        float fullMapSize = ResolveMapViewOrthographicSize(camera, mapViewEndOrthographicSize);
        float maxSize = gameplayCameraMaxOrthographicSize > 0f
            ? Mathf.Min(gameplayCameraMaxOrthographicSize, fullMapSize)
            : fullMapSize;
        if (maxSize < minSize)
        {
            maxSize = minSize;
        }

        ApplyGameplayCameraInput(minSize, maxSize, fullMapSize);
        gameplayCameraTargetOrthographicSize = Mathf.Clamp(gameplayCameraTargetOrthographicSize, minSize, maxSize);

        float zoomAmount = 1f - Mathf.Exp(-Mathf.Max(0.01f, gameplayCameraZoomSharpness) * Time.deltaTime);
        camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, gameplayCameraTargetOrthographicSize, zoomAmount);
        FollowCameraTowardFox(gameplayCameraTarget);
    }

    private void ApplyGameplayCameraInput(float minSize, float maxSize, float fullMapSize)
    {
        float zoomDelta = ReadGameplayZoomInput();
        if (Mathf.Abs(zoomDelta) > 0.0001f)
        {
            gameplayCameraTargetOrthographicSize = Mathf.Clamp(
                gameplayCameraTargetOrthographicSize + zoomDelta,
                minSize,
                maxSize);
        }

        if (WasGameplayCameraClosePressed())
        {
            gameplayCameraTargetOrthographicSize = Mathf.Clamp(gameplayCameraCloseOrthographicSize, minSize, maxSize);
        }
        else if (WasGameplayCameraNormalPressed())
        {
            gameplayCameraTargetOrthographicSize = Mathf.Clamp(foxCameraGameplayOrthographicSize, minSize, maxSize);
        }
        else if (WasGameplayCameraOverviewPressed())
        {
            float overviewSize = gameplayCameraOverviewOrthographicSize > 0f
                ? gameplayCameraOverviewOrthographicSize
                : fullMapSize;
            gameplayCameraTargetOrthographicSize = Mathf.Clamp(overviewSize, minSize, maxSize);
        }
        else if (WasGameplayCameraResetPressed())
        {
            gameplayCameraTargetOrthographicSize = Mathf.Clamp(foxCameraGameplayOrthographicSize, minSize, maxSize);
        }
    }

    private float ReadGameplayZoomInput()
    {
        float zoomDelta = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float scrollSteps = Mathf.Clamp(scrollY / 120f, -3f, 3f);
                zoomDelta -= scrollSteps * Mathf.Max(0.01f, gameplayCameraScrollZoomStep);
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.equalsKey.isPressed || Keyboard.current.numpadPlusKey.isPressed)
            {
                zoomDelta -= gameplayCameraKeyboardZoomSpeed * Time.deltaTime;
            }

            if (Keyboard.current.minusKey.isPressed || Keyboard.current.numpadMinusKey.isPressed)
            {
                zoomDelta += gameplayCameraKeyboardZoomSpeed * Time.deltaTime;
            }
        }

        zoomDelta += ReadTouchPinchZoomDelta();
#else
        float scrollY = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            zoomDelta -= scrollY * Mathf.Max(0.01f, gameplayCameraScrollZoomStep);
        }

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
        {
            zoomDelta -= gameplayCameraKeyboardZoomSpeed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
        {
            zoomDelta += gameplayCameraKeyboardZoomSpeed * Time.deltaTime;
        }
#endif

        return zoomDelta;
    }

#if ENABLE_INPUT_SYSTEM
    private float ReadTouchPinchZoomDelta()
    {
        if (Touchscreen.current == null)
        {
            lastTouchPinchDistance = -1f;
            return 0f;
        }

        TouchControl touch0 = Touchscreen.current.touches.Count > 0 ? Touchscreen.current.touches[0] : null;
        TouchControl touch1 = Touchscreen.current.touches.Count > 1 ? Touchscreen.current.touches[1] : null;
        if (touch0 == null || touch1 == null || !touch0.press.isPressed || !touch1.press.isPressed)
        {
            lastTouchPinchDistance = -1f;
            return 0f;
        }

        Vector2 position0 = touch0.position.ReadValue();
        Vector2 position1 = touch1.position.ReadValue();
        float distance = Vector2.Distance(position0, position1);
        if (lastTouchPinchDistance < 0f)
        {
            lastTouchPinchDistance = distance;
            return 0f;
        }

        float delta = lastTouchPinchDistance - distance;
        lastTouchPinchDistance = distance;
        return delta * Mathf.Max(0.0001f, gameplayCameraPinchZoomSensitivity);
    }
#endif

    private static bool WasGameplayCameraClosePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Alpha1);
#endif
    }

    private static bool WasGameplayCameraNormalPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Alpha2);
#endif
    }

    private static bool WasGameplayCameraOverviewPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Alpha3);
#endif
    }

    private static bool WasGameplayCameraResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private IEnumerator WalkFoxWithCameraFollow(Transform fox, SpriteRenderer shadow)
    {
        if (fox == null)
        {
            yield break;
        }

        GameObject foxObject = fox.gameObject;
        SetFoxAnimatorsWalking(foxObject, true);

        Vector3 startPosition = fox.position;
        Vector3 endPosition = foxEndWorldPosition;
        float duration = Mathf.Max(0.01f, foxWalkDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            fox.position = Vector3.LerpUnclamped(startPosition, endPosition, t);
            UpdateFoxShadowPosition(shadow, fox.position);
            FollowCameraTowardFox(fox);
            yield return null;
        }

        fox.position = endPosition;
        UpdateFoxShadowPosition(shadow, endPosition);
        FollowCameraTowardFox(fox, true);
        SetFoxAnimatorsWalking(foxObject, false);
    }

    private void SetFoxAnimatorsWalking(GameObject fox, bool isWalking)
    {
        if (fox == null)
        {
            return;
        }

        Animator[] animators = fox.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            SetFoxAnimatorWalking(animators[i], isWalking);
        }
    }

    private void FollowCameraTowardFox(Transform fox, bool snap = false)
    {
        Camera camera = Camera.main;
        if (fox == null || camera == null || !camera.orthographic)
        {
            return;
        }

        Vector3 targetPosition = GetCameraPositionForFox(
            camera,
            fox.position,
            camera.orthographicSize,
            foxCameraFollowOffset);

        if (snap)
        {
            camera.transform.position = targetPosition;
            return;
        }

        float followAmount = 1f - Mathf.Exp(-Mathf.Max(0.01f, foxCameraFollowSharpness) * Time.deltaTime);
        camera.transform.position = Vector3.LerpUnclamped(camera.transform.position, targetPosition, followAmount);
    }

    private Vector3 GetCameraPositionForFox(Camera camera, Vector3 foxPosition, float orthographicSize, Vector2 offset)
    {
        Vector3 cameraPosition = camera != null ? camera.transform.position : mapViewBaseCameraPosition;
        Vector3 desiredPosition = new Vector3(
            foxPosition.x + offset.x,
            foxPosition.y + offset.y,
            cameraPosition.z);

        return ClampCameraPositionToBackground(camera, desiredPosition, orthographicSize);
    }

    private static void UpdateFoxShadowPosition(SpriteRenderer shadow, Vector3 foxPosition)
    {
        if (shadow == null)
        {
            return;
        }

        shadow.transform.position = new Vector3(foxPosition.x, foxPosition.y - 0.12f, -0.48f);
    }

    private static Sprite GetFoxShadowSprite()
    {
        if (sharedFoxShadowSprite != null)
        {
            return sharedFoxShadowSprite;
        }

        const int width = 128;
        const int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Fox Soft Shadow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float normalizedY = ((y + 0.5f) / height - 0.5f) * 2f;
            for (int x = 0; x < width; x++)
            {
                float normalizedX = ((x + 0.5f) / width - 0.5f) * 2f;
                float radius = normalizedX * normalizedX + normalizedY * normalizedY;
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.18f, 1f, radius));
                alpha *= alpha;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        sharedFoxShadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            height);
        sharedFoxShadowSprite.name = "Runtime Fox Soft Shadow";
        sharedFoxShadowSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFoxShadowSprite;
    }

    private static Sprite GetFoxTalkBubbleSprite()
    {
        if (sharedFoxTalkBubbleSprite != null)
        {
            return sharedFoxTalkBubbleSprite;
        }

        const int width = 256;
        const int height = 128;
        const float radius = 30f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Fox Talk Bubble",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color fill = new Color(1f, 0.98f, 0.9f, 1f);
        Color outline = new Color(0.38f, 0.23f, 0.1f, 1f);
        Color shadow = new Color(0f, 0f, 0f, 0.13f);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = RoundedRectSignedDistance(
                    new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(width * 0.5f, height * 0.5f + 2f),
                    new Vector2(width * 0.5f - 13f, height * 0.5f - 18f),
                    radius);

                Color color = clear;
                if (distance <= 0f)
                {
                    color = distance > -5f ? outline : fill;
                }
                else if (distance < 6f)
                {
                    color = new Color(shadow.r, shadow.g, shadow.b, shadow.a * (1f - distance / 6f));
                }

                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        sharedFoxTalkBubbleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(42f, 42f, 42f, 42f));
        sharedFoxTalkBubbleSprite.name = "Runtime Fox Talk Bubble";
        sharedFoxTalkBubbleSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFoxTalkBubbleSprite;
    }

    private static float RoundedRectSignedDistance(Vector2 point, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 distance = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - (halfSize - Vector2.one * radius);
        return new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude
            + Mathf.Min(Mathf.Max(distance.x, distance.y), 0f)
            - radius;
    }

    private Image CreateDimCutoutOverlay(QuestIntroPoint selectedPoint = null)
    {
        List<QuestIntroPoint> visiblePoints = new List<QuestIntroPoint>();
        if (selectedPoint != null)
        {
            visiblePoints.Add(selectedPoint);
        }
        else
        {
            visiblePoints.AddRange(questPoints);
        }

        return CreateDimCutoutOverlayWithPoints(visiblePoints);
    }

    private Image CreateDimCutoutOverlayWithPoints(IList<QuestIntroPoint> visiblePoints)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return null;
        }

        Texture2D texture = CreateDimCutoutTexture(camera, visiblePoints, null, 0f);
        if (texture == null)
        {
            return null;
        }

        GameObject canvasObject = new GameObject("QuestMap Dim Cutout Overlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = dimOverlaySortingOrder;

        GameObject imageObject = new GameObject("Dim Cutout", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private Texture2D CreateDimCutoutTexture(
        Camera camera,
        IList<QuestIntroPoint> visiblePoints,
        QuestIntroPoint revealingPoint,
        float revealingAmount)
    {
        int width = Mathf.Max(16, dimOverlayTextureSize.x);
        int height = Mathf.Max(16, dimOverlayTextureSize.y);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Quest Map Dim Cutout",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        FillDimCutoutTexture(texture, camera, visiblePoints, revealingPoint, revealingAmount);
        return texture;
    }

    private void UpdateDimCutoutOverlay(
        Image overlay,
        IList<QuestIntroPoint> visiblePoints,
        QuestIntroPoint revealingPoint,
        float revealingAmount)
    {
        if (overlay == null || overlay.sprite == null || overlay.sprite.texture == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        FillDimCutoutTexture(overlay.sprite.texture, camera, visiblePoints, revealingPoint, revealingAmount);
    }

    private SpriteRenderer CreateWorldDimCutoutOverlay(QuestIntroPoint selectedPoint)
    {
        if (selectedPoint == null)
        {
            return null;
        }

        if (!hasMapBackgroundBounds)
        {
            CacheMapBackgroundBounds();
        }

        if (!hasMapBackgroundBounds || mapBackgroundBounds.size.sqrMagnitude <= 0f)
        {
            return null;
        }

        Texture2D texture = CreateWorldDimCutoutTexture(selectedPoint, mapBackgroundBounds);
        if (texture == null)
        {
            return null;
        }

        const float pixelsPerUnit = 100f;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = "Runtime Quest Map World Dim Cutout";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        GameObject overlayObject = new GameObject("QuestMap World Dim Cutout Overlay");
        overlayObject.transform.position = new Vector3(mapBackgroundBounds.center.x, mapBackgroundBounds.center.y, 0f);
        overlayObject.transform.localScale = new Vector3(
            mapBackgroundBounds.size.x / Mathf.Max(0.01f, texture.width / pixelsPerUnit),
            mapBackgroundBounds.size.y / Mathf.Max(0.01f, texture.height / pixelsPerUnit),
            1f);

        SpriteRenderer renderer = overlayObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = dimOverlaySortingOrder;
        renderer.color = new Color(1f, 1f, 1f, 0f);
        return renderer;
    }

    private Texture2D CreateWorldDimCutoutTexture(QuestIntroPoint selectedPoint, Bounds bounds)
    {
        int width = Mathf.Max(128, selectedQuestWorldOverlayTextureWidth);
        int height = Mathf.Max(128, Mathf.RoundToInt(width * bounds.size.y / Mathf.Max(0.01f, bounds.size.x)));
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Quest Map World Dim Cutout",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        Color overlayColor = dimTint;
        for (int y = 0; y < height; y++)
        {
            float worldY = Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / height);
            for (int x = 0; x < width; x++)
            {
                float worldX = Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / width);
                float alphaFactor = GetWorldCutoutAlphaFactor(new Vector2(worldX, worldY), selectedPoint);
                pixels[y * width + x] = new Color(
                    overlayColor.r,
                    overlayColor.g,
                    overlayColor.b,
                    overlayColor.a * alphaFactor);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private float GetWorldCutoutAlphaFactor(Vector2 worldPoint, QuestIntroPoint selectedPoint)
    {
        if (selectedPoint == null)
        {
            return 1f;
        }

        float feather = Mathf.Clamp(cutoutFeather, 0.02f, 0.45f);
        float innerRadius = Mathf.Clamp01(1f - feather);
        float normalizedX = (worldPoint.x - selectedPoint.FocusCenter.x) / Mathf.Max(0.001f, selectedPoint.FocusSize.x * 0.5f);
        float normalizedY = (worldPoint.y - selectedPoint.FocusCenter.y) / Mathf.Max(0.001f, selectedPoint.FocusSize.y * 0.5f);
        float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
        return distance <= innerRadius
            ? 0f
            : SmootherStep(Mathf.InverseLerp(innerRadius, 1f, distance));
    }

    private void FillDimCutoutTexture(
        Texture2D texture,
        Camera camera,
        IList<QuestIntroPoint> visiblePoints,
        QuestIntroPoint revealingPoint,
        float revealingAmount)
    {
        if (texture == null || camera == null)
        {
            return;
        }

        int width = texture.width;
        int height = texture.height;
        List<CutoutArea> cutouts = GetCutoutAreas(camera, visiblePoints, revealingPoint, revealingAmount);
        Color[] pixels = new Color[width * height];
        Color overlayColor = dimTint;

        for (int y = 0; y < height; y++)
        {
            float viewportY = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float viewportX = (x + 0.5f) / width;
                float alphaFactor = GetCutoutAlphaFactor(new Vector2(viewportX, viewportY), cutouts);
                pixels[y * width + x] = new Color(
                    overlayColor.r,
                    overlayColor.g,
                    overlayColor.b,
                    overlayColor.a * alphaFactor);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
    }

    private List<CutoutArea> GetCutoutAreas(
        Camera camera,
        IList<QuestIntroPoint> visiblePoints,
        QuestIntroPoint revealingPoint,
        float revealingAmount)
    {
        List<CutoutArea> cutouts = new List<CutoutArea>();
        if (visiblePoints != null)
        {
            for (int i = 0; i < visiblePoints.Count; i++)
            {
                AddCutoutArea(camera, visiblePoints[i], 1f, cutouts);
            }
        }

        if (revealingPoint != null && revealingAmount > 0f)
        {
            AddCutoutArea(camera, revealingPoint, Mathf.Clamp01(revealingAmount), cutouts);
        }

        return cutouts;
    }

    private static void AddCutoutArea(Camera camera, QuestIntroPoint point, float revealAmount, List<CutoutArea> cutouts)
    {
        if (camera == null || point == null || cutouts == null)
        {
            return;
        }

        Vector3 centerWorld = new Vector3(point.FocusCenter.x, point.FocusCenter.y, 0f);
        Vector2 center = camera.WorldToViewportPoint(centerWorld);
        Vector2 radius = GetViewportRadius(camera, point.FocusCenter, point.FocusSize);
        if (radius.x <= 0f || radius.y <= 0f)
        {
            return;
        }

        cutouts.Add(new CutoutArea
        {
            Center = center,
            Radius = radius,
            RevealAmount = Mathf.Clamp01(revealAmount)
        });
    }

    private static Vector2 GetViewportRadius(Camera camera, Vector2 center, Vector2 size)
    {
        Vector3 centerViewport = camera.WorldToViewportPoint(new Vector3(center.x, center.y, 0f));
        Vector3 horizontalViewport = camera.WorldToViewportPoint(new Vector3(center.x + size.x * 0.5f, center.y, 0f));
        Vector3 verticalViewport = camera.WorldToViewportPoint(new Vector3(center.x, center.y + size.y * 0.5f, 0f));
        return new Vector2(
            Mathf.Abs(horizontalViewport.x - centerViewport.x),
            Mathf.Abs(verticalViewport.y - centerViewport.y));
    }

    private float GetCutoutAlphaFactor(Vector2 viewportPoint, List<CutoutArea> cutouts)
    {
        float alphaFactor = 1f;
        float feather = Mathf.Clamp(cutoutFeather, 0.02f, 0.45f);
        float innerRadius = Mathf.Clamp01(1f - feather);

        for (int i = 0; i < cutouts.Count; i++)
        {
            CutoutArea cutout = cutouts[i];
            float revealAmount = SmootherStep(cutout.RevealAmount);
            if (revealAmount <= 0f)
            {
                continue;
            }

            float radiusScale = Mathf.Lerp(0.58f, 1f, revealAmount);
            float normalizedX = (viewportPoint.x - cutout.Center.x) / Mathf.Max(0.001f, cutout.Radius.x * radiusScale);
            float normalizedY = (viewportPoint.y - cutout.Center.y) / Mathf.Max(0.001f, cutout.Radius.y * radiusScale);
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            float cutoutAlpha = distance <= innerRadius
                ? 0f
                : SmootherStep(Mathf.InverseLerp(innerRadius, 1f, distance));
            alphaFactor = Mathf.Min(alphaFactor, Mathf.Lerp(1f, cutoutAlpha, revealAmount));
        }

        return alphaFactor;
    }

    private IEnumerator FadeDimOverlay(Image overlay)
    {
        yield return FadeDimOverlay(overlay, 1f, dimFadeDuration);
    }

    private IEnumerator FadeDimOverlay(Image overlay, float targetAlpha, float durationSeconds)
    {
        if (overlay == null)
        {
            yield break;
        }

        float startAlpha = overlay.color.a;
        float endAlpha = Mathf.Clamp01(targetAlpha);
        float duration = Mathf.Max(0.01f, durationSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            overlay.color = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, t));
            yield return null;
        }

        overlay.color = new Color(1f, 1f, 1f, endAlpha);
    }

    private IEnumerator FadeWorldDimOverlay(SpriteRenderer overlay, float targetAlpha, float durationSeconds)
    {
        if (overlay == null)
        {
            yield break;
        }

        float startAlpha = overlay.color.a;
        float endAlpha = Mathf.Clamp01(targetAlpha);
        float duration = Mathf.Max(0.01f, durationSeconds);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            overlay.color = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, t));
            yield return null;
        }

        overlay.color = new Color(1f, 1f, 1f, endAlpha);
    }

    private static void DestroyDimOverlay(Image overlay)
    {
        if (overlay == null)
        {
            return;
        }

        Transform root = overlay.transform.root;
        if (root != null)
        {
            Destroy(root.gameObject);
        }
    }

    private IEnumerator RevealRenderer(SpriteRenderer renderer, Vector3 baseScale, float durationSeconds, float startScaleMultiplier)
    {
        if (renderer == null)
        {
            yield break;
        }

        Transform target = renderer.transform;
        Color original = GetOriginalColor(renderer);
        float duration = Mathf.Max(0.01f, durationSeconds);
        Vector3 startScale = baseScale * Mathf.Max(0.01f, startScaleMultiplier);

        target.localScale = startScale;
        renderer.color = new Color(original.r, original.g, original.b, 0f);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(startScale, baseScale, t);
            renderer.color = new Color(original.r, original.g, original.b, original.a * t);
            yield return null;
        }

        target.localScale = baseScale;
        renderer.color = original;
    }

    private IEnumerator RevealMagicStonesSequentially()
    {
        for (int i = 0; i < questPoints.Count; i++)
        {
            QuestIntroPoint point = questPoints[i];
            if (point.Stone == null)
            {
                continue;
            }

            yield return RevealRenderer(point.Stone, point.StoneBaseScale, stoneRevealDuration, revealStartScale);
            if (i < questPoints.Count - 1 && delayBetweenQuestPointReveals > 0f)
            {
                yield return new WaitForSeconds(delayBetweenQuestPointReveals);
            }
        }
    }

    private IEnumerator RevealQuestionSignsTogether()
    {
        List<QuestIntroPoint> signPoints = new List<QuestIntroPoint>();
        for (int i = 0; i < questPoints.Count; i++)
        {
            QuestIntroPoint point = questPoints[i];
            if (point.Sign == null)
            {
                continue;
            }

            Transform target = point.Sign.transform;
            Vector3 startScale = point.SignBaseScale * Mathf.Max(0.01f, revealStartScale);
            Color original = GetOriginalColor(point.Sign);
            target.localScale = startScale;
            point.Sign.color = new Color(original.r, original.g, original.b, 0f);
            signPoints.Add(point);
        }

        if (signPoints.Count == 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, signRevealDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < signPoints.Count; i++)
            {
                QuestIntroPoint point = signPoints[i];
                Color original = GetOriginalColor(point.Sign);
                point.Sign.transform.localScale = Vector3.LerpUnclamped(
                    point.SignBaseScale * Mathf.Max(0.01f, revealStartScale),
                    point.SignBaseScale,
                    t);
                point.Sign.color = new Color(original.r, original.g, original.b, original.a * t);
            }

            yield return null;
        }

        for (int i = 0; i < signPoints.Count; i++)
        {
            QuestIntroPoint point = signPoints[i];
            point.Sign.transform.localScale = point.SignBaseScale;
            point.Sign.color = GetOriginalColor(point.Sign);
            StartCoroutine(BobQuestionSign(point.Sign.transform, point.SignBaseLocalPosition));
        }
    }

    private IEnumerator BobQuestionSign(Transform target, Vector3 baseLocalPosition)
    {
        if (target == null)
        {
            yield break;
        }

        float cycleDuration = Mathf.Max(0.1f, questionBobDuration);
        float height = Mathf.Max(0f, questionBobHeight);
        float clock = 0f;

        while (target != null)
        {
            clock += Time.deltaTime;
            float y = Mathf.Sin(clock / cycleDuration * Mathf.PI * 2f) * height;
            target.localPosition = baseLocalPosition + Vector3.up * y;
            yield return null;
        }
    }

    private bool TryGetPointerPress(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private QuestIntroPoint FindQuestPointAt(Vector3 worldPosition, out SpriteRenderer clickedMarker)
    {
        Vector2 clickPoint = new Vector2(worldPosition.x, worldPosition.y);
        for (int i = 0; i < questPoints.Count; i++)
        {
            QuestIntroPoint point = questPoints[i];
            if (IsClickOnQuestMarker(point.Stone, clickPoint))
            {
                clickedMarker = point.Stone;
                return point;
            }

            if (IsClickOnQuestMarker(point.Sign, clickPoint))
            {
                clickedMarker = point.Sign;
                return point;
            }
        }

        clickedMarker = null;
        return null;
    }

    private bool IsClickOnQuestMarker(SpriteRenderer renderer, Vector2 clickPoint)
    {
        if (renderer == null)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        float padding = Mathf.Max(0f, questClickBoundsPadding);
        bounds.Expand(new Vector3(padding, padding, 0.01f));
        return bounds.Contains(new Vector3(clickPoint.x, clickPoint.y, bounds.center.z));
    }

    private IEnumerator OpenQuestRoutine(QuestIntroPoint point, SpriteRenderer clickedMarker)
    {
        if (point == null || string.IsNullOrWhiteSpace(point.NextSceneName))
        {
            yield break;
        }

        isTransitioningToQuest = true;
        canSelectQuest = false;
        if (clickedMarker != null)
        {
            StartCoroutine(BounceQuestMarker(clickedMarker, point));
        }

        if (delayAfterQuestClickBeforeFocus > 0f)
        {
            yield return new WaitForSeconds(delayAfterQuestClickBeforeFocus);
        }

        SpriteRenderer selectedOverlay = CreateWorldDimCutoutOverlay(point);
        if (selectedOverlay != null)
        {
            yield return FadeWorldDimOverlay(selectedOverlay, 1f, selectedQuestDimFadeDuration);
        }
        else
        {
            Image fallbackOverlay = CreateDimCutoutOverlay(point);
            yield return FadeDimOverlay(fallbackOverlay, 1f, selectedQuestDimFadeDuration);
        }

        if (selectedQuestHoldBeforeZoom > 0f)
        {
            yield return new WaitForSeconds(selectedQuestHoldBeforeZoom);
        }

        yield return ZoomCameraToQuest(point);

        if (selectedQuestHoldAfterZoom > 0f)
        {
            yield return new WaitForSeconds(selectedQuestHoldAfterZoom);
        }

        yield return FadeToNextScene();
        SceneManager.LoadScene(point.NextSceneName);
    }

    private IEnumerator BounceQuestMarker(SpriteRenderer renderer, QuestIntroPoint point)
    {
        if (renderer == null)
        {
            yield break;
        }

        Transform target = renderer.transform;
        Vector3 baseScale = GetMarkerBaseScale(renderer, point);
        Vector3 peakScale = baseScale * Mathf.Max(1f, questClickBounceScale);
        Vector3 settleScale = new Vector3(baseScale.x * 0.98f, baseScale.y * 1.02f, baseScale.z);
        float duration = Mathf.Max(0.01f, questClickBounceDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            if (normalized < 0.38f)
            {
                float t = SmootherStep(normalized / 0.38f);
                target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, t);
            }
            else if (normalized < 0.68f)
            {
                float t = SmootherStep((normalized - 0.38f) / 0.3f);
                target.localScale = Vector3.LerpUnclamped(peakScale, settleScale, t);
            }
            else
            {
                float t = SmootherStep((normalized - 0.68f) / 0.32f);
                target.localScale = Vector3.LerpUnclamped(settleScale, baseScale, t);
            }

            yield return null;
        }

        if (target != null)
        {
            target.localScale = baseScale;
        }
    }

    private Vector3 GetMarkerBaseScale(SpriteRenderer renderer, QuestIntroPoint point)
    {
        if (renderer == null)
        {
            return Vector3.one;
        }

        if (point != null)
        {
            if (renderer == point.Sign)
            {
                return point.SignBaseScale;
            }

            if (renderer == point.Stone)
            {
                return point.StoneBaseScale;
            }
        }

        return GetOriginalScale(renderer.transform);
    }

    private IEnumerator ZoomCameraToQuest(QuestIntroPoint point)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic || point == null)
        {
            yield break;
        }

        if (!hasMapBackgroundBounds)
        {
            CacheMapBackgroundBounds();
        }

        Transform cameraTransform = camera.transform;
        Vector3 startPosition = cameraTransform.position;
        Vector3 targetPosition = new Vector3(
            point.FocusCenter.x + selectedQuestCameraOffset.x,
            point.FocusCenter.y + selectedQuestCameraOffset.y,
            startPosition.z);
        float startSize = camera.orthographicSize;
        float targetSize = Mathf.Clamp(selectedQuestZoomOrthographicSize, 0.75f, startSize);
        float duration = Mathf.Max(0.01f, selectedQuestZoomDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            float currentSize = Mathf.LerpUnclamped(startSize, targetSize, t);
            camera.orthographicSize = currentSize;
            cameraTransform.position = ClampCameraPositionToBackground(
                camera,
                Vector3.LerpUnclamped(startPosition, targetPosition, t),
                currentSize);

            yield return null;
        }

        camera.orthographicSize = targetSize;
        cameraTransform.position = ClampCameraPositionToBackground(camera, targetPosition, targetSize);
    }

    private Vector3 ClampCameraPositionToBackground(Camera camera, Vector3 desiredPosition, float orthographicSize)
    {
        if (camera == null)
        {
            return desiredPosition;
        }

        if (!hasMapBackgroundBounds || mapBackgroundBounds.size.sqrMagnitude <= 0f)
        {
            return desiredPosition;
        }

        float halfHeight = Mathf.Max(0.01f, orthographicSize);
        float halfWidth = halfHeight * camera.aspect;
        float minX = mapBackgroundBounds.min.x + halfWidth;
        float maxX = mapBackgroundBounds.max.x - halfWidth;
        float minY = mapBackgroundBounds.min.y + halfHeight;
        float maxY = mapBackgroundBounds.max.y - halfHeight;

        if (minX > maxX)
        {
            desiredPosition.x = mapBackgroundBounds.center.x;
        }
        else
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            desiredPosition.y = mapBackgroundBounds.center.y;
        }
        else
        {
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        return desiredPosition;
    }

    private void CacheMapBackgroundBounds()
    {
        SpriteRenderer topRenderer = FindMapRendererByName(topViewBackgroundName);
        SpriteRenderer isoRenderer = FindMapRendererByName(isometricBackgroundName);
        SpriteRenderer backgroundRenderer = null;

        if (topRenderer != null && topRenderer.gameObject.activeInHierarchy && topRenderer.color.a > 0.01f)
        {
            backgroundRenderer = topRenderer;
        }

        if (backgroundRenderer == null && isoRenderer != null)
        {
            backgroundRenderer = isoRenderer;
        }

        if (backgroundRenderer == null)
        {
            backgroundRenderer = topRenderer;
        }

        if (backgroundRenderer != null)
        {
            mapBackgroundBounds = backgroundRenderer.bounds;
            hasMapBackgroundBounds = true;
            return;
        }

        mapBackgroundBounds = new Bounds(Vector3.zero, Vector3.zero);
        hasMapBackgroundBounds = false;
    }

    private IEnumerator FadeToNextScene()
    {
        Image fadeImage = CreateSceneFadeImage();
        if (fadeImage == null)
        {
            yield break;
        }

        Color color = sceneFadeColor;
        color.a = 0f;
        fadeImage.color = color;

        float duration = Mathf.Max(0.01f, sceneFadeInDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmootherStep(Mathf.Clamp01(elapsed / duration));
            color.a = t;
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;

        if (sceneFadeHoldDuration > 0f)
        {
            yield return new WaitForSeconds(sceneFadeHoldDuration);
        }
    }

    private Image CreateSceneFadeImage()
    {
        GameObject canvasObject = new GameObject("QuestMap Scene Fade Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new GameObject("Scene Fade", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void HideRendererForReveal(SpriteRenderer renderer, Vector3 baseScale)
    {
        if (renderer == null)
        {
            return;
        }

        Color original = GetOriginalColor(renderer);
        renderer.color = new Color(original.r, original.g, original.b, 0f);
        renderer.transform.localScale = baseScale * Mathf.Max(0.01f, revealStartScale);
    }

    private bool IsFocusRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        for (int i = 0; i < questPoints.Count; i++)
        {
            QuestIntroPoint point = questPoints[i];
            if (renderer == point.Sign || renderer == point.Stone || renderer == point.ExtraFocusRenderer)
            {
                return true;
            }
        }

        return false;
    }

    private SpriteRenderer FindRendererByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject found = GameObject.Find(objectName);
        return found != null ? found.GetComponent<SpriteRenderer>() : null;
    }

    private SpriteRenderer FindRendererNear(Vector2 position, float radius)
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>();
        SpriteRenderer best = null;
        float bestDistance = Mathf.Max(0.01f, radius);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            float distance = Vector2.Distance(position, renderer.transform.position);
            if (distance <= bestDistance)
            {
                best = renderer;
                bestDistance = distance;
            }
        }

        return best;
    }

    private Vector3 GetOriginalScale(Transform target)
    {
        if (target == null)
        {
            return Vector3.one;
        }

        return originalScales.TryGetValue(target, out Vector3 scale) ? scale : target.localScale;
    }

    private Vector3 GetOriginalLocalPosition(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        return originalLocalPositions.TryGetValue(target, out Vector3 position) ? position : target.localPosition;
    }

    private Color GetOriginalColor(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return Color.white;
        }

        return originalColors.TryGetValue(renderer, out Color color) ? color : renderer.color;
    }

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private struct CutoutArea
    {
        public Vector2 Center;
        public Vector2 Radius;
        public float RevealAmount;
    }

    private sealed class QuestIntroPoint
    {
        public string Name;
        public string NextSceneName;
        public SpriteRenderer Sign;
        public SpriteRenderer Stone;
        public SpriteRenderer ExtraFocusRenderer;
        public Vector2 FocusCenter;
        public Vector2 FocusSize;
        public Vector3 SignBaseScale = Vector3.one;
        public Vector3 StoneBaseScale = Vector3.one;
        public Vector3 SignBaseLocalPosition = Vector3.zero;
    }
}
