# Day-1 Loop Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the word-build encounter into a finished Day-1 loop (full-length cutscene voice, a real mic record beat, a stone-reveal + word-effect cutscene resolution, slide animation, all-icon UI, and a build-latency KPI), validated on the existing ปา / bear encounter.

**Architecture:** Extends the layered `WordFlow.Adventure` assembly. One new pure-C# Core class (`BuildLatencyTracker`, unit-tested). Targeted edits to `CutscenePlayer` (Bug A), `GradeApiClient` (Bug B), the two data SOs (`WordEncounterData`, `EncounterConfig`), and `WordBuildEncounterController` (build slide, icon UI, mic beat, stone reveal + effect cutscenes, latency wiring). Scene work duplicates `word_build_prototype.unity` and wires ปา content via MCP. Voice lines are authored in the backend repo (cross-repo, last).

**Tech Stack:** Unity 6 (6000.4.3f1), URP 2D, New Input System, uGUI/TextMeshPro, NUnit EditMode tests, MCP for Unity for all scene/SO work. Backend = FastAPI `/tts` + `/grade` at `http://127.0.0.1:8001` with `AUTH_IMPL=fake`.

## Global Constraints

- **No child-facing text — everything is an ICON.** Mic = mic/pulse icon, Confirm = checkmark icon, Hear-it = speaker icon. No HUD prompt text. The ONLY on-screen glyphs are the Thai grapheme tiles (the learning content). Dev/debug controls may keep text but MUST be hidden in the child build (gate behind a serialized `showDebugControls` defaulting to `false`). Where final icon art is missing, use a clearly iconic placeholder glyph (✓, ►, a pulsing disc) — **never a text label, even as a stopgap.**
- **Nullable-art-safe.** Every new `Sprite`/`AudioClip`/`CutsceneData` field is nullable; null must degrade to the current placeholder/no-op (use `AvHelpers.TryPlay`/`TrySetSprite`). Nothing breaks when art is missing.
- **`/grade` is invisible.** The child never sees anything about pronunciation. Grading never decides or blocks the resolve branch.
- **Branch by what was BUILT, not by grade.** Real word (Correct or WrongWord) → stone reveal + effect cutscene. Non-word → smoke + retry.
- **Do not touch the old quest flow** (`MagicStonePuzzleController`, `cut_scene*`, `WorldMap`, `quest_map1`, `success`) or its scenes.
- **Unity workflow:** after any `.cs` edit, poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])` before using the new type. `execute_code` is broken — use `manage_scriptable_object` / `manage_*` tools for SO/scene work.
- **Core layer has zero `UnityEngine` gameplay deps** and is the only unit-tested layer. Run tests via MCP `run_tests` (`mode="EditMode"`).
- Commit messages use `feat(adventure):` / `fix(adventure):` and end with `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

### Task 1: `BuildLatencyTracker` (Core, unit-tested)

Pure C# clock that times the build phase. Fed timestamps (seconds, from `Time.realtimeSinceStartupAsDouble` at the call site). Produces total build latency and per-tile placement latencies in milliseconds. Spec change #5.

**Files:**
- Create: `Assets/Scripts/Adventure/Core/BuildLatencyTracker.cs`
- Test: `Assets/Tests/Adventure/BuildLatencyTrackerTests.cs`

