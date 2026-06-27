using UnityEngine;

namespace WordFlow.Adventure.Core
{
    /// <summary>Null-safe play/set helpers so the prototype runs with zero audio/art.</summary>
    public static class AvHelpers
    {
        public static void TryPlay(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip);
        }

        public static void TrySetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null) return;
            renderer.sprite = sprite;
        }

        public static void TrySetSprite(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
        }
    }
}
