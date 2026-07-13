using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dramatic "the bear village needs help" intro played once when the hero first reaches wp_1,
/// between the bear/"!" reveal and entering CutScene_bear. It:
///   1. Zooms the camera in toward the bear.
///   2. Darkens everything with a soft circular spotlight left over the bear + its "!"
///      (reuses the NightOverlayCutout shader via <see cref="spotlightOverlay"/>'s material).
///   3. Fades an owl guide into the lower-left corner, which plays the "owl_wow" surprise clip
///      once ("แย่แล้ว") then loops "owl_talk" for the rest of the voice line.
///   4. Speaks the baked owl voice line locally (no backend), ducking the music under it.
/// QuestPathSequence yields on <see cref="PlayIntro"/> then does its usual cover + LoadScene.
/// Frame playback is a plain sprite-swap (no Animator) so it stays cheap on mobile; the
/// per-second frame rate and an overall speed multiplier are tunable in the inspector.
/// </summary>
public sealed class BearIntroSequence : MonoBehaviour
{
    [Header("Camera zoom")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("What the camera centers on (usually the bear). Its XY is framed; Z is kept.")]
    [SerializeField] private Transform zoomFocus;
    [Tooltip("Orthographic size to zoom to (smaller = closer). The scene's default is ~10.")]
    [SerializeField] private float zoomOrthoSize = 5.5f;
    [Tooltip("Extra offset added to the focus point so the framing isn't dead-centre.")]
    [SerializeField] private Vector2 zoomOffset = new Vector2(0f, 0.5f);
    [SerializeField] private float zoomDuration = 1.1f;

    [Header("Spotlight darkness")]
    [Tooltip("Big SpriteRenderer using the NightOverlayCutout material, covering the view.")]
    [SerializeField] private SpriteRenderer spotlightOverlay;
    [SerializeField] private Color darkColor = new Color(0.03f, 0.03f, 0.06f, 1f);
    [Range(0f, 1f)] [SerializeField] private float maxDarkAlpha = 0.82f;
    [Tooltip("World transforms the spotlight openings center on (bear, then its \"!\"). Up to 4.")]
    [SerializeField] private Transform[] spotlightTargets;
    [Tooltip("Outer radius of each opening (world units); index matches spotlightTargets.")]
    [SerializeField] private float[] spotlightRadii = { 3f, 1.6f };
    [Range(0.01f, 0.9f)] [SerializeField] private float clearCenterFraction = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float minimumDarknessInLight = 0.0f;
    [SerializeField] private float darkFadeDuration = 0.9f;

    [Header("Owl guide")]
    [SerializeField] private CanvasGroup owlGroup;
    [SerializeField] private Image owlImage;
    [Tooltip("Surprise clip, played ONCE at the start (\"แย่แล้ว\").")]
    [SerializeField] private Sprite[] wowFrames;
    [Tooltip("Talking clip, LOOPED for the rest of the voice line.")]
    [SerializeField] private Sprite[] talkFrames;
    [Tooltip("Frames per second of the owl animation.")]
    [SerializeField, Min(1f)] private float owlFps = 24f;
    [Tooltip("Overall speed multiplier for the owl animation (1 = fps as-is).")]
    [SerializeField, Min(0.05f)] private float owlSpeed = 1f;
    [SerializeField] private float owlFadeDuration = 0.4f;

    [Header("Voice")]
    [Tooltip("Baked owl line (Assets/Resources/TTS). Assigned in the inspector; no backend call.")]
    [SerializeField] private AudioClip voiceClip;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [Tooltip("Extra seconds to keep talking/holding after the voice clip ends.")]
    [SerializeField] private float tailHold = 0.3f;

    private AudioSource voiceSource;
    private MaterialPropertyBlock overlayProps;