**Interfaces:**
- Produces (consumed by Task 7 controller wiring):
  - `void Start(double nowSeconds)` — clock starts when tiles become interactive; clears prior placements.
  - `void RecordPlacement(double nowSeconds)` — one tile placed; appends the gap since the previous event (Start or prior placement) in ms.
  - `void Complete(double nowSeconds)` — build finished (`IsComplete`); sets `TotalMs` = `nowSeconds - startSeconds` in ms, `HasResult = true`.
  - `void Reset()` — back to empty/idle (`HasResult = false`, placements cleared).
  - `bool HasResult { get; }`
  - `long TotalMs { get; }`
  - `IReadOnlyList<long> PerTilePlacementMs { get; }`

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Tests/Adventure/BuildLatencyTrackerTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class BuildLatencyTrackerTests
    {
        [Test]
        public void Idle_HasNoResult()
        {
            var t = new BuildLatencyTracker();
            Assert.IsFalse(t.HasResult);
            Assert.AreEqual(0, t.PerTilePlacementMs.Count);
        }

        [Test]
        public void TwoTiles_TotalAndPerTile_AreMilliseconds()
        {
            var t = new BuildLatencyTracker();
            t.Start(10.0);
            t.RecordPlacement(11.0);   // 1.0s after start
            t.RecordPlacement(12.5);   // 1.5s after previous placement
            t.Complete(12.5);
            Assert.IsTrue(t.HasResult);
            Assert.AreEqual(2500, t.TotalMs);
            CollectionAssert.AreEqual(new List<long> { 1000, 1500 }, t.PerTilePlacementMs);
        }

        [Test]
        public void Reset_ClearsResultAndPlacements()
        {
            var t = new BuildLatencyTracker();
            t.Start(0.0);
            t.RecordPlacement(0.4);
            t.Complete(0.4);
            t.Reset();
            Assert.IsFalse(t.HasResult);
            Assert.AreEqual(0, t.TotalMs);
            Assert.AreEqual(0, t.PerTilePlacementMs.Count);
        }

        [Test]
        public void StartAfterUse_StartsFresh()
        {
            var t = new BuildLatencyTracker();
            t.Start(0.0); t.RecordPlacement(0.5); t.Complete(0.5);
            t.Start(100.0);
            t.RecordPlacement(100.2);
            Assert.IsFalse(t.HasResult);              // not Complete yet this round
            CollectionAssert.AreEqual(new List<long> { 200 }, t.PerTilePlacementMs);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via MCP: `run_tests(mode="EditMode", test_filter="BuildLatencyTrackerTests")`
Expected: compile error / FAIL — `BuildLatencyTracker` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/Scripts/Adventure/Core/BuildLatencyTracker.cs
using System;
using System.Collections.Generic;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Times the build phase from "tiles interactive" (Start) to IsComplete (Complete).
    /// Pure C#, zero Unity deps. Latency is measured on BUILDING (cognition / RAN-relevant),
    /// not on speech onset. Fed seconds; reports milliseconds.
    /// </summary>
    public sealed class BuildLatencyTracker
    {
        private readonly List<long> _perTileMs = new List<long>();
        private double _startSeconds;
        private double _lastEventSeconds;
        private bool _running;

        public bool HasResult { get; private set; }
        public long TotalMs { get; private set; }
        public IReadOnlyList<long> PerTilePlacementMs => _perTileMs;

        public void Start(double nowSeconds)
        {
            _perTileMs.Clear();
            _startSeconds = nowSeconds;
            _lastEventSeconds = nowSeconds;
            _running = true;
            HasResult = false;
            TotalMs = 0;
        }

        public void RecordPlacement(double nowSeconds)
        {
            if (!_running) return;
            _perTileMs.Add(ToMs(nowSeconds - _lastEventSeconds));
            _lastEventSeconds = nowSeconds;
        }

        public void Complete(double nowSeconds)
        {
            if (!_running) return;
            TotalMs = ToMs(nowSeconds - _startSeconds);
            HasResult = true;
            _running = false;
        }

        public void Reset()
        {
            _perTileMs.Clear();
            _running = false;
            HasResult = false;
            TotalMs = 0;
        }

        private static long ToMs(double seconds) => (long)Math.Round(Math.Max(0.0, seconds) * 1000.0);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `run_tests(mode="EditMode", test_filter="BuildLatencyTrackerTests")`
Expected: 4/4 PASS. Then `run_tests(mode="EditMode")` — whole suite stays green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Adventure/Core/BuildLatencyTracker.cs Assets/Scripts/Adventure/Core/BuildLatencyTracker.cs.meta Assets/Tests/Adventure/BuildLatencyTrackerTests.cs Assets/Tests/Adventure/BuildLatencyTrackerTests.cs.meta
git commit -m "feat(adventure): BuildLatencyTracker Core KPI (total + per-tile build latency)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Data fields for stone reveal + wrong-effect cutscene

Add the nullable fields the resolve sequence (Task 8) reads. Spec change #4. Open question #4 resolved: effect-cutscene field lives on `EncounterConfig` (mirrors the existing intro/outro slots); **Correct reuses the existing `outroCutscene` slot**, Wrong is the new `wrongEffectCutscene`.

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/WordEncounterData.cs`
- Modify: `Assets/Scripts/Adventure/Data/EncounterConfig.cs`

**Interfaces:**
- Produces (consumed by Task 8): `WordEncounterData.magicStone : Sprite` (nullable), `EncounterConfig.wrongEffectCutscene : CutsceneData` (nullable). `EncounterConfig.outroCutscene` is the de-facto `correctEffectCutscene`.

- [ ] **Step 1: Add `magicStone` to `WordEncounterData`**

In `WordEncounterData.cs`, after the `wordAudio` line:

```csharp
        public AudioClip wordAudio; // nullable
        public Sprite magicStone;   // nullable; revealed (popped + sound replayed) when this word is built
```

- [ ] **Step 2: Add `wrongEffectCutscene` to `EncounterConfig`**

In `EncounterConfig.cs`, replace the cutscene block comment + fields:

```csharp
        // Encounter cutscenes (NPC-only VN, voice + visuals, no text). All nullable —
        // a null slot keeps the original no-cutscene flow byte-for-byte.
        public CutsceneData introCutscene;       // plays before the word-build
        public CutsceneData outroCutscene;        // = correct-EFFECT cutscene: plays after a Correct build
        public CutsceneData wrongEffectCutscene;  // plays after a WrongWord build (real word, wrong context)
        // Forward-compat (boss-only, not used now): shuffleTray, distractorTiles.
```

- [ ] **Step 3: Verify compile**

Poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])`.
Expected: no errors. Existing `encounter_*.asset` files are unaffected (new fields default to null).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Adventure/Data/WordEncounterData.cs Assets/Scripts/Adventure/Data/EncounterConfig.cs
git commit -m "feat(adventure): add magicStone + wrongEffectCutscene data fields (nullable)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Cutscene voice never cut off (`CutscenePlayer`) — Bug A

Root cause: `ttsTimeout = 3f` makes the player give up on a slow/uncached `/tts` fetch after 3s, play nothing, and advance. The frame-hold already waits the full clip length (`Mathf.Max(frame.holdSeconds, clipLen)`); the only fix needed is raising the fetch safety cap to ~8s so the clip is actually obtained before it plays. Spec change #2. Surgical — one field + comment.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/CutscenePlayer.cs:24`

- [ ] **Step 1: Raise the fetch safety cap**

Replace line 24:

```csharp
        [SerializeField] private float ttsTimeout = 3f; // don't hang the demo on a slow fetch
```

with:

```csharp
        // Safety cap on a slow/uncached /tts fetch. We WAIT up to this long for the clip
        // rather than dropping the voice (Bug A: 3s cut off uncached lines). The frame still
        // then holds for the full clip length, and a tap can advance/skip early.
        [SerializeField] private float ttsTimeout = 8f;
```

- [ ] **Step 2: Verify compile**

Poll editor state until not compiling, then `read_console(types=["error"])`. Expected: no errors.

- [ ] **Step 3: Live-smoke the fix (deferred to Task 9 scene)**

Note: full audio verification requires the backend running and the duplicated scene (Task 9). At this step only confirm compile. The behavioural check ("intro voice plays in full, no cutoff") is performed in Task 9's manual verification.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Adventure/View/CutscenePlayer.cs
git commit -m "fix(adventure): cutscene voice cut off — raise /tts fetch cap 3s->8s (Bug A)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Mic = real record beat — `GradeApiClient` split (Bug B, engine half)

Split the single fixed blocking `RecordAndGrade` into `StartRecording()` + `StopAndGrade(ctx, onResult)` so the controller can drive a real "tap to start / tap again or auto-stop" beat. Add the forward-compat `buildLatencyMs` multipart field. Spec changes #3 + #5. `RecordAndGrade` is kept here (controller still calls it until Task 8) and removed in Task 8.

**Files:**
- Modify: `Assets/Scripts/Adventure/Net/GradeApiClient.cs`

**Interfaces:**
- Produces (consumed by Task 8): `void StartRecording()`, `bool IsRecording { get; }`, `void StopAndGrade(GradeContext ctx, Action<GradeResponse> onResult = null)`.
- Produces (consumed by Task 7): `GradeContext.buildLatencyMs : long`.

- [ ] **Step 1: Bump record buffer to 5s**

Replace `GradeApiClient.cs:18`:

```csharp
        [SerializeField] private int maxSeconds = 4;
```

with:

```csharp
        [SerializeField] private int maxSeconds = 5; // matches the controller's 5s mic auto-stop
```

- [ ] **Step 2: Add `buildLatencyMs` to `GradeContext`**

In the `GradeContext` struct, after `outcomeTag`:

```csharp
            public string outcomeTag; // "non_word" | "wrong_word" | null (forward-compat)
            public long buildLatencyMs; // forward-compat KPI; 0 = unset (backend ignores for now)
```

- [ ] **Step 3: Add recording state + `StartRecording`/`IsRecording`/`StopAndGrade`**

Add fields below the serialized fields (after `maxSeconds`):

```csharp
        private string _device;
        private AudioClip _recording;
        private bool _isRecording;

        public bool IsRecording => _isRecording;
```

Add these methods (place right above `RecordAndGrade`):

```csharp
        /// <summary>Begin capturing a mic clip. No mic/permission -> stays not-recording (IsRecording==false).</summary>
        public void StartRecording() => StartCoroutine(StartRecordingRoutine());

        private IEnumerator StartRecordingRoutine()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) ||
                Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[GradeApiClient] No mic / permission; recording skipped.");
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
                Debug.LogWarning("[GradeApiClient] Empty recording; skipping /grade.");
                onResult?.Invoke(null);
                yield break;
            }
            yield return PostGrade(wav, ctx, onResult);
        }
```

- [ ] **Step 4: Extract the multipart POST into `PostGrade` (shared by old + new path)**

Replace the body of `RecordAndGradeRoutine` from the `WWWForm form = ...` line through the end of its `using (...)` block with a call to the shared helper, and add the helper. The simplest surgical form — replace everything from line `WWWForm form = new WWWForm();` to the end of the method body with:

```csharp
            yield return PostGrade(wav, new GradeContext
            {
                targetWordId = ctx.targetWordId,
                childId = ctx.childId,
                questId = ctx.questId,
                sessionId = ctx.sessionId,
                sceneId = ctx.sceneId,
                outcomeTag = ctx.outcomeTag,
                buildLatencyMs = ctx.buildLatencyMs
            }, onResult);
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
```

(The pre-existing mic-acquisition top half of `RecordAndGradeRoutine` — auth, `Microphone.Start`, the record-wait loop, `EncodeClip` — stays unchanged; only its POST tail is replaced by the `PostGrade` call above. `RecordAndGrade` therefore still works and is removed in Task 8.)

- [ ] **Step 5: Verify compile**

Poll editor state until not compiling, then `read_console(types=["error"])`. Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Adventure/Net/GradeApiClient.cs
git commit -m "feat(adventure): split GradeApiClient into Start/StopAndGrade + buildLatencyMs (Bug B)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Build mechanic — click-slide + reusable stone sprite

Replace the destroy-and-rebuild `RebuildTrayAndSlots()` snap with persistent tiles that **slide** between a tray home and a slot, and wire the nullable `StoneTileData.icon` so tiles render the reusable magic-stone sprite (glyph drawn on top). Investigation result: only a shared `Assets/Art/visaul_novel/quest/magic_stone.png` frame plus one per-grapheme `stone  ก.png` exist — so tiles use the shared frame via `StoneTileData.icon`; null icon keeps the current colored square. Spec change #1.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

**Interfaces:**
- Produces (used by Tasks 6–8): persistent `_trayTiles`/`_slots` (never destroyed during an encounter); `IEnumerator SlideTile(StoneTile tile, Vector2 to)`; `void ResetTilesToTray()`; `_boardRoot` holds tiles + slots in one coordinate space.
- Consumes: `EncounterModel.PlaceFromTrayAt`, `ReturnSlotAt`, `SlotTrayIndex`, `IsComplete`; `StoneTileData.icon`.

- [ ] **Step 1: Add board fields + tile-home storage**

In the field block (after `_slotRow;`), add:

```csharp
        private RectTransform _boardRoot;
        private readonly List<Vector2> _tileHome = new List<Vector2>();
        private bool _animating;
        [SerializeField] private float slideSeconds = 0.22f;
        private const float TileSize = 120f, TileGap = 28f, TrayY = -120f, SlotY = 60f;
```

- [ ] **Step 2: Replace `BuildUi` row creation with a single board root**

In `BuildUi`, replace the two `MakeRow` lines:

```csharp
            _slotRow = MakeRow(canvas.transform, "Slots", new Vector2(0, 60));
            _trayRow = MakeRow(canvas.transform, "Tray", new Vector2(0, -120));
```

with:

```csharp
            _boardRoot = MakeStretch(canvas.transform, "Board");
```

Then delete the now-orphaned `private RectTransform _trayRow;` and `private RectTransform _slotRow;` fields (your change made them unused).

Add this helper near the other factory helpers:

```csharp
        private static RectTransform MakeStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Evenly-spaced anchored X for index i of n items, centered on x=0.
        private static float RowX(int i, int n)
        {
            float step = TileSize + TileGap;
            float startX = -(step * (n - 1)) / 2f;
            return startX + i * step;
        }
```

Delete the now-unused `MakeRow` method (it is orphaned by this change).

- [ ] **Step 3: Replace `RebuildTrayAndSlots` with one-time persistent construction**

Replace the entire `RebuildTrayAndSlots()` method with:

```csharp
        // Build persistent tiles + slots ONCE per encounter. Tiles live at their tray home and
        // SLIDE onto slots; they are never destroyed mid-encounter (so we can animate them).
        private void BuildBoard()
        {
            foreach (var t in _trayTiles) if (t != null) { t.Tapped -= OnTileTapped; Destroy(t.gameObject); }
            foreach (var s in _slots) if (s != null) { s.Tapped -= OnSlotTapped; Destroy(s.gameObject); }
            _trayTiles.Clear(); _slots.Clear(); _tileHome.Clear();

            int n = _model.SlotCount;
            for (int s = 0; s < n; s++)
            {
                var slot = MakeSlot(_boardRoot, s, "");
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(RowX(s, n), SlotY);
                slot.Tapped += OnSlotTapped;
                _slots.Add(slot);
            }
            for (int i = 0; i < _model.TrayCount; i++)
            {
                Vector2 home = new Vector2(RowX(i, _model.TrayCount), TrayY);
                _tileHome.Add(home);
                var tile = MakeTile(_boardRoot, i, _model.TrayGrapheme(i), new Color(0.85f, 0.7f, 0.3f, 1f));
                ((RectTransform)tile.transform).anchoredPosition = home;
                tile.Tapped += OnTileTapped;
                _trayTiles.Add(tile);
            }
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
```

- [ ] **Step 4: Re-point callers from `RebuildTrayAndSlots` to the new methods**

- In `EnterBuildingPhase`, change `RebuildTrayAndSlots();` to:
  ```csharp
            if (_trayTiles.Count == 0) BuildBoard(); else ResetTilesToTray();
  ```
- In `OnTileTapped`, replace the `RebuildTrayAndSlots();` call and gate on `_animating`:
  ```csharp
        private void OnTileTapped(int trayIndex)
        {
            if (_phase != Phase.Building || _model == null || _animating) return;
            int slot = _model.PlaceFromTrayAt(trayIndex);
            if (slot < 0) return;

            if (config.mode == EncounterMode.Supported)
            {
                var data = config.target.tiles[trayIndex];
                AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
            }

            Vector2 slotPos = ((RectTransform)_slots[slot].transform).anchoredPosition;
            StartCoroutine(PlaceTileRoutine(trayIndex, slotPos));
        }

        private IEnumerator PlaceTileRoutine(int trayIndex, Vector2 slotPos)
        {
            yield return SlideTile(_trayTiles[trayIndex], slotPos);
            if (_model.IsComplete) EnterConfirmPhase();
        }
```
- In `OnSlotTapped`, replace the `RebuildTrayAndSlots();` else-branch so the returned tile slides home:
  ```csharp
        private void OnSlotTapped(int slotIndex)
        {
            if (_model == null || _animating || (_phase != Phase.Building && _phase != Phase.Confirm)) return;
            int trayIdx = _model.SlotTrayIndex(slotIndex);
            if (_model.ReturnSlotAt(slotIndex) < 0) return;
            if (_phase == Phase.Confirm) { _phase = Phase.Building; _confirmButton.gameObject.SetActive(false); }
            if (trayIdx >= 0) StartCoroutine(SlideTile(_trayTiles[trayIdx], _tileHome[trayIdx]));
        }
```
  (Note: the old `OnSlotTapped` called `EnterBuildingPhase()` on un-confirm, which would `ResetTilesToTray`. We instead just drop the phase + hide Confirm and slide the single tile home, so the other placed tiles stay put.)

- [ ] **Step 5: Wire the reusable stone sprite + tile size in `MakeTile`**

Replace `MakeTile` with:

```csharp
        private StoneTile MakeTile(Transform parent, int index, string grapheme, Color color)
        {
            var go = new GameObject($"Tile_{index}", typeof(RectTransform), typeof(Image), typeof(StoneTile));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(TileSize, TileSize);
            var img = go.GetComponent<Image>();
            var icon = (index < config.target.tiles.Count && config.target.tiles[index] != null)
                ? config.target.tiles[index].icon : null;
            if (icon != null) { img.sprite = icon; img.color = Color.white; img.preserveAspect = true; }
            else img.color = color; // no art -> current placeholder square
            AddCenterLabel(go.transform, grapheme, 48);
            var tile = go.GetComponent<StoneTile>();
            tile.Configure(index, grapheme);
            return tile;
        }
```

And update `MakeSlot` so slots anchor to center and carry no glyph (the tile carries it):

```csharp
        private TileSlot MakeSlot(Transform parent, int index, string label)
        {
            var go = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(TileSlot));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(TileSize, TileSize);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            var slot = go.GetComponent<TileSlot>();
            slot.Configure(index);
            return slot;
        }
```

- [ ] **Step 6: Verify compile + visual**

Poll editor until not compiling; `read_console(types=["error"])` — no errors. Defer full Play-mode slide screenshots to Task 9 (the duplicated scene). At minimum confirm the controller compiles and no `RebuildTrayAndSlots`/`MakeRow` references remain (`find_in_file` for both — expect none).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): click-slide tiles + reusable stone sprite (build mechanic)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: Icon-ify the UI (no child-facing text)

Replace the text-label buttons with iconic placeholders, remove the on-screen HUD prompt, and hide the debug toggles behind a serialized flag. Glyph placeholders only: Confirm = `✓`, Hear-it = `►`, Mic = a pulsing disc (no glyph; identity is colour + pulse). Spec "Hard rule" + change in §"The polished loop". `UpdateHud` keeps logging to the console (devs) but draws nothing in the child build.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

**Interfaces:**
- Produces (used by Task 7/8): `_micButton` is an iconic disc; `Image _micIcon` reference for pulsing; `IEnumerator PulseMic()`/`StopPulseMic()`.

- [ ] **Step 1: Add the debug flag + mic-icon field**

In the field block add:

```csharp
        [SerializeField] private bool showDebugControls = false; // hidden in the child build
        private Image _micIcon;
        private Coroutine _micPulse;
```

- [ ] **Step 2: Convert `MakeButton` to an icon button + add a glyph-icon factory**

Add an icon-button helper and keep the old text `MakeButton` ONLY for debug controls:

```csharp
        // Square iconic button: colored disc + a single optional glyph (NOT a text label).
        private (Button btn, Image icon) MakeIconButton(Transform parent, string name, string glyph,
            Color color, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(140, 140);
            var img = go.GetComponent<Image>(); img.color = color;
            if (!string.IsNullOrEmpty(glyph)) AddCenterLabel(go.transform, glyph, 64);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            return (go.GetComponent<Button>(), img);
        }
```

- [ ] **Step 3: Replace the UI build block in `BuildUi`**

Replace everything from `_hud = MakeLabel(...)` through the debug `MakeButton(... "ToggleEcho" ...)` line with:

```csharp
            // HUD prompt text is REMOVED for the child build (LD no-text rule); the prompt is
            // carried by NPC/owl voice + the empty slots. Keep a hidden label only for debug.
            if (showDebugControls)
            {
                _hud = MakeLabel(canvas.transform, "HUD", new Vector2(0, 280), 28);
                _hud.alignment = TextAlignmentOptions.Center;
            }

            _hearItButton = MakeIconButton(canvas.transform, "HearIt", "►",
                new Color(0.2f, 0.45f, 0.8f, 0.95f), new Vector2(0, 200), ReplayWord).btn;

            _confirmButton = MakeIconButton(canvas.transform, "Confirm", "✓",
                new Color(0.15f, 0.7f, 0.25f, 0.95f), new Vector2(0, 90), OnConfirm).btn;
            var mic = MakeIconButton(canvas.transform, "Mic", "",
                new Color(0.85f, 0.25f, 0.25f, 0.95f), new Vector2(0, 90), OnMic);
            _micButton = mic.btn; _micIcon = mic.icon;
            _confirmButton.gameObject.SetActive(false);
            _micButton.gameObject.SetActive(false);

            if (showDebugControls)
            {
                MakeButton(canvas.transform, "ToggleMode", "Mode: Supported/Recall", new Vector2(-260, -260), ToggleMode);
                MakeButton(canvas.transform, "ToggleEcho", "Echo on/off", new Vector2(260, -260), ToggleEcho);
            }
```

(`MakeButton`, `MakeLabel` stay — used by debug only. `_hud` may be null now; `UpdateHud` already null-checks `_hud`, so its callers still work and simply log via the `Debug.Log` lines that remain in the resolve code.)

- [ ] **Step 4: Add the mic pulse coroutine (used in Task 8)**

```csharp
        private void StartMicPulse()
        {
            if (_micIcon == null) return;
            if (_micPulse != null) StopCoroutine(_micPulse);
            _micPulse = StartCoroutine(PulseMic());
        }
        private void StopMicPulse()
        {
            if (_micPulse != null) { StopCoroutine(_micPulse); _micPulse = null; }
            if (_micIcon != null) _micIcon.transform.localScale = Vector3.one;
        }
        private IEnumerator PulseMic()
        {
            var tr = _micIcon.transform;
            while (true)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
                tr.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }
```

- [ ] **Step 5: Verify compile + screenshot**

Poll editor until not compiling; `read_console(types=["error"])` — no errors. Defer the "no text on screen" screenshot to Task 9. (`AddCenterLabel` renders the `✓`/`►` glyphs and the Thai tiles; ensure a Thai-capable TMP font is assigned per Task 9 / open question #3.)

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): icon-only UI (mic/check/speaker), hide HUD + debug in child build

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: Wire `BuildLatencyTracker` into the controller

Measure build latency and pass it on the existing `/grade` POST. Clock starts when tiles become interactive (end of `BeginEncounter`), records each placement, completes at `IsComplete`, resets on retry. Spec change #5. This compiles standalone — it still uses the existing `RecordAndGrade`/`FireGrade` (which now carries `buildLatencyMs` from Task 4).

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

**Interfaces:**
- Consumes: `BuildLatencyTracker` (Task 1), `GradeContext.buildLatencyMs` (Task 4).
- Produces (used by Task 8): `_latency` field already populated by resolve time.

- [ ] **Step 1: Add the tracker field**

In the field block:

```csharp
        private readonly Core.BuildLatencyTracker _latency = new Core.BuildLatencyTracker();
```

(Or add `using` — the controller already `using WordFlow.Adventure.Core;`, so just `private readonly BuildLatencyTracker _latency = new BuildLatencyTracker();`.)

- [ ] **Step 2: Start the clock when tiles go interactive**

At the very end of `BeginEncounter` (after the Hear-it button line):

```csharp
            _latency.Start(Time.realtimeSinceStartupAsDouble);
```

- [ ] **Step 3: Record placements and completion**

In `PlaceTileRoutine` (from Task 5), record the placement when the slide lands and complete on `IsComplete`:

```csharp
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
```

- [ ] **Step 4: Reset on retry / re-begin**

`BeginEncounter` calls `_latency.Start`, which already resets. Add a `_latency.Reset()` wherever the build is sent back to the tray after a wrong/non-word — these live in `ResolveBuild` today (the `_model.ResetToTray(); EnterBuildingPhase();` branches). Insert `_latency.Reset();` immediately before each `_model.ResetToTray();` in `ResolveBuild`. (Task 8 rewrites `ResolveBuild`; if Task 8 runs after, it carries these resets — keep them in the Task 8 version too. For Task 7 alone, add them to the current `ResolveBuild`.)

- [ ] **Step 5: Send `buildLatencyMs` on `/grade`**

In `FireGrade`, set the new field in the `GradeContext` initializer:

```csharp
                outcomeTag = tag,
                buildLatencyMs = _latency.HasResult ? _latency.TotalMs : 0
```

- [ ] **Step 6: Verify compile**

Poll editor until not compiling; `read_console(types=["error"])` — no errors. Spot-check in Play: build ปา, confirm the console `/grade` request line includes a non-zero `buildLatencyMs` (visible in the backend log at Task 9).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): wire BuildLatencyTracker -> buildLatencyMs on /grade

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: Mic record beat + stone reveal + effect cutscenes + non-word smoke

Rewrite the post-Confirm sequence into the polished resolve: real mic beat (tap to start / tap again or 5s auto-stop, mic pulses), wait for capture, fire `/grade` in the background, then resolve by what was **built** — real word → pop its magic stone (~1.5s, replay its sound) → effect cutscene; non-word → black-smoke + funny SFX → retry. Spec changes #3 (controller half) + #4. Removes the now-unused `RecordAndGrade` and the green/blue/grey `Flash` resolution.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`
- Modify: `Assets/Scripts/Adventure/Net/GradeApiClient.cs` (remove orphaned `RecordAndGrade`)

**Interfaces:**
- Consumes: `GradeApiClient.StartRecording/StopAndGrade/IsRecording` (Task 4), `WordEncounterData.magicStone` + `EncounterConfig.wrongEffectCutscene`/`outroCutscene` (Task 2), `_latency` (Task 7), `database.LookupByThai`, `StartMicPulse/StopMicPulse` (Task 6).

- [ ] **Step 1: Add mic-beat state + tunables**

In the field block:

```csharp
        [SerializeField] private float micSeconds = 5f;       // auto-stop
        [SerializeField] private float stoneRevealSeconds = 1.5f;
        [SerializeField] private AudioClip nonWordSfx;        // nullable; funny non-demotivating "nope"
        private bool _recording;
        private bool _micStopRequested;
        private Image _smoke;                                 // black-smoke puff overlay
```

- [ ] **Step 2: Replace `OnMic` with a start/stop tap handler + the mic-and-resolve routine**

Replace `OnMic` and `ResolveBuild` with:

```csharp
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
            _micButton.gameObject.SetActive(false);

            // Background /grade (invisible, never blocks visuals). No mic -> degrade: just resolve.
            FireGrade(built, outcome);

            // ---- resolve by what was BUILT ----
            _phase = Phase.Resolving;
            yield return ResolveBuild(built, outcome);
        }

        private IEnumerator ResolveBuild(string built, Outcome outcome)
        {
            Debug.Log($"[WordBuild] built='{built}' target='{config.target.thai}' outcome={outcome}");
            var builtWord = database != null ? database.LookupByThai(built) : null;

            if (builtWord != null) // REAL WORD (Correct or WrongWord) -> reveal + effect cutscene
            {
                yield return RevealStone(builtWord);

                bool correct = outcome == Outcome.Correct;
                CutsceneData effect = correct ? config.outroCutscene : config.wrongEffectCutscene;
                if (effect != null && cutscenePlayer != null)
                {
                    bool done = false;
                    cutscenePlayer.Play(effect, () => done = true);
                    while (!done) yield return null;
                }

                if (correct)
                {
                    UpdateHud($"Correct! +1 level (built {built})");
                    Debug.Log("[WordBuild] +1 level (stub)");
                    _phase = Phase.Done;
                }
                else
                {
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
            go.transform.SetParent(_boardRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 40);
            rt.sizeDelta = new Vector2(260, 260);
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
                go.transform.SetParent(_boardRoot, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 40);
                rt.sizeDelta = new Vector2(240, 240);
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
```

- [ ] **Step 3: Remove the old `Flash` resolution + orphaned helpers**

The new `ResolveBuild` no longer calls `Flash(...)`. Remove the `Flash` method and the `_flash` field and its `MakeFlash` call/method (orphaned by this change — they were only used by the superseded green/blue/grey resolution). Confirm with `find_in_file` that `_flash`, `Flash`, `MakeFlash` have no remaining references.

- [ ] **Step 4: Remove the orphaned `RecordAndGrade` from `GradeApiClient`**

`FireGrade` now calls `gradeClient.StopAndGrade(...)` instead of `RecordAndGrade(...)`. Update `FireGrade`'s call:

```csharp
            gradeClient.StopAndGrade(new GradeApiClient.GradeContext
            {
                targetWordId = targetId,
                childId = config.childId,
                questId = config.questId,
                sceneId = config.sceneId,
                outcomeTag = tag,
                buildLatencyMs = _latency.HasResult ? _latency.TotalMs : 0
            }, OnGraded);
```

Then delete the now-unused `RecordAndGrade` + `RecordAndGradeRoutine` from `GradeApiClient.cs` (the mic-acquisition top half moved conceptually into `StartRecording`/`StopAndGrade`; `EncodeClip` and `PostGrade` stay). Confirm no references to `RecordAndGrade` remain (`Grep` the Adventure tree).

- [ ] **Step 5: Verify compile**

Poll editor until not compiling; `read_console(types=["error"])` — no errors. `run_tests(mode="EditMode")` — existing suite + `BuildLatencyTrackerTests` still green (no Core changes here, but confirm nothing broke the assembly).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs Assets/Scripts/Adventure/Net/GradeApiClient.cs
git commit -m "feat(adventure): mic record beat + stone reveal + effect cutscenes + non-word smoke

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: Duplicate the scene, wire ปา content, live-smoke the loop

Duplicate `word_build_prototype.unity`, leave the original intact, wire the new content fields on the ปา config/word, assign a Thai-capable TMP font, confirm the network/EventSystem setup, and manually verify the whole loop against the backend. Spec change #7 + open questions #1–#3.

**Files:**
- Create: `Assets/Scenes/region 1/adventure/word_build_paa_polished.unity` (duplicate)
- Modify (assets, via MCP): `Assets/Data/Adventure/words/paa.asset`, `Assets/Data/Adventure/configs/encounter_paa_supported.asset`, ปา stone assets' `icon`
- Create (via MCP): `Assets/Data/Adventure/Cutscenes/e_paa_wrong_effect.asset` (and reuse `e_paa_outro.asset` as the correct-effect cutscene)

- [ ] **Step 1: Duplicate the scene**

Use MCP `manage_asset` (action `duplicate`) to copy `Assets/Scenes/region 1/adventure/word_build_prototype.unity` to `Assets/Scenes/region 1/adventure/word_build_paa_polished.unity`. Add the copy to Build Settings (enabled) via `manage_editor` / build settings tooling. Leave the original scene and its Build Settings entry untouched.

- [ ] **Step 2: Confirm network + EventSystem on the copy**

Open the duplicated scene (`manage_scene` load). Verify via `manage_gameobject`/`manage_components`:
- `GradeApiClient.gradeUrl` = `http://127.0.0.1:8001/api/v1/grade`, `authorization` = `Bearer demo-token`.
- `TtsApiClient` base/`ttsUrl` = `http://127.0.0.1:8001` (`...:8001/api/v1/tts`), `Authorization: Bearer demo-token`.
- The `EventSystem` uses `InputSystemUIInputModule` (NOT the legacy `StandaloneInputModule`).
- A Thai-capable TMP font asset is assigned as the TMP default / on the tiles' labels (open question #3: `LiberationSans SDF` renders ก/า as □). If none exists, create a TMP Font Asset from a Thai font (e.g. Noto Sans Thai) and set it as the TMP Settings default font. Verify by screenshot that ป/า render as glyphs, not □.

- [ ] **Step 3: Wire ปา content assets**

Via `manage_scriptable_object`:
- `words/paa.asset` → set `magicStone` to `Assets/Art/visaul_novel/quest/magic_stone.png` (shared frame; per investigation, no per-grapheme ป/า art exists).
- ปา's two `StoneTileData` assets (the ป and า stones referenced by `paa.asset.tiles`) → set `icon` to `magic_stone.png` (shared frame) so tiles render the stone. (`stone  ก.png` is available only for ก / the กา encounter.)
- `configs/encounter_paa_supported.asset` → set `wrongEffectCutscene` to `e_paa_wrong_effect.asset` (created next). `outroCutscene` already = `e_paa_outro.asset` and now doubles as the correct-effect cutscene.

- [ ] **Step 4: Create the wrong-effect cutscene asset**

Via `manage_scriptable_object` create `Assets/Data/Adventure/Cutscenes/e_paa_wrong_effect.asset` (`CutsceneData`) with one `CutsceneFrame`: `npc` = bear sprite (reuse the ปา outro's NPC), `voiceLineId` = `e_paa_wrong_effect` (authored in Task 10), `holdSeconds` = 2.0. Leave `voiceClip` null (fetched from `/tts`).

> Prototype reality (spec): with only ปา's two tiles in the tray, WrongWord is unreachable — only Correct or NonWord fire. The wrong-effect wiring is built anyway (same code path) and becomes reachable when boss distractor tiles are added later. Don't block on testing the WrongWord branch live.

- [ ] **Step 5: Start the backend**

From `D:\Gimme\wordflow-backend\gateway\`:
```bash
AUTH_IMPL=fake ../.venv/Scripts/python.exe -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```
Confirm `GET http://127.0.0.1:8001/api/v1/tts?line_id=e_paa_intro` (with `Authorization: Bearer demo-token`) returns audio, not `401`.

- [ ] **Step 6: Play-mode walkthrough with screenshots**

Enter Play (`manage_editor`). Capture `manage_camera(action="screenshot", include_image=true)` at each beat and confirm:
- intro cutscene plays and **voice is heard in full, no cutoff** (Bug A fixed).
- BUILD: tapping a tray tile **slides** it onto a slot; tapping a slot slides it back.
- the tiles show the **stone sprite** with the Thai glyph on top; **no on-screen text** anywhere except the grapheme tiles + the `✓`/`►` icon glyphs.
- CONFIRM is the green `✓` icon; tapping it plays the echo, then the red **mic** icon appears.
- MIC: tapping the mic starts a record beat — the icon **pulses**; it stops on a second tap or after 5s; resolve **waits** for it (no "just cuts").
- RESOLVE (Correct, building ปา): the magic stone **pops ~1.5s + replays its sound**, then the outro/effect cutscene plays, console logs `+1 level`.
- RESOLVE (Non-word, e.g. build า+ป = าป if reachable, or temporarily reorder): **black smoke puff + SFX**, back to build.
- backend log shows the `/grade` POST including a non-zero `buildLatencyMs`.

Record any failures and fix before committing (use systematic-debugging if a beat misbehaves).

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scenes/region 1/adventure/word_build_paa_polished.unity" "Assets/Scenes/region 1/adventure/word_build_paa_polished.unity.meta" Assets/Data/Adventure/ "ProjectSettings/EditorBuildSettings.asset"
git commit -m "feat(adventure): polished ปา scene + content wiring (stone, wrong-effect cutscene, font)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 10: Author word-effect voice lines (cross-repo, backend)

Add the correct/wrong word-effect owl/bear lines to the backend TTS catalog and pre-warm them so `/tts` serves from cache. Spec change #6. **Cross-repo:** `D:\Gimme\wordflow-backend`. Lines below are drafts for review — finalize Thai wording with the user during this task.

**Files:**
- Modify: `D:\Gimme\wordflow-backend\gateway\tts_lines.json`
- Run: `D:\Gimme\wordflow-backend\scripts\prewarm_tts.py`

- [ ] **Step 1: Add the line entries**

Add to `gateway/tts_lines.json` (match the existing entry shape — confirm key/field names by reading neighbouring entries first):
- `e_paa_correct_effect` — bear/owl celebrating the word landing (ปา = "throw"; the magic of the word happening). Draft Thai: "ปา! เก่งมาก ลูกทำได้แล้ว!" — confirm with user.
- `e_paa_wrong_effect` — gentle, non-demotivating: a real word, just not the one this needs. Draft Thai: "นี่ก็เป็นคำจริงนะ แต่ยังไม่ใช่คำที่เราต้องการ ลองอีกครั้งนะ" — confirm with user.

(If `encounter_paa_supported.outroCutscene` / `e_paa_outro` already carries a correct-effect line, reuse that `voiceLineId` instead of adding `e_paa_correct_effect`, and only add `e_paa_wrong_effect`.)

- [ ] **Step 2: Pre-warm the cache**

```bash
cd D:/Gimme/wordflow-backend
.venv/Scripts/python.exe scripts/prewarm_tts.py
```
Expected: the new line_ids synthesize and land in the disk cache. If blocked by the Google free-tier daily quota, note it and retry next day (known open follow-up) — the loop still runs (CutscenePlayer waits up to 8s, then silent frame).

- [ ] **Step 3: Smoke the new lines**

```bash
curl -s -H "Authorization: Bearer demo-token" "http://127.0.0.1:8001/api/v1/tts?line_id=e_paa_wrong_effect" -o /tmp/wrong.mp3 && file /tmp/wrong.mp3
```
Expected: audio file, not a 401/empty.

- [ ] **Step 4: Commit (in the backend repo)**

```bash
cd D:/Gimme/wordflow-backend
git add gateway/tts_lines.json
git commit -m "feat(tts): ปา word-effect lines (correct + wrong) for Day-1 loop polish

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Plan-level verification

- `run_tests(mode="EditMode")` — full Adventure suite green, including `BuildLatencyTrackerTests`.
- The duplicated `word_build_paa_polished` scene plays the full polished loop end-to-end against the backend on `:8001` with zero child-facing text and no voice cutoff (Task 9, Step 6 checklist).
- The original `word_build_prototype.unity` and the entire old quest flow are unchanged (`git diff --stat` touches only Adventure scripts/data, the new scene, build settings, and the backend repo).

## Out of scope (carried from spec)

Old quest flow, auto-run/world-map, boss/distractor tiles, **ยา content authoring** (pure content drop-in afterward: author its cutscenes + owl lines + stone art; zero engine work), backend consumption of `buildLatencyMs`, final icon/stone art production.
