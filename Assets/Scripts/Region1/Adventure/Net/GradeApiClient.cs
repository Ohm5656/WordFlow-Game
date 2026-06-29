using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Net
{
    /// <summary>
    /// Records a short mic clip, encodes 16kHz mono WAV, and POSTs multipart to /grade.
    /// Fire-and-forget: never blocks gameplay. Response logged + optional callback.
    /// </summary>
    public sealed class GradeApiClient : MonoBehaviour
    {
        [SerializeField] private string gradeUrl = "http://127.0.0.1:8000/api/v1/grade";
        [SerializeField] private string authorization = "Bearer demo-token";
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private int maxSeconds = 5; // matches the controller's 5s mic auto-stop
        [SerializeField] private float deviceStallGrace = 1f; // matches MagicStonePuzzleController's proven mic stall-detection window

        private string _device;
        private AudioClip _recording;
        private bool _isRecording;

        public bool IsRecording => _isRecording;

        /// <summary>Override the serialized endpoint/token at runtime so a scene/controller can
        /// point this client at its configured /grade URL (e.g. :8001). Blank args keep the
        /// serialized defaults.</summary>
        public void Configure(string url, string auth)
        {
            if (!string.IsNullOrWhiteSpace(url)) gradeUrl = url;
            if (!string.IsNullOrWhiteSpace(auth)) authorization = auth;
        }

        public struct GradeContext
        {
            public string targetWordId;
            public string childId;
            public string questId;   // optional
            public string sessionId; // optional
            public string sceneId;   // optional (forward-compat; backend ignores)
            public string outcomeTag; // "non_word" | "wrong_word" | null (forward-compat)
            public long buildLatencyMs; // forward-compat KPI; 0 = unset (backend ignores for now)
        }

        /// <summary>Begin capturing a mic clip. No mic/permission -> stays not-recording (IsRecording==false).</summary>
        public void StartRecording() => StartCoroutine(StartRecordingRoutine());

        private IEnumerator StartRecordingRoutine()
        {
#if UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL
            // Runtime permission prompts only exist on these platforms; matches the
            // MagicStonePuzzleController guard so Standalone doesn't depend on this API.
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("[GradeApiClient] Mic permission not granted; recording skipped (invisible — play continues).");
                _isRecording = false;
                yield break;
            }
#endif
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.Log("[GradeApiClient] No mic device found; recording skipped (invisible — play continues).");
                _isRecording = false;
                yield break;
            }

            _device = Microphone.devices[0];
            _recording = Microphone.Start(_device, false, maxSeconds, sampleRate);
            if (_recording == null)
            {
                Debug.Log("[GradeApiClient] Microphone.Start returned null; recording skipped (invisible — play continues).");
                _device = null;
                _isRecording = false;
                yield break;
            }

            // Stall guard (ported from MagicStonePuzzleController.RecordingRoutine): some
            // drivers/devices hand back a valid clip but never advance playback position
            // (exclusive-mode conflict, disabled device, unsupported sample rate). Confirm
            // real capture is happening before committing the encounter's mic window to it —
            // otherwise StopAndGrade silently gets a 0-sample clip after wasting maxSeconds.
            float waitUntil = Time.realtimeSinceStartup + deviceStallGrace;
            while (Microphone.GetPosition(_device) <= 0 && Time.realtimeSinceStartup < waitUntil)
                yield return null;

            if (Microphone.GetPosition(_device) <= 0)
            {
                Microphone.End(_device);
                Debug.Log($"[GradeApiClient] Mic device '{_device}' did not advance within {deviceStallGrace}s (stalled); recording skipped (invisible — play continues).");
                _recording = null;
                _device = null;
                _isRecording = false;
                yield break;
            }

            _isRecording = true;
        }

        /// <summary>Stop the active recording and POST it to /grade (background). Safe if not recording.</summary>
        public void StopAndGrade(GradeContext ctx, Action<GradeResponse> onResult = null)
            => StartCoroutine(StopAndGradeRoutine(ctx, onResult));

        private IEnumerator StopAndGradeRoutine(GradeContext ctx, Action<GradeResponse> onResult)
        {
            if (!_isRecording || _recording == null)
            {
                _isRecording = false;
                onResult?.Invoke(null);
                yield break;
            }
            int sampleFrames = Mathf.Clamp(Microphone.GetPosition(_device), 0, _recording.samples);
            Microphone.End(_device);
            _isRecording = false;

            byte[] wav = EncodeClip(_recording, sampleFrames);
            if (wav == null)
            {
                Debug.Log("[GradeApiClient] Empty recording; /grade skipped (invisible — play continues).");
                onResult?.Invoke(null);
                yield break;
            }
            yield return PostGrade(wav, ctx, onResult);
        }

        private IEnumerator PostGrade(byte[] wav, GradeContext ctx, Action<GradeResponse> onResult)
        {
            WWWForm form = new WWWForm();
            form.AddBinaryData("audio", wav, "attempt.wav", "audio/wav");
            form.AddField("targetWordId", ctx.targetWordId ?? "");
            form.AddField("childId", ctx.childId ?? "");
            if (!string.IsNullOrWhiteSpace(ctx.questId)) form.AddField("questId", ctx.questId);
            if (!string.IsNullOrWhiteSpace(ctx.sessionId)) form.AddField("sessionId", ctx.sessionId);
            if (!string.IsNullOrWhiteSpace(ctx.sceneId)) form.AddField("sceneId", ctx.sceneId);
            if (!string.IsNullOrWhiteSpace(ctx.outcomeTag)) form.AddField("outcome", ctx.outcomeTag);
            if (ctx.buildLatencyMs > 0) form.AddField("buildLatencyMs", ctx.buildLatencyMs.ToString());

            using (UnityWebRequest req = UnityWebRequest.Post(gradeUrl.Trim(), form))
            {
                if (!string.IsNullOrWhiteSpace(authorization))
                    req.SetRequestHeader("Authorization", authorization.Trim());
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GradeApiClient] /grade failed: {req.error} {req.downloadHandler.text}");
                    onResult?.Invoke(null);
                    yield break;
                }

                string body = req.downloadHandler.text;
                Debug.Log($"[GradeApiClient] /grade 200: {body}");
                GradeResponse parsed = null;
                try { parsed = JsonUtility.FromJson<GradeResponse>(body); }
                catch (Exception e) { Debug.LogWarning($"[GradeApiClient] parse failed: {e.Message}"); }
                onResult?.Invoke(parsed);
            }
        }

        /// <summary>AudioClip overload of WavEncoder.Encode (kept out of the tested core).</summary>
        private static byte[] EncodeClip(AudioClip clip, int sampleFrames)
        {
            if (clip == null || sampleFrames <= 0) return null;
            int channels = Mathf.Max(1, clip.channels);
            int safeFrames = Mathf.Clamp(sampleFrames, 0, clip.samples);
            float[] samples = new float[safeFrames * channels];
            clip.GetData(samples, 0);
            return WavEncoder.Encode(samples, channels, clip.frequency);
        }
    }
}
