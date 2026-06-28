using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// App-wide auth/session holder (one per app, survives scene loads). Persists only the Firebase
    /// refresh token to <see cref="PlayerPrefs"/> so the child stays logged in across launches; keeps
    /// the live idToken, uid and resolved childId in memory. The other Net clients read
    /// <see cref="BearerHeader"/> and <see cref="ChildId"/> at call time, so a refreshed token is
    /// picked up immediately. UI screens drive the public flows below.
    /// </summary>
    public sealed class AuthSession : MonoBehaviour
    {
        public static AuthSession Instance { get; private set; }

        private const string RefreshTokenKey = "wf_refresh_token";

        [SerializeField] private FirebaseAuthClient auth;
        [SerializeField] private string baseUrl = "http://127.0.0.1:8000/api/v1";

        public string IdToken { get; private set; }
        public string Uid { get; private set; }
        public string ChildId { get; private set; }
        public bool IsNewProfile { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(IdToken) && !string.IsNullOrEmpty(ChildId);
        public bool HasStoredSession => !string.IsNullOrEmpty(PlayerPrefs.GetString(RefreshTokenKey, ""));
        public string BearerHeader => "Bearer " + IdToken;

        private Coroutine _refreshHandle;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (auth == null) auth = GetComponent<FirebaseAuthClient>();
            if (auth == null) auth = FindObjectOfType<FirebaseAuthClient>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ---- public flows (UI calls these) ----

        /// <summary>Register a new adult account, then resolve the child profile via /bootstrap.</summary>
        public void Register(string email, string password, Action<bool, string> onDone)
            => auth.SignUp(email, password, r => OnAuthed(r, onDone));

        /// <summary>Log a returning adult in, then resolve the child profile via /bootstrap.</summary>
        public void Login(string email, string password, Action<bool, string> onDone)
            => auth.SignIn(email, password, r => OnAuthed(r, onDone));

        /// <summary>Silently restore a stored session on launch. onDone(false) → show the login screen.</summary>
        public void TryAutoLogin(Action<bool> onDone)
        {
            string refresh = PlayerPrefs.GetString(RefreshTokenKey, "");
            if (string.IsNullOrEmpty(refresh)) { onDone?.Invoke(false); return; }
            auth.Refresh(refresh, r => OnAuthed(r, (ok, _) => onDone?.Invoke(ok)));
        }

        public void Logout()
        {
            if (_refreshHandle != null) { StopCoroutine(_refreshHandle); _refreshHandle = null; }
            IdToken = Uid = ChildId = null;
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.Save();
        }

        public void SendPasswordReset(string email, Action<bool, string> onDone)
            => auth.SendPasswordReset(email, onDone);

        /// <summary>Save the child's name + birthdate to the dashboard (PUT /me/child).</summary>
        public void SetChildProfile(string displayName, string birthDate, Action<bool, string> onDone)
            => StartCoroutine(SendAuthedJson("PUT", "me/child",
                JsonUtility.ToJson(new ChildProfileBody { displayName = displayName, birthDate = birthDate }),
                onDone, "บันทึกข้อมูลไม่สำเร็จ กรุณาลองใหม่"));

        /// <summary>Link this child to a doctor by their WF-XXXX code (POST /link-by-code).</summary>
        public void LinkByCode(string code, Action<bool, string> onDone)
            => StartCoroutine(SendAuthedJson("POST", "link-by-code",
                JsonUtility.ToJson(new CodeBody { code = code }),
                onDone, "เชื่อมต่อรหัสแพทย์ไม่สำเร็จ กรุณาลองใหม่",
                notFoundError: "ไม่พบรหัสแพทย์นี้ กรุณาตรวจสอบอีกครั้ง"));

        // ---- internals ----

        private void OnAuthed(FirebaseAuthClient.AuthResult r, Action<bool, string> onDone)
        {
            if (!r.Ok) { onDone?.Invoke(false, r.Error); return; }
            StoreTokens(r);
            StartCoroutine(Bootstrap(onDone));
        }

        private void StoreTokens(FirebaseAuthClient.AuthResult r)
        {
            IdToken = r.IdToken;
            Uid = r.Uid;
            if (!string.IsNullOrEmpty(r.RefreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, r.RefreshToken);
                PlayerPrefs.Save();
            }
            ScheduleRefresh(r.ExpiresInSeconds, r.RefreshToken);
        }

        private IEnumerator Bootstrap(Action<bool, string> onDone)
        {
            using (var req = UnityWebRequest.Get($"{baseUrl.TrimEnd('/')}/bootstrap"))
            {
                req.SetRequestHeader("Authorization", BearerHeader);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Auth] bootstrap failed: {req.error} {req.downloadHandler.text}");
                    onDone?.Invoke(false, "เชื่อมต่อเซิร์ฟเวอร์ไม่สำเร็จ กรุณาลองใหม่");
                    yield break;
                }
                var b = JsonUtility.FromJson<BootstrapBrief>(req.downloadHandler.text);
                ChildId = b.childId;
                IsNewProfile = b.isNewProfile;
                Debug.Log($"[Auth] logged in uid={Uid} child={ChildId} new={IsNewProfile}");
                onDone?.Invoke(true, null);
            }
        }

        // Proactive refresh a minute before expiry keeps long sittings authenticated. (On-401 retry
        // is a later hardening; because every Net client reads BearerHeader at call time, a refreshed
        // token is used immediately with no extra plumbing.)
        private void ScheduleRefresh(long expiresInSeconds, string refreshToken)
        {
            if (_refreshHandle != null) StopCoroutine(_refreshHandle);
            if (expiresInSeconds > 0 && !string.IsNullOrEmpty(refreshToken))
                _refreshHandle = StartCoroutine(RefreshLoop(expiresInSeconds, refreshToken));
        }

        private IEnumerator RefreshLoop(long expiresInSeconds, string refreshToken)
        {
            yield return new WaitForSeconds(Mathf.Max(30f, expiresInSeconds - 60f));
            auth.Refresh(refreshToken, r =>
            {
                if (r.Ok) StoreTokens(r);
                else Debug.LogWarning($"[Auth] token refresh failed: {r.Error}");
            });
        }

        private IEnumerator SendAuthedJson(string method, string path, string body,
            Action<bool, string> onDone, string genericError, string notFoundError = null)
        {
            using (var req = new UnityWebRequest($"{baseUrl.TrimEnd('/')}/{path}", method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}")),
                downloadHandler = new DownloadHandlerBuffer(),
            })
            {
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", BearerHeader);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) { onDone?.Invoke(true, null); yield break; }
                Debug.LogWarning($"[Auth] {method} {path} failed ({req.responseCode}): {req.downloadHandler.text}");
                string msg = (req.responseCode == 404 && notFoundError != null) ? notFoundError : genericError;
                onDone?.Invoke(false, msg);
            }
        }

        [Serializable] private struct BootstrapBrief { public string childId; public string displayName; public bool isNewProfile; }
        [Serializable] private struct ChildProfileBody { public string displayName; public string birthDate; }
        [Serializable] private struct CodeBody { public string code; }
    }
}
