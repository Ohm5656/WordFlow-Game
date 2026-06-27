# Word-Build Encounter Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the standalone, art-free word-building encounter prototype from `docs/superpowers/specs/2026-06-14-word-build-encounter-design.md` — a child taps Thai grapheme tiles into slots, the build is echoed, they speak it, and `/grade` fires — verified by EditMode unit tests on the logic core and Unity play-mode/MCP screenshots on the view.

**Architecture:** Production-grade separation. A new `WordFlow.Adventure` assembly holds three layers: **Data** (ScriptableObjects mirroring backend `Word`/`Stone`), **Core** (pure C#, zero-Unity logic: `EncounterModel`, `OutcomeEvaluator`, `WavEncoder` — fully unit-tested), and **View/Net** (thin MonoBehaviours that build placeholder UGUI at runtime and drive the model). A sibling `WordFlow.Adventure.Tests` EditMode assembly tests the Core. This deliberately introduces asmdefs (new-work boundary) while leaving the old `Assembly-CSharp` quest flow untouched.

**Tech Stack:** Unity 6 (6000.4.3f1), URP 2D, uGUI + TextMeshPro, New Input System, Unity Test Framework 1.6.0, `UnityWebRequest`/`WWWForm` for `/grade`. Driven via MCP for Unity.

---

## Conventions for every task

- **Compile gate:** After any `.cs`/`.asmdef` change, poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])`. Zero errors before proceeding.
- **Test gate:** Run EditMode tests via MCP `run_tests(mode="EditMode", ...)` (or Window > General > Test Runner). A task's tests must pass before its commit.
- **Visual gate (view tasks):** `manage_camera(action="screenshot", include_image=true)` in play mode; compare against the named expectation.
- **Namespaces:** `WordFlow.Adventure.Data`, `WordFlow.Adventure.Core`, `WordFlow.Adventure.View`, `WordFlow.Adventure.Net`; tests `WordFlow.Adventure.Tests`.
- **Defensive style:** null-check and degrade (skip, don't throw), matching the existing codebase.
- **Commit** after each task with the message shown. Do not push.

## File structure (created across tasks)

```
Assets/Scripts/Adventure/
  WordFlow.Adventure.asmdef
  Data/   StoneTileData.cs  WordEncounterData.cs  WordDatabase.cs  EncounterConfig.cs
  Core/   Outcome.cs  OutcomeEvaluator.cs  EncounterModel.cs  WavEncoder.cs  AvHelpers.cs
  Net/    GradeResponse.cs  GradeApiClient.cs
  View/   StoneTile.cs  TileSlot.cs  WordBuildEncounterController.cs
Assets/Tests/Adventure/
  WordFlow.Adventure.Tests.asmdef
  OutcomeEvaluatorTests.cs  EncounterModelTests.cs  WavEncoderTests.cs  WordDatabaseTests.cs
