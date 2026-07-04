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

        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        /// <summary>GET /tts?line_id=. onResult(AudioClip|null) when done (or on failure).</summary>
        public void GetLine(string lineId, Action<AudioClip> onResult)
        {
            if (string.IsNullOrWhiteSpace(lineId)) { onResult?.Invoke(null); return; }
            if (_cache.TryGetValue(lineId, out var cached)) { onResult?.Invoke(cached); return; }
            StartCoroutine(GetLineRoutine(lineId, onResult));
        }

        private IEnumerator GetLineRoutine(string lineId, Action<AudioClip> onResult)
        {
            string url = $"{BackendConfig.BaseUrl}/tts?line_id={UnityWebRequest.EscapeURL(lineId)}";
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                BackendConfig.Prepare(req);
                string bearer = ResolveBearer();
                if (!string.IsNullOrWhiteSpace(bearer))
                    req.SetRequestHeader("Authorization", bearer);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[TtsApiClient] /tts '{lineId}' failed: {req.error}");
                    onResult?.Invoke(null);
                    yield break;
                }

                AudioClip clip = null;
                try { clip = DownloadHandlerAudioClip.GetContent(req); }
                catch (Exception e) { Debug.LogWarning($"[TtsApiClient] decode '{lineId}' failed: {e.Message}"); }
                if (clip != null) { clip.name = lineId; _cache[lineId] = clip; }
                onResult?.Invoke(clip);
            }
        }

        private string ResolveBearer()
        {
            if (AuthSession.Instance != null && !string.IsNullOrEmpty(AuthSession.Instance.IdToken))
                return AuthSession.Instance.BearerHeader;

            return string.IsNullOrWhiteSpace(authorization) ? null : authorization.Trim();
        }
    }
}
