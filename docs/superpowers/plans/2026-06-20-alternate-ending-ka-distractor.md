# Alternate Ending — ก Distractor → กา Reveals Its Meaning — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a curated distractor stone (ก) to the ปา word-build encounter so the child can build กา (crow) — a real-but-wrong word — and turn that build into a gentle, non-punishing teaching beat that mirrors the success path.

**Architecture:** Decouple slot count from tray count in the pure `EncounterModel` (3 tray tiles, 2 slots) via a new constructor overload (TDD, Core). Activate the already-reserved `EncounterConfig.distractorTiles` list. Refactor `WordBuildEncounterController` to drive the model from a combined `target.tiles + distractorTiles` list. The existing `ResolveBuild` real-but-wrong branch (pop stone → `wrongEffectCutscene` → back to Build) already handles the กา case — the work is making กา *reachable* and giving it audio/content. Bake four Gemini-TTS owl-voice clips and wire them to `stone_ko`/`kaa` plus the config.

**Tech Stack:** Unity 6 (6000.4.3f1), C#, `WordFlow.Adventure` asmdef, NUnit EditMode tests (`WordFlow.Adventure.Tests`), MCP for Unity (`manage_scriptable_object`, `run_tests`, `read_console`, `refresh_unity`), Python (backend `app.tts.synthesize`, Gemini TTS owl voice "Leda").

## Global Constraints

- **Adventure Redesign only.** Do NOT touch the old quest flow or `word_build_prototype.unity`. Live demo scene is `word_build_paa_polished.unity`.
- **LD no-text rule.** No new child-facing text. The reveal's tone lives entirely in authored cutscene content.
- **Decision rule unchanged.** Branch by *what was built*, never by pronunciation. `/grade` stays invisible.
- **Backward-compatible by default.** Empty `distractorTiles` ⇒ today's behaviour byte-for-byte. The 1-arg `EncounterModel` constructor and every existing test/caller must remain byte-for-byte unchanged.
- **After every `.cs` edit:** poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])` must show zero errors before using the new type.
- **Audio is baked, not live.** The demo must not depend on the gateway at runtime; clips are pre-rendered WAVs imported as `AudioClip` from `Assets/Audio/Adventure/`.
- **Verified facts (do not re-derive):** `kaa.asset` = thai `กา`, meaning `crow`, tiles `[stone_ko(ก), stone_aa(า)]`, registered in `WordDatabase.asset` (7/7 word assets present) ⇒ `OutcomeEvaluator` already tags กา as `WrongWord`. `paa.asset` target tiles `[stone_po(ป), stone_aa(า)]` with wordAudio/soundOutAudio/magicStone all assigned. `stone_ko.asset` grapheme `ก`, phonemeAudio + icon currently null.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/Scripts/Adventure/Core/EncounterModel.cs` | Pure tap-to-place state | Add 2-arg ctor `(trayGraphemes, slotCount)`; 1-arg delegates |
| `Assets/Tests/Adventure/EncounterModelTests.cs` | Core unit tests | Add 4 tests for the slot/tray split |
| `Assets/Scripts/Adventure/Data/EncounterConfig.cs` | Encounter config SO | Replace forward-compat comment with real `distractorTiles` field |
| `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs` | Encounter orchestrator | Add `_trayData`; build combined tray; drive model with `slotCount`; replace 4 `config.target.tiles[...]` reads |
| `D:\Gimme\wordflow-backend\gateway\scripts\bake_kaa_distractor_clips.py` | One-off TTS bake | New script: ko / kaa_word / kaa_sound_out / kaa_try_again |
| `Assets/Data/Adventure/stones/stone_ko.asset` | ก stone SO | Wire `phonemeAudio` (MCP) |
| `Assets/Data/Adventure/words/kaa.asset` | กา word SO | Wire `wordAudio` + `soundOutAudio` (MCP) |
| `Assets/Data/Adventure/configs/encounter_paa_supported.asset` | ปา encounter config | Add `stone_ko` to `distractorTiles` (MCP) |

