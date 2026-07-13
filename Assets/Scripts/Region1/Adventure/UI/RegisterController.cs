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
