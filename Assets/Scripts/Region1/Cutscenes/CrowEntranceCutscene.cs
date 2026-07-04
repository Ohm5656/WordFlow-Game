using System.Collections;
using UnityEngine;

// Crow intro cutscene for CutScene_ga (replaces the bear entrance). The crow prefab
// (UGUI Image + Animator with states ga_fly / ga_stone) fades in at wp_0, flies
// wp_0 -> wp_1 -> wp_center on the looping ga_fly clip, then — because ga_fly and
// ga_stone are consecutive frames of ONE rendered sequence — waits for the fly loop
// to reach its boundary and switches to ga_stone (plays once, holds the last frame)
// so the petrification is seamless. Hands off to OwlGreetingCutscene like BearCutscene did.
//
// Waypoints: children of `path` (scene object "waypoints"), looked up BY NAME
// (wp_0, wp_1, wp_center). Movement uses WORLD position (crow and waypoints live
// under different parents).
public sealed class CrowEntranceCutscene : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Crow Animator (states: ga_fly, ga_stone). Empty = Animator on this GameObject.")]
    [SerializeField] private Animator crow;
    [Tooltip("Parent of the named waypoints. Empty = a scene object named 'waypoints'.")]
    [SerializeField] private Transform path;
    [Tooltip("Owl greeting played after the petrification. Empty = first OwlGreetingCutscene found.")]
    [SerializeField] private OwlGreetingCutscene owlGreeting;

    [Header("Flight")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float fadeInDuration = 0.4f;
    [Tooltip("wp_0 -> wp_1 travel seconds.")]
    [SerializeField] private float leg1Duration = 3f;
    [Tooltip("wp_1 -> wp_center travel seconds.")]
    [SerializeField] private float leg2Duration = 2.4f;

    private CanvasGroup fade;
    private Coroutine routine;

    private void Awake()
    {
        if (crow == null) crow = GetComponent<Animator>();
        if (path == null)
        {
            GameObject go = GameObject.Find("waypoints");
            if (go != null) path = go.transform;
        }
        if (owlGreeting == null) owlGreeting = FindObjectOfType<OwlGreetingCutscene>(true);

        fade = GetComponent<CanvasGroup>();
        if (fade == null) fade = gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
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
        if (crow == null) { Debug.LogWarning("[CrowEntrance] no Animator assigned"); return; }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Returning after a wrong word: crow is already stone — skip the flight and the owl
        // intro, jump straight to the puzzle (PlayGreeting consumes the retry flag).
        if (MagicStonePuzzleController.IsRetryAfterCrowRequested)
        {
            transform.position = WaypointPos("wp_center", transform.position);
            crow.Play("ga_stone", 0, 1f); // last frame = stone crow
            fade.alpha = 1f;
            routine = null;
            if (owlGreeting != null) owlGreeting.PlayGreeting();
            yield break;
        }

        transform.position = WaypointPos("wp_0", transform.position);
        crow.speed = 1f;
        crow.Play("ga_fly");
        yield return FadeTo(1f, fadeInDuration);

        yield return MoveTo("wp_1", leg1Duration);
        yield return MoveTo("wp_center", leg2Duration);

        // Petrify seamlessly: ga_stone's first frame is the very next rendered frame after
        // ga_fly's last, so switch exactly at the loop boundary (hover in place until then).
        yield return null; // let the current state info settle after the move
        while (crow.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f < 0.98f)
            yield return null;
        crow.Play("ga_stone", 0, 0f);
        yield return null; // let the state register so length is readable

        // wait out the stone clip (it holds its last frame afterwards)
        float remaining = crow.GetCurrentAnimatorStateInfo(0).length;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        routine = null;
        if (owlGreeting != null) owlGreeting.PlayGreeting();
    }

    // Slide to the named waypoint over `seconds` (eased) while ga_fly keeps looping.
    private IEnumerator MoveTo(string waypoint, float seconds)
    {
        Vector3 from = transform.position;
        Vector3 to = WaypointPos(waypoint, from);
        float duration = Mathf.Max(0.01f, seconds);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, to, Smooth(Mathf.Clamp01(t / duration)));
            yield return null;
        }
        transform.position = to;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float from = fade.alpha;
        if (duration <= 0f) { fade.alpha = target; yield break; }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            fade.alpha = Mathf.Lerp(from, target, Smooth(t / duration));
            yield return null;
        }
        fade.alpha = target;
    }

    private Vector3 WaypointPos(string name, Vector3 fallback)
    {
        if (path == null) return fallback;
        Transform wp = path.Find(name);
        if (wp == null)
        {
            Debug.LogWarning($"[CrowEntrance] waypoint '{name}' not found under {path.name}");
            return fallback;
        }
        return wp.position;
    }

    private static float Smooth(float v) { v = Mathf.Clamp01(v); return v * v * (3f - 2f * v); }
}
