# Authorable, Book-Themed Encounter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the word-build encounter visually authorable in the Unity Editor — a book-themed build page laid out by dragging (not magic numbers), and an intro cutscene that shows owl + bear together with per-frame focus (zoom + dim) — without splitting the loop into separate scenes.

**Architecture:** Two independent code changes plus one scene-scaffold step. (1) The cutscene data/player gains optional extra-character slots + a per-frame focus index. (2) `WordBuildEncounterController` stops *creating* its UI and instead *references* an authored `EncounterCanvas` hierarchy, distributing tiles/slots across authored container rects. (3) That hierarchy is scaffolded into the prototype scene via Unity MCP with `book_craft.png` assigned. The Core phase machine and `/grade` path are untouched.

**Tech Stack:** Unity 6 (6000.4.3f1), URP 2D, uGUI + TextMeshPro, New Input System, `WordFlow.Adventure` asmdef. Driven through MCP for Unity; EditMode NUnit tests for Core only.

## Global Constraints

- **LD no-text rule:** every child-facing affordance is an icon/visual, never text; the only on-screen glyphs are the Thai grapheme tiles. Scoring stays invisible. (Dev-only `showDebugControls` text may remain, hidden in the child build.)
- **Do not modify the old quest flow** (`MagicStonePuzzleController`, `cut_scene1`, `WorldMap`, etc.).
- **Backward compatibility:** existing authored `CutsceneData` assets must keep working unchanged (new fields default to empty/`-1`).
- **Null-safe house style:** missing sprite/ref/clip → skip and degrade, never throw.
- **No new scenes;** all work stays in `Assets/Scenes/region 1/adventure/word_build_prototype.unity` (Build Settings index 10).
- **No bespoke beam tracker;** the "following" beam is a user-authored legacy `AnimationClip`.
- **Verification reality:** there is no headless harness for MonoBehaviour view code. Verify view/scene work by (a) `mcpforunity://editor/state` `is_compiling == false`, (b) `read_console(types=["error"])` clean, and (c) `manage_camera(action="screenshot")`. The Core EditMode suite is the regression guard and must stay green.
- After editing any `.cs`, wait for compile to finish and check `read_console` for errors **before** using the new type.

---

## Task 1: Extend cutscene data model (extra characters + focus)

Adds the optional data fields. No behavior yet — Task 2 consumes them.

**Files:**
- Modify: `Assets/Scripts/Adventure/Data/CutsceneData.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `class CutsceneCharacter { public Sprite sprite; public Vector2 offset; public AnimationClip anim; }` (`[Serializable]`)
  - `CutsceneFrame.extraCharacters` : `List<CutsceneCharacter>` (default `new List<CutsceneCharacter>()`)
  - `CutsceneFrame.focusIndex` : `int` (default `-1`; `-1` = no focus, `0` = primary `npc`, `1..N` = `extraCharacters[focusIndex-1]`)

- [ ] **Step 1: Add the `CutsceneCharacter` type and the two `CutsceneFrame` fields**

In `Assets/Scripts/Adventure/Data/CutsceneData.cs`, add the new serializable class after `CutsceneFrame` and add the two fields inside `CutsceneFrame` (immediately after the existing `npcAnim` line):

```csharp
        public AnimationClip npcAnim;    // nullable; best-effort legacy clip

        // Extra characters share the frame with `npc` (e.g. owl + bear + beam). Empty by
        // default, so every existing cutscene asset is unchanged. Each is freely positioned
        // by `offset` and may carry its own best-effort legacy clip.
        public List<CutsceneCharacter> extraCharacters = new List<CutsceneCharacter>();

        // Which character is emphasised this frame (zoom in + dim the rest):
        //  -1 = none (all full), 0 = the primary `npc`, 1..N = extraCharacters[focusIndex-1].
        public int focusIndex = -1;
```

```csharp
    /// <summary>An additional on-screen character for a frame (owl/bear/beam), positioned by
    /// an explicit offset so it can be placed precisely. Nullable fields degrade silently.</summary>
    [Serializable]
    public sealed class CutsceneCharacter
    {
        public Sprite sprite;            // nullable
        public Vector2 offset;           // anchored offset from screen-bottom-center
        public AnimationClip anim;       // nullable; best-effort legacy clip
    }
