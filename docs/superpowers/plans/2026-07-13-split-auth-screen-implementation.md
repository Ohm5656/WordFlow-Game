# Split Auth Screen into 3 Scenes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single combined WordFlow auth screen with three focused Unity scenes — `Login.unity`, `Register.unity`, `ForgotPassword.unity` — each showing only its relevant fields, with text links to navigate between them.

**Architecture:** A new abstract `AuthControllerBase : MonoBehaviour` holds the shared UI helpers (status text, busy toggle, email validation, scene navigation, failure handling). Three thin subclasses (`LoginController`, `RegisterController`, `ForgotPasswordController`) each own one flow, moved verbatim from the existing `AuthScreenController`. The idempotent editor tool `WordFlowLoginSceneBuilder.cs` is refactored into shared layout helpers plus three build methods (one per scene) and a "Build All" convenience item. `AuthSession` and `FirebaseAuthClient` are untouched. The original `AuthScreenController.cs` is deleted last, after the three replacements compile and are verified.

**Tech Stack:** Unity 6000.4.3f1, C#, TextMeshPro (TMP), Unity UI (uGUI), NUnit (Unity Test Framework, EditMode), Firebase Identity Toolkit REST (via existing `FirebaseAuthClient`).

## Global Constraints

- Unity Editor version: `6000.4.3f1` (do not upgrade).
- All user-facing status/placeholder copy stays **Thai** (with the existing `Thai / English` placeholder style), copied verbatim from `AuthScreenController.cs`.
- New runtime scripts live in namespace `WordFlow.Adventure.UI` under `Assets/Scripts/Region1/Adventure/UI/` (part of the `WordFlow.Adventure` asmdef).
- Tests live in `Assets/Tests/Adventure/` under the `WordFlow.Adventure.Tests` asmdef (NUnit `[Test]`, namespace `WordFlow.Adventure.Tests`).
- Scene build order: `Login.unity` MUST stay at build index 0. `Register.unity` and `ForgotPassword.unity` are appended, enabled, matching `Login`'s enabled state.
- Scene names used by `SceneManager.LoadScene` are exactly `"Login"`, `"Register"`, `"ForgotPassword"`, `"WorldMap"` (no `.unity`, no path).
- Do NOT modify `AuthSession.cs` or `FirebaseAuthClient.cs`. No backend changes.
- Do NOT commit the uncommitted Thai-font WIP (`Assets/Fonts/LeelawUI SDF.asset`, `Assets/Fonts/ThaiFontDoctor_LeelawUI.asset*`) — it is unrelated work-in-progress. Stage only the files each task names.

## Reference: existing AuthSession API (do not change — call as-is)

```csharp
// namespace WordFlow.Adventure.Net
AuthSession.Instance                                   // static, may be null before Awake
bool  AuthSession.Instance.HasStoredSession            // true if a refresh token is stored
void  Register(string email, string password, Action<bool,string> onDone)
void  Login(string email, string password, Action<bool,string> onDone)
void  TryAutoLogin(Action<bool> onDone)
void  SendPasswordReset(string email, Action<bool,string> onDone)
void  SetChildProfile(string displayName, string birthDate, Action<bool,string> onDone)
void  LinkByCode(string code, Action<bool,string> onDone)
```

`AuthSession.Awake()` already self-destructs duplicate instances and calls `DontDestroyOnLoad`, so including an `AuthBootstrap` (`FirebaseAuthClient` + `AuthSession`) in all three scenes is safe — the first-loaded instance wins and persists across scene loads.

---

### Task 1: `AuthControllerBase` + email-validation TDD

**Files:**
- Create: `Assets/Scripts/Region1/Adventure/UI/AuthControllerBase.cs`
- Test: `Assets/Tests/Adventure/AuthEmailValidationTests.cs`

**Interfaces:**
- Produces (used by Tasks 2–4):
  - `public static bool IsValidEmail(string email)`
  - `protected void ShowStatus(string message)`
  - `protected virtual void SetBusy(bool busy)` — sets `_busy`, toggles `busyIndicator`
  - `protected bool _busy`
  - `protected void Fail(string message)` — `SetBusy(false)` + status
  - `protected bool Ready()` — false + status if `AuthSession.Instance == null`
  - `protected void GoToScene(string sceneName)` — wraps `SceneManager.LoadScene`
  - `protected static string Trimmed(TMP_InputField field)`
  - Serialized fields available to subclasses: `protected TMP_Text statusText`, `protected GameObject busyIndicator`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Adventure/AuthEmailValidationTests.cs`:

```csharp
using NUnit.Framework;
using WordFlow.Adventure.UI;