**User-authored (NOT in this plan's steps — handoff section at end):** `stone_ko.icon`, `kaa.magicStone`, the `e_paa_wrong_effect.asset` frame (bear + crow + owl line), Thai-text approval for the owl line, Play-test.

---

### Task 1: Core — decouple slot count from tray count (TDD)

**Files:**
- Modify: `Assets/Scripts/Adventure/Core/EncounterModel.cs:17-27` (constructor)
- Test: `Assets/Tests/Adventure/EncounterModelTests.cs` (append tests)

**Interfaces:**
- Consumes: nothing new.
- Produces: `EncounterModel(IReadOnlyList<string> trayGraphemes, int slotCount)` — `slotCount` in `[1, trayGraphemes.Count]`, else `ArgumentException`. `TrayCount` = `trayGraphemes.Count`, `SlotCount` = `slotCount`. The existing 1-arg `EncounterModel(IReadOnlyList<string>)` is preserved and now delegates with `slotCount = trayGraphemes.Count`. Task 3 calls `new EncounterModel(graphemes, config.target.tiles.Count)`.

- [ ] **Step 1: Write the failing tests** — append to `Assets/Tests/Adventure/EncounterModelTests.cs` inside the `EncounterModelTests` class, after `SlotTrayIndex_ReportsWhichTileIsWhere` (before the closing brace of the class):

```csharp
        // ---- distractor: 3 tray tiles, 2 slots (ก distractor on the ปา encounter) ----

        private static EncounterModel MakeDistractor() =>
            new EncounterModel(new[] { "ป", "า", "ก" }, slotCount: 2);

        [Test]
        public void Distractor_ThreeTilesTwoSlots_SlotCountIsTwo_TrayCountIsThree()
        {
            var m = MakeDistractor();
            Assert.AreEqual(2, m.SlotCount);
            Assert.AreEqual(3, m.TrayCount);
            Assert.IsFalse(m.IsComplete);
        }

        [Test]
        public void Distractor_BuildTarget_IgnoringDistractor_Completes()
        {
            var m = MakeDistractor();
            Assert.IsTrue(m.PlaceFromTray(0)); // ป -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // า -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("ปา", m.BuiltString);
            Assert.IsTrue(m.IsInTray(2));      // ก distractor never required, still in tray
        }

        [Test]
        public void Distractor_BuildWrongRealWord_WithDistractor_Completes()
        {
            var m = MakeDistractor();
            Assert.IsTrue(m.PlaceFromTray(2)); // ก -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // า -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void Distractor_ReturningASlot_FreesTheRightTile()
        {
            var m = MakeDistractor();
            m.PlaceFromTray(2); // ก -> slot 0
            m.PlaceFromTray(1); // า -> slot 1
            Assert.IsTrue(m.ReturnSlot(0));     // free slot 0 (held ก, tray idx 2)
            Assert.IsTrue(m.IsInTray(2));       // ก back in tray
            Assert.IsTrue(m.PlaceFromTray(0));  // ป -> first empty slot (0)
            Assert.AreEqual("ปา", m.BuiltString);
        }

        [Test]
        public void Distractor_SlotCountGuards_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new EncounterModel(new[] { "ก", "า" }, slotCount: 0));
            Assert.Throws<System.ArgumentException>(() => new EncounterModel(new[] { "ก", "า" }, slotCount: 3));
        }
```

- [ ] **Step 2: Run the new tests to verify they fail (compile error: no 2-arg ctor)**

Run via MCP: `run_tests(mode="EditMode", test_filter="EncounterModelTests")`
Expected: FAIL — compile error "No overload for method 'EncounterModel' takes 2 arguments" (the new tests reference a ctor that doesn't exist yet).

- [ ] **Step 3: Add the 2-arg constructor; make the 1-arg delegate** — replace `Assets/Scripts/Adventure/Core/EncounterModel.cs:17-27` (the single existing constructor) with:

```csharp
        public EncounterModel(IReadOnlyList<string> trayGraphemes)
            : this(trayGraphemes, trayGraphemes != null ? trayGraphemes.Count : 0) { }

        public EncounterModel(IReadOnlyList<string> trayGraphemes, int slotCount)
        {
            if (trayGraphemes == null || trayGraphemes.Count == 0)
                throw new ArgumentException("trayGraphemes must be non-empty");
            int n = trayGraphemes.Count;
            if (slotCount < 1 || slotCount > n)
                throw new ArgumentException("slotCount must be between 1 and trayGraphemes.Count");
            _trayGraphemes = new string[n];
            for (int i = 0; i < n; i++) _trayGraphemes[i] = trayGraphemes[i];
            _placed = new bool[n];
            _slotToTray = new int[slotCount];
            for (int i = 0; i < slotCount; i++) _slotToTray[i] = -1;
        }
```

Note: `_placed` is sized to the tray (`n`); `_slotToTray` is sized to `slotCount`. `IsInTray`/`TrayCount` already key off `_placed.Length`/`_trayGraphemes.Length` (= n); `SlotCount`/`BuiltString`/`IsComplete`/`PlaceFromTrayAt` already iterate `_slotToTray` (= slotCount). No other method needs changing. The null guard runs before the slotCount guard, so the delegating call with `slotCount = 0` (null tray) still throws "must be non-empty" first.

- [ ] **Step 4: Run the full Core suite to verify all pass**

Run via MCP: `run_tests(mode="EditMode", test_filter="EncounterModelTests")`
Expected: PASS — all original tests + the 5 new ones green.

- [ ] **Step 5: Confirm no compile errors project-wide**

Poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])`.
Expected: zero errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Adventure/Core/EncounterModel.cs Assets/Tests/Adventure/EncounterModelTests.cs
git commit -m "feat(adventure): EncounterModel slotCount overload (tray>slots for distractors)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Data — activate the reserved distractor list

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/EncounterConfig.cs:23` (the forward-compat comment)