```

- [ ] **Step 2: Verify it compiles cleanly**

Wait for `mcpforunity://editor/state` `is_compiling == false`, then run `read_console(types=["error"])`.
Expected: no errors.

- [ ] **Step 3: Verify Core tests still pass (regression guard)**

Run `run_tests` with `mode="EditMode"` (whole suite).
Expected: PASS — `EncounterModelTests`, `OutcomeEvaluatorTests`, `WavEncoderTests`, `WordDatabaseTests` all green (data-only change, nothing should break).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Adventure/Data/CutsceneData.cs
git commit -m "feat(adventure): cutscene frame extra-character + focus data fields"
```

---

## Task 2: Render extra characters + apply focus in CutscenePlayer

Makes owl + bear share a frame and emphasises the active speaker by zoom + dim.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/CutscenePlayer.cs`

**Interfaces:**
- Consumes (from Task 1): `CutsceneFrame.extraCharacters`, `CutsceneFrame.focusIndex`, `CutsceneCharacter.{sprite,offset,anim}`.
- Produces: behavior only (no new public API). New serialized tunables `focusScale`, `dimAlpha`, `focusLerpSeconds`.

- [ ] **Step 1: Add focus tunables and an extra-character Image list**

In `CutscenePlayer.cs`, add serialized tunables next to the existing `[SerializeField]` fields (after `ttsTimeout`):

```csharp
        [SerializeField] private float focusScale = 1.15f;   // focused character scale-up
        [SerializeField] private float unfocusScale = 0.92f; // others scale-down when a focus is set
        [SerializeField] private float dimAlpha = 0.5f;      // others' alpha when a focus is set
```

Add a private list next to `private Image _npc;`:

```csharp
        private readonly System.Collections.Generic.List<Image> _extraNpcs =
            new System.Collections.Generic.List<Image>();
```

- [ ] **Step 2: Build one Image per extra character, then apply focus, inside the frame loop**

In `PlayRoutine`, the per-frame block currently calls `ApplyNpc(frame)`. Replace the single
`ApplyNpc(frame);` call with the primary apply plus extras + focus:

```csharp
                AvHelpers.TrySetSprite(_backdrop, frame.background);
                ApplyNpc(frame);
                ApplyExtraCharacters(frame);
                ApplyFocus(frame);
```

- [ ] **Step 3: Implement `ApplyExtraCharacters` and `ApplyFocus`**

Add these methods to `CutscenePlayer` (next to `ApplyNpc`):

```csharp
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
                ((RectTransform)img.transform).anchoredPosition = new Vector2(0f, -30f) + c.offset;
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
            rt.sizeDelta = new Vector2(520, 620);
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
            float scale = noFocus ? 1f : (focused ? focusScale : unfocusScale);
            float alpha = noFocus ? 1f : (focused ? 1f : dimAlpha);
            img.transform.localScale = new Vector3(scale, scale, 1f);
            var col = img.color; col.a = alpha; img.color = col;
        }
```

- [ ] **Step 4: Generalise the existing legacy-anim helper to any Image**

`PlayNpcAnim` currently hard-codes `_npc`. Rename/retarget it to take an `Image` so extras can
animate too. Replace the existing `PlayNpcAnim` method body and its single caller:

```csharp
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
```

In `ApplyNpc`, change the final `PlayNpcAnim(frame.npcAnim);` line to `PlayClipOn(_npc, frame.npcAnim);`.

- [ ] **Step 5: Tear down extra Images with the overlay**

In `Teardown()`, clear the extras list when the canvas is destroyed (the GameObjects are
children of `_canvas` and die with it; just drop the stale references):

```csharp
        private void Teardown()
        {
            if (audioSource != null) audioSource.Stop();
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null; _group = null; _backdrop = null; _npc = null;
            _extraNpcs.Clear();
        }
```

- [ ] **Step 6: Verify it compiles cleanly**

Wait for `is_compiling == false`, then `read_console(types=["error"])`.
Expected: no errors. (`PlayNpcAnim` is fully replaced by `PlayClipOn` — confirm no dangling reference remains.)

- [ ] **Step 7: Visual smoke test (manual, in Editor)**

