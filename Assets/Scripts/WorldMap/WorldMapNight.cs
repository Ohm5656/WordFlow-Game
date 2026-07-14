using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Self-bootstrapping night dressing for WorldMap: a world-space dark overlay (the same
/// NSC/NightOverlayCutout shader reference_forest uses) with a soft circular opening over the
/// playable island — everything else on the map is dark. No UI, no button: the child enters the
/// night redo by clicking the (lit) island itself, exactly like the day flow
/// (WorldMapProblemIslands.HandlePlayableIslandClick / LoadNextSceneRoutine starts the session).
/// Built entirely at runtime (WorldMap has no dark-overlay object in the scene). Only appears when
/// NightMode.NightPhase is true.
public sealed class WorldMapNight : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string SetupObjectName = "WorldMap Night";

    [Header("Overlay")]
    [SerializeField] private Color darkColor = new Color(0.04f, 0.09f, 0.20f, 1f);
    [SerializeField] private float maxDarkAlpha = 0.82f;
    [SerializeField] private float fadeStartDelay = 1.7f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float overscan = 1.1f;

    [Header("Island opening")]
    [Tooltip("Opening radius as a multiple of the island's own half-size (bigger = softer glow).")]
    [SerializeField] private float islandRadiusMultiplier = 1.6f;
    [SerializeField] private float islandInnerFraction = 0.45f;
    [SerializeField] private float minimumDarknessInLight = 0.15f;
    [Tooltip("How long to keep retrying to find the playable island's bounds before giving up.")]
    [SerializeField] private float boundsResolveTimeout = 3f;

    private SpriteRenderer overlay;
    private MaterialPropertyBlock overlayProps;
    private static readonly int[] LightDataIds =
    {
        Shader.PropertyToID("_LightData0"),
        Shader.PropertyToID("_LightData1"),
        Shader.PropertyToID("_LightData2"),
        Shader.PropertyToID("_LightData3")
    };

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
        BuildOverlay();

        // Push a fully-closed hole immediately (island not lit yet) so the very first frame the
        // overlay is visible it's already correct, then resolve the real island bounds.
        PushLightData(Vector4.zero);
        StartCoroutine(ResolveIslandHoleRoutine());

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

    private void BuildOverlay()
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
        overlay.sharedMaterial = material;
        overlay.sortingOrder = 32000; // matches reference_forest's NightDark convention

        Color c = darkColor;
        c.a = 0f;
        overlay.color = c;

        // Cover the full orthographic view with headroom (overscan) so nothing pokes out at the
        // edges when the camera is exactly framed.
        float height = 2f * cam.orthographicSize * overscan;
        float width = height * cam.aspect;
        float spriteWorldSize = whiteSprite.bounds.size.x; // 8px @ PPU 100 = 0.08 world units, square
        Vector3 camPos = cam.transform.position;
        overlayGo.transform.position = new Vector3(camPos.x, camPos.y, 0f);
        overlayGo.transform.localScale = new Vector3(width / spriteWorldSize, height / spriteWorldSize, 1f);
    }

    private IEnumerator ResolveIslandHoleRoutine()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, boundsResolveTimeout);
        WorldMapProblemIslands map = null;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (map == null) map = FindObjectOfType<WorldMapProblemIslands>();
            if (map != null && map.TryGetPlayableIslandWorldBounds(out Bounds bounds))
            {
                float outer = Mathf.Max(bounds.extents.x, bounds.extents.y) * islandRadiusMultiplier;
                float inner = outer * Mathf.Clamp01(islandInnerFraction);
                PushLightData(new Vector4(bounds.center.x, bounds.center.y, inner, outer));
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[WorldMapNight] could not resolve the playable island's bounds in time — map stays fully dark");
    }

    private void PushLightData(Vector4 slot0)
    {
        if (overlay == null) return;
        if (overlayProps == null) overlayProps = new MaterialPropertyBlock();

        overlay.GetPropertyBlock(overlayProps);
        overlayProps.SetVector(LightDataIds[0], slot0);
        overlayProps.SetVector(LightDataIds[1], Vector4.zero);
        overlayProps.SetVector(LightDataIds[2], Vector4.zero);
        overlayProps.SetVector(LightDataIds[3], Vector4.zero);
        overlayProps.SetFloat("_MinimumDarkness", minimumDarknessInLight);
        overlay.SetPropertyBlock(overlayProps);
    }
}
