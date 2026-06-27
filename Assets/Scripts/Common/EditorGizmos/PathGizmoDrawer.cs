using UnityEngine;

public class PathGizmoDrawer : MonoBehaviour
{
    public Vector2 pointSize = new Vector2(1f, 1f);
    public Color pointColor = Color.yellow;
    public Color lineColor = Color.green;

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        Transform previousPoint = null;

        foreach (Transform point in transform)
        {
            Gizmos.color = pointColor;
            Gizmos.DrawWireCube(point.position, pointSize);

            if (previousPoint != null)
            {
                Gizmos.color = lineColor;
                Gizmos.DrawLine(previousPoint.position, point.position);
            }

            previousPoint = point;
        }
    }
}