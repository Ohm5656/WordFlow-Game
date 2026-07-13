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
