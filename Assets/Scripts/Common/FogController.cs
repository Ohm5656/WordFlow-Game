using UnityEngine;
using UnityEngine.UI;

public class FogController : MonoBehaviour
{
    [Header("Fog")]
    [SerializeField]
    private RawImage fogImage;

    [Header("Timing")]
    [Tooltip("Fallback duration used only when no WordAssemblyTimer is in the scene. Normally the fog " +
             "syncs to the puzzle clock so it fills exactly as time runs out.")]
    [SerializeField]
    private float fogDuration = 60f;

    [SerializeField]
    private AnimationCurve fogCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );

    [Header("Urgency")]
    [Tooltip("How much faster the smoke churns as the clock runs out. 0 = constant speed, " +
             "2 = 3x the drift/boil rate at zero seconds left.")]
    [SerializeField, Range(0f, 4f)]
    private float urgency = 2f;

    // Set by the puzzle clock (WordAssemblyTimer, in another assembly) so this Common-side view can
    // sync without Common depending on Region1. Returns whether the clock has started + its 0..1
    // progress. When null (no clock in scene) the fog falls back to its own timer.
    public static System.Func<bool> SmokeActiveProvider;
    public static System.Func<float> SmokeProgressProvider;

    private Material fogMaterial;

    private float elapsedTime;

    // Own time base instead of Time.unscaledTime: it speeds up with the countdown so the smoke
    // visibly churns harder in the last seconds. Monotonic, so the noise never jumps backwards.
    private float fogTime;


    private static readonly int ProgressID =
        Shader.PropertyToID("_Progress");

    private static readonly int FogTimeID =
        Shader.PropertyToID("_FogTime");


    private void Awake()
    {
        if (fogImage == null)
        {
            Debug.LogError(
                "FogController: Fog Image is missing."
            );

            enabled = false;
            return;
        }

        if (fogImage.material == null)
        {
            Debug.LogError(
                "FogController: Fog Material is missing."
            );

            enabled = false;
            return;
        }

        fogMaterial = new Material(
            fogImage.material
        );

        fogImage.material = fogMaterial;

        SetProgress(0f);
    }


    private void Update()
    {
        float progress;

        if (SmokeActiveProvider != null && SmokeProgressProvider != null)
        {
            // Sync to the puzzle clock: no fog before the build starts, creeping in as the
            // countdown runs, full reach exactly when time is up. Frozen while the timer is paused.
            progress = SmokeActiveProvider()
                ? fogCurve.Evaluate(SmokeProgressProvider())
                : 0f;
        }
        else
        {
            // Standalone fallback (e.g. testing the fog scene on its own).
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / fogDuration);
            progress = fogCurve.Evaluate(normalizedTime);
        }

        SetProgress(progress);

        fogTime += Time.unscaledDeltaTime * (1f + progress * urgency);

        fogMaterial.SetFloat(
            FogTimeID,
            fogTime
        );
    }


    private void SetProgress(float progress)
    {
        fogMaterial.SetFloat(
            ProgressID,
            Mathf.Clamp01(progress)
        );
    }


    public void ResetFog()
    {
        elapsedTime = 0f;
        fogTime = 0f;

        SetProgress(0f);
    }


    private void OnDestroy()
    {
        if (fogMaterial != null)
        {
            Destroy(fogMaterial);
        }
    }
}