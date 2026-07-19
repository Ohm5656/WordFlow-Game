# first_page Title Screen Video Intro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the empty `Assets/Scenes/first_page.unity` into the game's title screen: a speed-ramped forward/backward intro video, followed by a forward/backward idle loop that waits for a tap, then a fade to `WorldMap`.

**Architecture:** Four pre-baked mp4s (2 sources x forward/reverse) live in `Assets/Video/`. A single runtime component, `FirstPageIntro`, ping-pongs two `VideoPlayer`s — one playing, one `Prepare()`-ing the next clip — so clip swaps show no black frame, and drives `playbackSpeed` to ramp 1x→2x→1x during the intro only. An Editor script builds the scene (Canvas, RawImage, TMP text, the two VideoPlayers, `FirstPageIntro`) in one atomic pass and registers the scene in Build Settings at index 0.

**Tech Stack:** Unity Editor scripting (`UnityEditor`, `TMPro`), `UnityEngine.Video.VideoPlayer`, new Input System (`UnityEngine.InputSystem`), ffmpeg (video bake, one-time, already installed via `winget install Gyan.FFmpeg`).

## Status (updated 2026-07-19)

- [x] **Task 1 DONE** — all four clips baked and committed (`57853a5c3`). Verified 1918x1080 / 24fps / 5.042s / no audio. **BUT** the four `Assets/Video/*.mp4.meta` files are still untracked — commit them in Task 0 below.
- [x] **Task 2 DONE** — `FirstPageIntro.cs` committed (`8bc08861e`). The working tree has **uncommitted additions on top** (a `AnimatePressToStart()` pulse: alpha 0.55↔1 + scale 0.97↔1.03 on the prompt while idle). These additions are intentional — commit them as-is in Task 0, do **not** revert the file to the code block shown in Task 2. `FirstPageIntro.cs.meta` is also untracked.
- [x] **Task 0 DONE** (`cc148e773`).
- [x] **Task 3 DONE** (`25fb9dd7a`) — both RenderTextures created, throwaway script deleted.
- [x] **Task 4 DONE** (`658795b67`) — scene built, `first_page.unity` registered at build index 0, `Login.unity` still present at index 1.
- [x] **Task 5 DONE** (`3056f50a7`) — `Tools/FirstPage/Validate` passes; Play Mode verified.

**Deviation from Task 4's code, and why:** the builder as written left the prompt on
TMP's default `LiberationSans SDF`, so `"กดเพื่อเข้าเกม"` rendered as `□□□□□`. TMP resets a
freshly `AddComponent`-ed text back to the project default when it first initializes,
so the in-builder `text.font = font` never reached disk (adding `EditorUtility.SetDirty`
alone did not fix it). `BuildFirstPageScene.ApplyThaiFont` now reapplies the font on the
reopened scene and saves again — the same post-pass `ThaiTMPSetup.cs:62-87` already uses
for the Login scene. Verified: `first_page.unity` holds the LeelawUI guid
`96f1ad4e221207b4bb82f6c411d55078` twice and the LiberationSans guid zero times.

**Play Mode evidence (2026-07-19), read off the live components rather than screenshots:**
Game View captures come back solid white because the capture path renders through Main
Camera, which excludes Screen Space - Overlay canvases — that is a capture limitation,
not a scene defect.

- Intro forward leg: `playerA.clip = intro.mp4`, `isPlaying = true`, `time = 2.125` of
  `5.0417`, `playbackSpeed = 1.42` — matches `Lerp(1, 2, 0.42)` exactly, so the ramp is live.
- Sequence advanced through `intro_rev` to `idle.mp4` at `playbackSpeed = 1.0`, and
  `RawImage.texture` tracked the active player (`FirstPageIntroA` ↔ `FirstPageIntroB`),
  confirming the two-player ping-pong and the idle loop.
- `PressToStart` CanvasGroup alpha sampled at `0.927` then `0.627` — faded in and pulsing
  between the 0.55 and 1.0 bounds, so the idle phase is reached and input is live.
- `audioOutputMode = 0` (None) and `playOnAwake = false` on both players, as specified.

**Still unverified — needs the user:** the click → `SceneFadeController.Cover(0.6f)` →
`WorldMap` transition. No MCP tool can click inside a running Game View. Enter Play Mode on
`first_page`, wait for the Thai prompt, and click once.

