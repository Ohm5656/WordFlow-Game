using System.Collections.Generic;
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>Flat list of all known Region-1 words. Used for the local outcome lookup.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Word Database", fileName = "WordDatabase")]
    public sealed class WordDatabase : ScriptableObject
    {
        public List<WordEncounterData> words = new List<WordEncounterData>();

        public WordEncounterData LookupByThai(string thai)
        {
            if (string.IsNullOrEmpty(thai)) return null;
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i] != null && words[i].thai == thai) return words[i];
            }
            return null;
        }

        public bool ContainsThai(string thai) => LookupByThai(thai) != null;
    }
}
