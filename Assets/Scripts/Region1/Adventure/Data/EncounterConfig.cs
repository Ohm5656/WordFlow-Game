using UnityEngine;

namespace WordFlow.Adventure.Data
{
    public enum EncounterMode { Supported, Recall }

    /// <summary>The single source the controller reads to set up one encounter.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Encounter Config", fileName = "encounter")]
    public sealed class EncounterConfig : ScriptableObject
    {
        public EncounterMode mode = EncounterMode.Supported;
        public bool echo = false;
        public WordEncounterData target;
        public string questId = "";
        public string sceneId = "word_build_prototype";
        public string childId = "kid_demo_01";

        // Encounter cutscenes (NPC-only VN, voice + visuals, no text). All nullable —
        // a null slot keeps the original no-cutscene flow byte-for-byte.
        public CutsceneData introCutscene;       // plays before the word-build
        public CutsceneData outroCutscene;        // = correct-EFFECT cutscene: plays after a Correct build
        public CutsceneData wrongEffectCutscene;  // plays after a WrongWord build (real word, wrong context)
        // Extra tray tiles that are never required to complete the word (e.g. ก on the ปา
        // encounter, making the real-but-wrong word กา reachable). Empty = no distractor =
        // today's behaviour exactly. Forward-compat (boss-only, not used now): shuffleTray.
        public System.Collections.Generic.List<StoneTileData> distractorTiles = new System.Collections.Generic.List<StoneTileData>();
    }
}
