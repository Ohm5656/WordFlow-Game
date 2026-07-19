using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a sprite sequence on a UGUI Image at a constant frame rate, ping-ponging
/// forward then backward forever. Wrapping straight from the last frame to the first
/// shows a visible seam when the sequence does not loop cleanly; bouncing hides it.
/// </summary>
public sealed class UISpriteLoop : MonoBehaviour
{
    [SerializeField] private Image target;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 24f;

    private float elapsed;

    private void Update()
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (frames.Length == 1)
        {
            target.sprite = frames[0];
            return;
        }

        elapsed += Time.deltaTime;
        target.sprite = frames[Mathf.RoundToInt(Mathf.PingPong(elapsed * fps, frames.Length - 1))];
    }
}
