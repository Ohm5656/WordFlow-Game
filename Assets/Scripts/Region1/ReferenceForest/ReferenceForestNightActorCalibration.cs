using System;
using UnityEngine;

/// <summary>
/// Keeps the moving forest actors visually attached to the authored night painting.
///
/// The night image is an independently composed illustration, not a colour treatment of the day
/// Tilemaps. Its pens, roads and quest spaces therefore have their own world-space coordinates.
/// This component changes positions only while a night-redo session is loading; a normal daytime
/// scene load never reads or changes these values.
/// </summary>
[DisallowMultipleComponent]
public sealed class ReferenceForestNightActorCalibration : MonoBehaviour
{
    [Serializable]
    private sealed class PositionCalibration
    {
        [Tooltip("Human-readable label only, shown in the Inspector.")]
        public string label;
        [Tooltip("Actor, marker, waypoint, or flight point to place on the night painting.")]
        public Transform target;
        [Tooltip("Absolute world XY coordinate matching the night background.")]
        public Vector2 nightPosition;
    }

    [Serializable]
    private sealed class PatrolCalibration
    {
        [Tooltip("Human-readable label only, shown in the Inspector.")]
        public string label;
        [Tooltip("The existing movement component. Its animation remains unchanged.")]
        public PatrolWalk patrol;
        [Tooltip("Patrol centre/start on the night painting.")]
        public Vector2 nightPosition;
        [Tooltip("Night-only distance travelled in the positive direction.")]
        [Min(0f)] public float rangeForward = 0.6f;
        [Tooltip("Night-only distance travelled in the negative direction.")]
        [Min(0f)] public float rangeBack = 0.6f;
        [Tooltip("Use this only where the night pen's long side differs from the day pen.")]
        public bool horizontal;
    }

    [Header("Night-only positions")]
    [Tooltip("Quest actors, the hero, the two quest stops, the star-board anchor, and crow flight points.")]
    [SerializeField] private PositionCalibration[] positions = Array.Empty<PositionCalibration>();

    [Header("Night-only animal pens")]
    [Tooltip("Existing PatrolWalk movement is kept, but its centre/range is re-based to its night pen.")]
    [SerializeField] private PatrolCalibration[] patrols = Array.Empty<PatrolCalibration>();

    private bool applied;

    private void Awake()
    {
        // Daytime continues to use the authored scene transforms. The scene is reloaded for a
        // night redo, so changing runtime positions here cannot leak into the daytime layout.
        if (NightMode.RedoActive)
        {
            ApplyNightLayout();
        }
    }

    /// <summary>Applies the painted-night layout once. Public for a future in-editor calibration tool.</summary>
    public void ApplyNightLayout()
    {
        if (applied)
        {
            return;
        }

        applied = true;

        for (int i = 0; i < positions.Length; i++)
        {
            PositionCalibration calibration = positions[i];
            if (calibration == null || calibration.target == null)
            {
                continue;
            }

            Vector3 position = calibration.target.position;
            position.x = calibration.nightPosition.x;
            position.y = calibration.nightPosition.y;
            calibration.target.position = position;
        }

        for (int i = 0; i < patrols.Length; i++)
        {
            PatrolCalibration calibration = patrols[i];
            if (calibration == null || calibration.patrol == null)
            {
                continue;
            }

            Transform target = calibration.patrol.transform;
            Vector3 position = target.position;
            position.x = calibration.nightPosition.x;
            position.y = calibration.nightPosition.y;
            target.position = position;

            calibration.patrol.rangeForward = calibration.rangeForward;
            calibration.patrol.rangeBack = calibration.rangeBack;
            calibration.patrol.horizontal = calibration.horizontal;
            calibration.patrol.RestartFromCurrentPosition();
        }
    }
}
