using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight word-assembly mechanic for practice_night: reveals the ก/ป/า stones, accepts
/// stone-to-slot placement, and resolves against a single target word supplied by
/// PracticeNightController.Configure. The child can replay the target word while assembling,
/// then listens and records it from the matching result page before the resolution cutscene.
///
/// Sits on the WordAssembly/magic_stone GameObject (stone1/stone2/stone3 are its children;
/// inputSlot1/inputSlot2/book_craft are siblings under the parent WordAssembly canvas) — the same
/// layout MagicStonePuzzleController used, so no scene reparenting is required.
/// </summary>
public sealed class PracticeWordAssembly : MonoBehaviour, IStonePuzzleOwner
{
    [Header("Stones (children of this GameObject)")]
    [SerializeField] private RectTransform stone1; // ก
    [SerializeField] private RectTransform stone2; // ป
    [SerializeField] private RectTransform stone3; // า

    [Header("Slots (siblings under the parent WordAssembly canvas)")]
    [SerializeField] private RectTransform inputSlot1;
    [SerializeField] private RectTransform inputSlot2;

    [Header("Assembly page (sibling under the parent WordAssembly canvas)")]
    [SerializeField] private RectTransform bookCraftRoot;

    [Header("Reveal")]
    [SerializeField] private float revealDelayBetweenStones = 0.22f;
    [SerializeField] private float revealDuration = 0.42f;
    [SerializeField] private float revealYOffset = -120f;
    [SerializeField] private float revealStartScale = 0.2f;

    [Header("Motion")]
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private float returnDuration = 0.22f;
    [SerializeField] private float snappedScaleMultiplier = 0.82f;

    [Header("Result reveal")]
    [SerializeField] private float resultRevealDelay = 0.08f;
    [SerializeField] private float resultRevealDuration = 0.38f;
    [SerializeField] private float resultStartScale = 0.96f;

    [Header("Result recording")]
    [SerializeField] private float maxRecordingSeconds = 5f;
    [SerializeField] private float recordingPulseScale = 1.1f;
    [SerializeField] private float recordingPulseSpeed = 5f;

    [Header("Sound and microphone button feedback")]
    [SerializeField] private float actionPulseScale = 1.12f;
    [SerializeField] private float actionPulseSpeed = 5.5f;
    [SerializeField] private float buttonPressScale = 0.88f;
    [SerializeField] private float buttonPressDuration = 0.14f;

    [Header("Result: slide the whole book+stones UI off-screen")]
    [Tooltip("Root to slide out. Empty = this GameObject's parent (the WordAssembly canvas child holding book_craft/magic_stone/slots).")]
    [SerializeField] private RectTransform assemblyUiRoot;
    [SerializeField] private Vector2 slideOutOffset = new Vector2(0f, -1600f);
    [SerializeField] private float slideOutDuration = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource wordAudioSource;

    [Header("Assembly sound hint (plays the target word while assembling)")]
    [SerializeField] private UnityEngine.UI.Button assemblySoundButton;

    private readonly List<MagicStonePuzzleStone> stones = new List<MagicStonePuzzleStone>();
    private readonly List<RectTransform> resultPages = new List<RectTransform>();
    private readonly MagicStonePuzzleStone[] slotOccupants = new MagicStonePuzzleStone[2];

    private string targetWord = "";
    private RectTransform resultPage;
    private AudioClip wordClip;
    private Action onSuccess;

    private bool revealFinished;
    private bool resultShown;
    private Coroutine misassemblyRoutine;
    private Coroutine recordingRoutine;
    private Button resultSoundButton;
    private Button resultMicButton;
    private Coroutine assemblySoundFeedbackRoutine;
    private Coroutine resultSoundFeedbackRoutine;
    private Vector3 resultNaturalScale = Vector3.one;
    private Vector3 assemblySoundNaturalScale = Vector3.one;
    private Vector3 resultSoundNaturalScale = Vector3.one;
    private Vector3 resultMicNaturalScale = Vector3.one;
    private bool recordStopRequested;
    private bool isRecording;
    private string recordingDevice;

    public RectTransform DragParent => transform as RectTransform;
    public float ReturnDuration => returnDuration;
    public bool CanInteract => revealFinished && !resultShown && misassemblyRoutine == null;

