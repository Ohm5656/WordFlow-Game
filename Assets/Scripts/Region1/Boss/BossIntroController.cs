using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Boss-scene-only presentation controller.  It leaves the authored scene data untouched: when
/// the Boss scene is unloaded (or this script is removed), the original forest map and actors
/// are exactly as authored again.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class BossIntroController : MonoBehaviour
{
    private const string BossSceneName = "Boss";
    // The minimal video-backed boss scene deliberately has no legacy world presentation.
    // Keeping the old entrance opt-in prevents this runtime bootstrap from recreating it there.
    private const string LegacyIntroMarkerName = "BossLegacyIntro";
    private const int OverlaySortingOrder = 31980;
    private const int StatueSortingOrder = 32000;

    [Header("Entrance timing")]
    [SerializeField, Min(0.01f)] private float darkenDuration = 0.32f;
    [SerializeField, Min(0.01f)] private float liftDuration = 0.36f;
    [SerializeField, Min(0.01f)] private float centreDuration = 0.72f;
    [SerializeField, Range(0f, 1f)] private float entranceDarkAlpha = 0.66f;
    [SerializeField, Range(0f, 1f)] private float restingDarkAlpha = 0.27f;
    [SerializeField] private float liftHeight = 1.15f;
    [SerializeField] private float finalStatueScale = 3.1f;

    private readonly List<TilemapColorState> tilemapStates = new List<TilemapColorState>();

    private Camera sceneCamera;
    private UniversalAdditionalCameraData cameraData;
    private bool originalPostProcessing;
    private Volume volume;
    private VolumeProfile volumeProfile;
    private SpriteRenderer darkOverlay;
    private Sprite generatedOverlaySprite;

    private QuestPathSequence questSequence;
    private bool originalQuestSequenceEnabled;
    private GameObject questRoot;
    private bool originalQuestRootActive;
    private BearIntroSequence bearIntro;
    private bool originalBearIntroEnabled;

    private Animator playerAnimator;
    private int originalOrientation;
    private float originalPlayerSpeed;

    // The Boss scene already contains the same WordAssembly hierarchy used by CutScene_bear.
    // Only its `magic_stone` child owns the actual three draggable stones; the other controllers
    // are decorative/result-page copies that must not compete for the same input slots.
    private MagicStonePuzzleController bossPuzzle;

    private Transform statue;
    private SpriteRenderer statueRenderer;
    private Vector3 originalStatuePosition;
    private Vector3 originalStatueScale;
    private int originalStatueSortingOrder;
    private bool originalStatueRendererEnabled;

    private sealed class TilemapColorState
    {
        public Tilemap Tilemap;
        public Color OriginalColor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribeToSceneLoads()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToInitialBossScene()
    {
        EnsureController(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureController(scene);
    }

    private static void EnsureController(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != BossSceneName)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        bool hasLegacyIntroMarker = false;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == LegacyIntroMarkerName)
            {
                hasLegacyIntroMarker = true;
            }

            if (roots[i].GetComponentInChildren<BossIntroController>(true) != null)
            {
                return;
            }
        }

        if (!hasLegacyIntroMarker)
        {
            return;
        }

        GameObject host = new GameObject("Boss Intro Controller");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<BossIntroController>();
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != BossSceneName)
        {
            enabled = false;
            return;
        }

        sceneCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        PrepareQuestAndHero();
        PrepareWordAssembly();
        PrepareStatue();
        ApplyBossPalette();
        CreateDarkOverlay();
    }

    private void Start()
    {
        if (enabled)
        {
            StartCoroutine(PlayEntrance());
        }
    }

    private void PrepareQuestAndHero()
    {
        GameObject sequenceObject = GameObject.Find("quest_sequence");
        questSequence = sequenceObject != null ? sequenceObject.GetComponent<QuestPathSequence>() : null;
        if (questSequence != null)
        {
            originalQuestSequenceEnabled = questSequence.enabled;
            questSequence.enabled = false;
        }

        questRoot = GameObject.Find("quest");
        if (questRoot != null)
        {
            originalQuestRootActive = questRoot.activeSelf;
            questRoot.SetActive(false);
        }

        bearIntro = FindAnyObjectByType<BearIntroSequence>();
        if (bearIntro != null)
        {
            originalBearIntroEnabled = bearIntro.enabled;
            bearIntro.enabled = false;
        }

        GameObject player = GameObject.Find("spritesheet_7");
        playerAnimator = player != null ? player.GetComponent<Animator>() : null;
        if (playerAnimator != null)
        {
            originalOrientation = playerAnimator.GetInteger("orientation");
            originalPlayerSpeed = playerAnimator.GetFloat("speed");
            // CharacterRunDirection uses 4 for the front/down-facing idle.
            playerAnimator.SetInteger("orientation", 4);
            playerAnimator.SetFloat("speed", 0f);
        }
    }

    private void PrepareStatue()
    {
        GameObject statueObject = GameObject.Find("statue_02");
        if (statueObject == null)
        {
            Debug.LogWarning("[BossIntroController] statue_02 was not found; the boss entrance was skipped.");
            return;
        }

        statue = statueObject.transform;
        statueRenderer = statueObject.GetComponent<SpriteRenderer>();
        originalStatuePosition = statue.position;
        originalStatueScale = statue.localScale;

        if (statueRenderer != null)
        {
            originalStatueSortingOrder = statueRenderer.sortingOrder;
            originalStatueRendererEnabled = statueRenderer.enabled;
            // The statue only appears after the world has darkened, so the entrance reads clearly.
            statueRenderer.enabled = false;
            statueRenderer.sortingOrder = StatueSortingOrder;
        }
    }

    private void PrepareWordAssembly()
    {
        GameObject puzzleObject = GameObject.Find("magic_stone");
        bossPuzzle = puzzleObject != null ? puzzleObject.GetComponent<MagicStonePuzzleController>() : null;
        if (bossPuzzle == null)
        {
            Debug.LogWarning("[BossIntroController] BossUI/magic_stone is missing; word assembly was not started.");
            return;
        }

        // `book_craft`, `book_craft_pa`, and `book_craft_ga` carry copied controllers from the
        // CutScene_bear prefab.  The active controller discovers those roots as its result pages;
        // the copied components themselves are not puzzle owners and must stay disabled.
        MagicStonePuzzleController[] controllers = FindObjectsByType<MagicStonePuzzleController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i] != bossPuzzle)
            {
                controllers[i].enabled = false;
            }
        }

        // Hide all stones and result pages now; after the boss entrance, PlayIntroReveal restores
        // the exact CutScene_bear one-by-one reveal and enables inputSlot1/inputSlot2 interaction.
        bossPuzzle.PrepareForIntro();
    }

    private void ApplyBossPalette()
    {
        if (sceneCamera == null)
        {
            return;
        }

        cameraData = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            originalPostProcessing = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 300f;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = volumeProfile;

            ColorAdjustments adjustments = volumeProfile.Add<ColorAdjustments>(true);
            // This deliberately shifts the bright reference map toward a mossy olive / deep-teal
            // boss palette.  It keeps the source pixel art intact rather than pretending it is a
            // new painted background.
            adjustments.postExposure.Override(-0.36f);
            adjustments.contrast.Override(14f);
            adjustments.hueShift.Override(-4f);
            adjustments.saturation.Override(-22f);
            adjustments.colorFilter.Override(new Color(0.84f, 0.84f, 0.70f, 1f));
        }

        TintTilemap("L0_Ground", new Color(0.76f, 0.75f, 0.57f, 1f));
        TintTilemap("L1_Water", new Color(0.52f, 0.78f, 0.88f, 1f));
        TintTilemap("bride", new Color(0.80f, 0.68f, 0.54f, 1f));
    }

    private void TintTilemap(string objectName, Color tint)
    {
        GameObject tilemapObject = GameObject.Find(objectName);
        Tilemap tilemap = tilemapObject != null ? tilemapObject.GetComponent<Tilemap>() : null;
        if (tilemap == null)
        {
            return;
        }

        tilemapStates.Add(new TilemapColorState
        {
            Tilemap = tilemap,
            OriginalColor = tilemap.color
        });
        tilemap.color = tint;
    }

    private void CreateDarkOverlay()
    {
        if (sceneCamera == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("Boss Entrance Dark Overlay");
        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.position = new Vector3(sceneCamera.transform.position.x, sceneCamera.transform.position.y, 0f);

        float viewHeight = sceneCamera.orthographicSize * 2f + 2f;
        float viewWidth = viewHeight * sceneCamera.aspect + 2f;
        overlayObject.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);

        darkOverlay = overlayObject.AddComponent<SpriteRenderer>();
        generatedOverlaySprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        darkOverlay.sprite = generatedOverlaySprite;
        darkOverlay.sortingOrder = OverlaySortingOrder;
        darkOverlay.color = new Color(0.026f, 0.031f, 0.021f, 0f);
    }

    private IEnumerator PlayEntrance()
    {
        // Avoid spending the entrance behind the global scene-transition cover.
        yield return new WaitUntil(() => SceneFadeController.RevealComplete);

        yield return FadeOverlay(0f, entranceDarkAlpha, darkenDuration);

        if (statue != null && statueRenderer != null)
        {
            statueRenderer.enabled = originalStatueRendererEnabled;
            Vector3 liftPosition = originalStatuePosition + Vector3.up * liftHeight;
            Vector3 liftScale = originalStatueScale * 1.12f;
            yield return AnimateStatue(originalStatuePosition, liftPosition, originalStatueScale, liftScale, liftDuration);

            Vector3 finalPosition = new Vector3(
                sceneCamera != null ? sceneCamera.transform.position.x : 0f,
                sceneCamera != null ? sceneCamera.transform.position.y + 0.2f : 0.2f,
                originalStatuePosition.z);
            Vector3 finalScale = originalStatueScale * finalStatueScale;
            yield return AnimateStatue(liftPosition, finalPosition, liftScale, finalScale, centreDuration);
        }

        // The full dim only belongs to the reveal.  A lighter residual layer keeps the statue
        // legible while letting the player see the boss-stage map beneath it.
        yield return FadeOverlay(entranceDarkAlpha, restingDarkAlpha, 0.22f);

        if (bossPuzzle != null)
        {
            yield return new WaitForSeconds(0.08f);
            yield return bossPuzzle.PlayIntroReveal();
        }
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (darkOverlay == null)
        {
            yield break;
        }

        Color color = darkOverlay.color;
        duration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            color.a = Mathf.Lerp(from, to, SmoothStep(elapsed / duration));
            darkOverlay.color = color;
            yield return null;
        }

        color.a = to;
        darkOverlay.color = color;
    }

    private IEnumerator AnimateStatue(
        Vector3 fromPosition,
        Vector3 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = SmoothStep(elapsed / duration);
            statue.position = Vector3.LerpUnclamped(fromPosition, toPosition, t);
            statue.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        statue.position = toPosition;
        statue.localScale = toScale;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < tilemapStates.Count; i++)
        {
            TilemapColorState state = tilemapStates[i];
            if (state.Tilemap != null)
            {
                state.Tilemap.color = state.OriginalColor;
            }
        }

        if (cameraData != null)
        {
            cameraData.renderPostProcessing = originalPostProcessing;
        }

        if (volumeProfile != null)
        {
            Destroy(volumeProfile);
        }

        if (generatedOverlaySprite != null)
        {
            Destroy(generatedOverlaySprite);
        }

        if (questSequence != null)
        {
            questSequence.enabled = originalQuestSequenceEnabled;
        }

        if (questRoot != null)
        {
            questRoot.SetActive(originalQuestRootActive);
        }

        if (bearIntro != null)
        {
            bearIntro.enabled = originalBearIntroEnabled;
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetInteger("orientation", originalOrientation);
            playerAnimator.SetFloat("speed", originalPlayerSpeed);
        }

        if (statue != null)
        {
            statue.position = originalStatuePosition;
            statue.localScale = originalStatueScale;
        }

        if (statueRenderer != null)
        {
            statueRenderer.sortingOrder = originalStatueSortingOrder;
            statueRenderer.enabled = originalStatueRendererEnabled;
        }
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