namespace WordFlow.Adventure.Tests
{
    public sealed class AuthEmailValidationTests
    {
        [Test]
        public void PlainEmail_IsValid()
        {
            Assert.IsTrue(AuthControllerBase.IsValidEmail("parent@example.com"));
        }

        [Test]
        public void MissingAt_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail("parent.example.com"));
        }

        [Test]
        public void MissingDot_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail("parent@examplecom"));
        }

        [Test]
        public void Empty_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail(""));
        }

        [Test]
        public void Null_IsInvalid()
        {
            Assert.IsFalse(AuthControllerBase.IsValidEmail(null));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

In Unity: `Window > General > Test Runner > EditMode > Run All`.
Expected: FAIL — compile error / `AuthControllerBase` does not exist.
(CLI alternative, run from repo root — adjust the Unity path:
`"C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe" -runTests -batchmode -projectPath "." -testPlatform EditMode -testResults "%TEMP%\wf-tests.xml"` then read the XML.)

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Region1/Adventure/UI/AuthControllerBase.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// Shared behaviour for the three auth-screen controllers (Login / Register / ForgotPassword).
    /// Holds the status text + busy indicator, email validation, scene navigation and failure
    /// handling. Each concrete controller owns its own fields, primary button and link buttons.
    /// </summary>
    public abstract class AuthControllerBase : MonoBehaviour
    {
        [Header("Shared UI")]
        [SerializeField] protected TMP_Text statusText;
        [SerializeField] protected GameObject busyIndicator;   // optional spinner; hidden when idle

        protected bool _busy;

        protected void ShowStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? "";
        }

        protected virtual void SetBusy(bool busy)
        {
            _busy = busy;
            if (busyIndicator != null) busyIndicator.SetActive(busy);
        }

        protected void Fail(string message)
        {
            SetBusy(false);
            ShowStatus(string.IsNullOrEmpty(message) ? "เกิดข้อผิดพลาด กรุณาลองใหม่" : message);
        }

        protected bool Ready()
        {
            if (AuthSession.Instance != null) return true;
            ShowStatus("ระบบล็อกอินยังไม่พร้อม");
            return false;
        }

        protected void GoToScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[Auth] GoToScene called with empty scene name.");
                return;
            }
            SceneManager.LoadScene(sceneName);
        }

        protected static string Trimmed(TMP_InputField field) => field != null ? field.text.Trim() : "";

        public static bool IsValidEmail(string email)
            => !string.IsNullOrEmpty(email) && email.Contains("@") && email.Contains(".");
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

In Unity: `Test Runner > EditMode > Run All`.
Expected: PASS — all 5 tests in `AuthEmailValidationTests` green, Console has no compile errors.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Region1/Adventure/UI/AuthControllerBase.cs" \
        "Assets/Scripts/Region1/Adventure/UI/AuthControllerBase.cs.meta" \
        "Assets/Tests/Adventure/AuthEmailValidationTests.cs" \
        "Assets/Tests/Adventure/AuthEmailValidationTests.cs.meta"
