# first_page Auth UI + Logo Drop + Shortened Intro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the working `first_page` title screen with: a shortened intro (reverse leg only, 2x→1x), a WordFlow logo that drop-bounces in during that leg then sparkle-loops forever, and session-aware UI that floats up when the idle loop starts — Login/Sign-up buttons for new players, press-to-start + logout for returning players (with silent TryAutoLogin before entering WorldMap).

**Architecture:** `FirstPageIntro` stays the single scene state machine, rewritten for the new phases. A tiny `UISpriteLoop` component cycles the 39 cropped logo frames on a UGUI Image (no Animator). `AuthSession` + `FirebaseAuthClient` get their own GameObject in `first_page` (same singleton pattern as the Login scene; duplicates self-destroy). All scene assembly stays inside the one idempotent `BuildFirstPageScene.Build()` menu item.

**Tech Stack:** Unity 6000.4.3f1, UGUI + TMP (LeelawUI SDF), new Input System, `UnityEngine.Video`, Python + PIL (one-off art crop), Unity MCP (`execute_menu_item`).

## Decisions confirmed with the user (2026-07-19)

1. Press-to-start (stored session) runs `AuthSession.TryAutoLogin` first; on success → WorldMap, on failure → swap UI to the Login/Sign-up buttons.
2. Logout button shows **only** when a stored session exists; pressing it clears the session and swaps the UI to Login/Sign-up in place (stays in first_page).
3. The forward intro leg (1x→2x) is cut entirely. The game opens directly on `intro_rev` at 2x easing to 1x (~3.5 s). `Assets/Video/intro.mp4` is deleted.
4. After the drop-bounce, the logo's 39-frame sparkle animation loops forever.

## Status — all tasks DONE (2026-07-19)

- [x] Task 1 (`4d5d66dc5`) — crop art assets
- [x] Task 2 (`1a8cf6739`) — runtime code
- [x] Task 3 (`68b809a5b`) — scene builder + validator, scene rebuilt, `Tools/FirstPage/Validate` passes
- [x] Task 4 — Play Mode verified, both session branches

### Deviation from Task 1: alpha threshold instead of plain bbox

The planned `getbbox()` crop was useless — it returned nearly the full 1920x1080 frame
because both the logo frames and the button art carry near-invisible glow/sparkle dust
out to the edges. Cropping now thresholds alpha at 32 (12% opacity) before taking the
bounding box, which drops only dust that cannot be seen:

- logo union bbox `(276, 246, 1728, 800)` → **1452x554** per frame, 39 frames
- `login_btn.png` → 975x338, `signup_btn.png` → 962x327

Verified the buttons keep clean edges by compositing over a sky-blue background — no grey
halo. (Viewing the raw PNG is misleading: transparent pixels carry grey RGB, so a viewer
that flattens alpha shows a grey box that does not exist once composited.)

### Play Mode evidence

MCP reads race the play-mode transition (instance IDs and component values come back
edit-mode until the transition settles, and the resource layer reports
`playmode_transition` inconsistently), so verification used temporary `[VERIFY]`
`Debug.Log` lines at the phase transitions instead — console output is timing-independent.
The logs were removed after the run; `FirstPageIntro.cs` has no net diff from them.

Fresh player (no stored session):

```
[VERIFY] intro leg done, endSpeed=1.000 logoAlpha=1.00 logoY=-235.0 hasSession=False
[VERIFY] fresh branch: auth=1.00 interactable=True prompt=0.00 logout=0.00
```

Returning player (stored session restored):

```
[VERIFY] intro leg done, endSpeed=1.000 logoAlpha=1.00 logoY=-235.0 hasSession=True
[VERIFY] session branch: prompt=1.00 logout=1.00 auth=0.00 acceptingInput=True
```

That confirms: the 2x→1x ramp lands exactly on 1.000; the logo faded to alpha 1 and the
drop-bounce settled on its rest Y of -235; and each branch reveals only its own UI.
Separately confirmed from live component reads: `Logo` Image cycling mid-sequence
(`0288.png`) with all 39 frames wired at 24 fps, and the idle ping-pong running
(`idle_rev.mp4` prepared on standby while `idle.mp4` plays).

