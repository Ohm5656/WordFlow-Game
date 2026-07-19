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
    [Min(1f)]
    [SerializeField] private float fps = 24f;

    private float frameTimer;
    private int frameIndex;
    private int direction = 1;

    private void OnEnable()
    {
        frameTimer = 0f;
        frameIndex = 0;
        direction = 1;
        SetCurrentFrame();
    }

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

        frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        float frameDuration = 1f / Mathf.Max(1f, fps);
        frameTimer += Time.deltaTime;

        // Advance a whole number of frames. This stays smooth if a rendered frame
        // takes longer than normal and never turns the end of the sequence into a
        // last-frame-to-first-frame jump.
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }

        SetCurrentFrame();
    }

    private void AdvanceFrame()
    {
        int finalFrame = frames.Length - 1;
        if (frameIndex == finalFrame)
        {
            direction = -1;
        }
        else if (frameIndex == 0)
        {
            direction = 1;
        }

        frameIndex += direction;
    }

    private void SetCurrentFrame()
    {
        if (target != null && frames != null && frames.Length > 0)
        {
            target.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }
    }
}
