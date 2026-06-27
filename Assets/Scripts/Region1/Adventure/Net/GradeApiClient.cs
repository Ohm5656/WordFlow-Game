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
        // Recording buffer length. MUST be longer than the controller's mic window (micSeconds),
        // because Microphone.Start(loop:false) auto-stops at maxSeconds and GetPosition() then
        // returns 0 -> "Empty recording". Keep headroom so the mic is still recording on StopAndGrade.
        [SerializeField] private int maxSeconds = 10;

        private string _device;
        private AudioClip _recording;
        private bool _isRecording;

        public bool IsRecording => _isRecording;

        /// <summary>Override the endpoint/auth at runtime (e.g. from a scene that keeps its own
        /// URL field). Empty/whitespace values are ignored so defaults survive.</summary>
        public void Configure(string url, string auth)
        {
            if (!string.IsNullOrWhiteSpace(url)) gradeUrl = url.Trim();
            if (!string.IsNullOrWhiteSpace(auth)) authorization = auth.Trim();
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
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) ||
                Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.Log("[GradeApiClient] No mic / permission; recording skipped (invisible — play continues).");
                _isRecording = false;
                yield break;
            }
            _device = Microphone.devices[0];
            _recording = Microphone.Start(_device, false, maxSeconds, sampleRate);
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