    private void Awake()
    {
        ResolveStones();
        ResolveSlots();
        ResolveResultPages();
        EnsureWordAudioSource();

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetCurrentSlot(-1);
            stones[i].HideImmediate();
        }

        if (assemblyUiRoot == null) assemblyUiRoot = transform.parent as RectTransform;
        HideAllResultPages();
    }

    private void OnDisable()
    {
        StopRecording();
        StopActionFeedback(ref assemblySoundFeedbackRoutine, assemblySoundButton, assemblySoundNaturalScale);
        StopActionFeedback(ref resultSoundFeedbackRoutine, resultSoundButton, resultSoundNaturalScale);
    }

    /// Sets the round's target word, result page and echo clip, and wires the success callback.
    /// Call once before PlayReveal().
    public void Configure(string word, RectTransform resultPageRoot, AudioClip resultWordClip, Action onSuccessCallback)
    {
        targetWord = word ?? "";
        resultPage = resultPageRoot;
        wordClip = resultWordClip;
        onSuccess = onSuccessCallback;
        if (resultPage != null)
        {
            AddResultPage(resultPage);
            resultNaturalScale = resultPage.localScale;
        }
        HideAllResultPages();
        ResolveResultControls();

        if (assemblySoundButton != null)
        {
            assemblySoundButton.onClick.RemoveAllListeners();
            assemblySoundButton.onClick.AddListener(PlayAssemblyHint);
            assemblySoundNaturalScale = assemblySoundButton.transform.localScale;
        }
    }

    private void PlayAssemblyHint()
    {
        if (!CanInteract || wordClip == null || wordAudioSource == null) return;
        GameAudio.PlayClick();
        PlayWordClip();
        StartActionFeedback(assemblySoundButton, assemblySoundNaturalScale, ref assemblySoundFeedbackRoutine);
    }

    public IEnumerator PlayReveal()
    {
        Vector2 offset = new Vector2(0f, revealYOffset);
        float startScale = Mathf.Max(0.01f, revealStartScale);

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetCurrentSlot(-1);
            stones[i].CaptureHome(true);
            stones[i].PrepareHidden(offset, startScale);
        }

        for (int i = 0; i < stones.Count; i++)
        {
            yield return stones[i].Reveal(revealDuration, offset, startScale);
            if (i < stones.Count - 1 && revealDelayBetweenStones > 0f)
            {
                yield return new WaitForSeconds(revealDelayBetweenStones);
            }
        }

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(true);
        }

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

        int targetSlot = GetFirstEmptySlot();
        if (!IsValidSlot(targetSlot))
        {
            return;
        }

        SnapStoneToSlot(stone, targetSlot);
    }

    private void SnapStoneToSlot(MagicStonePuzzleStone stone, int slotIndex)
    {
        if (!TryGetSlotPosition(slotIndex, out Vector2 slotPosition))
        {
            Debug.LogWarning("[PracticeWordAssembly] Input slot reference is missing; keeping the stone at its authored position.");
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
        stone.MoveTo(slotPosition, stone.HomeScale * Mathf.Max(0.01f, snappedScaleMultiplier), snapDuration, true);

        TryComplete();
    }

    private void TryComplete()
    {
        if (slotOccupants[0] == null || slotOccupants[1] == null)
        {
            return;
        }

        string built = (slotOccupants[0].Letter ?? "") + (slotOccupants[1].Letter ?? "");

        if (built == targetWord)
        {
            StartCoroutine(SuccessRoutine());
        }
        else if (misassemblyRoutine == null)
        {
            misassemblyRoutine = StartCoroutine(MisassemblyRoutine());
        }
    }

    private IEnumerator SuccessRoutine()
    {
        resultShown = true;

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
        }

        if (resultRevealDelay > 0f)
        {
            yield return new WaitForSeconds(resultRevealDelay);
        }

        yield return ShowResultPage();
    }

    private IEnumerator ShowResultPage()
    {
        HideAssemblyForResult();
        if (resultPage == null)
        {
            yield return SlideOutAssembly();
            onSuccess?.Invoke();
            yield break;
        }

        resultPage.gameObject.SetActive(true);
        resultPage.localScale = resultNaturalScale * Mathf.Max(0.01f, resultStartScale);
        SetResultControlsInteractable(false);

        float safeDuration = Mathf.Max(0.01f, resultRevealDuration);
        Vector3 start = resultPage.localScale;
        for (float t = 0f; t < safeDuration; t += Time.deltaTime)
        {
            resultPage.localScale = Vector3.LerpUnclamped(
                start,
                resultNaturalScale,
                EaseOutBack(Mathf.Clamp01(t / safeDuration)));
            yield return null;
        }
        resultPage.localScale = resultNaturalScale;

        PlayWordClip();
        if (wordClip != null)
        {
            yield return new WaitForSeconds(wordClip.length);
        }

        SetResultControlsInteractable(true);
    }

    private void HandleResultSoundClicked()
    {
        if (!resultShown || isRecording) return;
        GameAudio.PlayClick();
        PlayWordClip();
        StartActionFeedback(resultSoundButton, resultSoundNaturalScale, ref resultSoundFeedbackRoutine);
    }

    private void HandleResultMicClicked()
    {
        if (!resultShown) return;

        if (isRecording)
        {
            recordStopRequested = true;
            return;
        }

        if (recordingRoutine != null) return;

        GameAudio.PlayClick();
        recordingRoutine = StartCoroutine(RecordThenResolve());
    }

    private IEnumerator RecordThenResolve()
    {
        isRecording = true;
        recordStopRequested = false;
        if (wordAudioSource != null) wordAudioSource.Stop();
        yield return PlayButtonPress(resultMicButton, resultMicNaturalScale);
        SetResultControlsInteractable(false);
        if (resultMicButton != null) resultMicButton.interactable = true;

        bool microphoneStarted = false;
#if UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
#endif
        {
            string[] devices = Microphone.devices;
            if (devices != null && devices.Length > 0)
            {
                recordingDevice = devices[0];
                try
                {
                    microphoneStarted = Microphone.Start(
                        recordingDevice,
                        false,
                        Mathf.CeilToInt(Mathf.Max(1f, maxRecordingSeconds)) + 1,
                        16000) != null;
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[PracticeWordAssembly] Could not start microphone recording: {exception.Message}");
                    recordingDevice = null;
                }
            }
        }

        float duration = microphoneStarted ? Mathf.Max(1f, maxRecordingSeconds) : 0.15f;
        float endTime = Time.realtimeSinceStartup + duration;
        while (!recordStopRequested && Time.realtimeSinceStartup < endTime)
        {
            PulseRecordingButton();
            yield return null;
        }

        StopRecording();
        SetResultControlsInteractable(false);
        yield return SlideOutAssembly();
        onSuccess?.Invoke();
        recordingRoutine = null;
    }

    private IEnumerator SlideOutAssembly()
    {
        if (assemblyUiRoot == null || slideOutDuration <= 0f) yield break;

        Vector2 from = assemblyUiRoot.anchoredPosition;
        Vector2 to = from + slideOutOffset;
        float safe = Mathf.Max(0.01f, slideOutDuration);
        for (float t = 0f; t < safe; t += Time.deltaTime)
        {
            assemblyUiRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, SmoothStep(Mathf.Clamp01(t / safe)));
            yield return null;
        }
        assemblyUiRoot.anchoredPosition = to;
    }

    private void HideAssemblyForResult()
    {
        if (bookCraftRoot != null) bookCraftRoot.gameObject.SetActive(false);
        if (inputSlot1 != null) inputSlot1.gameObject.SetActive(false);
        if (inputSlot2 != null) inputSlot2.gameObject.SetActive(false);

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].SetInteractable(false);
            stones[i].gameObject.SetActive(false);
        }
    }

    private void ResolveResultPages()
    {
        resultPages.Clear();
        if (transform.parent == null) return;

        AddResultPage(transform.parent.Find("book_craft_pa") as RectTransform);
        AddResultPage(transform.parent.Find("book_craft_ga") as RectTransform);
    }

    private void AddResultPage(RectTransform page)
    {
        if (page != null && !resultPages.Contains(page)) resultPages.Add(page);
    }

    private void HideAllResultPages()
    {
        for (int i = 0; i < resultPages.Count; i++)
        {
            RectTransform page = resultPages[i];
            if (page != null) page.gameObject.SetActive(false);
        }
    }

    private void ResolveResultControls()
    {
        resultSoundButton = FindButton(resultPage, "result_sound");
        resultMicButton = FindButton(resultPage, "result_mic");

        if (resultSoundButton != null)
        {
            resultSoundButton.onClick.RemoveAllListeners();
            resultSoundButton.onClick.AddListener(HandleResultSoundClicked);
            resultSoundNaturalScale = resultSoundButton.transform.localScale;
        }

        if (resultMicButton != null)
        {
            resultMicButton.onClick.RemoveAllListeners();
            resultMicButton.onClick.AddListener(HandleResultMicClicked);
            resultMicNaturalScale = resultMicButton.transform.localScale;
        }
    }

    private static Button FindButton(RectTransform page, string childName)
    {
        if (page == null) return null;
        Transform child = page.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private void SetResultControlsInteractable(bool value)
    {
        if (resultSoundButton != null) resultSoundButton.interactable = value;
        if (resultMicButton != null) resultMicButton.interactable = value;
    }

    private void PulseRecordingButton()
    {
        if (resultMicButton == null) return;
        float pulse = 1f + (Mathf.Sin(Time.realtimeSinceStartup * recordingPulseSpeed) + 1f) * 0.5f
            * Mathf.Max(0f, recordingPulseScale - 1f);
        resultMicButton.transform.localScale = resultMicNaturalScale * pulse;
    }

    private void StartActionFeedback(Button button, Vector3 naturalScale, ref Coroutine routine)
    {
        StopActionFeedback(ref routine, button, naturalScale);
        if (button != null) routine = StartCoroutine(ActionFeedbackRoutine(button, naturalScale));
    }

    private void StopActionFeedback(ref Coroutine routine, Button button, Vector3 naturalScale)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (button != null) button.transform.localScale = naturalScale;
    }

    private IEnumerator ActionFeedbackRoutine(Button button, Vector3 naturalScale)
    {
        yield return PlayButtonPress(button, naturalScale);

        float speed = Mathf.Max(0.01f, actionPulseSpeed);
        float pulseScale = Mathf.Max(1f, actionPulseScale);
        for (float elapsed = 0f; wordAudioSource != null && wordAudioSource.isPlaying; elapsed += Time.deltaTime)
        {
            float wave = (Mathf.Sin(elapsed * speed) + 1f) * 0.5f;
            button.transform.localScale = naturalScale * Mathf.Lerp(1f, pulseScale, SmoothStep(wave));
            yield return null;
        }

        button.transform.localScale = naturalScale;
    }

    private IEnumerator PlayButtonPress(Button button, Vector3 naturalScale)
    {
        if (button == null) yield break;

        float halfDuration = Mathf.Max(0.01f, buttonPressDuration) * 0.5f;
        Vector3 pressedScale = naturalScale * Mathf.Clamp(buttonPressScale, 0.1f, 1f);

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            button.transform.localScale = Vector3.LerpUnclamped(
                naturalScale,
                pressedScale,
                SmoothStep(Mathf.Clamp01(elapsed / halfDuration)));
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            button.transform.localScale = Vector3.LerpUnclamped(
                pressedScale,
                naturalScale,
                EaseOutBack(Mathf.Clamp01(elapsed / halfDuration)));
            yield return null;
        }

        button.transform.localScale = naturalScale;
    }

    private void EnsureWordAudioSource()
    {
        if (wordAudioSource == null) wordAudioSource = GetComponent<AudioSource>();
        if (wordAudioSource == null) wordAudioSource = gameObject.AddComponent<AudioSource>();
        wordAudioSource.playOnAwake = false;
        wordAudioSource.spatialBlend = 0f;
    }

    private void PlayWordClip()
    {
        if (wordClip == null || wordAudioSource == null) return;
        wordAudioSource.Stop();
        wordAudioSource.clip = wordClip;
        wordAudioSource.Play();
    }

    private void StopRecording()
    {
        if (!string.IsNullOrEmpty(recordingDevice))
        {
            try
            {
                if (Microphone.IsRecording(recordingDevice)) Microphone.End(recordingDevice);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PracticeWordAssembly] Could not stop microphone recording: {exception.Message}");
            }
        }

        recordingDevice = null;
        isRecording = false;
        if (resultMicButton != null) resultMicButton.transform.localScale = resultMicNaturalScale;
    }

    /// Two stones, no match (the other real word or garbage): freeze the board, red-flash via
    /// MisassemblyLock (no smoke — practice has no clock), then spring the stones home.
    private IEnumerator MisassemblyRoutine()
    {
        if (MisassemblyLock.Instance != null)
        {
            yield return MisassemblyLock.Instance.PlayRoutine();
        }

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            MagicStonePuzzleStone occupant = slotOccupants[i];
            if (occupant == null) continue;

            slotOccupants[i] = null;
            occupant.SetCurrentSlot(-1);
            occupant.ReturnHome();
        }

        misassemblyRoutine = null;
    }

    private int GetFirstEmptySlot()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null) return i;
        }
        return -1;
    }

    private static bool IsValidSlot(int index) => index >= 0 && index < 2;

    private bool TryGetSlotPosition(int index, out Vector2 slotPosition)
    {
        RectTransform slot = index == 0 ? inputSlot1 : inputSlot2;
        RectTransform dragParent = DragParent;
        if (slot != null && dragParent != null)
        {
            slotPosition = dragParent.InverseTransformPoint(slot.position);
            return true;
        }

        slotPosition = default;
        return false;
    }

    private void ResolveStones()
    {
        if (stone1 == null) stone1 = transform.Find("stone1") as RectTransform;
        if (stone2 == null) stone2 = transform.Find("stone2") as RectTransform;
        if (stone3 == null) stone3 = transform.Find("stone3") as RectTransform;

        stones.Clear();
        AddStone(stone1, "ก");
        AddStone(stone2, "ป");
        AddStone(stone3, "า");
    }

    private void AddStone(RectTransform stoneRect, string letter)
    {
        if (stoneRect == null) return;

        MagicStonePuzzleStone stone = stoneRect.GetComponent<MagicStonePuzzleStone>();
        if (stone == null)
        {
            stone = stoneRect.gameObject.AddComponent<MagicStonePuzzleStone>();
        }

        stone.Initialize(this);
        stone.SetLetter(letter);
        stones.Add(stone);

        Image image = stoneRect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private void ResolveSlots()
    {
        if (transform.parent == null) return;
        if (inputSlot1 == null) inputSlot1 = transform.parent.Find("inputSlot1") as RectTransform;
        if (inputSlot2 == null) inputSlot2 = transform.parent.Find("inputSlot2") as RectTransform;
        if (bookCraftRoot == null) bookCraftRoot = transform.parent.Find("book_craft") as RectTransform;
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

#if UNITY_EDITOR
    // ponytail: one runnable check for the branch that matters most (slot ORDER changes the built
    // word — า+ก must not equal ก+า). No test framework in this project; a menu item matches the
    // existing Tools/* convention (see e.g. Tools/Boss, Tools/Crow).
    [UnityEditor.MenuItem("Tools/Practice/Verify Word Classification")]
    private static void VerifyWordClassification()
    {
        var cases = new (string slot1, string slot2, string target, bool expectMatch)[]
        {
            ("ก", "า", "กา", true),
            ("า", "ก", "กา", false), // order matters: าก != กา
            ("ป", "า", "ปา", true),
            ("า", "ป", "ปา", false),
            ("ก", "ป", "กา", false), // garbage
            ("ป", "ก", "ปา", false), // garbage
        };

        int passed = 0;
        foreach (var c in cases)
        {
            bool actual = (c.slot1 + c.slot2) == c.target;
            bool ok = actual == c.expectMatch;
            Debug.Log(ok
                ? $"[PracticeWordAssembly] PASS: {c.slot1}+{c.slot2} vs target {c.target} -> {actual}"
                : $"[PracticeWordAssembly] FAIL: {c.slot1}+{c.slot2} vs target {c.target} -> expected {c.expectMatch}, got {actual}");
            if (ok) passed++;
        }
        Debug.Log($"[PracticeWordAssembly] {passed}/{cases.Length} passed");
    }
#endif
}
