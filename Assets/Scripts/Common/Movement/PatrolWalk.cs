using UnityEngine;

/// <summary>
/// Paces a sprite back and forth along one axis (walk out, turn, walk back, loop).
/// The travel range is set independently for each side (<see cref="rangeForward"/> /
/// <see cref="rangeBack"/>) so the animal can reach both fences of its pen without
/// leaving it. Drives its own walk animation from directional frame sets, so facing
/// always matches the real movement direction (no mirroring). Runs only in Play mode.
///
/// A green gizmo shows the two turn-around points when the object is selected — drag
/// the range values until the points sit on the fence.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PatrolWalk : MonoBehaviour
{
    [Tooltip("Travel distance in the + direction (right when horizontal, up when vertical), world units.")]
    public float rangeForward = 1.0f;

    [Tooltip("Travel distance in the - direction (left when horizontal, down when vertical), world units.")]
    public float rangeBack = 1.0f;

    [Tooltip("Move speed in world units per second.")]
    public float speed = 0.15f;

    [Tooltip("Patrol left-right (true) or up-down (false).")]
    public bool horizontal = true;

    [Tooltip("Initial travel direction: +1 = right/up, -1 = left/down. Set to match the placed facing.")]
    public int initialDir = 1;

    [Tooltip("Animation frames per second.")]
    public float fps = 5f;

    [Tooltip("Frames played while moving in the + direction (right when horizontal, up when vertical).")]
    public Sprite[] forwardFrames;

    [Tooltip("Frames played while moving in the - direction (left when horizontal, down when vertical).")]
    public Sprite[] backFrames;

    Vector3 _start;
    SpriteRenderer _sr;
    int _dir;
    float _t;

    Vector3 Axis => horizontal ? Vector3.right : Vector3.up;

    void OnEnable()
    {
        RestartFromCurrentPosition();
        _sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Re-bases this patrol after a runtime layout swap. Used by the night forest calibration so
    /// the first turn-around point is measured from the new pen rather than the daytime one.
    /// </summary>
    public void RestartFromCurrentPosition()
    {
        _start = transform.position;
        _dir = initialDir >= 0 ? 1 : -1;
        _t = 0f;
    }

    void Update()
    {
        transform.position += Axis * (_dir * speed * Time.deltaTime);

        float offset = Vector3.Dot(transform.position - _start, Axis);
        if (offset >= rangeForward && _dir > 0) _dir = -1;
        else if (offset <= -rangeBack && _dir < 0) _dir = 1;

        var frames = _dir > 0 ? forwardFrames : backFrames;
        if (frames != null && frames.Length > 0 && _sr != null)
        {
            _t += Time.deltaTime * fps;
            _sr.sprite = frames[((int)_t) % frames.Length];
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _start : transform.position;
        Vector3 a = center + Axis * rangeForward;
        Vector3 b = center - Axis * rangeBack;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.12f);
        Gizmos.DrawWireSphere(b, 0.12f);
    }
}
