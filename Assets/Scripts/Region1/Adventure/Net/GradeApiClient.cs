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
        [SerializeField] private string authorization = "Bearer demo-token";
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private string preferredDeviceName = "";
        [SerializeField] private bool useDefaultInputDevice = true;
        [SerializeField] private int requestTimeoutSeconds = 120;
        [SerializeField] private bool prewarmOnAwake = true;
        [SerializeField] private float prewarmDelaySeconds = 0.35f;
        [SerializeField] private bool verboseLogging = false;
        // Recording buffer length. MUST be longer than the controller's mic window (maxRecordingSeconds),
        // because Microphone.Start(loop:false) auto-stops at maxSeconds and GetPosition() then returns 0
        // -> "Empty recording". Keep headroom so the mic is still recording on StopAndGrade.
        [SerializeField] private int maxSeconds = 10;
        [SerializeField] private float deviceStallGrace = 1f; // matches MagicStonePuzzleController's proven mic stall-detection window

        private string _device;
        private AudioClip _recording;
        private bool _isRecording;
        private Coroutine _startRoutine;
        private Coroutine _prewarmRoutine;
        private string _prewarmDevice;
        private float _startedAtRealtime;
        private static bool _micPrewarmed;

        public bool IsRecording => _isRecording;

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

        private void Awake()
        {
            if (prewarmOnAwake && !_micPrewarmed && Application.isPlaying)
                _prewarmRoutine = StartCoroutine(PrewarmRoutine());
        }

        /// <summary>Begin capturing a mic clip. No mic/permission -> stays not-recording (IsRecording==false).</summary>
        public void StartRecording()
        {
            CancelPrewarm();

            if (_isRecording || _startRoutine != null)
            {
                LogVerbose("[GradeApiClient] StartRecording ignored; recording is already starting/active.");
                return;
            }

            _startRoutine = StartCoroutine(StartRecordingRoutine());
        }

        private IEnumerator StartRecordingRoutine()
        {
            string[] devices = Microphone.devices ?? Array.Empty<string>();
            LogVerbose($"[GradeApiClient] StartRecording requested. devices={DescribeDevices(devices)} sampleRate={sampleRate} preferred='{preferredDeviceName}' useDefault={useDefaultInputDevice}");
#if UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL
            // Runtime permission prompts only exist on these platforms; matches the
            // MagicStonePuzzleController guard so Standalone doesn't depend on this API.
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("[GradeApiClient] Mic permission not granted; recording skipped (invisible — play continues).");
                ResetRecordingState();
                _startRoutine = null;
                yield break;
            }