**Note for future test runs:** a stray click in the Game View while the logout button is
visible calls `AuthSession.Logout()` and wipes the real stored session. It happened during
this run; the token was restored from a `reg export` backup of
`HKCU\Software\Unity\UnityEditor\NSC\WordFlow`. Back that key up before Play Mode testing.

### Still unverified — needs the user (no MCP tool can click the Game View)

- Login button → fade → Login scene, form shows immediately (no auto-login).
- Sign-up button → Register scene.
- With session: press any key → TryAutoLogin → WorldMap.
- Logout button → session UI swaps to Login/Sign-up in place.
- WorldMap: the red "Logout (dev)" IMGUI button is gone.

## Global Constraints

- Art sources (verified 2026-07-19): both button PNGs are 1536x1024 RGBA with real alpha (button art centered, transparent margins + soft glow); logo frames are `Assets/Art/login/WordFlow_logo/0282.png`–`0320.png`, 39 frames, 1920x1080 RGBA, transparent, ~24 fps render.
- Existing working pieces to **not** break: two-VideoPlayer ping-pong (no black frame at clip swaps), `AspectRatioFitter` EnvelopeParent cover-fit, Thai font post-pass (`ApplyThaiFont` — TMP resets a freshly AddComponent-ed text to the default font, so fonts must be reapplied on the reopened scene after the first save), build index 0 registration.
- `AuthSession.HasStoredSession` reads PlayerPrefs key `wf_refresh_token`; `AuthSession` is a DontDestroyOnLoad singleton whose `Awake` destroys duplicates — adding one to first_page is safe alongside the existing ones in Login/Register/ForgotPassword. `FirebaseAuthClient.apiKey` has a safe default in code; no config copying needed.
- Scene names: `Login`, `Register`, `WorldMap` (all registered in Build Settings).
- Input: new Input System only (`Keyboard.current` etc.). UI buttons additionally need an `EventSystem` + `InputSystemUIInputModule` in the scene — first_page currently has **none**; the builder must add it.
- Press-to-start polling must ignore pointer presses over UI (`EventSystem.current.IsPointerOverGameObject()`) so clicking Logout doesn't also trigger enter-game.
- Unity scene edits via MCP: everything inside one `[MenuItem]` method ending in `EditorSceneManager.SaveScene`; invoke via `execute_menu_item`; never ad hoc `execute_code`.
- After every script change: `refresh_unity` (force, compile) then check compilation errors before running menu items.
- Do NOT `git add` anything under `Assets/Art/quest_map/bear/`, `Assets/Scenes/WorldMap.unity`, `Assets/Scenes/region 1/` — unrelated pending changes from other work.

---

### Task 1: Crop art assets

**Files:**
- Create: `Assets/Art/login/WordFlow_logo_cropped/*.png` (39 frames, union-bbox crop)
- Create: `Assets/Art/login/login_btn.png`, `Assets/Art/login/signup_btn.png` (per-image bbox crop)

**Interfaces:**
- Produces: importable PNGs at the exact paths above. Task 3's builder loads `login_btn.png` / `signup_btn.png` as Sprites and every PNG in `WordFlow_logo_cropped/` (sorted ordinal) as the logo frames.

- [ ] **Step 1: Run the crop script** (PIL is available in the system Python)

Save nothing — run inline via a heredoc from the repo root:

```bash
python - <<'EOF'
from PIL import Image
import glob, os

def crop(src, dst, pad=8, bbox=None):
    im = Image.open(src).convert("RGBA")
    b = bbox or im.split()[3].getbbox()
    b = (max(0, b[0]-pad), max(0, b[1]-pad), min(im.width, b[2]+pad), min(im.height, b[3]+pad))
    im.crop(b).save(dst)

files = sorted(glob.glob("Assets/Art/login/WordFlow_logo/*.png"))
union = None
for f in files:
    b = Image.open(f).convert("RGBA").split()[3].getbbox()
    union = b if union is None else (min(union[0], b[0]), min(union[1], b[1]),
                                     max(union[2], b[2]), max(union[3], b[3]))
os.makedirs("Assets/Art/login/WordFlow_logo_cropped", exist_ok=True)
for f in files:
    crop(f, "Assets/Art/login/WordFlow_logo_cropped/" + os.path.basename(f), bbox=union)

crop("Assets/Art/login/ChatGPT Image Jul 18, 2026, 07_32_04 PM.png", "Assets/Art/login/login_btn.png")
crop("Assets/Art/login/ChatGPT Image Jul 18, 2026, 07_34_39 PM.png", "Assets/Art/login/signup_btn.png")
print("union bbox:", union)
EOF
```

Expected: prints `union bbox: (x0, y0, x1, y1)`; 39 files in `WordFlow_logo_cropped/`; two new button PNGs.

- [ ] **Step 2: Verify**

```bash
ls Assets/Art/login/WordFlow_logo_cropped | grep -c png$   # expect 39
python -c "from PIL import Image; print(Image.open('Assets/Art/login/login_btn.png').size, Image.open('Assets/Art/login/signup_btn.png').size, Image.open('Assets/Art/login/WordFlow_logo_cropped/0282.png').size)"
```

Expected: 39; button sizes roughly 900–1100 x 300–450 (the glow widens the bbox); all cropped logo frames share one size.

- [ ] **Step 3: Let Unity import, then commit**

Trigger `refresh_unity` (mode=force, scope=assets) so `.meta` files are generated, then:

```bash
git add Assets/Art/login/WordFlow_logo_cropped Assets/Art/login/WordFlow_logo_cropped.meta \
        Assets/Art/login/login_btn.png Assets/Art/login/login_btn.png.meta \
        Assets/Art/login/signup_btn.png Assets/Art/login/signup_btn.png.meta
git commit -m "feat: crop WordFlow logo frames and auth button art for first_page"
```

---

### Task 2: Runtime code

**Files:**
- Create: `Assets/Scripts/FirstPage/UISpriteLoop.cs`
- Modify: `Assets/Scripts/FirstPage/FirstPageIntro.cs` (full replacement below)
- Modify: `Assets/Scripts/Region1/Adventure/UI/LoginController.cs:42-52` (delete auto-login block)
- Delete: `Assets/Scripts/Region1/Adventure/Dev/DevLogoutButton.cs` (+ `.meta`)
- Delete: `Assets/Video/intro.mp4` (+ `.meta`)

**Interfaces:**
- Consumes: `AuthSession.Instance` (`HasStoredSession`, `TryAutoLogin(Action<bool>)`, `Logout()`), `SceneFadeController.Cover(float)`.
- Produces: `FirstPageIntro` with these serialized fields, all wired by Task 3's builder — `playerA`, `playerB`, `videoSurface`, `introReverseClip`, `idleClip`, `idleReverseClip`, `logoGroup`, `logoRect`, `pressToStartGroup`, `pressToStartRect`, `logoutGroup`, `logoutButton`, `authButtonsGroup`, `authButtonsRect`, `loginButton`, `signupButton`. Also `UISpriteLoop` with fields `target` (Image), `frames` (Sprite[]), `fps` (float, default 24).

- [ ] **Step 1: Write `UISpriteLoop.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cycles a fixed sprite sequence on a UGUI Image at a constant frame rate.</summary>
public sealed class UISpriteLoop : MonoBehaviour
{
    [SerializeField] private Image target;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 24f;

    private float elapsed;

    private void Update()
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        elapsed += Time.deltaTime;
        target.sprite = frames[(int)(elapsed * fps) % frames.Length];
    }
}
```

- [ ] **Step 2: Replace `FirstPageIntro.cs` entirely with:**

