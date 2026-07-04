using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordFlow.Adventure.Core;
using WordFlow.Adventure.Net;

public sealed class MagicStonePuzzleController : MonoBehaviour
{
    // Times the build (stones interactive -> word assembled), like word_build_paa_polished,
    // so /grade gets a real buildLatencyMs instead of 0.
    private readonly BuildLatencyTracker _latency = new BuildLatencyTracker();
    private const string DefaultNextSceneName = "Assets/Scenes/region 1/practice.unity";
    private const string RetryAfterCrowPlayerPrefsKey = "MagicStonePuzzleRetryAfterCrow";
    private const string RetryAfterAltWordPlayerPrefsKey = "MagicStonePuzzleRetryAfterAlt";

    [Header("Stones")]
    [SerializeField] private RectTransform stonePa;
    [SerializeField] private RectTransform stoneKa;
    [SerializeField] private RectTransform stoneSaraAa;

    [Header("Slots")]
    [SerializeField] private RectTransform firstSlot;
    [SerializeField] private RectTransform secondSlot;
    [SerializeField] private Vector2 firstSlotPosition = new Vector2(-1040f, 150f);
    [SerializeField] private Vector2 secondSlotPosition = new Vector2(-250f, 150f);
    [SerializeField] private float snapDistance = 300f;
    [SerializeField] private float snappedScaleMultiplier = 0.82f;

    [Header("Word Recipe")]
    [Tooltip("The assembled word (slot1 + slot2) that triggers the craft result.")]
    [SerializeField] private string targetWord = "ปา";
    [Tooltip("Real Thai words other than the target that the stones can spell (e.g. กา). Used to tag a wrong build as wrong_word vs non_word, like word_build_paa_polished.")]
    [SerializeField] private string[] knownWords = { "กา" };
    [Tooltip("GameObject names of the puzzle stones, paired index-for-index with stoneLetters.")]
    [SerializeField] private string[] stoneObjectNames = { "stone1", "stone2", "stone3" };
    [Tooltip("Glyph each stone represents, paired index-for-index with stoneObjectNames.")]
    [SerializeField] private string[] stoneLetters = { "ก", "ป", "า" };

    [Header("Reveal")]
    [SerializeField] private float revealDelayBetweenStones = 0.22f;
    [SerializeField] private float revealDuration = 0.42f;
    [SerializeField] private float revealYOffset = -120f;
    [SerializeField] private float revealStartScale = 0.2f;

    [Header("Motion")]
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private float returnDuration = 0.22f;

    [Header("Completion Ritual")]
    [SerializeField] private bool playRitualWhenBothSlotsFilled = true;
    [SerializeField] private RectTransform ritualShakeRoot;
    [SerializeField] private float ritualShakeDuration = 2f;
    [SerializeField] private float ritualShakeStrength = 60f;
    [SerializeField] private float ritualShakeFrequency = 26f;
    [SerializeField] private float ritualShakeStartIntensity = 0.02f;
    [SerializeField] private float ritualShakeEndIntensity = 2.1f;
    [SerializeField] private float ritualShakeRampPower = 2.6f;
    [SerializeField] private float ritualShakeFrequencyRampMultiplier = 1.45f;
    [SerializeField] private float ritualFullscreenOverscanScale = 1.12f;
    [SerializeField] private float ritualChargeFlashMaxAlpha = 0.36f;
    [SerializeField] private float ritualFinalPulseStart = 0.72f;
    [SerializeField] private float ritualFinalPulseAlpha = 0.72f;
    [SerializeField] private float whiteFlashFadeInDuration = 0.28f;
    [SerializeField] private float whiteFlashHoldDuration = 0.18f;
    [SerializeField] private float whiteFlashFadeOutDuration = 0.7f;
    [SerializeField] private Color whiteFlashColor = Color.white;
    [SerializeField] private string nextSceneName = DefaultNextSceneName;

    [Header("Craft Result")]
    [SerializeField] private RectTransform bookCraftRoot;
    [SerializeField] private RectTransform bookCraftSuccessRoot;
    [SerializeField] private RectTransform bookCraftCrowRoot; // the target word (ปา) result page = book_craft_pa
    [Tooltip("Second valid word (e.g. กา): building it reveals book_craft_ga and ends at Success_ga, using the SAME flow + backend handling as the target word (ปา -> book_craft_pa -> Success_pa). Only the page, wordId and success scene differ. Empty page = found by name 'book_craft_ga'.")]
    [SerializeField] private RectTransform bookCraftAltRoot;
    [SerializeField] private string altWord = "กา";
    [SerializeField] private string altWordId = "kaa";
    [SerializeField] private string altSceneName = "Assets/Scenes/region 1/Success_ga.unity";
    [SerializeField] private RectTransform soundButtonRoot;
    [SerializeField] private RectTransform micButtonRoot;
    [SerializeField] private float craftResultDelay = 0.08f;
    [SerializeField] private float craftResultFadeDuration = 0.38f;
    [SerializeField] private float craftResultStartScale = 0.96f;
    [SerializeField] private float actionIconFadeDelay = 0.12f;
    [SerializeField] private float actionIconFadeDuration = 0.35f;

    [Header("Crow Craft Feedback")]
    [SerializeField] private string crowSceneName = "cut_scene3";
    [SerializeField] private float crowCraftHoldDuration = 0.55f;
    [SerializeField] private bool useWhiteFlashToCrowScene = false;
    [SerializeField] private float crowFlashDelay = 0.04f;

    [Header("Voice Interaction")]
    [SerializeField] private AudioSource actionAudioSource;
    [SerializeField] private AudioClip soundPlaybackClip;
    [Tooltip("Result-word echo for the alt word (กา): plays กอ-อา-กา instead of soundPlaybackClip's ปอ-อา-ปา when the alt word was built.")]
    [SerializeField] private AudioClip altSoundPlaybackClip;
    [SerializeField] private string targetWordId = "paa";
    [SerializeField] private string childId = "kid_demo_01";
    [SerializeField] private string questId = "q_region1_throw";
    [SerializeField] private string sessionId = "";
    [SerializeField] private string recordingSceneId = "cut_scene1";
    [SerializeField] private bool uploadRecordingToBackend = false;
    [SerializeField] private float maxRecordingSeconds = 5f; // matches word_build_paa_polished micSeconds
    [SerializeField] private float activeIconPulseScale = 1.12f;
    [SerializeField] private float activeIconPulseSpeed = 5.5f;

    [Header("Stone Placement Voice (pre-baked, like word_build_paa_polished)")]
    [Tooltip("AudioClip per stone, paired index-for-index with stoneLetters. Played when the stone snaps into a slot.")]
    [SerializeField] private AudioClip[] stonePlacementClips;
    [Tooltip("Volume scale per stone, paired index-for-index with stoneLetters (0-1). Use to equalise clips that were recorded/TTS-generated at different loudness levels. Defaults to 1 if shorter than stoneLetters.")]
    [SerializeField] private float[] stonePlacementVolumes;
    [Tooltip("While a stone's placement sound is still playing, block placing the next stone into a slot.")]
    [SerializeField] private bool blockPlacementWhileVoicePlaying = true;
    [Tooltip("Auto-play the result word clip (soundPlaybackClip) once when the sound/mic icons appear (the post-build echo the child copies).")]
    [SerializeField] private bool autoPlayResultClip = true;

    [Header("Gameplay TTS (line ids from backend tts_lines.json; the wav clips above stay as offline fallback)")]
    [Tooltip("Fetches the line ids below from /tts. Empty = first TtsApiClient found in the scene.")]
    [SerializeField] private TtsApiClient ttsClient;
    [Tooltip("TTS line id per stone, paired index-for-index with stoneLetters (e.g. gameplay_ko). Replaces that stone's stonePlacementClips entry when the fetch succeeds.")]
    [SerializeField] private string[] stonePlacementLineIds;
    [Tooltip("TTS line ids voiced in order as the target-word echo (e.g. gameplay_po, gameplay_aa, gameplay_paa). Replaces soundPlaybackClip when every fetch succeeds.")]
    [SerializeField] private string[] soundPlaybackLineIds;
    [Tooltip("TTS line ids voiced in order as the alt-word echo. Replaces altSoundPlaybackClip when every fetch succeeds.")]
    [SerializeField] private string[] altSoundPlaybackLineIds;
    [Tooltip("Silence between syllables when stitching the echo TTS lines into one clip.")]
    [SerializeField] private float ttsEchoGapSeconds = 0.2f;

    [Header("Backend Grading (mic upload, like word_build_paa_polished)")]
    [Tooltip("Records the mic clip and POSTs it to /grade, identical to word_build_paa_polished. Auto-added at runtime if left empty.")]
    [SerializeField] private GradeApiClient gradeClient;
    [Tooltip("Durable build-attempt telemetry, like word_build_paa_polished. Auto-found in scene if left empty.")]
    [SerializeField] private TelemetryClient telemetry;

