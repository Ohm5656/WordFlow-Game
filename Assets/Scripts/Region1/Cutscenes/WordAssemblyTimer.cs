using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Countdown clock for the word-assembly (magic stone) puzzle. Pops the time_root board in when the
/// craft book appears, counts 30s down while the child assembles, and pauses the moment a word is
/// built and routed to the craft page / sound / mic. If a wrong word sends the player through a
/// Success scene and back, the remaining time is persisted (PlayerPrefs, like the retry flags) and
/// resumes where it left off. At 10s left it plays a per-second countdown beep for pressure.
///
/// Driven statically (WordAssemblyTimer.Instance?.X()) so the puzzle + owl cutscene need no wiring:
///  - OwlGreetingCutscene.GreetingRoutine  -> BeginFresh() when the book pops (fresh 30s)
///  - OwlGreetingCutscene.RetryMagicStonePuzzleRoutine -> Resume() (continue after a wrong word)
///  - MagicStonePuzzleController.WordResultRoutine -> Pause() (word built -> craft/sound/mic)
/// </summary>
public sealed class WordAssemblyTimer : MonoBehaviour
{
    private const string RemainingKey = "WordAssemblyTimerRemaining";

    [Header("Digits (index 0..3 = MM:SS, left -> right)")]
    [SerializeField] private Image[] digits = new Image[4];
    [Tooltip("Sprite per glyph, index 0..9 = number_0..number_9 (sliced from number.png).")]
    [SerializeField] private Sprite[] numberSprites = new Sprite[10];

    [Header("Countdown")]
    [SerializeField] private float totalSeconds = 30f;
    [SerializeField] private float countdownAt = 10f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countdownClip;

    [Header("Board pop-in")]
    [SerializeField] private RectTransform popRoot; // defaults to this transform
    [SerializeField] private float popDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float popStartScale = 0.2f;
    [SerializeField] private float popOvershoot = 1.15f;

    public static WordAssemblyTimer Instance { get; private set; }

    /// <summary>True once the puzzle clock has begun (fresh or resumed). The smoke fog reads this
    /// so it only creeps in during the build, never during the preceding bear cutscene.</summary>
    public bool SmokeActive => started;

    /// <summary>0 at the start of the countdown, 1 when time is up — drives the edge fog's reach so
    /// the smoke ceiling lands exactly when the clock runs out. Frozen while paused (word built).</summary>
    public float SmokeProgress01 =>
        started && totalSeconds > 0f ? Mathf.Clamp01(1f - remaining / totalSeconds) : 0f;

    private bool started;
    private CanvasGroup group;
    private Vector3 baseScale = Vector3.one;
    private Coroutine tickRoutine;
    private Coroutine popRoutine;
    private float remaining;
    private bool beepPlaying;
    private bool running;

    private void Awake()
    {
        Instance = this;
        if (popRoot == null) popRoot = transform as RectTransform;
        baseScale = popRoot != null ? popRoot.localScale : Vector3.one;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        HideBoard(); // stays hidden until BeginFresh/Resume

        // Let the smoke fog (FogController, in Game.Common) read our progress without a hard
        // cross-assembly reference back to this Region1 type.
        FogController.SmokeActiveProvider = () => Instance != null && Instance.SmokeActive;
        FogController.SmokeProgressProvider = () => Instance != null ? Instance.SmokeProgress01 : 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            FogController.SmokeActiveProvider = null;
            FogController.SmokeProgressProvider = null;
        }
    }

    /// <summary>New attempt: reset to the full timer and start counting with a pop-in.</summary>
    public void BeginFresh()
    {
        started = true;
        remaining = Mathf.Max(0f, totalSeconds);
        Save();
        StartCounting();
    }

    /// <summary>Returning to fix a wrong word: continue from the persisted remaining time.</summary>
    public void Resume()
    {
        started = true;
        remaining = PlayerPrefs.GetFloat(RemainingKey, totalSeconds);
        if (remaining <= 0f)
        {
            ShowBoard();
            UpdateDigits();
            return;
        }
        StartCounting();
    }

    /// <summary>Word built -> craft/sound/mic: stop counting, persist, silence the beep, hide board.</summary>
    public void Pause()
    {
        running = false;
        if (tickRoutine != null) { StopCoroutine(tickRoutine); tickRoutine = null; }
        StopBeep();
        Save();
        HideBoard();
    }

    private void StartCounting()
    {
        running = true;
        ShowBoard();
        UpdateDigits();

        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());

        if (tickRoutine != null) StopCoroutine(tickRoutine);
        tickRoutine = StartCoroutine(Tick());
    }

    private IEnumerator Tick()
    {
        while (running && remaining > 0f)
        {
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            UpdateDigits();
            HandleBeep();
            yield return null;
        }

        if (remaining <= 0f)
        {
            remaining = 0f;
            UpdateDigits();
            Save();
            running = false;
            tickRoutine = null;
            // ponytail: time's up = hold 00:00. Fail handling (force submit / auto-retry) is TBD;
            // hook it here when the game decides what running out should do.
        }
    }

    private void HandleBeep()
    {
        if (beepPlaying || remaining > countdownAt) return;
        if (countdownClip == null || audioSource == null) return;

        audioSource.clip = countdownClip;
        audioSource.loop = false;
        // Resume the countdown audio at the right point (e.g. paused with 6s left -> start 4s in).
        audioSource.time = Mathf.Clamp(countdownAt - remaining, 0f, Mathf.Max(0f, countdownClip.length - 0.05f));
        audioSource.Play();
        beepPlaying = true;
    }

    private void StopBeep()
    {
        if (audioSource != null && audioSource.clip == countdownClip && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        beepPlaying = false;
    }

    private void UpdateDigits()
    {
        int secs = Mathf.Clamp(Mathf.CeilToInt(remaining), 0, 99 * 60 + 59);
        int mm = secs / 60;
        int ss = secs % 60;
        SetDigit(0, mm / 10);
        SetDigit(1, mm % 10);
        SetDigit(2, ss / 10);
        SetDigit(3, ss % 10);
    }

    private void SetDigit(int index, int value)
    {
        if (digits == null || index < 0 || index >= digits.Length || digits[index] == null) return;
        if (numberSprites == null || value < 0 || value >= numberSprites.Length || numberSprites[value] == null) return;
        digits[index].sprite = numberSprites[value];
    }

    private void ShowBoard()
    {
        if (group != null) group.alpha = 1f;
    }

    private void HideBoard()
    {
        if (group != null) group.alpha = 0f;
        if (popRoot != null) popRoot.localScale = baseScale;
    }

    private IEnumerator PopIn()
    {
        float d = Mathf.Max(0.01f, popDuration);
        Vector3 from = baseScale * Mathf.Max(0.01f, popStartScale);
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            float k = EaseOutBack(Mathf.Clamp01(t / d));
            if (popRoot != null) popRoot.localScale = Vector3.LerpUnclamped(from, baseScale, k);
            yield return null;
        }
        if (popRoot != null) popRoot.localScale = baseScale;
        popRoutine = null;
    }

    private float EaseOutBack(float v)
    {
        v = Mathf.Clamp01(v);
        float s = v - 1f;
        float o = popOvershoot;
        return 1f + s * s * ((o + 1f) * s + o);
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(RemainingKey, remaining);
        PlayerPrefs.Save();
    }
}
