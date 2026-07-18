using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// first_page title screen: plays the intro forward then backward with a
/// 1x-2x-1x speed ramp, then loops the idle clip forward/backward forever at
/// normal speed until the player presses any key/click/tap, at which point it
/// fades to black and loads WorldMap.
/// </summary>
public sealed class FirstPageIntro : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const float MinSpeed = 1f;
    private const float MaxSpeed = 2f;
    private const float TextFadeDuration = 0.6f;
    private const float SceneFadeDuration = 0.6f;
    private const double FallbackClipLength = 5.0417; // seconds, matches the baked source clips

    [SerializeField] private VideoPlayer playerA;
    [SerializeField] private VideoPlayer playerB;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private CanvasGroup pressToStartGroup;
    [SerializeField] private VideoClip introClip;
    [SerializeField] private VideoClip introReverseClip;
    [SerializeField] private VideoClip idleClip;
    [SerializeField] private VideoClip idleReverseClip;

    private enum RampPhase
    {
        Forward,
        Reverse,
        None,
    }

    private VideoPlayer active;
    private VideoPlayer standby;
    private RampPhase ramp = RampPhase.Forward;
    private bool acceptingInput;
    private bool transitioning;

    private void Awake()
    {
        ConfigurePlayer(playerA);
        ConfigurePlayer(playerB);

        active = playerA;
        standby = playerB;

        pressToStartGroup.alpha = 0f;

        playerA.loopPointReached += HandleLoopPointReached;
        playerB.loopPointReached += HandleLoopPointReached;

        active.clip = introClip;
        active.prepareCompleted += OnFirstClipReady;
        active.Prepare();

        standby.clip = introReverseClip;
        standby.Prepare();
    }

    private void Update()
    {
        if (ramp != RampPhase.None)
        {
            double length = active.length > 0 ? active.length : FallbackClipLength;
            float t = Mathf.Clamp01((float)(active.time / length));
            active.playbackSpeed = ramp == RampPhase.Forward
                ? Mathf.Lerp(MinSpeed, MaxSpeed, t)
                : Mathf.Lerp(MaxSpeed, MinSpeed, t);
        }

        if (acceptingInput && !transitioning && InputPressedThisFrame())
        {
            transitioning = true;
            StartCoroutine(GoToWorldMap());
        }
    }

    private void OnFirstClipReady(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnFirstClipReady;
        videoSurface.texture = vp.targetTexture;
        vp.Play();
    }

    private void HandleLoopPointReached(VideoPlayer vp)
    {
        if (vp != active)
        {
            return;
        }

        VideoClip justFinished = active.clip;

        VideoPlayer finishedPlayer = active;
        active = standby;
        standby = finishedPlayer;

        videoSurface.texture = active.targetTexture;
        active.Play();

        standby.clip = NextIdleClipFor(justFinished);
        standby.Prepare();

        if (justFinished == introClip)
        {
            ramp = RampPhase.Reverse;
        }
        else if (justFinished == introReverseClip)
        {
            ramp = RampPhase.None;
            active.playbackSpeed = 1f;
            StartCoroutine(FadeInPressToStart());
        }
    }

    private VideoClip NextIdleClipFor(VideoClip justFinished)
    {
        bool wasReverseLeg = justFinished == introReverseClip || justFinished == idleReverseClip;
        return wasReverseLeg ? idleReverseClip : idleClip;
    }

    private IEnumerator FadeInPressToStart()
    {
        for (float t = 0f; t < TextFadeDuration; t += Time.unscaledDeltaTime)
        {
            pressToStartGroup.alpha = Mathf.Clamp01(t / TextFadeDuration);
            yield return null;
        }

        pressToStartGroup.alpha = 1f;
        acceptingInput = true;
    }

    private IEnumerator GoToWorldMap()
    {
        yield return StartCoroutine(SceneFadeController.Cover(SceneFadeDuration));
        SceneManager.LoadScene(WorldMapSceneName);
    }

    private static bool InputPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
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

    private static void ConfigurePlayer(VideoPlayer player)
    {
        player.playOnAwake = false;
        player.isLooping = false;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.renderMode = VideoRenderMode.RenderTexture;
    }
}
