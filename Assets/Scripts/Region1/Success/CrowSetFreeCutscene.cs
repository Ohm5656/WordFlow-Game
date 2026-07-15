using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Success_ga_correct celebration: the petrified crow is set free. ONE Crow Image plays two
// one-shot sprite clips back to back:
//   1. ga_set_free  — the stone shatters. It starts at scene size (expandStartScale=1); from
//      expandStartFrame the debris blooms OUTWARD, growing to expandEndScale so the frame
//      overflows the screen — the (rectangular) sprite edges leave the view, so the spread
//      reads as a full-screen shatter with no visible frame border. Then fades out.
//   2. ga_set_free2 — the freed crow at scene size; fades in, plays once, fades out.
// Then the owl epilogue praises and reference_forest loads. Dedicated (not the generic
// BearCutscene stepper) because of the per-frame expand + the fade at each clip's end.
public sealed class CrowSetFreeCutscene : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Crow Animator (states: ga_set_free, ga_set_free2). Empty = Animator on this GameObject.")]
    [SerializeField] private Animator crow;
    [Tooltip("Owl praise played after the crow flies off. Empty = first SuccessPaOwlEpilogue found.")]
    [SerializeField] private SuccessPaOwlEpilogue owlEpilogue;

    [Header("Flow")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float fadeInDuration = 0.3f;
    [Tooltip("A short dissolve of the big spread shatter keeps the light dust readable without delaying the next beat.")]
    [SerializeField] private float fadeOutDuration = 0.45f;
    [Tooltip("ga_set_free2 (the freed crow) eases back in over this — the clips are continuous, so a soft fade reads as one shot.")]
    [SerializeField] private float clip2FadeInDuration = 0.35f;
    [SerializeField] private string setFreeState = "ga_set_free";
    [SerializeField] private string setFree2State = "ga_set_free2";

    [Header("Shatter expand (ga_set_free only)")]
    [Tooltip("Frame index within ga_set_free where the outward grow begins (0349 - 0312 = 37).")]
    [SerializeField] private int expandStartFrame = 37;
    [Tooltip("Clip frame rate the expand timing assumes — must match the built clip (30).")]
    [SerializeField] private float clipFps = 30f;
    [Tooltip("Scale (x the placed scene scale) while the stone is still whole / just cracking — 1 = scene size.")]
    [SerializeField, Min(0.01f)] private float expandStartScale = 1f;
    [Tooltip("Scale at the last frame. >1 overflows the screen so the sprite's rectangular edges leave the view (3.5 = well past the 3840x2160 canvas for a 1618x1080 frame).")]
    [SerializeField, Min(0.01f)] private float expandEndScale = 3.5f;

    [Header("Ending")]
    [SerializeField] private string nextScene = "reference_forest";
    [SerializeField] private bool resumeForestAtBeat2 = true;
    [Tooltip("This IS the crow (ga) quest: on success the forest skips the crow quest and resumes at Beat 3 (foxes). Set true here, and set resumeForestAtBeat2 false.")]
    [SerializeField] private bool resumeForestAtBeat3 = false;

    private CanvasGroup fade;
    private Vector3 baseScale;
    private Coroutine routine;

    private void Awake()
    {
        if (crow == null) crow = GetComponent<Animator>();
        if (owlEpilogue == null) owlEpilogue = FindObjectOfType<SuccessPaOwlEpilogue>(true);
        baseScale = transform.localScale;
        fade = GetComponent<CanvasGroup>();
        if (fade == null) fade = gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
    }

    private void OnEnable() { if (playOnStart) Play(); }
    private void OnDisable()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        GameAudio.StopSfxLoop();
    }

    public void Play()
    {
        if (crow == null) { Debug.LogWarning("[CrowSetFree] no Animator assigned"); return; }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // --- ga_set_free: shatter small, bloom out to scene size, fade out on the last frame ---
        transform.localScale = baseScale * expandStartScale;
        crow.speed = 1f;
        crow.Play(setFreeState, 0, 0f);
        GameAudio.PlayRockBreak();
        yield return null; // let the state register so its length is readable

        float len = crow.GetCurrentAnimatorStateInfo(0).length;
        float expandStart = expandStartFrame / Mathf.Max(1f, clipFps);
        float elapsed = 0f, guard = 0f;
        while (true)
        {
            float nt = crow.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (nt >= 1f || guard > len + 1f) break;

            fade.alpha = fadeInDuration <= 0f ? 1f : Mathf.Min(1f, Smooth(elapsed / fadeInDuration));
            float k = Mathf.InverseLerp(expandStart, len, nt * len); // 0 before expandStart -> 1 at the end
            // EaseOut: bursts outward at full speed then decelerates into the last frame.
            transform.localScale = baseScale * Mathf.Lerp(expandStartScale, expandEndScale, EaseOut(k));

            elapsed += Time.deltaTime;
            guard += Time.deltaTime;
            yield return null;
        }
        fade.alpha = 1f;
        transform.localScale = baseScale * expandEndScale; // filled the screen, edges off-view
        yield return Fade(1f, 0f, fadeOutDuration);

        // --- ga_set_free2: freed crow at scene size, fade in / play once / fade out ---
        transform.localScale = baseScale; // back to scene size for the flying crow
        crow.speed = 1f;
        crow.Play(setFree2State, 0, 0f);
        GameAudio.PlayCrowLoop();
        yield return null;
        float len2 = crow.GetCurrentAnimatorStateInfo(0).length;
        yield return Fade(0f, 1f, clip2FadeInDuration);
        float remain = len2 - clip2FadeInDuration;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        GameAudio.StopSfxLoop();
        yield return Fade(1f, 0f, fadeOutDuration);

        routine = null;

        // --- owl praise (day only), then on to reference_forest ---
        // Night redo: no praise — the child already earned it on the daytime run.
        if (owlEpilogue != null && !NightMode.RedoActive) yield return owlEpilogue.Play();
        if (resumeForestAtBeat2) BearEncounterFlow.ResumeAtBeat2 = true;
        if (resumeForestAtBeat3) BearEncounterFlow.ResumeAtBeat3 = true;
        if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene.Trim());
    }

    private IEnumerator Fade(float from, float to, float d)
    {
        if (d <= 0f) { fade.alpha = to; yield break; }
        for (float t = 0f; t < d; t += Time.deltaTime)
        { fade.alpha = Mathf.Lerp(from, to, Smooth(t / d)); yield return null; }
        fade.alpha = to;
    }

    private static float Smooth(float v) { v = Mathf.Clamp01(v); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Mathf.Clamp01(v); return 1f - (1f - v) * (1f - v); } // fast start, slow end
}