#endif
            if (devices.Length == 0)
            {
                Debug.Log("[GradeApiClient] No mic device found; recording skipped (invisible — play continues).");
                ResetRecordingState();
                _startRoutine = null;
                yield break;
            }

            _device = SelectDevice(devices);
            int frequency = ResolveSampleRate(_device);
            LogVerbose($"[GradeApiClient] Microphone.Start begin device={FormatDevice(_device)} frequency={frequency} seconds={maxSeconds}");
            try
            {
                _recording = Microphone.Start(_device, false, Mathf.Max(1, maxSeconds), frequency);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GradeApiClient] Microphone.Start threw for device={FormatDevice(_device)}: {e.GetType().Name}: {e.Message}");
                ResetRecordingState();
                _startRoutine = null;
                yield break;
            }
            if (_recording == null)
            {
                Debug.Log("[GradeApiClient] Microphone.Start returned null; recording skipped (invisible — play continues).");
                ResetRecordingState();
                _startRoutine = null;
                yield break;
            }

            LogVerbose($"[GradeApiClient] Microphone.Start ok device={FormatDevice(_device)} clipFrequency={_recording.frequency} channels={_recording.channels} samples={_recording.samples}");

            // Stall guard (ported from MagicStonePuzzleController.RecordingRoutine): some
            // drivers/devices hand back a valid clip but never advance playback position
            // (exclusive-mode conflict, disabled device, unsupported sample rate). Confirm
            // real capture is happening before committing the encounter's mic window to it —
            // otherwise StopAndGrade silently gets a 0-sample clip after wasting maxSeconds.
            float waitUntil = Time.realtimeSinceStartup + deviceStallGrace;
            int position = SafeGetPosition(_device);
            while (position <= 0 && Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
                position = SafeGetPosition(_device);
            }

            if (position <= 0)
            {
                SafeEnd(_device);
                Debug.Log($"[GradeApiClient] Mic device '{_device}' did not advance within {deviceStallGrace}s (stalled); recording skipped (invisible — play continues).");
                ResetRecordingState();
                _startRoutine = null;
                yield break;
            }

            _startedAtRealtime = Time.realtimeSinceStartup;
            _isRecording = true;
            _startRoutine = null;
            LogVerbose($"[GradeApiClient] Recording active device={FormatDevice(_device)} position={position}");
        }

        /// <summary>Stop the active recording and POST it to /grade (background). Safe if not recording.</summary>
        public void StopAndGrade(GradeContext ctx, Action<GradeResponse> onResult = null)
            => StartCoroutine(StopAndGradeRoutine(ctx, onResult));

        private IEnumerator StopAndGradeRoutine(GradeContext ctx, Action<GradeResponse> onResult)
        {
            if (!_isRecording || _recording == null)
            {
                LogVerbose("[GradeApiClient] StopAndGrade skipped; no active recording.");
                _isRecording = false;
                onResult?.Invoke(null);
                yield break;
            }

            int sampleFrames = Mathf.Clamp(SafeGetPosition(_device), 0, _recording.samples);
            if (sampleFrames <= 0 && _startedAtRealtime > 0f)
            {
                float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - _startedAtRealtime);
                sampleFrames = Mathf.Clamp(Mathf.RoundToInt(elapsed * _recording.frequency), 0, _recording.samples);
                Debug.LogWarning($"[GradeApiClient] Microphone position was 0 at stop; estimated {sampleFrames} frames from elapsed time.");
            }

            SafeEnd(_device);
            _isRecording = false;

            byte[] wav = EncodeClip(_recording, sampleFrames);
            if (wav == null)
            {
                Debug.Log("[GradeApiClient] Empty recording; /grade skipped (invisible — play continues).");
                onResult?.Invoke(null);
                yield break;
            }
            GradeUploadRunner.Instance.Post(wav, ctx, ResolveBearer(), requestTimeoutSeconds, onResult);
            yield break;
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

            using (UnityWebRequest req = UnityWebRequest.Post($"{BackendConfig.BaseUrl}/grade", form))
            {
                if (requestTimeoutSeconds > 0)
                    req.timeout = requestTimeoutSeconds;

                BackendConfig.Prepare(req);
                string bearer = ResolveBearer();
                if (!string.IsNullOrWhiteSpace(bearer))
                    req.SetRequestHeader("Authorization", bearer);
                Debug.Log($"[GradeApiClient] POST /grade begin target={ctx.targetWordId} child={ctx.childId} session={ctx.sessionId} wavBytes={wav.Length} timeout={req.timeout}s");
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

        private string ResolveBearer()
        {
            if (AuthSession.Instance != null && !string.IsNullOrEmpty(AuthSession.Instance.IdToken))
                return AuthSession.Instance.BearerHeader;

            return string.IsNullOrWhiteSpace(authorization) ? null : authorization.Trim();
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log(message);
        }

        private IEnumerator PrewarmRoutine()
        {
            if (prewarmDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(prewarmDelaySeconds);

            if (_isRecording || _startRoutine != null || _micPrewarmed)
            {
                _prewarmRoutine = null;
                yield break;
            }

            string[] devices = Microphone.devices ?? Array.Empty<string>();
            if (devices.Length == 0)
            {
                _prewarmRoutine = null;
                yield break;
            }

            _prewarmDevice = SelectDevice(devices);
            int frequency = ResolveSampleRate(_prewarmDevice);
            AudioClip clip = null;

            LogVerbose($"[GradeApiClient] Mic prewarm begin device={FormatDevice(_prewarmDevice)} frequency={frequency}");
            try
            {
                clip = Microphone.Start(_prewarmDevice, false, 1, frequency);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GradeApiClient] Mic prewarm failed for {FormatDevice(_prewarmDevice)}: {e.Message}");
            }

            if (clip != null)
            {
                float deadline = Time.realtimeSinceStartup + 0.5f;
                while (SafeGetPosition(_prewarmDevice) <= 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;

                SafeEnd(_prewarmDevice);
                _micPrewarmed = true;
                LogVerbose("[GradeApiClient] Mic prewarm complete.");
            }

            _prewarmDevice = null;
            _prewarmRoutine = null;
        }

        private void CancelPrewarm()
        {
            if (_prewarmRoutine == null)
                return;

            StopCoroutine(_prewarmRoutine);
            SafeEnd(_prewarmDevice);
            _prewarmDevice = null;
            _prewarmRoutine = null;
            LogVerbose("[GradeApiClient] Mic prewarm cancelled for live recording.");
        }

        private string SelectDevice(string[] devices)
        {
            if (!string.IsNullOrWhiteSpace(preferredDeviceName))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(devices[i], preferredDeviceName, StringComparison.OrdinalIgnoreCase))
                        return devices[i];
                }

                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].IndexOf(preferredDeviceName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return devices[i];
                }

                Debug.LogWarning($"[GradeApiClient] Preferred mic '{preferredDeviceName}' was not found; falling back.");
            }

            return useDefaultInputDevice ? null : devices[0];
        }

        private int ResolveSampleRate(string device)
        {
            int desired = Mathf.Max(1, sampleRate);
            try
            {
                Microphone.GetDeviceCaps(device, out int min, out int max);
                if (min <= 0 && max <= 0) return desired;
                if (min > 0 && desired < min) return min;
                if (max > 0 && desired > max) return max;
                return desired;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GradeApiClient] GetDeviceCaps failed for {FormatDevice(device)}: {e.Message}; using {desired}Hz.");
                return desired;
            }
        }

        private static int SafeGetPosition(string device)
        {
            try { return Microphone.GetPosition(device); }
            catch (Exception e)
            {
                Debug.LogWarning($"[GradeApiClient] Microphone.GetPosition failed for {FormatDevice(device)}: {e.Message}");
                return 0;
            }
        }

        private static void SafeEnd(string device)
        {
            try { Microphone.End(device); }
            catch (Exception e) { Debug.LogWarning($"[GradeApiClient] Microphone.End failed for {FormatDevice(device)}: {e.Message}"); }
        }

        private void ResetRecordingState()
        {
            _device = null;
            _recording = null;
            _isRecording = false;
            _startedAtRealtime = 0f;
        }

        private static string DescribeDevices(string[] devices)
        {
            if (devices == null || devices.Length == 0) return "<none>";
            return string.Join(" | ", devices);
        }

        private static string FormatDevice(string device) =>
            string.IsNullOrEmpty(device) ? "<default>" : device;

        private sealed class GradeUploadRunner : MonoBehaviour
        {
            private static GradeUploadRunner _instance;

            public static GradeUploadRunner Instance
            {
                get
                {
                    if (_instance != null)
                        return _instance;

                    GameObject host = new GameObject("GradeUploadRunner");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    _instance = host.AddComponent<GradeUploadRunner>();
                    return _instance;
                }
            }

            public void Post(byte[] wav, GradeContext ctx, string bearer, int timeoutSeconds, Action<GradeResponse> onResult)
            {
                StartCoroutine(PostRoutine(wav, ctx, bearer, timeoutSeconds, onResult));
            }

            private IEnumerator PostRoutine(byte[] wav, GradeContext ctx, string bearer, int timeoutSeconds, Action<GradeResponse> onResult)
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

                using (UnityWebRequest req = UnityWebRequest.Post($"{BackendConfig.BaseUrl}/grade", form))
                {
                    if (timeoutSeconds > 0)
                        req.timeout = timeoutSeconds;

                    BackendConfig.Prepare(req);
                    if (!string.IsNullOrWhiteSpace(bearer))
                        req.SetRequestHeader("Authorization", bearer);

                    Debug.Log($"[GradeApiClient] POST /grade begin target={ctx.targetWordId} child={ctx.childId} session={ctx.sessionId} wavBytes={wav.Length} timeout={req.timeout}s");
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[GradeApiClient] /grade failed: {req.error} {req.downloadHandler.text}");
                        InvokeResult(onResult, null);
                        yield break;
                    }

                    string body = req.downloadHandler.text;
                    Debug.Log($"[GradeApiClient] /grade 200: {body}");
                    GradeResponse parsed = null;
                    try { parsed = JsonUtility.FromJson<GradeResponse>(body); }
                    catch (Exception e) { Debug.LogWarning($"[GradeApiClient] parse failed: {e.Message}"); }
                    InvokeResult(onResult, parsed);
                }
            }

            private static void InvokeResult(Action<GradeResponse> onResult, GradeResponse result)
            {
                try { onResult?.Invoke(result); }
                catch (Exception e) { Debug.LogWarning($"[GradeApiClient] result callback failed: {e.Message}"); }
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
