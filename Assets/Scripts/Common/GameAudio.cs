using UnityEngine;
using UnityEngine.SceneManagement;

// Central game audio: BGM that survives scene loads with a seamless crossfade loop,
// plus one-shot and looping SFX channels. Singleton auto-created from
// Resources/GameAudio.prefab on first static access, so any scene (or direct editor
// play of a single scene) works without manual setup.
//
// Music is picked per scene: Golden Gleam for the title/auth/WorldMap flow, the main theme
// for forest exploration, and the quest theme for focused play. Scene mix levels keep music
// supportive instead of equally loud everywhere: puzzles sit low for speech, map/menu can
// breathe, cutscenes make room for VO/SFX.
public sealed class GameAudio : MonoBehaviour
{
    [Header("Music - tracks")]
    [Tooltip("Shared theme: first_page, Login, ForgotPassword, Register, WorldMap.")]
    [SerializeField] private AudioClip menuTheme;
    [SerializeField, Range(0f, 1f)] private float menuThemeVolume = 0.85f;
    [Tooltip("Forest/exploration theme: reference_forest.")]
    [SerializeField] private AudioClip mainTheme;
    [SerializeField, Range(0f, 1f)] private float mainThemeVolume = 0.85f;
    [Tooltip("Quest/cutscene theme: every scene NOT listed in Main Theme Scenes.")]
    [SerializeField] private AudioClip questTheme;
    [SerializeField, Range(0f, 1f)] private float questThemeVolume = 0.85f;
    [Tooltip("Scenes that use the forest/exploration theme. Title/auth/WorldMap scenes use Menu Theme first.")]
    [SerializeField] private string[] mainThemeScenes =
    {
        "reference_forest"
    };

    [Header("Music - scene mix")]
    [SerializeField, Range(0f, 1f)] private float menuMusicLevel = 0.82f;
    [SerializeField, Range(0f, 1f)] private float worldMapMusicLevel = 0.74f;
    [SerializeField, Range(0f, 1f)] private float nightWorldMapMusicLevel = 0.58f;
    [SerializeField, Range(0f, 1f)] private float forestMusicLevel = 0.52f;
    [SerializeField, Range(0f, 1f)] private float nightForestMusicLevel = 0.34f;
    [SerializeField, Range(0f, 1f)] private float questMapMusicLevel = 0.55f;
    [SerializeField, Range(0f, 1f)] private float cutsceneMusicLevel = 0.36f;
    [SerializeField, Range(0f, 1f)] private float learningMusicLevel = 0.24f;
    [SerializeField, Range(0f, 1f)] private float successMusicLevel = 0.40f;
    [SerializeField, Range(0f, 1f)] private float bossMusicLevel = 0.58f;
    [Tooltip("How quickly the music settles into a scene's mix level after a load.")]
    [SerializeField] private float sceneMixLerpSpeed = 2.2f;

    [Header("Music - fades")]
    [Tooltip("Crossfade overlap at the loop seam, seconds.")]
    [SerializeField] private float crossfadeSeconds = 1.4f;
    [Tooltip("Fade out the old track / fade in the new one when the scene's track changes.")]
    [SerializeField] private float trackSwitchFadeSeconds = 2f;
    [Tooltip("Fade used when a title/auth/WorldMap scene reloads the same shared theme from the beginning.")]
    [SerializeField] private float sameThemeRestartFadeSeconds = 1.2f;
    [Tooltip("Start Golden Gleam from the beginning each time first_page/Login/Register/ForgotPassword/WorldMap loads.")]
    [SerializeField] private bool restartSharedThemeOnSceneLoad = true;
    [Tooltip("Music fade-in when it first starts (WorldMap black reveal).")]
    [SerializeField] private float musicFadeInSeconds = 2.4f;

    [Header("Music - bridge ambience")]
    [Tooltip("Optional ambience clip that stays under title/auth/world transitions. If empty, a very soft generated pad is used.")]
    [SerializeField] private AudioClip bridgeAmbience;
    [SerializeField] private bool generateBridgeAmbience = true;
    [SerializeField, Range(0f, 1f)] private float bridgeAmbienceVolume = 0.045f;
    [SerializeField] private float bridgeAmbienceFadeSpeed = 0.65f;
    [SerializeField] private string[] bridgeAmbienceScenes =
    {
        "first_page", "Login", "Register", "ForgotPassword", "WorldMap", "reference_forest"
    };