Temporarily add a second `CutsceneCharacter` (any sprite + an `offset` like `(300, 0)`) and set
`focusIndex` on one frame of an existing intro `CutsceneData` asset, enter Play mode on
`word_build_prototype.unity`, and `manage_camera(action="screenshot", include_image=true)`.
Expected: two sprites visible side-by-side; the focused one larger, the other dimmer. Revert the
temporary edit after confirming.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Adventure/View/CutscenePlayer.cs
git commit -m "feat(adventure): multi-character cutscene frames + per-frame focus (zoom+dim)"
```

---

## Task 3: Refactor controller to reference an authored hierarchy

Stops creating UI in code; references serialized objects and distributes tiles/slots across
authored container rects. Phase machine logic untouched. After this task the in-scene controller
will have null refs until Task 4 scaffolds them — that is expected and degrades gracefully.

**Files:**
- Modify: `Assets/Scripts/Adventure/View/WordBuildEncounterController.cs`

**Interfaces:**
- Consumes: nothing new (uses existing `EncounterModel`, `config`, `database`, `AvHelpers`).
- Produces: new `[SerializeField]` fields the scene wires in Task 4 — `RectTransform trayContainer`, `RectTransform slotContainer`, `RectTransform stoneRevealAnchor`, `Button listenButton`, `Button confirmButton`, `Button micButton`, `Image micIcon`.

- [ ] **Step 1: Replace the UI-construction serialized fields with reference fields**

In `WordBuildEncounterController.cs`, add the authored references near the other `[SerializeField]`
fields (keep `config`, `database`, `gradeClient`, `audioSource`, `cutscenePlayer`, the icon
sprites, `showDebugControls`, and the tunables `slideSeconds`/`micSeconds`/`stoneRevealSeconds`/`nonWordSfx`):

```csharp
        [SerializeField] private RectTransform trayContainer;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private RectTransform stoneRevealAnchor;
        [SerializeField] private Button listenButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button micButton;
        [SerializeField] private Image micIcon;
```

Remove the now-unused private fields that only existed for the runtime canvas: `_boardRoot`,
`_hud`, `_listenButton`, `_confirmButton`, `_micButton`, `_micIcon` (the serialized ones above
replace the last four), and the layout constants `TileSize`, `TileGap`, `TrayY`, `SlotY`. Keep
`_tileHome` (repurposed below), `_trayTiles`, `_slots`, `_animating`, `_smoke`.

- [ ] **Step 2: Replace `BuildUi()` with reference wiring**

`BuildUi()` no longer builds anything. Replace its whole body with validation + listener wiring:

```csharp
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
```

- [ ] **Step 3: Update every button show/hide call to the new refs + null-safe helper**

Replace the three `ShowBuildingButtons`/`EnterConfirmPhase`/`ConfirmRoutine`/`MicAndResolve`
button toggles to use `SetActive(...)` and the serialized fields. Concretely:

```csharp
        private void ShowBuildingButtons()
        {
            SetActive(confirmButton, false);
            SetActive(micButton, false);
            SetActive(listenButton, config.mode == EncounterMode.Supported);
        }
```

In `EnterConfirmPhase`: `SetActive(confirmButton, true); SetActive(micButton, false); SetActive(listenButton, false);`
In `ConfirmRoutine` (Echo→Mic): `SetActive(confirmButton, false);` then later `SetActive(micButton, true); SetActive(listenButton, true);`
In `OnConfirm`/`OnMic` guards, replace `_confirmButton`/`_micButton` references with `confirmButton`/`micButton`.
In `MicAndResolve` after recording: `SetActive(micButton, false); SetActive(listenButton, false);`
In the mic-pulse helpers, replace `_micIcon` with the serialized `micIcon`.

- [ ] **Step 4: Add `DistributeX` and rewrite `BuildBoard()` to use the containers**

Replace the static `RowX` helper with a container-relative distributor, and parent tiles/slots
to their containers:

```csharp
        // Evenly distribute item i of n across `container`'s width, centred. Positions derive
        // from where the (authored) container sits/sizes in the Editor — no magic numbers.
        private static float DistributeX(int i, int n, RectTransform container)
        {
            if (n <= 1) return 0f;
            float w = container.rect.width;
            float step = w / n;
            return -w / 2f + step * (i + 0.5f);
        }