Assets/Data/Adventure/         (.asset instances: 7 stones, 7 words, 1 database, configs)
Assets/Scenes/region 1/adventure/word_build_prototype.unity
```

## Region-1 content (locked)

| wordId | thai | tiles (graphemes, in order) |
|--------|------|------|
| kaa  | กา | ก, า |
| paa  | ปา | ป, า |
| taa  | ตา | ต, า |
| yaa  | ยา | ย, า |
| khaa | ขา | ข, า |
| tii  | ตี | ต, ี |
| pii  | ปี | ป, ี |

7 unique tiles: `ก ป ต ย ข า ี`. `kaa` and `paa` are confirmed seeded in the backend (`gateway/seed/seed_firestore.py`); use `kaa` for the live `/grade` smoke test in Task 12.

---

### Task 0: Module scaffolding (two assemblies)

**Files:**
- Create: `Assets/Scripts/Adventure/WordFlow.Adventure.asmdef`
- Create: `Assets/Tests/Adventure/WordFlow.Adventure.Tests.asmdef`

- [ ] **Step 1: Create the runtime assembly definition**

`Assets/Scripts/Adventure/WordFlow.Adventure.asmdef`:
```json
{
  "name": "WordFlow.Adventure",
  "rootNamespace": "WordFlow.Adventure",
  "references": ["UnityEngine.UI", "Unity.TextMeshPro", "Unity.InputSystem"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Create the EditMode test assembly definition**

`Assets/Tests/Adventure/WordFlow.Adventure.Tests.asmdef`:
```json
{
  "name": "WordFlow.Adventure.Tests",
  "rootNamespace": "WordFlow.Adventure.Tests",
  "references": ["WordFlow.Adventure", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 3: Compile gate.** Poll editor state to `is_compiling == false`, then `read_console(types=["error"])`.
Expected: no errors. (Empty assemblies compile clean. If `UnityEngine.UI` or `Unity.TextMeshPro` is reported as an unresolved reference, fix the reference name and recompile before continuing.)

- [ ] **Step 4: Confirm the test assembly is visible.** Open Window > General > Test Runner (EditMode tab) or call MCP `run_tests(mode="EditMode")`.
Expected: `WordFlow.Adventure.Tests` appears (0 tests). This proves the asmdef wiring before any test exists.

- [ ] **Step 5: Commit**
```bash
git add "Assets/Scripts/Adventure/WordFlow.Adventure.asmdef" "Assets/Tests/Adventure/WordFlow.Adventure.Tests.asmdef"
git commit -m "feat(adventure): scaffold Adventure runtime + EditMode test assemblies"
```

---

### Task 1: Data ScriptableObjects

These are plain data containers mirroring backend `Word`/`Stone`. Public fields (data SOs, encapsulation adds nothing here) so both tests and MCP-driven `.asset` creation can populate them. All `AudioClip`/`Sprite` fields nullable.

**Files:**
- Create: `Assets/Scripts/Adventure/Data/StoneTileData.cs`
- Create: `Assets/Scripts/Adventure/Data/WordEncounterData.cs`
- Create: `Assets/Scripts/Adventure/Data/EncounterConfig.cs`

- [ ] **Step 1: StoneTileData**
```csharp
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>One phoneme/grapheme tile. Mirrors backend Stone. Audio/icon nullable (art-free prototype).</summary>
    [CreateAssetMenu(menuName = "WordFlow/Stone Tile", fileName = "stone")]
    public sealed class StoneTileData : ScriptableObject
    {
        public string id;          // e.g. "stone_aa"
        public string grapheme;    // e.g. "า"
        public AudioClip phonemeAudio; // nullable
        public Sprite icon;            // nullable
    }
}
```

- [ ] **Step 2: WordEncounterData**
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>One word the child builds. Mirrors backend Word. wordAudio nullable.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Word Encounter", fileName = "word")]
    public sealed class WordEncounterData : ScriptableObject
    {
        public string id;      // backend wordId, e.g. "yaa"
        public string thai;    // e.g. "ยา"
        public List<StoneTileData> tiles = new List<StoneTileData>(); // ordered left->right
        public string ipa;
        public string meaning;
        public AudioClip wordAudio; // nullable
    }
}
```

- [ ] **Step 3: EncounterConfig + mode enum**
```csharp
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    public enum EncounterMode { Supported, Recall }

    /// <summary>The single source the controller reads to set up one encounter.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Encounter Config", fileName = "encounter")]
    public sealed class EncounterConfig : ScriptableObject
    {
        public EncounterMode mode = EncounterMode.Supported;
        public bool echo = false;
        public WordEncounterData target;
        public string questId = "";
        public string sceneId = "word_build_prototype";
        public string childId = "kid_demo_01";
        // Forward-compat (boss-only, not used now): shuffleTray, distractorTiles.
    }
}
```

- [ ] **Step 4: Compile gate.** Editor state idle, `read_console(types=["error"])` → no errors.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Adventure/Data/StoneTileData.cs Assets/Scripts/Adventure/Data/WordEncounterData.cs Assets/Scripts/Adventure/Data/EncounterConfig.cs
git commit -m "feat(adventure): data ScriptableObjects (StoneTileData, WordEncounterData, EncounterConfig)"
```

---

### Task 2: WordDatabase + Lookup (TDD)

**Files:**
- Create: `Assets/Scripts/Adventure/Data/WordDatabase.cs`
- Test: `Assets/Tests/Adventure/WordDatabaseTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Adventure/WordDatabaseTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;
using WordFlow.Adventure.Data;

namespace WordFlow.Adventure.Tests
{
    public sealed class WordDatabaseTests
    {
        private static WordEncounterData Word(string id, string thai)
        {
            var w = ScriptableObject.CreateInstance<WordEncounterData>();
            w.id = id; w.thai = thai;
            return w;
        }

        [Test]
        public void Lookup_ByThai_ReturnsEntry()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));
            db.words.Add(Word("paa", "ปา"));

            Assert.AreEqual("kaa", db.LookupByThai("กา").id);
        }

        [Test]
        public void Lookup_UnknownThai_ReturnsNull()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));

            Assert.IsNull(db.LookupByThai("าก"));
        }

        [Test]
        public void Contains_KnownThai_True_UnknownThai_False()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));

            Assert.IsTrue(db.ContainsThai("กา"));
            Assert.IsFalse(db.ContainsThai("xx"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run EditMode tests (`run_tests(mode="EditMode")`).
Expected: FAIL — `WordDatabase` type does not exist / `LookupByThai` undefined.

- [ ] **Step 3: Implement WordDatabase**

`Assets/Scripts/Adventure/Data/WordDatabase.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace WordFlow.Adventure.Data
{
    /// <summary>Flat list of all known Region-1 words. Used for the local outcome lookup.</summary>
    [CreateAssetMenu(menuName = "WordFlow/Word Database", fileName = "WordDatabase")]
    public sealed class WordDatabase : ScriptableObject
    {
        public List<WordEncounterData> words = new List<WordEncounterData>();

        public WordEncounterData LookupByThai(string thai)
        {
            if (string.IsNullOrEmpty(thai)) return null;
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i] != null && words[i].thai == thai) return words[i];
            }
            return null;
        }

        public bool ContainsThai(string thai) => LookupByThai(thai) != null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**
Expected: 3 PASS.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Adventure/Data/WordDatabase.cs Assets/Tests/Adventure/WordDatabaseTests.cs
git commit -m "feat(adventure): WordDatabase with thai lookup (TDD)"
```

---

### Task 3: OutcomeEvaluator (TDD)

Pure logic, zero Unity. The 3-way branch from the spec.

**Files:**
- Create: `Assets/Scripts/Adventure/Core/Outcome.cs`
- Create: `Assets/Scripts/Adventure/Core/OutcomeEvaluator.cs`
- Test: `Assets/Tests/Adventure/OutcomeEvaluatorTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Adventure/OutcomeEvaluatorTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class OutcomeEvaluatorTests
    {
        private static readonly HashSet<string> Known = new HashSet<string> { "กา", "ปา", "ตา" };

        [Test]
        public void ExactTarget_IsCorrect()
        {
            Assert.AreEqual(Outcome.Correct, OutcomeEvaluator.Evaluate("กา", "กา", Known));
        }

        [Test]
        public void DifferentKnownWord_IsWrongWord()
        {
            Assert.AreEqual(Outcome.WrongWord, OutcomeEvaluator.Evaluate("ปา", "กา", Known));
        }

        [Test]
        public void UnknownString_IsNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate("าก", "กา", Known));
        }

        [Test]
        public void EmptyBuild_IsNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate("", "กา", Known));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**
Expected: FAIL — `Outcome` / `OutcomeEvaluator` undefined.

- [ ] **Step 3: Implement**

`Assets/Scripts/Adventure/Core/Outcome.cs`:
```csharp
namespace WordFlow.Adventure.Core
{
    public enum Outcome { Correct, WrongWord, NonWord }
}
```

`Assets/Scripts/Adventure/Core/OutcomeEvaluator.cs`:
```csharp
using System.Collections.Generic;

namespace WordFlow.Adventure.Core
{
    /// <summary>Local tile-build check. Decides gameplay branch independent of /grade.</summary>
    public static class OutcomeEvaluator
    {
        public static Outcome Evaluate(string builtString, string targetThai, ICollection<string> knownWordsThai)
        {
            if (!string.IsNullOrEmpty(builtString) && builtString == targetThai) return Outcome.Correct;
            if (knownWordsThai != null && knownWordsThai.Contains(builtString)) return Outcome.WrongWord;
            return Outcome.NonWord;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**
Expected: 4 PASS.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Adventure/Core/Outcome.cs Assets/Scripts/Adventure/Core/OutcomeEvaluator.cs Assets/Tests/Adventure/OutcomeEvaluatorTests.cs
git commit -m "feat(adventure): OutcomeEvaluator 3-way build check (TDD)"
```

---

### Task 4: EncounterModel — tap-to-place state machine (TDD)

Pure C#, the gameplay brain. Tray tiles have fixed indices (tray order never changes). Slots fill left→right; returning a slot frees its tile back to its fixed tray index.

**Files:**
- Create: `Assets/Scripts/Adventure/Core/EncounterModel.cs`
- Test: `Assets/Tests/Adventure/EncounterModelTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Adventure/EncounterModelTests.cs`:
```csharp
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class EncounterModelTests
    {
        private static EncounterModel Make() => new EncounterModel(new[] { "ก", "า" });

        [Test]
        public void New_TrayFull_SlotsEmpty_NotComplete()
        {
            var m = Make();
            Assert.AreEqual(2, m.SlotCount);
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.IsInTray(1));
        }

        [Test]
        public void PlaceInOrder_BuildsTargetString()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(0)); // -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void PlacementOrderDeterminesString()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(1)); // slot 0 = า
            Assert.IsTrue(m.PlaceFromTray(0)); // slot 1 = ก
            Assert.AreEqual("าก", m.BuiltString);
        }

        [Test]
        public void PlaceAlreadyPlacedTile_Fails()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(0));
            Assert.IsFalse(m.PlaceFromTray(0));
            Assert.IsFalse(m.IsInTray(0));
        }

        [Test]
        public void PlaceWhenSlotsFull_Fails()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            Assert.IsFalse(m.PlaceFromTray(0)); // none left anyway
        }

        [Test]
        public void ReturnSlot_FreesTileBackToTray()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            Assert.IsTrue(m.ReturnSlot(0)); // returns tile that was in slot 0 (tray idx 0)
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.PlaceFromTray(0)); // re-placeable, goes to first empty slot (0)
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void ResetAll_ReturnsEverythingToTray()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            m.ResetToTray();
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.IsInTray(1));
        }

        [Test]
        public void SlotTrayIndex_ReportsWhichTileIsWhere()
        {
            var m = Make();
            m.PlaceFromTray(1);
            Assert.AreEqual(1, m.SlotTrayIndex(0)); // tray tile 1 sits in slot 0
            Assert.AreEqual(-1, m.SlotTrayIndex(1)); // slot 1 empty
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**
Expected: FAIL — `EncounterModel` undefined.

- [ ] **Step 3: Implement**

`Assets/Scripts/Adventure/Core/EncounterModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Pure tap-to-place state. Tray tiles keep fixed indices (tray order never changes).
    /// Slots fill left->right; returning a slot frees its tile back to the tray.
    /// </summary>
    public sealed class EncounterModel
    {
        private readonly string[] _trayGraphemes; // by fixed tray index
        private readonly bool[] _placed;          // tray tile currently in a slot?
        private readonly int[] _slotToTray;       // slot index -> tray index, or -1

        public EncounterModel(IReadOnlyList<string> trayGraphemes)
        {
            if (trayGraphemes == null || trayGraphemes.Count == 0)
                throw new ArgumentException("trayGraphemes must be non-empty");
            int n = trayGraphemes.Count;
            _trayGraphemes = new string[n];
            for (int i = 0; i < n; i++) _trayGraphemes[i] = trayGraphemes[i];
            _placed = new bool[n];
            _slotToTray = new int[n];
            for (int i = 0; i < n; i++) _slotToTray[i] = -1;
        }

        public int TrayCount => _trayGraphemes.Length;
        public int SlotCount => _slotToTray.Length;

        public bool IsInTray(int trayIndex) =>
            trayIndex >= 0 && trayIndex < _placed.Length && !_placed[trayIndex];

        public int SlotTrayIndex(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slotToTray.Length ? _slotToTray[slotIndex] : -1;

        public string TrayGrapheme(int trayIndex) =>
            trayIndex >= 0 && trayIndex < _trayGraphemes.Length ? _trayGraphemes[trayIndex] : "";

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _slotToTray.Length; i++)
                    if (_slotToTray[i] < 0) return false;
                return true;
            }
        }

        /// <summary>Concatenation of filled slots, left->right (partial allowed).</summary>
        public string BuiltString
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _slotToTray.Length; i++)
                {
                    int t = _slotToTray[i];
                    if (t >= 0) sb.Append(_trayGraphemes[t]);
                }
                return sb.ToString();
            }
        }

        /// <summary>Move a tray tile into the lowest empty slot. Returns the slot index, or -1 on failure.</summary>
        public int PlaceFromTrayAt(int trayIndex)
        {
            if (!IsInTray(trayIndex)) return -1;
            for (int s = 0; s < _slotToTray.Length; s++)
            {
                if (_slotToTray[s] < 0)
                {
                    _slotToTray[s] = trayIndex;
                    _placed[trayIndex] = true;
                    return s;
                }
            }
            return -1;
        }

        public bool PlaceFromTray(int trayIndex) => PlaceFromTrayAt(trayIndex) >= 0;

        /// <summary>Empty a slot; its tile returns to the tray. Returns the freed tray index, or -1.</summary>
        public int ReturnSlotAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotToTray.Length) return -1;
            int t = _slotToTray[slotIndex];
            if (t < 0) return -1;
            _slotToTray[slotIndex] = -1;
            _placed[t] = false;
            return t;
        }

        public bool ReturnSlot(int slotIndex) => ReturnSlotAt(slotIndex) >= 0;

        public void ResetToTray()
        {
            for (int s = 0; s < _slotToTray.Length; s++) _slotToTray[s] = -1;
            for (int t = 0; t < _placed.Length; t++) _placed[t] = false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**
Expected: 8 PASS.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Adventure/Core/EncounterModel.cs Assets/Tests/Adventure/EncounterModelTests.cs
git commit -m "feat(adventure): EncounterModel tap-to-place state machine (TDD)"
```

---

### Task 5: WavEncoder (TDD)

Extract the `EncodeWav` byte-layout logic from `MagicStonePuzzleController.cs:1433-1493` into a pure, testable encoder that operates on a `float[]` buffer. A thin `AudioClip` overload is added in Task 7 (where `GradeApiClient` lives) to avoid pulling audio capture into the tested core.

**Files:**
- Create: `Assets/Scripts/Adventure/Core/WavEncoder.cs`
- Test: `Assets/Tests/Adventure/WavEncoderTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Adventure/WavEncoderTests.cs`:
```csharp
using System;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class WavEncoderTests
    {
        [Test]
        public void Header_HasRiffWaveDataMarkers_AndCorrectLength()
        {
            byte[] wav = WavEncoder.Encode(new float[] { 0f, 0f, 0f, 0f }, channels: 1, frequency: 16000);
            Assert.AreEqual(44 + 4 * 2, wav.Length);
            Assert.AreEqual("RIFF", Ascii(wav, 0, 4));
            Assert.AreEqual("WAVE", Ascii(wav, 8, 4));
            Assert.AreEqual("data", Ascii(wav, 36, 4));
            Assert.AreEqual(16000, BitConverter.ToInt32(wav, 24)); // sample rate
            Assert.AreEqual(1, BitConverter.ToInt16(wav, 22));     // channels
            Assert.AreEqual(16, BitConverter.ToInt16(wav, 34));    // bits per sample
            Assert.AreEqual(4 * 2, BitConverter.ToInt32(wav, 40)); // data chunk size
        }

        [Test]
        public void FullScaleSample_EncodesToInt16Max()
        {
            byte[] wav = WavEncoder.Encode(new float[] { 1f }, channels: 1, frequency: 16000);
            Assert.AreEqual(short.MaxValue, BitConverter.ToInt16(wav, 44));
        }

        [Test]
        public void NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(WavEncoder.Encode(null, 1, 16000));
            Assert.IsNull(WavEncoder.Encode(Array.Empty<float>(), 1, 16000));
        }

        private static string Ascii(byte[] b, int offset, int len)
        {
            var c = new char[len];
            for (int i = 0; i < len; i++) c[i] = (char)b[offset + i];
            return new string(c);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**
Expected: FAIL — `WavEncoder` undefined.

- [ ] **Step 3: Implement**

`Assets/Scripts/Adventure/Core/WavEncoder.cs`:
```csharp
using System;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// 16-bit PCM mono/stereo WAV encoder. Byte layout extracted from
    /// MagicStonePuzzleController.EncodeWav so it is reusable and unit-testable.
    /// </summary>
    public static class WavEncoder
    {
        private const int HeaderSize = 44;
        private const int BytesPerSample = 2;

        public static byte[] Encode(float[] samples, int channels, int frequency)
        {
            if (samples == null || samples.Length == 0) return null;
            channels = Math.Max(1, channels);
            frequency = Math.Max(1, frequency);

            byte[] wav = new byte[HeaderSize + samples.Length * BytesPerSample];

            WriteAscii(wav, 0, "RIFF");
            WriteInt(wav, 4, wav.Length - 8);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt(wav, 16, 16);
            WriteShort(wav, 20, 1); // PCM
            WriteShort(wav, 22, (short)channels);
            WriteInt(wav, 24, frequency);
            WriteInt(wav, 28, frequency * channels * BytesPerSample);
            WriteShort(wav, 32, (short)(channels * BytesPerSample));
            WriteShort(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt(wav, 40, samples.Length * BytesPerSample);

            int offset = HeaderSize;
            for (int i = 0; i < samples.Length; i++)
            {
                int v = (int)Math.Round(samples[i] * short.MaxValue);
                if (v > short.MaxValue) v = short.MaxValue;
                if (v < short.MinValue) v = short.MinValue;
                WriteShort(wav, offset, (short)v);
                offset += BytesPerSample;
            }
            return wav;
        }

        private static void WriteAscii(byte[] t, int o, string v)
        {
            for (int i = 0; i < v.Length; i++) t[o + i] = (byte)v[i];
        }

        private static void WriteInt(byte[] t, int o, int v) =>
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, t, o, 4);

        private static void WriteShort(byte[] t, int o, short v) =>
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, t, o, 2);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**
Expected: 3 PASS. Total EditMode suite so far: 18 PASS.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Adventure/Core/WavEncoder.cs Assets/Tests/Adventure/WavEncoderTests.cs
git commit -m "feat(adventure): pure WavEncoder extracted from MagicStonePuzzleController (TDD)"
```

---

### Task 6: AvHelpers + GradeResponse DTO

The null-safe audio/sprite helpers (no-op on null → art-free) and the `/grade` response shape.

**Files:**
- Create: `Assets/Scripts/Adventure/Core/AvHelpers.cs`
- Create: `Assets/Scripts/Adventure/Net/GradeResponse.cs`

- [ ] **Step 1: AvHelpers**
```csharp
using UnityEngine;

namespace WordFlow.Adventure.Core
{
    /// <summary>Null-safe play/set helpers so the prototype runs with zero audio/art.</summary>
    public static class AvHelpers
    {
        public static void TryPlay(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip);
        }

        public static void TrySetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null) return;
            renderer.sprite = sprite;
        }

        public static void TrySetSprite(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
        }
    }
}
```

- [ ] **Step 2: GradeResponse** (matches `gateway/app/models.py` `GradeResponse`: `par`, `grade`, plus extras tolerated)
```csharp
using System;

