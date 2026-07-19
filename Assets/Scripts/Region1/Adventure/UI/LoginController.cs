using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// Login scene. Email + password → AuthSession.Login. Auto-login now lives in first_page.
    /// Links to the Register and ForgotPassword scenes.
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
            if (toRegisterLink != null) toRegisterLink.interactable = !busy;
            if (toForgotPasswordLink != null) toForgotPasswordLink.interactable = !busy;
        }
    }
}
