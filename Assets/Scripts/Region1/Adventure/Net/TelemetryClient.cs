using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// Durable, fire-and-forget telemetry sink. Every event is journalled to disk first
    /// (<see cref="JournalFile"/>) so nothing is lost if the backend is down or the laptop is
    /// offline mid-demo; a pump coroutine drains the journal FIFO, dropping each line on a 2xx
    /// and backing off on connection/5xx failures. The retry/ordering logic is the unit-tested
    /// <see cref="TelemetryQueue"/>; this MonoBehaviour only owns disk + HTTP.
    /// </summary>
    public sealed class TelemetryClient : MonoBehaviour
    {
        public const string JournalFile = "telemetry_queue.jsonl";

        [SerializeField] private string baseUrl = "http://127.0.0.1:8000/api/v1";
        [SerializeField] private string authorization = "Bearer demo-token";
        [Tooltip("Seconds between pump ticks (also the floor on retry latency).")]
        [SerializeField] private float pumpIntervalSeconds = 1f;

        private readonly TelemetryQueue _queue = new TelemetryQueue();
        private string _journalPath;

        // One journalled request. body is the raw JSON string; JsonUtility escapes it on the
        // way into the envelope and unescapes it on the way out, so the journal stays one JSON
        // object per line.
        [System.Serializable]
        private struct Envelope
        {
            public string method;
            public string path;   // relative to baseUrl, no leading slash
            public string body;   // JSON payload
        }

        private void Awake()
        {
            _journalPath = Path.Combine(Application.persistentDataPath, JournalFile);
            LoadJournal();
            StartCoroutine(Pump());
        }

        // ---- public API ----

        /// <summary>Queue an arbitrary JSON request. Returns immediately; never blocks gameplay.</summary>
        public void PostJson(string relativePath, string jsonBody, string method = "POST")
        {
            var env = new Envelope { method = method, path = relativePath, body = jsonBody ?? "{}" };
            _queue.Enqueue(JsonUtility.ToJson(env));
            PersistJournal();
        }

        /// <summary>Record one word-build attempt (what the child built this try + how long it took).</summary>
        public void PostBuildAttempt(string kidId, string sessionId, string wordId,
            string builtString, string outcome, long buildLatencyMs)
        {
            if (string.IsNullOrEmpty(kidId) || string.IsNullOrEmpty(sessionId)) return;
            string body = JsonUtility.ToJson(new BuildAttemptBody
            {
                wordId = wordId ?? "",
                builtString = builtString ?? "",
                outcome = outcome ?? "",
                buildLatencyMs = buildLatencyMs
            });
            PostJson($"children/{kidId}/sessions/{sessionId}/build-attempts", body);
        }

        /// <summary>Record a progression event ("where they are"): quest cleared, region entered, etc.</summary>
        public void PostProgressEvent(string kidId, string type, string questId = null)
        {
            if (string.IsNullOrEmpty(kidId) || string.IsNullOrEmpty(type)) return;
            // Body is just {type, questId}: the backend's ProgressEvent.region is Optional[int],
            // so we must omit it rather than send an empty string (which would 422).
            string body = JsonUtility.ToJson(new ProgressEventBody { type = type, questId = questId ?? "" });
            PostJson($"children/{kidId}/progress/events", body);
        }

        [System.Serializable]
        private struct BuildAttemptBody
        {
            public string wordId;
            public string builtString;
            public string outcome;
            public long buildLatencyMs;
        }

        [System.Serializable]
        private struct ProgressEventBody
        {
            public string type;
            public string questId;
        }

        // ---- pump ----

        private IEnumerator Pump()
        {
            var wait = new WaitForSeconds(pumpIntervalSeconds);
            while (true)
            {
                if (_queue.TryPeek(Time.realtimeSinceStartup, out string line))
                    yield return SendLine(line);
                yield return wait;
            }
        }

        private IEnumerator SendLine(string line)
        {
            Envelope env;
            try { env = JsonUtility.FromJson<Envelope>(line); }
            catch
            {
                // Unparseable journal line is poison — drop it so it can't wedge the FIFO.
                Debug.LogWarning($"[Telemetry] dropping unparseable journal line: {line}");
                _queue.OnSent();
                PersistJournal();
                yield break;
            }

            string url = $"{baseUrl.TrimEnd('/')}/{env.path}";
            byte[] payload = Encoding.UTF8.GetBytes(env.body ?? "{}");
            using (var req = new UnityWebRequest(url, string.IsNullOrEmpty(env.method) ? "POST" : env.method))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(authorization))
                    req.SetRequestHeader("Authorization", authorization.Trim());
                yield return req.SendWebRequest();

                long code = req.responseCode;
                if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
                {
                    _queue.OnSent();
                    PersistJournal();
                }
                else if (code >= 400 && code < 500)
                {
                    // Client error (e.g. 404 unseeded word, 422 bad shape): retrying can't help,
                    // so drop it rather than block every later event behind a poison message.
                    Debug.LogWarning($"[Telemetry] {code} on {env.path} — dropping (won't retry): {req.downloadHandler.text}");
                    _queue.OnSent();
                    PersistJournal();
                }
                else
                {
                    // Connection failure / 5xx: keep the line, back off, try again later.
                    _queue.OnFailed(Time.realtimeSinceStartup);
                }
            }
        }

        // ---- disk journal ----

        private void LoadJournal()
        {
            try { if (File.Exists(_journalPath)) _queue.Load(File.ReadAllText(_journalPath)); }
            catch (System.Exception e) { Debug.LogWarning($"[Telemetry] journal load failed: {e.Message}"); }
        }

        private void PersistJournal()
        {
            try { File.WriteAllText(_journalPath, _queue.Serialize()); }
            catch (System.Exception e) { Debug.LogWarning($"[Telemetry] journal write failed: {e.Message}"); }
        }

        // Belt-and-suspenders: the pump already persists, but flush on background/quit too so the
        // last events survive an app kill that interrupts an in-flight tick.
        private void OnApplicationPause(bool paused) { if (paused) PersistJournal(); }
        private void OnApplicationQuit() => PersistJournal();
    }
}