```csharp
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
///   1. intro_rev plays once, speed ramping 2x -> 1x; the WordFlow logo
///      drop-bounces in partway through.
///   2. idle/idle_rev ping-pong forever at 1x. On entering this phase the UI
///      floats up: press-to-start + logout when a stored session exists,
///      Login/Sign-up buttons otherwise.
/// Press-to-start runs a silent TryAutoLogin before entering WorldMap; on
/// failure (expired token) the auth buttons replace the prompt.
/// </summary>
public sealed class FirstPageIntro : MonoBehaviour
{
    private const string WorldMapSceneName = "WorldMap";
    private const string LoginSceneName = "Login";
    private const string RegisterSceneName = "Register";

    private const float MinSpeed = 1f;
    private const float MaxSpeed = 2f;
    private const float SceneFadeDuration = 0.6f;
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
    [SerializeField] private VideoPlayer playerA;
    [SerializeField] private VideoPlayer playerB;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private VideoClip introReverseClip;
    [SerializeField] private VideoClip idleClip;
    [SerializeField] private VideoClip idleReverseClip;

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

    private VideoPlayer active;
    private VideoPlayer standby;
    private bool ramping = true;
    private bool acceptingInput;
    private bool transitioning;
    private Vector2 logoRestPosition;
    private Vector2 promptRestPosition;
    private Vector2 authRestPosition;

    private void Awake()
    {
        ConfigurePlayer(playerA);
        ConfigurePlayer(playerB);

        active = playerA;
        standby = playerB;

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

        playerA.loopPointReached += HandleLoopPointReached;
        playerB.loopPointReached += HandleLoopPointReached;

        active.clip = introReverseClip;
        active.prepareCompleted += OnFirstClipReady;
        active.Prepare();

        standby.clip = idleClip;
        standby.Prepare();
    }

    private void Update()
    {
        if (ramping)
        {
            double length = active.length > 0 ? active.length : FallbackClipLength;
            float t = Mathf.Clamp01((float)(active.time / length));
            active.playbackSpeed = Mathf.Lerp(MaxSpeed, MinSpeed, t);
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

    private void OnFirstClipReady(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnFirstClipReady;
        videoSurface.texture = vp.targetTexture;
        vp.Play();
        StartCoroutine(LogoEntrance());
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
        active.playbackSpeed = 1f;
        active.Play();

        standby.clip = active.clip == idleClip ? idleReverseClip : idleClip;
        standby.Prepare();

        if (justFinished == introReverseClip)
        {
            ramping = false;
            StartCoroutine(RevealUi());
        }
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
        bool hasSession = AuthSession.Instance != null && AuthSession.Instance.HasStoredSession;
        if (hasSession)
        {
            StartCoroutine(FadeIn(logoutGroup, UiFloatDuration));
            yield return FloatUp(pressToStartGroup, pressToStartRect, promptRestPosition);
            EnableGroup(logoutGroup);
            acceptingInput = true;
        }
        else
        {
            yield return FloatUp(authButtonsGroup, authButtonsRect, authRestPosition);
            EnableGroup(authButtonsGroup);
        }
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
        AuthSession.Instance.TryAutoLogin(ok =>
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
        });
    }

    private void OnLogout()
    {
        if (transitioning)
        {
            return;
        }

        AuthSession.Instance?.Logout();
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

    private static void ConfigurePlayer(VideoPlayer player)
    {
        player.playOnAwake = false;
        player.isLooping = false;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.renderMode = VideoRenderMode.RenderTexture;
    }
}
```

Note the `SwapPromptForAuthButtons` ordering: interaction/raycasts are killed immediately (never `HideGroup` here — that zeroes alpha and makes the fade-out invisible), the fades are cosmetic.

- [ ] **Step 3: Remove the auto-login block from `LoginController.Start`**

In `Assets/Scripts/Region1/Adventure/UI/LoginController.cs`, delete exactly this block (lines 42-52) and nothing else:

```csharp
            // Returning player: restore the stored session silently and skip the form.
            if (AuthSession.Instance.HasStoredSession)
            {
                SetBusy(true);
                ShowStatus("กำลังเข้าสู่ระบบ…");
                AuthSession.Instance.TryAutoLogin(ok =>
                {
                    if (ok) GoToScene(nextSceneName);
                    else { SetBusy(false); ShowStatus(""); }   // stored token invalid → show the form
                });
            }
```

