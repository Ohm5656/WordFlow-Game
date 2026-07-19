using UnityEngine;
using UnityEngine.UI;

/// <summary>Cycles a fixed sprite sequence on a UGUI Image at a constant frame rate.</summary>
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

        elapsed += Time.deltaTime;
        target.sprite = frames[(int)(elapsed * fps) % frames.Length];
    }
}
