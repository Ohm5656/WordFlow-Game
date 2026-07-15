using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Bear intro cutscene for cut_scene1. The bear (new Bear.prefab: UGUI Image + Animator)
// fades in, walks a waypoint path playing a chosen animation per leg (a leg can ramp its
// speed up/down so the run can decelerate into the jump for a smoother change), then hands
// off to the existing OwlGreetingCutscene (which reveals the book/stone puzzle).
//
// Waypoints: children (in order) of `path` (Canvas/waypoints). Movement uses WORLD position
// so it works even though the bear and the waypoints sit under different RectTransform parents.
// State names must match BearController.controller: bear_breathing, bear_jump, bear_run2,
// bear_run_back, Thow.
public sealed class BearCutscene : MonoBehaviour
{
    public enum Timing
    {
        LoopUntilArrive, // loop the clip while sliding to the target waypoint over `seconds`
        PlayClip,        // play the clip once; duration = clip length (slides to target meanwhile)
        Hold,            // stay in place and play for `seconds`
    }

    [System.Serializable]
    public struct Step
    {
        public string state;     // Animator state to play
        public int toWaypoint;   // index in `path` to move to during this step (-1 = stay put)
        public Timing timing;
        public float seconds;    // LoopUntilArrive: travel time. Hold: dwell time. PlayClip: ignored (uses clip length)
        public float speed;      // playback speed at the START of the step (1 = normal; <=0 -> 1)
        public float speedEnd;   // playback speed at the END of the step (0 = constant = same as speed). Movement eases to match.
        public float scale;      // size multiplier vs the placed base scale (1 = base; <=0 -> 1)
        public float fadeSeconds;// >0 = cross-dissolve into this step (fade out -> swap -> fade in) for a smooth change
        public float moveDelay;  // seconds into the step to hold in place before movement starts (e.g. turn-around plays first); 0 = move immediately
    }

    [Header("References")]
    [Tooltip("Bear Animator. Empty = Animator on this GameObject.")]
    [SerializeField] private Animator bear;
    [Tooltip("Parent of the ordered waypoints. Empty = a scene object named 'waypoints'.")]
    [SerializeField] private Transform path;
    [Tooltip("Played after the bear finishes (book/stone reveal). Empty = first OwlGreetingCutscene found.")]
    [SerializeField] private OwlGreetingCutscene owlGreeting;

    [Header("Flow")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("Success_pa only: play paa.wav once when the separate Thow animation starts with this cutscene.")]
    [SerializeField] private bool playPaaThrowSfxOnStart = false;
    [SerializeField] private int startWaypoint = 0;       // teleport here before the sequence
    [SerializeField] private float fadeInDuration = 0.6f; // bear appearance fade

    [Header("Optional visual crop")]
    [Tooltip("Crop this many local UI pixels from only the left edge. Used to hide a thin render border on the Eye result.")]
    [SerializeField, Min(0f)] private float cropLeftPixels = 0f;

    [Header("Ending (optional — defaults keep the owl hand-off)")]
    [Tooltip("Fade these out together with the bear at the end (e.g. the throw hands).")]
    [SerializeField] private CanvasGroup[] alsoFade;
    [Tooltip(">0 = fade the bear (and alsoFade) out after the sequence.")]
    [SerializeField] private float endFadeOutDuration = 0f;
    [Tooltip("Non-empty = load this scene after the ending fade (instead of the owl hand-off).")]
    [SerializeField] private string nextScene = "";
    [Tooltip("Set the forest's ResumeAtBeat2 flag before loading nextScene so reference_forest skips the bear intro (Beat 1) and resumes at the crow quest (Beat 2).")]
    [SerializeField] private bool resumeForestAtBeat2 = false;
    [Tooltip("Optional epilogue (e.g. owl speech in Success_pa) that plays after the bear fade-out but before the next scene loads.")]
    [SerializeField] private SuccessPaOwlEpilogue owlEpilogue;
    [Tooltip("Set the puzzle retry flags before loading nextScene (wrong-word scenes: return to the assembly in retry mode, stones shown all at once).")]
    [SerializeField] private bool setPuzzleRetryFlags = false;

