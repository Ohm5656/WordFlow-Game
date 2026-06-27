# Encounter-Loop Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make one full word-build encounter (intro cutscene → build → outro) demo-ready: bigger tiles/slots/stones, a real background behind the book, transparent slot container + buttons, author-tunable cutscene character size and dimming, a procedural "float-in" entrance, and more natural Thai voice lines.

**Architecture:** Pure additive/plumbing changes in `WordFlow.Adventure` (View + Data). Expose hardcoded magic numbers as serialized fields; add nullable/default-zero fields to the cutscene `ScriptableObject`s so existing assets are byte-for-byte unchanged; split the cutscene overlay's single dark backdrop into a faithful BG layer + a separate dim layer; add a position-only procedural entrance. Voice lines are a JSON content edit on the backend, resolved by stable `line_id`.

**Tech Stack:** Unity 6 (6000.4.3f1), URP 2D, uGUI/TMP, New Input System, MCP for Unity (no CLI build). Backend: FastAPI gateway + `tts_lines.json` + Gemini TTS.

## Global Constraints

- **No on-screen text** for any child-facing affordance (LD rule). Only the Thai grapheme tiles are glyphs. Buttons stay icon-only.
- **Do not touch** the old quest flow or `word_build_prototype.unity`. Work targets `word_build_paa_polished.unity` and `Assets/Data/Adventure/Cutscenes/e_*.asset`.
- **Adventure assembly:** all C# under `Assets/Scripts/Adventure/` (assembly `WordFlow.Adventure`).
- **Existing cutscene assets must be unchanged by new fields** — every new field defaults to a value that reproduces today's behaviour (`Vector2.zero` = "use default", entrance `None`, dims = current constants).
- **Unity verification (every code task):** after editing a `.cs`, poll `mcpforunity://editor/state` until `is_compiling == false`, then `read_console(types=["error"])` → expect none. Then `run_tests(mode="EditMode")` → expect **24/24 pass** (regression guard; no new Core tests). Paths use forward slashes, relative to `Assets/`.
- **MCP caveat:** ScreenSpace-Overlay canvases (EncounterCanvas, CutscenePlayer overlay) cannot be screenshotted — visual confirmation is the **user in Play mode**.

---

### Task 1: Word-build page tunable sizes

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

**Interfaces:**
- Produces: serialized fields `tileSize`, `slotSize`, `slotGap`, `stoneRevealSize`, `smokeSize`, `slotFrameSprite`, `slotColor` on `WordBuildEncounterController`; new private `float SlotX(int i, int n)`.

- [ ] **Step 1: Add the size fields.** In the field block (after `stoneRevealSeconds` ~line 60), add:

```csharp
        [Header("Layout sizes")]
        [SerializeField] private Vector2 tileSize = new Vector2(200f, 200f);
        [SerializeField] private Vector2 slotSize = new Vector2(210f, 210f);
        [SerializeField] private float slotGap = 40f;                 // space between the two slot cells
        [SerializeField] private Vector2 stoneRevealSize = new Vector2(420f, 420f);
        [SerializeField] private Vector2 smokeSize = new Vector2(400f, 400f);
        [SerializeField] private Sprite slotFrameSprite;              // nullable -> faint box fallback
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.12f);
```

- [ ] **Step 2: Use `tileSize` in `MakeTile`.** Replace `rt.sizeDelta = new Vector2(120f, 120f);` with:

```csharp
            rt.sizeDelta = tileSize;
```

- [ ] **Step 3: Use `slotSize`/`slotFrameSprite`/`slotColor` in `MakeSlot`.** Replace these two lines:

```csharp
            rt.sizeDelta = new Vector2(120f, 120f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
```

with:

```csharp
            rt.sizeDelta = slotSize;
            var slotImg = go.GetComponent<Image>();
            if (slotFrameSprite != null) { slotImg.sprite = slotFrameSprite; slotImg.preserveAspect = true; slotImg.color = Color.white; }
            else slotImg.color = slotColor;
```

- [ ] **Step 4: Lay slots out by size+gap.** Add this helper next to `DistributeX`:

