using System.Collections;
using UnityEngine;

/// <summary>
/// Reveal effect: fades the sprites in (alpha 0 -> normal) while playing a short
/// phone-notification-style buzz (rapid, decaying jitter). Call <see cref="Play"/> when the
/// object is revealed. Shakes the root's localPosition only, so a child bob keeps running.
/// Fades every SpriteRenderer under this object (so a grouped quest spot fades as one).
/// </summary>
public sealed class RevealPopShake : MonoBehaviour
{
    [Tooltip("Max buzz offset in local units at the start.")]
    [SerializeField] private float amplitude = 0.12f;

    [Tooltip("How long the buzz lasts (seconds).")]
    [SerializeField] private float duration = 0.4f;

    [Tooltip("Buzz oscillations per second (higher = faster, more phone-like).")]
    [SerializeField] private float frequency = 26f;

    [Tooltip("Fade the sprites in on reveal.")]
    [SerializeField] private bool fadeIn = true;

    [Tooltip("Fade-in length (seconds).")]
    [SerializeField] private float fadeDuration = 0.35f;

    private Vector3 baseLocal;
    private bool cached;
    private Coroutine routine;
    private SpriteRenderer[] renderers;
    private float[] targetAlpha;

    private void Awake() => Cache();

    private void Cache()
    {
        if (cached) return;
        baseLocal = transform.localPosition;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        targetAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) targetAlpha[i] = renderers[i].color.a;
        cached = true;
    }

    /// <summary>Restart the fade + buzz from the beginning.</summary>
    public void Play()
    {
        Cache();
        if (!isActiveAndEnabled) return;
        if (routine != null) StopCoroutine(routine);
        if (fadeIn) SetAlphaMul(0f);
        routine = StartCoroutine(Routine());
    }

    private void SetAlphaMul(float mul)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = renderers[i].color;
            c.a = targetAlpha[i] * mul;
            renderers[i].color = c;
        }
    }

    private IEnumerator Routine()
    {
        float t = 0f;
        float total = Mathf.Max(duration, fadeIn ? fadeDuration : 0f);
        while (t < total)
        {
            t += Time.deltaTime;

            if (t <= duration)
            {
                float damp = 1f - Mathf.Clamp01(t / duration);
                float buzz = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amplitude * damp;
                transform.localPosition = baseLocal + new Vector3(buzz, buzz * 0.25f, 0f);
            }
            else
            {
                transform.localPosition = baseLocal;
            }

            if (fadeIn && t <= fadeDuration)
                SetAlphaMul(Mathf.Clamp01(t / fadeDuration));

            yield return null;
        }

        transform.localPosition = baseLocal;
        if (fadeIn) SetAlphaMul(1f);
        routine = null;
    }
}