**Decision confirmed with the user 2026-07-19:** the prompt text stays bottom-center (lower third), exactly as Task 4's builder places it. Do not move it to frame center.

### Task 0: Commit the leftovers from Tasks 1-2

- [ ] **Step 1: Commit pending metas and the pulse addition**

```bash
git add Assets/Video/intro.mp4.meta Assets/Video/intro_rev.mp4.meta \
        Assets/Video/idle.mp4.meta Assets/Video/idle_rev.mp4.meta \
        Assets/Scripts/FirstPage/FirstPageIntro.cs Assets/Scripts/FirstPage/FirstPageIntro.cs.meta
git commit -m "feat: add press-to-start pulse + missing video/script metas"
```

Do NOT `git add` anything under `Assets/Art/quest_map/bear/` — those are unrelated pending changes from other work.

---

## Global Constraints

- Source clips: 1918x1080, 24fps, 5.042s (121 frames), h264+aac. Both `b2d52762-3aa6-42a4-8e30-ca1d8d6830b4.mp4` (intro) and `c2288469-5694-438a-b88a-89109b82c3b6.mp4` (idle loop) share this format exactly.
- All baked clips are muted (`-an`); both runtime `VideoPlayer`s use `audioOutputMode = VideoAudioOutputMode.None`.
- Speed ramp: intro forward `Lerp(1, 2, t)`, intro reverse `Lerp(2, 1, t)`, where `t = active.time / active.length`. Idle loop always plays at speed `1`.
- No input is accepted until the idle loop begins (i.e., not during either intro leg).
- Text is `"กดเพื่อเข้าเกม"`, font `Assets/Fonts/LeelawUI SDF.asset` (already imported, used by existing WordFlow scenes), fades in over 0.6s on entering idle.
- Video surface fits "cover" (crop overflow, no letterbox) via `AspectRatioFitter` in `EnvelopeParent` mode — no custom fitting code.
- Transition to `WorldMap` on any key/click/tap: `SceneFadeController.Cover(0.6f)` (`Assets/Scripts/Common/SceneFadeController.cs`, already exists, do not modify) then `SceneManager.LoadScene("WorldMap")`.
- Input polling uses the new Input System (`Keyboard.current`, `Mouse.current`, `Touchscreen.current`) — this project's established convention (see `Assets/Scripts/Region1/ReferenceForest/QuestPathSequence.cs`), never the legacy `Input` class.
- This project has no unit test framework anywhere (no `*.Tests.asmdef` exists). Verification is via an Editor `Validate` menu item plus manual Play Mode observation — the established pattern for every other `Tools/*` editor script in this repo (e.g. `Assets/Editor/WordFlowLoginSceneBuilder.cs`).
- Unity scene edits made through MCP tools must happen inside **one** Editor script method that also calls `EditorSceneManager.SaveScene` — GameObjects created via ad hoc `execute_code` calls vanish on the next domain reload in this project's MCP setup. Invoke the finished script via `unity_execute_menu_item`, never `unity_execute_code`.
- Login/signup/auto-login UI is explicitly out of scope. `first_page` becomes build index 0; `Login.unity` stays registered in Build Settings, just pushed to a later index.

---

### Task 1: Bake the four video clips

**Files:**
- Create: `Assets/Video/intro.mp4`
- Create: `Assets/Video/intro_rev.mp4`
- Create: `Assets/Video/idle.mp4`
- Create: `Assets/Video/idle_rev.mp4`

**Interfaces:**
- Produces: four importable `VideoClip` assets at the paths above, each muted, each 121 frames / 24fps / 1918x1080. Task 4 (scene builder) loads these by exact path.

- [ ] **Step 1: Create the target directory**

```bash
mkdir -p "C:/Users/NTP/Documents/NSC-Game/Assets/Video"
```

- [ ] **Step 2: Bake `intro.mp4` and `intro_rev.mp4` from the intro source**

```bash
ffmpeg -y -i "C:/Users/NTP/Downloads/b2d52762-3aa6-42a4-8e30-ca1d8d6830b4.mp4" \
  -c:v libx264 -pix_fmt yuv420p -an \
  "C:/Users/NTP/Documents/NSC-Game/Assets/Video/intro.mp4"

ffmpeg -y -i "C:/Users/NTP/Downloads/b2d52762-3aa6-42a4-8e30-ca1d8d6830b4.mp4" \
  -vf reverse -c:v libx264 -pix_fmt yuv420p -an \
  "C:/Users/NTP/Documents/NSC-Game/Assets/Video/intro_rev.mp4"
```

