using UnityEngine;

public class WaypointGizmo : MonoBehaviour
{
    public Vector2 size = new Vector2(1f, 1f);
    public Color color = Color.yellow;

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, size);
    }
}