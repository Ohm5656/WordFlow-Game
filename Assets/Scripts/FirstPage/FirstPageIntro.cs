using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using WordFlow.Adventure.Net;

/// <summary>
/// first_page title screen. Sequence:
///   1. intro_rev plays once, speed ramping 2x -> 1x; the WordFlow logo drop-bounces in.
///   2. idle_loop (already baked forward+backward) loops natively at 1x. On
///      entering this phase the press-to-start prompt floats up (plus a logout
///      button when a stored session exists). A stored session is silently
///      restored during the intro so pressing can enter WorldMap immediately
///      when it is still live; no/expired sessions swap the prompt for the
///      Login/Sign-up buttons.
///
/// The idle ping-pong is baked into one clip rather than swapped between two
/// players: Unity's own looping has no swap latency, and the intro player is
/// stopped once it hands over, so only one video decoder runs from then on.
/// Press-to-start uses the warmed silent TryAutoLogin before entering WorldMap;
/// on failure (expired token) the auth buttons replace the prompt.
/// </summary>
public sealed class FirstPageIntro : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string LoginSceneName = "Login";
    private const string RegisterSceneName = "Register";

    private const float MinSpeed = 1f;
    private const float MaxSpeed = 2f;
    private const float SceneFadeDuration = 0.35f;
    private const double FallbackClipLength = 5.0417; // seconds, matches the baked source clips

    private const float LogoEntranceDelay = 0.6f;  // seconds into the intro leg
    private const float LogoDropDuration = 1.1f;
    private const float LogoDropHeight = 420f;     // px the logo falls from
    private const float LogoFadePortion = 0.35f;   // fraction of the drop spent fading in

    private const float UiFloatDuration = 0.55f;
    private const float UiFloatDistance = 160f;
    private const float UiSwapFadeDuration = 0.25f;

    private const float PromptPulsePeriod = 1.15f;
    private const float PromptMinimumAlpha = 0.55f;
    private const float PromptMinimumScale = 0.97f;
    private const float PromptMaximumScale = 1.03f;

    [Header("Video")]
    [SerializeField] private VideoPlayer introPlayer;
    [SerializeField] private VideoPlayer idlePlayer;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private VideoClip introForwardClip;
    [SerializeField] private VideoClip introReverseClip;
    [SerializeField] private VideoClip idleLoopClip;

    [Header("Logo")]
    [SerializeField] private CanvasGroup logoGroup;
    [SerializeField] private RectTransform logoRect;

    [Header("Session UI")]
    [SerializeField] private CanvasGroup pressToStartGroup;
    [SerializeField] private RectTransform pressToStartRect;
    [SerializeField] private CanvasGroup logoutGroup;
    [SerializeField] private Button logoutButton;

    [Header("Auth UI")]
    [SerializeField] private CanvasGroup authButtonsGroup;
    [SerializeField] private RectTransform authButtonsRect;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signupButton;

    private bool enteredIdle;
    private bool acceptingInput;
    private bool transitioning;
    private bool autoLoginStarted;
    private bool autoLoginFinished;
    private bool autoLoginSucceeded;
    private int autoLoginGeneration;
    private Vector2 logoRestPosition;
    private Vector2 promptRestPosition;
    private Vector2 authRestPosition;

    private void Awake()
    {
        ConfigurePlayer(introPlayer, loop: false);
        ConfigurePlayer(idlePlayer, loop: true);

        logoRestPosition = logoRect.anchoredPosition;
        promptRestPosition = pressToStartRect.anchoredPosition;
        authRestPosition = authButtonsRect.anchoredPosition;

        HideGroup(logoGroup);
        HideGroup(pressToStartGroup);
        HideGroup(logoutGroup);
        HideGroup(authButtonsGroup);

        loginButton.onClick.AddListener(() => LeaveTo(LoginSceneName));
        signupButton.onClick.AddListener(() => LeaveTo(RegisterSceneName));
        logoutButton.onClick.AddListener(OnLogout);

        introPlayer.loopPointReached += OnIntroClipFinished;

        // Only the reverse leg plays: intro_rev at 2x ramping to 1x, then it hands off to the idle loop.
        introPlayer.clip = introReverseClip;
        introPlayer.prepareCompleted += OnReverseClipReady;
        introPlayer.Prepare();

        idlePlayer.clip = idleLoopClip;
        idlePlayer.Prepare();
    }

    private void Start()
    {
        StartAutoLoginWarmup();
    }

    private void Update()
    {
        if (!enteredIdle)
        {
            double length = introPlayer.length > 0 ? introPlayer.length : FallbackClipLength;
            float t = Mathf.Clamp01((float)(introPlayer.time / length));
            introPlayer.playbackSpeed = Mathf.Lerp(MaxSpeed, MinSpeed, t);

            // loopPointReached is unreliable on a non-looping clip whose playbackSpeed is being ramped
            // every frame, so hand off to the idle loop the moment the reverse clip hits its last frame
            // rather than waiting on the event. The last reverse frame matches idle_loop's first frame.
            if (introPlayer.frameCount > 0 && introPlayer.frame >= (long)introPlayer.frameCount - 1)
            {
                EnterIdleLoop();
            }
        }

        if (acceptingInput && !transitioning && EnterPressedThisFrame())
        {
            OnPressToStart();
        }

        if (acceptingInput && !transitioning)
        {
            AnimatePressToStart();
        }
    }

    private void OnIntroClipFinished(VideoPlayer vp)
    {
        EnterIdleLoop();
    }

    /// Swap to the looping idle clip and float the UI up. Runs once; both loopPointReached and the
    /// Update frame-check can call it, whichever wins.
    private void EnterIdleLoop()
    {
        if (enteredIdle) return;
        enteredIdle = true;

        introPlayer.loopPointReached -= OnIntroClipFinished;

        // The idle player was prepared during the intro, so its first frame is already
        // in its render texture — swapping the surface first means no black flash.
        videoSurface.texture = idlePlayer.targetTexture;
        idlePlayer.Play();
        introPlayer.Stop();

        StartCoroutine(RevealUi());
    }

    private void OnReverseClipReady(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnReverseClipReady;
        videoSurface.texture = vp.targetTexture;
        vp.playbackSpeed = MaxSpeed;
        vp.Play();
        StartCoroutine(LogoEntrance());
    }

    // ---- logo ----

    private IEnumerator LogoEntrance()
    {
        yield return new WaitForSeconds(LogoEntranceDelay);

        for (float t = 0f; t < LogoDropDuration; t += Time.deltaTime)
        {
            float p = t / LogoDropDuration;
            logoGroup.alpha = Mathf.Clamp01(p / LogoFadePortion);
            logoRect.anchoredPosition = logoRestPosition + Vector2.up * (LogoDropHeight * (1f - EaseOutBounce(p)));
            yield return null;
        }

        logoGroup.alpha = 1f;
        logoRect.anchoredPosition = logoRestPosition;
    }

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1; return n1 * t * t + 0.984375f;
    }

    // ---- UI reveal ----

    private IEnumerator RevealUi()
    {
        // Always float up press-to-start first. A stored session is already being restored in the
        // background; on press, a live session enters WorldMap while no/expired sessions swap in the
        // Login/Sign-up buttons. Logout only makes sense when a stored session exists, so it rides in
        // alongside the prompt in that case.
        bool hasSession = AuthSession.Instance != null && AuthSession.Instance.HasStoredSession;
        if (hasSession) StartCoroutine(FadeIn(logoutGroup, UiFloatDuration));
        yield return FloatUp(pressToStartGroup, pressToStartRect, promptRestPosition);
        if (hasSession) EnableGroup(logoutGroup);
        acceptingInput = true;
    }

    private IEnumerator FloatUp(CanvasGroup group, RectTransform rect, Vector2 rest)
    {
        for (float t = 0f; t < UiFloatDuration; t += Time.deltaTime)
        {
            float p = t / UiFloatDuration;
            float eased = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
            group.alpha = p;
            rect.anchoredPosition = rest + Vector2.down * (UiFloatDistance * (1f - eased));
            yield return null;
        }

        group.alpha = 1f;
        rect.anchoredPosition = rest;
    }

    private static IEnumerator FadeIn(CanvasGroup group, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            group.alpha = t / duration;
            yield return null;
        }

        group.alpha = 1f;
    }

    private static IEnumerator FadeOut(CanvasGroup group, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        group.alpha = 0f;
    }

    private IEnumerator SwapPromptForAuthButtons()
    {
        // Kill interaction immediately; the fades below are cosmetic.
        logoutGroup.interactable = false;
        logoutGroup.blocksRaycasts = false;
        pressToStartGroup.interactable = false;
        pressToStartGroup.blocksRaycasts = false;

        StartCoroutine(FadeOut(logoutGroup, UiSwapFadeDuration));
        yield return FadeOut(pressToStartGroup, UiSwapFadeDuration);
        yield return FloatUp(authButtonsGroup, authButtonsRect, authRestPosition);
        EnableGroup(authButtonsGroup);
    }

    // ---- actions ----

    private void OnPressToStart()
    {
        transitioning = true;

        if (autoLoginFinished)
        {
            CompletePressToStart(autoLoginSucceeded);
            return;
        }

        if (autoLoginStarted)
        {
            StartCoroutine(WaitForAutoLoginThenContinue());
            return;
        }

        CompletePressToStart(false);
    }

    private void StartAutoLoginWarmup()
    {
        AuthSession session = AuthSession.Instance;
        if (session == null || autoLoginStarted)
        {
            return;
        }

        if (session.IsLoggedIn)
        {
            autoLoginStarted = true;
            autoLoginFinished = true;
            autoLoginSucceeded = true;
            return;
        }

        if (!session.HasStoredSession)
        {
            return;
        }

        autoLoginStarted = true;
        int generation = ++autoLoginGeneration;
        session.TryAutoLogin(ok =>
        {
            if (generation != autoLoginGeneration)
            {
                return;
            }

            autoLoginSucceeded = ok;
            autoLoginFinished = true;
        });
    }

    private IEnumerator WaitForAutoLoginThenContinue()
    {
        while (!autoLoginFinished)
        {
            yield return null;
        }

        CompletePressToStart(autoLoginSucceeded);
    }

    private void CompletePressToStart(bool ok)
    {
        if (ok)
        {
            StartCoroutine(LoadAfterFade(WorldMapSceneName));
        }
        else
        {
            transitioning = false;
            acceptingInput = false;
            StartCoroutine(SwapPromptForAuthButtons());
        }
    }

    private void OnLogout()
    {
        if (transitioning)
        {
            return;
        }

        AuthSession.Instance?.Logout();
        autoLoginGeneration++;
        autoLoginStarted = false;
        autoLoginFinished = true;
        autoLoginSucceeded = false;
        acceptingInput = false;
        StartCoroutine(SwapPromptForAuthButtons());
    }

    private void LeaveTo(string sceneName)
    {
        if (transitioning)
        {
            return;
        }

        transitioning = true;
        authButtonsGroup.interactable = false;
        StartCoroutine(LoadAfterFade(sceneName));
    }

    private IEnumerator LoadAfterFade(string sceneName)
    {
        yield return StartCoroutine(SceneFadeController.Cover(SceneFadeDuration));
        SceneManager.LoadScene(sceneName);
    }

    // ---- prompt pulse ----

    private void AnimatePressToStart()
    {
        float phase = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / PromptPulsePeriod) + 1f) * 0.5f;
        pressToStartGroup.alpha = Mathf.Lerp(PromptMinimumAlpha, 1f, phase);

        float scale = Mathf.Lerp(PromptMinimumScale, PromptMaximumScale, phase);
        pressToStartGroup.transform.localScale = Vector3.one * scale;
    }

    // ---- input ----

    private static bool EnterPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (pointerOverUi)
        {
            return false;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    // ---- helpers ----

    private static void HideGroup(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static void EnableGroup(CanvasGroup group)
    {
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private static void ConfigurePlayer(VideoPlayer player, bool loop)
    {
        player.playOnAwake = false;
        player.isLooping = loop;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.renderMode = VideoRenderMode.RenderTexture;
    }
}