Note: `-c:v copy` was considered for the forward copy but dropped — re-encoding
both with the same `libx264` settings keeps `intro.mp4` and `intro_rev.mp4`
frame-identical in codec/profile, which avoids any decoder hiccup when the
`FirstPageIntro` component swaps between them.

- [ ] **Step 3: Bake `idle.mp4` and `idle_rev.mp4` from the loop source**

```bash
ffmpeg -y -i "C:/Users/NTP/Downloads/c2288469-5694-438a-b88a-89109b82c3b6.mp4" \
  -c:v libx264 -pix_fmt yuv420p -an \
  "C:/Users/NTP/Documents/NSC-Game/Assets/Video/idle.mp4"

ffmpeg -y -i "C:/Users/NTP/Downloads/c2288469-5694-438a-b88a-89109b82c3b6.mp4" \
  -vf reverse -c:v libx264 -pix_fmt yuv420p -an \
  "C:/Users/NTP/Documents/NSC-Game/Assets/Video/idle_rev.mp4"
```

- [ ] **Step 4: Verify all four clips have identical frame counts and no audio stream**

```bash
for f in intro intro_rev idle idle_rev; do
  echo "=== $f ==="
  ffprobe -v error -select_streams v:0 -show_entries stream=nb_frames,width,height,r_frame_rate,codec_name -of default=nw=1 \
    "C:/Users/NTP/Documents/NSC-Game/Assets/Video/$f.mp4"
  ffprobe -v error -select_streams a -show_entries stream=codec_name -of default=nw=1 \
    "C:/Users/NTP/Documents/NSC-Game/Assets/Video/$f.mp4"
done
```

Expected: every file reports `nb_frames=121`, `width=1918`, `height=1080`,
`r_frame_rate=24/1`, `codec_name=h264` for the video stream, and prints
**nothing** for the audio-stream query (no audio stream present).

- [ ] **Step 5: Let Unity import the new clips**

Open the Unity Editor (or, if already open, wait for it to regain focus) so
`AssetDatabase` picks up the four new `.mp4` files under `Assets/Video/` and
imports each as a `VideoClip`. Confirm via:

```
unity_search_assets query="Assets/Video" type="VideoClip"
```

Expected: 4 results — `intro`, `intro_rev`, `idle`, `idle_rev`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Video/intro.mp4 Assets/Video/intro.mp4.meta \
        Assets/Video/intro_rev.mp4 Assets/Video/intro_rev.mp4.meta \
        Assets/Video/idle.mp4 Assets/Video/idle.mp4.meta \
        Assets/Video/idle_rev.mp4 Assets/Video/idle_rev.mp4.meta
git commit -m "feat: bake first_page intro/idle video clips (forward + reversed)"
```

---

### Task 2: `FirstPageIntro` runtime component

**Files:**
- Create: `Assets/Scripts/FirstPage/FirstPageIntro.cs`

**Interfaces:**
- Consumes: `SceneFadeController.Cover(float duration)` (static coroutine,
  `Assets/Scripts/Common/SceneFadeController.cs:79`) — starts a black fade-out
  and leaves the overlay opaque when done.
- Produces: a `MonoBehaviour` with these serialized fields, all wired by the
  Task 4 scene builder via `SerializedObject`:
  - `VideoPlayer playerA`
  - `VideoPlayer playerB`
  - `RawImage videoSurface`
  - `CanvasGroup pressToStartGroup`
  - `VideoClip introClip`
  - `VideoClip introReverseClip`
  - `VideoClip idleClip`
  - `VideoClip idleReverseClip`

  Each `VideoPlayer`'s `targetTexture` is expected to already be set (by the
  scene builder) to its own dedicated `RenderTexture` before the scene runs;
  `FirstPageIntro` never assigns `targetTexture` itself, only reads it via
  `active.targetTexture`.

No scene exists yet to run this in (Task 4 builds it), so this task is
implemented and reviewed by reading, not by a Play Mode run — Task 5 is where
the whole thing is exercised end-to-end in the Editor.

- [ ] **Step 1: Create the folder and write the component**

```bash
mkdir -p "C:/Users/NTP/Documents/NSC-Game/Assets/Scripts/FirstPage"
```

`Assets/Scripts/FirstPage/FirstPageIntro.cs`:

```csharp
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
```

- [ ] **Step 2: Confirm it compiles**

```
unity_get_compilation_errors
```

Expected: no errors referencing `FirstPageIntro.cs`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/FirstPage/FirstPageIntro.cs Assets/Scripts/FirstPage/FirstPageIntro.cs.meta
git commit -m "feat: add FirstPageIntro ping-pong video state machine"
```

