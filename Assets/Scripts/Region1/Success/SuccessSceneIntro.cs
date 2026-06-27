using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SuccessSceneIntro : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip introClip;
    [SerializeField] private float introSoundDelay = 0.05f;
    [SerializeField] private float introVolume = 1f;

    [Header("Black Fade")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float blackHoldDuration = 0.12f;
    [SerializeField] private float blackFadeOutDuration = 1.45f;
    [SerializeField] private Color blackFadeColor = Color.black;

    [Header("World Map Progress")]
    [SerializeField] private bool markWorldMapProgressOnStart = true;
    [SerializeField] private int completedRegionNumber = 1;

    private Coroutine introRoutine;

    private void OnEnable()
    {
        if (markWorldMapProgressOnStart)
        {
            WorldMapProblemIslands.MarkRegionCompleted(completedRegionNumber);
        }

        if (playOnStart)
        {
            PlayIntro();
        }
    }

    private void OnDisable()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }
    }

    public void PlayIntro()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
        }

        introRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        PlayIntroSound();

        Image fadeImage = CreateFadeImage();
        if (fadeImage == null)
        {
            introRoutine = null;
            yield break;
        }

        Color color = blackFadeColor;
        color.a = 1f;
        fadeImage.color = color;

        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        }

        float duration = Mathf.Max(0.01f, blackFadeOutDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(1f, 0f, EaseInOutSine(t));
            fadeImage.color = color;
            yield return null;
        }

        Destroy(fadeImage.transform.root.gameObject);
        introRoutine = null;
    }

    private void PlayIntroSound()
    {
        if (introClip == null)
        {
            return;
        }

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = Mathf.Clamp01(introVolume);
        audioSource.clip = introClip;

        if (introSoundDelay > 0f)
        {
            audioSource.PlayDelayed(introSoundDelay);
        }
        else
        {
            audioSource.Play();
        }
    }

    private static Image CreateFadeImage()
    {
        GameObject fadeCanvasObject = new GameObject("SuccessBlackFadeCanvas");
        Canvas fadeCanvas = fadeCanvasObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = fadeCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(3840f, 2160f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("SuccessBlackFade");
        imageObject.transform.SetParent(fadeCanvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return image;
    }

    private static float EaseInOutSine(float t)
    {
        t = Mathf.Clamp01(t);
        return 0.5f - Mathf.Cos(t * Mathf.PI) * 0.5f;
    }
}