namespace WordFlow.Adventure.Net
{
    /// <summary>Subset of the backend GradeResponse we read for the debug HUD. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class GradeResponse
    {
        public float par;
        public string grade;
    }
}
```

- [ ] **Step 3: Compile gate.** Editor idle, `read_console(types=["error"])` → no errors.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Adventure/Core/AvHelpers.cs Assets/Scripts/Adventure/Net/GradeResponse.cs
git commit -m "feat(adventure): null-safe AV helpers + GradeResponse DTO"
```

---

### Task 7: GradeApiClient — mic capture + WAV + POST /grade

Generalizes the inline mic/WWWForm logic from `MagicStonePuzzleController.cs:942-1313`. Plays no gameplay role beyond firing `/grade`; the response is logged and surfaced via an optional callback. No unit test (microphone + network); compile-verified here, live-verified in Task 12.

**Files:**
- Create: `Assets/Scripts/Adventure/Net/GradeApiClient.cs`

- [ ] **Step 1: Implement**
```csharp
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
        [SerializeField] private int maxSeconds = 4;

        public struct GradeContext
        {
            public string targetWordId;
            public string childId;
            public string questId;   // optional
            public string sessionId; // optional
            public string sceneId;   // optional (forward-compat; backend ignores)
            public string outcomeTag; // "non_word" | "wrong_word" | null (forward-compat)
        }

        /// <summary>Record then grade. onResult(GradeResponse|null) invoked when done (or on failure).</summary>
        public void RecordAndGrade(GradeContext ctx, Action<GradeResponse> onResult = null)
        {
            StartCoroutine(RecordAndGradeRoutine(ctx, onResult));
        }

        private IEnumerator RecordAndGradeRoutine(GradeContext ctx, Action<GradeResponse> onResult)
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) ||
                Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[GradeApiClient] No mic / permission; skipping /grade.");
                onResult?.Invoke(null);
                yield break;
            }

            string device = Microphone.devices[0];
            AudioClip clip = Microphone.Start(device, false, maxSeconds, sampleRate);
            float until = Time.realtimeSinceStartup + maxSeconds;
            while (Microphone.IsRecording(device) && Time.realtimeSinceStartup < until)
                yield return null;

            int sampleFrames = Mathf.Clamp(Microphone.GetPosition(device), 0, clip != null ? clip.samples : 0);
            Microphone.End(device);

            byte[] wav = EncodeClip(clip, sampleFrames);
            if (wav == null)
            {
                Debug.LogWarning("[GradeApiClient] Empty recording; skipping /grade.");
                onResult?.Invoke(null);
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddBinaryData("audio", wav, "attempt.wav", "audio/wav");
            form.AddField("targetWordId", ctx.targetWordId ?? "");
            form.AddField("childId", ctx.childId ?? "");
            if (!string.IsNullOrWhiteSpace(ctx.questId)) form.AddField("questId", ctx.questId);
            if (!string.IsNullOrWhiteSpace(ctx.sessionId)) form.AddField("sessionId", ctx.sessionId);
            if (!string.IsNullOrWhiteSpace(ctx.sceneId)) form.AddField("sceneId", ctx.sceneId);
            if (!string.IsNullOrWhiteSpace(ctx.outcomeTag)) form.AddField("outcome", ctx.outcomeTag);

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
```

