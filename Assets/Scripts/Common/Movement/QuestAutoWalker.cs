using UnityEngine;

/// <summary>
/// Moves this character toward the nearest quest item (a child of <see cref="questRoot"/>)
/// and stops when within <see cref="arriveDistance"/>. The actual transform movement is what
/// drives the pack's CharacterAppearance 4-direction walk animation.
/// </summary>
public sealed class QuestAutoWalker : MonoBehaviour
{
    [Tooltip("The 'quest' container whose children are the quest spots to walk to.")]
    [SerializeField] private Transform questRoot;

    [Tooltip("If set, walk straight to this exact target instead of picking the nearest quest item.")]
    [SerializeField] private Transform explicitTarget;

    [Tooltip("Walk speed in world units per second.")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Tooltip("Stop this far from the target (1 = one block/tile).")]
    [SerializeField] private float arriveDistance = 1.0f;

    [Tooltip("Only walk to quest children whose name contains this (empty = any child). Default targets the '!' markers.")]
    [SerializeField] private string targetNameContains = "QuestMarker";

    [Tooltip("Pick the nearest quest and start walking automatically on Start.")]
    [SerializeField] private bool walkOnStart = true;

    [Tooltip("Measure distance on the XY plane only (top-down 2D).")]
    [SerializeField] private bool ignoreZ = true;

    private Transform target;
    private bool arrived;

    private void Start()
    {
        if (!walkOnStart) return;
        if (explicitTarget != null) { target = explicitTarget; arrived = false; }
        else PickNearestQuest();
    }

    /// <summary>Choose the closest child of questRoot as the walk target.</summary>
    public void PickNearestQuest()
    {
        arrived = false;
        target = null;
        if (questRoot == null) return;

        float best = float.MaxValue;
        foreach (Transform child in questRoot)
        {
            if (child == null) continue;
            if (!string.IsNullOrEmpty(targetNameContains) && !child.name.Contains(targetNameContains)) continue;
            Vector3 d = child.position - transform.position;
            if (ignoreZ) d.z = 0f;
            float sq = d.sqrMagnitude;
            if (sq < best) { best = sq; target = child; }
        }
    }

    /// <summary>Walk toward a specific target instead (e.g. wired from a quest event).</summary>
    public void WalkTo(Transform t)
    {
        target = t;
        arrived = false;
    }

    private void FixedUpdate()
    {
        if (target == null || arrived) return;

        Vector3 cur = transform.position;
        Vector3 to = target.position;
        if (ignoreZ) to.z = cur.z;

        Vector3 delta = to - cur;
        float dist = delta.magnitude;
        if (dist <= arriveDistance) { arrived = true; return; }

        float stepLen = Mathf.Min(moveSpeed * Time.fixedDeltaTime, dist - arriveDistance);
        transform.position = cur + delta.normalized * stepLen;
    }
}
