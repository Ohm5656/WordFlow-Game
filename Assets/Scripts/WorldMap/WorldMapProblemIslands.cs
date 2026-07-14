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
    [SerializeField] private float sceneFadeFallbackWait = 3.4f;
    [SerializeField] private float unlockAnimationDelay = 0.45f;
    [SerializeField] private float unlockAnimationDuration = 1.8f;
    [SerializeField] private float unlockShakeDegrees = 2.5f;
    [SerializeField] private float unlockPopScale = 1.04f;
    [SerializeField] private float unlockHoldDuration = 1.1f;
    [SerializeField] private float unlockFadeOutDuration = 0.8f;

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

    /// The scene the currently-playable island loads (e.g. "reference_forest"). Empty if progress
    /// has not been applied yet or no island is playable.
    public string PlayableSceneName =>
        playableIsland != null && playableIsland.Progress != null ? playableIsland.Progress.SceneName : string.Empty;

    /// World-space sprite bounds of the currently-playable island (excludes lock/unlock/glow
    /// renderers). False until progress has been applied / no island is playable yet.
    public bool TryGetPlayableIslandWorldBounds(out Bounds bounds) => TryGetPlayableIslandSpriteBounds(out bounds);

    /// Every island's root transform, in the authored order. Empty until progress has been applied
    /// (the island list is cached a frame into Start). WorldMapNight reads this to keep the islands
    /// lit above the night darkness.
    public IReadOnlyList<Transform> IslandRoots
    {
        get
        {
            islandRootsCache.Clear();
            for (int i = 0; i < runtimeIslands.Count; i++)
            {
                if (runtimeIslands[i].Root != null) islandRootsCache.Add(runtimeIslands[i].Root);
            }

            return islandRootsCache;
        }
    }

    private readonly List<Transform> islandRootsCache = new List<Transform>();

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

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
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
        if (!progressApplied)
        {
            return;
        }

        AnimateLocks();
        AnimatePlayableIslandPrompt();
        HandlePlayableIslandClick();
    }

    private void EnsureInitialProgress()
    {
        int firstRegion = Mathf.Max(1, firstUnlockRegionNumber);
        int highestUnlockedRegion = PlayerPrefs.GetInt(HighestUnlockedRegionKey, 0);
        if (highestUnlockedRegion >= firstRegion)
        {
            if (replayFirstUnlockUntilNextRegion
                && !NightMode.NightPhase   // night: the island is long unlocked, do not replay it
                && highestUnlockedRegion == firstRegion
                && PlayerPrefs.GetInt(PendingUnlockRegionKey, 0) == 0)
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

    private void HandlePlayableIslandClick()
    {
        if (isLoadingNextScene || !playablePromptReady || playableIsland == null || worldCamera == null)
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

    private IEnumerator LoadNextSceneRoutine()
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

        string sceneName = playableIsland != null && playableIsland.Progress != null
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