- [ ] **Step 2: Compile gate.** Editor idle, `read_console(types=["error"])` → no errors.

- [ ] **Step 3: Commit**
```bash
git add Assets/Scripts/Adventure/Net/GradeApiClient.cs
git commit -m "feat(adventure): GradeApiClient mic+WAV+/grade POST (generalized from MagicStonePuzzle)"
```

---

### Task 8: Tile + Slot view components

Thin MonoBehaviours. They hold an index and raise a tap event; they own no game logic. Tap is read via `IPointerClickHandler`, driven by the InputSystem UI module on the EventSystem (consistent with the New Input System convention).

**Files:**
- Create: `Assets/Scripts/Adventure/View/StoneTile.cs`
- Create: `Assets/Scripts/Adventure/View/TileSlot.cs`

- [ ] **Step 1: StoneTile**
```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WordFlow.Adventure.View
{
    /// <summary>A tray tile placeholder (colored square + grapheme). Raises Tapped(trayIndex).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class StoneTile : MonoBehaviour, IPointerClickHandler
    {
        public int TrayIndex { get; private set; }
        public string Grapheme { get; private set; }
        public event Action<int> Tapped;

        public void Configure(int trayIndex, string grapheme)
        {
            TrayIndex = trayIndex;
            Grapheme = grapheme;
        }

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(TrayIndex);
    }
}
```