```csharp
        // Slots are laid out by their own size + gap (centred), so the two cells read as two
        // distinct cells regardless of container width. Tiles still use DistributeX (container-width).
        private float SlotX(int i, int n)
        {
            float step = slotSize.x + slotGap;
            float total = step * n;
            return -total / 2f + step * (i + 0.5f);
        }
```

In `BuildBoard`, replace the slot positioning line:

```csharp
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(DistributeX(s, n, slotContainer), 0f);
```

with:

```csharp
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(SlotX(s, n), 0f);
```

- [ ] **Step 5: Use `stoneRevealSize` and `smokeSize`.** In `RevealStone` replace `rt.sizeDelta = new Vector2(260, 260);` with `rt.sizeDelta = stoneRevealSize;`. In `SmokePuff` replace `rt.sizeDelta = new Vector2(240, 240);` with `rt.sizeDelta = smokeSize;`.

- [ ] **Step 6: Verify.** Poll `is_compiling == false`; `read_console(types=["error"])` → none. `run_tests(mode="EditMode")` → 24/24.

- [ ] **Step 7: Commit.**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "feat(adventure): tunable tile/slot/stone/smoke sizes + slot layout by size+gap"
```

---

### Task 2: Background behind the book + transparency authoring

**Files:**
- Modify (via MCP scene edit): `Assets/Scenes/region 1/adventure/word_build_paa_polished.unity`
- No `.cs` changes.

**Interfaces:**
- Produces: a `SceneBackground` `Image` GameObject as the first child of `EncounterCanvas`.

- [ ] **Step 1: Open the scene.** `manage_scene(action="load", name="word_build_paa_polished")` (or its full path). Confirm load via the returned hierarchy.

- [ ] **Step 2: Find EncounterCanvas.** `find_gameobjects` for `EncounterCanvas`; note its instance id and confirm `BookBackground` is a child.

- [ ] **Step 3: Create SceneBackground.** Use `manage_gameobject` to create a child of `EncounterCanvas` named `SceneBackground` with a `UnityEngine.UI.Image` component. Set RectTransform anchors stretched (`anchorMin=(0,0)`, `anchorMax=(1,1)`, `offsetMin=(0,0)`, `offsetMax=(0,0)`), `Image.raycastTarget=false`, `Image.preserveAspect=false`, and leave `sprite` empty. Then move it to be the **first** sibling (drawn behind `BookBackground`) — `SetAsFirstSibling` (via `manage_gameobject` sibling-index set to 0).

- [ ] **Step 4: Save the scene.** `manage_scene(action="save")`.

- [ ] **Step 5: Verify structure.** `find_gameobjects`/scene hierarchy shows `SceneBackground` as `EncounterCanvas`'s first child with a stretched Image. (No screenshot — overlay canvas.)

- [ ] **Step 6: Commit.**

```bash
git add "Assets/Scenes/region 1/adventure/word_build_paa_polished.unity"
git commit -m "feat(adventure): add SceneBackground layer behind BookBackground"
```

- [ ] **Step 7: Hand the user the authoring tutorial** (they own sprites/transparency). Deliver these exact Editor steps:
  1. Open `word_build_paa_polished` and select `EncounterCanvas/SceneBackground`. Drag your full-screen background sprite onto **Image → Source Image**. (Replaces the blue clear-colour around the book.)
  2. Select `SlotContainer` → Image → set **Color alpha to 0** (container becomes invisible; the two slot cells still show). If you have a slot-frame sprite, instead assign it to the controller's **Slot Frame Sprite** field (Task 1) so each cell gets a frame.
  3. For each of `Listen`, `Confirm`, `Mic` buttons → its **Image → Color alpha to 0**, keep the icon child visible.
  4. Optionally tune **Tile/Slot/Stone Reveal/Smoke Size** + **Slot Gap** on the `WordBuildEncounterController` inspector to taste.
  5. Enter Play and eyeball: book over your background, two clearly separated slots, bigger tiles, invisible buttons-but-visible-icons.

---

### Task 3: Author-tunable cutscene character size

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/CutsceneData.cs`
- Modify: `Assets/Scripts/Adventure/View/CutscenePlayer.cs`