---

### Task 3: RenderTexture assets

**Files:**
- Create: `Assets/Video/FirstPageIntroA.renderTexture`
- Create: `Assets/Video/FirstPageIntroB.renderTexture`

**Interfaces:**
- Produces: two 1920x1080 `RenderTexture` assets. Task 4's scene builder loads
  them by these exact paths and assigns one to each `VideoPlayer.targetTexture`.

Two separate textures (not one shared texture) are used deliberately: a
`VideoPlayer` that has finished `Prepare()`-ing renders its first frame to its
target texture even before `Play()` is called. If both players shared one
texture, the standby player's `Prepare()` could stomp the currently-visible
frame. Two textures make that impossible.

- [ ] **Step 1: Write a throwaway Editor script to create both assets**

`Assets/Editor/CreateFirstPageRenderTextures.cs` (temporary — deleted in Step
3 of this task once the assets exist, since nothing needs to recreate them
after this one-time run):

```csharp
using UnityEditor;
using UnityEngine;

public static class CreateFirstPageRenderTextures
{
    [MenuItem("Tools/FirstPage/Create RenderTextures (one-time)")]
    public static void Create()
    {
        CreateOne("Assets/Video/FirstPageIntroA.renderTexture");
        CreateOne("Assets/Video/FirstPageIntroB.renderTexture");
        AssetDatabase.SaveAssets();
        Debug.Log("[FirstPage] RenderTextures created.");
    }

    private static void CreateOne(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<RenderTexture>(path) != null)
        {
            return;
        }

        var rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path),
        };
        AssetDatabase.CreateAsset(rt, path);
    }
}
```

- [ ] **Step 2: Run it**

```
unity_execute_menu_item menu_path="Tools/FirstPage/Create RenderTextures (one-time)"
unity_search_assets query="Assets/Video" type="RenderTexture"
```

Expected: 2 results, `FirstPageIntroA` and `FirstPageIntroB`.

- [ ] **Step 3: Delete the throwaway script**

```bash
rm "C:/Users/NTP/Documents/NSC-Game/Assets/Editor/CreateFirstPageRenderTextures.cs" \
   "C:/Users/NTP/Documents/NSC-Game/Assets/Editor/CreateFirstPageRenderTextures.cs.meta"
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Video/FirstPageIntroA.renderTexture Assets/Video/FirstPageIntroA.renderTexture.meta \
        Assets/Video/FirstPageIntroB.renderTexture Assets/Video/FirstPageIntroB.renderTexture.meta
git commit -m "feat: add first_page ping-pong RenderTextures"
```

---

### Task 4: Scene builder — assemble `first_page.unity`

**Files:**
- Create: `Assets/Editor/BuildFirstPageScene.cs`
- Modify: `Assets/Scenes/first_page.unity` (rebuilt by the script, not hand-edited)
- Modify: `ProjectSettings/EditorBuildSettings.asset` (via `EditorBuildSettings.scenes`, not hand-edited)

**Interfaces:**
- Consumes: `FirstPageIntro` (Task 2) serialized field names exactly as listed
  in Task 2's Interfaces block; `Assets/Video/{intro,intro_rev,idle,idle_rev}.mp4`
  (Task 1); `Assets/Video/FirstPageIntro{A,B}.renderTexture` (Task 3);
  `Assets/Fonts/LeelawUI SDF.asset` (existing project asset).
- Produces: the fully-wired `first_page` scene, saved to disk, registered in
  `EditorBuildSettings.scenes` at index 0.

Everything happens inside one `[MenuItem]` method so the whole scene graph is
created and saved atomically in a single editor-script run — GameObjects
created via separate ad hoc calls do not survive a domain reload in this
project's Unity MCP setup, so building it any other way silently loses work.

- [ ] **Step 1: Write the builder**

