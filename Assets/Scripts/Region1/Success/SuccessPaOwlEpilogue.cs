using System.Collections;
using UnityEngine;
using WordFlow.Adventure.Net;

// Owl epilogue for Success_pa: after the bear fades out, the owl appears, speaks 3 phrases,
// then fades out before BearCutscene loads the next scene. Wire owlAnimator to the owl prefab
// (the same talking owl used in CutScene_bear), set owlPosition to (0, -89), assign clips/TTS.
public sealed class SuccessPaOwlEpilogue : MonoBehaviour
{
    [Tooltip("Animator on the owl talking prefab. Same prefab as CutScene_bear's talkingAnimator.")]
    [SerializeField] private Animator owlAnimator;
    [SerializeField] private string talkingStateName = "Owl";
    [Tooltip("Anchored position where the owl appears (local to its parent RectTransform).")]
    [SerializeField] private Vector2 owlPosition = new Vector2(0f, -89f);
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [Tooltip("After the owl finishes all phrases, freeze on its current frame for this long before fading out. 0 = no hold.")]
    [SerializeField, Min(0f)] private float holdFrozenAfterRound = 0.5f;
    [SerializeField] private float phraseGap = 0.05f;
    [Tooltip("Maximum time to wait for TTS before continuing with an offline fallback.")]
    [SerializeField, Min(0.1f)] private float ttsWaitTimeoutSeconds = 8f;
    [Tooltip("If TTS fails / no clip assigned, hold this long before fading out (so owl isn't invisible).")]
    [SerializeField] private float fallbackHoldSeconds = 6f;

    [Header("Phrase 1 — มันหนีเข้าป่าไปแล้ว")]
    [SerializeField] private AudioClip phrase1Clip;
    [SerializeField] private string phrase1LineId;
    [SerializeField, Min(0.01f)] private float phrase1AnimSpeed = 1f;
    [SerializeField, Min(0f)] private float phrase1SecondsPerLoop = 0f;

    [Header("Phrase 2 — เก่งมากเลยเด็กเด็ก")]
    [SerializeField] private AudioClip phrase2Clip;
    [SerializeField] private string phrase2LineId;
    [SerializeField, Min(0.01f)] private float phrase2AnimSpeed = 1f;
    [SerializeField, Min(0f)] private float phrase2SecondsPerLoop = 0f;

    [Header("Phrase 3 — สักวันมันอาจกลับมาช่วยเราก็ได้นะ")]
    [SerializeField] private AudioClip phrase3Clip;
    [SerializeField] private string phrase3LineId;
    [SerializeField, Min(0.01f)] private float phrase3AnimSpeed = 1f;
    [SerializeField, Min(0f)] private float phrase3SecondsPerLoop = 0f;

    [SerializeField] private OwlHelloSequence owlHello;
    [SerializeField] private TtsApiClient ttsClient;
    [SerializeField] private AudioSource voiceSource;
    [Tooltip("Wrong-word scene: play lose.wav instead of win.wav when the owl appears.")]
    [SerializeField] private bool playLoseSting = false;

    private RectTransform owlRect;
    private CanvasGroup owlCanvasGroup;
    private int pendingTtsLines;
    private bool ttsResolutionStarted;

    private void Awake()
    {
        if (owlAnimator == null) return;
        owlRect = owlAnimator.transform as RectTransform;
        owlCanvasGroup = owlAnimator.GetComponent<CanvasGroup>();
        if (owlCanvasGroup == null) owlCanvasGroup = owlAnimator.gameObject.AddComponent<CanvasGroup>();
        owlCanvasGroup.alpha = 0f;
        owlAnimator.gameObject.SetActive(false);
        ResolveTts();
    }

    private void ResolveTts()
    {
        if (ttsResolutionStarted) return;
        ttsResolutionStarted = true;
        if (ttsClient == null) ttsClient = FindObjectOfType<TtsApiClient>();
        if (ttsClient == null) { Debug.LogWarning("[OwlEpilogue] No TtsApiClient found"); return; }
        Debug.Log($"[OwlEpilogue] ResolveTts — ttsClient={ttsClient.gameObject.name}");
        if (!string.IsNullOrWhiteSpace(phrase1LineId))
            RequestTtsLine(phrase1LineId, c => {
                Debug.Log($"[OwlEpilogue] phrase1 '{phrase1LineId}' → {(c != null ? c.name + " len=" + c.length : "NULL")}");
                phrase1Clip = c;
            });
        if (!string.IsNullOrWhiteSpace(phrase2LineId))
            RequestTtsLine(phrase2LineId, c => phrase2Clip = c);
        if (!string.IsNullOrWhiteSpace(phrase3LineId))
            RequestTtsLine(phrase3LineId, c => phrase3Clip = c);
    }

    private void RequestTtsLine(string lineId, System.Action<AudioClip> assign)
    {
        pendingTtsLines++;
        ttsClient.GetLine(lineId, clip =>
        {
            if (clip != null) assign(clip);
            pendingTtsLines = Mathf.Max(0, pendingTtsLines - 1);
        });
    }

