using System.Collections.Generic;
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>A craft effect played when this word is successfully built (over the build
    /// canvas, after the magic-stone reveal). None = no effect (today's behaviour).</summary>
    public enum WordEffectKind { None, ProjectileBearFlees, Birds }

    /// <summary>One word the child builds. Mirrors backend Word. wordAudio nullable.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Word Encounter", fileName = "word")]
    public sealed class WordEncounterData : ScriptableObject
    {
        public string id;      // backend wordId, e.g. "yaa"
        public string thai;    // e.g. "ยา"
        public List<StoneTileData> tiles = new List<StoneTileData>(); // ordered left->right
        public string ipa;
        // Backend id of this word's SOUND-OUT reference (e.g. "paa_soundout"), graded against
        // the child's full ปอ อา ปา. Empty -> grade against the whole word id (today's behaviour).
        public string soundOutWordId;
        public string meaning;
        public AudioClip wordAudio;     // nullable; the whole word said once (post-build echo)
        public AudioClip soundOutAudio; // nullable; sounded out then blended, e.g. "ปอ อา ปา" (Listen-while-building)
        public Sprite magicStone;   // nullable; revealed (popped + sound replayed) when this word is built

        [Header("Craft effect")]
        public WordEffectKind effect = WordEffectKind.None; // played after the reveal when built
        public Sprite effectSprite; // nullable; thrown object (Projectile) or crow (Birds)
    }
}