    [Header("Recording Success")]
    [SerializeField] private Sprite recordingSuccessSprite;
    [SerializeField] private AudioClip recordingSuccessSfx;
    [SerializeField] private Vector2 recordingSuccessSize = new Vector2(640f, 430f);
    [SerializeField] private Vector2 recordingSuccessPosition = Vector2.zero;
    [SerializeField] private float recordingSuccessPopDuration = 0.38f;
    [SerializeField] private float recordingSuccessHoldDuration = 0.45f;
    [SerializeField] private float recordingSuccessStartScale = 0.35f;
    [SerializeField] private float recordingSuccessEndScale = 1f;
    [SerializeField] private bool playSuccessShakeAndFlash = true;
    [SerializeField] private float transitionDelayAfterSuccess = 0.08f;

    private readonly List<MagicStonePuzzleStone> stones = new List<MagicStonePuzzleStone>();
    private readonly MagicStonePuzzleStone[] slotOccupants = new MagicStonePuzzleStone[2];
    private RectTransform rectTransform;
    private bool prepared;
    private bool revealFinished;
    private bool ritualPlaying;
    private bool ritualCompleted;
    private Coroutine completionRoutine;
    private Button soundButton;
    private Button micButton;
    private Coroutine soundPlaybackRoutine;
    private Coroutine micRecordingRoutine;
    private Coroutine uploadRecordingRoutine;
    private Coroutine soundPulseRoutine;
    private Coroutine micPulseRoutine;
    private Vector3 soundIconBaseScale = Vector3.one;
    private Vector3 micIconBaseScale = Vector3.one;
    private bool hasSoundIconBaseScale;
    private bool hasMicIconBaseScale;
    private bool isRecording;
    private bool recordingSuccessPlaying;
    private bool crowFeedbackPlaying;
    // The result variant chosen at completion (target ปา vs alt กา): which page to reveal, which
    // wordId to grade, and which success scene to load. Defaults fall back to the ปา fields.
    private RectTransform activeBookRoot;
    private string activeWordId;
    private string activeSceneName;
    private string activeResultWord;
    private float recordingStartedAt;
    private bool isPlacementVoicePlaying;
    private Coroutine placementVoiceRoutine;

    public RectTransform DragParent => rectTransform;
    public bool CanInteract => revealFinished && !ritualPlaying && !ritualCompleted && !crowFeedbackPlaying;
    public float ReturnDuration => returnDuration;

    public static bool IsRetryAfterCrowRequested => PlayerPrefs.GetInt(RetryAfterCrowPlayerPrefsKey, 0) == 1;

    public static void RequestRetryAfterCrow()
    {
        PlayerPrefs.SetInt(RetryAfterCrowPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeRetryAfterCrow()
    {
        if (!IsRetryAfterCrowRequested)
        {
            return false;
        }

        PlayerPrefs.DeleteKey(RetryAfterCrowPlayerPrefsKey);
        PlayerPrefs.Save();
        return true;
    }

    public static void RequestRetryAfterAlt()
    {
        PlayerPrefs.SetInt(RetryAfterAltWordPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeRetryAfterAlt()
    {
        if (PlayerPrefs.GetInt(RetryAfterAltWordPlayerPrefsKey, 0) != 1) return false;
        PlayerPrefs.DeleteKey(RetryAfterAltWordPlayerPrefsKey);
        PlayerPrefs.Save();
        return true;
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (!HasPuzzleStoneChildren())
        {
            enabled = false;
            return;
        }

        PrepareForIntro();
        PrefetchGameplayTts();
    }

    // Swaps the baked wav audio for backend TTS (same lines the owl uses). Placement lines
    // replace stonePlacementClips entries; the echo sequences are stitched into ONE clip so
    // all the existing single-clip playback plumbing (ActiveSoundClip, autoplay, mic gating)
    // stays untouched. Any failed fetch leaves the wav fallback in place (TtsApiClient is
    // null-safe and caches by line id).
    private void PrefetchGameplayTts()
    {
        if (ttsClient == null) ttsClient = FindObjectOfType<TtsApiClient>();
        if (ttsClient == null) return;

        if (stonePlacementLineIds != null && stoneLetters != null)
        {
            int count = Mathf.Min(stonePlacementLineIds.Length, stoneLetters.Length);
            if (count > 0 && (stonePlacementClips == null || stonePlacementClips.Length < count))
            {
                System.Array.Resize(ref stonePlacementClips, count);
            }

            for (int i = 0; i < count; i++)
            {
                int index = i;
                if (string.IsNullOrWhiteSpace(stonePlacementLineIds[index]))
                {
                    continue;
                }

                ttsClient.GetLine(stonePlacementLineIds[index], clip =>
                {
                    if (clip != null)
                    {
                        stonePlacementClips[index] = clip;
                    }
                });
            }
        }

        PrefetchEchoSequence(soundPlaybackLineIds, clip => soundPlaybackClip = clip);
        PrefetchEchoSequence(altSoundPlaybackLineIds, clip => altSoundPlaybackClip = clip);
    }

    private void PrefetchEchoSequence(string[] lineIds, System.Action<AudioClip> assign)
    {
        if (lineIds == null || lineIds.Length == 0)
        {
            return;
        }

        AudioClip[] parts = new AudioClip[lineIds.Length];
        int pending = lineIds.Length;
        for (int i = 0; i < lineIds.Length; i++)
        {
            int index = i;
            ttsClient.GetLine(lineIds[index], clip =>
            {
                parts[index] = clip;
                if (--pending != 0)
                {
                    return;
                }

                AudioClip merged = ConcatClips(parts, ttsEchoGapSeconds);
                if (merged != null)
                {
                    assign(merged);
                }
            });
        }
    }

    // Stitches the syllable clips into one, with a silent gap between them. Returns null if
    // any part is missing (keep the baked wav fallback instead of a partial echo).
    private static AudioClip ConcatClips(AudioClip[] parts, float gapSeconds)
    {
        int frequency = 0;
        int channels = 0;
        int totalFrames = 0;
        foreach (AudioClip part in parts)
        {
            if (part == null)
            {
                return null;
            }

            if (frequency == 0)
            {
                frequency = part.frequency;
                channels = part.channels;
            }

            // ponytail: assumes all /tts lines share one format (backend emits 24kHz mono wav)
            if (part.frequency != frequency || part.channels != channels)
            {
                return null;
            }

            totalFrames += part.samples;
        }

        if (totalFrames == 0)
        {
            return null;
        }

        int gapFrames = Mathf.Max(0, Mathf.RoundToInt(gapSeconds * frequency));
        totalFrames += gapFrames * (parts.Length - 1);

        float[] data = new float[totalFrames * channels];
        int offset = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                offset += gapFrames * channels;
            }

            float[] chunk = new float[parts[i].samples * parts[i].channels];
            parts[i].GetData(chunk, 0);
            chunk.CopyTo(data, offset);
            offset += chunk.Length;
        }

        AudioClip merged = AudioClip.Create("tts_echo", totalFrames, channels, frequency, false);
        merged.SetData(data, 0);
        return merged;
    }

    private void OnDisable()
    {
        StopPlacementVoice();
        StopSoundPlayback();
        StopRecordingWithoutUpload();
        StopUploadFeedback();
        recordingSuccessPlaying = false;
        crowFeedbackPlaying = false;
        StopActionPulse(soundButtonRoot, ref soundPulseRoutine);
        StopActionPulse(micButtonRoot, ref micPulseRoutine);
    }

    public void PrepareForIntro()
    {
        ResolveStones();
        ResolveCraftResultUi();
        StopSoundPlayback();
        StopRecordingWithoutUpload();
        StopUploadFeedback();
        recordingSuccessPlaying = false;

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            slotOccupants[i] = null;
        }

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].CaptureHome();
            stones[i].SetCurrentSlot(-1);
            stones[i].PrepareHidden(new Vector2(0f, revealYOffset), Mathf.Max(0.01f, revealStartScale));
        }

        prepared = true;
        revealFinished = false;
        ritualPlaying = false;
        ritualCompleted = false;
        crowFeedbackPlaying = false;

