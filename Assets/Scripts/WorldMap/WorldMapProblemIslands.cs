using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public sealed class WorldMapProblemIslands : MonoBehaviour
{
    private const string HighestUnlockedRegionKey = "WorldMapHighestUnlockedRegion";
    private const string PendingUnlockRegionKey = "WorldMapPendingUnlockRegion";

    // The adventure-card back button should return to a ready, already-unlocked island rather
    // than replaying the first unlock animation.  This is intentionally session-only: opening a
    // fresh game can still use the authored introductory unlock behaviour.
    private static bool suppressFirstUnlockReplayOnce;

    [Header("Scene")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private IslandProgress[] islands =
    {
        new IslandProgress { IslandName = "Island2", RegionNumber = 1, SceneName = "reference_forest" },
        new IslandProgress { IslandName = "Island3", RegionNumber = 2, SceneName = "" },
        new IslandProgress { IslandName = "Island4", RegionNumber = 3, SceneName = "" },
        new IslandProgress { IslandName = "Island5", RegionNumber = 4, SceneName = "" }
    };
    [SerializeField] private int firstUnlockRegionNumber = 1;
    [SerializeField] private bool replayFirstUnlockUntilNextRegion = true;
    [SerializeField] private float loadDelayAfterClick = 0.12f;
    [Tooltip("Seconds to fade the screen to black before loading the selected island's scene.")]
    [SerializeField] private float sceneExitCoverDuration = 0.7f;

    [Header("Lock State")]
    [SerializeField] private string lockNamePrefix = "lock";
    [SerializeField] private string unlockNamePrefix = "unlock";
    [SerializeField] private bool animateLocks = true;
    [SerializeField] private float lockBobHeight = 0.07f;
    [SerializeField] private float lockBobDuration = 3f;
    [SerializeField] private float lockPulseScale = 1.01f;
    [SerializeField] private bool disableLockedIslandClickAreas = true;
    [SerializeField] private bool hideLockedIslandStars = true;

    [Header("Unlock Animation")]
    [SerializeField] private bool waitForSceneFadeBeforeUnlock = true;
    [SerializeField] private string sceneFadeObjectName = "WorldMap Fade Canvas";
    [SerializeField] private float sceneFadeFallbackWait = 1.1f;
    [SerializeField] private float unlockAnimationDelay = 0.15f;
    [SerializeField] private float unlockAnimationDuration = 0.95f;
    [SerializeField] private float unlockShakeDegrees = 2.5f;
    [SerializeField] private float unlockPopScale = 1.04f;
    [SerializeField] private float unlockHoldDuration = 0.35f;
    [SerializeField] private float unlockFadeOutDuration = 0.35f;

    [Header("Playable Island Prompt")]
    [SerializeField] private bool animatePlayableIslandPrompt = true;
    [SerializeField] private float playableIslandPromptDuration = 2.1f;
    [SerializeField] private float playableIslandBreathScale = 1.018f;
    [SerializeField] private bool createPlayableIslandGlow = false;
    [SerializeField] private Color playableGlowColor = new Color(1f, 0.78f, 0.18f, 0.32f);
    [SerializeField] private float playableGlowBaseScale = 1.08f;
    [SerializeField] private float playableGlowPulseScale = 1.22f;
    [SerializeField] private float playableGlowAlphaMin = 0.12f;
    [SerializeField] private float playableGlowAlphaMax = 0.32f;
    [SerializeField] private float playableClickBoundsPadding = 0.05f;

    [Header("Island 1 collection book")]
    [Tooltip("The map island that opens the collection book. It is intentionally separate from the playable-region route.")]
    [SerializeField] private string collectionIslandName = "Island1";
    [SerializeField] private string collectionBookObjectName = "book_collection";
    [SerializeField, Min(0.01f)] private float collectionBookOpenDuration = 0.26f;
    [SerializeField, Min(0.01f)] private float collectionBookCloseDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float collectionBookStartScale = 0.78f;
    [SerializeField, Min(0f)] private float collectionBookLift = 0.24f;
    [SerializeField, Range(0f, 0.05f)] private float collectionBookIdleScale = 0.012f;
    [SerializeField, Range(0f, 0.85f)] private float collectionBookBackdropAlpha = 0.58f;
    [Tooltip("A short visual confirmation that Island1 was pressed before its collection book opens.")]
    [SerializeField, Min(0.01f)] private float collectionIslandBounceDuration = 0.12f;
    [SerializeField, Range(0.01f, 0.15f)] private float collectionIslandBounceScale = 0.075f;
    [SerializeField, Min(0f)] private float collectionIslandBounceLift = 0.035f;

    private readonly List<AnimatedTarget> lockTargets = new List<AnimatedTarget>();
    private readonly List<IslandRuntime> runtimeIslands = new List<IslandRuntime>();
    private readonly HashSet<Transform> locksInUnlockAnimation = new HashSet<Transform>();
    private IslandRuntime playableIsland;
    private Transform playablePromptTarget;
    private Transform playableGlowRoot;
    private SpriteRenderer playableGlowRenderer;
    private Vector3 playableBaseLocalPosition;
    private Vector3 playableBaseLocalScale;
    private Quaternion playableBaseLocalRotation;
    private Vector3 playableBreathPivotOffset;
    private Vector3 playableGlowBaseLocalScale;
    private bool playablePromptReady;
    private bool isLoadingNextScene;
    private bool progressApplied;
    private static Sprite glowRingSprite;
    private static Sprite collectionBackdropSprite;

    private Transform collectionIsland;
    private Transform collectionIslandVisual;
    private Transform collectionBookRoot;
    private SpriteRenderer collectionBookRenderer;
    private Transform collectionBackdropRoot;
    private SpriteRenderer collectionBackdropRenderer;
    private Vector3 collectionBookBaseLocalPosition;
    private Vector3 collectionBookBaseLocalScale;
    private Color collectionBookBaseColor;
    private CollectionBookState collectionBookState;
    private float collectionBookStateTime;
    private Vector3 collectionIslandVisualBaseLocalPosition;
    private Vector3 collectionIslandVisualBaseLocalScale;
    private float collectionIslandBounceTime = -1f;

    [System.Serializable]
    private sealed class IslandProgress
    {
        public string IslandName;
        public int RegionNumber = 1;
        public string SceneName;
    }

    private sealed class IslandRuntime
    {
        public IslandProgress Progress;
        public Transform Root;
        public Transform Visuals;
        public Transform Lock;
        public Transform Unlock;
        public bool IsUnlocked;
    }

    private sealed class AnimatedTarget
    {
        public Transform Transform;
        public Vector3 BaseLocalPosition;
        public Vector3 BaseLocalScale;
        public float Phase;
    }

    private enum CollectionBookState
    {
        Hidden,
        OpeningQueued,
        Opening,
        Visible,
        Closing
    }

    /// The scene the currently-playable island loads (e.g. "reference_forest"). Empty if progress
    /// has not been applied yet or no island is playable.
    public string PlayableSceneName =>
        playableIsland != null && playableIsland.Progress != null ? playableIsland.Progress.SceneName : string.Empty;

    /// World-space sprite bounds of the currently-playable island (excludes lock/unlock/glow
    /// renderers). False until progress has been applied / no island is playable yet.
    public bool TryGetPlayableIslandWorldBounds(out Bounds bounds) => TryGetPlayableIslandSpriteBounds(out bounds);


    public static void MarkRegionCompleted(int completedRegionNumber)
    {
        if (completedRegionNumber < 1)
        {
            return;
        }

        int nextRegionNumber = completedRegionNumber + 1;
        int highestUnlockedRegion = PlayerPrefs.GetInt(HighestUnlockedRegionKey, 0);
        if (nextRegionNumber <= highestUnlockedRegion)
        {
            PlayerPrefs.SetInt(PendingUnlockRegionKey, nextRegionNumber);
            PlayerPrefs.Save();
            return;
        }

        PlayerPrefs.SetInt(HighestUnlockedRegionKey, nextRegionNumber);
        PlayerPrefs.SetInt(PendingUnlockRegionKey, nextRegionNumber);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Suppresses only the next automatic replay of the first island unlock. Used when the player
    /// leaves the daytime adventure card before beginning its route.
    /// </summary>
    public static void SkipFirstUnlockReplayOnce()
    {
        suppressFirstUnlockReplayOnce = true;
    }

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        CacheCollectionBook();
    }

    private IEnumerator Start()
    {
        yield return null;
        EnsureInitialProgress();
        CacheIslandState();
        ApplyProgressState();
        progressApplied = true;
    }

    private void Update()
    {
        AnimateCollectionIslandPress();
        AnimateCollectionBook();

        if (!progressApplied)
        {
            return;
        }

        AnimateLocks();
        AnimatePlayableIslandPrompt();

        // The collection panel is an Island1 interaction, not a region launch. It gets first
        // chance to consume the press so a visible book behaves like a light modal.
        if (HandleCollectionBookInput())
        {
            return;
        }

        HandlePlayableIslandClick();
    }

    private void OnDestroy()
    {
        ResetCollectionIslandPress();
    }

    private void EnsureInitialProgress()
    {
        int firstRegion = Mathf.Max(1, firstUnlockRegionNumber);
        int highestUnlockedRegion = PlayerPrefs.GetInt(HighestUnlockedRegionKey, 0);
        if (highestUnlockedRegion >= firstRegion)
        {
            bool replayFirstUnlock = replayFirstUnlockUntilNextRegion
                && !NightMode.NightPhase   // night: the island is long unlocked, do not replay it
                && highestUnlockedRegion == firstRegion
                && PlayerPrefs.GetInt(PendingUnlockRegionKey, 0) == 0;

            bool skipReplayForAdventureCardBack = suppressFirstUnlockReplayOnce;
            suppressFirstUnlockReplayOnce = false;

            if (replayFirstUnlock && !skipReplayForAdventureCardBack)
            {
                PlayerPrefs.SetInt(PendingUnlockRegionKey, firstRegion);
                PlayerPrefs.Save();
            }

            return;
        }

        PlayerPrefs.SetInt(HighestUnlockedRegionKey, firstRegion);
        PlayerPrefs.SetInt(PendingUnlockRegionKey, firstRegion);
        PlayerPrefs.Save();
    }

    private void CacheIslandState()
    {
        runtimeIslands.Clear();

        if (islands == null)
        {
            return;
        }

        for (int i = 0; i < islands.Length; i++)
        {
            IslandProgress progress = islands[i];
            if (progress == null || string.IsNullOrWhiteSpace(progress.IslandName))
            {
                continue;
            }

            GameObject islandObject = GameObject.Find(progress.IslandName);
            if (islandObject == null)
            {
                continue;
            }

            Transform root = islandObject.transform;
            Transform visuals = root.Find("Visuals");
            runtimeIslands.Add(new IslandRuntime
            {
                Progress = progress,
                Root = root,
                Visuals = visuals != null ? visuals : root,
                Lock = FindChildByPrefix(root, lockNamePrefix),
                Unlock = FindChildByPrefix(root, unlockNamePrefix)
            });
        }
    }

    private void ApplyProgressState()
    {
        lockTargets.Clear();
        locksInUnlockAnimation.Clear();
        playableIsland = null;
        playablePromptReady = false;

        int highestUnlockedRegion = PlayerPrefs.GetInt(HighestUnlockedRegionKey, Mathf.Max(1, firstUnlockRegionNumber));
        int pendingUnlockRegion = PlayerPrefs.GetInt(PendingUnlockRegionKey, 0);
        if (pendingUnlockRegion > highestUnlockedRegion)
        {
            pendingUnlockRegion = 0;
        }

        IslandRuntime pendingUnlockIsland = null;

        for (int i = 0; i < runtimeIslands.Count; i++)
        {
            IslandRuntime island = runtimeIslands[i];
            int regionNumber = Mathf.Max(1, island.Progress.RegionNumber);
            island.IsUnlocked = regionNumber <= highestUnlockedRegion;

            bool isPendingUnlock = island.IsUnlocked && regionNumber == pendingUnlockRegion;
            bool showLocked = !island.IsUnlocked || isPendingUnlock;
            ApplyIslandLockVisualState(island, showLocked);

            if (showLocked && island.Lock != null)
            {
                AddLockAnimationTarget(island.Lock);
            }

            if (isPendingUnlock)
            {
                pendingUnlockIsland = island;
            }

            if (regionNumber == highestUnlockedRegion)
            {
                playableIsland = island;
            }
        }

        if (playableIsland == null)
        {
            playableIsland = FindHighestUnlockedIsland(highestUnlockedRegion);
        }

        if (pendingUnlockIsland != null)
        {
            StartCoroutine(UnlockIslandRoutine(pendingUnlockIsland));
        }
        else if (playableIsland != null && playableIsland.IsUnlocked)
        {
            CachePlayableIslandPrompt(playableIsland);
            playablePromptReady = true;
        }
    }

    private IslandRuntime FindHighestUnlockedIsland(int highestUnlockedRegion)
    {
        IslandRuntime fallback = null;
        for (int i = 0; i < runtimeIslands.Count; i++)
        {
            IslandRuntime island = runtimeIslands[i];
            int regionNumber = Mathf.Max(1, island.Progress.RegionNumber);
            if (regionNumber <= highestUnlockedRegion)
            {
                fallback = island;
            }
        }

        return fallback;
    }

    private void ApplyIslandLockVisualState(IslandRuntime island, bool showLocked)
    {
        if (island == null)
        {
            return;
        }

        if (island.Lock != null)
        {
            island.Lock.gameObject.SetActive(showLocked);
            SetRendererAlpha(island.Lock, 1f);
        }

        if (island.Unlock != null)
        {
            island.Unlock.gameObject.SetActive(false);
            SetRendererAlpha(island.Unlock, 1f);
        }

        bool canClick = island.IsUnlocked && !showLocked;
        SetDirectChildActive(island.Root, "ClickArea", canClick || !disableLockedIslandClickAreas);
        SetDirectChildActive(island.Root, "Stars", !showLocked || !hideLockedIslandStars);
    }

    private void AddLockAnimationTarget(Transform lockTransform)
    {
        lockTargets.Add(new AnimatedTarget
        {
            Transform = lockTransform,
            BaseLocalPosition = lockTransform.localPosition,
            BaseLocalScale = lockTransform.localScale,
            Phase = lockTargets.Count * 0.55f
        });
    }

    private IEnumerator UnlockIslandRoutine(IslandRuntime island)
    {
        yield return null;

        if (waitForSceneFadeBeforeUnlock)
        {
            float fallbackEndTime = Time.realtimeSinceStartup + Mathf.Max(0f, sceneFadeFallbackWait);
            yield return null;
            while (IsSceneFadeBlocking() || Time.realtimeSinceStartup < fallbackEndTime)
            {
                yield return null;
            }
        }

        if (unlockAnimationDelay > 0f)
        {
            yield return new WaitForSeconds(unlockAnimationDelay);
        }

        GameAudio.PlayUnlock();
        playablePromptReady = false;
        if (island.Lock != null)
        {
            locksInUnlockAnimation.Add(island.Lock);
        }

        Transform lockTransform = island.Lock;
        Transform unlockTransform = island.Unlock;
        Vector3 lockBasePosition = lockTransform != null ? lockTransform.localPosition : Vector3.zero;
        Vector3 lockBaseScale = lockTransform != null ? lockTransform.localScale : Vector3.one;
        Quaternion lockBaseRotation = lockTransform != null ? lockTransform.localRotation : Quaternion.identity;
        Vector3 unlockBasePosition = unlockTransform != null ? unlockTransform.localPosition : Vector3.zero;
        Vector3 unlockBaseScale = unlockTransform != null ? unlockTransform.localScale : Vector3.one;
        Quaternion unlockBaseRotation = unlockTransform != null ? unlockTransform.localRotation : Quaternion.identity;
        Vector3 transitionPosition = lockTransform != null
            && unlockTransform != null
            && lockTransform.parent == unlockTransform.parent
                ? lockBasePosition
                : unlockBasePosition;
        Quaternion transitionRotation = lockTransform != null ? lockBaseRotation : unlockBaseRotation;
        Vector3 unlockDisplayPosition = transitionPosition;

        if (lockTransform != null)
        {
            lockTransform.gameObject.SetActive(true);
            lockTransform.localPosition = lockBasePosition;
            lockTransform.localScale = lockBaseScale;
            lockTransform.localRotation = lockBaseRotation;
            SetRendererAlpha(lockTransform, 1f);
        }

        if (unlockTransform != null)
        {
            unlockTransform.gameObject.SetActive(false);
            unlockTransform.localPosition = unlockBasePosition;
            unlockTransform.localScale = unlockBaseScale;
            unlockTransform.localRotation = unlockBaseRotation;
            SetRendererAlpha(unlockTransform, 1f);
        }

        if (unlockTransform != null)
        {
            unlockTransform.gameObject.SetActive(true);
            unlockTransform.localPosition = transitionPosition;
            unlockTransform.localScale = unlockBaseScale;
            unlockTransform.localRotation = transitionRotation;
            SetRendererAlpha(unlockTransform, 0f);
            AlignVisualCenter(unlockTransform, lockTransform);
            unlockDisplayPosition = unlockTransform.localPosition;
        }

        float totalDuration = Mathf.Max(0.2f, unlockAnimationDuration);
        float shakeDuration = Mathf.Max(0.08f, totalDuration * 0.16f);
        for (float elapsed = 0f; elapsed < shakeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / shakeDuration);
            if (lockTransform != null)
            {
                float shake = Mathf.Sin(t * Mathf.PI * 2f) * unlockShakeDegrees * (1f - t);
                float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.018f;
                lockTransform.localPosition = lockBasePosition;
                lockTransform.localRotation = lockBaseRotation * Quaternion.Euler(0f, 0f, shake);
                lockTransform.localScale = lockBaseScale * pop;
            }

            yield return null;
        }

        float blendDuration = Mathf.Max(0.2f, totalDuration - shakeDuration);
        for (float elapsed = 0f; elapsed < blendDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / blendDuration);
            float smooth = EaseInOutSine(t);
            float softPop = Mathf.Sin(smooth * Mathf.PI) * Mathf.Max(0f, unlockPopScale - 1f);
            float sharedScale = 1f + softPop;

            if (lockTransform != null)
            {
                lockTransform.localRotation = lockBaseRotation;
                lockTransform.localPosition = transitionPosition;
                lockTransform.localScale = lockBaseScale * sharedScale;
                SetRendererAlpha(lockTransform, 1f - smooth);
            }

            if (unlockTransform != null)
            {
                unlockTransform.localPosition = transitionPosition;
                unlockTransform.localScale = unlockBaseScale * sharedScale;
                unlockTransform.localRotation = transitionRotation;
                AlignVisualCenter(unlockTransform, lockTransform);
                unlockDisplayPosition = unlockTransform.localPosition;
                SetRendererAlpha(unlockTransform, smooth);
            }

            yield return null;
        }

        if (lockTransform != null)
        {
            locksInUnlockAnimation.Remove(lockTransform);
            lockTransform.localPosition = lockBasePosition;
            lockTransform.localScale = lockBaseScale;
            lockTransform.localRotation = lockBaseRotation;
            SetRendererAlpha(lockTransform, 1f);
            lockTransform.gameObject.SetActive(false);
        }

        if (unlockTransform != null)
        {
            unlockTransform.localPosition = unlockDisplayPosition;
            unlockTransform.localScale = unlockBaseScale;
            unlockTransform.localRotation = transitionRotation;
            SetRendererAlpha(unlockTransform, 1f);
            unlockTransform.gameObject.SetActive(true);
        }

        if (unlockHoldDuration > 0f)
        {
            yield return new WaitForSeconds(unlockHoldDuration);
        }

        if (unlockTransform != null)
        {
            float fadeDuration = Mathf.Max(0.01f, unlockFadeOutDuration);
            for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float smooth = EaseInOutSine(t);
                unlockTransform.localPosition = unlockDisplayPosition;
                unlockTransform.localScale = unlockBaseScale * Mathf.Lerp(1f, 0.88f, smooth);
                SetRendererAlpha(unlockTransform, 1f - smooth);
                yield return null;
            }

            unlockTransform.localPosition = unlockBasePosition;
            unlockTransform.localScale = unlockBaseScale;
            unlockTransform.localRotation = unlockBaseRotation;
            SetRendererAlpha(unlockTransform, 1f);
            unlockTransform.gameObject.SetActive(false);
        }

        SetDirectChildActive(island.Root, "ClickArea", true);
        SetDirectChildActive(island.Root, "Stars", true);

        int pendingUnlockRegion = PlayerPrefs.GetInt(PendingUnlockRegionKey, 0);
        if (pendingUnlockRegion == Mathf.Max(1, island.Progress.RegionNumber))
        {
            PlayerPrefs.DeleteKey(PendingUnlockRegionKey);
            PlayerPrefs.Save();
        }

        lockTargets.RemoveAll(target => target.Transform == null || target.Transform == lockTransform);

        if (island == playableIsland)
        {
            CachePlayableIslandPrompt(island);
            playablePromptReady = true;
        }
    }

    private bool IsSceneFadeBlocking()
    {
        if (WorldMapFadeIn.IsFadeBlocking)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(sceneFadeObjectName)
            && GameObject.Find(sceneFadeObjectName) != null;
    }

    private void CachePlayableIslandPrompt(IslandRuntime island)
    {
        if (playableGlowRoot != null)
        {
            Destroy(playableGlowRoot.gameObject);
            playableGlowRoot = null;
            playableGlowRenderer = null;
        }

        playablePromptTarget = island.Visuals;
        playableBaseLocalPosition = playablePromptTarget.localPosition;
        playableBaseLocalScale = playablePromptTarget.localScale;
        playableBaseLocalRotation = playablePromptTarget.localRotation;
        playableBreathPivotOffset = Vector3.zero;

        if (TryGetPlayableIslandSpriteBounds(out Bounds bounds))
        {
            Transform parent = playablePromptTarget.parent;
            Vector3 centerInParent = parent != null ? parent.InverseTransformPoint(bounds.center) : bounds.center;
            playableBreathPivotOffset = centerInParent - playableBaseLocalPosition;
        }

        if (createPlayableIslandGlow)
        {
            CreatePlayableIslandGlow();
        }
    }

    private void AnimateLocks()
    {
        if (!animateLocks)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, lockBobDuration);
        float pulseScale = Mathf.Max(1f, lockPulseScale);
        for (int i = 0; i < lockTargets.Count; i++)
        {
            AnimatedTarget target = lockTargets[i];
            if (target.Transform == null || locksInUnlockAnimation.Contains(target.Transform))
            {
                continue;
            }

            float wave = Mathf.Sin(Time.time / duration * Mathf.PI * 2f + target.Phase);
            float pulse = Mathf.Lerp(1f, pulseScale, wave * 0.5f + 0.5f);
            target.Transform.localPosition = target.BaseLocalPosition + Vector3.up * (wave * lockBobHeight);
            target.Transform.localScale = target.BaseLocalScale * pulse;
        }
    }

    private void AnimatePlayableIslandPrompt()
    {
        if (!animatePlayableIslandPrompt || !playablePromptReady || isLoadingNextScene || playablePromptTarget == null)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, playableIslandPromptDuration);
        float wave = Mathf.Sin(Time.time / duration * Mathf.PI * 2f);
        float normalizedWave = wave * 0.5f + 0.5f;
        float breath = Mathf.Lerp(1f, Mathf.Max(1f, playableIslandBreathScale), normalizedWave);

        playablePromptTarget.localPosition = playableBaseLocalPosition - playableBreathPivotOffset * (breath - 1f);
        playablePromptTarget.localScale = playableBaseLocalScale * breath;
        playablePromptTarget.localRotation = playableBaseLocalRotation;

        AnimatePlayableIslandGlow(normalizedWave);
    }

    private void CacheCollectionBook()
    {
        collectionIsland = FindSceneTransformByName(collectionIslandName);
        collectionIslandVisual = collectionIsland != null ? collectionIsland.Find("Visuals") : null;
        if (collectionIslandVisual == null)
        {
            collectionIslandVisual = collectionIsland;
        }

        if (collectionIslandVisual != null)
        {
            collectionIslandVisualBaseLocalPosition = collectionIslandVisual.localPosition;
            collectionIslandVisualBaseLocalScale = collectionIslandVisual.localScale;
        }

        collectionBookRoot = FindSceneTransformByName(collectionBookObjectName);
        if (collectionBookRoot == null)
        {
            return;
        }

        collectionBookRenderer = collectionBookRoot.GetComponent<SpriteRenderer>();
        if (collectionBookRenderer == null)
        {
            collectionBookRenderer = collectionBookRoot.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (collectionBookRenderer == null)
        {
            Debug.LogWarning("[WorldMap] book_collection has no SpriteRenderer; collection-book interaction is disabled.");
            collectionBookRoot = null;
            return;
        }

        collectionBookBaseLocalPosition = collectionBookRoot.localPosition;
        collectionBookBaseLocalScale = collectionBookRoot.localScale;
        collectionBookBaseColor = collectionBookRenderer.color;
        CreateCollectionBackdrop();
        HideCollectionBookImmediate();
    }

    private bool HandleCollectionBookInput()
    {
        if (collectionBookRoot == null
            || collectionBookRenderer == null
            || NightMode.NightPhase
            || WorldMapNight.BlocksIslandInput
            || !TryGetPointerPress(out Vector2 screenPosition)
            || worldCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));

        if (collectionBookState == CollectionBookState.Hidden)
        {
            if (!IsInsideCollectionIsland(worldPosition))
            {
                return false;
            }

            GameAudio.PlayClick();
            StartCollectionIslandPress();
            collectionBookState = CollectionBookState.OpeningQueued;
            return true;
        }

        // The collection book is not a level-launcher. Until its individual stones become
        // selectable, a tap outside is the simple, familiar way to dismiss this lightweight panel.
        if (!IsInsideCollectionBook(worldPosition) && collectionBookState == CollectionBookState.Visible)
        {
            GameAudio.PlayClick();
            collectionBookState = CollectionBookState.Closing;
            collectionBookStateTime = 0f;
        }

        return true;
    }

    private void StartCollectionIslandPress()
    {
        if (collectionIslandVisual == null)
        {
            return;
        }

        // Restarting keeps repeated taps deterministic even if a previous open animation was
        // interrupted by a scene or night-mode transition.
        collectionIslandBounceTime = 0f;
        collectionIslandVisual.localPosition = collectionIslandVisualBaseLocalPosition;
        collectionIslandVisual.localScale = collectionIslandVisualBaseLocalScale;
    }

    private void AnimateCollectionIslandPress()
    {
        if (collectionIslandVisual == null || collectionIslandBounceTime < 0f)
        {
            return;
        }

        collectionIslandBounceTime += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(collectionIslandBounceTime / Mathf.Max(0.01f, collectionIslandBounceDuration));
        float bounce = Mathf.Sin(normalizedTime * Mathf.PI);
        collectionIslandVisual.localScale = collectionIslandVisualBaseLocalScale
                                          * (1f + bounce * collectionIslandBounceScale);
        collectionIslandVisual.localPosition = collectionIslandVisualBaseLocalPosition
                                             + Vector3.up * (bounce * collectionIslandBounceLift);

        if (normalizedTime >= 1f)
        {
            ResetCollectionIslandPress();
        }
    }

    private void ResetCollectionIslandPress()
    {
        collectionIslandBounceTime = -1f;
        if (collectionIslandVisual == null)
        {
            return;
        }

        collectionIslandVisual.localPosition = collectionIslandVisualBaseLocalPosition;
        collectionIslandVisual.localScale = collectionIslandVisualBaseLocalScale;
    }

    private void OpenCollectionBook()
    {
        collectionBookState = CollectionBookState.Opening;
        collectionBookStateTime = 0f;
        collectionBookRoot.gameObject.SetActive(true);
        if (collectionBackdropRoot != null) collectionBackdropRoot.gameObject.SetActive(true);
        ApplyCollectionBookVisual(0f);
    }

    private void AnimateCollectionBook()
    {
        if (collectionBookRoot == null || collectionBookState == CollectionBookState.Hidden)
        {
            return;
        }

        if (NightMode.NightPhase)
        {
            HideCollectionBookImmediate();
            return;
        }

        if (collectionBookState == CollectionBookState.OpeningQueued)
        {
            if (collectionIslandBounceTime < 0f)
            {
                OpenCollectionBook();
            }
            return;
        }

        collectionBookStateTime += Time.unscaledDeltaTime;
        float visualAmount;
        switch (collectionBookState)
        {
            case CollectionBookState.Opening:
            {
                float progress = Mathf.Clamp01(collectionBookStateTime / Mathf.Max(0.01f, collectionBookOpenDuration));
                visualAmount = EaseOutBack(progress);
                ApplyCollectionBookVisual(visualAmount);
                if (progress >= 1f)
                {
                    collectionBookState = CollectionBookState.Visible;
                    collectionBookStateTime = 0f;
                    ApplyCollectionBookVisual(1f);
                }
                break;
            }
            case CollectionBookState.Closing:
            {
                float progress = Mathf.Clamp01(collectionBookStateTime / Mathf.Max(0.01f, collectionBookCloseDuration));
                visualAmount = 1f - EaseInOutSine(progress);
                ApplyCollectionBookVisual(visualAmount);
                if (progress >= 1f)
                {
                    HideCollectionBookImmediate();
                    return;
                }
                break;
            }
            default:
            {
                float breath = 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * collectionBookIdleScale;
                collectionBookRoot.localScale = collectionBookBaseLocalScale * breath;
                collectionBookRoot.localPosition = collectionBookBaseLocalPosition
                                                   + Vector3.up * (Mathf.Sin(Time.unscaledTime * 1.7f) * 0.025f);
                SetCollectionBookAlpha(1f);
                visualAmount = 1f;
                break;
            }
        }

    }

    private void ApplyCollectionBookVisual(float amount)
    {
        amount = Mathf.Clamp01(amount);
        collectionBookRoot.localPosition = collectionBookBaseLocalPosition
                                           + Vector3.down * (collectionBookLift * (1f - amount));
        collectionBookRoot.localScale = collectionBookBaseLocalScale
                                        * Mathf.Lerp(collectionBookStartScale, 1f, amount);
        SetCollectionBookAlpha(amount);
        SetCollectionBackdropAlpha(amount);
    }

    private void HideCollectionBookImmediate()
    {
        collectionBookState = CollectionBookState.Hidden;
        collectionBookStateTime = 0f;
        ResetCollectionIslandPress();
        if (collectionBackdropRoot != null) collectionBackdropRoot.gameObject.SetActive(false);
        if (collectionBookRoot != null)
        {
            collectionBookRoot.localPosition = collectionBookBaseLocalPosition;
            collectionBookRoot.localScale = collectionBookBaseLocalScale;
            SetCollectionBookAlpha(1f);
            collectionBookRoot.gameObject.SetActive(false);
        }
    }

    private void SetCollectionBookAlpha(float amount)
    {
        if (collectionBookRenderer == null) return;
        Color color = collectionBookBaseColor;
        color.a *= Mathf.Clamp01(amount);
        collectionBookRenderer.color = color;
    }

    private void CreateCollectionBackdrop()
    {
        if (worldCamera == null || collectionBookRenderer == null || collectionBackdropRoot != null)
        {
            return;
        }

        GameObject backdropObject = new GameObject("CollectionBookBackdrop");
        collectionBackdropRoot = backdropObject.transform;
        collectionBackdropRenderer = backdropObject.AddComponent<SpriteRenderer>();
        collectionBackdropRenderer.sprite = GetCollectionBackdropSprite();
        collectionBackdropRenderer.sortingLayerID = collectionBookRenderer.sortingLayerID;
        collectionBackdropRenderer.sortingOrder = collectionBookRenderer.sortingOrder - 1;
        collectionBackdropRenderer.color = Color.black;
        RefreshCollectionBackdropBounds();
    }

    private void RefreshCollectionBackdropBounds()
    {
        if (collectionBackdropRoot == null || worldCamera == null)
        {
            return;
        }

        float height = worldCamera.orthographicSize * 2.05f;
        float width = height * worldCamera.aspect;
        Vector3 cameraPosition = worldCamera.transform.position;
        collectionBackdropRoot.position = new Vector3(cameraPosition.x, cameraPosition.y, collectionBookRoot.position.z);
        collectionBackdropRoot.localScale = new Vector3(width, height, 1f);
    }

    private void SetCollectionBackdropAlpha(float amount)
    {
        if (collectionBackdropRenderer == null)
        {
            return;
        }

        RefreshCollectionBackdropBounds();
        Color color = Color.black;
        color.a = collectionBookBackdropAlpha * Mathf.Clamp01(amount);
        collectionBackdropRenderer.color = color;
    }

    private bool IsInsideCollectionIsland(Vector3 worldPosition)
    {
        if (collectionIsland == null || !TryGetCollectionIslandBounds(out Bounds bounds))
        {
            return false;
        }

        bounds.Expand(0.05f);
        return bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z));
    }

    private bool IsInsideCollectionBook(Vector3 worldPosition)
    {
        Bounds bounds = collectionBookRenderer.bounds;
        return bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z));
    }

    private bool TryGetCollectionIslandBounds(out Bounds bounds)
    {
        bounds = default;
        if (collectionIsland == null) return false;

        SpriteRenderer[] renderers = collectionIsland.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return hasBounds;
    }

    private static Transform FindSceneTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == targetName) return transforms[j];
            }
        }
        return null;
    }

    private static Sprite GetCollectionBackdropSprite()
    {
        if (collectionBackdropSprite != null) return collectionBackdropSprite;
        collectionBackdropSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect);
        collectionBackdropSprite.name = "CollectionBookBackdropQuad";
        return collectionBackdropSprite;
    }

    private void HandlePlayableIslandClick()
    {
        if (WorldMapNight.BlocksIslandInput
            || isLoadingNextScene
            || !playablePromptReady
            || playableIsland == null
            || worldCamera == null)
        {
            return;
        }

        if (!TryGetPointerPress(out Vector2 screenPosition))
        {
            return;
        }

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        if (!IsInsidePlayableIsland(worldPosition))
        {
            return;
        }

        GameAudio.PlayClick();
        StartCoroutine(LoadNextSceneRoutine());
    }

    /// Starts the same island-loading path as a direct island click. The night choice panel calls
    /// this only after its press animation and temporary extra dim have finished.
    public bool StartNightRedoFromChoice()
    {
        string nightSceneName = FindFirstConfiguredSceneName();
        if (!NightMode.NightPhase
            || isLoadingNextScene
            || !progressApplied
            || string.IsNullOrWhiteSpace(nightSceneName))
        {
            return false;
        }

        // This first implementation has one playable region with redoable quests. Use its configured
        // scene directly instead of the highest unlocked island, which may already point at a later
        // (not-yet-built) region after the daytime completion scene advanced map progress.
        StartCoroutine(LoadNextSceneRoutine(nightSceneName));
        return true;
    }

    private string FindFirstConfiguredSceneName()
    {
        for (int i = 0; i < runtimeIslands.Count; i++)
        {
            IslandRuntime island = runtimeIslands[i];
            if (island != null
                && island.Progress != null
                && !string.IsNullOrWhiteSpace(island.Progress.SceneName))
            {
                return island.Progress.SceneName.Trim();
            }
        }

        return string.Empty;
    }

    private IEnumerator LoadNextSceneRoutine(string sceneOverride = null)
    {
        isLoadingNextScene = true;
        playablePromptReady = false;

        if (playablePromptTarget != null)
        {
            playablePromptTarget.localPosition = playableBaseLocalPosition;
            playablePromptTarget.localScale = playableBaseLocalScale;
            playablePromptTarget.localRotation = playableBaseLocalRotation;
        }

        if (playableGlowRoot != null)
        {
            playableGlowRoot.gameObject.SetActive(false);
        }

        if (loadDelayAfterClick > 0f)
        {
            yield return new WaitForSeconds(loadDelayAfterClick);
        }

        string sceneName = !string.IsNullOrWhiteSpace(sceneOverride)
            ? sceneOverride
            : playableIsland != null && playableIsland.Progress != null
                ? playableIsland.Progress.SceneName
                : string.Empty;

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            if (NightMode.NightPhase)
            {
                // Night: clicking the (lit) island IS the redo run. Fresh session; drop any stale
                // wrong-word retry flags so the puzzle takes the fresh-clock path, not a resume.
                NightMode.BeginSession(NightMode.QuestIds);
                MagicStonePuzzleController.ConsumeRetryAfterCrow();
                MagicStonePuzzleController.ConsumeRetryAfterAlt();
            }

            // Fade the screen to black before loading; the target scene fades back in on entry.
            yield return SceneFadeController.Cover(sceneExitCoverDuration);
            SceneManager.LoadScene(sceneName.Trim());
        }
        else
        {
            Debug.LogWarning($"World map island '{playableIsland?.Progress?.IslandName}' is unlocked but has no scene name.");
            isLoadingNextScene = false;
            playablePromptReady = true;
            if (playableGlowRoot != null)
            {
                playableGlowRoot.gameObject.SetActive(true);
            }
        }
    }

    private bool IsInsidePlayableIsland(Vector3 worldPosition)
    {
        if (playableIsland == null || playableIsland.Root == null)
        {
            return false;
        }

        if (!TryGetPlayableIslandSpriteBounds(out Bounds bounds))
        {
            return false;
        }

        bounds.Expand(playableClickBoundsPadding);
        return bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z));
    }

    private void CreatePlayableIslandGlow()
    {
        if (!TryGetPlayableIslandSpriteBounds(out Bounds islandBounds))
        {
            return;
        }

        GameObject glowObject = new GameObject("PlayableIslandGlow");
        glowObject.transform.SetParent(playableIsland.Root, false);
        glowObject.transform.position = new Vector3(islandBounds.center.x, islandBounds.center.y, playableIsland.Root.position.z);

        playableGlowRoot = glowObject.transform;
        playableGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        playableGlowRenderer.sprite = GetGlowRingSprite();
        playableGlowRenderer.color = new Color(playableGlowColor.r, playableGlowColor.g, playableGlowColor.b, playableGlowAlphaMax);

        SpriteRenderer referenceRenderer = FindPlayableIslandReferenceRenderer();
        if (referenceRenderer != null)
        {
            playableGlowRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            playableGlowRenderer.sortingOrder = referenceRenderer.sortingOrder - 1;
        }

        float spriteWorldSize = playableGlowRenderer.sprite.bounds.size.x;
        float largestIslandSide = Mathf.Max(islandBounds.size.x, islandBounds.size.y);
        float baseScale = spriteWorldSize > 0f ? largestIslandSide / spriteWorldSize : 1f;
        playableGlowBaseLocalScale = Vector3.one * baseScale;
        playableGlowRoot.localScale = playableGlowBaseLocalScale * playableGlowBaseScale;
    }

    private void AnimatePlayableIslandGlow(float normalizedWave)
    {
        if (playableGlowRoot == null || playableGlowRenderer == null)
        {
            return;
        }

        float expand = Mathf.Lerp(playableGlowBaseScale, playableGlowPulseScale, normalizedWave);
        float alpha = Mathf.Lerp(playableGlowAlphaMax, playableGlowAlphaMin, normalizedWave);
        playableGlowRoot.localScale = playableGlowBaseLocalScale * expand;
        playableGlowRenderer.color = new Color(playableGlowColor.r, playableGlowColor.g, playableGlowColor.b, alpha);
    }

    private bool TryGetPlayableIslandSpriteBounds(out Bounds bounds)
    {
        bounds = default;
        if (playableIsland == null || playableIsland.Root == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = playableIsland.Root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null
                || renderer.sprite == null
                || IsLockOrUnlock(renderer.transform)
                || IsPlayablePromptRenderer(renderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private SpriteRenderer FindPlayableIslandReferenceRenderer()
    {
        if (playableIsland == null || playableIsland.Root == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = playableIsland.Root.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer lowestRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null
                || renderer.sprite == null
                || IsLockOrUnlock(renderer.transform)
                || IsPlayablePromptRenderer(renderer.transform))
            {
                continue;
            }

            if (lowestRenderer == null || renderer.sortingOrder < lowestRenderer.sortingOrder)
            {
                lowestRenderer = renderer;
            }
        }

        return lowestRenderer;
    }

    private bool TryGetPointerPress(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private Transform FindChildByPrefix(Transform root, string namePrefix)
    {
        if (root == null || string.IsNullOrEmpty(namePrefix))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != root && child.name.StartsWith(namePrefix))
            {
                return child;
            }
        }

        return null;
    }

    private bool IsLockOrUnlock(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return (!string.IsNullOrEmpty(lockNamePrefix) && target.name.StartsWith(lockNamePrefix))
            || (!string.IsNullOrEmpty(unlockNamePrefix) && target.name.StartsWith(unlockNamePrefix));
    }

    private bool IsPlayablePromptRenderer(Transform target)
    {
        return playableGlowRoot != null && target != null && target.IsChildOf(playableGlowRoot);
    }

    private static void AlignVisualCenter(Transform target, Transform reference)
    {
        if (target == null || reference == null)
        {
            return;
        }

        SpriteRenderer targetRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer referenceRenderer = reference.GetComponentInChildren<SpriteRenderer>(true);
        if (targetRenderer == null || referenceRenderer == null || targetRenderer.sprite == null || referenceRenderer.sprite == null)
        {
            return;
        }

        Vector3 delta = referenceRenderer.bounds.center - targetRenderer.bounds.center;
        target.position += delta;
    }

    private static void SetDirectChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent.Find(childName);
        if (child != null && child.gameObject.activeSelf != active)
        {
            child.gameObject.SetActive(active);
        }
    }

    private static void SetRendererAlpha(Transform root, float alpha)
    {
        if (root == null)
        {
            return;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = renderers[i].color;
            color.a = Mathf.Clamp01(alpha);
            renderers[i].color = color;
        }
    }

    private static Sprite GetGlowRingSprite()
    {
        if (glowRingSprite != null)
        {
            return glowRingSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float ringRadius = textureSize * 0.36f;
        float ringThickness = textureSize * 0.055f;
        float feather = textureSize * 0.09f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ringDistance = Mathf.Abs(distance - ringRadius);
                float alpha = Mathf.Clamp01(1f - (ringDistance - ringThickness) / feather);
                alpha *= alpha;
                pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.name = "Generated World Map Glow Ring";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        glowRingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize * 0.5f,
            0u,
            SpriteMeshType.FullRect);
        glowRingSprite.name = "Generated World Map Glow Ring";

        return glowRingSprite;
    }

    private static float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return 0.5f - Mathf.Cos(t * Mathf.PI) * 0.5f;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
