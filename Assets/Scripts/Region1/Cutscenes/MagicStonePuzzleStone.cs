using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The minimal contract MagicStonePuzzleStone needs from whatever orchestrates it. Extracted so
/// PracticeWordAssembly (practice_night) can reuse the stone's drag/reveal/snap behaviour without
/// depending on the much larger, quest-specific MagicStonePuzzleController.
/// </summary>
public interface IStonePuzzleOwner
{
    bool CanInteract { get; }
    float ReturnDuration { get; }
    void HandleStoneClicked(MagicStonePuzzleStone stone);
}

public sealed class MagicStonePuzzleStone : MonoBehaviour, IPointerClickHandler
{
    private IStonePuzzleOwner controller;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image image;
    private Coroutine motionRoutine;
    private Color homeColor = Color.white;
    private bool homeCaptured;
    private bool interactable;

    public RectTransform RectTransform => rectTransform;
    public Vector2 HomePosition { get; private set; }
    public Vector3 HomeScale { get; private set; }
    public int CurrentSlot { get; private set; } = -1;

    // The glyph this stone represents (e.g. "ก", "ป", "า"). Used to assemble the word.
    public string Letter { get; private set; } = "";

    public void Initialize(IStonePuzzleOwner owner)
    {
        controller = owner;
        rectTransform = transform as RectTransform;
        image = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void SetLetter(string letter)
    {
        Letter = letter ?? "";
    }

    public void CaptureHome(bool force = false)
    {
        Initialize(controller);

        if ((!force && homeCaptured) || rectTransform == null)
        {
            return;
        }

        HomePosition = rectTransform.anchoredPosition;
        HomeScale = rectTransform.localScale;
        homeColor = image != null ? image.color : Color.white;
        homeCaptured = true;
    }

    public void HideImmediate()
    {
        Initialize(controller);
        StopMotion();
        SetAlpha(0f);
        SetInteractable(false);
        gameObject.SetActive(true);
    }

    public void PrepareHidden(Vector2 offset, float startScale)
    {
        CaptureHome();
        StopMotion();

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = HomePosition + offset;
            rectTransform.localScale = HomeScale * Mathf.Max(0.01f, startScale);
        }

        SetAlpha(0f);
        SetInteractable(false);
        gameObject.SetActive(true);
    }

    // Keeps the authored scene position intact while preparing the visual reveal.
    // The offset overload above remains for the older MagicStonePuzzleController flow.
    public void PrepareHidden(float startScale)
    {
        CaptureHome();
        StopMotion();

        if (rectTransform != null)
        {
            rectTransform.localScale = HomeScale * Mathf.Max(0.01f, startScale);
        }

        SetAlpha(0f);
        SetInteractable(false);
        gameObject.SetActive(true);
    }

    public IEnumerator Reveal(float duration, Vector2 offset, float startScale)
    {
        CaptureHome();
        StopMotion();
        gameObject.SetActive(true);

        Vector2 fromPosition = HomePosition + offset;
        Vector3 fromScale = HomeScale * Mathf.Max(0.01f, startScale);
        float safeDuration = Mathf.Max(0.01f, duration);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smooth = SmoothStep(t);
            float scaleT = EaseOutBack(t);

            rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, HomePosition, smooth);
            rectTransform.localScale = Vector3.LerpUnclamped(fromScale, HomeScale, scaleT);
            SetAlpha(smooth);
            yield return null;
        }

        rectTransform.anchoredPosition = HomePosition;
        rectTransform.localScale = HomeScale;
        SetAlpha(1f);
    }

    // Reveals at the exact authored scene position; only alpha and scale animate.
    public IEnumerator Reveal(float duration, float startScale)
    {
        CaptureHome();
        StopMotion();
        gameObject.SetActive(true);

        Vector3 fromScale = rectTransform != null
            ? rectTransform.localScale
            : HomeScale * Mathf.Max(0.01f, startScale);
        float safeDuration = Mathf.Max(0.01f, duration);

        SetAlpha(0f);
        SetInteractable(false);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float fadeT = SmoothStep(t);
            float scaleT = EaseOutBack(t);

            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.LerpUnclamped(fromScale, HomeScale, scaleT);
            }

            SetAlpha(fadeT);
            yield return null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = HomeScale;
        }

        SetAlpha(1f);
        SetInteractable(true);
    }

    public void SetInteractable(bool value)
    {
        interactable = value;

        if (image != null)
        {
            image.raycastTarget = value;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = value;
            canvasGroup.interactable = value;
        }
    }

    public void SetCurrentSlot(int slotIndex)
    {
        CurrentSlot = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || controller == null || !controller.CanInteract)
        {
            return;
        }

        controller.HandleStoneClicked(this);
    }

    public void MoveTo(Vector2 targetPosition, Vector3 targetScale, float duration, bool bounce)
    {
        StopMotion();
        motionRoutine = StartCoroutine(MoveRoutine(targetPosition, targetScale, duration, bounce));
    }

    public void ReturnHome()
    {
        MoveTo(HomePosition, HomeScale, controller != null ? controller.ReturnDuration : 0.2f, false);
    }

    private IEnumerator MoveRoutine(Vector2 targetPosition, Vector3 targetScale, float duration, bool bounce)
    {
        Vector2 fromPosition = rectTransform.anchoredPosition;
        Vector3 fromScale = rectTransform.localScale;
        float safeDuration = Mathf.Max(0.01f, duration);

        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smooth = bounce ? EaseOutBack(t) : SmoothStep(t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, targetPosition, smooth);
            rectTransform.localScale = Vector3.LerpUnclamped(fromScale, targetScale, smooth);
            yield return null;
        }

        rectTransform.anchoredPosition = targetPosition;
        rectTransform.localScale = targetScale;
        motionRoutine = null;
    }

    private void StopMotion()
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            return;
        }

        if (image != null)
        {
            Color color = homeColor;
            color.a *= Mathf.Clamp01(alpha);
            image.color = color;
        }
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutBack(float value)
    {
        value = Mathf.Clamp01(value);
        const float overshoot = 1.15f;
        float shifted = value - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }
}