        SetRootVisible(true);
        DisableDecorativeBookRaycasts();
        HideCraftResultUi();
    }

    public IEnumerator PlayIntroReveal(bool simultaneous = false)
    {
        ResolveStones();

        if (!prepared)
        {
            PrepareForIntro();
        }

        if (revealFinished)
        {
            yield break;
        }

        Vector2 offset = new Vector2(0f, revealYOffset);
        float startScale = Mathf.Max(0.01f, revealStartScale);

        if (simultaneous)
        {
            for (int i = 0; i < stones.Count; i++)
                StartCoroutine(stones[i].Reveal(revealDuration, offset, startScale));
            yield return new WaitForSeconds(revealDuration);
        }
        else
        {
            for (int i = 0; i < stones.Count; i++)
            {
                yield return stones[i].Reveal(revealDuration, offset, startScale);
                if (i < stones.Count - 1 && revealDelayBetweenStones > 0f)
                    yield return new WaitForSeconds(revealDelayBetweenStones);
            }
        }

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(true);
        }

        _latency.Start(Time.realtimeSinceStartupAsDouble);
        revealFinished = true;
    }

    public void HandleStoneClicked(MagicStonePuzzleStone stone)
    {
        if (stone == null || !CanInteract)
        {
            return;
        }

        GameAudio.PlayClick();
        int currentSlot = stone.CurrentSlot;

        // Tapping a stone that is already in a slot sends it back home and frees the slot.
        if (IsValidSlot(currentSlot))
        {
            if (slotOccupants[currentSlot] == stone)
            {
                slotOccupants[currentSlot] = null;
            }

            stone.SetCurrentSlot(-1);
            stone.ReturnHome();
            return;
        }

        // Gate: while a previously-placed stone's example sound is still playing, the child
        // cannot drop the next stone into a slot (they must wait for the phoneme to finish).
        if (blockPlacementWhileVoicePlaying && isPlacementVoicePlaying)
        {
            return;
        }

        // Otherwise drop it into the first free slot (slot 1, then slot 2).
        int targetSlot = GetFirstEmptySlot();
        if (!IsValidSlot(targetSlot))
        {
            return;
        }

        SnapStoneToSlot(stone, targetSlot);
    }

    private int GetFirstEmptySlot()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void SnapStoneToSlot(MagicStonePuzzleStone stone, int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
        {
            stone.ReturnHome();
            return;
        }

        MagicStonePuzzleStone existing = slotOccupants[slotIndex];
        if (existing != null && existing != stone)
        {
            existing.SetCurrentSlot(-1);
            existing.ReturnHome();
        }

        slotOccupants[slotIndex] = stone;
        stone.SetCurrentSlot(slotIndex);
        stone.MoveTo(GetSlotPosition(slotIndex), stone.HomeScale * Mathf.Max(0.01f, snappedScaleMultiplier), snapDuration, true);

        _latency.RecordPlacement(Time.realtimeSinceStartupAsDouble);
        PlayPlacementVoice(stone);

        TryStartCompletionRitual();
    }

    private int GetNearestSlot(Vector2 position, out float nearestDistance)
    {
        int nearestSlot = -1;
        nearestDistance = float.MaxValue;

        for (int i = 0; i < 2; i++)
        {
            float distance = Vector2.Distance(position, GetSlotPosition(i));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSlot = i;
            }
        }

        return nearestSlot;
    }

    private Vector2 GetSlotPosition(int index)
    {
        RectTransform slot = GetSlotRect(index);
        if (slot != null)
        {
            return GetSlotPositionInPuzzleSpace(slot);
        }

        return index == 0 ? firstSlotPosition : secondSlotPosition;
    }

    private RectTransform GetSlotRect(int index)
    {
        if (index == 0)
        {
            if (firstSlot == null)
            {
                firstSlot = FindSiblingRect("inputSlot1");
            }

            return firstSlot;
        }

        if (index == 1)
        {
            if (secondSlot == null)
            {
                secondSlot = FindSiblingRect("inputSlot2");
            }

            return secondSlot;
        }

        return null;
    }

    private Vector2 GetSlotPositionInPuzzleSpace(RectTransform slot)
    {
        if (slot == null)
        {
            return Vector2.zero;
        }

        RectTransform puzzleSpace = DragParent;
        if (puzzleSpace == null || slot.parent == puzzleSpace)
        {
            return slot.anchoredPosition;
        }

        Vector3 worldPosition = slot.TransformPoint(slot.rect.center);
        Vector3 localPosition = puzzleSpace.InverseTransformPoint(worldPosition);
        return new Vector2(localPosition.x, localPosition.y);
    }

    private static bool IsValidSlot(int index)
    {
        return index >= 0 && index < 2;
    }

    private void ResolveStones()
    {
        rectTransform = rectTransform != null ? rectTransform : transform as RectTransform;

        if (stonePa == null)
        {
            stonePa = FindChildRect("stone3");
        }

        if (stoneKa == null)
        {
            stoneKa = FindChildRect("stone2");
        }

        if (stoneSaraAa == null)
        {
            stoneSaraAa = FindChildRect("stone1");
        }

        stones.Clear();
        AddStone(stonePa);
        AddStone(stoneKa);
        AddStone(stoneSaraAa);
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform child = transform.Find(childName);
        return child as RectTransform;
    }

    private RectTransform FindSiblingRect(string siblingName)
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return null;
        }

        Transform sibling = parent.Find(siblingName);
        if (sibling != null)
        {
            return sibling as RectTransform;
        }

        return FindDescendantRect(parent, siblingName, transform);
    }

    private void AddStone(RectTransform stoneRect)
    {
        if (stoneRect == null || !IsStoneRect(stoneRect))
        {
            return;
        }

        MagicStonePuzzleStone stone = stoneRect.GetComponent<MagicStonePuzzleStone>();
        if (stone == null)
        {
            stone = stoneRect.gameObject.AddComponent<MagicStonePuzzleStone>();
        }

        stone.Initialize(this);
        stone.SetLetter(GetLetterForStone(stoneRect));
        stones.Add(stone);

        Image image = stoneRect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private string GetLetterForStone(RectTransform stoneRect)
    {
        if (stoneRect == null)
        {
            return "";
        }

        string objectName = stoneRect.gameObject.name;

        if (stoneObjectNames != null && stoneLetters != null)
        {
            int count = Mathf.Min(stoneObjectNames.Length, stoneLetters.Length);
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(stoneObjectNames[i], objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return stoneLetters[i];
                }
            }
        }

        // Fallback so the recipe still works even if the serialized arrays are empty.
        switch (objectName.ToLowerInvariant())
        {
            case "stone1": return "ก";
            case "stone2": return "ป";
            case "stone3": return "า";
            default: return "";
        }
    }

    private void TryStartCompletionRitual()
    {
        if (ritualPlaying || ritualCompleted)
        {
            return;
        }

        if (slotOccupants[0] == null || slotOccupants[1] == null)
        {
            return;
        }

        // Both slots full: route by the assembled word. The target word (ปา) reveals book_craft_pa
        // and ends at Success_pa; the alt word (กา) reveals book_craft_ga and ends at Success_ga.
        // Same flow + identical backend handling — only the page, wordId and success scene differ.
        string built = GetAssembledWord();
        string target = string.IsNullOrEmpty(targetWord) ? "ปา" : targetWord;

        if (built == target)
        {
            SetActiveResultVariant(GetCrowBookRoot(), targetWordId, nextSceneName, target);
        }
        else if (!string.IsNullOrEmpty(altWord) && built == altWord)
        {
            SetActiveResultVariant(GetAltBookRoot(), altWordId, altSceneName, altWord);
        }
        else
        {
            // Not a valid word: leave the stones in place so the player can tap them back out and
            // retry. No backend send here — like word_build_paa_polished, the build-attempt (and
            // /grade) fire together later at mic-stop. Just restart the build timer.
            _latency.Start(Time.realtimeSinceStartupAsDouble);
            return;
        }

        completionRoutine = StartCoroutine(WordResultRoutine());
    }

    // Pick the result page / backend wordId / success scene for the word that was just built.
    private void SetActiveResultVariant(RectTransform bookRoot, string wordId, string sceneName, string word)
    {
        activeBookRoot = bookRoot;
        activeWordId = wordId;
        activeSceneName = sceneName;
        activeResultWord = word;
    }

    private RectTransform GetCrowBookRoot()
    {
        if (bookCraftCrowRoot == null) bookCraftCrowRoot = FindSiblingRect("book_craft_pa");
        return bookCraftCrowRoot;
    }

    private RectTransform GetAltBookRoot()
    {
        if (bookCraftAltRoot == null) bookCraftAltRoot = FindSiblingRect("book_craft_ga");
        return bookCraftAltRoot;
    }

    // The result-word echo clip for the page that's currently showing: กอ-อา-กา for the alt
    // word (กา), ปอ-อา-ปา (soundPlaybackClip) otherwise.
    private AudioClip ActiveSoundClip =>
        (!string.IsNullOrEmpty(altWord) && activeResultWord == altWord && altSoundPlaybackClip != null)
            ? altSoundPlaybackClip
            : soundPlaybackClip;

    private string GetAssembledWord()
    {
        if (slotOccupants[0] == null || slotOccupants[1] == null)
        {
            return "";
        }

        return (slotOccupants[0].Letter ?? "") + (slotOccupants[1].Letter ?? "");
    }

    // Result for a correctly assembled word: crossfade to the book_craft_crow page
    // (revealing its prefab / stone / stone_example) and fade in the sound + mic icons.
    // Stays on this page — no scene transition.
    private IEnumerator WordResultRoutine()
    {
        ritualPlaying = true;
        // Freeze buildLatencyMs now; the build-attempt + /grade fire together later at mic-stop
        // (CompleteRecordingAndUpload), exactly like word_build_paa_polished — so the webapp pairs
        // them into one row instead of logging a separate attempt at assembly time.
        _latency.Complete(Time.realtimeSinceStartupAsDouble);

        // Progression telemetry ("where they are"), mirroring word_build_paa_polished on a correct
        // build: bump the sitting's cleared count and post a quest_completed event.
        SessionContext clearedSession = SessionContext.Instance;
        if (clearedSession != null) clearedSession.RecordCleared();
        if (telemetry != null)
        {
            string kid = clearedSession != null && !string.IsNullOrEmpty(clearedSession.KidId) ? clearedSession.KidId : childId;
            telemetry.PostProgressEvent(kid, "quest_completed", questId);
        }

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
        }

        float settleDelay = Mathf.Max(craftResultDelay, snapDuration);
        if (settleDelay > 0f)
        {
            yield return new WaitForSeconds(settleDelay);
        }

        yield return ShowCrowCraftRoutine();

        HideSlotFrames();

        if (actionIconFadeDelay > 0f)
        {
            yield return new WaitForSeconds(actionIconFadeDelay);
        }

        yield return FadeInActionIcons();

        ritualCompleted = true;
        ritualPlaying = false;
        completionRoutine = null;

        // Post-build echo: voice the assembled word once so the child hears the result before
        // they recite it. They can replay it any time via the sound button, or speak via mic.
        if (autoPlayResultClip)
        {
            HandleSoundButtonClicked();
        }
    }

    private void HideSlotFrames()
    {
        HideUi(GetSlotRect(0));
        HideUi(GetSlotRect(1));
    }

    private IEnumerator CompletionRitualRoutine()
    {
        ritualPlaying = true;

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
        }

        float settleDelay = Mathf.Max(craftResultDelay, snapDuration);
        if (settleDelay > 0f)
        {
            yield return new WaitForSeconds(settleDelay);
        }

        yield return ShowCraftSuccessRoutine();

        ritualCompleted = true;
        ritualPlaying = false;
        completionRoutine = null;
    }

    private bool IsCorrectCraftRecipe()
    {
        ResolveStones();
        return slotOccupants[0] != null
            && slotOccupants[1] != null
            && slotOccupants[0].RectTransform == stoneKa
            && slotOccupants[1].RectTransform == stonePa;
    }

    private bool IsCrowCraftRecipe()
    {
        ResolveStones();
        return slotOccupants[0] != null
            && slotOccupants[1] != null
            && slotOccupants[0].RectTransform == stoneSaraAa
            && slotOccupants[1].RectTransform == stonePa;
    }

    private IEnumerator CrowCraftFeedbackRoutine()
    {
        crowFeedbackPlaying = true;

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
        }

        float settleDelay = Mathf.Max(craftResultDelay, snapDuration);
        if (settleDelay > 0f)
        {
            yield return new WaitForSeconds(settleDelay);
        }

        yield return ShowCrowCraftRoutine();

        RequestRetryAfterCrow();

        if (crowFlashDelay > 0f)
        {
            yield return new WaitForSeconds(crowFlashDelay);
        }

        string sceneName = string.IsNullOrWhiteSpace(crowSceneName) ? "cut_scene3" : crowSceneName.Trim();
        if (useWhiteFlashToCrowScene)
        {
            Image flashImage = CreateFullscreenImage("MagicStoneCrowFlash");
            yield return WhiteFlash(flashImage);
            PlaySceneTransition(sceneName, flashImage);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private IEnumerator ShowCraftSuccessRoutine()
    {
        ResolveCraftResultUi();

        CanvasGroup bookGroup = EnsureCanvasGroup(bookCraftRoot);
        CanvasGroup successGroup = EnsureCanvasGroup(bookCraftSuccessRoot);
        CanvasGroup crowGroup = EnsureCanvasGroup(bookCraftCrowRoot);
        CanvasGroup stoneRootGroup = EnsureCanvasGroup(rectTransform);

        Vector3 successTargetScale = bookCraftSuccessRoot != null ? bookCraftSuccessRoot.localScale : Vector3.one;
        Vector3 successStartScale = successTargetScale * Mathf.Max(0.01f, craftResultStartScale);

        if (bookCraftRoot != null)
        {
            bookCraftRoot.gameObject.SetActive(true);
            SetUiGroup(bookGroup, 1f, false);
        }

        if (bookCraftSuccessRoot != null)
        {
            bookCraftSuccessRoot.gameObject.SetActive(true);
            bookCraftSuccessRoot.localScale = successStartScale;
            SetUiGroup(successGroup, 0f, false);
        }

        HideUi(bookCraftCrowRoot);

        SetUiGroup(stoneRootGroup, 1f, false);
        HideActionIcons();

        float duration = Mathf.Max(0.01f, craftResultFadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            float pop = EaseOutBack(t);

            SetUiGroup(bookGroup, 1f - smooth, false);
            SetUiGroup(successGroup, smooth, false);
            SetUiGroup(crowGroup, 0f, false);
            SetUiGroup(stoneRootGroup, 1f - smooth, false);

            if (bookCraftSuccessRoot != null)
            {
                bookCraftSuccessRoot.localScale = Vector3.LerpUnclamped(successStartScale, successTargetScale, pop);
            }

            yield return null;
        }

        SetUiGroup(bookGroup, 0f, false);
        SetUiGroup(successGroup, 1f, false);
        SetUiGroup(stoneRootGroup, 0f, false);

        if (bookCraftRoot != null)
        {
            bookCraftRoot.gameObject.SetActive(false);
        }

        if (bookCraftSuccessRoot != null)
        {
            bookCraftSuccessRoot.localScale = successTargetScale;
        }

        if (actionIconFadeDelay > 0f)
        {
            yield return new WaitForSeconds(actionIconFadeDelay);
        }

        yield return FadeInActionIcons();
    }

    private IEnumerator ShowCrowCraftRoutine()
    {
        ResolveCraftResultUi();

        // Reveal the page for the word that was actually built (ปา -> book_craft_pa,
        // กา -> book_craft_ga); keep the other word's page hidden.
        RectTransform crowRoot = activeBookRoot != null ? activeBookRoot : bookCraftCrowRoot;
        RectTransform inactiveRoot = crowRoot == bookCraftAltRoot ? bookCraftCrowRoot : bookCraftAltRoot;

        CanvasGroup bookGroup = EnsureCanvasGroup(bookCraftRoot);
        CanvasGroup successGroup = EnsureCanvasGroup(bookCraftSuccessRoot);
        CanvasGroup crowGroup = EnsureCanvasGroup(crowRoot);
        CanvasGroup stoneRootGroup = EnsureCanvasGroup(rectTransform);

        Vector3 crowTargetScale = crowRoot != null ? crowRoot.localScale : Vector3.one;
        Vector3 crowStartScale = crowTargetScale * Mathf.Max(0.01f, craftResultStartScale);

        if (bookCraftRoot != null)
        {
            bookCraftRoot.gameObject.SetActive(true);
            SetUiGroup(bookGroup, 1f, false);
        }

        HideUi(bookCraftSuccessRoot);
        HideUi(inactiveRoot);

        if (crowRoot != null)
        {
            crowRoot.gameObject.SetActive(true);
            crowRoot.localScale = crowStartScale;
            SetUiGroup(crowGroup, 0f, false);
        }

        SetUiGroup(successGroup, 0f, false);
        SetUiGroup(stoneRootGroup, 1f, false);
        HideActionIcons();

        float duration = Mathf.Max(0.01f, craftResultFadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            float pop = EaseOutBack(t);

            SetUiGroup(bookGroup, 1f - smooth, false);
            SetUiGroup(crowGroup, smooth, false);
            SetUiGroup(stoneRootGroup, 1f - smooth, false);

            if (crowRoot != null)
            {
                crowRoot.localScale = Vector3.LerpUnclamped(crowStartScale, crowTargetScale, pop);
            }

            yield return null;
        }

        SetUiGroup(bookGroup, 0f, false);
        SetUiGroup(crowGroup, 1f, false);
        SetUiGroup(stoneRootGroup, 0f, false);

        if (bookCraftRoot != null)
        {
            bookCraftRoot.gameObject.SetActive(false);
        }

        if (crowRoot != null)
        {
            crowRoot.localScale = crowTargetScale;
        }

        if (crowCraftHoldDuration > 0f)
        {
            yield return new WaitForSeconds(crowCraftHoldDuration);
        }
    }

    private void ResolveCraftResultUi()
    {
        if (bookCraftRoot == null)
        {
            bookCraftRoot = FindSiblingRect("book_craft");
        }

        if (bookCraftSuccessRoot == null)
        {
            bookCraftSuccessRoot = FindSiblingRect("book_craft_success");
        }

        if (bookCraftCrowRoot == null)
        {
            bookCraftCrowRoot = FindSiblingRect("book_craft_pa");
        }

        if (bookCraftAltRoot == null)
        {
            bookCraftAltRoot = FindSiblingRect("book_craft_ga");
        }

        if (soundButtonRoot == null)
        {
            soundButtonRoot = FindSiblingChildRect("icon", "sound");
        }

        if (micButtonRoot == null)
        {
            micButtonRoot = FindSiblingChildRect("icon", "mic");
        }
    }

    private void DisableDecorativeBookRaycasts()
    {
        SetGraphicRaycastTargets(bookCraftRoot, false);
        SetGraphicRaycastTargets(bookCraftSuccessRoot, false);
        SetGraphicRaycastTargets(bookCraftCrowRoot, false);
        SetGraphicRaycastTargets(bookCraftAltRoot, false);
    }

    private void HideCraftResultUi()
    {
        HideUi(bookCraftSuccessRoot);
        HideUi(bookCraftCrowRoot);
        HideUi(bookCraftAltRoot);
        HideActionIcons();
    }

    private void HideActionIcons()
    {
        HideActionIcon(soundButtonRoot);
        HideActionIcon(micButtonRoot);
    }

    private void HideActionIcon(RectTransform iconRoot)
    {
        if (iconRoot == null)
        {
            return;
        }

        iconRoot.gameObject.SetActive(true);
        CanvasGroup group = EnsureCanvasGroup(iconRoot);
        SetUiGroup(group, 0f, false);
        SetGraphicRaycastTargets(iconRoot, false);

        Button button = iconRoot.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = false;
        }

        iconRoot.gameObject.SetActive(false);
    }

    private IEnumerator FadeInActionIcons()
    {
        Button resolvedSoundButton = EnsureActionButton(soundButtonRoot, HandleSoundButtonClicked);
        Button resolvedMicButton = EnsureActionButton(micButtonRoot, HandleMicButtonClicked);
        CanvasGroup soundGroup = EnsureCanvasGroup(soundButtonRoot);
        CanvasGroup micGroup = EnsureCanvasGroup(micButtonRoot);

        if (soundButtonRoot != null)
        {
            soundButtonRoot.gameObject.SetActive(true);
            SetUiGroup(soundGroup, 0f, false);
        }

        if (micButtonRoot != null)
        {
            micButtonRoot.gameObject.SetActive(true);
            SetUiGroup(micGroup, 0f, false);
        }

        float duration = Mathf.Max(0.01f, actionIconFadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float smooth = SmoothStep(Mathf.Clamp01(elapsed / duration));
            SetUiGroup(soundGroup, smooth, false);
            SetUiGroup(micGroup, smooth, false);
            yield return null;
        }

        SetUiGroup(soundGroup, 1f, true);
        SetUiGroup(micGroup, 1f, true);
        SetGraphicRaycastTargets(soundButtonRoot, true);
        SetGraphicRaycastTargets(micButtonRoot, true);

        if (resolvedSoundButton != null)
        {
            resolvedSoundButton.interactable = true;
        }

        if (resolvedMicButton != null)
        {
            resolvedMicButton.interactable = true;
        }
    }

    private Button EnsureActionButton(RectTransform iconRoot, UnityEngine.Events.UnityAction onClick)
    {
        if (iconRoot == null)
        {
            return null;
        }

        Image image = iconRoot.GetComponent<Image>();
        Button button = iconRoot.GetComponent<Button>();
        if (button == null)
        {
            button = iconRoot.gameObject.AddComponent<Button>();
        }

        if (image != null && button.targetGraphic == null)
        {
            button.targetGraphic = image;
        }

        // CutScene_bear has several MagicStonePuzzleController instances (book_craft, magic_stone,
        // icon, ...) that ALL resolve the SAME shared icon/sound + icon/mic buttons and each wire
        // their own handler — so one click used to fire every instance's record+/grade+build-attempt
        // (= duplicate dashboard rows). Clear first so each action button keeps exactly ONE listener
        // (the last instance to reveal owns it); word_build_paa_polished has a single controller so
        // it never hit this. ponytail: dedupe-by-last-writer; fine because all instances share state.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        if (iconRoot == soundButtonRoot)
        {
            soundButton = button;
            CaptureActionIconBaseScale(soundButtonRoot);
        }
        else if (iconRoot == micButtonRoot)
        {
            micButton = button;
            CaptureActionIconBaseScale(micButtonRoot);
        }

        return button;
    }

    private void HandleSoundButtonClicked()
    {
        if (!CanUseActionButtons() || isRecording || recordingSuccessPlaying)
        {
            return;
        }

        GameAudio.PlayClick();
        AudioClip clip = ActiveSoundClip;
        if (clip == null)
        {
            Debug.LogWarning("Sound button has no playback clip assigned.");
            return;
        }

        AudioSource source = GetOrCreateActionAudioSource();
        if (source == null)
        {
            return;
        }

        StopSoundPlayback();
        source.clip = clip;
        source.Play();
        SetActionButtonInteractable(micButton, false);
        StartActionPulse(soundButtonRoot, ref soundPulseRoutine);
        soundPlaybackRoutine = StartCoroutine(SoundPlaybackStateRoutine(source));
    }

    private void HandleMicButtonClicked()
    {
        if (!CanUseActionButtons() || recordingSuccessPlaying)
        {
            return;
        }

        GameAudio.PlayClick();
        if (isRecording)
        {
            CompleteRecordingAndUpload();
            return;
        }

        StopSoundPlayback();

        if (micRecordingRoutine != null)
        {
            return;
        }

        micRecordingRoutine = StartCoroutine(RecordingRoutine());
    }

    private bool CanUseActionButtons()
    {
        return ritualCompleted && !ritualPlaying;
    }

    private AudioSource GetOrCreateActionAudioSource()
    {
        if (actionAudioSource != null)
        {
            return actionAudioSource;
        }

        actionAudioSource = GetComponent<AudioSource>();
        if (actionAudioSource == null)
        {
            actionAudioSource = gameObject.AddComponent<AudioSource>();
        }

        actionAudioSource.playOnAwake = false;
        return actionAudioSource;
    }

    private IEnumerator SoundPlaybackStateRoutine(AudioSource source)
    {
        while (source != null && source.isPlaying)
        {
            yield return null;
        }

        StopActionPulse(soundButtonRoot, ref soundPulseRoutine);
        SetActionButtonInteractable(micButton, CanUseActionButtons() && !isRecording);
        soundPlaybackRoutine = null;
    }

    private void StopSoundPlayback()
    {
        if (soundPlaybackRoutine != null)
        {
            StopCoroutine(soundPlaybackRoutine);
            soundPlaybackRoutine = null;
        }

        if (actionAudioSource != null && actionAudioSource.isPlaying)
        {
            actionAudioSource.Stop();
        }

        StopActionPulse(soundButtonRoot, ref soundPulseRoutine);
        SetActionButtonInteractable(micButton, CanUseActionButtons() && !isRecording);
    }

    // ---- stone placement voice (pre-baked phoneme per stone) ----

    // Pick the placement clip for a stone by matching its Letter against stoneLetters, paired
    // index-for-index with stonePlacementClips (e.g. "ก"/"ป"/"า"). Null if unmapped/unassigned.
    private AudioClip GetPlacementClipForStone(MagicStonePuzzleStone stone)
    {
        if (stone == null || stonePlacementClips == null || stoneLetters == null)
        {
            return null;
        }

        string letter = stone.Letter ?? "";
        int count = Mathf.Min(stoneLetters.Length, stonePlacementClips.Length);
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(stoneLetters[i], letter, System.StringComparison.Ordinal))
            {
                return stonePlacementClips[i];
            }
        }

        return null;
    }

    private float GetPlacementVolumeForStone(MagicStonePuzzleStone stone)
    {
        if (stone == null || stonePlacementVolumes == null || stoneLetters == null) return 1f;
        string letter = stone.Letter ?? "";
        int count = Mathf.Min(stoneLetters.Length, stonePlacementVolumes.Length);
        for (int i = 0; i < count; i++)
            if (string.Equals(stoneLetters[i], letter, System.StringComparison.Ordinal))
                return Mathf.Clamp(stonePlacementVolumes[i], 0f, 1f);
        return 1f;
    }

    private void PlayPlacementVoice(MagicStonePuzzleStone stone)
    {
        AudioClip clip = GetPlacementClipForStone(stone);
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetOrCreateActionAudioSource();
        if (source == null)
        {
            return;
        }

        StopSoundPlayback();
        if (placementVoiceRoutine != null)
        {
            StopCoroutine(placementVoiceRoutine);
        }

        float volume = GetPlacementVolumeForStone(stone);
        placementVoiceRoutine = StartCoroutine(PlacementVoiceRoutine(source, clip, volume));
    }

    private IEnumerator PlacementVoiceRoutine(AudioSource source, AudioClip clip, float volume = 1f)
    {
        isPlacementVoicePlaying = true;
        source.PlayOneShot(clip, volume);

        float until = Time.realtimeSinceStartup + clip.length;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
        }

        isPlacementVoicePlaying = false;
        placementVoiceRoutine = null;
    }

    private void StopPlacementVoice()
    {
        if (placementVoiceRoutine != null)
        {
            StopCoroutine(placementVoiceRoutine);
            placementVoiceRoutine = null;
        }

        isPlacementVoicePlaying = false;
    }

    private IEnumerator RecordingRoutine()
    {
        // Record exactly like word_build_paa_polished: GradeApiClient owns mic capture + the
        // /grade upload. We only drive the on-screen mic window + pulse here.
        GradeApiClient grade = GetOrCreateGradeClient();
        if (grade == null)
        {
            Debug.LogWarning("Could not create GradeApiClient for recording.");
            micRecordingRoutine = null;
            yield break;
        }

        grade.StartRecording();

        isRecording = true;
        recordingStartedAt = Time.realtimeSinceStartup;
        SetActionButtonInteractable(soundButton, false);
        SetActionButtonInteractable(micButton, true);
        StartActionPulse(micButtonRoot, ref micPulseRoutine);

        float safeMaxSeconds = Mathf.Max(1f, maxRecordingSeconds);
        while (isRecording && Time.realtimeSinceStartup - recordingStartedAt < safeMaxSeconds)
        {
            yield return null;
        }

        if (isRecording)
        {
            CompleteRecordingAndUpload();
        }

        micRecordingRoutine = null;
    }

    private void CompleteRecordingAndUpload()
    {
        if (!isRecording)
        {
            StopRecordingWithoutUpload();
            return;
        }

        isRecording = false;
        StopActionPulse(micButtonRoot, ref micPulseRoutine);
        SetActionButtonInteractable(soundButton, CanUseActionButtons());

        // Stop the mic and report, exactly like word_build_paa_polished MicAndResolve: fire /grade
        // and the build-attempt TOGETHER, once, at mic-stop, with the same word + outcome. This is
        // what makes the webapp show a single row per recording (the build-attempt opens the row,
        // /grade fills its accuracy + time) instead of a separate attempt logged at assembly time.
        if (uploadRecordingToBackend)
        {
            GradeApiClient grade = GetOrCreateGradeClient();
            if (grade != null)
            {
                // Grade against the word that was actually crafted (ปา or กา) so its tag is "correct".
                string target = !string.IsNullOrEmpty(activeResultWord) ? activeResultWord
                    : (string.IsNullOrEmpty(targetWord) ? "ปา" : targetWord);
                string tag = OutcomeTag(OutcomeEvaluator.Evaluate(GetAssembledWord(), target, knownWords));

                GradeApiClient.GradeContext ctx = BuildGradeContext();
                ctx.outcomeTag = tag; // grade carries the outcome too, like word_build's FireGrade
                grade.StopAndGrade(ctx, OnGraded);
                FireBuildAttempt(tag);
            }
        }

        // Always play the on-screen success after a correct word: shake + white flash, then
        // the scene transition. (Independent of whether the upload succeeds.)
        if (uploadRecordingRoutine != null)
        {
            StopCoroutine(uploadRecordingRoutine);
        }

        uploadRecordingRoutine = StartCoroutine(RecordingSuccessRoutine());
    }

    private GradeApiClient GetOrCreateGradeClient()
    {
        if (gradeClient == null)
        {
            gradeClient = GetComponent<GradeApiClient>();
            if (gradeClient == null)
            {
                gradeClient = gameObject.AddComponent<GradeApiClient>();
            }
        }

        // GradeApiClient posts to BackendConfig.BaseUrl/grade (central host, ngrok-aware).
        return gradeClient;
    }

    private GradeApiClient.GradeContext BuildGradeContext()
    {
        // Prefer the open sitting's session/kid (like word_build_paa_polished) so the attempt
        // groups under the same session the webapp shows; fall back to the inspector fields.
        SessionContext session = SessionContext.Instance;
        string sid = session != null && !string.IsNullOrEmpty(session.SessionId) ? session.SessionId : sessionId;
        string kid = session != null && !string.IsNullOrEmpty(session.KidId) ? session.KidId : childId;

        return new GradeApiClient.GradeContext
        {
            targetWordId = !string.IsNullOrEmpty(activeWordId) ? activeWordId : targetWordId,
            childId = kid,
            questId = questId,
            sessionId = sid,
            sceneId = recordingSceneId,
            outcomeTag = null,
            buildLatencyMs = _latency.HasResult ? _latency.TotalMs : 0
        };
    }

    // Durable telemetry, mirroring word_build_paa_polished FireBuildAttempt: one row per build try
    // (target word + what was assembled + outcome + latency). Independent of the mic, so it lands
    // even if /grade gets nothing.
    private void FireBuildAttempt(string outcomeTag)
    {
        long latency = _latency.HasResult ? _latency.TotalMs : 0;
        SessionContext session = SessionContext.Instance;
        if (session != null) session.RecordAttempt(latency);

        if (telemetry == null) telemetry = FindFirstObjectByType<TelemetryClient>();
        if (telemetry == null) return;

        string kid = session != null && !string.IsNullOrEmpty(session.KidId) ? session.KidId : childId;
        string sid = session != null ? session.SessionId : null;
        string wordId = !string.IsNullOrEmpty(activeWordId) ? activeWordId : targetWordId;
        telemetry.PostBuildAttempt(kid, sid, wordId, GetAssembledWord(), outcomeTag, latency);
    }

    private static string OutcomeTag(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Correct: return "correct";
            case Outcome.WrongWord: return "wrong_word";
            default: return "non_word";
        }
    }

    // Mirror word_build_paa_polished: feed the hidden /grade result back into the sitting
    // aggregate so the bear encounter groups under the same session KPIs.
    private void OnGraded(GradeResponse r)
    {
        if (r == null) return;
        if (SessionContext.Instance != null) SessionContext.Instance.RecordGrade(r.par);
        Debug.Log($"[MagicStone] /grade (hidden): PAR {r.par:0.00} grade {r.grade}");
    }

    private void StopRecordingWithoutUpload()
    {
        if (micRecordingRoutine != null)
        {
            StopCoroutine(micRecordingRoutine);
            micRecordingRoutine = null;
        }

        isRecording = false;
        StopActionPulse(micButtonRoot, ref micPulseRoutine);
        SetActionButtonInteractable(soundButton, CanUseActionButtons());
        SetActionButtonInteractable(micButton, CanUseActionButtons());
    }

    private IEnumerator RecordingSuccessRoutine()
    {
        recordingSuccessPlaying = true;
        SetActionButtonInteractable(soundButton, false);
        SetActionButtonInteractable(micButton, false);
        StopActionPulse(soundButtonRoot, ref soundPulseRoutine);
        StopActionPulse(micButtonRoot, ref micPulseRoutine);

        if (recordingSuccessSfx != null)
        {
            AudioSource source = GetOrCreateActionAudioSource();
            if (source != null)
            {
                source.PlayOneShot(recordingSuccessSfx);
            }
        }

        yield return ShowRecordingSuccessMarkRoutine();

        Image flashImage = null;
        if (playSuccessShakeAndFlash)
        {
            flashImage = CreateFullscreenImage("MagicStoneWhiteFlash");
            SetFlashAlpha(flashImage, 0f);
            yield return ShakeRitualTargets(flashImage);

            if (transitionDelayAfterSuccess > 0f)
            {
                yield return new WaitForSeconds(transitionDelayAfterSuccess);
            }

            yield return WhiteFlash(flashImage);
        }

        PlaySceneTransition(GetNextSceneName(), flashImage);
        uploadRecordingRoutine = null;
    }

    private IEnumerator ShowRecordingSuccessMarkRoutine()
    {
        Image successImage = CreateRecordingSuccessImage();
        float holdDuration = recordingSuccessHoldDuration;

        if (recordingSuccessSfx != null)
        {
            holdDuration = Mathf.Max(holdDuration, Mathf.Min(recordingSuccessSfx.length, 1.1f));
        }

        if (successImage == null)
        {
            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            yield break;
        }

        RectTransform successRect = successImage.transform as RectTransform;
        Color color = successImage.color;
        color.a = 0f;
        successImage.color = color;

        Vector3 targetScale = Vector3.one * Mathf.Max(0.01f, recordingSuccessEndScale);
        Vector3 startScale = targetScale * Mathf.Max(0.01f, recordingSuccessStartScale);
        successRect.localScale = startScale;

        float duration = Mathf.Max(0.01f, recordingSuccessPopDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(t);
            float pop = EaseOutBack(t);

            color.a = smooth;
            successImage.color = color;
            successRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, pop);
            yield return null;
        }

        color.a = 1f;
        successImage.color = color;
        successRect.localScale = targetScale;

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }
    }

    private Image CreateRecordingSuccessImage()
    {
        if (recordingSuccessSprite == null)
        {
            return null;
        }

        GameObject overlayCanvasObject = new GameObject("RecordingSuccessCanvas");
        Canvas overlayCanvas = overlayCanvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler canvasScaler = overlayCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(3840f, 2160f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("RecordingSuccessCheck");
        imageObject.transform.SetParent(overlayCanvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = recordingSuccessPosition;
        imageRect.sizeDelta = recordingSuccessSize;

        Image image = imageObject.AddComponent<Image>();
        image.sprite = recordingSuccessSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void StopUploadFeedback()
    {
        if (uploadRecordingRoutine != null)
        {
            StopCoroutine(uploadRecordingRoutine);
            uploadRecordingRoutine = null;
        }

        StopActionPulse(micButtonRoot, ref micPulseRoutine);
    }

    private void CaptureActionIconBaseScale(RectTransform iconRoot)
    {
        if (iconRoot == null)
        {
            return;
        }

        if (iconRoot == soundButtonRoot && !hasSoundIconBaseScale)
        {
            soundIconBaseScale = iconRoot.localScale;
            hasSoundIconBaseScale = true;
        }
        else if (iconRoot == micButtonRoot && !hasMicIconBaseScale)
        {
            micIconBaseScale = iconRoot.localScale;
            hasMicIconBaseScale = true;
        }
    }

    private Vector3 GetActionIconBaseScale(RectTransform iconRoot)
    {
        CaptureActionIconBaseScale(iconRoot);

        if (iconRoot == soundButtonRoot)
        {
            return soundIconBaseScale;
        }

        if (iconRoot == micButtonRoot)
        {
            return micIconBaseScale;
        }

        return iconRoot != null ? iconRoot.localScale : Vector3.one;
    }

    private void StartActionPulse(RectTransform iconRoot, ref Coroutine pulseRoutine)
    {
        StopActionPulse(iconRoot, ref pulseRoutine);

        if (iconRoot == null)
        {
            return;
        }

        pulseRoutine = StartCoroutine(ActionIconPulseRoutine(iconRoot));
    }

    private void StopActionPulse(RectTransform iconRoot, ref Coroutine pulseRoutine)
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (iconRoot != null)
        {
            iconRoot.localScale = GetActionIconBaseScale(iconRoot);
        }
    }

    private IEnumerator ActionIconPulseRoutine(RectTransform iconRoot)
    {
        Vector3 baseScale = GetActionIconBaseScale(iconRoot);
        float speed = Mathf.Max(0.01f, activeIconPulseSpeed);
        float pulseScale = Mathf.Max(1f, activeIconPulseScale);

        for (float elapsed = 0f; ; elapsed += Time.deltaTime)
        {
            float t = (Mathf.Sin(elapsed * speed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, pulseScale, SmoothStep(t));
            iconRoot.localScale = baseScale * scale;
            yield return null;
        }
    }

    private static void SetActionButtonInteractable(Button button, bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }

    private static byte[] EncodeWav(AudioClip clip, int sampleFrames)
    {
        if (clip == null || sampleFrames <= 0)
        {
            return null;
        }

        int channels = Mathf.Max(1, clip.channels);
        int frequency = Mathf.Max(1, clip.frequency);
        int safeFrames = Mathf.Clamp(sampleFrames, 0, clip.samples);
        float[] samples = new float[safeFrames * channels];
        clip.GetData(samples, 0);

        const int headerSize = 44;
        const int bytesPerSample = 2;
        byte[] wav = new byte[headerSize + samples.Length * bytesPerSample];

        WriteAscii(wav, 0, "RIFF");
        WriteInt(wav, 4, wav.Length - 8);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt(wav, 16, 16);
        WriteShort(wav, 20, 1);
        WriteShort(wav, 22, (short)channels);
        WriteInt(wav, 24, frequency);
        WriteInt(wav, 28, frequency * channels * bytesPerSample);
        WriteShort(wav, 32, (short)(channels * bytesPerSample));
        WriteShort(wav, 34, 16);
        WriteAscii(wav, 36, "data");
        WriteInt(wav, 40, samples.Length * bytesPerSample);

        int offset = headerSize;
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            WriteShort(wav, offset, value);
            offset += bytesPerSample;
        }

        return wav;
    }

    private static void WriteAscii(byte[] target, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            target[offset + i] = (byte)value[i];
        }
    }

    private static void WriteInt(byte[] target, int offset, int value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        System.Buffer.BlockCopy(bytes, 0, target, offset, bytes.Length);
    }

    private static void WriteShort(byte[] target, int offset, short value)
    {
        byte[] bytes = System.BitConverter.GetBytes(value);
        System.Buffer.BlockCopy(bytes, 0, target, offset, bytes.Length);
    }

    private void HideUi(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(true);
        CanvasGroup group = EnsureCanvasGroup(target);
        SetUiGroup(group, 0f, false);
        SetGraphicRaycastTargets(target, false);
        target.gameObject.SetActive(false);
    }

    private void SetRootVisible(bool visible)
    {
        CanvasGroup group = EnsureCanvasGroup(rectTransform);
        SetUiGroup(group, visible ? 1f : 0f, visible);
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform target)
    {
        if (target == null)
        {
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static void SetUiGroup(CanvasGroup group, float alpha, bool interactable)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = Mathf.Clamp01(alpha);
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }

    private static void SetGraphicRaycastTargets(RectTransform root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            images[i].raycastTarget = enabled;
        }
    }

    private RectTransform FindSiblingChildRect(string siblingName, string childName)
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return null;
        }

        Transform sibling = parent.Find(siblingName);
        if (sibling == null)
        {
            sibling = FindDescendant(parent, siblingName, transform);
        }

        if (sibling != null)
        {
            Transform child = sibling.Find(childName);
            if (child != null)
            {
                return child as RectTransform;
            }
        }

        Transform directChild = parent.Find(childName);
        if (directChild != null)
        {
            return directChild as RectTransform;
        }

        return FindDescendantRect(parent, childName, transform);
    }

    private static bool IsStoneRect(RectTransform target)
    {
        return target != null && target.gameObject.name.StartsWith("stone", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool HasPuzzleStoneChildren()
    {
        return IsStoneRect(stonePa)
            || IsStoneRect(stoneKa)
            || IsStoneRect(stoneSaraAa)
            || transform.Find("stone1") != null
            || transform.Find("stone2") != null
            || transform.Find("stone3") != null;
    }

    private static RectTransform FindDescendantRect(Transform root, string targetName, Transform excluded)
    {
        Transform found = FindDescendant(root, targetName, excluded);
        return found as RectTransform;
    }

    private static Transform FindDescendant(Transform root, string targetName, Transform excluded)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != excluded && string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform found = FindDescendant(child, targetName, excluded);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private string GetNextSceneName()
    {
        string scene = !string.IsNullOrWhiteSpace(activeSceneName) ? activeSceneName : nextSceneName;
        return string.IsNullOrWhiteSpace(scene) ? DefaultNextSceneName : scene.Trim();
    }

    private void PlaySceneTransition(string sceneName, Image flashImage)
    {
        MagicStoneSceneTransitionRunner runner = GetTransitionRunner(flashImage);
        if (runner == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        runner.Play(sceneName, flashImage, whiteFlashFadeOutDuration, whiteFlashColor);
    }

    private MagicStoneSceneTransitionRunner GetTransitionRunner(Image flashImage)
    {
        if (flashImage == null)
        {
            return null;
        }

        Transform overlayRoot = flashImage.transform.root;
        if (overlayRoot == null)
        {
            return null;
        }

        MagicStoneSceneTransitionRunner runner = overlayRoot.GetComponent<MagicStoneSceneTransitionRunner>();
        if (runner == null)
        {
            runner = overlayRoot.gameObject.AddComponent<MagicStoneSceneTransitionRunner>();
        }

        return runner;
    }

    private IEnumerator ShakeRitualTargets(Image transitionImage)
    {
        RectTransform root = GetRitualShakeRoot();
        List<RectTransform> shakeTargets = GetRitualShakeTargets(root);
        List<Vector2> startPositions = new List<Vector2>(shakeTargets.Count);
        List<Vector3> startScales = new List<Vector3>(shakeTargets.Count);

        for (int i = 0; i < shakeTargets.Count; i++)
        {
            startPositions.Add(shakeTargets[i].anchoredPosition);
            startScales.Add(shakeTargets[i].localScale);
        }

        float safeDuration = Mathf.Max(0.01f, ritualShakeDuration);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float ramp = Mathf.Pow(SmoothStep(t), Mathf.Max(0.1f, ritualShakeRampPower));
            float intensity = Mathf.Lerp(Mathf.Max(0f, ritualShakeStartIntensity), Mathf.Max(0f, ritualShakeEndIntensity), ramp);
            float frequency = Mathf.Lerp(ritualShakeFrequency * 0.55f, ritualShakeFrequency * Mathf.Max(1f, ritualShakeFrequencyRampMultiplier), ramp);
            float overscanScale = Mathf.Lerp(1f, Mathf.Max(1f, ritualFullscreenOverscanScale), ramp);
            float finalPulse = Mathf.Clamp01(Mathf.InverseLerp(ritualFinalPulseStart, 1f, t));
            finalPulse = SmoothStep(finalPulse);
            float pulseX = Mathf.Sin(elapsed * frequency);
            float pulseY = Mathf.Sin(elapsed * frequency * 0.73f + 1.4f);
            float tremorX = Mathf.Sin(elapsed * frequency * 2.6f + 0.8f);
            float tremorY = Mathf.Cos(elapsed * frequency * 2.2f + 2.1f);
            Vector2 waveOffset = new Vector2(pulseX, pulseY * 0.65f) * ritualShakeStrength * intensity;
            Vector2 jitterOffset = Random.insideUnitCircle * ritualShakeStrength * 0.12f * intensity;
            Vector2 tremorOffset = new Vector2(tremorX, tremorY) * ritualShakeStrength * 0.24f * finalPulse;
            Vector2 offset = waveOffset + jitterOffset + tremorOffset;

            UpdateChargeFlash(transitionImage, ramp, finalPulse);

            for (int i = 0; i < shakeTargets.Count; i++)
            {
                shakeTargets[i].anchoredPosition = startPositions[i] + offset;

                if (ShouldOverscanShakeTarget(shakeTargets[i]))
                {
                    shakeTargets[i].localScale = startScales[i] * overscanScale;
                }
            }

            yield return null;
        }

        for (int i = 0; i < shakeTargets.Count; i++)
        {
            shakeTargets[i].anchoredPosition = startPositions[i];
            shakeTargets[i].localScale = startScales[i];
        }
    }

    private void UpdateChargeFlash(Image flashImage, float ramp, float finalPulse)
    {
        if (flashImage == null)
        {
            return;
        }

        Color color = whiteFlashColor;
        float buildupAlpha = Mathf.Clamp01(ritualChargeFlashMaxAlpha) * SmoothStep(ramp);
        float finalSurgeAlpha = Mathf.Clamp01(ritualFinalPulseAlpha) * SmoothStep(finalPulse);
        color.a = Mathf.Clamp01(Mathf.Max(flashImage.color.a, Mathf.Max(buildupAlpha, finalSurgeAlpha)));
        flashImage.color = color;
    }

    private RectTransform GetRitualShakeRoot()
    {
        if (ritualShakeRoot != null)
        {
            return ritualShakeRoot;
        }

        ritualShakeRoot = transform.parent as RectTransform;
        if (ritualShakeRoot != null)
        {
            return ritualShakeRoot;
        }

        ritualShakeRoot = FindSiblingRect("book_craft");
        return ritualShakeRoot;
    }

    private List<RectTransform> GetRitualShakeTargets(RectTransform root)
    {
        List<RectTransform> targets = new List<RectTransform>();
        if (root == null)
        {
            return targets;
        }

        if (root == transform.parent || root.GetComponent<Canvas>() != null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child != null && !IsTransitionOverlay(child))
                {
                    targets.Add(child);
                }
            }

            return targets;
        }

        targets.Add(root);
        return targets;
    }

    private static bool IsTransitionOverlay(RectTransform target)
    {
        return target != null && target.gameObject.name.StartsWith("MagicStoneWhiteFlash");
    }

    private bool ShouldOverscanShakeTarget(RectTransform target)
    {
        if (target == null || ritualFullscreenOverscanScale <= 1f)
        {
            return false;
        }

        if (target.GetComponent<Image>() == null)
        {
            return false;
        }

        string objectName = target.gameObject.name.ToLowerInvariant();
        return objectName.Contains("background");
    }

    private IEnumerator WhiteFlash(Image flashImage)
    {
        if (flashImage == null)
        {
            flashImage = CreateFullscreenImage("MagicStoneWhiteFlash");
        }

        if (flashImage == null)
        {
            yield break;
        }

        Color color = whiteFlashColor;
        float startAlpha = Mathf.Clamp01(flashImage.color.a);
        color.a = startAlpha;
        flashImage.color = color;

        float fadeDuration = Mathf.Max(0.01f, whiteFlashFadeInDuration);
        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            color.a = Mathf.Lerp(startAlpha, 1f, SmoothStep(t));
            flashImage.color = color;
            yield return null;
        }

        color.a = 1f;
        flashImage.color = color;

        if (whiteFlashHoldDuration > 0f)
        {
            yield return new WaitForSeconds(whiteFlashHoldDuration);
        }
    }

    private void SetFlashAlpha(Image flashImage, float alpha)
    {
        if (flashImage == null)
        {
            return;
        }

        Color color = whiteFlashColor;
        color.a = Mathf.Clamp01(alpha);
        flashImage.color = color;
    }

    private Image CreateFullscreenImage(string objectName)
    {
        GameObject overlayRoot = new GameObject(objectName + "Root", typeof(RectTransform));
        Object.DontDestroyOnLoad(overlayRoot);

        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = overlayRoot.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(3840f, 2160f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject flashObject = new GameObject(objectName, typeof(RectTransform));
        flashObject.transform.SetParent(overlayRoot.transform, false);

        RectTransform flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Image flashImage = flashObject.AddComponent<Image>();
        flashImage.raycastTarget = false;
        return flashImage;
    }

    private void DestroyTransitionOverlay(Image flashImage)
    {
        if (flashImage == null)
        {
            return;
        }

        Transform overlayRoot = flashImage.transform.root;
        if (overlayRoot != null && overlayRoot.gameObject.name.StartsWith("MagicStoneWhiteFlashRoot"))
        {
            Object.Destroy(overlayRoot.gameObject);
        }
    }

    private sealed class MagicStoneSceneTransitionRunner : MonoBehaviour
    {
        private Coroutine routine;

        public void Play(string sceneName, Image flashImage, float fadeOutDuration, Color flashColor)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine(sceneName, flashImage, fadeOutDuration, flashColor));
        }

        private IEnumerator PlayRoutine(string sceneName, Image flashImage, float fadeOutDuration, Color flashColor)
        {
            AsyncOperation loadOperation = null;

            try
            {
                loadOperation = SceneManager.LoadSceneAsync(sceneName);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Could not load scene '{sceneName}'. {exception.Message}");
            }

            if (loadOperation != null)
            {
                while (!loadOperation.isDone)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogError($"Could not load scene '{sceneName}'. Check that it is added to Build Settings.");
            }

            yield return FadeOutWhite(flashImage, fadeOutDuration, flashColor);
            Destroy(gameObject);
        }

        private static IEnumerator FadeOutWhite(Image flashImage, float fadeOutDuration, Color flashColor)
        {
            if (flashImage == null)
            {
                yield break;
            }

            Color color = flashColor;
            float startAlpha = Mathf.Clamp01(flashImage.color.a);
            float safeDuration = Mathf.Max(0.01f, fadeOutDuration);

            for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / safeDuration);
                color.a = Mathf.Lerp(startAlpha, 0f, SmoothStep(t));
                flashImage.color = color;
                yield return null;
            }

            color.a = 0f;
            flashImage.color = color;
        }
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutBack(float value)
    {
        value = Mathf.Clamp01(value);
        const float overshoot = 1.15f;
        float shifted = value - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }
}