    [Header("Music - ducking")]
    [Tooltip("Music volume multiplier while the owl is speaking, so the voice sits on top.")]
    [SerializeField] private float voiceDuck = 0.2f;
    [Tooltip("Brief music dip under important non-voice stingers such as quest enter / win / rock break.")]
    [SerializeField, Range(0f, 1f)] private float stingerDuck = 0.55f;
    [Tooltip("How fast the music ducks in/out when the owl starts/stops speaking (per second).")]
    [SerializeField] private float duckLerpSpeed = 4.5f;

    [Header("SFX clips")]
    [SerializeField] private AudioClip click;                 // click.mp3
    [SerializeField] private AudioClip unlockSting;           // unlock.wav
    [SerializeField] private AudioClip questEnter;            // click-quest.mp3
    [SerializeField] private AudioClip afterQuest;            // after-quest.mp3
    [SerializeField] private AudioClip winSting;              // win.wav
    [SerializeField] private AudioClip loseSting;             // lose.wav
    [SerializeField] private AudioClip bearRun;               // Bear.mp3 (loops)
    [SerializeField] private AudioClip bearRoar;              // bear-kamram.mp3
    [SerializeField] private AudioClip crowFly;               // Crow.mp3 (loops)
    [SerializeField] private AudioClip paaThrow;              // paa.wav (one shot)
    [SerializeField, Min(1f)] private float paaThrowGain = 1.5f;
    [SerializeField] private AudioClip rockBreak;             // rock-break.mp3 (one shot)
    [Tooltip("Perceived loudness boost for Crow.mp3. Applied once to a runtime copy with soft limiting, so all crow scenes stay equally loud without clipping.")]
    [SerializeField, Min(1f)] private float crowGain = 1.8f;
    [Header("Forest footsteps")]
    [Tooltip("Played in order while the reference_forest hero walks, then repeated from the first clip.")]
    [SerializeField] private AudioClip[] forestFootsteps;
    [SerializeField, Range(0f, 1f)] private float forestFootstepVolume = 0.85f;
    [SerializeField] private float sfxVolume = 1f;

    private static GameAudio instance;

    // Start the music in whatever scene the game launches into (so playing
    // reference_forest — or any scene — directly still has music), not only when
    // WorldMap's fade-in calls EnsureMusic. Runs once at startup; after that the
    // DontDestroyOnLoad singleton carries the music across scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => EnsureMusic();

    private AudioSource bgmA;
    private AudioSource bgmB;
    private AudioSource sfx;
    private AudioSource sfxLoop;
    private AudioSource footstepSource;
    private AudioSource bridgeAmbienceSource;
    private AudioListener ownListener; // fallback listener for scenes whose camera has none (e.g. reference_forest)
    private double nextLoopDspTime;     // when the idle source takes over
    private AudioSource activeBgm;      // the source currently mid-track
    private AudioSource pendingBgm;     // scheduled to start at nextLoopDspTime
    private bool voiceActive;           // owl currently speaking -> duck to voiceDuck
    private float speechDuckUntil;      // short-lived word/TTS playback duck
    private float stingerDuckUntil;     // short-lived SFX/stinger duck
    private float dampedDuck = 1f;      // smoothed duck actually applied (no hard volume jumps)
    private float sceneMusicTarget = 1f;
    private float dampedSceneMusic = 1f;
    private float fadeIn = 1f;          // 0->1 over musicFadeInSeconds at start
    private float fadeInElapsed;
    private bool musicStarted;
    private AudioClip currentTrack;     // clip loaded in bgmA/bgmB right now
    private AudioClip queuedTrack;      // what to start once the fade-out finishes
    private float menuResumeTime;       // saved playhead per track, seconds
    private float mainResumeTime;
    private float questResumeTime;
    private float trackFade;            // 0..1 track-switch fade multiplier
    private int fadeDir;                // -1 fading out, +1 fading in, 0 settled
    private bool queuedTrackStartsAtBeginning;
    private float activeTrackSwitchFadeSeconds;
    private float bridgeAmbienceTarget;
    private float dampedBridgeAmbience;
    private AudioClip amplifiedCrowFly;
    private AudioClip amplifiedPaaThrow;
    private AudioClip generatedBridgeAmbience;
    private Coroutine footstepRoutine;
    private int footstepIndex;

