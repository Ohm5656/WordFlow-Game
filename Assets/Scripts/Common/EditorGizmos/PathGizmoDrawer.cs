using UnityEngine;

public class PathGizmoDrawer : MonoBehaviour
{
    // UI canvas units are ~pixels, so a size of 1 is invisible; default big.
    public Vector2 pointSize = new Vector2(60f, 60f);
    public Color pointColor = Color.yellow;
    public Color lineColor = Color.green;

    private void OnDrawGizmos()
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