Also update the class doc comment first line from
`/// Login scene (build index 0). Email + password → AuthSession.Login. On Start it tries a`
`/// silent auto-login from a stored session and skips the form on success. Links to the`
to
`/// Login scene. Email + password → AuthSession.Login. Auto-login now lives in first_page. Links to the`

- [ ] **Step 4: Delete dead files**

```bash
rm "Assets/Scripts/Region1/Adventure/Dev/DevLogoutButton.cs" "Assets/Scripts/Region1/Adventure/Dev/DevLogoutButton.cs.meta"
rm "Assets/Video/intro.mp4" "Assets/Video/intro.mp4.meta"
```

- [ ] **Step 5: Compile check**

`refresh_unity` (force, compile) → `unity_get_compilation_errors` (or `read_console` errors). Expected: no errors. (`BuildFirstPageScene.cs` still references `intro.mp4` and the old `introClip` field — that is Task 3; if it errors on the missing field name it must be fixed in the same commit window as Task 3, so run Task 3 Step 1 before the commit if compilation fails.)

Note: `Assets/Editor/BuildFirstPageScene.cs` `SetRef` calls reference field names via string, so removing `introClip` does NOT break compilation — only a runtime warning if run. Safe to commit Task 2 alone.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/FirstPage/UISpriteLoop.cs Assets/Scripts/FirstPage/UISpriteLoop.cs.meta \
        Assets/Scripts/FirstPage/FirstPageIntro.cs \
        Assets/Scripts/Region1/Adventure/UI/LoginController.cs \
        Assets/Scripts/Region1/Adventure/Dev/DevLogoutButton.cs Assets/Scripts/Region1/Adventure/Dev/DevLogoutButton.cs.meta \
        Assets/Video/intro.mp4 Assets/Video/intro.mp4.meta
git commit -m "feat: session-aware first_page UI flow; cut forward intro leg

- FirstPageIntro: reverse-only intro (2x->1x), logo drop-bounce, float-up
  Login/Sign-up vs press-to-start+logout by stored session, TryAutoLogin
  before WorldMap
- UISpriteLoop: sprite-sequence loop for the logo Image
- LoginController: auto-login moved to first_page
- DevLogoutButton removed (replaced by first_page logout)
- intro.mp4 (forward leg) deleted"
```

---

### Task 3: Scene builder + validator update

**Files:**
- Modify: `Assets/Editor/BuildFirstPageScene.cs` (full replacement below)
- Modify: `Assets/Editor/ValidateFirstPageScene.cs` (field list + clip checks)
- Modify: `Assets/Scenes/first_page.unity` (rebuilt by the script)

**Interfaces:**
- Consumes: every serialized field name from Task 2's Interfaces block — they must match exactly; art from Task 1; existing `FirstPageIntro{A,B}.renderTexture`, `LeelawUI SDF.asset`.
- Produces: rebuilt scene with logo, auth buttons, logout button, EventSystem, Auth GO, all wired; still build index 0.

- [ ] **Step 1: Replace `BuildFirstPageScene.cs` entirely with:**

```csharp
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using WordFlow.Adventure.Net;

/// <summary>
/// One-click builder for the first_page title screen scene. Idempotent:
/// re-running rebuilds the scene from scratch and re-registers it in
/// EditorBuildSettings at index 0. Run via Tools > FirstPage > Build Scene.
/// </summary>
public static class BuildFirstPageScene
{
    private const string ScenePath = "Assets/Scenes/first_page.unity";
    private const string ThaiFontPath = "Assets/Fonts/LeelawUI SDF.asset";
    private const string LogoFramesDir = "Assets/Art/login/WordFlow_logo_cropped";
    private const string LoginBtnPath = "Assets/Art/login/login_btn.png";
    private const string SignupBtnPath = "Assets/Art/login/signup_btn.png";