    private IEnumerator WaitForTts()
    {
        ResolveTts();
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, ttsWaitTimeoutSeconds);
        while (pendingTtsLines > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        if (pendingTtsLines > 0)
            Debug.LogWarning($"[OwlEpilogue] TTS prefetch timed out with {pendingTtsLines} line(s) pending");
    }

    public IEnumerator Play()
    {
        if (owlAnimator == null) yield break;

        // The old implementation started the owl immediately even when its async TTS request
        // had not completed, producing a silent animation that then disappeared.
        yield return WaitForTts();

        if (playLoseSting) GameAudio.PlayLose(); else GameAudio.PlayWin();

        if (owlRect != null) owlRect.anchoredPosition = owlPosition;

        if (owlHello != null)
        {
            // Match the wave's frame rate to the talking owl's so the seam owl_hello->owl is continuous.
            float talkFps = GetTalkingDisplayFps(phrase1AnimSpeed, phrase1SecondsPerLoop);
            if (talkFps > 0f) owlHello.SetPlaybackFps(talkFps);
            owlHello.SetHideOnComplete(false); // keep the last frame so we can crossfade it out
            yield return owlHello.Play();
        }

        AudioSource source = GetOrCreateAudioSource();
        owlAnimator.gameObject.SetActive(true);
        owlCanvasGroup.alpha = 0f;
        // Crossfade: owl_hello fades out while the talking owl fades in — smooth dissolve at the seam.
        if (owlHello != null) StartCoroutine(owlHello.FadeOut(fadeInDuration));
        yield return Fade(0f, 1f, fadeInDuration);

        AudioClip[] clips = { phrase1Clip, phrase2Clip, phrase3Clip };
        float[] speeds = { phrase1AnimSpeed, phrase2AnimSpeed, phrase3AnimSpeed };
        float[] spls = { phrase1SecondsPerLoop, phrase2SecondsPerLoop, phrase3SecondsPerLoop };

        Debug.Log($"[OwlEpilogue] Play — phrase1Clip={phrase1Clip}, phrase2Clip={phrase2Clip}, phrase3Clip={phrase3Clip}");
        GameAudio.SetVoiceDucking(true); // drop the music under the owl's voice
        bool playedAny = false;
        bool first = true;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (!first && phraseGap > 0f) yield return new WaitForSeconds(phraseGap);
            first = false;
            playedAny = true;

            owlAnimator.speed = ResolveSpeed(speeds[i], spls[i]);
            PlayAnim();
            source.clip = clips[i];
            source.Play();
            yield return new WaitForSeconds(clips[i].length);
        }

        if (!playedAny && fallbackHoldSeconds > 0f)
            yield return new WaitForSeconds(fallbackHoldSeconds);

        GameAudio.SetVoiceDucking(false); // owl done speaking — let the music back up
        owlAnimator.speed = 0f; // freeze on the last frame
        if (playedAny && holdFrozenAfterRound > 0f)
            yield return new WaitForSeconds(holdFrozenAfterRound);
        yield return Fade(1f, 0f, fadeOutDuration);
        owlAnimator.gameObject.SetActive(false);
    }

    // Effective sprite-swap rate (frames/sec) the talking owl runs at, so owl_hello can match it
    // and the seam has no cadence jump. 0 = unknown (caller keeps owl_hello's own fps).
    private float GetTalkingDisplayFps(float speed, float spl)
    {
        var ctrl = owlAnimator != null ? owlAnimator.runtimeAnimatorController : null;
        if (ctrl == null) return 0f;

        AnimationClip clip = null;
        foreach (var c in ctrl.animationClips)
        {
            if (c == null) continue;
            if (clip == null) clip = c;
            if (string.Equals(c.name, talkingStateName, System.StringComparison.OrdinalIgnoreCase)) { clip = c; break; }
        }

        if (clip == null || clip.length <= 0f || clip.frameRate <= 0f) return 0f;
        float animSpeed = spl > 0f ? clip.length / Mathf.Max(0.01f, spl) : Mathf.Max(0.01f, speed);
        return clip.frameRate * animSpeed;
    }

    private float ResolveSpeed(float speed, float spl)
    {
        if (spl <= 0f) return Mathf.Max(0.01f, speed);
        var ctrl = owlAnimator.runtimeAnimatorController;
        if (ctrl == null) return Mathf.Max(0.01f, speed);
        foreach (var clip in ctrl.animationClips)
        {
            if (clip == null || clip.length <= 0f) continue;
            if (string.Equals(clip.name, talkingStateName, System.StringComparison.OrdinalIgnoreCase))
                return clip.length / Mathf.Max(0.01f, spl);
        }
        var first = ctrl.animationClips.Length > 0 ? ctrl.animationClips[0] : null;
        return first != null && first.length > 0f
            ? first.length / Mathf.Max(0.01f, spl)
            : Mathf.Max(0.01f, speed);
    }

    private void PlayAnim()
    {
        int hash = Animator.StringToHash(talkingStateName);
        if (owlAnimator.HasState(0, hash))
            owlAnimator.Play(hash, 0, 0f);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { owlCanvasGroup.alpha = to; yield break; }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            owlCanvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        owlCanvasGroup.alpha = to;
    }

    private AudioSource GetOrCreateAudioSource()
    {
        if (voiceSource != null) return voiceSource;
        voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        return voiceSource;
    }
}