    [SerializeField] private Step[] sequence =
    {
        // wp_0 -> wp_1: run at full speed (constant)
        new Step { state = "bear_run2", toWaypoint = 1, timing = Timing.LoopUntilArrive, seconds = 1.4f, speed = 1.3f, speedEnd = 0f,    scale = 1f, fadeSeconds = 0f },
        // wp_1 -> wp_2: keep running but decelerate, so the jump start feels connected
        new Step { state = "bear_run2", toWaypoint = 2, timing = Timing.LoopUntilArrive, seconds = 1.2f, speed = 1.3f, speedEnd = 0.4f, scale = 1f, fadeSeconds = 0f },
        // wp_2 -> wp_3: jump once (clip length), sliding to the landing point (no dissolve, hard cut from run)
        new Step { state = "bear_jump", toWaypoint = 3, timing = Timing.PlayClip,        seconds = 0f,   speed = 1f,   speedEnd = 0f,    scale = 1f, fadeSeconds = 0f },
        // land -> breathe in place, dissolve in for smoothness
        new Step { state = "bear_breathing", toWaypoint = -1, timing = Timing.Hold,      seconds = 2f,   speed = 1f,   speedEnd = 0f,    scale = 1f, fadeSeconds = 0.35f },
    };

    private CanvasGroup fade;
    private Vector3 baseScale;
    private Coroutine routine;

    private void Awake()
    {
        if (bear == null) bear = GetComponent<Animator>();
        if (path == null)
        {
            GameObject go = GameObject.Find("waypoints");
            if (go != null) path = go.transform;
        }
        if (owlGreeting == null) owlGreeting = FindObjectOfType<OwlGreetingCutscene>(true);

        ApplyLeftEdgeCrop();

        baseScale = transform.localScale;

        fade = GetComponent<CanvasGroup>();
        if (fade == null) fade = gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f; // hidden until the fade-in
    }

    private void OnEnable()
    {
        if (playOnStart) Play();
    }