    [MenuItem("Tools/FirstPage/Build Scene")]
    public static void Build()
    {
        VideoClip introReverseClip = LoadClip("Assets/Video/intro_rev.mp4");
        VideoClip idleClip = LoadClip("Assets/Video/idle.mp4");
        VideoClip idleReverseClip = LoadClip("Assets/Video/idle_rev.mp4");
        RenderTexture rtA = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroA.renderTexture");
        RenderTexture rtB = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Video/FirstPageIntroB.renderTexture");
        TMP_FontAsset thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontPath);

        Sprite loginSprite = ImportSprite(LoginBtnPath, 1024);
        Sprite signupSprite = ImportSprite(SignupBtnPath, 1024);
        Sprite[] logoFrames = ImportLogoFrames();

        if (introReverseClip == null || idleClip == null || idleReverseClip == null
            || rtA == null || rtB == null || thaiFont == null
            || loginSprite == null || signupSprite == null || logoFrames.Length == 0)
        {
            Debug.LogError("[FirstPage] Missing a required asset — aborting.");
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
        (CanvasGroup logoGroup, RectTransform logoRect, UISpriteLoop spriteLoop, Image logoImage) = CreateLogo(canvas.transform, logoFrames[0]);
        (CanvasGroup authGroup, RectTransform authRect, Button loginBtn, Button signupBtn) =
            CreateAuthButtons(canvas.transform, loginSprite, signupSprite);
        CanvasGroup pressToStartGroup = CreatePressToStart(canvas.transform, thaiFont);
        (CanvasGroup logoutGroup, Button logoutBtn) = CreateLogoutButton(canvas.transform, thaiFont);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var authGO = new GameObject("Auth");
        var firebase = authGO.AddComponent<FirebaseAuthClient>();
        var session = authGO.AddComponent<AuthSession>();
        var sessionSo = new SerializedObject(session);
        SetRef(sessionSo, "auth", firebase);
        sessionSo.ApplyModifiedPropertiesWithoutUndo();

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
        SetRef(so, "introReverseClip", introReverseClip);
        SetRef(so, "idleClip", idleClip);
        SetRef(so, "idleReverseClip", idleReverseClip);
        SetRef(so, "logoGroup", logoGroup);
        SetRef(so, "logoRect", logoRect);
        SetRef(so, "pressToStartGroup", pressToStartGroup);
        SetRef(so, "pressToStartRect", pressToStartGroup.GetComponent<RectTransform>());
        SetRef(so, "logoutGroup", logoutGroup);
        SetRef(so, "logoutButton", logoutBtn);
        SetRef(so, "authButtonsGroup", authGroup);
        SetRef(so, "authButtonsRect", authRect);
        SetRef(so, "loginButton", loginBtn);
        SetRef(so, "signupButton", signupBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        var loopSo = new SerializedObject(spriteLoop);
        SetRef(loopSo, "target", logoImage);
        var framesProp = loopSo.FindProperty("frames");
        framesProp.arraySize = logoFrames.Length;
        for (int i = 0; i < logoFrames.Length; i++)
        {
            framesProp.GetArrayElementAtIndex(i).objectReferenceValue = logoFrames[i];
        }
        loopSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        // TMP resets a freshly AddComponent-ed text back to the default font when it
        // first initializes, so the font only sticks once the scene has been written
        // and reopened. Same post-pass ThaiTMPSetup uses for the Login scene.
        ApplyThaiFont(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single), thaiFont);

        RegisterAtIndexZero(ScenePath);

        Debug.Log("[FirstPage] Scene built and registered at build index 0.");
    }

    // ---- element builders ----

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

