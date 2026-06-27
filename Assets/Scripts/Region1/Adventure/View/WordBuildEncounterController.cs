using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordFlow.Adventure.Core;
using WordFlow.Adventure.Data;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.View
{
    /// <summary>
    /// Drives one word-build encounter end-to-end over placeholder UGUI.
    ///
    /// Loop (locked design — wordflow-game-design.md "Encounter outcome"):
    ///   intro cutscene -> BUILD the word from tiles -> CONFIRM the built word
    ///   -> post-build ECHO (owl voices what they built) -> MIC (child says it)
    ///   -> /grade fires INVISIBLY (clinical only; the player sees nothing)
    ///   -> branch by what was BUILT:
    ///        Correct (suits the context) -> outro cutscene + "+1 level"
    ///        WrongWord / NonWord (not suited) -> back to BUILD (try again).
    /// Grading never decides the branch and never blocks it.
    /// </summary>
    public sealed class WordBuildEncounterController : MonoBehaviour
    {
        // Explicit beats of the encounter. Taps are gated on the phase so a stray
        // click during echo/mic/resolve can't corrupt the build.
        private enum Phase { Building, Confirm, Echo, Mic, Resolving, Done }

        [SerializeField] private EncounterConfig config;
        [SerializeField] private WordDatabase database;
        [SerializeField] private GradeApiClient gradeClient;
        [SerializeField] private TelemetryClient telemetry;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private CutscenePlayer cutscenePlayer;
        [SerializeField] private RectTransform trayContainer;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private RectTransform stoneRevealAnchor;
        [SerializeField] private Button listenButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button micButton;
        [SerializeField] private Image micIcon;

        private EncounterModel _model;
        // The ordered tile data driving the tray: target tiles first, then distractors.
        // trayIndex indexes into THIS, not config.target.tiles (which may be shorter).
        private List<StoneTileData> _trayData = new List<StoneTileData>();
        private Phase _phase = Phase.Building;
        private readonly List<StoneTile> _trayTiles = new List<StoneTile>();
        private readonly List<TileSlot> _slots = new List<TileSlot>();
        private readonly List<Vector2> _tileHome = new List<Vector2>();
        private bool _animating;
        [SerializeField] private float slideSeconds = 0.22f;
        [SerializeField] private Sprite listenIconSprite;   // speaker icon (nullable -> glyph fallback)
        [SerializeField] private Sprite confirmIconSprite;   // check icon
        [SerializeField] private Sprite micIconSprite;       // mic icon
        private Coroutine _listenRoutine;
        private HashSet<string> _knownThai;
        [SerializeField] private bool showDebugControls = false; // hidden in the child build
        private Coroutine _micPulse;
        private readonly BuildLatencyTracker _latency = new BuildLatencyTracker();
        [SerializeField] private float micSeconds = 5f;       // mic auto-stop
        [SerializeField] private float stoneRevealSeconds = 1.5f;
        [Header("Return to forest (when entered from reference_forest)")]
        [Tooltip("Scene loaded back after a successful build, only when BearEncounterFlow.ReturnToForest is set.")]
        [SerializeField] private string returnSceneName = "reference_forest";
        [Tooltip("Black fade-out duration before returning to the forest.")]
        [SerializeField] private float returnCoverDuration = 1.0f;
        [SerializeField] private AudioClip nonWordSfx;        // nullable; funny non-demotivating "nope"
        [Header("Layout sizes")]
        [SerializeField] private Vector2 tileSize = new Vector2(200f, 200f);
        [SerializeField] private Vector2 slotSize = new Vector2(210f, 210f);
        [SerializeField] private float slotGap = 40f;                 // space between the two slot cells
        [SerializeField] private Vector2 stoneRevealSize = new Vector2(420f, 420f);
        [SerializeField] private Vector2 smokeSize = new Vector2(400f, 400f);
        [SerializeField] private Sprite slotFrameSprite;              // nullable -> faint box fallback
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.12f);

        [Header("Hover-to-hear")]
        [SerializeField] private bool hoverToHearEnabled = false;  // demo: hover phoneme muted (flip on to restore)
        [SerializeField] private float hoverCooldown = 0.6f;  // min gap before the SAME stone re-speaks
        private int _lastHoverIndex = -1;
        private float _lastHoverFreeAt;

        [Header("Craft effects")]
        [SerializeField] private Sprite bearSprite;                 // bear that flees on the ปา throw
        [SerializeField] private Vector2 effectSpriteSize = new Vector2(220f, 220f); // thrown object / crow size
        [SerializeField] private Vector2 bearSize = new Vector2(360f, 360f);
        [SerializeField] private float projectileArcSeconds = 0.9f;
        [SerializeField] private float projectileArcHeight = 260f;  // peak height above the straight line
        [SerializeField] private float bearFleeSeconds = 0.8f;
        [SerializeField] private int crowCount = 5;
        [SerializeField] private float crowFlySeconds = 1.4f;
        private RectTransform _fxLayer;                             // full-screen overlay for craft effects

        private bool _recording;
        private bool _micStopRequested;
        private Image _smoke;                                 // black-smoke puff overlay

        private void Start()
        {
            if (config == null || config.target == null)
            {
                Debug.LogError("[WordBuild] config/target not assigned.");
                return;
            }
            if (cutscenePlayer == null) cutscenePlayer = GetComponent<CutscenePlayer>();
            BuildKnownSet();
            BuildUi();

            // Intro cutscene (if assigned) plays over the UI, then hands off to the build.
            // No cutscene -> begin immediately, exactly as before.
            if (config.introCutscene != null && cutscenePlayer != null)
                cutscenePlayer.Play(config.introCutscene, BeginEncounter);
            else
                BeginEncounter();
        }

        private void BuildKnownSet()
        {
            _knownThai = new HashSet<string>();
            if (database != null)
                foreach (var w in database.words)
                    if (w != null && !string.IsNullOrEmpty(w.thai)) _knownThai.Add(w.thai);
        }

        // ---- encounter lifecycle ----

        private void BeginEncounter()
        {
            StopAllCoroutines(); // cancel any in-flight routine (e.g. debug mode/echo toggle mid-resolution)
            // Tray = target tiles + distractors (distractors are never required to complete).
            _trayData = new List<StoneTileData>(config.target.tiles);
            if (config.distractorTiles != null) _trayData.AddRange(config.distractorTiles);
            var graphemes = new List<string>();
            foreach (var t in _trayData) graphemes.Add(t != null ? t.grapheme : "");
            _model = new EncounterModel(graphemes, slotCount: config.target.tiles.Count);
            EnterBuildingPhase();
            UpdateHud($"Build: {config.target.thai}   [{config.mode}{(config.echo ? "+Echo" : "")}]");

            // Reveal (Supported only): play the whole word once. The Listen button's
            // visibility is set by ShowBuildingButtons() inside EnterBuildingPhase above.
            if (config.mode == EncounterMode.Supported)
                AvHelpers.TryPlay(audioSource, config.target.wordAudio);

            // Build-latency clock starts now that tiles are interactive (end of BeginEncounter).
            _latency.Start(Time.realtimeSinceStartupAsDouble);
        }

        // Building: tiles are tappable, slots can be emptied, no confirm/mic affordance yet.
        private void EnterBuildingPhase()
        {
            _phase = Phase.Building;
            if (_trayTiles.Count == 0) BuildBoard(); else ResetTilesToTray();
            ShowBuildingButtons();
        }

        // Building affordances: Listen helps only in Supported mode; Confirm/Mic stay hidden
        // until the word is whole. (Three buttons, three distinct anchors — never overlapping.)
        private void ShowBuildingButtons()
        {
            SetActive(confirmButton, false);
            SetActive(micButton, false);
            SetActive(listenButton, config.mode == EncounterMode.Supported);
        }

        private void OnTileTapped(int trayIndex)
        {
            if (_model == null || _animating) return;
            if (_phase != Phase.Building && _phase != Phase.Confirm) return;

            // Already in a slot? Tapping the stone pulls it back to the tray. (The placed tile
            // sits ON TOP of its slot and eats the tap, so retrieval lives here, not in
            // OnSlotTapped — which the tile would otherwise block.) Pulling a tile during
            // Confirm un-commits the word back to Building.
            if (!_model.IsInTray(trayIndex))
            {
                int filledSlot = SlotHoldingTray(trayIndex);
                if (filledSlot < 0) return;
                _model.ReturnSlotAt(filledSlot);
                if (_phase == Phase.Confirm) { _phase = Phase.Building; ShowBuildingButtons(); }
                StartCoroutine(SlideTile(_trayTiles[trayIndex], _tileHome[trayIndex]));
                return;
            }

            // Otherwise place it into the next empty slot (Building only).
            if (_phase != Phase.Building) return;
            int slot = _model.PlaceFromTrayAt(trayIndex);
            if (slot < 0) return;

            // Supported: each placed tile speaks its phoneme.
            if (config.mode == EncounterMode.Supported)
            {
                var data = _trayData[trayIndex];
                AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
            }

            Vector2 slotPos = SlotPosInTraySpace(slot);
            StartCoroutine(PlaceTileRoutine(trayIndex, slotPos));
        }

        // Hover-to-hear: pointing at a tray stone speaks its phoneme name (ป = "ปอ"). A
        // Supported-mode scaffold (Recall turns hints off), gated to the build phases so a
        // stray hover during echo/mic can't talk over the model. On touch there's no hover,
        // so this is a laptop-demo affordance; the place-time phoneme still fires on mobile.
        private void OnTileHovered(int trayIndex)
        {
            if (!hoverToHearEnabled) return;   // demo: hover-to-hear muted
            if (config == null || config.target == null) return;
            if (config.mode != EncounterMode.Supported) return;
            if (_phase != Phase.Building && _phase != Phase.Confirm) return;
            if (trayIndex < 0 || trayIndex >= _trayData.Count) return;

            // Rate-limit: the same stone won't re-speak until its clip has finished + a short
            // cooldown, so a held/jittery pointer can't machine-gun the phoneme. A DIFFERENT
            // stone speaks immediately (stays responsive).
            float now = Time.realtimeSinceStartup;
            if (trayIndex == _lastHoverIndex && now < _lastHoverFreeAt) return;

            var data = _trayData[trayIndex];
            var clip = data != null ? data.phonemeAudio : null;
            if (audioSource != null && clip != null)
            {
                audioSource.Stop();              // cut any prior hover sound -> no overlap
                audioSource.PlayOneShot(clip);
            }
            _lastHoverIndex = trayIndex;
            _lastHoverFreeAt = now + (clip != null ? clip.length : 0f) + hoverCooldown;
        }

        // Which slot currently holds this tray tile (-1 if none).
        private int SlotHoldingTray(int trayIndex)
        {
            for (int s = 0; s < _model.SlotCount; s++)
                if (_model.SlotTrayIndex(s) == trayIndex) return s;
            return -1;
        }

        // Slide the tapped tile onto its slot, then (if the word is whole) offer Confirm. We do
        // NOT auto-resolve: the child reviews the word and commits it (or pulls a tile back).
        private IEnumerator PlaceTileRoutine(int trayIndex, Vector2 slotPos)
        {
            yield return SlideTile(_trayTiles[trayIndex], slotPos);
            _latency.RecordPlacement(Time.realtimeSinceStartupAsDouble);
            if (_model.IsComplete)
            {
                _latency.Complete(Time.realtimeSinceStartupAsDouble);
                EnterConfirmPhase();
            }
        }

        private void OnSlotTapped(int slotIndex)
        {
            // Editing a slot is only allowed while still composing (Building) or while the
            // word is built-but-uncommitted (Confirm). Pulling a tile in Confirm un-commits.
            if (_model == null || _animating || (_phase != Phase.Building && _phase != Phase.Confirm)) return;
            int trayIdx = _model.SlotTrayIndex(slotIndex);
            if (_model.ReturnSlotAt(slotIndex) < 0) return;
            if (_phase == Phase.Confirm) { _phase = Phase.Building; ShowBuildingButtons(); }
            if (trayIdx >= 0) StartCoroutine(SlideTile(_trayTiles[trayIdx], _tileHome[trayIdx]));
        }

        // Confirm: the word is whole but not committed. A Confirm button commits it; tapping
        // a placed tile back drops to Building (handled in OnSlotTapped).
        private void EnterConfirmPhase()
        {
            _phase = Phase.Confirm;
            UpdateHud($"You built {_model.BuiltString} — confirm?");
            SetActive(confirmButton, true);
            SetActive(micButton, false);
            SetActive(listenButton, false);
        }

        // Commit -> post-build echo -> open the mic. Echo always plays (every mode), once,
        // before the child recites (locked design: post-build echo is always on).
        private void OnConfirm()
        {
            if (_phase != Phase.Confirm) return;
            StartCoroutine(ConfirmRoutine());
        }

        private IEnumerator ConfirmRoutine()
        {
            _phase = Phase.Echo;
            SetActive(confirmButton, false);
            string built = _model.BuiltString;
            UpdateHud($"Listen: {built}");
            yield return PlayPostBuildEcho(built);

            // Now invite the child to say it. The mic button is the only way grading starts;
            // the Listen button lets them replay the example (ปอ อา ปา) as often as they want.
            _phase = Phase.Mic;
            UpdateHud($"Now say it!  {built}");
            SetActive(micButton, true);
            SetActive(listenButton, true);
        }

        // First tap: start recording. Tap again (while recording): stop early. Else auto-stop at 5s.
        private void OnMic()
        {
            if (_phase != Phase.Mic) return;
            if (!_recording) StartCoroutine(MicAndResolve());
            else _micStopRequested = true;
        }

        private IEnumerator MicAndResolve()
        {
            _recording = true;
            _micStopRequested = false;
            string built = _model.BuiltString;
            Outcome outcome = OutcomeEvaluator.Evaluate(built, config.target.thai, _knownThai);

            // ---- the mic beat (a real record moment, not fire-and-forget) ----
            if (gradeClient != null) gradeClient.StartRecording();
            StartMicPulse();
            float until = Time.realtimeSinceStartup + micSeconds;
            while (Time.realtimeSinceStartup < until && !_micStopRequested) yield return null;
            StopMicPulse();
            _recording = false;
            SetActive(micButton, false);
            SetActive(listenButton, false);

            // Background /grade (invisible, never blocks visuals). No mic -> degrade: just resolve.
            FireGrade(built, outcome);
            // Telemetry: record what they BUILT this try (fires even if the mic got nothing).
            FireBuildAttempt(built, outcome);

            // ---- resolve by what was BUILT ----
            _phase = Phase.Resolving;
            yield return ResolveBuild(built, outcome);
        }

        private IEnumerator ResolveBuild(string built, Outcome outcome)
        {
            Debug.Log($"[WordBuild] built='{built}' target='{config.target.thai}' outcome={outcome}");
            var builtWord = database != null ? database.LookupByThai(built) : null;

            if (builtWord != null) // REAL WORD (Correct or WrongWord) -> reveal stone, then effect + cutscene
            {
                yield return RevealStone(builtWord);

                bool correct = outcome == Outcome.Correct;
                CutsceneData effect = correct ? config.outroCutscene : config.wrongEffectCutscene;

                if (correct)
                {
                    // Success: the craft effect plays INSIDE the outro (layered on top of the
                    // cutscene via the high-sorting fx layer), NOT on the book page. Start the
                    // outro, throw over it, then wait for the cutscene to finish. No outro
                    // authored -> still play the effect so it isn't lost.
                    if (effect != null && cutscenePlayer != null)
                    {
                        bool done = false;
                        cutscenePlayer.Play(effect, () => done = true);
                        yield return PlayWordEffect(builtWord);
                        while (!done) yield return null;
                    }
                    else
                    {
                        yield return PlayWordEffect(builtWord);
                    }

                    UpdateHud($"Correct! +1 level (built {built})");
                    Debug.Log("[WordBuild] +1 level (stub)");
                    // Telemetry: the encounter is cleared -> progression event ("where they are").
                    if (SessionContext.Instance != null) SessionContext.Instance.RecordCleared();
                    if (telemetry != null) telemetry.PostProgressEvent(Kid(), "quest_completed", config.questId);
                    _phase = Phase.Done;

                    // Entered from reference_forest (the bear quest)? Fade out and hand back so the
                    // forest resumes at Beat 2 with the bear gone. Standalone play (flag unset) stays put.
                    if (BearEncounterFlow.ReturnToForest)
                    {
                        BearEncounterFlow.ReturnToForest = false;
                        BearEncounterFlow.ResumeAtBeat2 = true;
                        yield return SceneFadeController.Cover(returnCoverDuration);
                        SceneManager.LoadScene(returnSceneName);
                        yield break;
                    }
                }
                else
                {
                    // Wrong word: effect on the page, then the reveal cutscene (unchanged).
                    yield return PlayWordEffect(builtWord);
                    if (effect != null && cutscenePlayer != null)
                    {
                        bool done = false;
                        cutscenePlayer.Play(effect, () => done = true);
                        while (!done) yield return null;
                    }
                    Debug.Log($"[WordBuild] real word '{built}', wrong for context -> retry");
                    _latency.Reset();
                    _model.ResetToTray();
                    EnterBuildingPhase();
                }
            }
            else // NON-WORD -> black smoke + funny SFX, no stone, back to build
            {
                yield return SmokePuff();
                _latency.Reset();
                _model.ResetToTray();
                EnterBuildingPhase();
            }
        }

        // Pop the word's magic stone centered, replay its sound, hold ~1.5s. Null sprite -> no-op pause.
        private IEnumerator RevealStone(WordEncounterData word)
        {
            AvHelpers.TryPlay(audioSource, word.wordAudio);
            if (word.magicStone == null) { yield return new WaitForSeconds(0.4f); yield break; }

            var go = new GameObject("StoneReveal", typeof(RectTransform), typeof(Image));
            var parent = stoneRevealAnchor != null ? (Transform)stoneRevealAnchor : trayContainer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = stoneRevealSize;
            var img = go.GetComponent<Image>();
            img.sprite = word.magicStone; img.preserveAspect = true; img.raycastTarget = false;

            float t = 0f;
            while (t < stoneRevealSeconds)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(t / 0.35f)); // pop in
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator SmokePuff()
        {
            if (_smoke == null)
            {
                var go = new GameObject("Smoke", typeof(RectTransform), typeof(Image));
                var parent = stoneRevealAnchor != null ? (Transform)stoneRevealAnchor : trayContainer;
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = smokeSize;
                _smoke = go.GetComponent<Image>();
                _smoke.color = new Color(0.05f, 0.05f, 0.07f, 0f);
                _smoke.raycastTarget = false;
            }
            AvHelpers.TryPlay(audioSource, nonWordSfx);
            _smoke.gameObject.SetActive(true);
            float t = 0f;
            while (t < 0.6f) { t += Time.deltaTime; _smoke.color = new Color(0.05f, 0.05f, 0.07f, Mathf.PingPong(t * 2f, 1f) * 0.85f); yield return null; }
            _smoke.color = new Color(0.05f, 0.05f, 0.07f, 0f);
            _smoke.gameObject.SetActive(false);
        }

        // ---- craft effects (procedural, over a full-screen fx layer) ----

        // Dispatch the built word's effect after its magic-stone reveal. None/null -> no-op.
        private IEnumerator PlayWordEffect(WordEncounterData word)
        {
            if (word == null) yield break;
            switch (word.effect)
            {
                case WordEffectKind.ProjectileBearFlees: yield return ProjectileBearFleesEffect(word); break;
                case WordEffectKind.Birds: yield return BirdsEffect(word); break;
                default: yield break;
            }
        }

        // ปา ("throw"): a thrown object arcs across the screen toward the bear, who runs away.
        private IEnumerator ProjectileBearFleesEffect(WordEncounterData word)
        {
            var layer = EnsureFxLayer();
            yield return null; // let the stretched layer's rect settle before measuring
            float w = layer.rect.width, h = layer.rect.height;
            if (w <= 0f) w = 1080f;
            if (h <= 0f) h = 1920f;

            Vector2 bearPos = new Vector2(-w * 0.28f, -h * 0.12f);
            Image bear = bearSprite != null ? MakeFxImage(bearSprite, bearSize, bearPos) : null;

            Vector2 from = new Vector2(w * 0.5f + effectSpriteSize.x, h * 0.05f);
            Image proj = MakeFxImage(word.effectSprite, effectSpriteSize, from);

            // Bear bolts off the left edge as the object is thrown.
            if (bear != null)
                StartCoroutine(MoveRt((RectTransform)bear.transform, new Vector2(-w * 0.85f, bearPos.y), bearFleeSeconds, true));
            yield return ArcRt((RectTransform)proj.transform, from, bearPos, projectileArcSeconds, projectileArcHeight);
            if (proj != null) Destroy(proj.gameObject);
            yield return new WaitForSeconds(0.1f);
        }

        // กา ("crow"): a flock of crows flies across the screen (reveals the word's meaning).
        private IEnumerator BirdsEffect(WordEncounterData word)
        {
            var layer = EnsureFxLayer();
            yield return null;
            float w = layer.rect.width, h = layer.rect.height;
            if (w <= 0f) w = 1080f;
            if (h <= 0f) h = 1920f;

            int n = Mathf.Max(1, crowCount);
            const float stagger = 0.12f;
            for (int i = 0; i < n; i++)
            {
                float band = (i % 3 - 1) * (h * 0.18f) + Random.Range(-h * 0.04f, h * 0.04f);
                Vector2 start = new Vector2(-w * 0.6f - effectSpriteSize.x, band);
                Vector2 end = new Vector2(w * 0.6f + effectSpriteSize.x, band + h * 0.12f);
                var crow = MakeFxImage(word.effectSprite, effectSpriteSize, start);
                StartCoroutine(DelayThenMove((RectTransform)crow.transform, end, crowFlySeconds, i * stagger));
            }
            yield return new WaitForSeconds(crowFlySeconds + n * stagger + 0.2f);
        }

        // Lazily build a full-screen, non-interactive overlay under the root canvas for effects.
        private RectTransform EnsureFxLayer()
        {
            if (_fxLayer != null) return _fxLayer;
            Canvas canvas = trayContainer != null ? trayContainer.GetComponentInParent<Canvas>() : null;
            Transform parent = canvas != null ? canvas.transform
                : (trayContainer != null ? trayContainer.parent : transform);
            var go = new GameObject("CraftFxLayer", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            // Sort the craft FX ABOVE the cutscene overlay (which sorts at short.MaxValue-1) so the
            // success effect plays INSIDE the outro, layered on top of it rather than hidden behind.
            var fxCanvas = go.GetComponent<Canvas>();
            fxCanvas.overrideSorting = true;
            fxCanvas.sortingOrder = short.MaxValue;
            rt.SetAsLastSibling();
            _fxLayer = rt;
            return rt;
        }

        private Image MakeFxImage(Sprite sprite, Vector2 size, Vector2 pos)
        {
            var layer = EnsureFxLayer();
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            return img;
        }

        // Lerp a RectTransform to an anchored target (from its current pos), optionally destroying it.
        private IEnumerator MoveRt(RectTransform rt, Vector2 end, float dur, bool destroyAtEnd)
        {
            if (rt == null) yield break;
            Vector2 start = rt.anchoredPosition;
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
            if (rt == null) yield break;
            rt.anchoredPosition = end;
            if (destroyAtEnd) Destroy(rt.gameObject);
        }

        private IEnumerator DelayThenMove(RectTransform rt, Vector2 end, float dur, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return MoveRt(rt, end, dur, true);
        }

        // Parabolic toss from->to with a spin, peaking at the midpoint.
        private IEnumerator ArcRt(RectTransform rt, Vector2 from, Vector2 to, float dur, float arcHeight)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                Vector2 p = Vector2.Lerp(from, to, u);
                p.y += arcHeight * 4f * u * (1f - u);
                rt.anchoredPosition = p;
                rt.localRotation = Quaternion.Euler(0f, 0f, -360f * u);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
        }

        private IEnumerator PlayPostBuildEcho(string built)
        {
            var known = database != null ? database.LookupByThai(built) : null;
            // Sound the word out grapheme-by-grapheme then blended (ปอ อา ปา), falling back to
            // the whole word. Wait the clip's length so the mic doesn't open over the model.
            var clip = known != null
                ? (known.soundOutAudio != null ? known.soundOutAudio : known.wordAudio)
                : null;
            if (clip != null)
            {
                AvHelpers.TryPlay(audioSource, clip);
                yield return new WaitForSeconds(clip.length + 0.2f);
            }
            else
            {
                // Best-effort blend: each placed tile's phoneme back-to-back.
                for (int s = 0; s < _model.SlotCount; s++)
                {
                    int trayIdx = _model.SlotTrayIndex(s);
                    if (trayIdx < 0) continue;
                    var data = _trayData[trayIdx];
                    AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
                    yield return new WaitForSeconds(0.25f);
                }
            }
        }

        private void FireGrade(string built, Outcome outcome)
        {
            if (gradeClient == null) { Debug.LogWarning("[WordBuild] no GradeApiClient; skipping /grade"); return; }

            // Correct/NonWord target the encounter word; WrongWord targets what they actually built.
            string targetId = config.target.id;
            string tag = null;
            if (outcome == Outcome.NonWord) tag = "non_word";
            else if (outcome == Outcome.WrongWord)
            {
                tag = "wrong_word";
                var builtWord = database != null ? database.LookupByThai(built) : null;
                if (builtWord != null) targetId = builtWord.id;
            }

            gradeClient.StopAndGrade(new GradeApiClient.GradeContext
            {
                targetWordId = targetId,
                childId = Kid(),
                questId = config.questId,
                sessionId = SessionContext.Instance != null ? SessionContext.Instance.SessionId : "",
                sceneId = config.sceneId,
                outcomeTag = tag,
                buildLatencyMs = _latency.HasResult ? _latency.TotalMs : 0
            }, OnGraded);
        }

        // Durable telemetry: one row per build attempt (the encounter's target word + what was
        // actually built + outcome + how long the build took). Unlike /grade this never depends on
        // the mic, so a "0 samples" mic can't lose the attempt. Also feeds the sitting aggregate.
        private void FireBuildAttempt(string built, Outcome outcome)
        {
            long latency = _latency.HasResult ? _latency.TotalMs : 0;
            if (SessionContext.Instance != null) SessionContext.Instance.RecordAttempt(latency);
            if (telemetry == null) return;
            string sid = SessionContext.Instance != null ? SessionContext.Instance.SessionId : null;
            telemetry.PostBuildAttempt(Kid(), sid, config.target.id, built, OutcomeTag(outcome), latency);
        }

        private static string OutcomeTag(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Correct: return "correct";
                case Outcome.WrongWord: return "wrong_word";
                default: return "non_word";
            }
        }

        // Clinical sink ONLY — the player must never see grade output (LD no-text rule).
        private void OnGraded(GradeResponse r)
        {
            if (r == null) return;
            if (SessionContext.Instance != null) SessionContext.Instance.RecordGrade(r.par);
            Debug.Log($"[WordBuild] /grade (hidden): PAR {r.par:0.00} grade {r.grade}");
        }

        // Telemetry kid: prefer the open session's kid so build-attempts group under the same
        // child as /grade; fall back to the config when no session is running.
        private string Kid() => SessionContext.Instance != null && !string.IsNullOrEmpty(SessionContext.Instance.KidId)
            ? SessionContext.Instance.KidId : config.childId;

        // ---- placeholder UGUI (runtime-generated) ----

        private void BuildUi()
        {
            if (trayContainer == null || slotContainer == null)
            { Debug.LogError("[WordBuild] tray/slot container not assigned."); return; }

            if (listenButton != null) listenButton.onClick.AddListener(OnListen);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (micButton != null) micButton.onClick.AddListener(OnMic);
            SetActive(listenButton, false);
            SetActive(confirmButton, false);
            SetActive(micButton, false);
        }

        private static void SetActive(Component c, bool on)
        { if (c != null) c.gameObject.SetActive(on); }

        // Build persistent tiles + slots ONCE per encounter. Tiles live at their tray home and
        // SLIDE onto slots; they are never destroyed mid-encounter (so we can animate them).
        private void BuildBoard()
        {
            if (trayContainer == null || slotContainer == null) return; // not yet authored (Task 4) -> degrade, no throw
            foreach (var t in _trayTiles) if (t != null) { t.Tapped -= OnTileTapped; t.Hovered -= OnTileHovered; Destroy(t.gameObject); }
            foreach (var s in _slots) if (s != null) { s.Tapped -= OnSlotTapped; Destroy(s.gameObject); }
            _trayTiles.Clear(); _slots.Clear(); _tileHome.Clear();

            int n = _model.SlotCount;
            for (int s = 0; s < n; s++)
            {
                var slot = MakeSlot(slotContainer, s);
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(SlotX(s, n), 0f);
                slot.Tapped += OnSlotTapped;
                _slots.Add(slot);
            }
            for (int i = 0; i < _model.TrayCount; i++)
            {
                Vector2 home = new Vector2(DistributeX(i, _model.TrayCount, trayContainer), 0f);
                _tileHome.Add(home);
                var tile = MakeTile(trayContainer, i, _model.TrayGrapheme(i), new Color(0.85f, 0.7f, 0.3f, 1f));
                ((RectTransform)tile.transform).anchoredPosition = home;
                tile.Tapped += OnTileTapped;
                tile.Hovered += OnTileHovered;
                _trayTiles.Add(tile);
            }
        }

        // The slot lives under slotContainer; the sliding tile lives under trayContainer.
        // Convert the slot's world position into trayContainer's local space so the slide lands
        // exactly on the slot regardless of where the two containers are dragged.
        private Vector2 SlotPosInTraySpace(int slotIndex)
        {
            var slotRt = (RectTransform)_slots[slotIndex].transform;
            Vector3 world = slotRt.position;
            Vector3 local = trayContainer.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        // Snap every tile back to its tray home (used on (re)entering Building / retry).
        private void ResetTilesToTray()
        {
            for (int i = 0; i < _trayTiles.Count; i++)
                if (_trayTiles[i] != null)
                    ((RectTransform)_trayTiles[i].transform).anchoredPosition = _tileHome[i];
        }

        private IEnumerator SlideTile(StoneTile tile, Vector2 to)
        {
            if (tile == null) yield break;
            _animating = true;
            var rt = (RectTransform)tile.transform;
            Vector2 from = rt.anchoredPosition;
            float t = 0f;
            while (t < slideSeconds)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / slideSeconds));
                yield return null;
            }
            rt.anchoredPosition = to;
            _animating = false;
        }

        // ---- debug controls ----

        // Listen: while still building in Supported mode, sound the word out as a model the child
        // copies (ปอ อา ปา) — the soundOutAudio clip, falling back to the whole word if unset.
        // During the say-it beat, replay the post-build echo (the same sound-out model).
        // Re-tappable and never overlaps itself (prior playback routine is cancelled).
        private void OnListen()
        {
            if (_model == null) return;
            if (_listenRoutine != null) StopCoroutine(_listenRoutine);
            if (_phase == Phase.Mic || _phase == Phase.Echo)
                _listenRoutine = StartCoroutine(PlayPostBuildEcho(_model.BuiltString));
            else if (config.mode == EncounterMode.Supported)
                AvHelpers.TryPlay(audioSource,
                    config.target.soundOutAudio != null ? config.target.soundOutAudio : config.target.wordAudio);
        }

        private void ToggleMode()
        {
            config.mode = config.mode == EncounterMode.Supported ? EncounterMode.Recall : EncounterMode.Supported;
            BeginEncounter();
        }

        private void ToggleEcho()
        {
            config.echo = !config.echo;
            BeginEncounter();
        }

        // No on-screen HUD text in the authored hierarchy (LD no-text rule); calls are harmless no-ops.
        private void UpdateHud(string text) { }

        // ---- tiny UGUI factory helpers ----

        // Evenly distribute item i of n across `container`'s width, centred. Positions derive
        // from where the (authored) container sits/sizes in the Editor — no magic numbers.
        private static float DistributeX(int i, int n, RectTransform container)
        {
            if (n <= 1) return 0f;
            float w = container.rect.width;
            float step = w / n;
            return -w / 2f + step * (i + 0.5f);
        }

        // Slots are laid out by their own size + gap (centred), so the two cells read as two
        // distinct cells regardless of container width. Tiles still use DistributeX (container-width).
        private float SlotX(int i, int n)
        {
            float step = slotSize.x + slotGap;
            float total = step * n;
            return -total / 2f + step * (i + 0.5f);
        }

        private StoneTile MakeTile(Transform parent, int index, string grapheme, Color color)
        {
            var go = new GameObject($"Tile_{index}", typeof(RectTransform), typeof(Image), typeof(StoneTile));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tileSize;
            var img = go.GetComponent<Image>();
            var icon = (index < _trayData.Count && _trayData[index] != null)
                ? _trayData[index].icon : null;
            if (icon != null) { img.sprite = icon; img.color = Color.white; img.preserveAspect = true; }
            else
            {
                // No art -> placeholder square WITH the grapheme so dev tiles aren't blank.
                // When the stone art is present it already has the letter engraved, so we add
                // NO text overlay (LD no-text rule; also removes the Thai-font dependency).
                img.color = color;
                AddCenterLabel(go.transform, grapheme, 48);
            }
            var tile = go.GetComponent<StoneTile>();
            tile.Configure(index, grapheme);
            return tile;
        }

        private TileSlot MakeSlot(Transform parent, int index)
        {
            var go = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(TileSlot));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = slotSize;
            var slotImg = go.GetComponent<Image>();
            if (slotFrameSprite != null) { slotImg.sprite = slotFrameSprite; slotImg.preserveAspect = true; slotImg.color = Color.white; }
            else slotImg.color = slotColor;
            var slot = go.GetComponent<TileSlot>();
            slot.Configure(index);
            return slot;
        }

        private static void AddCenterLabel(Transform parent, string text, float size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }

        private void StartMicPulse()
        {
            if (micIcon == null) return;
            if (_micPulse != null) StopCoroutine(_micPulse);
            _micPulse = StartCoroutine(PulseMic());
        }

        private void StopMicPulse()
        {
            if (_micPulse != null) { StopCoroutine(_micPulse); _micPulse = null; }
            if (micIcon != null) micIcon.transform.localScale = Vector3.one;
        }

        private IEnumerator PulseMic()
        {
            var tr = micIcon.transform;
            while (true)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
                tr.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

    }
}
