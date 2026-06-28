using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// Drives the first-launch auth screen (Thai). On start it tries a silent auto-login from the
    /// stored refresh token; if there's no session it shows the form. An adult registers once
    /// (email + password + child name + birthdate + optional doctor code) or logs back in; on
    /// success we load the game scene. Wire the serialized fields to a Canvas in the editor — see
    /// the header comment of the login design spec for the exact wiring.
    /// </summary>
    public sealed class AuthScreenController : MonoBehaviour
    {
        [Header("Shared")]
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject busyIndicator;     // optional spinner; hidden when idle
        [SerializeField] private string nextSceneName = "WorldMap";

        [Header("Register-only fields")]
        [SerializeField] private TMP_InputField childNameField;
        [SerializeField] private TMP_InputField birthDateField;   // YYYY-MM-DD
        [SerializeField] private TMP_InputField doctorCodeField;  // optional (WF-XXXX)

        [Header("Buttons")]
        [SerializeField] private Button registerButton;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button forgotPasswordButton;

        private bool _busy;

        private void Awake()
        {
            if (registerButton != null) registerButton.onClick.AddListener(OnRegister);
            if (loginButton != null) loginButton.onClick.AddListener(OnLogin);
            if (forgotPasswordButton != null) forgotPasswordButton.onClick.AddListener(OnForgotPassword);
        }

        private void Start()
        {
            SetBusy(false);
            SetStatus("");
            if (AuthSession.Instance == null)
            {
                SetStatus("ระบบล็อกอินยังไม่พร้อม (ไม่พบ AuthSession ในซีน)");
                return;
            }
            // Returning player: restore the stored session silently and skip the form.
            if (AuthSession.Instance.HasStoredSession)
            {
                SetBusy(true);
                SetStatus("กำลังเข้าสู่ระบบ…");
                AuthSession.Instance.TryAutoLogin(ok =>
                {
                    if (ok) GoToGame();
                    else { SetBusy(false); SetStatus(""); }   // stored token invalid → show the form
                });
            }
        }

        // ---- button handlers ----

        public void OnRegister()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            string password = passwordField != null ? passwordField.text : "";
            string childName = Trimmed(childNameField);
            string birthDate = Trimmed(birthDateField);
            string code = Trimmed(doctorCodeField);

            if (!ValidEmail(email)) { SetStatus("กรุณากรอกอีเมลให้ถูกต้อง"); return; }
            if (password.Length < 6) { SetStatus("รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร"); return; }
            if (string.IsNullOrEmpty(childName)) { SetStatus("กรุณากรอกชื่อของเด็ก"); return; }

            SetBusy(true);
            SetStatus("กำลังสร้างบัญชี…");
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
                            GoToGame();   // linking is optional; proceed regardless
                        });
                    else
                        GoToGame();
                });
            });
        }

        public void OnLogin()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            string password = passwordField != null ? passwordField.text : "";
            if (!ValidEmail(email)) { SetStatus("กรุณากรอกอีเมลให้ถูกต้อง"); return; }
            if (string.IsNullOrEmpty(password)) { SetStatus("กรุณากรอกรหัสผ่าน"); return; }

            SetBusy(true);
            SetStatus("กำลังเข้าสู่ระบบ…");
            AuthSession.Instance.Login(email, password, (ok, err) =>
            {
                if (ok) GoToGame();
                else Fail(err);
            });
        }

        public void OnForgotPassword()
        {
            if (_busy || !Ready()) return;
            string email = Trimmed(emailField);
            if (!ValidEmail(email)) { SetStatus("กรอกอีเมลก่อน แล้วกด \"ลืมรหัสผ่าน\""); return; }
            SetBusy(true);
            AuthSession.Instance.SendPasswordReset(email, (ok, err) =>
            {
                SetBusy(false);
                SetStatus(ok ? "ส่งอีเมลรีเซ็ตรหัสผ่านแล้ว กรุณาตรวจสอบกล่องจดหมาย" : err);
            });
        }

        // ---- helpers ----

        private bool Ready()
        {
            if (AuthSession.Instance != null) return true;
            SetStatus("ระบบล็อกอินยังไม่พร้อม");
            return false;
        }

        private void GoToGame()
        {
            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("[Auth] nextSceneName is empty — set it on AuthScreenController.");
                SetBusy(false);
                return;
            }
            SceneManager.LoadScene(nextSceneName);
        }

        private void Fail(string message)
        {
            SetBusy(false);
            SetStatus(string.IsNullOrEmpty(message) ? "เกิดข้อผิดพลาด กรุณาลองใหม่" : message);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (busyIndicator != null) busyIndicator.SetActive(busy);
            if (registerButton != null) registerButton.interactable = !busy;
            if (loginButton != null) loginButton.interactable = !busy;
            if (forgotPasswordButton != null) forgotPasswordButton.interactable = !busy;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? "";
        }

        private static string Trimmed(TMP_InputField field) => field != null ? field.text.Trim() : "";

        private static bool ValidEmail(string email)
            => !string.IsNullOrEmpty(email) && email.Contains("@") && email.Contains(".");
    }
}
