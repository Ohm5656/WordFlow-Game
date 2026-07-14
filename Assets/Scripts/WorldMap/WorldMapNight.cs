using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Self-bootstrapping night dressing for WorldMap.
///
/// The sea/backdrop goes dark; the ISLANDS THEMSELVES stay lit. That is done by dropping a
/// full-screen dark sprite over the map and then lifting every island's renderers above it — so the
/// light follows the island artwork exactly, instead of a round glow that would also light the sea
/// around it. An island that still has a quest under 3 stars gets the same "!" marker the forest
/// quests use, bobbing over it, so the child knows which island to tap.
///
/// Tapping the island is what starts the night redo (WorldMapProblemIslands.LoadNextSceneRoutine
/// opens the session) — there is no separate button.
/// Only appears when NightMode.NightPhase is true.
public sealed class WorldMapNight : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string SetupObjectName = "WorldMap Night";

    // Lifts island renderers above the dark overlay while preserving their order among themselves.
    private const int DarkOverlayOrder = 32000;
    private const int IslandOrderLift = 33000;
    private const int MarkerOrder = 34000;

    [Header("Darkness")]
    [SerializeField] private Color darkColor = new Color(0.04f, 0.09f, 0.20f, 1f);
    [SerializeField] private float maxDarkAlpha = 0.82f;
    [SerializeField] private float fadeStartDelay = 1.7f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float overscan = 1.1f;

    [Header("Quest marker")]
    [Tooltip("Marker width as a fraction of the island's width.")]
    [SerializeField] private float markerWidthFraction = 0.34f;
    [Tooltip("Extra height above the island's top edge, as a fraction of the island's height.")]
    [SerializeField] private float markerHeightFraction = 0.25f;
    [SerializeField] private float markerBobAmplitude = 0.35f;
    [SerializeField] private float markerBobPeriod = 2.4f;
    [Tooltip("How long to keep retrying to find the islands before giving up.")]
    [SerializeField] private float resolveTimeout = 3f;

    private SpriteRenderer overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryStartForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryStartForActiveScene();

    private static void TryStartForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != WorldMapSceneName) return;
        if (!NightMode.NightPhase) return;
        if (GameObject.Find(SetupObjectName) != null) return;

        GameObject host = new GameObject(SetupObjectName);
        host.AddComponent<WorldMapNight>();
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        BuildDarkOverlay();
        StartCoroutine(LightIslandsRoutine());

        if (fadeStartDelay > 0f)
        {
            yield return new WaitForSeconds(fadeStartDelay);
        }

        float duration = Mathf.Max(0.01f, fadeDuration);
        Color c = darkColor;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(0f, maxDarkAlpha, t / duration);
            overlay.color = c;
            yield return null;
        }

        c.a = maxDarkAlpha;
        overlay.color = c;
    }

    private void BuildDarkOverlay()
    {
        Sprite whiteSprite = Resources.Load<Sprite>("Night/night_white");
        Material material = Resources.Load<Material>("Night/NightCutoutOverlay");
        Camera cam = Camera.main;
        if (whiteSprite == null || material == null || cam == null)
        {
            Debug.LogWarning("[WorldMapNight] missing night_white sprite, NightCutoutOverlay material, or Camera.main — night overlay skipped");
            enabled = false;
            return;
        }

        GameObject overlayGo = new GameObject("Night Overlay");
        overlayGo.transform.SetParent(transform, false);

        overlay = overlayGo.AddComponent<SpriteRenderer>();
        overlay.sprite = whiteSprite;
        overlay.sharedMaterial = material; // unlit; its light slots default to 0 = uniform darkness
        overlay.sortingOrder = DarkOverlayOrder;

        Color c = darkColor;
        c.a = 0f;
        overlay.color = c;

        float height = 2f * cam.orthographicSize * overscan;
        float width = height * cam.aspect;
        float spriteWorldSize = whiteSprite.bounds.size.x; // 8px @ PPU 100 = 0.08 world units, square
        Vector3 camPos = cam.transform.position;
        overlayGo.transform.position = new Vector3(camPos.x, camPos.y, 0f);
        overlayGo.transform.localScale = new Vector3(width / spriteWorldSize, height / spriteWorldSize, 1f);
    }

    // The islands are what the night leaves lit. Waits for WorldMapProblemIslands to apply progress
    // (it caches the island list a frame into Start), then lifts every island renderer above the
    // dark overlay and marks the one that still owes stars.
    private IEnumerator LightIslandsRoutine()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, resolveTimeout);
        WorldMapProblemIslands map = null;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (map == null) map = FindObjectOfType<WorldMapProblemIslands>();
            if (map != null && map.IslandRoots != null && map.IslandRoots.Count > 0)
            {
                for (int i = 0; i < map.IslandRoots.Count; i++)
                {
                    LiftAboveDarkness(map.IslandRoots[i]);
                }

                TryPlaceQuestMarker(map);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[WorldMapNight] could not resolve the islands in time — the map stays fully dark");
    }

    private static void LiftAboveDarkness(Transform islandRoot)
    {
        if (islandRoot == null) return;

        SpriteRenderer[] renderers = islandRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sortingOrder += IslandOrderLift; // relative order among the island's parts is kept
        }
    }

    // "!" over the island whose region still has a quest under 3 stars — the same marker art the
    // forest quests use, so the cue reads identically.
    private void TryPlaceQuestMarker(WorldMapProblemIslands map)
    {
        if (!QuestStars.AnyNeedsRedo(NightMode.QuestIds)) return;
        if (!map.TryGetPlayableIslandWorldBounds(out Bounds bounds)) return;

        Sprite markSprite = Resources.Load<Sprite>("Night/quest_mark");
        if (markSprite == null)
        {
            Debug.LogWarning("[WorldMapNight] Night/quest_mark sprite not found — no island marker");
            return;
        }

        GameObject marker = new GameObject("Night Quest Marker");
        marker.transform.SetParent(transform, false);

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = markSprite;
        renderer.sortingOrder = MarkerOrder; // above the lifted islands

        float targetWidth = bounds.size.x * markerWidthFraction;
        float scale = targetWidth / Mathf.Max(0.0001f, markSprite.bounds.size.x);
        marker.transform.localScale = Vector3.one * scale;

        float markerHeight = markSprite.bounds.size.y * scale;
        float y = bounds.max.y + bounds.size.y * markerHeightFraction + markerHeight * 0.5f;
        marker.transform.position = new Vector3(bounds.center.x, y, 0f);

        MarkerBob bob = marker.AddComponent<MarkerBob>();
        bob.Configure(markerBobAmplitude, markerBobPeriod);
    }
}