    private static readonly int[] LightDataIds =
    {
        Shader.PropertyToID("_LightData0"),
        Shader.PropertyToID("_LightData1"),
        Shader.PropertyToID("_LightData2"),
        Shader.PropertyToID("_LightData3")
    };
    private static readonly int MinDarknessId = Shader.PropertyToID("_MinimumDarkness");

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        // NOTE: we deliberately do NOT touch owlGroup.alpha here. The owl is left visible in the
        // scene (alpha 1) so its size/position can be tuned in the editor; PlayIntro drives the
        // runtime fade itself. Hide it however you like in the scene (disable the GameObject, the
        // Image, or set alpha 0) — PlayIntro force-restores it, so the intro always still plays.
        if (spotlightOverlay != null)
        {
            Color c = darkColor; c.a = 0f;
            spotlightOverlay.color = c;
        }
    }

    /// <summary>Runs the whole intro; returns when the owl has finished speaking.</summary>
    public IEnumerator PlayIntro()
    {
        // Restore the owl no matter how it was hidden in the scene, then start it invisible so it
        // fades in cleanly.
        EnsureOwlVisible();
        if (owlGroup != null) owlGroup.alpha = 0f;

        // Kick off camera zoom + darkness in parallel with the owl appearing.
        Coroutine zoom = StartCoroutine(ZoomIn());
        Coroutine dark = StartCoroutine(FadeDark(0f, maxDarkAlpha, darkFadeDuration));
        Coroutine owlIn = StartCoroutine(FadeOwl(0f, 1f, owlFadeDuration));

        // Start the voice + duck the music.
        float voiceLength = 0f;
        if (voiceClip != null)
        {
            EnsureVoiceSource();
            voiceSource.clip = voiceClip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            voiceLength = voiceClip.length;
            GameAudio.SetVoiceDucking(true);
        }

        // owl_wow once ("แย่แล้ว"), then owl_talk loops until the voice line finishes.
        yield return owlIn;
        Coroutine owlAnim = StartCoroutine(PlayOwlFrames());

        float wait = Mathf.Max(voiceLength + tailHold, EstimatedOwlWowSeconds());
        yield return new WaitForSeconds(wait);

        if (owlAnim != null) StopCoroutine(owlAnim);
        GameAudio.SetVoiceDucking(false);

        // Make sure the parallel visual cues have settled before handing back.
        yield return zoom;
        yield return dark;
    }

    // ------------------------------------------------------------ camera

    private IEnumerator ZoomIn()
    {
        if (targetCamera == null || zoomFocus == null) yield break;

        Vector3 startPos = targetCamera.transform.position;
        float startSize = targetCamera.orthographic ? targetCamera.orthographicSize : startPos.z;
        Vector3 focus = zoomFocus.position;
        Vector3 endPos = new Vector3(focus.x + zoomOffset.x, focus.y + zoomOffset.y, startPos.z);

        float duration = Mathf.Max(0.01f, zoomDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float e = Smoother(t / duration);
            targetCamera.transform.position = Vector3.Lerp(startPos, endPos, e);
            if (targetCamera.orthographic)
                targetCamera.orthographicSize = Mathf.Lerp(startSize, zoomOrthoSize, e);
            yield return null;
        }

        targetCamera.transform.position = endPos;
        if (targetCamera.orthographic) targetCamera.orthographicSize = zoomOrthoSize;
    }

    // ------------------------------------------------------------ darkness / spotlight

    private IEnumerator FadeDark(float from, float to, float duration)
    {
        if (spotlightOverlay == null) yield break;
        PushSpotlightData();

        duration = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetDarkAlpha(Mathf.Lerp(from, to, Smoother(t / duration)));
            PushSpotlightData(); // cheap; keeps openings on their targets
            yield return null;
        }
        SetDarkAlpha(to);
        PushSpotlightData();
    }

    private void SetDarkAlpha(float a)
    {
        Color c = darkColor;
        c.a = a;
        spotlightOverlay.color = c;
    }

    private void PushSpotlightData()
    {
        if (spotlightOverlay == null) return;
        if (overlayProps == null) overlayProps = new MaterialPropertyBlock();

        spotlightOverlay.GetPropertyBlock(overlayProps);
        for (int i = 0; i < 4; i++)
        {
            Vector4 data = Vector4.zero;
            if (spotlightTargets != null && i < spotlightTargets.Length && spotlightTargets[i] != null)
            {
                Vector3 center = spotlightTargets[i].position;
                float outer = spotlightRadii != null && i < spotlightRadii.Length
                    ? Mathf.Max(0.01f, spotlightRadii[i]) : 2.5f;
                float inner = outer * Mathf.Clamp01(clearCenterFraction);
                data = new Vector4(center.x, center.y, inner, outer);
            }
            overlayProps.SetVector(LightDataIds[i], data);
        }
        overlayProps.SetFloat(MinDarknessId, minimumDarknessInLight);
        spotlightOverlay.SetPropertyBlock(overlayProps);
    }

    // ------------------------------------------------------------ owl

    // Re-enable the owl whichever way it was hidden in the scene (disabled GameObject anywhere up
    // its parent chain, or a disabled Image), so tuning/hiding it never stops the intro working.
    private void EnsureOwlVisible()
    {
        if (owlImage != null) owlImage.enabled = true;
        Transform t = owlGroup != null ? owlGroup.transform
            : (owlImage != null ? owlImage.transform : null);
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private IEnumerator FadeOwl(float from, float to, float duration)
    {
        if (owlGroup == null) yield break;
        duration = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            owlGroup.alpha = Mathf.Lerp(from, to, Smoother(t / duration));
            yield return null;
        }
        owlGroup.alpha = to;
    }

    private IEnumerator PlayOwlFrames()
    {
        if (owlImage == null) yield break;
        float interval = 1f / (Mathf.Max(1f, owlFps) * Mathf.Max(0.05f, owlSpeed));

        // owl_wow once
        if (wowFrames != null)
        {
            for (int i = 0; i < wowFrames.Length; i++)
            {
                if (wowFrames[i] != null) owlImage.sprite = wowFrames[i];
                yield return new WaitForSeconds(interval);
            }
        }

        // owl_talk loop until stopped
        if (talkFrames != null && talkFrames.Length > 0)
        {
            int i = 0;
            while (true)
            {
                if (talkFrames[i] != null) owlImage.sprite = talkFrames[i];
                i = (i + 1) % talkFrames.Length;
                yield return new WaitForSeconds(interval);
            }
        }
    }

    private float EstimatedOwlWowSeconds()
    {
        int n = wowFrames != null ? wowFrames.Length : 0;
        return n / (Mathf.Max(1f, owlFps) * Mathf.Max(0.05f, owlSpeed));
    }

    private void EnsureVoiceSource()
    {
        if (voiceSource != null) return;
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
    }

    private static float Smoother(float v)
    {
        v = Mathf.Clamp01(v);
        return v * v * v * (v * (v * 6f - 15f) + 10f);
    }
}
