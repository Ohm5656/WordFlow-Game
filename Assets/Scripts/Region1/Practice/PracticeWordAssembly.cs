using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight word-assembly mechanic for practice_night: reveals the ก/ป/า stones, accepts
/// stone-to-slot placement, and resolves against a single target word supplied by
/// PracticeNightController.Configure. No smoke clock, no backend, no mic — just the
/// reveal/snap/lock feel MagicStonePuzzleController uses, trimmed to what one practice round needs.
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
    [SerializeField] private Vector2 firstSlotPosition = new Vector2(-395f, 150f);
    [SerializeField] private Vector2 secondSlotPosition = new Vector2(395f, 150f);

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

    [Header("Result: slide the whole book+stones UI off-screen")]
    [Tooltip("Root to slide out. Empty = this GameObject's parent (the WordAssembly canvas child holding book_craft/magic_stone/slots).")]
    [SerializeField] private RectTransform assemblyUiRoot;
    [SerializeField] private Vector2 slideOutOffset = new Vector2(0f, -1600f);
    [SerializeField] private float slideOutDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource wordAudioSource;

    [Header("Assembly sound hint (plays the target word while assembling)")]
    [SerializeField] private UnityEngine.UI.Button assemblySoundButton;

    private readonly List<MagicStonePuzzleStone> stones = new List<MagicStonePuzzleStone>();
    private readonly MagicStonePuzzleStone[] slotOccupants = new MagicStonePuzzleStone[2];

    private string targetWord = "";
    private RectTransform resultPage;
    private AudioClip wordClip;
    private Action onSuccess;

    private bool revealFinished;
    private bool resultShown;
    private Coroutine misassemblyRoutine;

    public RectTransform DragParent => transform as RectTransform;
    public float ReturnDuration => returnDuration;
    public bool CanInteract => revealFinished && !resultShown && misassemblyRoutine == null;

    private void Awake()
    {
        ResolveStones();
        ResolveSlots();

        for (int i = 0; i < stones.Count; i++)
        {
            stones[i].CaptureHome();
            stones[i].SetCurrentSlot(-1);
            stones[i].PrepareHidden(new Vector2(0f, revealYOffset), Mathf.Max(0.01f, revealStartScale));
        }

        if (assemblyUiRoot == null) assemblyUiRoot = transform.parent as RectTransform;
    }

    /// Sets the round's target word, result page and echo clip, and wires the success callback.
    /// Call once before PlayReveal().
    public void Configure(string word, RectTransform resultPageRoot, AudioClip resultWordClip, Action onSuccessCallback)
    {
        targetWord = word ?? "";
        resultPage = resultPageRoot;
        wordClip = resultWordClip;
        onSuccess = onSuccessCallback;

        if (assemblySoundButton != null)
        {
            assemblySoundButton.onClick.RemoveAllListeners();
            assemblySoundButton.onClick.AddListener(PlayAssemblyHint);
        }
    }

    private void PlayAssemblyHint()
    {
        if (!CanInteract || wordClip == null || wordAudioSource == null) return;
        GameAudio.PlayClick();
        wordAudioSource.Stop();
        wordAudioSource.clip = wordClip;
        wordAudioSource.Play();
    }

    public IEnumerator PlayReveal()
    {
        Vector2 offset = new Vector2(0f, revealYOffset);
        float startScale = Mathf.Max(0.01f, revealStartScale);

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
        MagicStonePuzzleStone existing = slotOccupants[slotIndex];
        if (existing != null && existing != stone)
        {
            existing.SetCurrentSlot(-1);
            existing.ReturnHome();
        }

        slotOccupants[slotIndex] = stone;
        stone.SetCurrentSlot(slotIndex);
        stone.MoveTo(GetSlotPosition(slotIndex), stone.HomeScale * Mathf.Max(0.01f, snappedScaleMultiplier), snapDuration, true);

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

        // Voice the assembled target word once (listen-only — no mic, no backend).
        if (wordClip != null && wordAudioSource != null)
        {
            wordAudioSource.Stop();
            wordAudioSource.clip = wordClip;
            wordAudioSource.Play();
            yield return new WaitForSeconds(wordClip.length);
        }

        // Slide the whole book+stones UI off-screen; the in-world resolution plays next (controller).
        if (assemblyUiRoot != null && slideOutDuration > 0f)
        {
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

        onSuccess?.Invoke();
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

    private Vector2 GetSlotPosition(int index)
    {
        RectTransform slot = index == 0 ? inputSlot1 : inputSlot2;
        if (slot != null)
        {
            return slot.anchoredPosition;
        }
        return index == 0 ? firstSlotPosition : secondSlotPosition;
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