**Interfaces:**
- Consumes: `StoneTileData` (existing `WordFlow.Adventure.Data` type).
- Produces: `EncounterConfig.distractorTiles` (`List<StoneTileData>`, default empty) — read by Task 3, populated via MCP in Task 5.

- [ ] **Step 1: Add the field** — in `Assets/Scripts/Adventure/Data/EncounterConfig.cs`, replace the line:

```csharp
        // Forward-compat (boss-only, not used now): shuffleTray, distractorTiles.
```

with:

```csharp
        // Extra tray tiles that are never required to complete the word (e.g. ก on the ปา
        // encounter, making the real-but-wrong word กา reachable). Empty = no distractor =
        // today's behaviour exactly. Forward-compat (boss-only, not used now): shuffleTray.
        public System.Collections.Generic.List<StoneTileData> distractorTiles = new System.Collections.Generic.List<StoneTileData>();
```

- [ ] **Step 2: Verify it compiles**

Poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])`.
Expected: zero errors.

- [ ] **Step 3: Verify the field is serialized on existing configs (no data loss)**

Run via MCP: `manage_scriptable_object` read on `Assets/Data/Adventure/configs/encounter_paa_supported.asset`.
Expected: the asset loads with a `distractorTiles` property present and empty (existing fields unchanged).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Adventure/Data/EncounterConfig.cs
git commit -m "feat(adventure): EncounterConfig.distractorTiles (extra never-required tray tiles)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Controller — compose the tray from target + distractors

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs` — add `_trayData` field (~line 48 region); rewrite `BeginEncounter` tray build (lines 106-108); replace four `config.target.tiles[...]` reads (lines 165, 182-183, 403, 616-617).

**Interfaces:**
- Consumes: `EncounterModel(graphemes, slotCount)` (Task 1), `EncounterConfig.distractorTiles` (Task 2).
- Produces: no new public surface; the encounter now lays out `target.tiles.Count + distractorTiles.Count` tray tiles over `target.tiles.Count` slots. `DistributeX` already spreads `TrayCount` tiles, so layout needs no math change.

