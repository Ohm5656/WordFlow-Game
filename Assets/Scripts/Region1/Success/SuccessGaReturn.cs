using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Success_ga = the player built กา (a real word, but NOT the target ปา). After the celebration
// animations finish, fade to black and return to CutScene_bear in retry mode so they reassemble —
// not a fresh start (the retry flag makes the bear skip its entrance walk). No world-map progress
// is marked here: กา is the wrong word.
public sealed class SuccessGaReturn : MonoBehaviour
{
    [Tooltip("Let the ga / bear_question animations play this long before fading out.")]
    [SerializeField] private float playSeconds = 4.5f; // ponytail: ga clip ~4s; tune to clip length
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private string returnSceneName = "CutScene_bear";

    private void Start() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        // Match the other crow scenes: loop Crow.mp3 only while the celebration animation plays.
        GameAudio.PlayCrowLoop();

        if (playSeconds > 0f)
        {
            yield return new WaitForSeconds(playSeconds);
        }

        GameAudio.StopSfxLoop();

        Image fade = CreateFadeImage(fadeColor);
        Color c = fadeColor;
        float d = Mathf.Max(0.01f, fadeDuration);
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            c.a = Mathf.Clamp01(t / d);
            fade.color = c;
            yield return null;
        }
        c.a = 1f;
        fade.color = c;

        MagicStonePuzzleController.RequestRetryAfterCrow(); // back to assembly, skip the intro walk
        MagicStonePuzzleController.RequestRetryAfterAlt();  // signal: show stones all-at-once on return
        SceneManager.LoadScene(returnSceneName);
    }

    private void OnDisable()
    {
        GameAudio.StopSfxLoop();
    }

    private static Image CreateFadeImage(Color color)
    {
        var canvasGo = new GameObject("SuccessGaFadeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840f, 2160f);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGo = new GameObject("SuccessGaFade");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rect = imgGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        color.a = 0f;
        img.color = color;
        img.raycastTarget = true; // block taps during the fade
        return img;
    }
}