    private static (CanvasGroup, RectTransform, UISpriteLoop, Image) CreateLogo(Transform canvasTransform, Sprite firstFrame)
    {
        var go = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UISpriteLoop));
        go.transform.SetParent(canvasTransform, false);

        float aspect = firstFrame.rect.width / firstFrame.rect.height;
        const float width = 780f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -235f);
        rt.sizeDelta = new Vector2(width, width / aspect);

        var image = go.GetComponent<Image>();
        image.sprite = firstFrame;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        return (group, rt, go.GetComponent<UISpriteLoop>(), image);
    }

    private static (CanvasGroup, RectTransform, Button, Button) CreateAuthButtons(
        Transform canvasTransform, Sprite loginSprite, Sprite signupSprite)
    {
        var container = new GameObject("AuthButtons", typeof(RectTransform), typeof(CanvasGroup));
        container.transform.SetParent(canvasTransform, false);

        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 95f);
        rt.sizeDelta = new Vector2(900, 130);

        var group = container.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Button loginBtn = CreateSpriteButton(container.transform, "LoginButton", loginSprite, new Vector2(-185f, 0f));
        Button signupBtn = CreateSpriteButton(container.transform, "SignupButton", signupSprite, new Vector2(185f, 0f));

        return (group, rt, loginBtn, signupBtn);
    }

    private static Button CreateSpriteButton(Transform parent, string name, Sprite sprite, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        float aspect = sprite.rect.width / sprite.rect.height;
        const float height = 110f;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(height * aspect, height);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        return button;
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

    private static (CanvasGroup, Button) CreateLogoutButton(Transform canvasTransform, TMP_FontAsset font)
    {
        var go = new GameObject("LogoutButton", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasTransform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(190, 58);

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = "ออกจากระบบ";
        text.font = font;
        text.fontSize = 26;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return (group, go.GetComponent<Button>());
    }

    // ---- asset helpers ----

    private static Sprite ImportSprite(string path, int maxSize)
    {
        ApplySpriteImport(path, maxSize);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] ImportLogoFrames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LogoFramesDir });
        var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".png"))
                         .OrderBy(p => p, System.StringComparer.Ordinal)
                         .ToArray();

        foreach (var p in paths)
        {
            ApplySpriteImport(p, 1024);
        }

        return paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
    }

    private static void ApplySpriteImport(string path, int maxSize)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            return;
        }

        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (dirty)
        {
            ti.SaveAndReimport();
        }
    }

    private static void ApplyThaiFont(Scene scene, TMP_FontAsset font)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.font = font;
                EditorUtility.SetDirty(tmp);
            }
        }

        EditorSceneManager.SaveScene(scene);
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

- [ ] **Step 2: Update `ValidateFirstPageScene.cs`**

Replace the `Validate()` body's clip checks and the wiring field list:

```csharp
    [MenuItem("Tools/FirstPage/Validate")]
    public static void Validate()
    {
        bool ok = true;
        ok &= CheckClipPair("Assets/Video/idle.mp4", "Assets/Video/idle_rev.mp4");
        ok &= CheckClipExists("Assets/Video/intro_rev.mp4");
        ok &= CheckGone("Assets/Video/intro.mp4");
        ok &= CheckBuildSettings();
        ok &= CheckSceneWiring();

        Debug.Log(ok ? "[FirstPage] Validate: ALL CHECKS PASSED" : "[FirstPage] Validate: FAILED — see errors above");
    }

    private static bool CheckClipExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VideoClip>(path) == null)
        {
            Debug.LogError($"[FirstPage] Missing clip: {path}");
            return false;
        }

        return true;
    }

    private static bool CheckGone(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            Debug.LogError($"[FirstPage] {path} should have been deleted");
            return false;
        }

        return true;
    }
```

And inside `CheckSceneWiring`, replace the field array with:

```csharp
            foreach (var field in new[]
            {
                "playerA", "playerB", "videoSurface",
                "introReverseClip", "idleClip", "idleReverseClip",
                "logoGroup", "logoRect",
                "pressToStartGroup", "pressToStartRect", "logoutGroup", "logoutButton",
                "authButtonsGroup", "authButtonsRect", "loginButton", "signupButton",
            })
```

Additionally, at the end of `CheckSceneWiring` (before the `if (!wasOpen)` close), add scene-level presence checks:

