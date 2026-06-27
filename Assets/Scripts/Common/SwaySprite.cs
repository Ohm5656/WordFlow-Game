using UnityEngine;

/// <summary>
/// Gentle idle sway for plant/tree sprites. Rotates the sprite a few degrees
/// around its base (bottom-center) using a sine wave. Runs only in Play mode so
/// it never disturbs scene editing. Each instance can use a random phase so a
/// field of plants does not sway in lockstep.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SwaySprite : MonoBehaviour
{
    [Tooltip("Max sway angle in degrees.")]
    public float amplitude = 2.5f;

    [Tooltip("Sway speed (radians per second multiplier).")]
    public float speed = 1.5f;

    [Tooltip("Phase offset so multiple plants are out of sync.")]
    public float phase = 0f;

    Vector3 _basePos;
    Quaternion _baseRot;
    Vector3 _pivot;

    void OnEnable()
    {
        _basePos = transform.position;
        _baseRot = transform.rotation;
        var sr = GetComponent<SpriteRenderer>();
        // Pivot at bottom-center of the sprite so it bends like a stalk.
        _pivot = sr != null
            ? new Vector3(sr.bounds.center.x, sr.bounds.min.y, _basePos.z)
            : _basePos;
    }

    void Update()
    {
        // Reset to base each frame to avoid rotation drift, then sway.
        transform.position = _basePos;
        transform.rotation = _baseRot;
        float angle = amplitude * Mathf.Sin((Time.time + phase) * speed);
        transform.RotateAround(_pivot, Vector3.forward, angle);
    }
}