**Interfaces:**
- Produces: `CutsceneCharacter.size` (Vector2), `CutsceneFrame.npcSize` (Vector2); `CutscenePlayer.DefaultNpcSize` constant.
- Consumes: existing `CutsceneFrame`, `CutsceneCharacter`, `CutscenePlayer.ApplyNpc/ApplyExtraCharacters/CreateExtraNpc/BuildOverlay`.

- [ ] **Step 1: Add `size` to `CutsceneCharacter`.** In `CutsceneData.cs`, inside `CutsceneCharacter` (after `offset`):

```csharp
        public Vector2 size;             // (0,0) = use the default 520x620
```

- [ ] **Step 2: Add `npcSize` to `CutsceneFrame`.** Inside `CutsceneFrame` (after `npcAnim`):

```csharp
        public Vector2 npcSize;          // (0,0) = use the default 520x620
```

- [ ] **Step 3: Add the shared default + apply it.** In `CutscenePlayer.cs`, add a constant near the top of the class:

```csharp
        private static readonly Vector2 DefaultNpcSize = new Vector2(520f, 620f);
```

In `BuildOverlay`, replace `rt.sizeDelta = new Vector2(520, 620);` with `rt.sizeDelta = DefaultNpcSize;`. In `CreateExtraNpc`, replace `rt.sizeDelta = new Vector2(520, 620);` with `rt.sizeDelta = DefaultNpcSize;`.

- [ ] **Step 4: Apply `npcSize` per frame in `ApplyNpc`.** After `_npc.preserveAspect = true;` add:

```csharp
            ((RectTransform)_npc.transform).sizeDelta = frame.npcSize != Vector2.zero ? frame.npcSize : DefaultNpcSize;
```

- [ ] **Step 5: Apply `size` per extra in `ApplyExtraCharacters`.** After `img.preserveAspect = true;` add:

```csharp
                ((RectTransform)img.transform).sizeDelta = c.size != Vector2.zero ? c.size : DefaultNpcSize;
```

- [ ] **Step 6: Verify.** `is_compiling == false`; `read_console` errors → none; `run_tests(mode="EditMode")` → 24/24.

- [ ] **Step 7: Commit.**

```bash
git add Assets/Scripts/Adventure/Data/CutsceneData.cs Assets/Scripts/Adventure/View/CutscenePlayer.cs
git commit -m "feat(adventure): author-tunable cutscene character size (npc + extras)"
```

---

### Task 4: Per-cutscene dimming + faithful background layer

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/CutsceneData.cs`
- Modify: `Assets/Scripts/Adventure/View/CutscenePlayer.cs`

**Interfaces:**
- Produces: `CutsceneData.backdropDim/focusDimAlpha/focusScale/unfocusScale` (floats); `CutscenePlayer._data`, `_bg`, `_dim`.
- Consumes: `CutscenePlayer.BuildOverlay/PlayRoutine/ApplyFocus/ApplyFocusTo/Teardown`, removes player `dimAlpha/focusScale/unfocusScale`.

- [ ] **Step 1: Add dim fields to `CutsceneData`.** In `CutsceneData` (the SO class, after `frames`):

```csharp
        [Range(0f, 1f)] public float backdropDim = 0.35f;   // darkness of the layer over the background
        [Range(0f, 1f)] public float focusDimAlpha = 0.5f;  // non-focused character alpha when a focus is set
        public float focusScale = 1.15f;                    // focused character scale-up
        public float unfocusScale = 0.92f;                  // non-focused character scale-down
```

- [ ] **Step 2: Remove the now-redundant player fields.** In `CutscenePlayer.cs` delete these three serialized fields:

```csharp
        [SerializeField] private float focusScale = 1.15f;   // focused character scale-up
        [SerializeField] private float unfocusScale = 0.92f; // others scale-down when a focus is set
        [SerializeField] private float dimAlpha = 0.5f;      // others' alpha when a focus is set
```

- [ ] **Step 3: Track the playing data.** Add a field with the other private state:

```csharp
        private CutsceneData _data;
