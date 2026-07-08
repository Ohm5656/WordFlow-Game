using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    public enum NpcSide { Left, Center, Right }
    public enum EntranceMode { None, SlideInLeft, SlideInRight, SlideInUp, Bob }

    /// <summary>
    /// A reusable, data-driven visual-novel cutscene: an ordered list of frames.
    /// NPC-only, voice + visuals, NO on-screen text (the LD no-fluent-reading rule).
    /// An encounter with no CutsceneData behaves exactly as before.
    /// </summary>
    [CreateAssetMenu(menuName = "WordFlow/Cutscene", fileName = "cutscene")]
    public sealed class CutsceneData : ScriptableObject
    {
        public List<CutsceneFrame> frames = new List<CutsceneFrame>();

        [Range(0f, 1f)] public float backdropDim = 0.35f;   // darkness of the layer over the background
        [Range(0f, 1f)] public float focusDimAlpha = 0.5f;  // non-focused character alpha when a focus is set
        public float focusScale = 1.15f;                    // focused character scale-up
        public float unfocusScale = 0.92f;                  // non-focused character scale-down
    }

    /// <summary>One beat of a cutscene. Every visual/audio field is nullable and
    /// degrades silently — a frame with only a voiceLineId is valid.</summary>
    [Serializable]
    public sealed class CutsceneFrame
    {
        public Sprite background;        // nullable; e.g. background_cut_scene.png
        public Sprite npc;              // nullable; villager/owl/bear art
        public NpcSide side = NpcSide.Center;
        public AnimationClip npcAnim;    // nullable; best-effort legacy clip
        public Vector2 npcSize;          // (0,0) = use the default 520x620
        public EntranceMode npcEntrance = EntranceMode.None;
        public float npcEntranceSeconds = 0.8f;

        // Extra characters share the frame with `npc` (e.g. owl + bear + beam). Empty by
        // default, so every existing cutscene asset is unchanged. Each is freely positioned
        // by `offset` and may carry its own best-effort legacy clip.
        public List<CutsceneCharacter> extraCharacters = new List<CutsceneCharacter>();

        // Which character is emphasised this frame (zoom in + dim the rest):
        //  -1 = none (all full), 0 = the primary `npc`, 1..N = extraCharacters[focusIndex-1].
        public int focusIndex = -1;

        public string voiceLineId;       // -> approved TTS line id (baked Resources/TTS first)
        public AudioClip voiceClip;      // legacy fallback only when voiceLineId is empty
        public float holdSeconds = 1.5f; // minimum on-screen time
    }

    /// <summary>An additional on-screen character for a frame (owl/bear/beam), positioned by
    /// an explicit offset so it can be placed precisely. Nullable fields degrade silently.</summary>
    [Serializable]
    public sealed class CutsceneCharacter
    {
        public Sprite sprite;            // nullable
        public Vector2 offset;           // anchored offset from screen-bottom-center
        public Vector2 size;             // (0,0) = use the default 520x620
        public AnimationClip anim;       // nullable; best-effort legacy clip
        public EntranceMode entrance = EntranceMode.None;
        public float entranceSeconds = 0.8f;
    }
}
