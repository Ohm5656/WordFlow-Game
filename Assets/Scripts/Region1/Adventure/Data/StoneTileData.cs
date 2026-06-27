using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>One phoneme/grapheme tile. Mirrors backend Stone. Audio/icon nullable (art-free prototype).</summary>
    [CreateAssetMenu(menuName = "WordFlow/Stone Tile", fileName = "stone")]
    public sealed class StoneTileData : ScriptableObject
    {
        public string id;          // e.g. "stone_aa"
        public string grapheme;    // e.g. "า"
        public AudioClip phonemeAudio; // nullable
        public Sprite icon;            // nullable
    }
}