```

Rewrite `BuildBoard()`:

```csharp
        private void BuildBoard()
        {
            foreach (var t in _trayTiles) if (t != null) { t.Tapped -= OnTileTapped; Destroy(t.gameObject); }
            foreach (var s in _slots) if (s != null) { s.Tapped -= OnSlotTapped; Destroy(s.gameObject); }
            _trayTiles.Clear(); _slots.Clear(); _tileHome.Clear();

            int n = _model.SlotCount;
            for (int s = 0; s < n; s++)
            {
                var slot = MakeSlot(slotContainer, s);
                ((RectTransform)slot.transform).anchoredPosition = new Vector2(DistributeX(s, n, slotContainer), 0f);
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
                _trayTiles.Add(tile);
            }
        }
```

Note: `_tileHome` is now the distributed tray position; `ResetTilesToTray` and `SlideTile`
keep working unchanged because they already read `_tileHome` / animate `anchoredPosition`.
**Slot target positions** in `OnTileTapped`/`PlaceTileRoutine` already read
`((RectTransform)_slots[slot].transform).anchoredPosition` — these now live in `slotContainer`.
Because tiles live in `trayContainer` and slots in `slotContainer`, the slide must use a
**common space**: convert the slot's position into the tray container's local space when sliding.
Update the place call in `OnTileTapped`:

```csharp
            Vector2 slotPos = SlotPosInTraySpace(slot);
            StartCoroutine(PlaceTileRoutine(trayIndex, slotPos));
```

And add the converter:

```csharp
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
```

- [ ] **Step 5: Point reveal/smoke at `stoneRevealAnchor`; update `MakeSlot`/`MakeTile` signatures**

In `RevealStone` and `SmokePuff`, change the parent from `_boardRoot` to `stoneRevealAnchor`
(fall back to `trayContainer` if the anchor is null) and drop the hardcoded
`anchoredPosition = new Vector2(0, 40)` — center on the anchor (`Vector2.zero`):

```csharp
            var parent = stoneRevealAnchor != null ? (Transform)stoneRevealAnchor : trayContainer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
