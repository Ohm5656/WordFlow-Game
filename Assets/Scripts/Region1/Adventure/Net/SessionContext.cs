using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// Owns the play "sitting": opens a backend session at app start so every grade/build-attempt
    /// groups under one <see cref="SessionId"/> ("where they are"), accumulates the sitting's
    /// aggregates, and closes the session on quit (durably, via <see cref="TelemetryClient"/>) to
    /// feed the doctor/parent dashboard. One per scene; reachable as <see cref="Instance"/>.
    /// </summary>
    public sealed class SessionContext : MonoBehaviour
    {
        public static SessionContext Instance { get; private set; }

        [SerializeField] private string kidId = "kid_demo_01";
        [SerializeField] private int island = 1;
        [SerializeField] private string baseUrl = "http://127.0.0.1:8000/api/v1";
        [SerializeField] private string authorization = "Bearer demo-token";
        [SerializeField] private TelemetryClient telemetry;

        // A live login always wins. The serialized value stays available for direct scene testing
        // in the Editor, where the Login scene (and therefore AuthSession) may be skipped.
        public string KidId =>
            AuthSession.Instance != null && !string.IsNullOrEmpty(AuthSession.Instance.ChildId)
                ? AuthSession.Instance.ChildId
                : kidId;
        public string SessionId { get; private set; }

        // Sitting aggregates (sent on close).
        private int _wordsAttempted;
        private int _level;
        private long _latencySumMs;
        private int _latencyCount;
        private float _parSum;
        private int _parCount;

        private void Awake()
        {
            // One backend session per app sitting: persist across scene loads and dedupe. Without
            // this, every scene opened its OWN session (each scene carries a SessionContext), so a
            // multi-scene flow like CutScene_bear -> practice/reference_forest created an extra
            // empty 0%/0s session that showed as a second dashboard row per recording. word_build_
            // paa_polished never hit this because it stays in one scene for the whole encounter.
            // This GameObject also carries the TelemetryClient, so persisting it keeps the durable
            // queue alive across scenes too.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // a session is already open for this sitting — keep it
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SessionId = null;
            if (telemetry == null) telemetry = FindObjectOfType<TelemetryClient>();
        }

        private void Start() => StartCoroutine(OpenSession());

        private IEnumerator OpenSession()
        {
            string kid = KidId;
            if (string.IsNullOrEmpty(kid))
            {
                Debug.LogWarning("[Session] no child id (not logged in?) — session not opened.");
                yield break;
            }

            string url = $"{baseUrl.TrimEnd('/')}/children/{kid}/sessions";
            byte[] payload = Encoding.UTF8.GetBytes($"{{\"island\":{island}}}");
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                string bearer = ResolveBearer();
                if (!string.IsNullOrWhiteSpace(bearer))
                    req.SetRequestHeader("Authorization", bearer);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // No session -> attempts simply stay ungrouped; play is unaffected (invisible).
                    Debug.LogWarning($"[Session] open failed: {req.error} {req.downloadHandler.text}");
                    yield break;
                }
                var parsed = JsonUtility.FromJson<OpenResponse>(req.downloadHandler.text);
                SessionId = parsed != null ? parsed.sessionId : null;
                Debug.Log($"[Session] opened {SessionId} for {kid}");
            }
        }

        // ---- aggregate recorders (called by the controller) ----
        // Each recorder pushes the sitting's running aggregates immediately (see PushAggregates).

        public void RecordAttempt(long buildLatencyMs)
        {
            _wordsAttempted++;
            if (buildLatencyMs > 0) { _latencySumMs += buildLatencyMs; _latencyCount++; }
            PushAggregates();
        }

        public void RecordGrade(float par)
        {
            _parSum += par;
            _parCount++;
            PushAggregates();
        }

        public void RecordCleared() { _level++; PushAggregates(); }

        // ---- aggregate push ----

        // PATCH the sitting's running aggregates so the dashboard always reflects real data *so
        // far*. We push after every attempt/grade/clear (not only on quit) because the push goes
        // through the durable queue, which flushes within ~1s: that way a quit/crash/interruption
        // leaves the session showing its true partial numbers. The old code pushed only from
        // OnApplicationQuit, but the process died before the queue's next pump tick could send it,
        // so an interrupted session kept the 0/0 it was created with and rendered as a bogus 0%
        // "Fail" until the next launch flushed the queue.
        private void PushAggregates()
        {
            if (telemetry == null || string.IsNullOrEmpty(SessionId)) return;
            // Dashboard contract: avgAccuracy is a percentage (0-100; computeReadingLevel: <60 = Fail)
            // and avgLatency is in seconds (mastery gate is <= 2.5s). PAR is a 0-1 fraction and our
            // latency is accumulated in ms, so convert both here before PATCHing the session.
            float avgAccuracy = _parCount > 0 ? (_parSum / _parCount) * 100f : 0f;
            float avgLatency = _latencyCount > 0 ? (_latencySumMs / (float)_latencyCount) / 1000f : 0f;
            string body = JsonUtility.ToJson(new CloseBody
            {
                avgAccuracy = avgAccuracy,
                avgLatency = avgLatency,
                wordsAttempted = _wordsAttempted,
                level = _level.ToString()   // backend SessionEndRequest.level is a string
            });
            telemetry.PostJson($"children/{KidId}/sessions/{SessionId}", body, "PATCH");
        }

        private string ResolveBearer()
        {
            if (AuthSession.Instance != null && !string.IsNullOrEmpty(AuthSession.Instance.IdToken))
                return AuthSession.Instance.BearerHeader;

            return string.IsNullOrWhiteSpace(authorization) ? null : authorization.Trim();
        }

        // Final flush on quit (redundant safety net — the per-attempt pushes already keep the
        // doc current; this just catches any aggregate change since the last recorder call).
        private void OnApplicationQuit() => PushAggregates();
        private void OnDestroy() { if (Instance == this) Instance = null; }

        [System.Serializable] private class OpenResponse { public string sessionId; public string startedAt; }

        [System.Serializable]
        private struct CloseBody
        {
            public float avgAccuracy;
            public float avgLatency;
            public int wordsAttempted;
            public string level;
        }
    }
}