    private void OnDisable()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }

    public void Play()
    {
        if (bear == null) { Debug.LogWarning("[BearCutscene] no Animator assigned"); return; }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Returning from the crow scene = skip the bear entrance and jump straight to the
        // puzzle (PlayGreeting consumes the retry flag and reveals it without the owl intro).
        if (MagicStonePuzzleController.IsRetryAfterCrowRequested)
        {
            fade.alpha = 0f;
            routine = null;
            if (owlGreeting != null) owlGreeting.PlayGreeting();
            yield break;
        }

        if (playPaaThrowSfxOnStart)
        {
            GameAudio.PlayPaaThrow();
        }

        transform.position = WaypointPos(startWaypoint, transform.position);
        fade.alpha = 0f;

        // kick off the first state at its size and fade the bear in as it enters
        if (sequence.Length > 0)
        {
            bear.speed = sequence[0].speed > 0f ? sequence[0].speed : 1f;
            if (!string.IsNullOrEmpty(sequence[0].state)) { bear.Play(sequence[0].state); GameAudio.OnCutsceneState(sequence[0].state); }
            ApplyScale(sequence[0].scale);
        }
        yield return Fade(0f, 1f, fadeInDuration);

        foreach (Step step in sequence)
        {
            float startSpeed = step.speed > 0f ? step.speed : 1f;
            float endSpeed = step.speedEnd > 0f ? step.speedEnd : startSpeed;
            bool ramp = !Mathf.Approximately(startSpeed, endSpeed);
            bear.speed = startSpeed;

            // Smooth change between two sprite clips: dissolve through transparency.
            if (step.fadeSeconds > 0f)
            {
                yield return Fade(fade.alpha, 0f, step.fadeSeconds * 0.5f);
                if (!string.IsNullOrEmpty(step.state)) { bear.Play(step.state); GameAudio.OnCutsceneState(step.state); }
                ApplyScale(step.scale);
                yield return Fade(0f, 1f, step.fadeSeconds * 0.5f);
            }
            else
            {
                if (!string.IsNullOrEmpty(step.state)) { bear.Play(step.state); GameAudio.OnCutsceneState(step.state); }
                ApplyScale(step.scale);
            }

            // GetCurrentAnimatorStateInfo only reflects the new state after one evaluation.
            if (step.timing == Timing.PlayClip) yield return null;

            Vector3 from = transform.position;
            bool moving = step.toWaypoint >= 0;
            Vector3 to = moving ? WaypointPos(step.toWaypoint, from) : from;

            float duration = step.timing == Timing.PlayClip ? ClipLength(startSpeed) : Mathf.Max(0.01f, step.seconds);

            if (moving)
            {
                // Optional hold-in-place at the start of the step (e.g. the bear turns around
                // before it actually runs). Movement is squeezed into the time after moveDelay.
                float moveStart = Mathf.Clamp(step.moveDelay, 0f, duration);
                float moveDur = Mathf.Max(0.0001f, duration - moveStart);
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    if (ramp) bear.speed = Mathf.Lerp(startSpeed, endSpeed, t / duration);
                    float mp = Mathf.Clamp01((t - moveStart) / moveDur);
                    // jump: linear horizontal (clip owns the arc). run: ease to match speed ramp.
                    float e;
                    if (step.timing == Timing.PlayClip) e = mp;
                    else if (ramp) e = endSpeed < startSpeed ? EaseOut(mp) : EaseIn(mp);
                    else e = Smooth(mp);
                    transform.position = Vector3.Lerp(from, to, e);
                    yield return null;
                }
                transform.position = to;
                if (ramp) bear.speed = endSpeed;
            }
            else if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
        }

        GameAudio.StopSfxLoop(); // never let footsteps bleed into the owl greeting
        routine = null;

        // Ending: fade the bear (and any alsoFade groups, e.g. the throw hands) out and load the
        // next scene. Defaults (empty nextScene, 0 fade) keep the original owl hand-off untouched.
        if (!string.IsNullOrEmpty(nextScene) || endFadeOutDuration > 0f)
        {
            yield return FadeOutAll(endFadeOutDuration);
            // Night redo: no owl praise — the child already earned it on the daytime run. Straight
            // back to the forest for the next quest.
            if (owlEpilogue != null) yield return owlEpilogue.Play();
            // Tell reference_forest to skip Beat 1 (bear) and resume at Beat 2 (crow quest).
            if (resumeForestAtBeat2) BearEncounterFlow.ResumeAtBeat2 = true;
            if (setPuzzleRetryFlags)
            {
                MagicStonePuzzleController.RequestRetryAfterCrow(); // skip the intro flight
                MagicStonePuzzleController.RequestRetryAfterAlt();  // show stones all at once
            }
            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene.Trim());
            yield break;
        }

        // hand off to the existing flow: owl greeting -> book/stone puzzle reveal
        if (owlGreeting != null) owlGreeting.PlayGreeting();
    }

    private void ApplyLeftEdgeCrop()
    {
        RectTransform target = transform as RectTransform;
        if (cropLeftPixels <= 0f || target == null || target.parent == null) return;

        Transform originalParent = target.parent;
        int originalSibling = target.GetSiblingIndex();

        GameObject maskObject = new GameObject(name + "_CropMask", typeof(RectTransform), typeof(RectMask2D));
        maskObject.layer = gameObject.layer;
        RectTransform maskRect = maskObject.GetComponent<RectTransform>();
        maskRect.SetParent(originalParent, false);
        maskRect.SetSiblingIndex(originalSibling);
        maskRect.anchorMin = target.anchorMin;
        maskRect.anchorMax = target.anchorMax;
        maskRect.pivot = target.pivot;
        maskRect.anchoredPosition3D = target.anchoredPosition3D + new Vector3(cropLeftPixels * 0.5f, 0f, 0f);
        maskRect.sizeDelta = new Vector2(Mathf.Max(1f, target.sizeDelta.x - cropLeftPixels), target.sizeDelta.y);
        maskRect.localRotation = target.localRotation;
        maskRect.localScale = target.localScale;

        target.SetParent(maskRect, true);
    }

    // Fade the bear and every alsoFade group from their current alpha to 0 in lockstep.
    private IEnumerator FadeOutAll(float duration)
    {
        float start = fade.alpha;
        if (duration <= 0f)
        {
            SetAllAlpha(0f);
            yield break;
        }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAllAlpha(Mathf.Lerp(start, 0f, Smooth(t / duration)));
            yield return null;
        }
        SetAllAlpha(0f);
    }

    private void SetAllAlpha(float a)
    {
        fade.alpha = a;
        if (alsoFade != null)
            foreach (var g in alsoFade) if (g != null) g.alpha = a;
    }

    private void ApplyScale(float mult)
    {
        float m = mult > 0f ? mult : 1f;
        transform.localScale = baseScale * m;
    }

    private Vector3 WaypointPos(int index, Vector3 fallback)
    {
        if (path != null && index >= 0 && index < path.childCount)
            return path.GetChild(index).position;
        return fallback;
    }

    // Real-time length of the state currently playing, scaled by playback speed.
    private float ClipLength(float speed)
    {
        float len = bear.GetCurrentAnimatorStateInfo(0).length;
        float s = speed > 0f ? speed : 1f;
        return Mathf.Max(0.05f, len / s);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { fade.alpha = to; yield break; }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, Smooth(t / duration));
            yield return null;
        }
        fade.alpha = to;
    }

    private static float Smooth(float v) { v = Mathf.Clamp01(v); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Mathf.Clamp01(v); return 1f - (1f - v) * (1f - v); } // fast start, slow end (decel)
    private static float EaseIn(float v) { v = Mathf.Clamp01(v); return v * v; }                      // slow start, fast end (accel)
}