git commit -m "feat(auth): add AuthControllerBase with email validation (TDD)"
```

---

### Task 2: `LoginController`

**Files:**
- Create: `Assets/Scripts/Region1/Adventure/UI/LoginController.cs`

**Interfaces:**
- Consumes (from Task 1): `IsValidEmail`, `ShowStatus`, `SetBusy`, `_busy`, `Fail`, `Ready`, `GoToScene`, `Trimmed`.
- Produces (used by Task 5's builder): serialized field names `emailField`, `passwordField`, `loginButton`, `toRegisterLink`, `toForgotPasswordLink`, `nextSceneName`; public handler `OnLogin()`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Region1/Adventure/UI/LoginController.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// Login scene (build index 0). Email + password → AuthSession.Login. On Start it tries a
    /// silent auto-login from a stored session and skips the form on success. Links to the
    /// Register and ForgotPassword scenes.
    /// </summary>
    public sealed class LoginController : AuthControllerBase
    {
        [Header("Fields")]
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button toRegisterLink;
        [SerializeField] private Button toForgotPasswordLink;

        [SerializeField] private string nextSceneName = "WorldMap";

        private void Awake()
        {
            if (loginButton != null) loginButton.onClick.AddListener(OnLogin);
            if (toRegisterLink != null) toRegisterLink.onClick.AddListener(() => GoToScene("Register"));
            if (toForgotPasswordLink != null) toForgotPasswordLink.onClick.AddListener(() => GoToScene("ForgotPassword"));
        }

        private void Start()
        {
            SetBusy(false);
            ShowStatus("");
            if (AuthSession.Instance == null)
            {
                ShowStatus("ระบบล็อกอินยังไม่พร้อม (ไม่พบ AuthSession ในซีน)");
                return;
            }
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
        }

        public void OnLogin()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            string password = passwordField != null ? passwordField.text : "";
            if (!IsValidEmail(email)) { ShowStatus("กรุณากรอกอีเมลให้ถูกต้อง"); return; }
            if (string.IsNullOrEmpty(password)) { ShowStatus("กรุณากรอกรหัสผ่าน"); return; }

            SetBusy(true);
            ShowStatus("กำลังเข้าสู่ระบบ…");
            AuthSession.Instance.Login(email, password, (ok, err) =>
            {
                if (ok) GoToScene(nextSceneName);
                else Fail(err);
            });
        }

        protected override void SetBusy(bool busy)
        {
            base.SetBusy(busy);
            if (loginButton != null) loginButton.interactable = !busy;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Return focus to the Unity Editor; let it recompile. Expected: Console shows no errors, no warnings referencing `LoginController`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Region1/Adventure/UI/LoginController.cs" \
        "Assets/Scripts/Region1/Adventure/UI/LoginController.cs.meta"
git commit -m "feat(auth): add LoginController (email/password + auto-login + links)"
```

---

### Task 3: `RegisterController`

**Files:**
- Create: `Assets/Scripts/Region1/Adventure/UI/RegisterController.cs`

