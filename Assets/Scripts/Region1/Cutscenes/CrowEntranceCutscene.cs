using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Crow intro cutscene for CutScene_ga (replaces the bear entrance). The crow prefab
// (UGUI Image + Animator with states ga_left / ga_right / ga_stone) flies a waypoint path:
//   ga_left  wp_0 -> wp_4, ga_right wp_4 -> wp_1, ga_left wp_1 -> wp_center (flapping
// faster as it closes in), freezes on `freezeSprite` (ga_left 0038) for `freezeHold`,
// then a white flash + shake "petrification" swaps it to the ga_stone clip (plays once,
// holds the last frame). Hands off to OwlGreetingCutscene like BearCutscene did.
//
// Waypoints: children of `path` (scene object "waypoints"), looked up BY NAME (wp_0..wp_4,
// wp_center). Movement uses WORLD position (crow and waypoints live under different parents).
public sealed class CrowEntranceCutscene : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Crow Animator (states: ga_left, ga_right, ga_stone). Empty = Animator on this GameObject.")]
    [SerializeField] private Animator crow;
    [Tooltip("Crow Image the sprite freeze writes to. Empty = Image on this GameObject.")]
    [SerializeField] private Image crowImage;
    [Tooltip("Parent of the named waypoints. Empty = a scene object named 'waypoints'.")]
    [SerializeField] private Transform path;
    [Tooltip("Owl greeting played after the petrification. Empty = first OwlGreetingCutscene found.")]
    [SerializeField] private OwlGreetingCutscene owlGreeting;
    [Tooltip("Sprite held at wp_center before transforming (ga_left_cropped/0038).")]
    [SerializeField] private Sprite freezeSprite;

    [Header("Flight")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float fadeInDuration = 0.4f;
    [Tooltip("wp_0 -> wp_4 (ga_left) travel seconds.")]
    [SerializeField] private float leg1Duration = 3f;
    [Tooltip("wp_4 -> wp_1 (ga_right) travel seconds.")]
    [SerializeField] private float leg2Duration = 3f;
    [Tooltip("wp_1 -> wp_center (ga_left) travel seconds.")]
    [SerializeField] private float leg3Duration = 2.4f;
    [Tooltip("Animator speed at the END of the wp_center approach (starts at 1 = faster flapping as it closes in).")]
    [SerializeField] private float approachSpeedEnd = 2.2f;

    [Header("Petrification")]
    [Tooltip("Seconds the crow holds freezeSprite at wp_center before transforming.")]
    [SerializeField] private float freezeHold = 0.5f;
    [SerializeField] private float shakeDuration = 0.35f;
    [SerializeField] private float shakeStrength = 14f;
    [SerializeField] private float flashInDuration = 0.15f;
    [SerializeField] private float flashOutDuration = 0.4f;
    [SerializeField, Range(0f, 1f)] private float flashMaxAlpha = 0.85f;

    private CanvasGroup fade;
    private Coroutine routine;

    private void Awake()
    {
        if (crow == null) crow = GetComponent<Animator>();
        if (crowImage == null) crowImage = GetComponent<Image>();
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
        crow.Play("ga_left");
        yield return FadeTo(1f, fadeInDuration);

        // wp_0 -> wp_4 flying left-anim, then wp_4 -> wp_1 flying right-anim
        yield return MoveTo("wp_4", leg1Duration, 1f, 1f);
        crow.Play("ga_right");
        yield return MoveTo("wp_1", leg2Duration, 1f, 1f);

        // final approach: flap faster and faster into wp_center
        crow.Play("ga_left");
        yield return MoveTo("wp_center", leg3Duration, 1f, approachSpeedEnd);

        // freeze on the chosen wing pose
        crow.enabled = false;
        if (crowImage != null && freezeSprite != null) crowImage.sprite = freezeSprite;
        yield return new WaitForSeconds(freezeHold);

        // petrify: shake + white flash, swap to the stone clip at the flash peak
        yield return Shake(shakeDuration);
        Image flash = CreateFlashImage();
        yield return FadeImage(flash, 0f, flashMaxAlpha, flashInDuration);
        crow.enabled = true;
        crow.speed = 1f;
        crow.Play("ga_stone", 0, 0f);
        yield return null; // let the state register so length is readable
        yield return FadeImage(flash, flashMaxAlpha, 0f, flashOutDuration);
        if (flash != null) Destroy(flash.transform.root.gameObject);

        // wait out the rest of the stone clip (it holds its last frame afterwards)
        float remaining = crow.GetCurrentAnimatorStateInfo(0).length - flashOutDuration;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        routine = null;
        if (owlGreeting != null) owlGreeting.PlayGreeting();
    }

    // Slide to the named waypoint over `seconds` while ramping animator speed from
    // `speedStart` to `speedEnd` (movement eases; flapping ramp is linear).
    private IEnumerator MoveTo(string waypoint, float seconds, float speedStart, float speedEnd)
    {
        Vector3 from = transform.position;
        Vector3 to = WaypointPos(waypoint, from);
        float duration = Mathf.Max(0.01f, seconds);
        bool ramp = !Mathf.Approximately(speedStart, speedEnd);
        crow.speed = speedStart;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            if (ramp) crow.speed = Mathf.Lerp(speedStart, speedEnd, p);
            transform.position = Vector3.Lerp(from, to, Smooth(p));
            yield return null;
        }
        transform.position = to;
        crow.speed = speedEnd;
    }

    private IEnumerator Shake(float duration)
    {
        Vector3 basePos = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float damp = 1f - Mathf.Clamp01(t / duration) * 0.4f;
            transform.position = basePos + (Vector3)(Random.insideUnitCircle * shakeStrength * damp);
            yield return null;
        }
        transform.position = basePos;
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

    private static IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        Color color = image.color;
        float safe = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < safe; t += Time.deltaTime)
        {
            color.a = Mathf.Lerp(from, to, Smooth(t / safe));
            image.color = color;
            yield return null;
        }
        color.a = to;
        image.color = color;
    }

    // Fullscreen white overlay on its own topmost canvas (same pattern as the crow scene fade).
    private static Image CreateFlashImage()
    {
        GameObject canvasObject = new GameObject("CrowPetrifyFlashCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new GameObject("CrowPetrifyFlash");
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
        return image;
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
