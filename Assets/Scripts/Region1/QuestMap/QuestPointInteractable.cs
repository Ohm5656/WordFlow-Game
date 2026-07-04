using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class QuestPointInteractable : MonoBehaviour
{
    [SerializeField]
    private Transform questSignTransform;
    [SerializeField]
    private Transform magicStoneTransform;
    [SerializeField]
    private Transform bearTransform;
    [SerializeField]
    private string nextSceneName = "cut_scene1";
    [SerializeField]
    private float pulseDuration = 1.6f;
    [SerializeField]
    private float pulseScaleMin = 0.94f;
    [SerializeField]
    private float pulseScaleMax = 1.06f;
    [SerializeField]
    private float glowAlphaMin = 0.08f;
    [SerializeField]
    private float glowAlphaMax = 0.24f;
    [SerializeField]
    private float clickRadius = 1.5f;
    [SerializeField]
    private float bearClickRadius = 1.8f;
    [SerializeField]
    private float bearBounceDuration = 0.42f;
    [SerializeField]
    private float bearBounceScale = 1.14f;
    [SerializeField]
    private float bearBounceHeight = 0.18f;
    [SerializeField]
    private float delayBeforeCameraZoom = 0.5f;
    [SerializeField]
    private float cameraZoomDuration = 0.85f;
    [SerializeField]
    private float cameraZoomOrthographicSize = 2.35f;
    [SerializeField]
    private Vector2 cameraZoomOffset = Vector2.zero;
    [SerializeField]
    private float delayAfterCameraZoom = 0.55f;
    [SerializeField]
    private float whiteFlashFadeInDuration = 0.18f;
    [SerializeField]
    private float whiteFlashHoldDuration = 0.08f;
    [SerializeField]
    private Color whiteFlashColor = Color.white;

    private Vector3 questSignBaseScale;
    private Vector3 stoneBaseScale;
    private Vector3 bearBaseScale;
    private Vector3 bearBasePosition;
    private SpriteRenderer questSignGlow;
    private SpriteRenderer stoneGlow;
    private SpriteRenderer bearRenderer;
    private Vector3 questSignPosition;
    private Vector3 stonePosition;
    private Vector3 bearPosition;
    private Coroutine pulseRoutine;
    private bool transitioning;

    public void Initialize(Transform questSign, Transform stone, Transform bear = null)
    {
        if (questSign != null)
        {
            questSignTransform = questSign;
        }

        if (stone != null)
        {
            magicStoneTransform = stone;
        }

        if (bear != null)
        {
            bearTransform = bear;
        }
    }

    private void Start()
    {
        if (questSignTransform == null)
        {
            questSignTransform = transform.Find("panel ?");
        }

        if (magicStoneTransform == null)
        {
            magicStoneTransform = transform.Find("stone");
        }

        if (bearTransform == null)
        {
            bearTransform = transform.Find("bear_0");
        }

        if (questSignTransform != null)
        {
            questSignBaseScale = questSignTransform.localScale;
            questSignPosition = questSignTransform.position;
            CreateGlowLayer(questSignTransform, out questSignGlow);
        }

        if (magicStoneTransform != null)
        {
            stoneBaseScale = magicStoneTransform.localScale;
            stonePosition = magicStoneTransform.position;
            CreateGlowLayer(magicStoneTransform, out stoneGlow);
        }

        if (bearTransform != null)
        {
            bearBaseScale = bearTransform.localScale;
            bearBasePosition = bearTransform.localPosition;
            bearPosition = bearTransform.position;
            bearRenderer = bearTransform.GetComponent<SpriteRenderer>();
        }

        StartPulsing();
    }

    private void Update()
    {
        if (transitioning)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseClick();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            HandleTouchClick();
        }
    }

    private void HandleMouseClick()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        if (IsClickOnQuestPoint(worldPos))
        {
            GameAudio.PlayClick();
            TryOpenNextScene();
        }
    }

    private void HandleTouchClick()
    {
        Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(touchPos);

        if (IsClickOnQuestPoint(worldPos))
        {
            GameAudio.PlayClick();
            TryOpenNextScene();
        }
    }

    private bool IsClickOnQuestPoint(Vector3 clickWorldPos)
    {
        Vector2 clickPoint = new Vector2(clickWorldPos.x, clickWorldPos.y);

        if (IsClickOnBear(clickPoint))
        {
            return true;
        }

        float distToSign = questSignTransform != null
            ? Vector2.Distance(clickPoint, new Vector2(questSignPosition.x, questSignPosition.y))
            : float.MaxValue;
        float distToStone = magicStoneTransform != null
            ? Vector2.Distance(clickPoint, new Vector2(stonePosition.x, stonePosition.y))
            : float.MaxValue;

        return Mathf.Min(distToSign, distToStone) <= clickRadius;
    }

    private bool IsClickOnBear(Vector2 clickPoint)
    {
        if (bearTransform == null)
        {
            return false;
        }

        if (bearRenderer != null)
        {
            Bounds bounds = bearRenderer.bounds;
            bounds.Expand(0.25f);

            if (bounds.Contains(new Vector3(clickPoint.x, clickPoint.y, bounds.center.z)))
            {
                return true;
            }
        }

        return Vector2.Distance(clickPoint, new Vector2(bearPosition.x, bearPosition.y)) <= bearClickRadius;
    }

    private void TryOpenNextScene()
    {
        if (transitioning)
        {
            return;
        }

        transitioning = true;
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        Coroutine bounceRoutine = StartCoroutine(BounceBear());

        if (delayBeforeCameraZoom > 0f)
        {
            yield return new WaitForSeconds(delayBeforeCameraZoom);
        }

        yield return ZoomCameraToQuestPoint();

        if (bounceRoutine != null)
        {
            yield return bounceRoutine;
        }

        if (delayAfterCameraZoom > 0f)
        {
            yield return new WaitForSeconds(delayAfterCameraZoom);
        }

        yield return WhiteFlash();

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("Quest point clicked, but nextSceneName is empty.");
            transitioning = false;
        }
    }

    private void StartPulsing()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
        }

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            yield return PulseOnce(1f);
            yield return new WaitForSeconds(0.24f);
        }
    }

    private IEnumerator PulseOnce(float targetScale)
    {
        float halfDuration = pulseDuration * 0.5f;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            UpdateTransformScale(smooth, targetScale, true);
            UpdateGlowAlpha(smooth, true);
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            UpdateTransformScale(smooth, targetScale, false);
            UpdateGlowAlpha(smooth, false);
            yield return null;
        }

        if (questSignTransform != null)
        {
            questSignTransform.localScale = questSignBaseScale;
        }

        if (magicStoneTransform != null)
        {
            magicStoneTransform.localScale = stoneBaseScale;
        }

        SetGlowAlpha(glowAlphaMin);
    }

    private IEnumerator BounceBear()
    {
        if (bearTransform == null || bearBounceDuration <= 0f)
        {
            yield break;
        }

        float halfDuration = bearBounceDuration * 0.5f;
        Vector3 raisedPosition = bearBasePosition + Vector3.up * bearBounceHeight;
        Vector3 enlargedScale = bearBaseScale * bearBounceScale;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
            bearTransform.localPosition = Vector3.LerpUnclamped(bearBasePosition, raisedPosition, smooth);
            bearTransform.localScale = Vector3.LerpUnclamped(bearBaseScale, enlargedScale, smooth);
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
            bearTransform.localPosition = Vector3.LerpUnclamped(raisedPosition, bearBasePosition, smooth);
            bearTransform.localScale = Vector3.LerpUnclamped(enlargedScale, bearBaseScale, smooth);
            yield return null;
        }

        bearTransform.localPosition = bearBasePosition;
        bearTransform.localScale = bearBaseScale;
    }

    private IEnumerator ZoomCameraToQuestPoint()
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic || cameraZoomDuration <= 0f)
        {
            yield break;
        }

        Transform cameraTransform = camera.transform;
        Vector3 startPosition = cameraTransform.position;
        float startSize = camera.orthographicSize;

        Vector3 focusPosition = GetQuestFocusPosition();
        Vector3 targetPosition = new Vector3(
            focusPosition.x + cameraZoomOffset.x,
            focusPosition.y + cameraZoomOffset.y,
            startPosition.z);
        float targetSize = Mathf.Clamp(cameraZoomOrthographicSize, 0.75f, startSize);

        for (float elapsed = 0f; elapsed < cameraZoomDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / cameraZoomDuration);
            float smooth = SmootherStep(t);
            cameraTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, smooth);
            camera.orthographicSize = Mathf.LerpUnclamped(startSize, targetSize, smooth);
            yield return null;
        }

        cameraTransform.position = targetPosition;
        camera.orthographicSize = targetSize;
    }

    private Vector3 GetQuestFocusPosition()
    {
        if (bearRenderer != null)
        {
            Vector3 center = bearRenderer.bounds.center;
            center.z = 0f;
            return center;
        }

        if (bearTransform != null)
        {
            Vector3 position = bearTransform.position;
            position.z = 0f;
            return position;
        }

        if (questSignTransform != null && magicStoneTransform != null)
        {
            Vector3 center = (questSignTransform.position + magicStoneTransform.position) * 0.5f;
            center.z = 0f;
            return center;
        }

        if (questSignTransform != null)
        {
            Vector3 position = questSignTransform.position;
            position.z = 0f;
            return position;
        }

        if (magicStoneTransform != null)
        {
            Vector3 position = magicStoneTransform.position;
            position.z = 0f;
            return position;
        }

        return Vector3.zero;
    }

    private IEnumerator WhiteFlash()
    {
        Image flashImage = CreateFlashImage();

        if (flashImage == null)
        {
            yield break;
        }

        Color color = whiteFlashColor;
        color.a = 0f;
        flashImage.color = color;

        float fadeDuration = Mathf.Max(0.01f, whiteFlashFadeInDuration);

        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
            color.a = smooth;
            flashImage.color = color;
            yield return null;
        }

        color.a = 1f;
        flashImage.color = color;

        if (whiteFlashHoldDuration > 0f)
        {
            yield return new WaitForSeconds(whiteFlashHoldDuration);
        }
    }

    private Image CreateFlashImage()
    {
        GameObject flashCanvasObject = new GameObject("QuestTransitionFlashCanvas");
        Canvas flashCanvas = flashCanvasObject.AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = short.MaxValue;

        CanvasScaler canvasScaler = flashCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject flashImageObject = new GameObject("WhiteFlash");
        flashImageObject.transform.SetParent(flashCanvasObject.transform, false);

        RectTransform flashRect = flashImageObject.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Image flashImage = flashImageObject.AddComponent<Image>();
        flashImage.raycastTarget = false;
        return flashImage;
    }

    private void UpdateTransformScale(float t, float scaleTarget, bool scalingUp)
    {
        float minScale = pulseScaleMin * scaleTarget;
        float maxScale = pulseScaleMax * scaleTarget;

        float questScale = scalingUp ?
            Mathf.LerpUnclamped(minScale, maxScale, t) :
            Mathf.LerpUnclamped(maxScale, minScale, t);

        if (questSignTransform != null)
        {
            questSignTransform.localScale = questSignBaseScale * questScale;
        }

        if (magicStoneTransform != null)
        {
            magicStoneTransform.localScale = stoneBaseScale * questScale;
        }
    }

    private void UpdateGlowAlpha(float t, bool glowingUp)
    {
        float glowAlpha = glowingUp ?
            Mathf.Lerp(glowAlphaMin, glowAlphaMax, t) :
            Mathf.Lerp(glowAlphaMax, glowAlphaMin, t);

        SetGlowAlpha(glowAlpha);
    }

    private void SetGlowAlpha(float alpha)
    {
        if (questSignGlow != null)
        {
            Color color = questSignGlow.color;
            color.a = alpha;
            questSignGlow.color = color;
        }

        if (stoneGlow != null)
        {
            Color color = stoneGlow.color;
            color.a = alpha;
            stoneGlow.color = color;
        }
    }

    private void CreateGlowLayer(Transform target, out SpriteRenderer glowRenderer)
    {
        glowRenderer = null;

        if (target == null)
        {
            return;
        }

        SpriteRenderer originalRenderer = target.GetComponent<SpriteRenderer>();
        if (originalRenderer == null)
        {
            return;
        }

        Transform existingGlow = target.Find("Glow");
        if (existingGlow != null)
        {
            glowRenderer = existingGlow.GetComponent<SpriteRenderer>();
            if (glowRenderer != null)
            {
                glowRenderer.gameObject.SetActive(true);
                return;
            }
        }

        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(target, false);
        glowObj.transform.localPosition = Vector3.zero;
        glowObj.transform.localScale = Vector3.one * 1.18f;

        glowRenderer = glowObj.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = originalRenderer.sprite;
        glowRenderer.color = new Color(1f, 1f, 1f, glowAlphaMin);
        glowRenderer.sortingOrder = originalRenderer.sortingOrder - 1;
    }

    private static float SmootherStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
}