**Interfaces:**
- Consumes (from Task 1): `IsValidEmail`, `ShowStatus`, `SetBusy`, `_busy`, `Fail`, `Ready`, `GoToScene`, `Trimmed`.
- Produces (used by Task 5's builder): serialized field names `emailField`, `passwordField`, `childNameField`, `birthDateField`, `doctorCodeField`, `registerButton`, `toLoginLink`, `nextSceneName`; public handler `OnRegister()`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Region1/Adventure/UI/RegisterController.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// Register scene. Adult registers once: email + password + child name + birthdate + optional
    /// doctor code. On success: create account → save child profile → optional doctor link → game.
    /// Links back to the Login scene. No auto-login on Start.
    /// </summary>
    public sealed class RegisterController : AuthControllerBase
    {
        [Header("Fields")]
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private TMP_InputField childNameField;
        [SerializeField] private TMP_InputField birthDateField;    // YYYY-MM-DD
        [SerializeField] private TMP_InputField doctorCodeField;   // optional (WF-XXXX)

        [Header("Buttons")]
        [SerializeField] private Button registerButton;
        [SerializeField] private Button toLoginLink;

        [SerializeField] private string nextSceneName = "WorldMap";

        private void Awake()
        {
            if (registerButton != null) registerButton.onClick.AddListener(OnRegister);
            if (toLoginLink != null) toLoginLink.onClick.AddListener(() => GoToScene("Login"));
        }

        private void Start()
        {
            SetBusy(false);
            ShowStatus("");
        }

        public void OnRegister()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            string password = passwordField != null ? passwordField.text : "";
            string childName = Trimmed(childNameField);
            string birthDate = Trimmed(birthDateField);
            string code = Trimmed(doctorCodeField);

            if (!IsValidEmail(email)) { ShowStatus("กรุณากรอกอีเมลให้ถูกต้อง"); return; }
            if (password.Length < 6) { ShowStatus("รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร"); return; }
            if (string.IsNullOrEmpty(childName)) { ShowStatus("กรุณากรอกชื่อของเด็ก"); return; }

            SetBusy(true);
            ShowStatus("กำลังสร้างบัญชี…");
            AuthSession.Instance.Register(email, password, (ok, err) =>
            {
                if (!ok) { Fail(err); return; }
                // Account + child exist now; save the child's name/birthdate, then optionally link.
                AuthSession.Instance.SetChildProfile(childName, birthDate, (pok, perr) =>
                {
                    if (!pok) Debug.LogWarning($"[Auth] child profile save failed: {perr}");
                    if (!string.IsNullOrEmpty(code))
                        AuthSession.Instance.LinkByCode(code, (lok, lerr) =>
                        {
                            if (!lok) Debug.LogWarning($"[Auth] doctor link failed: {lerr}");
                            GoToScene(nextSceneName);   // linking is optional; proceed regardless
                        });
                    else
                        GoToScene(nextSceneName);
                });
            });
        }

        protected override void SetBusy(bool busy)
        {
            base.SetBusy(busy);
            if (registerButton != null) registerButton.interactable = !busy;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Return focus to the Unity Editor; let it recompile. Expected: Console shows no errors referencing `RegisterController`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Region1/Adventure/UI/RegisterController.cs" \
        "Assets/Scripts/Region1/Adventure/UI/RegisterController.cs.meta"
git commit -m "feat(auth): add RegisterController (register → child profile → optional link)"
```

---

### Task 4: `ForgotPasswordController`

**Files:**
- Create: `Assets/Scripts/Region1/Adventure/UI/ForgotPasswordController.cs`

**Interfaces:**
- Consumes (from Task 1): `IsValidEmail`, `ShowStatus`, `SetBusy`, `_busy`, `Ready`, `GoToScene`, `Trimmed`.
- Produces (used by Task 5's builder): serialized field names `emailField`, `sendResetButton`, `toLoginLink`; public handler `OnSendReset()`.

- [ ] **Step 1: Write the implementation**

Create `Assets/Scripts/Region1/Adventure/UI/ForgotPasswordController.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// ForgotPassword scene. Email only → AuthSession.SendPasswordReset (Firebase-direct, no
    /// backend). Links back to the Login scene. No auto-login on Start.
    /// </summary>
    public sealed class ForgotPasswordController : AuthControllerBase
    {
        [Header("Fields")]
        [SerializeField] private TMP_InputField emailField;

        [Header("Buttons")]
        [SerializeField] private Button sendResetButton;
        [SerializeField] private Button toLoginLink;

        private void Awake()
        {
            if (sendResetButton != null) sendResetButton.onClick.AddListener(OnSendReset);
            if (toLoginLink != null) toLoginLink.onClick.AddListener(() => GoToScene("Login"));
        }

        private void Start()
        {
            SetBusy(false);
            ShowStatus("");
        }

        public void OnSendReset()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            if (!IsValidEmail(email)) { ShowStatus("กรอกอีเมลก่อน แล้วกด \"ส่งรีเซ็ตรหัสผ่าน\""); return; }

            SetBusy(true);
            AuthSession.Instance.SendPasswordReset(email, (ok, err) =>
            {
                SetBusy(false);
                ShowStatus(ok ? "ส่งอีเมลรีเซ็ตรหัสผ่านแล้ว กรุณาตรวจสอบกล่องจดหมาย" : err);
            });
        }

        protected override void SetBusy(bool busy)
        {
            base.SetBusy(busy);
            if (sendResetButton != null) sendResetButton.interactable = !busy;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Return focus to the Unity Editor; let it recompile. Expected: Console shows no errors referencing `ForgotPasswordController`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Region1/Adventure/UI/ForgotPasswordController.cs" \
        "Assets/Scripts/Region1/Adventure/UI/ForgotPasswordController.cs.meta"
git commit -m "feat(auth): add ForgotPasswordController (email → Firebase reset)"
```

---

### Task 5: Refactor `WordFlowLoginSceneBuilder` into 3 scene builders

**Files:**
- Modify (full rewrite): `Assets/Editor/WordFlowLoginSceneBuilder.cs`

**Interfaces:**
- Consumes: `LoginController`, `RegisterController`, `ForgotPasswordController` (Tasks 2–4) and their serialized field names; `FirebaseAuthClient` + `AuthSession` (`AuthBootstrap`).
- Produces menu items: `Tools > WordFlow > Build Login Scene`, `Build Register Scene`, `Build Forgot Password Scene`, `Build All Auth Scenes`.

- [ ] **Step 1: Rewrite the builder**

Replace the entire contents of `Assets/Editor/WordFlowLoginSceneBuilder.cs` with:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using WordFlow.Adventure.Net;
using WordFlow.Adventure.UI;

/// <summary>
/// One-click builders for the three WordFlow auth scenes (Login / Register / ForgotPassword).
/// Each is idempotent: re-running rebuilds that scene from scratch and re-registers it in
/// EditorBuildSettings. Login is always build index 0. Run via Tools > WordFlow > ...
/// </summary>
public static class WordFlowLoginSceneBuilder
{
    // ---- menu items ----

    [MenuItem("Tools/WordFlow/Build Login Scene")]
    public static void BuildLoginScene()
    {
        var canvas = NewAuthScene(out _);
        CreateText(canvas.transform, "Title", "WordFlow", 72, new Vector2(0, 800), new Vector2(900, 140),
            new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 480));
        var password = CreateInput(canvas.transform, "PasswordField", "รหัสผ่าน / Password", new Vector2(0, 360),
            TMP_InputField.ContentType.Password);

        var loginBtn = CreateButton(canvas.transform, "LoginButton", "เข้าสู่ระบบ / Login",
            new Vector2(0, 200), new Color(0.22f, 0.6f, 0.4f));
        var toRegister = CreateLinkButton(canvas.transform, "ToRegisterLink",
            "ยังไม่มีบัญชี? สมัครสมาชิก / Register", new Vector2(0, 80));
        var toForgot = CreateLinkButton(canvas.transform, "ToForgotPasswordLink",
            "ลืมรหัสผ่าน? / Forgot Password", new Vector2(0, 0));

        var status = CreateText(canvas.transform, "StatusText", "", 34, new Vector2(0, -160),
            new Vector2(900, 150), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("LoginController");
        var controller = controllerGO.AddComponent<LoginController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "passwordField", password);
        SetRef(so, "statusText", status);
        SetRef(so, "loginButton", loginBtn);
        SetRef(so, "toRegisterLink", toRegister);
        SetRef(so, "toForgotPasswordLink", toForgot);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/Login.unity", forceIndexZero: true);
        Debug.Log("[WordFlow] Login scene built at build index 0.");
    }

    [MenuItem("Tools/WordFlow/Build Register Scene")]
    public static void BuildRegisterScene()
    {
        var canvas = NewAuthScene(out _);
        CreateText(canvas.transform, "Title", "สมัครสมาชิก / Register", 60, new Vector2(0, 820),
            new Vector2(900, 140), new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 620));
        var password = CreateInput(canvas.transform, "PasswordField", "รหัสผ่าน / Password", new Vector2(0, 500),
            TMP_InputField.ContentType.Password);
        var childName = CreateInput(canvas.transform, "ChildNameField", "ชื่อของเด็ก / Child name", new Vector2(0, 380));
        var birth = CreateInput(canvas.transform, "BirthDateField", "วันเกิด YYYY-MM-DD", new Vector2(0, 260));
        var code = CreateInput(canvas.transform, "DoctorCodeField", "รหัสแพทย์ WF-XXXX (ไม่บังคับ)", new Vector2(0, 140));

        var registerBtn = CreateButton(canvas.transform, "RegisterButton", "สร้างบัญชี / Register",
            new Vector2(0, -20), new Color(0.16f, 0.5f, 0.9f));
        var toLogin = CreateLinkButton(canvas.transform, "ToLoginLink",
            "มีบัญชีอยู่แล้ว? เข้าสู่ระบบ / Login", new Vector2(0, -140));

        var status = CreateText(canvas.transform, "StatusText", "", 34, new Vector2(0, -300),
            new Vector2(900, 150), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("RegisterController");
        var controller = controllerGO.AddComponent<RegisterController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "passwordField", password);
        SetRef(so, "childNameField", childName);
        SetRef(so, "birthDateField", birth);
        SetRef(so, "doctorCodeField", code);
        SetRef(so, "statusText", status);
        SetRef(so, "registerButton", registerBtn);
        SetRef(so, "toLoginLink", toLogin);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/Register.unity", forceIndexZero: false);
        Debug.Log("[WordFlow] Register scene built.");
    }

    [MenuItem("Tools/WordFlow/Build Forgot Password Scene")]
    public static void BuildForgotPasswordScene()
    {
        var canvas = NewAuthScene(out _);
        CreateText(canvas.transform, "Title", "ลืมรหัสผ่าน / Forgot Password", 54, new Vector2(0, 760),
            new Vector2(940, 140), new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 520));

        var sendBtn = CreateButton(canvas.transform, "SendResetButton", "ส่งรีเซ็ตรหัสผ่าน / Send Reset",
            new Vector2(0, 360), new Color(0.16f, 0.5f, 0.9f));
        var toLogin = CreateLinkButton(canvas.transform, "ToLoginLink",
            "กลับไปเข้าสู่ระบบ / Back to Login", new Vector2(0, 240));

        var status = CreateText(canvas.transform, "StatusText", "", 34, new Vector2(0, 80),
            new Vector2(900, 200), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("ForgotPasswordController");
        var controller = controllerGO.AddComponent<ForgotPasswordController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "statusText", status);
        SetRef(so, "sendResetButton", sendBtn);
        SetRef(so, "toLoginLink", toLogin);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/ForgotPassword.unity", forceIndexZero: false);
        Debug.Log("[WordFlow] ForgotPassword scene built.");
    }

    [MenuItem("Tools/WordFlow/Build All Auth Scenes")]
    public static void BuildAllAuthScenes()
    {
        // Order matters: build the two secondary scenes first, then Login last so it lands at
        // index 0 and the secondaries are appended after it.
        BuildRegisterScene();
        BuildForgotPasswordScene();
        BuildLoginScene();
        Debug.Log("[WordFlow] All three auth scenes built and registered.");
    }

    // ---- shared scene scaffolding ----

    private static Canvas NewAuthScene(out UnityEngine.SceneManagement.Scene scene)
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.18f);

        return canvas;
    }

    private static void CreateAuthBootstrap()
    {
        var bootstrap = new GameObject("AuthBootstrap");
        var fac = bootstrap.AddComponent<FirebaseAuthClient>();
        var session = bootstrap.AddComponent<AuthSession>();
        var soSession = new SerializedObject(session);
        SetRef(soSession, "auth", fac);
        soSession.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SaveAndRegister(string path, bool forceIndexZero)
    {
        System.IO.Directory.CreateDirectory(Application.dataPath + "/Scenes");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);

        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        existing.RemoveAll(s => s.path == path);
        if (forceIndexZero)
            existing.Insert(0, new EditorBuildSettingsScene(path, true));
        else
            existing.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = existing.ToArray();
    }

    // ---- element helpers (unchanged from the original builder) ----

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[WordFlow] missing serialized property: " + prop); return; }
        p.objectReferenceValue = value;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 pos,
        TMP_InputField.ContentType content = TMP_InputField.ContentType.Standard)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(820, 96);
        go.GetComponent<Image>().color = Color.white;
        var input = go.AddComponent<TMP_InputField>();
        input.contentType = content;

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var art = area.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(20, 8); art.offsetMax = new Vector2(-20, -8);

        var ph = new GameObject("Placeholder", typeof(RectTransform));
        ph.transform.SetParent(area.transform, false);
        var pht = ph.AddComponent<TextMeshProUGUI>();
        pht.text = placeholder; pht.fontSize = 34; pht.color = new Color(0.5f, 0.5f, 0.5f);
        pht.alignment = TextAlignmentOptions.Left;
        Stretch(ph.GetComponent<RectTransform>());

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(area.transform, false);
        var tt = txt.AddComponent<TextMeshProUGUI>();
        tt.fontSize = 34; tt.color = Color.black; tt.alignment = TextAlignmentOptions.Left;
        Stretch(txt.GetComponent<RectTransform>());

        input.textViewport = art;
        input.textComponent = tt;
        input.placeholder = pht;
        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(820, 100);
        go.GetComponent<Image>().color = color;
        var labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        Stretch(labelGO.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    // A clickable text link: transparent background Image (so Button raycasts) + coloured label.
    private static Button CreateLinkButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(820, 70);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent but still raycasts
        var labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 32; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.55f, 0.75f, 1f);
        Stretch(labelGO.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
```

- [ ] **Step 2: Build the three scenes**

In Unity, after it recompiles: `Tools > WordFlow > Build All Auth Scenes`.
Expected: Console logs "Register scene built.", "ForgotPassword scene built.", "Login scene built at build index 0.", "All three auth scenes built and registered." No errors, no "missing serialized property" warnings.

- [ ] **Step 3: Verify build settings + scene files**

`File > Build Settings`: confirm `Login` is index 0, `Register` and `ForgotPassword` present and checked.
On disk:

```bash
ls "Assets/Scenes/Login.unity" "Assets/Scenes/Register.unity" "Assets/Scenes/ForgotPassword.unity"
```

Expected: all three exist.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Editor/WordFlowLoginSceneBuilder.cs" \
        "Assets/Scenes/Login.unity" "Assets/Scenes/Login.unity.meta" \
        "Assets/Scenes/Register.unity" "Assets/Scenes/Register.unity.meta" \
        "Assets/Scenes/ForgotPassword.unity" "Assets/Scenes/ForgotPassword.unity.meta" \
        "ProjectSettings/EditorBuildSettings.asset"
git commit -m "feat(auth): split builder into Login/Register/ForgotPassword scene builders"
```

---

### Task 6: Delete `AuthScreenController` + manual end-to-end verification

**Files:**
- Delete: `Assets/Scripts/Region1/Adventure/UI/AuthScreenController.cs` (+ `.meta`)

**Interfaces:**
- Consumes: the three built scenes (Task 5) and their controllers (Tasks 2–4).

- [ ] **Step 1: Confirm nothing else references `AuthScreenController`**

```bash
grep -rn "AuthScreenController" Assets --include=*.cs
```

Expected: only `Assets/Scripts/Region1/Adventure/UI/AuthScreenController.cs` itself (its own definition). If any *other* `.cs` references it, stop and fix that reference before deleting. (Old `Login.unity` referenced it via GUID, but Task 5 already rebuilt `Login.unity` to use `LoginController`, so no scene should reference it anymore.)

- [ ] **Step 2: Delete the file**

```bash
git rm "Assets/Scripts/Region1/Adventure/UI/AuthScreenController.cs" \
       "Assets/Scripts/Region1/Adventure/UI/AuthScreenController.cs.meta"
```

- [ ] **Step 3: Verify the project compiles clean**

Return focus to Unity; let it recompile. Expected: Console has zero compile errors (a lingering reference would surface here).

- [ ] **Step 4: Manual flow test in the Editor**

Per project convention (no automated Play Mode driver — Windows path-length limits). Open `Assets/Scenes/Login.unity`, press Play, and walk through:

- **Cross-links:** Login → tap "Register" link → Register scene loads (5 fields). Tap "Login" link → back to Login. Tap "Forgot Password?" → ForgotPassword scene (email only). Tap "Back to Login" → Login. Each scene's fields start empty.
- **Register (fresh account):** on Register, fill email/password/child name (birthdate optional, doctor code optional) → tap Register → lands on WorldMap.
- **Login (existing account):** on Login, enter that email/password → tap Login → lands on WorldMap.
- **Auto-login:** stop, re-enter Play on Login with a stored session → status shows "กำลังเข้าสู่ระบบ…" and auto-navigates to WorldMap without touching the form.
- **Forgot Password:** on ForgotPassword, enter a registered email → tap Send Reset → status shows "ส่งอีเมลรีเซ็ตรหัสผ่านแล้ว…".

Expected: all five behave as described; typed text is visible (dark text on white input fields); no Console errors during play.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(auth): remove AuthScreenController, superseded by 3-scene split"
```

---

## Notes for the implementer

- **Do not `git add -A` in Tasks 1–5** — stage only the listed files so the uncommitted Thai-font WIP stays out of these commits. (Task 6 Step 5 uses `-A` deliberately, only after the delete; if the font WIP is still present, replace it with an explicit `git add` of the deletion + any regenerated `.meta` files instead.)
- Unity may reorder/rewrite `.meta` GUIDs when it first imports the new scripts — that's expected; commit them as generated.
- If a `Build ... Scene` menu item logs "missing serialized property: X", the serialized field name in the controller (Tasks 2–4) and the `SetRef(so, "X", ...)` call in the builder (Task 5) have drifted — reconcile the names; they are listed together in each task's **Interfaces → Produces** block.
