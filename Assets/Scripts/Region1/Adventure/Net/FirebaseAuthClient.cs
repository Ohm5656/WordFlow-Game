using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// Thin Firebase Authentication REST client — no Firebase Unity SDK. Each call runs a coroutine
    /// and reports back via callback; it never blocks gameplay. The Web API key is configuration,
    /// not a secret (the same key ships in the web client), so embedding it here is safe.
    /// Failures come back with <see cref="AuthResult.Error"/> already mapped to a Thai, user-facing
    /// message; <see cref="AuthSession"/> orchestrates these into login/register flows.
    /// </summary>
    public sealed class FirebaseAuthClient : MonoBehaviour
    {
        // project wordflow-61d5a (web NEXT_PUBLIC_FIREBASE_API_KEY). Safe to embed; override in Inspector if needed.
        [SerializeField] private string apiKey = "AIzaSyBBZexqnzSgu0EGto0TeAp3vVVyYNFGfKM";

        private const string IdentityBase = "https://identitytoolkit.googleapis.com/v1/accounts";
        private const string SecureTokenUrl = "https://securetoken.googleapis.com/v1/token";

        /// <summary>Outcome of an auth call. On failure, <see cref="Error"/> is a Thai message.</summary>
        public sealed class AuthResult
        {
            public bool Ok;
            public string IdToken;
            public string RefreshToken;
            public string Uid;
            public long ExpiresInSeconds;
            public string Error;
            public string FirebaseCode;
            public long HttpStatus;
        }

        public void SignUp(string email, string password, Action<AuthResult> onResult)
            => StartCoroutine(PasswordRoutine("signUp", email, password, onResult));

        public void SignIn(string email, string password, Action<AuthResult> onResult)
            => StartCoroutine(PasswordRoutine("signInWithPassword", email, password, onResult));

        public void Refresh(string refreshToken, Action<AuthResult> onResult)
            => StartCoroutine(RefreshRoutine(refreshToken, onResult));

        public void SendPasswordReset(string email, Action<bool, string> onResult)
            => StartCoroutine(ResetRoutine(email, onResult));

        // ---- routines ----

        private IEnumerator PasswordRoutine(string verb, string email, string password, Action<AuthResult> onResult)
        {
            string url = $"{IdentityBase}:{verb}?key={apiKey}";
            string body = JsonUtility.ToJson(new PasswordRequest
            {
                email = email, password = password, returnSecureToken = true,
            });
            using (var req = JsonPost(url, body))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { onResult?.Invoke(Failure(req, verb)); yield break; }
                var p = JsonUtility.FromJson<PasswordResponse>(req.downloadHandler.text);
                onResult?.Invoke(new AuthResult
                {
                    Ok = true, IdToken = p.idToken, RefreshToken = p.refreshToken,
                    Uid = p.localId, ExpiresInSeconds = ParseLong(p.expiresIn),
                });
            }
        }

        private IEnumerator RefreshRoutine(string refreshToken, Action<AuthResult> onResult)
        {
            // The securetoken endpoint takes form-encoded input and returns snake_case fields,
            // unlike the identitytoolkit endpoints above — hence the separate response shape.
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "refresh_token");
            form.AddField("refresh_token", refreshToken);
            using (var req = UnityWebRequest.Post($"{SecureTokenUrl}?key={apiKey}", form))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { onResult?.Invoke(Failure(req, "refresh")); yield break; }
                var p = JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
                onResult?.Invoke(new AuthResult
                {
                    Ok = true, IdToken = p.id_token, RefreshToken = p.refresh_token,
                    Uid = p.user_id, ExpiresInSeconds = ParseLong(p.expires_in),
                });
            }
        }

        private IEnumerator ResetRoutine(string email, Action<bool, string> onResult)
        {
            string body = JsonUtility.ToJson(new ResetRequest { requestType = "PASSWORD_RESET", email = email });
            using (var req = JsonPost($"{IdentityBase}:sendOobCode?key={apiKey}", body))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    var failure = Failure(req, "password reset");
                    onResult?.Invoke(false, MapPasswordResetError(failure.FirebaseCode, failure.Error));
                }
                else onResult?.Invoke(true, null);
            }
        }

        // ---- helpers ----

        private static UnityWebRequest JsonPost(string url, string body)
        {
            var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        private static AuthResult Failure(UnityWebRequest req, string context)
        {
            string raw = req.downloadHandler != null ? req.downloadHandler.text : null;
            string code = null;
            if (!string.IsNullOrEmpty(raw))
            {
                try { code = JsonUtility.FromJson<ErrorEnvelope>(raw).error.message; } catch { /* not JSON */ }
            }
            Debug.LogWarning($"[Auth] {context} failed: http={req.responseCode} result={req.result} firebase={code ?? "none"} unity={req.error ?? "none"}");
            return new AuthResult { Ok = false, Error = MapError(code), FirebaseCode = code, HttpStatus = req.responseCode };
        }

        // Firebase returns machine codes (e.g. "EMAIL_EXISTS", sometimes suffixed); map the ones a
        // caregiver can act on to Thai, and fall back to a generic message for everything else.
        private static string MapError(string firebaseCode)
        {
            if (string.IsNullOrEmpty(firebaseCode))
                return "เชื่อมต่อไม่สำเร็จ กรุณาตรวจสอบอินเทอร์เน็ตแล้วลองใหม่";
            if (firebaseCode.StartsWith("EMAIL_EXISTS")) return "อีเมลนี้ถูกใช้งานแล้ว";
            if (firebaseCode.StartsWith("EMAIL_NOT_FOUND") || firebaseCode.StartsWith("INVALID_PASSWORD")
                || firebaseCode.StartsWith("INVALID_LOGIN_CREDENTIALS")) return "อีเมลหรือรหัสผ่านไม่ถูกต้อง";
            if (firebaseCode.StartsWith("INVALID_EMAIL")) return "รูปแบบอีเมลไม่ถูกต้อง";
            if (firebaseCode.StartsWith("WEAK_PASSWORD")) return "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร";
            if (firebaseCode.StartsWith("TOO_MANY_ATTEMPTS")) return "พยายามหลายครั้งเกินไป กรุณารอสักครู่แล้วลองใหม่";
            return "เกิดข้อผิดพลาด กรุณาลองใหม่";
        }

        private static string MapPasswordResetError(string firebaseCode, string fallback)
        {
            if (string.IsNullOrEmpty(firebaseCode))
                return string.IsNullOrEmpty(fallback) ? MapError(firebaseCode) : fallback;
            if (firebaseCode.StartsWith("EMAIL_NOT_FOUND"))
                return "ไม่พบบัญชีสำหรับอีเมลนี้";
            if (firebaseCode.StartsWith("INVALID_EMAIL"))
                return "รูปแบบอีเมลไม่ถูกต้อง";
            if (firebaseCode.StartsWith("OPERATION_NOT_ALLOWED"))
                return "ยังไม่ได้เปิดใช้งานการเข้าสู่ระบบด้วยอีเมลใน Firebase";
            if (firebaseCode.StartsWith("TOO_MANY_ATTEMPTS") || firebaseCode.StartsWith("RESET_PASSWORD_EXCEED_LIMIT"))
                return "ส่งคำขอหลายครั้งเกินไป กรุณารอสักครู่แล้วลองใหม่";
            if (firebaseCode.StartsWith("API_KEY_INVALID") || firebaseCode.StartsWith("INVALID_API_KEY"))
                return "ตั้งค่า Firebase API key ไม่ถูกต้อง";
            return string.IsNullOrEmpty(fallback) ? MapError(firebaseCode) : fallback;
        }

        private static long ParseLong(string s) => long.TryParse(s, out long v) ? v : 0;

        [Serializable] private struct PasswordRequest { public string email; public string password; public bool returnSecureToken; }
        [Serializable] private struct PasswordResponse { public string idToken; public string refreshToken; public string localId; public string expiresIn; }
        [Serializable] private struct RefreshResponse { public string id_token; public string refresh_token; public string user_id; public string expires_in; }
        [Serializable] private struct ResetRequest { public string requestType; public string email; }
        [Serializable] private struct ErrorEnvelope { public ErrorBody error; }
        [Serializable] private struct ErrorBody { public int code; public string message; }
    }
}
