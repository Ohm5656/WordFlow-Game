using UnityEngine;

/// <summary>
/// Bobs this object up/down around WHEREVER it is placed (captures its localPosition on enable
/// and oscillates around it). Unlike an Animator clip, it never overrides the placed position,
/// so the "!" stays exactly where the user dropped it and just floats in place.
/// </summary>
public sealed class MarkerBob : MonoBehaviour
{
    [Tooltip("Total up/down travel in local units (peak-to-peak).")]
    [SerializeField] private float amplitude = 0.8f;

    [Tooltip("Seconds per full up+down cycle (higher = slower).")]
    [SerializeField] private float period = 3.6f;

    private Vector3 baseLocal;

    /// For markers built at runtime (e.g. the WorldMap night island marker), which have no
    /// inspector to author these on.
    public void Configure(float bobAmplitude, float bobPeriod)
    {
        amplitude = bobAmplitude;
        period = bobPeriod;
        baseLocal = transform.localPosition;
    }

    private void OnEnable() => baseLocal = transform.localPosition;

    private void Update()
    {
        if (period <= 0.01f) return;
        float s = Mathf.Sin(Time.time / period * Mathf.PI * 2f);   // -1..1
        transform.localPosition = baseLocal + new Vector3(0f, amplitude * 0.5f * s, 0f);
    }
}
