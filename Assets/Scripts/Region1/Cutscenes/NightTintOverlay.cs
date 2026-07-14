using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Self-bootstrapping night wash for the puzzle/success scenes during a night redo.
///  - CutScene_bear / CutScene_ga: starts as a dark screen with a light circle FOLLOWING the
///    bear/crow entrance animation; the moment the craft book pops (WordAssemblyTimer.SmokeActive
///    flips true) the circle grows to swallow the whole screen and the overlay settles into a flat
///    dim — readable for the puzzle, matching every other night scene.
///  - Success_pa / Success_ga / Success_ga_correct / Success_ta_incorrect: flat dim only, no
///    spotlight (there is no entrance animation to follow there).
/// Only appears while NightMode.RedoActive is true; does nothing during the day.
public sealed class NightTintOverlay : MonoBehaviour
{
    private const string OverlayObjectName = "Night Tint";

    private static readonly string[] SpotlightScenes = { "CutScene_bear", "CutScene_ga" };
    private static readonly string[] FlatDimScenes =
    {
        "Success_pa", "Success_ga", "Success_ga_correct", "Success_ta_incorrect",
    };

    [Header("Colour")]
    [SerializeField] private Color tintColor = new Color(0.04f, 0.09f, 0.20f, 1f);
    [Tooltip("Overlay alpha while the spotlight is following the entrance animation — darker than the flat dim, since a spotlight beat reads best against real darkness.")]
    [SerializeField] private float spotlightAlpha = 0.85f;
    [Tooltip("Overlay alpha once the spotlight settles (and for the success scenes, which have no spotlight at all).")]
    [SerializeField] private float flatDimAlpha = 0.38f;
    [SerializeField] private float minimumDarknessInLight = 0.1f;

    [Header("Spotlight")]
    [Tooltip("Spotlight radius as a fraction of screen height (radii are in screen pixels — see NightOverlayCutout.shader usage notes).")]
    [SerializeField] private float spotRadiusFactor = 0.42f;
    [SerializeField] private float settleDuration = 0.8f;
    [Tooltip("Settle to flat dim anyway if the puzzle clock never starts within this long (safety net).")]
    [SerializeField] private float failsafeSeconds = 90f;

    private Image overlayImage;
    private Material materialInstance;
    private Transform followTarget;
    private bool settling;

    private static readonly int LightData0Id = Shader.PropertyToID("_LightData0");
    private static readonly int MinDarknessId = Shader.PropertyToID("_MinimumDarkness");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

    private static void TryCreateForActiveScene()
    {
        if (!NightMode.RedoActive) return;

        string sceneName = SceneManager.GetActiveScene().name;
        bool spotlight = Contains(SpotlightScenes, sceneName);
        bool flat = Contains(FlatDimScenes, sceneName);
        if (!spotlight && !flat) return;
        if (GameObject.Find(OverlayObjectName) != null) return;

        GameObject host = new GameObject(OverlayObjectName);
        NightTintOverlay overlay = host.AddComponent<NightTintOverlay>();
        overlay.Build(spotlight);
    }

    private static bool Contains(string[] values, string value)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == value) return true;
        }

        return false;
    }

    private void Build(bool spotlightMode)
    {
        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // under the 10000 scene-fade canvases, above everything else

        GameObject panel = new GameObject("Night Tint Panel");
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        overlayImage = panel.AddComponent<Image>();
        overlayImage.sprite = Resources.Load<Sprite>("Night/night_white");
        overlayImage.raycastTarget = false;

        Material sharedMaterial = Resources.Load<Material>("Night/NightCutoutOverlay");
        if (sharedMaterial != null)
        {
            // UGUI has no MaterialPropertyBlock — an instance is required so per-frame
            // SetVector/SetFloat calls don't mutate the shared asset every other consumer uses.
            materialInstance = new Material(sharedMaterial);
            overlayImage.material = materialInstance;
        }

        followTarget = spotlightMode ? FindFollowTarget() : null;

        if (spotlightMode && followTarget != null && materialInstance != null)
        {
            SetOverlayAlpha(spotlightAlpha);
            StartCoroutine(SpotlightRoutine());
        }
        else
        {
            // Flat dim: either a success scene (no spotlight at all) or a puzzle scene whose
            // entrance component wasn't found — fail safe to the readable flat wash.
            SetOverlayAlpha(flatDimAlpha);
            if (materialInstance != null)
            {
                materialInstance.SetVector(LightData0Id, Vector4.zero);
                materialInstance.SetFloat(MinDarknessId, minimumDarknessInLight);
            }
        }
    }

    private static Transform FindFollowTarget()
    {
        BearCutscene bear = FindObjectOfType<BearCutscene>();
        if (bear != null) return bear.transform;

        CrowEntranceCutscene crow = FindObjectOfType<CrowEntranceCutscene>();
        return crow != null ? crow.transform : null;
    }

    // Tracks the entrance animation every frame until either the craft book pops
    // (WordAssemblyTimer.SmokeActive) or the failsafe timeout elapses, then settles to flat dim.
    private IEnumerator SpotlightRoutine()
    {
        float radius = Screen.height * spotRadiusFactor;
        float elapsed = 0f;

        while (!settling)
        {
            if (followTarget == null)
            {
                break; // entrance object gone without the book popping — settle below
            }

            Vector3 pos = followTarget.position; // ScreenSpaceOverlay UI: world position == screen pixels
            materialInstance.SetVector(LightData0Id, new Vector4(pos.x, pos.y, radius * 0.4f, radius));
            materialInstance.SetFloat(MinDarknessId, minimumDarknessInLight);

            bool bookPopped = WordAssemblyTimer.Instance != null && WordAssemblyTimer.Instance.SmokeActive;
            elapsed += Time.deltaTime;
            if (bookPopped || elapsed > failsafeSeconds)
            {
                yield return SettleRoutine(pos, radius);
                yield break;
            }

            yield return null;
        }

        if (!settling)
        {
            yield return SettleRoutine(Vector3.zero, radius);
        }
    }

    // Grows the hole out past the screen edges while easing the alpha down to the flat dim, so the
    // spotlight circle visually "melts away" into a uniform wash instead of popping off.
    private IEnumerator SettleRoutine(Vector3 lastPos, float startRadius)
    {
        settling = true;

        float startAlpha = overlayImage.color.a;
        float targetRadius = Screen.width * 2f;
        float duration = Mathf.Max(0.01f, settleDuration);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float radius = Mathf.Lerp(startRadius, targetRadius, k);
            SetOverlayAlpha(Mathf.Lerp(startAlpha, flatDimAlpha, k));
            materialInstance.SetVector(LightData0Id, new Vector4(lastPos.x, lastPos.y, radius * 0.4f, radius));
            yield return null;
        }

        SetOverlayAlpha(flatDimAlpha);
        materialInstance.SetVector(LightData0Id, Vector4.zero); // fully off -> uniform flat darkness
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage == null) return;
        Color c = tintColor;
        c.a = alpha;
        overlayImage.color = c;
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}