```

Update `MakeSlot(Transform parent, int index)` (drop the unused `label` param) and confirm
`MakeTile(Transform parent, …)` parents to the passed container. Keep `AddCenterLabel`,
`MakeTile`'s icon/grapheme fallback, and `StoneTile`/`TileSlot` `Configure` calls as-is.

- [ ] **Step 6: Delete the now-orphaned factory helpers**

Remove `MakeIconButton`, `MakeButton`, `MakeStretch`, `MakeLabel`, and `MakeLabel`'s `_hud`
usages, plus the `showDebugControls` HUD/toggle button creation in the old `BuildUi` (the
`ToggleMode`/`ToggleEcho` *methods* may stay for now but their on-screen buttons are gone with
the runtime canvas; leave the methods, remove only the orphaned `MakeButton` calls). Remove only
helpers this change orphaned — do not touch unrelated code. `UpdateHud` becomes a no-op guarded
by `_hud == null`; leave it (calls are harmless) or delete its calls if trivial.

- [ ] **Step 7: Verify it compiles cleanly**

Wait for `is_compiling == false`, then `read_console(types=["error"])`.
Expected: no errors, no warnings about unresolved `_boardRoot`/`MakeIconButton`/`RowX`.

- [ ] **Step 8: Verify Core tests still pass**

Run `run_tests` `mode="EditMode"`.
Expected: PASS (controller is View-layer; Core unaffected).

- [ ] **Step 9: Commit**

```bash
git add Assets/Scripts/Adventure/View/WordBuildEncounterController.cs
git commit -m "refactor(adventure): controller references authored UI + container-distributed tiles"
```

---

## Task 4: Scaffold the book-themed EncounterCanvas in the scene (MCP)

Builds the authored hierarchy in the prototype scene, assigns `book_craft.png`, and wires the
serialized references so the build page renders as a book and the loop runs end-to-end.

**Files:**
- Modify (via MCP, in-Editor): `Assets/Scenes/region 1/adventure/word_build_prototype.unity`
- Asset used: `Assets/Art/visaul_novel/quest/book_craft.png`

**Interfaces:**
- Consumes (from Task 3): controller serialized fields `trayContainer`, `slotContainer`, `stoneRevealAnchor`, `listenButton`, `confirmButton`, `micButton`, `micIcon`.
- Produces: a runnable scene (no code).

- [ ] **Step 1: Open the scene and confirm state**

`manage_scene(action="load", name="word_build_prototype")` (or the full path). Confirm the
existing controller GameObject is present and note its name. `read_console(types=["error"])` clean.

- [ ] **Step 2: Create the EncounterCanvas hierarchy**

Using `manage_gameobject`, create under a new `EncounterCanvas` (Canvas, ScreenSpaceOverlay,
CanvasScaler ScaleWithScreenSize, reference 1280x720, + GraphicRaycaster):
- `BookBackground` — `Image`, stretched full-rect, sprite = `book_craft.png`, `raycastTarget=false`.
- `SlotContainer` — empty `RectTransform`, sized/placed over the book's page (rough: center, width ~700, height ~160, y ~+40).
- `TrayContainer` — empty `RectTransform`, lower page (rough: center, width ~700, height ~160, y ~-120).
- `StoneRevealAnchor` — empty `RectTransform`, book center (rough: 0, +40).
- `ListenButton`, `ConfirmButton`, `MicButton` — `Button`+`Image`, 140x140, at rough anchors (-160,90)/(0,90)/(160,90); assign the existing icon sprites if present, else leave the Image color so they're visible.

Confirm the scene's `EventSystem` uses `InputSystemUIInputModule` (per project convention); if a
legacy module is present, replace it.

- [ ] **Step 3: Wire the controller's serialized references**

Using `manage_gameobject`/`manage_components`, set the controller's fields: `trayContainer`,
`slotContainer`, `stoneRevealAnchor`, `listenButton`, `confirmButton`, `micButton`, and `micIcon`
(the Image on the MicButton). Leave `config`, `database`, `gradeClient`, `audioSource`,
`cutscenePlayer` as already wired.

- [ ] **Step 4: Save and screenshot (editor, not Play)**

`manage_scene(action="save")`, then `manage_camera(action="screenshot", include_image=true)`.
Expected: the book background renders; three buttons and the (empty) containers are visible and
on the book, not floating off-page.

- [ ] **Step 5: Play-mode end-to-end check**

Enter Play mode. Expected behavior:
- Build page shows the book; tiles appear in `TrayContainer`, slots in `SlotContainer`, both
  distributed across their authored rects.
- Tapping a tile slides it onto the correct slot (the tray→slot space conversion lands it
  exactly on the slot). Completing the word shows Confirm; Confirm → Echo → Mic appear in turn.
- On a correct build, the magic stone pops centered on `StoneRevealAnchor` (from the book).
- `read_console(types=["error"])` clean during the run.

Screenshot mid-build to confirm. Exit Play mode.

- [ ] **Step 6: Commit the scene**

```bash
git add "Assets/Scenes/region 1/adventure/word_build_prototype.unity"
git commit -m "feat(adventure): scaffold book-themed EncounterCanvas + wire controller refs"
```

---

## Manual follow-up (user, not in this plan)

- Author owl/bear/beam sprites and the bear-join, bear-flee, and beam-follow legacy
  `AnimationClip`s; add the bear + beam as `extraCharacters` on the intro `CutsceneData`, and set
  each frame's `focusIndex` (bear as it rushes in, owl as it speaks).
- Fine-tune the book-page layout (container rects, button positions) by dragging in the Editor.
- Replace placeholder button colors with final mic/speaker/check icon sprites.

## Self-Review Notes

- **Spec coverage:** Component 1 (book page + authored refs + container distribution + reveal
  anchor) → Tasks 3–4. Component 2 (extra characters + focus) → Tasks 1–2. Division-of-labor
  "user-authored art/clips" → Manual follow-up. Non-goals (no scene split, no beam tracker,
  phase machine untouched) respected.
- **Layout deviation from spec:** spec's `HorizontalLayoutGroup` is implemented as
  container-rect distribution instead (animation-safe); spec already updated to match.
- **Type consistency:** `DistributeX`, `SlotPosInTraySpace`, `SetActive`, `PlayClipOn`,
  `ApplyExtraCharacters`, `ApplyFocus`, `CreateExtraNpc`, `CutsceneCharacter.{sprite,offset,anim}`,
  `CutsceneFrame.{extraCharacters,focusIndex}` are referenced consistently across tasks.