`Assets/Editor/BuildFirstPageScene.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// One-click builder for the first_page title screen scene. Idempotent:
/// re-running rebuilds the scene from scratch and re-registers it in
/// EditorBuildSettings at index 0. Run via Tools > FirstPage > Build Scene.
/// </summary>
public static class BuildFirstPageScene
{
    private const string ScenePath = "Assets/Scenes/first_page.unity";
    private const string ThaiFontPath = "Assets/Fonts/LeelawUI SDF.asset";

    [MenuItem("Tools/FirstPage/Build Scene")]
    public static void Build()
    {
        VideoClip introClip = LoadClip("Assets/Video/intro.mp4");
        VideoClip introReverseClip = LoadClip("Assets/Video/intro_rev.mp4");
        VideoClip idleClip = LoadClip("Assets/Video/idle.mp4");
        VideoClip idleReverseClip = LoadClip("Assets/Video/idle_rev.mp4");
        RenderTexture rtA = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroA.renderTexture");
        RenderTexture rtB = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroB.renderTexture");
        TMP_FontAsset thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontPath);

        if (introClip == null || introReverseClip == null || idleClip == null || idleReverseClip == null
            || rtA == null || rtB == null || thaiFont == null)
        {
            Debug.LogError("[FirstPage] Missing a required asset — run Task 1-3 first. Aborting.");
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RawImage videoSurface = CreateVideoSurface(canvas.transform);
        CanvasGroup pressToStartGroup = CreatePressToStart(canvas.transform, thaiFont);

        var playersGO = new GameObject("FirstPageIntro");
        var playerA = playersGO.AddComponent<VideoPlayer>();
        var playerB = playersGO.AddComponent<VideoPlayer>();
        playerA.targetTexture = rtA;
        playerB.targetTexture = rtB;

        var intro = playersGO.AddComponent<FirstPageIntro>();
        var so = new SerializedObject(intro);
        SetRef(so, "playerA", playerA);
        SetRef(so, "playerB", playerB);
        SetRef(so, "videoSurface", videoSurface);
        SetRef(so, "pressToStartGroup", pressToStartGroup);
        SetRef(so, "introClip", introClip);
        SetRef(so, "introReverseClip", introReverseClip);
        SetRef(so, "idleClip", idleClip);
        SetRef(so, "idleReverseClip", idleReverseClip);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        RegisterAtIndexZero(ScenePath);

        Debug.Log("[FirstPage] Scene built and registered at build index 0.");
    }

    private static RawImage CreateVideoSurface(Transform canvasTransform)
    {
        var go = new GameObject("VideoSurface", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1920, 1080);

        var fitter = go.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1920f / 1080f;

        var image = go.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;

        return image;
    }

    private static CanvasGroup CreatePressToStart(Transform canvasTransform, TMP_FontAsset font)
    {
        var go = new GameObject("PressToStart", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 90f);
        rt.sizeDelta = new Vector2(700, 70);

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = "กดเพื่อเข้าเกม";
        text.font = font;
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return group;
    }

    private static VideoClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        if (clip == null)
        {
            Debug.LogError("[FirstPage] Missing VideoClip at " + path);
        }

        return clip;
    }

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null)
        {
            Debug.LogWarning("[FirstPage] missing serialized property: " + prop);
            return;
        }

        p.objectReferenceValue = value;
    }

    private static void RegisterAtIndexZero(string path)
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        existing.RemoveAll(s => s.path == path);
        existing.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = existing.ToArray();
    }
}
```

- [ ] **Step 2: Run the builder**

```
unity_execute_menu_item menu_path="Tools/FirstPage/Build Scene"
```

Expected console output: `[FirstPage] Scene built and registered at build index 0.`
with no preceding `LogError`.

- [ ] **Step 3: Verify Build Settings**

```
unity_get_compilation_errors
```

Then inspect `ProjectSettings/EditorBuildSettings.asset` (read the file):
expected `m_Scenes[0].path == Assets/Scenes/first_page.unity` and
`Assets/Scenes/Login.unity` still present later in the list.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/BuildFirstPageScene.cs Assets/Scenes/first_page.unity \
        ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: build first_page title screen scene"
