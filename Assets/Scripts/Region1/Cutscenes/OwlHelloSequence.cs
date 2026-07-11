using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sprite-sequence "owl_hello" wave animation played once per scene before the owl speaks.
// Attach to a dedicated GO that has an Image component; assign frames[] in order (0175→0216).
// Call Play() before any talking sequence; AlreadyPlayed guard prevents replaying it.
public sealed class OwlHelloSequence : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private Image helloImage;
    [Tooltip("The talking owl RectTransform whose position and displayed size this animation must match.")]
    [SerializeField] private RectTransform matchTarget;

    [Header("Playback")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Frames per second. 18 keeps the 42-frame wave around 2.4s, which feels like a natural greeting instead of slow motion.")]
    [SerializeField, Min(1f)] private float fps = 18f;
    [Tooltip("Overall owl_hello animation speed. 1 = normal, 2 = twice as fast, 0.5 = half speed.")]
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    [Tooltip("When false, Play() leaves the last frame on screen so the caller can crossfade it out.")]
    [SerializeField] private bool hideOnComplete = true;
    public void SetHideOnComplete(bool value) => hideOnComplete = value;
    [Tooltip("After the wave finishes, freeze on the last frame for this long before handing off. 0 = no hold.")]
    [SerializeField, Min(0f)] private float holdAfterPlay = 0.06f;

    [Header("Optional Zoom")]
    [Tooltip("RectTransform to scale for the zoom effect. Defaults to helloImage.rectTransform.")]
    [SerializeField] private RectTransform zoomTarget;
    [SerializeField] private float zoomInDuration = 0.16f;
    [SerializeField] private float zoomOutDuration = 0.16f;
    [Tooltip("Peak scale multiplier (1.15 = 15% larger).")]
    [SerializeField, Min(1f)] private float zoomScale = 1f;

    private bool _played;
    public bool AlreadyPlayed => _played;

    // Optional override for special cases. Normal cutscenes leave this at 0 so the wave plays at
    // its authored fps; forcing it to match slow talking loops makes the entrance feel choppy.
    private float _fpsOverride;
    public void SetPlaybackFps(float effectiveFps) => _fpsOverride = Mathf.Max(0f, effectiveFps);

    private void Awake()
    {
        if (helloImage == null) helloImage = GetComponent<Image>();
        if (helloImage != null) helloImage.enabled = false; // hidden until Play()
    }

    public IEnumerator Play()
    {
        if (_played || helloImage == null || frames == null || frames.Length == 0)
            yield break;
        _played = true;

        // Reparent to root Canvas so we're always on-screen regardless of OwlRoot's position
        EnsureAtRootCanvas();

        RectTransform rt = zoomTarget != null ? zoomTarget : helloImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        float speed = Mathf.Max(0.01f, playbackSpeed);
        Vector3 bigScale = baseScale * zoomScale;
        bool useZoom = !Mathf.Approximately(zoomScale, 1f);

        helloImage.enabled = true;

        if (useZoom)
            yield return Zoom(rt, baseScale, bigScale, zoomInDuration / speed);

        // playbackSpeed scales either the authored fps or an explicit override.
        float interval = _fpsOverride > 0f
            ? 1f / (_fpsOverride * speed)
            : 1f / (Mathf.Max(1f, fps) * speed);
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) helloImage.sprite = frames[i];
            yield return new WaitForSeconds(interval);
        }

        if (holdAfterPlay > 0f)
            yield return new WaitForSeconds(holdAfterPlay);

        if (useZoom)
            yield return Zoom(rt, bigScale, baseScale, zoomOutDuration / speed);

        if (hideOnComplete) helloImage.enabled = false;
        Debug.Log("[OwlHello] Play() completed");
    }

    // Crossfade the held last frame out, then disable. The cutscene runs this concurrently with the
    // talking owl's fade-in so the seam owl_hello->owl is a smooth dissolve instead of a hard cut.
    public IEnumerator FadeOut(float duration)
    {
        if (helloImage == null) yield break;
        helloImage.enabled = true;
        Color c = helloImage.color;
        float startA = c.a;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(startA, 0f, t / duration);
            helloImage.color = c;
            yield return null;
        }
        c.a = 0f; helloImage.color = c;
        helloImage.enabled = false;
        c.a = startA; helloImage.color = c; // restore alpha in case the image is reused
    }

    // Keep the hello image at the root Canvas so unrelated parent animation cannot move it,
    // then copy the talking owl's world placement and displayed size.
    private void EnsureAtRootCanvas()
    {
        Canvas[] parents = GetComponentsInParent<Canvas>(true);
        if (parents == null || parents.Length == 0) return;
        Canvas root = parents[parents.Length - 1]; // outermost Canvas

        if (transform.parent != root.transform)
            transform.SetParent(root.transform, false);

        var rt2 = transform as RectTransform;
        if (matchTarget != null)
        {
            rt2.anchorMin = new Vector2(0.5f, 0.5f);
            rt2.anchorMax = new Vector2(0.5f, 0.5f);
            rt2.pivot = matchTarget.pivot;
            rt2.sizeDelta = matchTarget.rect.size;
            rt2.position = matchTarget.position;
            rt2.rotation = matchTarget.rotation;
            rt2.localScale = DivideScale(matchTarget.lossyScale, root.transform.lossyScale);
        }
        else
        {
            rt2.anchorMin = Vector2.zero;
            rt2.anchorMax = Vector2.one;
            rt2.offsetMin = Vector2.zero;
            rt2.offsetMax = Vector2.zero;
            rt2.localScale = Vector3.one;
        }
        transform.SetAsLastSibling();
    }

    private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            Mathf.Approximately(divisor.x, 0f) ? value.x : value.x / divisor.x,
            Mathf.Approximately(divisor.y, 0f) ? value.y : value.y / divisor.y,
            Mathf.Approximately(divisor.z, 0f) ? value.z : value.z / divisor.z);
    }

    private static IEnumerator Zoom(RectTransform rt, Vector3 from, Vector3 to, float dur)
    {
        if (dur <= 0f) { rt.localScale = to; yield break; }
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            rt.localScale = Vector3.LerpUnclamped(from, to, Smooth(t / dur));
            yield return null;
        }
        rt.localScale = to;
    }

    private static float Smooth(float v) { v = Mathf.Clamp01(v); return v * v * (3f - 2f * v); }
}
