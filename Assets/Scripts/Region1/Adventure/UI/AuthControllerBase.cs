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
