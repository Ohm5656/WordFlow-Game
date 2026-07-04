using UnityEngine;
using UnityEngine.SceneManagement;

// Central game audio: BGM that survives scene loads with a seamless crossfade loop,
// plus one-shot and looping SFX channels. Singleton auto-created from
// Resources/GameAudio.prefab on first static access, so any scene (or direct editor
// play of a single scene) works without manual setup.
public sealed class GameAudio : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string LoginSceneName = "Login";

    [Header("Music")]
    [SerializeField] private AudioClip music;                 // Golden Gleam.ogg
    [Tooltip("Master music volume (played at full in WorldMap).")]
    [SerializeField] private float musicVolume = 0.85f;
    [Tooltip("Music volume multiplier in every scene EXCEPT WorldMap (a touch quieter so it doesn't dominate).")]
    [SerializeField] private float otherSceneDuck = 0.7f;
    [Tooltip("Music volume multiplier while the owl is speaking, so the voice sits on top.")]
    [SerializeField] private float voiceDuck = 0.2f;
    [Tooltip("How fast the music ducks in/out when the owl starts/stops speaking (per second).")]
    [SerializeField] private float duckLerpSpeed = 2.5f;
    [Tooltip("Crossfade overlap at the loop seam, seconds.")]
    [SerializeField] private float crossfadeSeconds = 2f;
    [Tooltip("Music fade-in when it first starts (WorldMap black reveal).")]
    [SerializeField] private float musicFadeInSeconds = 3.2f;

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
    // WorldMap's fade-in calls EnsureMusic. Skips the Login scene. Runs once at startup;
    // after that the DontDestroyOnLoad singleton carries the music across scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name == LoginSceneName) return;
        EnsureMusic();
    }

    private AudioSource bgmA;
    private AudioSource bgmB;
    private AudioSource sfx;
    private AudioSource sfxLoop;
    private AudioSource footstepSource;
    private AudioListener ownListener; // fallback listener for scenes whose camera has none (e.g. reference_forest)
    private double nextLoopDspTime;     // when the idle source takes over
    private AudioSource activeBgm;      // the source currently mid-track
    private AudioSource pendingBgm;     // scheduled to start at nextLoopDspTime
    private float duck = 1f;            // scene duck: 1 in WorldMap, otherSceneDuck elsewhere
    private bool voiceActive;           // owl currently speaking -> duck further to voiceDuck
    private float dampedDuck = 1f;      // smoothed duck actually applied (no hard volume jumps)
    private float fadeIn = 1f;          // 0->1 over musicFadeInSeconds at start
    private float fadeInElapsed;
    private bool musicStarted;
    private AudioClip amplifiedCrowFly;
    private AudioClip amplifiedPaaThrow;
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
    public static void EnsureMusic() { GameAudio audio = Instance; if (audio != null) audio.StartMusic(); }
    public static void PlayClick() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.click); }
    public static void PlayUnlock() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.unlockSting); }
    public static void PlayQuestEnter() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.questEnter); }
    public static float QuestEnterDuration
    {
        get
        {
            GameAudio audio = Instance;
            return audio != null && audio.questEnter != null ? audio.questEnter.length : 0f;
        }
    }
    public static void PlayAfterQuest() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.afterQuest); }
    public static void PlayWin() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.winSting); }
    public static void PlayLose() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.loseSting); }
    public static void PlayCrowLoop() { GameAudio audio = Instance; if (audio != null) audio.StartLoop(audio.GetCrowLoopClip()); }
    public static void StopSfxLoop() { GameAudio audio = Instance; if (audio != null) audio.StopLoop(); }
    public static void PlayPaaThrow() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.GetPaaThrowClip()); }
    public static void PlayRockBreak() { GameAudio audio = Instance; if (audio != null) audio.PlayOneShot(audio.rockBreak); }
    public static void StartForestFootsteps() { GameAudio audio = Instance; if (audio != null) audio.StartFootsteps(); }
    public static void StopForestFootsteps() { if (instance != null) instance.StopFootsteps(); }

    // Duck the music under the owl's voice. No spawn on a stray false call.
    public static void SetVoiceDucking(bool on) { if (instance != null) instance.voiceActive = on; }

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
        foreach (AudioSource source in new[] { bgmA, bgmB, sfx, sfxLoop, footstepSource })
        {
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        sfxLoop.loop = true;

        ownListener = gameObject.AddComponent<AudioListener>();
        ownListener.enabled = false;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyDuckForScene(SceneManager.GetActiveScene().name);
        EnsureListener();
        StartMusic();
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
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyDuckForScene(scene.name);
        voiceActive = false; // clear any voice duck left over from a scene that unloaded mid-speech
        EnsureListener();
    }

    private void ApplyDuckForScene(string sceneName)
    {
        // Full volume in WorldMap; every other scene a touch quieter. SFX are unaffected.
        duck = sceneName == WorldMapSceneName ? 1f : otherSceneDuck;
    }

    // ---- music: seamless crossfade loop ----
    private void StartMusic()
    {
        if (musicStarted || music == null) return;
        musicStarted = true;
        fadeIn = musicFadeInSeconds > 0f ? 0f : 1f;
        fadeInElapsed = 0f;
        dampedDuck = duck; // start at the scene's duck, don't lerp from 1

        activeBgm = bgmA;
        pendingBgm = bgmB;
        bgmA.clip = music;
        bgmB.clip = music;
        double now = AudioSettings.dspTime + 0.1;
        bgmA.PlayScheduled(now);
        ScheduleNextLoop(now);
    }

    private void ScheduleNextLoop(double trackStartDsp)
    {
        // The idle source starts the track from the top `crossfadeSeconds` before the
        // active one ends; Update crossfades their volumes over that overlap.
        double loopPoint = trackStartDsp + music.length - crossfadeSeconds;
        nextLoopDspTime = loopPoint;
        pendingBgm.PlayScheduled(loopPoint);
    }

    private void Update()
    {
        if (!musicStarted) return;

        if (fadeIn < 1f)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            fadeIn = Mathf.Clamp01(fadeInElapsed / Mathf.Max(0.01f, musicFadeInSeconds));
        }

        float targetDuck = voiceActive ? Mathf.Min(duck, voiceDuck) : duck;
        dampedDuck = Mathf.MoveTowards(dampedDuck, targetDuck, Mathf.Max(0.01f, duckLerpSpeed) * Time.unscaledDeltaTime);
        float target = musicVolume * dampedDuck * fadeIn;
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

    // ---- sfx ----
    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null) sfx.PlayOneShot(clip, sfxVolume);
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