```

In `PlayRoutine`, immediately after the `if (data == null || ...) { Finish(); yield break; }` guard, add:

```csharp
            _data = data;
```

- [ ] **Step 4: Split the backdrop into BG + Dim layers.** In `BuildOverlay`, replace the backdrop block:

```csharp
            // Opaque dark backdrop doubles as the cover (so the encounter UI never bleeds
            // through) and the background-sprite holder.
            _backdrop = MakeStretch(go.transform, "Backdrop");
            _backdrop.color = new Color(0.06f, 0.07f, 0.10f, 1f);
            _backdrop.preserveAspect = false;
```

with:

```csharp
            // BG layer: shows the frame's background faithfully (white tint, opaque so the
            // encounter UI never bleeds through). Dim layer: a separate black overlay above the
            // background and below the characters, its alpha authored per cutscene (backdropDim).
            _bg = MakeStretch(go.transform, "Bg");
            _bg.color = Color.white;
            _bg.preserveAspect = false;
            _dim = MakeStretch(go.transform, "Dim");
            _dim.color = new Color(0f, 0f, 0f, _data != null ? _data.backdropDim : 0.35f);
```

Replace the `_backdrop` field declaration:

```csharp
        private Image _backdrop;
```

with:

```csharp
        private Image _bg;
        private Image _dim;
```

- [ ] **Step 5: Point the frame background at `_bg`.** In `PlayRoutine`, replace `AvHelpers.TrySetSprite(_backdrop, frame.background);` with:

```csharp
                AvHelpers.TrySetSprite(_bg, frame.background);
```

- [ ] **Step 6: Read focus values from `_data`.** Replace `ApplyFocusTo`:

```csharp
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
```

- [ ] **Step 7: Clear new state in `Teardown`.** Replace the reset line `_canvas = null; _group = null; _backdrop = null; _npc = null;` with:

```csharp
            _canvas = null; _group = null; _bg = null; _dim = null; _npc = null; _data = null;
```

- [ ] **Step 8: Verify.** `is_compiling == false`; `read_console` errors → none; `run_tests(mode="EditMode")` → 24/24.

- [ ] **Step 9: Commit.**

```bash
git add Assets/Scripts/Adventure/Data/CutsceneData.cs Assets/Scripts/Adventure/View/CutscenePlayer.cs
git commit -m "feat(adventure): per-cutscene dim + faithful background layer (split backdrop)"
```

---

### Task 5: Procedural "float-in" entrance

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/CutsceneData.cs`
- Modify: `Assets/Scripts/Adventure/View/CutscenePlayer.cs`
- Modify (via MCP): `Assets/Data/Adventure/Cutscenes/e_paa_intro.asset`

**Interfaces:**
- Produces: `EntranceMode` enum; `CutsceneCharacter.entrance/entranceSeconds`; `CutsceneFrame.npcEntrance/npcEntranceSeconds`; `CutscenePlayer.StartEntrance/StopEntrances`.
- Consumes: `CutscenePlayer.ApplyNpc/ApplyExtraCharacters/PlayRoutine/Teardown`.

> **Design note:** entrance animates **position only** (slide + a settling sine bob, modelled on the old `BearCutsceneEntrance.PlayDirectEntrance`). Scale/alpha stay owned by the focus system (Task 4), so the two never fight. `ScaleIn` from the spec is intentionally omitted for that reason; `Bob` is a looping idle.

- [ ] **Step 1: Add the enum + fields to `CutsceneData.cs`.** Add the enum next to `NpcSide`:

```csharp
    public enum EntranceMode { None, SlideInLeft, SlideInRight, SlideInUp, Bob }
```

In `CutsceneCharacter` (after `size`):

```csharp
        public EntranceMode entrance = EntranceMode.None;
        public float entranceSeconds = 0.8f;
```

In `CutsceneFrame` (after `npcSize`):

```csharp
        public EntranceMode npcEntrance = EntranceMode.None;
        public float npcEntranceSeconds = 0.8f;
```

- [ ] **Step 2: Add entrance bookkeeping to `CutscenePlayer`.** Add a field:

```csharp
        private readonly System.Collections.Generic.List<Coroutine> _entranceCos =
            new System.Collections.Generic.List<Coroutine>();
```

Add these helpers to the class:

```csharp
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
```

- [ ] **Step 3: Stop prior entrances at each frame.** In `PlayRoutine`, inside the `for` loop right after `if (frame == null) continue;` add:

```csharp
                StopEntrances();
```

- [ ] **Step 4: Trigger the npc entrance.** In `ApplyNpc`, the resting position is set by the existing line `((RectTransform)_npc.transform).anchoredPosition = new Vector2(x, -30f);`. Immediately after it add:

```csharp
            StartEntrance((RectTransform)_npc.transform, new Vector2(x, -30f), frame.npcEntrance, frame.npcEntranceSeconds);
```

- [ ] **Step 5: Trigger extra-character entrances.** In `ApplyExtraCharacters`, the resting position is `new Vector2(0f, -30f) + c.offset`. Replace the positioning line:

```csharp
                ((RectTransform)img.transform).anchoredPosition = new Vector2(0f, -30f) + c.offset;
```

with:

```csharp
                Vector2 rest = new Vector2(0f, -30f) + c.offset;
                ((RectTransform)img.transform).anchoredPosition = rest;
                StartEntrance((RectTransform)img.transform, rest, c.entrance, c.entranceSeconds);
```

- [ ] **Step 6: Stop entrances on teardown.** At the top of `Teardown`, after `if (audioSource != null) audioSource.Stop();` add:

```csharp
            StopEntrances();
```

- [ ] **Step 7: Verify.** `is_compiling == false`; `read_console` errors → none; `run_tests(mode="EditMode")` → 24/24.

- [ ] **Step 8: Author the PAA bear entrance.** Use `manage_scriptable_object` to edit `Assets/Data/Adventure/Cutscenes/e_paa_intro.asset`: on frame 0's `extraCharacters[0]` (the bear, offset x=-380), set `entrance = SlideInRight` (enum value `2`) and `entranceSeconds = 1.0`. Leave the owl `npcEntrance` at `None` (or `Bob` to taste). Confirm the asset round-trips (re-read it; `entrance: 2` present).

- [ ] **Step 9: Commit.**

```bash
git add Assets/Scripts/Adventure/Data/CutsceneData.cs Assets/Scripts/Adventure/View/CutscenePlayer.cs Assets/Data/Adventure/Cutscenes/e_paa_intro.asset
git commit -m "feat(adventure): procedural float-in entrance (bear slides into PAA intro)"
```

---

### Task 6: Rewrite the TTS voice lines

**Files:**
- Modify: `D:/Gimme/wordflow-backend/gateway/tts_lines.json`
- Run: `D:/Gimme/wordflow-backend/gateway/scripts/prewarm_tts.py`

**Interfaces:**
- Consumes/Produces: same `line_id`s, `voice`s, and `en` glosses (cache key + Unity refs stay stable); only `text` and `style` change.

- [ ] **Step 1: Present the proposed Thai rewrites to the user and get approval/corrections** (content authority is the native speaker). Proposed `text` (style tweaks noted inline) — IDs unchanged:

