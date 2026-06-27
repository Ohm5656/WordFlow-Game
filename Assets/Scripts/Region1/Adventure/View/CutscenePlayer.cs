using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Core;
using WordFlow.Adventure.Data;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.View
{
    /// <summary>
    /// Plays a CutsceneData as an in-scene overlay: NPC-only, voice + visuals, no text.
    /// Auto-runs (visual-novel style, hands-off): each frame holds for the longer of its voice
    /// length and holdSeconds, then advances on its own; after the last frame it ends. No tap
    /// interaction. Raises onFinished exactly once. Builds its own high-sorting
    /// ScreenSpaceOverlay canvas so it covers the encounter UI without depending on it, and
    /// tears it down when done.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        [SerializeField] private TtsApiClient ttsClient;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float fadeSeconds = 0.25f;
        // Safety cap on a slow/uncached /tts fetch. We WAIT up to this long for the clip
        // rather than dropping the voice (Bug A: 3s cut off uncached lines). The frame still
        // then holds for the full clip length before auto-advancing.
        [SerializeField] private float ttsTimeout = 8f;

        private static readonly Vector2 DefaultNpcSize = new Vector2(520f, 620f);

        private CutsceneData _data;
        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _bg;
        private Image _dim;
        private Image _npc;
        private readonly System.Collections.Generic.List<Image> _extraNpcs =
            new System.Collections.Generic.List<Image>();
        private readonly System.Collections.Generic.List<Coroutine> _entranceCos =
            new System.Collections.Generic.List<Coroutine>();

        // Self-wire on the same GameObject (matches the project's name/sibling-lookup style),
        // so a scene only needs the CutscenePlayer component itself.
        private void Awake()
        {
            if (ttsClient == null) ttsClient = GetComponent<TtsApiClient>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        /// <summary>Play the cutscene; onFinished is invoked once when it ends (or immediately
        /// if data is null/empty, preserving the no-cutscene path).</summary>
        public void Play(CutsceneData data, Action onFinished)
        {
            StartCoroutine(PlayRoutine(data, onFinished));
        }

        private IEnumerator PlayRoutine(CutsceneData data, Action onFinished)
        {
            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                Teardown();
                onFinished?.Invoke();
            }

            if (data == null || data.frames == null || data.frames.Count == 0) { Finish(); yield break; }

            _data = data;
            BuildOverlay();
            yield return Fade(0f, 1f);

            for (int i = 0; i < data.frames.Count; i++)
            {
                var frame = data.frames[i];
                if (frame == null) continue;
                StopEntrances();

                AvHelpers.TrySetSprite(_bg, frame.background);
                ApplyNpc(frame);
                ApplyExtraCharacters(frame);
                ApplyFocus(frame);

                // Resolve audio: an explicit clip wins, else fetch from /tts (cached/async).
                AudioClip clip = frame.voiceClip;
                if (clip == null && ttsClient != null && !string.IsNullOrWhiteSpace(frame.voiceLineId))
                {
                    bool gotit = false;
                    ttsClient.GetLine(frame.voiceLineId, c => { clip = c; gotit = true; });
                    float waitUntil = Time.realtimeSinceStartup + ttsTimeout;
                    while (!gotit && Time.realtimeSinceStartup < waitUntil) yield return null;
                }

                float clipLen = 0f;
                if (audioSource != null) audioSource.Stop();
                if (clip != null) { AvHelpers.TryPlay(audioSource, clip); clipLen = clip.length; }

                // Hold the longer of the voice length and the authored hold, then auto-advance.
                yield return Wait(Mathf.Max(frame.holdSeconds, clipLen));
            }

            yield return Fade(1f, 0f);
            Finish();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static float SmoothStep01(float v) => v * v * (3f - 2f * v);

        private void StopEntrances()
        {
            foreach (var co in _entranceCos) if (co != null) StopCoroutine(co);
            _entranceCos.Clear();
        }

        private void StartEntrance(RectTransform rt, Vector2 rest, EntranceMode mode, float seconds)
        {
            if (rt == null || mode == EntranceMode.None) return;
            _entranceCos.Add(StartCoroutine(EntranceRoutine(rt, rest, mode, seconds)));
        }

        // Position-only float-in: ease an off-position to the resting spot with SmoothStep + a
        // settling sine bob (matches the old BearCutsceneEntrance feel). Bob is a looping idle.
        private IEnumerator EntranceRoutine(RectTransform rt, Vector2 rest, EntranceMode mode, float seconds)
        {
            if (mode == EntranceMode.Bob)
            {
                while (true)
                {
                    rt.anchoredPosition = rest + new Vector2(0f, Mathf.Sin(Time.realtimeSinceStartup * 3f) * 12f);
                    yield return null;
                }
            }

            Vector2 from = rest;
            switch (mode)
            {
                case EntranceMode.SlideInLeft:  from = rest + new Vector2(-700f, 0f); break;
                case EntranceMode.SlideInRight: from = rest + new Vector2( 700f, 0f); break;
                case EntranceMode.SlideInUp:    from = rest + new Vector2(0f, -500f); break;
            }
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float s = SmoothStep01(Mathf.Clamp01(t / dur));
                Vector2 p = Vector2.LerpUnclamped(from, rest, s);
                p.y += Mathf.Sin(s * Mathf.PI) * 10f; // settle bob
                rt.anchoredPosition = p;
                yield return null;
            }
            rt.anchoredPosition = rest;
        }

        private void ApplyNpc(CutsceneFrame frame)
        {
            if (_npc == null) return;
            if (frame.npc == null) { _npc.enabled = false; return; }
            _npc.enabled = true;
            _npc.sprite = frame.npc;
            _npc.preserveAspect = true;
            ((RectTransform)_npc.transform).sizeDelta = frame.npcSize != Vector2.zero ? frame.npcSize : DefaultNpcSize;
            float x = frame.side == NpcSide.Left ? -360f : frame.side == NpcSide.Right ? 360f : 0f;
            ((RectTransform)_npc.transform).anchoredPosition = new Vector2(x, -30f);
            StartEntrance((RectTransform)_npc.transform, new Vector2(x, -30f), frame.npcEntrance, frame.npcEntranceSeconds);
            PlayClipOn(_npc, frame.npcAnim);
        }

        // Spawn/refresh one Image per extra character for THIS frame. Images are created lazily
        // and reused across frames; surplus ones are disabled. Positioned by the character's
        // explicit offset from the same screen-bottom-center anchor the primary npc uses.
        private void ApplyExtraCharacters(CutsceneFrame frame)
        {
            int count = frame.extraCharacters != null ? frame.extraCharacters.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var c = frame.extraCharacters[i];
                Image img = i < _extraNpcs.Count ? _extraNpcs[i] : CreateExtraNpc();
                if (c == null || c.sprite == null) { img.enabled = false; continue; }
                img.enabled = true;
                img.sprite = c.sprite;
                img.preserveAspect = true;
                ((RectTransform)img.transform).sizeDelta = c.size != Vector2.zero ? c.size : DefaultNpcSize;
                Vector2 rest = new Vector2(0f, -30f) + c.offset;
                ((RectTransform)img.transform).anchoredPosition = rest;
                StartEntrance((RectTransform)img.transform, rest, c.entrance, c.entranceSeconds);
                PlayClipOn(img, c.anim);
            }
            for (int i = count; i < _extraNpcs.Count; i++) _extraNpcs[i].enabled = false;
        }

        private Image CreateExtraNpc()
        {
            var go = new GameObject($"ExtraNpc_{_extraNpcs.Count}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = DefaultNpcSize;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _extraNpcs.Add(img);
            return img;
        }

        // Emphasise the focused character (scale up); dim + shrink the others. focusIndex == -1
        // resets everyone to full. Indexing: 0 = primary npc, 1..N = extraCharacters[idx-1].
        private void ApplyFocus(CutsceneFrame frame)
        {
            int focus = frame.focusIndex;
            ApplyFocusTo(_npc, focus == 0, focus < 0);
            for (int i = 0; i < _extraNpcs.Count; i++)
                ApplyFocusTo(_extraNpcs[i], focus == i + 1, focus < 0);
        }

        private void ApplyFocusTo(Image img, bool focused, bool noFocus)
        {
            if (img == null || !img.enabled) return;
            float fScale = _data != null ? _data.focusScale : 1.15f;
            float uScale = _data != null ? _data.unfocusScale : 0.92f;
            float dAlpha = _data != null ? _data.focusDimAlpha : 0.5f;
            float scale = noFocus ? 1f : (focused ? fScale : uScale);
            float alpha = noFocus ? 1f : (focused ? 1f : dAlpha);
            img.transform.localScale = new Vector3(scale, scale, 1f);
            var col = img.color; col.a = alpha; img.color = col;
        }

        // Best-effort: only legacy clips can be driven by an Animation component. Anything
        // else (or any failure) is ignored — the sprite still shows, we never throw.
        private void PlayClipOn(Image target, AnimationClip clip)
        {
            if (clip == null || !clip.legacy || target == null) return;
            try
            {
                var anim = target.GetComponent<Animation>();
                if (anim == null) anim = target.gameObject.AddComponent<Animation>();
                anim.AddClip(clip, clip.name);
                anim.Play(clip.name);
            }
            catch (Exception e) { Debug.LogWarning($"[CutscenePlayer] anim skipped: {e.Message}"); }
        }

        // ---- overlay ----

        private void BuildOverlay()
        {
            var go = new GameObject("CutsceneOverlay", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue - 1; // above the encounter UI
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            // BG layer: shows the frame's background faithfully (white tint, opaque so the
            // encounter UI never bleeds through). Dim layer: a separate black overlay above the
            // background and below the characters, its alpha authored per cutscene (backdropDim).
            _bg = MakeStretch(go.transform, "Bg");
            _bg.color = Color.white;
            _bg.preserveAspect = false;
            _dim = MakeStretch(go.transform, "Dim");
            _dim.color = new Color(0f, 0f, 0f, _data != null ? _data.backdropDim : 0.35f);

            var npcGo = new GameObject("Npc", typeof(RectTransform), typeof(Image));
            npcGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)npcGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = DefaultNpcSize;
            rt.anchoredPosition = new Vector2(0, -30f);
            _npc = npcGo.GetComponent<Image>();
            _npc.raycastTarget = false;
            _npc.enabled = false;
        }

        private static Image MakeStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_group == null) yield break;
            if (fadeSeconds <= 0f) { _group.alpha = to; yield break; }
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            _group.alpha = to;
        }

        private void Teardown()
        {
            if (audioSource != null) audioSource.Stop();
            StopEntrances();
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null; _group = null; _bg = null; _dim = null; _npc = null; _data = null;
            _extraNpcs.Clear();
        }
    }
}