```csharp
        bool hasEventSystem = scene.GetRootGameObjects()
            .Any(go => go.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null);
        if (!hasEventSystem)
        {
            Debug.LogError("[FirstPage] No EventSystem in first_page scene — UI buttons will not click");
            ok = false;
        }

        bool hasAuth = scene.GetRootGameObjects()
            .Any(go => go.GetComponentInChildren<WordFlow.Adventure.Net.AuthSession>(true) != null);
        if (!hasAuth)
        {
            Debug.LogError("[FirstPage] No AuthSession in first_page scene — auto-login cannot run");
            ok = false;
        }
```

- [ ] **Step 3: Compile, build, validate**

`refresh_unity` (force, compile) → check no compilation errors → `execute_menu_item "Tools/FirstPage/Build Scene"` → console shows `[FirstPage] Scene built and registered at build index 0.` → `execute_menu_item "Tools/FirstPage/Validate"` → `ALL CHECKS PASSED`.

Then verify the Thai font stuck (both TMP texts — prompt and logout):

```bash
grep -c "96f1ad4e221207b4bb82f6c411d55078" Assets/Scenes/first_page.unity   # expect >= 4 (2 texts x font+material)
grep -c "8f586378b4e144a9851e7b34d9b748ee" Assets/Scenes/first_page.unity   # expect 0
```

- [ ] **Step 4: Save project (flush Build Settings) and commit**

`execute_menu_item "File/Save Project"`, then:

```bash
git add Assets/Editor/BuildFirstPageScene.cs Assets/Editor/ValidateFirstPageScene.cs \
        Assets/Scenes/first_page.unity ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: rebuild first_page with logo, auth buttons, logout, EventSystem, AuthSession"
```

---

### Task 4: Play Mode verification

Game View screenshots come back solid white through MCP (capture renders via Main Camera, which excludes Screen Space - Overlay canvases) — verify by reading live component state instead, as done for the previous milestone.

- [ ] **Step 1: Fresh-player path.** Delete the stored session first so the auth-buttons branch runs:

Run in Play Mode after `unity_play_mode play` on `first_page`, or simply check: `PlayerPrefs` key `wf_refresh_token`. To clear from the editor, run menu item `Tools/Scenes/Reset WorldMap Progress`? — NO (that clears island progress, unrelated). Instead verify whichever branch the current machine state produces and note it; to force the fresh path, call `AuthSession` logout via the UI once the session branch is verified, or delete the PlayerPrefs key with a throwaway editor one-liner if needed.

Checks via component reads (poll `mcpforunity://scene/gameobject/{id}/component/...`):
1. ~1 s in: `FirstPageIntro` active player has `clip = intro_rev.mp4`, `playbackSpeed` between 2.0 and 1.0 and decreasing (matches `Lerp(2, 1, t)`).
2. `Logo` CanvasGroup alpha rises 0→1 within ~2 s; `Logo` Image `sprite` name changes over time (UISpriteLoop cycling).
3. After the leg ends (~3.5 s): active clip alternates `idle.mp4` / `idle_rev.mp4`, speed 1.0.
4. No session: `AuthButtons` CanvasGroup alpha → 1, `interactable = true`; `PressToStart` and `LogoutButton` stay alpha 0.
   With session: `PressToStart` alpha pulsing 0.55–1.0, `LogoutButton` alpha 1.
5. Console: no errors (the two WindowsMediaFoundation color-primaries warnings on the mp4s are known and harmless).

- [ ] **Step 2: Manual click checks (needs the user — no MCP tool can click the Game View):**
   - Login button → black fade → Login scene (form shows immediately, no auto-login).
   - Sign-up button → Register scene.
   - With session: press anywhere → "กำลัง TryAutoLogin" → WorldMap; logout → session UI swaps to Login/Sign-up in place.
   - In WorldMap: the red "Logout (dev)" IMGUI button is gone.

- [ ] **Step 3: Update this plan's Status section with evidence, then commit docs**

```bash
git add docs/superpowers/plans/2026-07-19-first-page-auth-ui.md
git commit -m "docs: record first_page auth UI verification evidence"
```