```
e1_intro_owl_1   : "ดูสิจ๊ะ ลุงคนนั้นไม่สบาย เราไปช่วยกันเถอะ"
e1_intro_owl_2   : "ลองเสกคำว่า 'ยา' ดูสิจ๊ะ แล้วลุงจะหายดี"
e1_outro_villager_1: "ขอบใจมากนะหนู หนูช่วยชีวิตลุงไว้เลย"
e1_outro_villager_2: "ข้างหน้า...ยังมีความมืดรออยู่ ระวังตัวด้วยนะหนู"
e1_outro_owl_1   : "ไปกันต่อเถอะจ๊ะ ยังมีคนรอเราช่วยอีกเยอะเลย"
kaa_intro_owl_1  : "ดูสิจ๊ะ เจ้ากาตัวนั้นเงียบไปเลย มันร้องไม่ออกเสียงเลยนะ"
kaa_intro_owl_2  : "ลองเสกคำว่า 'กา' ดูสิจ๊ะ แล้วเสียงของมันจะกลับมา"
kaa_outro_owl_1  : "เก่งมากเลย! ฟังสิจ๊ะ มันร้อง 'กา กา' ได้แล้ว"
kaa_outro_owl_2  : "มันบินขึ้นฟ้าไปแล้ว ไปกันต่อเถอะหนู"   (unchanged — already natural)
paa_intro_owl_1  : "ระวังนะ! เจ้าหมีตัวใหญ่บุกเข้ามาแล้ว เราต้องช่วยกันไล่มันไป"
paa_intro_owl_2  : "เสกคำว่า 'ปา' แล้วปาแสงใส่มันเลยจ๊ะ!"
paa_outro_bear_1 : "กรรร์...! เจ้าตัวเล็ก แต่ใจกล้าดีนะ"   (unchanged — already good)
paa_outro_owl_1  : "มันหนีเข้าป่าไปแล้ว เก่งมากเลยหนู สักวันมันอาจกลับมาช่วยเราก็ได้นะ"
e1_intro_villager_1: "ช่วย... ช่วยด้วย..."   (unchanged — fits the weak/sick delivery)
```

Apply the user's corrections to this list before Step 2.

- [ ] **Step 2: Edit `tts_lines.json`.** For each approved line, update its `text` (and `style` if the user adjusted it). Leave `voice`/`en`/`line_id` untouched. Keep valid JSON.

- [ ] **Step 3: Re-prewarm the cache.** Text changes mean new cache entries. From `D:/Gimme/wordflow-backend/gateway/`:

```bash
../.venv/Scripts/python.exe scripts/prewarm_tts.py
```

Expected: each changed line synthesizes and caches (uncached lines hit the Google free-tier daily quota — if quota-blocked, note which lines deferred).

- [ ] **Step 4: Restart the gateway for the demo** (fake auth, port 8001). From `D:/Gimme/wordflow-backend/gateway/`:

```bash
AUTH_IMPL=fake ../.venv/Scripts/python.exe -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

- [ ] **Step 5: Verify by listening.** In Unity Play mode (gateway up), play the PAA intro/outro; lines sound natural. (Backend repo — commit there per its own conventions.)

```bash
cd D:/Gimme/wordflow-backend && git add gateway/tts_lines.json && git commit -m "content: more natural child-directed Thai for e1/kaa/paa voice lines"
```

---

## Self-Review

**Spec coverage:**
- WS1 (page layout: sizes, background, transparency) → Task 1 (sizes) + Task 2 (background GO + transparency tutorial). ✓
- WS2 (cutscene character size) → Task 3. ✓
- WS3 (per-cutscene dim + backdrop split) → Task 4. ✓
- WS4 (float-in entrance; PAA bear) → Task 5. ✓
- WS5 (TTS rewrite, all three sets) → Task 6. ✓
- Out-of-scope items (old flow, prototype scene, /grade, font) → untouched; no tasks. ✓

**Placeholder scan:** No TBD/TODO; all code blocks concrete; proposed Thai is real content gated on user approval (not a placeholder). ✓

**Type consistency:** Field names match across tasks — `slotFrameSprite`/`slotColor` (T1), `DefaultNpcSize`/`size`/`npcSize` (T3), `_data`/`_bg`/`_dim`/`backdropDim`/`focusDimAlpha`/`focusScale`/`unfocusScale` (T4), `EntranceMode`/`entrance`/`entranceSeconds`/`npcEntrance`/`npcEntranceSeconds`/`StartEntrance`/`StopEntrances`/`SmoothStep01` (T5). Task 4 removes `CutscenePlayer.focusScale/unfocusScale/dimAlpha` and Task 5 does not reference them (uses `_data`). `_backdrop` fully replaced by `_bg`/`_dim` in T4 before T5 touches the file. ✓

**Note on task order:** Tasks 3→4→5 edit the same two files in sequence; execute in order (4 depends on 3's `size` field placement context; 5 depends on 4's `_data`). Tasks 1, 2, 6 are independent.