- [ ] **Step 1: Add the combined tray-data field** — in `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`, immediately after the line `private EncounterModel _model;` (line 44), add:

```csharp
        // The ordered tile data driving the tray: target tiles first, then distractors.
        // trayIndex indexes into THIS, not config.target.tiles (which may be shorter).
        private List<StoneTileData> _trayData = new List<StoneTileData>();
```

- [ ] **Step 2: Build the combined tray + drive the model with an explicit slotCount** — in `BeginEncounter`, replace lines 106-108:

```csharp
            var graphemes = new List<string>();
            foreach (var t in config.target.tiles) graphemes.Add(t != null ? t.grapheme : "");
            _model = new EncounterModel(graphemes);
```

with:

```csharp
            // Tray = target tiles + distractors (distractors are never required to complete).
            _trayData = new List<StoneTileData>(config.target.tiles);
            if (config.distractorTiles != null) _trayData.AddRange(config.distractorTiles);
            var graphemes = new List<string>();
            foreach (var t in _trayData) graphemes.Add(t != null ? t.grapheme : "");
            _model = new EncounterModel(graphemes, slotCount: config.target.tiles.Count);
```

- [ ] **Step 3: Replace the place-time phoneme read** — in `OnTileTapped`, replace line 165:

```csharp
                var data = config.target.tiles[trayIndex];
```

with:

```csharp
                var data = _trayData[trayIndex];
```

- [ ] **Step 4: Replace the hover read + its bounds guard** — in `OnTileHovered`, replace lines 182-184:

```csharp
            if (trayIndex < 0 || trayIndex >= config.target.tiles.Count) return;
            var data = config.target.tiles[trayIndex];
            AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
```

with:

```csharp
            if (trayIndex < 0 || trayIndex >= _trayData.Count) return;
            var data = _trayData[trayIndex];
            AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
```

- [ ] **Step 5: Replace the post-build echo fallback read** — in `PlayPostBuildEcho`, replace line 403:

```csharp
                    var data = config.target.tiles[trayIdx];
```

with:

```csharp
                    var data = _trayData[trayIdx];
```

- [ ] **Step 6: Replace the tile-icon read in MakeTile** — in `MakeTile`, replace lines 616-617:

```csharp
            var icon = (index < config.target.tiles.Count && config.target.tiles[index] != null)
                ? config.target.tiles[index].icon : null;
```

with:

```csharp
            var icon = (index < _trayData.Count && _trayData[index] != null)
                ? _trayData[index].icon : null;
```

- [ ] **Step 7: Verify it compiles with zero errors**

Poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])`.
Expected: zero errors. (No grep for a stray `config.target.tiles[` index read should remain — only `config.target.tiles.Count` / `foreach (... config.target.tiles)` usages are fine.)

- [ ] **Step 8: Confirm no remaining indexed reads of `config.target.tiles[`**

Run via MCP `read_console` is not enough — grep the file:
Run: `grep -n "config.target.tiles\[" Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`
Expected: no matches.

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): controller drives tray from target+distractors (_trayData)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Content — bake the four owl-voice clips

**Files:**
- Create: `D:\Gimme\wordflow-backend\gateway\scripts\bake_kaa_distractor_clips.py`
- Produces (written into the Unity project): `Assets/Audio/Adventure/ko.wav`, `kaa_word.wav`, `kaa_sound_out.wav`, `kaa_try_again.wav`

**Interfaces:**
- Consumes: backend `app.config.Settings`, `app.tts.synthesize` (same as `bake_sound_out_clip.py`), owl voice `"Leda"`.
- Produces: 4 WAVs in `Assets/Audio/Adventure/` (Task 5 wires the first three; the user assigns `kaa_try_again.wav` to the `e_paa_wrong_effect` frame's `voiceClip`).

**Prerequisite gate:** The owl "try another one" line is editorial — **get the Thai text approved by the user before baking** (per the Part-1 voice-line process). The plan below uses **`ลองอีกคำนะ`** ("let's try another one"); if the user changes it, edit the `kaa_try_again.wav` entry's text before running.

- [ ] **Step 1: Confirm the owl line text with the user**

Ask: "The wrong-effect owl line will be baked as **`ลองอีกคำนะ`** ('let's try another one'). Approve, or give the exact Thai you want." Wait for approval; update the script text in Step 2 if changed.

- [ ] **Step 2: Write the bake script** — create `D:\Gimme\wordflow-backend\gateway\scripts\bake_kaa_distractor_clips.py`:

```python
"""One-off: bake the กา (crow) distractor clips for the ปา encounter's alternate ending.

Calls Gemini TTS (owl voice "Leda") and writes WAVs straight into the Unity project's
Assets so they import as static AudioClips — pre-rendered, so the live demo never
depends on the gateway. Run from the gateway dir with the repo venv:

    PYTHONPATH=. ../.venv/Scripts/python.exe scripts/bake_kaa_distractor_clips.py

Four clips for the alternate ending (child builds the real-but-wrong word กา):
  * ko.wav            -> StoneTileData(stone_ko).phonemeAudio  ("กอ", hover/place phonics for ก)
  * kaa_word.wav      -> WordEncounterData(kaa).wordAudio       ("กา", RevealStone replay)
  * kaa_sound_out.wav -> WordEncounterData(kaa).soundOutAudio   ("กอ อา กา", post-build echo)
  * kaa_try_again.wav -> e_paa_wrong_effect frame voiceClip     (owl: gentle "try another one")

The three phonics clips are baked BRISK (single syllable/word/sound-out), matching
bake_phoneme_clips.py / bake_sound_out_clip.py. The owl line is baked GENTLE — it must
read as encouraging, never as a fail buzzer.
"""
from pathlib import Path

from app.config import Settings
from app.tts import synthesize

_ASSETS = Path(r"D:/Gimme/NSC-Game-2026-06-03-17-40-54/Assets/Audio/Adventure")
_VOICE = "Leda"  # owl

_BRISK = (
    "Ignore any earlier slow-pacing instruction. Say this ONCE, briskly and crisply, "
    "like a quick friendly phonics prompt for a child — do not drag out the vowel."
)
_SOUND_OUT = (
    "Ignore any earlier slow-pacing instruction. Sound the word out for a child at a "
    "BRISK, lively pace: say กอ, then อา, then กา, with only a short beat between each "
    "part — quick and crisp, never slow or drawn out."
)
_GENTLE = (
    "Say this warmly and gently to a young child, encouraging and kind — like a friendly "
    "owl reassuring them it is fine to try a different word. Never sound disappointed or "
    "like a mistake buzzer."
)

_CLIPS = [
    ("ko.wav", "กอ", _BRISK),
    ("kaa_word.wav", "กา", _BRISK),
    ("kaa_sound_out.wav", "กอ อา กา", _SOUND_OUT),
    ("kaa_try_again.wav", "ลองอีกคำนะ", _GENTLE),  # owl "let's try another one" (user-approved)
]


def main() -> int:
    s = Settings()
    if not s.google_tts:
        print("FAILED — Google_TTS not in .env")
        return 1
    _ASSETS.mkdir(parents=True, exist_ok=True)
    for filename, text, style in _CLIPS:
        wav = synthesize(
            text,
            _VOICE,
            style,
            api_key=s.google_tts,
            model=s.tts_model,
            fallback_model=s.tts_fallback_model,
        )
        out = _ASSETS / filename
        out.write_bytes(wav)
        print(f"OK — wrote {len(wav)} bytes -> {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 3: Run the bake** (hits Gemini TTS — needs `Google_TTS` in the gateway `.env`; gated by the Google free-tier daily quota)

Run from `D:\Gimme\wordflow-backend\gateway`:
```bash
cd /d/Gimme/wordflow-backend/gateway && PYTHONPATH=. ../.venv/Scripts/python.exe scripts/bake_kaa_distractor_clips.py
```
Expected: four `OK — wrote <N> bytes -> .../Assets/Audio/Adventure/<file>.wav` lines (N > 0 each). If quota-blocked, retry later — do not fake the clips.

- [ ] **Step 4: Verify the four WAVs exist and are non-empty**

Run: `ls -l Assets/Audio/Adventure/ko.wav Assets/Audio/Adventure/kaa_word.wav Assets/Audio/Adventure/kaa_sound_out.wav Assets/Audio/Adventure/kaa_try_again.wav`
Expected: four files, each non-zero size.

- [ ] **Step 5: Import the new audio into Unity**

Run via MCP: `refresh_unity` (or `manage_asset` import) so the four WAVs import as `AudioClip`s.
Then poll `mcpforunity://editor/state` until `is_compiling == false` and `read_console(types=["error"])` shows no import errors.

- [ ] **Step 6: Commit**

```bash
# In the GAME repo (the WAVs land here):
git add Assets/Audio/Adventure/ko.wav Assets/Audio/Adventure/ko.wav.meta \
        Assets/Audio/Adventure/kaa_word.wav Assets/Audio/Adventure/kaa_word.wav.meta \
        Assets/Audio/Adventure/kaa_sound_out.wav Assets/Audio/Adventure/kaa_sound_out.wav.meta \
        Assets/Audio/Adventure/kaa_try_again.wav Assets/Audio/Adventure/kaa_try_again.wav.meta
git commit -m "feat(adventure): bake กา distractor owl clips (ko/kaa_word/kaa_sound_out/try_again)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```
The `bake_kaa_distractor_clips.py` script is committed separately in the backend repo:
```bash
# In D:\Gimme\wordflow-backend:
git add gateway/scripts/bake_kaa_distractor_clips.py
git commit -m "feat: bake script for กา distractor owl clips

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Wiring — assign clips and add the distractor to the config

**Files (edited via MCP `manage_scriptable_object`, not hand-edited YAML):**
- `Assets/Data/Adventure/stones/stone_ko.asset` — `phonemeAudio` ← `ko.wav`
- `Assets/Data/Adventure/words/kaa.asset` — `wordAudio` ← `kaa_word.wav`, `soundOutAudio` ← `kaa_sound_out.wav`
- `Assets/Data/Adventure/configs/encounter_paa_supported.asset` — `distractorTiles` += `stone_ko`

**Interfaces:**
- Consumes: clips from Task 4, `distractorTiles` field from Task 2.
- Produces: a `word_build_paa_polished` encounter where the tray is ป า ก and กา is reachable with audio.

- [ ] **Step 1: Wire `stone_ko.phonemeAudio`**

Run via MCP `manage_scriptable_object` on `Assets/Data/Adventure/stones/stone_ko.asset`: set `phonemeAudio` to the `AudioClip` at `Assets/Audio/Adventure/ko.wav`.
Expected: read-back shows `phonemeAudio` pointing at `ko.wav` (non-null).

- [ ] **Step 2: Wire `kaa.wordAudio` and `kaa.soundOutAudio`**

Run via MCP `manage_scriptable_object` on `Assets/Data/Adventure/words/kaa.asset`: set `wordAudio` ← `Assets/Audio/Adventure/kaa_word.wav`, `soundOutAudio` ← `Assets/Audio/Adventure/kaa_sound_out.wav`.
Expected: read-back shows both non-null.

- [ ] **Step 3: Add `stone_ko` to the encounter's `distractorTiles`**

Run via MCP `manage_scriptable_object` on `Assets/Data/Adventure/configs/encounter_paa_supported.asset`: append the `StoneTileData` at `Assets/Data/Adventure/stones/stone_ko.asset` to `distractorTiles` (resulting list = `[stone_ko]`). Leave `target`, cutscenes, and all other fields unchanged.
Expected: read-back shows `distractorTiles` with one entry referencing `stone_ko`.

- [ ] **Step 4: Sanity-check no accidental known-word collisions**

The tray ป า ก can also build ปก, กป, าก, าป, กก, ปป, าา. Confirm none is a known word in `WordDatabase.asset` (so each resolves as `NonWord` → smoke → retry, no new branch).
Run via MCP `manage_scriptable_object` read of `Assets/Data/Adventure/WordDatabase.asset`; resolve each referenced word's `thai`. The known set today is {ปา, กา, ขา, ปี, ตา, ตี, ยา} — confirm none of the above two-tile combos appears.
Expected: only ปา (target, Correct) and กา (distractor, WrongWord) are known; every other reachable combo is a non-word.

- [ ] **Step 5: Re-run the EditMode suite (regression gate)**

Run via MCP: `run_tests(mode="EditMode")` (whole suite).
Expected: all four fixtures green, including the new `EncounterModelTests` and an unchanged `OutcomeEvaluatorTests` (กา still tagged `WrongWord`).

- [ ] **Step 6: Commit**

```bash
git add Assets/Data/Adventure/stones/stone_ko.asset \
        Assets/Data/Adventure/words/kaa.asset \
        Assets/Data/Adventure/configs/encounter_paa_supported.asset
git commit -m "feat(adventure): wire กา distractor (stone_ko audio, kaa audio, ปา config)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## User Handoff (manual — outside assistant steps)

These are blocked on art/authoring/Play-mode and are the user's per the spec's division of labour:

1. **`stone_ko.icon`** ← `Assets/Art/visaul_novel/quest/stone  ก.png`.
2. **`kaa.magicStone`** ← `Assets/Art/visaul_novel/background/crow transparent.png` (the crow that "pops" and deals nothing), **or** leave null to skip the pop and let the cutscene carry the crow.
3. **`e_paa_wrong_effect.asset` frame:** author a frame with the **bear** (`character/bear/…`) + the **crow** (`crow transparent.png`) over `background_cut_scene.png`, with the owl voice line — assign `kaa_try_again.wav` to that frame's `voiceClip`. Owl/bear may use the `anim` option (owl_01–10 / bear animation art), same as intro/outro. The crow/bear sit as `extraCharacters` with explicit offsets; set `focusIndex` per beat if zooming.
4. **Approve** the Thai owl line before Task 4 Step 3 bakes it.
5. **Play-test** the reveal in `word_build_paa_polished.unity` (overlay/cutscene canvases are not screenshot-verifiable here): build กา → confirm → echo "กอ อา กา" → mic → crow pops → wrong-effect cutscene (bear + crow + owl "try another one") → back to Build with ป า ก still in the tray; then build ปา → success path unaffected.

---

## Self-Review

**Spec coverage:**
- §Behaviour (tray ป า ก, 3 tiles/2 slots) → Tasks 1+3+5. ✓
- §Component 1 (Core overload + tests) → Task 1. ✓
- §Component 2 (`distractorTiles` field) → Task 2. ✓
- §Component 3 (controller `_trayData`, four reads) → Task 3 (all four reads enumerated: 165, 182-183, 403, 616-617). ✓
- §Component 4 (bake ko/kaa_word/kaa_sound_out + owl line; user art) → Task 4 (bake) + handoff (art). ✓
- §Component 5 (no change to branch/evaluator/grade) → respected; no task touches them. ✓
- §Testing & verification → Task 1 Step 4, Task 5 Step 5 (suite + OutcomeEvaluator), per-edit console checks, user Play-test in handoff. ✓
- §Risks (reachable non-words; known-word collision) → Task 5 Step 4. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/"similar to". Every code step shows full code; every command shows expected output. ✓

**Type consistency:** `EncounterModel(graphemes, slotCount: int)` defined in Task 1, called identically in Task 3. `_trayData` (`List<StoneTileData>`) declared in Task 3 Step 1, used in Steps 2-6. `distractorTiles` (`List<StoneTileData>`) defined in Task 2, read in Task 3 Step 2, populated in Task 5 Step 3. ✓
