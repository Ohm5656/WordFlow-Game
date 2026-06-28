using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.UI
{
    /// <summary>
    /// In-game settings panel (Thai) for an adult: enter or change the doctor code (WF-XXXX) to link
    /// the child to a doctor, and log out. Linking is optional and can be done anytime; entering the
    /// same code again is a harmless no-op (the backend is idempotent). Wire the serialized fields to
    /// a Canvas/panel in the editor.
    /// </summary>
    public sealed class DoctorLinkController : MonoBehaviour
    {
        [SerializeField] private TMP_InputField codeField;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button linkButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private string authSceneName = "Login";   // scene to return to after logout

        private bool _busy;

        private void Awake()
        {
            if (linkButton != null) linkButton.onClick.AddListener(OnLink);
            if (logoutButton != null) logoutButton.onClick.AddListener(OnLogout);
        }

        public void OnLink()
        {
            if (_busy) return;
            if (AuthSession.Instance == null || !AuthSession.Instance.IsLoggedIn)
            {
                SetStatus("กรุณาเข้าสู่ระบบก่อน");
                return;
            }
            string code = codeField != null ? codeField.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrEmpty(code)) { SetStatus("กรุณากรอกรหัสแพทย์ (WF-XXXX)"); return; }

            SetBusy(true);
            SetStatus("กำลังเชื่อมต่อ…");
            AuthSession.Instance.LinkByCode(code, (ok, err) =>
            {
                SetBusy(false);
                SetStatus(ok ? "เชื่อมต่อกับแพทย์เรียบร้อยแล้ว" : err);
            });
        }

        public void OnLogout()
        {
            if (AuthSession.Instance != null) AuthSession.Instance.Logout();
            if (!string.IsNullOrEmpty(authSceneName)) SceneManager.LoadScene(authSceneName);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (linkButton != null) linkButton.interactable = !busy;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? "";
        }
    }
}
