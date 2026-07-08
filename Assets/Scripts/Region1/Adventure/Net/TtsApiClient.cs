using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// Fetches a voiced line from the backend /tts endpoint and decodes it to an
    /// AudioClip. Mirrors GradeApiClient (same host + bearer auth). In-memory cached
    /// by lineId so repeat plays are instant. Null-safe: any failure -> callback(null),
    /// and the caller shows the frame silently.
    /// </summary>
    public sealed class TtsApiClient : MonoBehaviour
    {
        [SerializeField] private string authorization = "Bearer demo-token";
        [SerializeField, Min(1f)] private float requestTimeoutSeconds = 8f;

        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, List<Action<AudioClip>>> _pending =
            new Dictionary<string, List<Action<AudioClip>>>();

        /// <summary>GET /tts?line_id=. onResult(AudioClip|null) when done (or on failure).</summary>
        public void GetLine(string lineId, Action<AudioClip> onResult)
        {
            if (string.IsNullOrWhiteSpace(lineId)) { onResult?.Invoke(null); return; }
            if (_cache.TryGetValue(lineId, out var cached)) { onResult?.Invoke(cached); return; }

            // Several scene components prefetch the same line during Awake. Share one request so
            // they cannot exhaust Unity's HTTP connections or hammer the gateway with duplicates.
            if (_pending.TryGetValue(lineId, out var callbacks))
            {
                callbacks.Add(onResult);
                return;
            }

            _pending[lineId] = new List<Action<AudioClip>> { onResult };
            StartCoroutine(GetLineRoutine(lineId));
        }

        private IEnumerator GetLineRoutine(string lineId)
        {
            string url = $"{BackendConfig.BaseUrl}/tts?line_id={UnityWebRequest.EscapeURL(lineId)}";
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                BackendConfig.Prepare(req);
                req.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
                string bearer = ResolveBearer();
                if (!string.IsNullOrWhiteSpace(bearer))
                    req.SetRequestHeader("Authorization", bearer);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[TtsApiClient] /tts '{lineId}' failed: HTTP {req.responseCode} {req.error}");
                    Complete(lineId, null);
                    yield break;
                }

                AudioClip clip = null;
                try { clip = DownloadHandlerAudioClip.GetContent(req); }
                catch (Exception e) { Debug.LogWarning($"[TtsApiClient] decode '{lineId}' failed: {e.Message}"); }
                if (clip != null) { clip.name = lineId; _cache[lineId] = clip; }
                Complete(lineId, clip);
            }
        }

        private void Complete(string lineId, AudioClip clip)
        {
            if (!_pending.TryGetValue(lineId, out var callbacks)) return;
            _pending.Remove(lineId);
            foreach (var callback in callbacks) callback?.Invoke(clip);
        }

        private void OnDisable()
        {
            // Disabling a scene object stops its coroutines. Release every waiter so owl/gameplay
            // flows never wait forever for a callback that can no longer arrive.
            foreach (var callbacks in _pending.Values)
                foreach (var callback in callbacks) callback?.Invoke(null);
            _pending.Clear();
        }

        private string ResolveBearer()
        {
            if (AuthSession.Instance != null && !string.IsNullOrEmpty(AuthSession.Instance.IdToken))
                return AuthSession.Instance.BearerHeader;

            return string.IsNullOrWhiteSpace(authorization) ? null : authorization.Trim();
        }
    }
}