- [ ] **Step 2: TileSlot**
```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WordFlow.Adventure.View
{
    /// <summary>A slot placeholder (outlined square). Raises Tapped(slotIndex) to return its tile.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TileSlot : MonoBehaviour, IPointerClickHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<int> Tapped;

        public void Configure(int slotIndex) => SlotIndex = slotIndex;

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(SlotIndex);
    }
}
```

- [ ] **Step 3: Compile gate.** Editor idle, `read_console(types=["error"])` → no errors.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Adventure/View/StoneTile.cs Assets/Scripts/Adventure/View/TileSlot.cs
git commit -m "feat(adventure): StoneTile + TileSlot view components"
```

---

### Task 9: WordBuildEncounterController — runtime UGUI + flow

The thin view orchestrator. Builds placeholder UGUI in code at runtime (matching the project's "runtime-generated visuals" convention), owns an `EncounterModel`, and runs the spec's flow: Init → Reveal (Supported) → tap-to-place → build complete → post-build echo (always) → record+grade (always) → local outcome branch. Includes the runtime debug toggle for the 4 Supported/Recall × Echo combos.

**Files:**
- Create: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

- [ ] **Step 1: Implement**
```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WordFlow.Adventure.Core;
using WordFlow.Adventure.Data;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.View
{
    /// <summary>Drives one word-build encounter end-to-end over placeholder UGUI.</summary>
    public sealed class WordBuildEncounterController : MonoBehaviour
    {
        [SerializeField] private EncounterConfig config;
        [SerializeField] private WordDatabase database;
        [SerializeField] private GradeApiClient gradeClient;
        [SerializeField] private AudioSource audioSource;

        private EncounterModel _model;
        private readonly List<StoneTile> _trayTiles = new List<StoneTile>();
        private readonly List<TileSlot> _slots = new List<TileSlot>();
        private RectTransform _trayRow;
        private RectTransform _slotRow;
        private TextMeshProUGUI _hud;
        private Button _hearItButton;
        private Image _flash;
        private HashSet<string> _knownThai;
        private bool _busy;

        private void Start()
        {
            if (config == null || config.target == null)
            {
                Debug.LogError("[WordBuild] config/target not assigned.");
                return;
            }
            BuildKnownSet();
            BuildUi();
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
            var graphemes = new List<string>();
            foreach (var t in config.target.tiles) graphemes.Add(t != null ? t.grapheme : "");
            _model = new EncounterModel(graphemes);
            RebuildTrayAndSlots();
            _busy = false;
            UpdateHud($"Build: {config.target.thai}   [{config.mode}{(config.echo ? "+Echo" : "")}]");

            // Reveal (Supported only): play the whole word once.
            if (config.mode == EncounterMode.Supported)
                AvHelpers.TryPlay(audioSource, config.target.wordAudio);
            _hearItButton.gameObject.SetActive(config.mode == EncounterMode.Supported);
        }

        private void OnTileTapped(int trayIndex)
        {
            if (_busy || _model == null) return;
            int slot = _model.PlaceFromTrayAt(trayIndex);
            if (slot < 0) return;

            // Supported: each placed tile speaks its phoneme.
            if (config.mode == EncounterMode.Supported)
            {
                var data = config.target.tiles[trayIndex];
                AvHelpers.TryPlay(audioSource, data != null ? data.phonemeAudio : null);
            }
            RebuildTrayAndSlots();

            if (_model.IsComplete) StartCoroutine(ResolveBuild());
        }

        private void OnSlotTapped(int slotIndex)
        {
            if (_busy || _model == null) return;
            if (_model.ReturnSlotAt(slotIndex) >= 0) RebuildTrayAndSlots();
        }

        private IEnumerator ResolveBuild()
        {
            _busy = true;
            string built = _model.BuiltString;

            // Post-build echo (ALWAYS): word audio if known, else concatenated phonemes.
            yield return PlayPostBuildEcho(built);

            Outcome outcome = OutcomeEvaluator.Evaluate(built, config.target.thai, _knownThai);
            Debug.Log($"[WordBuild] built='{built}' target='{config.target.thai}' outcome={outcome}");

            // /grade ALWAYS fires (does not block branching).
            FireGrade(built, outcome);

            switch (outcome)
            {
                case Outcome.Correct:
                    yield return Flash(new Color(0.1f, 0.8f, 0.2f, 0.7f));
                    UpdateHud($"Correct! +1 level  (built {built})");
                    Debug.Log("[WordBuild] +1 level (stub)");
                    break;
                case Outcome.WrongWord:
                    yield return Flash(new Color(0.2f, 0.4f, 0.9f, 0.7f));
                    UpdateHud($"Different word ({built}). Try again.");
                    _model.ResetToTray(); RebuildTrayAndSlots(); _busy = false;
                    break;
                default: // NonWord
                    yield return Flash(new Color(0.5f, 0.5f, 0.5f, 0.7f));
                    UpdateHud($"Not a word ({built}). Try again.");
                    _model.ResetToTray(); RebuildTrayAndSlots(); _busy = false;
                    break;
            }
        }

        private IEnumerator PlayPostBuildEcho(string built)
        {
            var known = database != null ? database.LookupByThai(built) : null;
            if (known != null && known.wordAudio != null)
            {
                AvHelpers.TryPlay(audioSource, known.wordAudio);
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                // Best-effort blend: each placed tile's phoneme back-to-back.
                for (int s = 0; s < _model.SlotCount; s++)
                {
                    int trayIdx = _model.SlotTrayIndex(s);
                    if (trayIdx < 0) continue;
                    var data = config.target.tiles[trayIdx];
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

            gradeClient.RecordAndGrade(new GradeApiClient.GradeContext
            {
                targetWordId = targetId,
                childId = config.childId,
                questId = config.questId,
                sceneId = config.sceneId,
                outcomeTag = tag
            }, OnGraded);
        }

        private void OnGraded(GradeResponse r)
        {
            if (r == null) return;
            UpdateHud(_hud.text + $"\n/grade: PAR {r.par:0.00} grade {r.grade}");
        }

        // ---- placeholder UGUI (runtime-generated) ----

        private void BuildUi()
        {
            var canvasGo = new GameObject("EncounterCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);

            _slotRow = MakeRow(canvas.transform, "Slots", new Vector2(0, 60));
            _trayRow = MakeRow(canvas.transform, "Tray", new Vector2(0, -120));

            _hud = MakeLabel(canvas.transform, "HUD", new Vector2(0, 280), 28);
            _hud.alignment = TextAlignmentOptions.Center;

            _hearItButton = MakeButton(canvas.transform, "HearIt", "Hear it", new Vector2(0, 180), ReplayWord);

            // Debug combo toggles.
            MakeButton(canvas.transform, "ToggleMode", "Mode: Supported/Recall", new Vector2(-260, -260), ToggleMode);
            MakeButton(canvas.transform, "ToggleEcho", "Echo on/off", new Vector2(260, -260), ToggleEcho);

            _flash = MakeFlash(canvas.transform);
        }

        private void RebuildTrayAndSlots()
        {
            foreach (var t in _trayTiles) if (t != null) Destroy(t.gameObject);
            foreach (var s in _slots) if (s != null) Destroy(s.gameObject);
            _trayTiles.Clear(); _slots.Clear();

            for (int i = 0; i < _model.TrayCount; i++)
            {
                var tile = MakeTile(_trayRow, i, _model.TrayGrapheme(i),
                    new Color(0.85f, 0.7f, 0.3f, 1f));
                tile.gameObject.SetActive(_model.IsInTray(i));
                tile.Tapped += OnTileTapped;
                _trayTiles.Add(tile);
            }
            for (int s = 0; s < _model.SlotCount; s++)
            {
                int trayIdx = _model.SlotTrayIndex(s);
                string label = trayIdx >= 0 ? _model.TrayGrapheme(trayIdx) : "";
                var slot = MakeSlot(_slotRow, s, label);
                slot.Tapped += OnSlotTapped;
                _slots.Add(slot);
            }
        }

        // ---- debug controls ----

        private void ReplayWord()
        {
            if (config.mode == EncounterMode.Supported)
                AvHelpers.TryPlay(audioSource, config.target.wordAudio);
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

        private void UpdateHud(string text) { if (_hud != null) _hud.text = text; }

        private IEnumerator Flash(Color color)
        {
            if (_flash == null) yield break;
            _flash.color = color;
            _flash.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.6f);
            _flash.gameObject.SetActive(false);
        }

        // ---- tiny UGUI factory helpers ----

        private static RectTransform MakeRow(Transform parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(800, 140);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 24; h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }

        private StoneTile MakeTile(Transform parent, int index, string grapheme, Color color)
        {
            var go = new GameObject($"Tile_{index}", typeof(RectTransform), typeof(Image), typeof(StoneTile));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(120, 120);
            go.GetComponent<Image>().color = color;
            AddCenterLabel(go.transform, grapheme, 48);
            var tile = go.GetComponent<StoneTile>();
            tile.Configure(index, grapheme);
            return tile;
        }

        private TileSlot MakeSlot(Transform parent, int index, string label)
        {
            var go = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(TileSlot));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(120, 120);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            AddCenterLabel(go.transform, label, 48);
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

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 pos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(1000, 120);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(360, 80);
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            AddCenterLabel(go.transform, label, 28);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            return btn;
        }

        private static Image MakeFlash(Transform parent)
        {
            var go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false; img.color = new Color(0, 0, 0, 0);
            go.SetActive(false);
            return img;
        }
    }
}
```

- [ ] **Step 2: Compile gate.** Editor idle, `read_console(types=["error"])` → no errors.

- [ ] **Step 3: Commit**
```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): WordBuildEncounterController runtime UGUI + spec flow"
```

---

### Task 10: ScriptableObject content assets

Create the 7 stones, 7 words, 1 database, and 2 configs as `.asset` files. Use MCP `manage_scriptable_object` (create + set fields) or `manage_asset`. All audio/sprite fields left null.

**Files (created under `Assets/Data/Adventure/`):**
- `stones/stone_ko.asset` (ก), `stone_po.asset` (ป), `stone_to.asset` (ต), `stone_yo.asset` (ย), `stone_kho.asset` (ข), `stone_aa.asset` (า), `stone_ii.asset` (ี)
- `words/kaa.asset … pii.asset` (per the content table; `tiles` referencing the stone assets in order)
- `WordDatabase.asset` (all 7 words)
- `configs/encounter_kaa_supported.asset` (mode=Supported, echo=false, target=kaa)
- `configs/encounter_kaa_recall.asset` (mode=Recall, echo=false, target=kaa)

- [ ] **Step 1: Create the 7 StoneTileData assets.** For each, set `id` (e.g. `stone_aa`) and `grapheme` (e.g. `า`).
- [ ] **Step 2: Create the 7 WordEncounterData assets.** Set `id`, `thai`, `ipa`, `meaning`, and `tiles` = the ordered stone references from the content table (e.g. `kaa` → [stone_ko, stone_aa]).
- [ ] **Step 3: Create WordDatabase.asset** with `words` = all 7 word assets.
- [ ] **Step 4: Create the 2 EncounterConfig assets** targeting `kaa`.
- [ ] **Step 5: Verify** via `manage_asset(action="search", ...)` or read each `.asset`: confirm 7+7+1+2 = 17 assets exist and `kaa.tiles` resolves to [ก, า] in order.
- [ ] **Step 6: Commit**
```bash
git add Assets/Data/Adventure
git commit -m "feat(adventure): Region-1 content assets (7 stones, 7 words, database, configs)"
```

---

### Task 11: Scene + Build Settings + behavioral verification

New standalone scene. Does NOT modify `quest_map1`, `cut_scene1`, `practice`.

**Files:**
- Create: `Assets/Scenes/region 1/adventure/word_build_prototype.unity`

- [ ] **Step 1: Create the scene** via `manage_scene(action="create", ...)`. Add: Main Camera, Directional Light, an **EventSystem with the InputSystemUIInputModule** (required for `IPointerClickHandler` under the New Input System).
- [ ] **Step 2: Add a bootstrap GameObject** `EncounterRoot` with components `WordBuildEncounterController`, `GradeApiClient`, and an `AudioSource`. Wire serialized fields: `config = encounter_kaa_supported`, `database = WordDatabase`, `gradeClient = (the GradeApiClient on this GO)`, `audioSource = (the AudioSource on this GO)`.
- [ ] **Step 3: Add the scene to Build Settings** (enabled) via `manage_editor`/`manage_build` so `SceneManager.LoadScene("word_build_prototype")` resolves later.
- [ ] **Step 4: Enter play mode** (`manage_editor(action="play")`). Screenshot.
  Expected (`Supported` mode): HUD reads "Build: กา [Supported]", a row of 2 tray tiles (ก, า) below 2 empty slots, "Hear it" button visible, two debug toggle buttons.
- [ ] **Step 5: Verify tap-to-place + return.** Use `manage_gameobject`/simulated pointer or click in the Game view: tap tile ก then า → both slots fill; tap a filled slot → tile returns to tray; tray order unchanged. Screenshot each. `read_console(types=["log"])` shows the build trace.
- [ ] **Step 6: Verify Correct path.** Build กา in order → green flash, console `outcome=Correct`, HUD "Correct! +1 level". (With the backend down, `/grade` logs a connection error but does NOT block the branch — confirm the green flash still happens.)
- [ ] **Step 7: Verify NonWord path.** Build าก (reversed) → grey flash, console `outcome=NonWord`, slots reset.
- [ ] **Step 8: Verify combos.** Click "Mode" and "Echo" toggles; confirm HUD tag changes (Supported↔Recall, +Echo) and that Recall suppresses the reveal/per-tile audio paths (no errors; with null audio this is a log/flow check, not an audible one). Screenshot.
- [ ] **Step 9: Exit play mode, save scene. Commit**
```bash
git add "Assets/Scenes/region 1/adventure/word_build_prototype.unity" "Assets/Scenes/region 1/adventure/word_build_prototype.unity.meta" ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(adventure): word_build_prototype scene wired + in Build Settings"
```

---

### Task 12: Live /grade smoke test (backend-gated, optional)

Only runnable once the FastAPI gateway is up. This is the final spec test criterion. If the backend session hasn't happened yet, mark this task blocked and stop — the prototype is complete and self-verifying without it.

- [ ] **Step 1: Start the backend** (separate session/terminal): from `D:\Gimme\wordflow-backend\gateway`, run the app on `127.0.0.1:8000`. To avoid the heavy gated Thai-IPA model during a smoke test, prefer `RECOGNIZER_IMPL=fake` for this run. Confirm `GET /api/v1/words` lists `kaa`.
- [ ] **Step 2: Play the scene, build กา (Correct).** With mic permission granted, speak when the mic opens.
- [ ] **Step 3: Verify** console shows `[GradeApiClient] /grade 200:` with a JSON body, and the HUD appends `PAR x.xx grade Y`. Confirm `targetWordId=kaa` was sent.
- [ ] **Step 4 (NonWord grade target):** build าก → confirm `/grade` still fires with `targetWordId=kaa` and form field `outcome=non_word`.
- [ ] **Step 5:** No commit needed (verification only); note results in the session.

---

## Self-Review

**Spec coverage:**
- 2-mode scaffold (Supported/Recall) → `EncounterMode`, `ToggleMode`, reveal/per-tile audio gated on Supported (Tasks 1, 9). ✓
- Tiles always start in tray, never pre-placed → `EncounterModel` ctor + `RebuildTrayAndSlots` (Tasks 4, 9). ✓
- Tiles never shuffled; tray order fixed → fixed tray indices in `EncounterModel` (Task 4). ✓
- Echo orthogonal, 4 combos switchable → `echo` field + `ToggleEcho` + HUD tag (Tasks 1, 9). ✓
- Post-build echo always, every mode → `PlayPostBuildEcho` called unconditionally before grade (Task 9). ✓
- `/grade` fires every attempt, never blocks branch → `FireGrade` before the branch switch; response via callback only (Tasks 7, 9). ✓
- Grade target rules (Correct/NonWord → encounter word; WrongWord → built word) + outcome tag → `FireGrade` (Task 9). ✓
- Data model mirrors backend Word/Stone; nullable AV; TryPlay/TrySetSprite no-op → Tasks 1, 6. ✓
- Controller flow steps 1–8 → Task 9. ✓
- WrongWord unreachable-but-implemented note → `OutcomeEvaluator` 3-way kept; documented in Task 12 (only kaa/paa reachable as real words, reversed = NonWord). ✓
- File/scene plan, placeholder visuals, prototype data, all 4 combos test → Tasks 8–11. ✓

**Placeholder scan:** No TBD/TODO; every code step has complete code. ✓
**Type consistency:** `PlaceFromTrayAt`/`PlaceFromTray`, `ReturnSlotAt`/`ReturnSlot`, `SlotTrayIndex`, `TrayGrapheme`, `BuiltString`, `LookupByThai`/`ContainsThai`, `OutcomeEvaluator.Evaluate(string,string,ICollection<string>)`, `WavEncoder.Encode(float[],int,int)`, `GradeApiClient.RecordAndGrade(GradeContext, Action<GradeResponse>)` — all defined before use and consistent across tasks. ✓

**Known deviation from spec:** spec lists `sceneId` as a `/grade` field; the actual endpoint (`gateway/app/routers/grade.py`) declares only `audio,targetWordId,childId,questId,sessionId`. We still send `sceneId`+`outcome` as extra form fields (FastAPI ignores unknown form fields) for forward-compat, matching the old `MagicStonePuzzleController` behavior. No backend change in scope.