    private static GameAudio Instance
    {
        get
        {
            if (instance != null) return instance;
            GameAudio prefab = Resources.Load<GameAudio>("GameAudio");
            if (prefab == null)
            {
                Debug.LogWarning("[GameAudio] Resources/GameAudio.prefab missing");
                return null;
            }
            instance = Instantiate(prefab);
            instance.name = "GameAudio";
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }
    }

    // ---- public API ----
    public static void EnsureMusic()
    {
        GameAudio audio = Instance;
        if (audio != null) audio.StartForScene(SceneManager.GetActiveScene().name, false);
    }
    public static void PlayClick() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.click); }
    public static void PlayUnlock() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.unlockSting, 0.45f); }
    public static void PlayQuestEnter() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.questEnter, 0.85f); }
    public static float QuestEnterDuration
    {
        get
        {
            GameAudio audio = Instance;
            return audio != null && audio.questEnter != null ? audio.questEnter.length : 0f;
        }
    }
    public static void PlayAfterQuest() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.afterQuest, 0.75f); }
    public static void PlayWin() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.winSting, 1.0f); }
    public static void PlayLose() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.loseSting, 1.0f); }
    public static void PlayCrowLoop() { GameAudio audio = Instance; if (audio != null) audio.StartLoop(audio.GetCrowLoopClip()); }
    public static void StopSfxLoop() { GameAudio audio = Instance; if (audio != null) audio.StopLoop(); }
    public static void PlayPaaThrow() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.GetPaaThrowClip(), 0.8f); }
    public static void PlayRockBreak() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.rockBreak, 0.9f); }
    public static void StartForestFootsteps() { GameAudio audio = Instance; if (audio != null) audio.StartFootsteps(); }
    public static void StopForestFootsteps() { if (instance != null) instance.StopFootsteps(); }

    // Duck the music under the owl's voice. No spawn on a stray false call.
    public static void SetVoiceDucking(bool on) { if (instance != null) instance.voiceActive = on; }
    public static void DuckForSpeech(float seconds)
    {
        GameAudio audio = Instance;
        if (audio != null) audio.PushSpeechDuck(seconds);
    }
    public static void ClearSpeechDucking()
    {
        if (instance != null) instance.speechDuckUntil = 0f;
    }

    // Cutscene state -> sound. Called by BearCutscene as each step's state starts.
    public static void OnCutsceneState(string state)
    {
        GameAudio audio = Instance;
        if (audio == null) return;
        if (state == "bear_run2") { audio.StartLoop(audio.bearRun); return; }
        audio.StopLoop();
        if (state == "bear_jump") audio.PlayOneShot(audio.bearRoar);
    }

    // ---- lifecycle ----
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }

        bgmA = gameObject.AddComponent<AudioSource>();
        bgmB = gameObject.AddComponent<AudioSource>();
        sfx = gameObject.AddComponent<AudioSource>();
        sfxLoop = gameObject.AddComponent<AudioSource>();
        footstepSource = gameObject.AddComponent<AudioSource>();
        bridgeAmbienceSource = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { bgmA, bgmB, sfx, sfxLoop, footstepSource, bridgeAmbienceSource })
        {
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        sfxLoop.loop = true;
        bridgeAmbienceSource.loop = true;

        ownListener = gameObject.AddComponent<AudioListener>();
        ownListener.enabled = false;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureListener();
        StartForScene(SceneManager.GetActiveScene().name, false);
    }

    // Keep exactly one active AudioListener: use ours only when the loaded scene has none of
    // its own (some scenes — e.g. reference_forest — ship without one, which mutes all audio).
    private void EnsureListener()
    {
        if (ownListener == null) return;
        bool otherActive = false;
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (listener != ownListener && listener.isActiveAndEnabled) { otherActive = true; break; }
        }
        ownListener.enabled = !otherActive;
    }

    private void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopFootsteps();
        if (amplifiedCrowFly != null) Destroy(amplifiedCrowFly);
        if (amplifiedPaaThrow != null) Destroy(amplifiedPaaThrow);
        if (generatedBridgeAmbience != null) Destroy(generatedBridgeAmbience);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        voiceActive = false; // clear any voice duck left over from a scene that unloaded mid-speech
        speechDuckUntil = 0f;
        stingerDuckUntil = 0f;
        EnsureListener();
        StartForScene(scene.name, true);
    }

    // ---- music: scene-picked tracks, seamless same-track loop, restart where needed ----
    private AudioClip TrackForScene(string sceneName)
    {
        if (IsSharedThemeScene(sceneName))
        {
            return menuTheme != null ? menuTheme : mainTheme;
        }

        if (sceneName == "practice_night"
            || (sceneName == "reference_forest" && NightMode.RedoActive))
        {
            return questTheme;
        }

        return IsMainThemeScene(sceneName)
            ? mainTheme
            : questTheme;
    }

    private static bool IsSharedThemeScene(string sceneName)
    {
        return sceneName == "first_page"
            || sceneName == "Login"
            || sceneName == "Register"
            || sceneName == "ForgotPassword"
            || sceneName == "WorldMap";
    }

    private static bool IsTitleOrAuthScene(string sceneName)
    {
        return sceneName == "first_page"
            || sceneName == "Login"
            || sceneName == "Register"
            || sceneName == "ForgotPassword";
    }

    private bool IsMainThemeScene(string sceneName)
    {
        return mainThemeScenes != null && System.Array.IndexOf(mainThemeScenes, sceneName) >= 0;
    }

    private void StartForScene(string sceneName, bool sceneJustLoaded)
    {
        sceneMusicTarget = MusicLevelForScene(sceneName);
        ConfigureBridgeAmbience(sceneName);
        if (!musicStarted)
        {
            dampedSceneMusic = sceneMusicTarget;
        }

        bool restartFromBeginning = sceneJustLoaded
            && restartSharedThemeOnSceneLoad
            && IsSharedThemeScene(sceneName);
        BeginSwitch(TrackForScene(sceneName), restartFromBeginning);
    }

    private float MusicLevelForScene(string sceneName)
    {
        if (sceneName == "WorldMap")
        {
            return NightMode.NightPhase ? nightWorldMapMusicLevel : worldMapMusicLevel;
        }
        if (IsTitleOrAuthScene(sceneName))
        {
            return menuMusicLevel;
        }
        if (sceneName == "reference_forest")
        {
            return NightMode.NightPhase || NightMode.RedoActive ? nightForestMusicLevel : forestMusicLevel;
        }
        if (sceneName == "quest_map1")
        {
            return questMapMusicLevel;
        }
        if (sceneName == "practice_night" || sceneName.StartsWith("word_build"))
        {
            return learningMusicLevel;
        }
        if (sceneName.StartsWith("CutScene") || sceneName.StartsWith("cut_scene"))
        {
            return cutsceneMusicLevel;
        }
        if (sceneName.StartsWith("Success") || sceneName == "success")
        {
            return successMusicLevel;
        }
        if (sceneName == "boss" || sceneName == "before_Boss")
        {
            return bossMusicLevel;
        }

        return cutsceneMusicLevel;
    }

    private void BeginSwitch(AudioClip next, bool restartFromBeginning)
    {
        if (!musicStarted || currentTrack == null)
        {
            queuedTrack = next;
            queuedTrackStartsAtBeginning = restartFromBeginning;
            StartTrack(next, restartFromBeginning);
            return;
        }

        if (next == currentTrack)
        {
            if (restartFromBeginning)
            {
                SaveResumeTime();
                bgmA.Stop();
                bgmB.Stop();
                StartTrack(next, true);
                return;
            }

            // Same track: keep playing untouched. If a fade-out to some other track was
            // already under way (scene bounced back before it finished), reverse it.
            if (fadeDir < 0) fadeDir = 1;
            return;
        }

        queuedTrack = next;
        queuedTrackStartsAtBeginning = restartFromBeginning;
        activeTrackSwitchFadeSeconds = trackSwitchFadeSeconds;
        fadeDir = -1; // Update fades the old track out, then swaps in queuedTrack
    }

    private void StartTrack(AudioClip clip, bool startFromBeginning)
    {
        currentTrack = clip;
        trackFade = 0f;
        fadeDir = 0;
        activeTrackSwitchFadeSeconds = startFromBeginning ? sameThemeRestartFadeSeconds : trackSwitchFadeSeconds;

        if (clip == null) { musicStarted = false; return; }

        // Resume where this track left off. Too close to the seam and the loop maths
        // has no room for the crossfade, so start clean instead.
        float resume = startFromBeginning ? 0f : ResumeTimeForTrack(clip);
        if (resume >= clip.length - crossfadeSeconds || resume < 0f) resume = 0f;

        if (!musicStarted)
        {
            // first track of the session: also run the long initial fade-in
            fadeIn = musicFadeInSeconds > 0f ? 0f : 1f;
            fadeInElapsed = 0f;
            dampedDuck = 1f;
            dampedSceneMusic = sceneMusicTarget;
        }
        musicStarted = true;

        activeBgm = bgmA;
        pendingBgm = bgmB;
        bgmA.clip = clip;
        bgmB.clip = clip;
        bgmA.volume = 0f;
        bgmB.volume = 0f;
        bgmA.time = resume;
        bgmB.time = 0f;

        double now = AudioSettings.dspTime + 0.1;
        bgmA.PlayScheduled(now);
        nextLoopDspTime = now + clip.length - resume - crossfadeSeconds;
        pendingBgm.PlayScheduled(nextLoopDspTime);
        fadeDir = 1; // fade the new track in
    }

    private void SaveResumeTime()
    {
        if (currentTrack == null || activeBgm == null) return;
        if (currentTrack == menuTheme) menuResumeTime = activeBgm.time;
        else if (currentTrack == mainTheme) mainResumeTime = activeBgm.time;
        else if (currentTrack == questTheme) questResumeTime = activeBgm.time;
    }

    private float ResumeTimeForTrack(AudioClip clip)
    {
        if (clip == menuTheme) return menuResumeTime;
        if (clip == mainTheme) return mainResumeTime;
        if (clip == questTheme) return questResumeTime;
        return 0f;
    }

    private void ScheduleNextLoop(double trackStartDsp)
    {
        // The idle source starts the track from the top `crossfadeSeconds` before the
        // active one ends; Update crossfades their volumes over that overlap.
        nextLoopDspTime = trackStartDsp + currentTrack.length - crossfadeSeconds;
        pendingBgm.time = 0f;
        pendingBgm.PlayScheduled(nextLoopDspTime);
    }

    private void Update()
    {
        bool speechActive = voiceActive || Time.unscaledTime < speechDuckUntil;
        float targetDuck = speechActive
            ? voiceDuck
            : Time.unscaledTime < stingerDuckUntil
                ? stingerDuck
                : 1f;
        dampedDuck = Mathf.MoveTowards(dampedDuck, targetDuck, Mathf.Max(0.01f, duckLerpSpeed) * Time.unscaledDeltaTime);
        UpdateBridgeAmbience();

        if (!musicStarted || currentTrack == null) return;

        if (fadeIn < 1f)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            fadeIn = Mathf.Clamp01(fadeInElapsed / Mathf.Max(0.01f, musicFadeInSeconds));
        }

        if (fadeDir != 0)
        {
            float fadeSeconds = Mathf.Max(0.01f, activeTrackSwitchFadeSeconds);
            trackFade = Mathf.Clamp01(trackFade + fadeDir * Time.unscaledDeltaTime / fadeSeconds);
            if (fadeDir < 0 && trackFade <= 0f)
            {
                SaveResumeTime();
                bgmA.Stop();
                bgmB.Stop();
                StartTrack(queuedTrack, queuedTrackStartsAtBeginning);
                return; // next frame drives the freshly scheduled track
            }
            if (fadeDir > 0 && trackFade >= 1f) fadeDir = 0;
        }

        dampedSceneMusic = Mathf.MoveTowards(
            dampedSceneMusic,
            sceneMusicTarget,
            Mathf.Max(0.01f, sceneMixLerpSpeed) * Time.unscaledDeltaTime);
        // Read the volume every frame so dragging the slider in the Inspector at runtime is live.
        float trackVolume = VolumeForTrack(currentTrack);
        float target = trackVolume * dampedSceneMusic * dampedDuck * trackFade * fadeIn;
        double now = AudioSettings.dspTime;
        double intoCross = now - nextLoopDspTime; // <0 before the seam, 0..crossfade inside it

        if (intoCross >= 0d)
        {
            float t = Mathf.Clamp01((float)(intoCross / crossfadeSeconds));
            activeBgm.volume = target * (1f - t);
            pendingBgm.volume = target * t;

            if (t >= 1f)
            {
                // hand over: pending becomes active, old source re-arms for the next seam
                activeBgm.Stop();
                (activeBgm, pendingBgm) = (pendingBgm, activeBgm);
                ScheduleNextLoop(nextLoopDspTime); // pending started at nextLoopDspTime
            }
        }
        else
        {
            activeBgm.volume = target;
            pendingBgm.volume = 0f;
        }
    }

    private float VolumeForTrack(AudioClip clip)
    {
        if (clip == menuTheme) return menuThemeVolume;
        if (clip == questTheme) return questThemeVolume;
        return mainThemeVolume;
    }

    private void ConfigureBridgeAmbience(string sceneName)
    {
        bool shouldPlay = bridgeAmbienceVolume > 0f
            && bridgeAmbienceScenes != null
            && System.Array.IndexOf(bridgeAmbienceScenes, sceneName) >= 0;
        bridgeAmbienceTarget = shouldPlay ? bridgeAmbienceVolume : 0f;

        if (!shouldPlay || bridgeAmbienceSource == null || bridgeAmbienceSource.isPlaying) return;

        AudioClip clip = GetBridgeAmbienceClip();
        if (clip == null) return;
        bridgeAmbienceSource.clip = clip;
        bridgeAmbienceSource.volume = 0f;
        bridgeAmbienceSource.Play();
    }

    private void UpdateBridgeAmbience()
    {
        if (bridgeAmbienceSource == null) return;

        dampedBridgeAmbience = Mathf.MoveTowards(
            dampedBridgeAmbience,
            bridgeAmbienceTarget,
            Mathf.Max(0.01f, bridgeAmbienceFadeSpeed) * Time.unscaledDeltaTime);
        bridgeAmbienceSource.volume = dampedBridgeAmbience * dampedDuck;

        if (bridgeAmbienceTarget <= 0f && dampedBridgeAmbience <= 0.001f && bridgeAmbienceSource.isPlaying)
        {
            bridgeAmbienceSource.Stop();
        }
    }

    private AudioClip GetBridgeAmbienceClip()
    {
        if (bridgeAmbience != null) return bridgeAmbience;
        if (!generateBridgeAmbience) return null;
        if (generatedBridgeAmbience != null) return generatedBridgeAmbience;

        const int frequency = 44100;
        const int seconds = 8;
        const int channels = 2;
        int samples = frequency * seconds;
        float[] data = new float[samples * channels];
        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / frequency;
            float slowPulse = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * time / seconds);
            float pad =
                Mathf.Sin(2f * Mathf.PI * 55f * time) * 0.012f
                + Mathf.Sin(2f * Mathf.PI * 82.5f * time) * 0.008f
                + Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.004f;
            float sample = pad * slowPulse;
            data[i * channels] = sample;
            data[i * channels + 1] = sample * 0.92f;
        }

        generatedBridgeAmbience = AudioClip.Create("GeneratedBridgeAmbience", samples, channels, frequency, false);
        generatedBridgeAmbience.SetData(data, 0);
        return generatedBridgeAmbience;
    }

    // ---- sfx ----
    private void PlayOneShot(AudioClip clip, float duckSeconds = 0f)
    {
        if (duckSeconds > 0f) PushStingerDuck(duckSeconds);
        if (clip != null) sfx.PlayOneShot(clip, sfxVolume);
    }

    private void PushSpeechDuck(float seconds)
    {
        if (seconds <= 0f) return;
        speechDuckUntil = Mathf.Max(speechDuckUntil, Time.unscaledTime + seconds);
    }

    private void PushStingerDuck(float seconds)
    {
        if (seconds <= 0f) return;
        stingerDuckUntil = Mathf.Max(stingerDuckUntil, Time.unscaledTime + seconds);
    }

    private void StartLoop(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxLoop.isPlaying && sfxLoop.clip == clip) return; // already looping this
        sfxLoop.clip = clip;
        sfxLoop.volume = sfxVolume;
        sfxLoop.Play();
    }

    private AudioClip GetCrowLoopClip()
    {
        if (crowFly == null || crowGain <= 1.001f) return crowFly;
        if (amplifiedCrowFly != null) return amplifiedCrowFly;

        // AudioSource.volume is already at its safe maximum (1). Build a louder in-memory copy
        // instead, using a soft limiter so quiet parts rise while peaks remain inside [-1, 1].
        if (crowFly.loadState != AudioDataLoadState.Loaded && !crowFly.LoadAudioData())
        {
            Debug.LogWarning("[GameAudio] Could not load Crow.mp3 for gain boost; using the original clip.");
            return crowFly;
        }

        int sampleCount = crowFly.samples * crowFly.channels;
        float[] samples = new float[sampleCount];
        if (!crowFly.GetData(samples, 0))
        {
            Debug.LogWarning("[GameAudio] Crow.mp3 is not readable; using the original clip.");
            return crowFly;
        }

        double drive = Mathf.Max(1f, crowGain);
        double normalizer = System.Math.Tanh(drive);
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(System.Math.Tanh(samples[i] * drive) / normalizer);
        }

        amplifiedCrowFly = AudioClip.Create(
            crowFly.name + "_Louder",
            crowFly.samples,
            crowFly.channels,
            crowFly.frequency,
            false);
        amplifiedCrowFly.SetData(samples, 0);
        return amplifiedCrowFly;
    }

    private AudioClip GetPaaThrowClip()
    {
        if (paaThrow == null || paaThrowGain <= 1.001f) return paaThrow;
        if (amplifiedPaaThrow != null) return amplifiedPaaThrow;

        if (paaThrow.loadState != AudioDataLoadState.Loaded && !paaThrow.LoadAudioData())
        {
            Debug.LogWarning("[GameAudio] Could not load paa.wav for gain boost; using the original clip.");
            return paaThrow;
        }

        int sampleCount = paaThrow.samples * paaThrow.channels;
        float[] samples = new float[sampleCount];
        if (!paaThrow.GetData(samples, 0))
        {
            Debug.LogWarning("[GameAudio] paa.wav is not readable; using the original clip.");
            return paaThrow;
        }

        double drive = Mathf.Max(1f, paaThrowGain);
        double normalizer = System.Math.Tanh(drive);
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(System.Math.Tanh(samples[i] * drive) / normalizer);
        }

        amplifiedPaaThrow = AudioClip.Create(
            paaThrow.name + "_Louder",
            paaThrow.samples,
            paaThrow.channels,
            paaThrow.frequency,
            false);
        amplifiedPaaThrow.SetData(samples, 0);
        return amplifiedPaaThrow;
    }

    private void StopLoop()
    {
        if (sfxLoop.isPlaying) sfxLoop.Stop();
    }

    private void StartFootsteps()
    {
        if (footstepRoutine != null || forestFootsteps == null || forestFootsteps.Length == 0) return;
        footstepRoutine = StartCoroutine(FootstepSequence());
    }

    private void StopFootsteps()
    {
        if (footstepRoutine != null)
        {
            StopCoroutine(footstepRoutine);
            footstepRoutine = null;
        }

        if (footstepSource != null) footstepSource.Stop();
    }

    private System.Collections.IEnumerator FootstepSequence()
    {
        while (true)
        {
            AudioClip clip = forestFootsteps[footstepIndex % forestFootsteps.Length];
            footstepIndex = (footstepIndex + 1) % forestFootsteps.Length;

            if (clip == null)
            {
                yield return null;
                continue;
            }

            footstepSource.clip = clip;
            footstepSource.volume = sfxVolume * forestFootstepVolume;
            footstepSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
    }
}
