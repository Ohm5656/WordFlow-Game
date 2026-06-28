using UnityEngine;

public class WaypointGizmo : MonoBehaviour
{
    // UI canvas units are ~pixels, so a size of 1 is invisible; default big.
    public Vector2 size = new Vector2(60f, 60f);
    public Color color = Color.yellow;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position, size);
    }
}