```

---

### Task 5: Validate menu item + manual Play Mode verification

**Files:**
- Create: `Assets/Editor/ValidateFirstPageScene.cs`

**Interfaces:**
- Consumes: the built `first_page` scene (Task 4), `FirstPageIntro`'s
  serialized fields (Task 2).
- Produces: `Tools/FirstPage/Validate` menu item, an automated sanity check
  runnable any time after this task without opening Play Mode.

- [ ] **Step 1: Write the validator**

`Assets/Editor/ValidateFirstPageScene.cs`:

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class ValidateFirstPageScene
{
    [MenuItem("Tools/FirstPage/Validate")]
    public static void Validate()
    {
        bool ok = true;
        ok &= CheckClipPair("Assets/Video/intro.mp4", "Assets/Video/intro_rev.mp4");
        ok &= CheckClipPair("Assets/Video/idle.mp4", "Assets/Video/idle_rev.mp4");
        ok &= CheckBuildSettings();
        ok &= CheckSceneWiring();

        Debug.Log(ok ? "[FirstPage] Validate: ALL CHECKS PASSED" : "[FirstPage] Validate: FAILED — see errors above");
    }

    private static bool CheckClipPair(string forwardPath, string reversePath)
    {
        var forward = AssetDatabase.LoadAssetAtPath<VideoClip>(forwardPath);
        var reverse = AssetDatabase.LoadAssetAtPath<VideoClip>(reversePath);

        if (forward == null || reverse == null)
        {
            Debug.LogError($"[FirstPage] Missing clip: {forwardPath} or {reversePath}");
            return false;
        }

        if (forward.frameCount != reverse.frameCount)
        {
            Debug.LogError($"[FirstPage] Frame count mismatch: {forwardPath} ({forward.frameCount}) vs {reversePath} ({reverse.frameCount})");
            return false;
        }

        return true;
    }

    private static bool CheckBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length == 0 || scenes[0].path != "Assets/Scenes/first_page.unity" || !scenes[0].enabled)
        {
            Debug.LogError("[FirstPage] first_page.unity is not enabled at Build Settings index 0");
            return false;
        }

        return true;
    }

    private static bool CheckSceneWiring()
    {
        bool wasOpen = SceneManager.GetSceneByPath("Assets/Scenes/first_page.unity").isLoaded;
        var scene = wasOpen
            ? SceneManager.GetSceneByPath("Assets/Scenes/first_page.unity")
            : EditorSceneManager.OpenScene("Assets/Scenes/first_page.unity", OpenSceneMode.Additive);

        var intro = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<FirstPageIntro>(true))
            .FirstOrDefault();

        bool ok = intro != null;
        if (!ok)
        {
            Debug.LogError("[FirstPage] No FirstPageIntro component found in first_page scene");
        }
        else
        {
            var so = new SerializedObject(intro);
            foreach (var field in new[] { "playerA", "playerB", "videoSurface", "pressToStartGroup", "introClip", "introReverseClip", "idleClip", "idleReverseClip" })
            {
                var p = so.FindProperty(field);
                if (p == null || p.objectReferenceValue == null)
                {
                    Debug.LogError($"[FirstPage] FirstPageIntro.{field} is unassigned");
                    ok = false;
                }
            }
        }

        if (!wasOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return ok;
    }
}
```

Note: `EditorSceneManager` requires `using UnityEditor.SceneManagement;` — add
it alongside the other usings at the top of the file.

- [ ] **Step 2: Run it**

```
unity_execute_menu_item menu_path="Tools/FirstPage/Validate"
```

Expected: `[FirstPage] Validate: ALL CHECKS PASSED` with no `LogError` lines
above it.

- [ ] **Step 3: Manual Play Mode check**

```
unity_scene_open scene_path="Assets/Scenes/first_page.unity"
unity_play_mode action="play"
```

Wait a few seconds, then:

```
unity_screenshot_game
```

(Per this project's known MCP behavior, the screenshot can land up to ~75s
late — if the first capture looks stale, request another rather than assuming
failure.)

Confirm across 2-3 screenshots taken a couple seconds apart:
1. The intro video is visible full-screen with no black bars or letterboxing.
2. After ~7s the same footage is visibly playing in reverse (compare a
   screenshot from the forward leg against one from the reverse leg — they
   should show mirrored progress through the same scene).
3. Once the idle loop starts, `"กดเพื่อเข้าเกม"` fades in near the bottom-center
   of the frame.

There is no MCP tool to simulate a mouse click inside a running Game view, so
the click itself is a manual step: ask the user to click in the Game view once
the prompt text is visible. Confirm the result with:

```
unity_scene_info
```

Expected: the screen fades to black and the active scene becomes `WorldMap`.

- [ ] **Step 4: Stop Play Mode and commit**

```
unity_play_mode action="stop"
```

```bash
git add Assets/Editor/ValidateFirstPageScene.cs
git commit -m "feat: add first_page validation menu item"
```
