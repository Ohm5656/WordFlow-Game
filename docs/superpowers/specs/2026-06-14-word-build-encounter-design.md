# Word-Build Encounter Prototype — Design Spec

Date: 2026-06-14
Status: design approved, pending spec review

## Context

The current Unity project (`NSC-Game-2026-06-03-17-40-54`) still runs the old quest
flow: `WorldMap → quest_map1 → cut_scene1 → MagicStonePuzzleController → practice`. The
"Adventure Redesign" (see the Obsidian vault: `wordflow-game-design.md`,
`wordflow-region-1.md`) replaces the quest-page loop with an auto-run adventure built
around **word-building encounters**. No art assets exist yet for the new system.

This spec covers a **standalone prototype** of one encounter: the child builds a Thai
word from phoneme/grapheme tiles, the word is echoed back, they say it aloud, and
`/grade` is called — all with placeholder visuals, fully testable without art.

**Out of scope** (explicitly not built here): the old quest flow (untouched), the
auto-run/world-map/camera systems, the boss fight, night practice mode, and any new art.

## Locked design decisions (2026-06-14 revision)

These corrections were made during brainstorming and are now reflected in the vault
(`wordflow-game-design.md` §"Content engine" and §"Encounter outcome"; `wordflow-region-1.md`
§"Content engine" and the Day 1–5 schedule tables):

1. **2-mode scaffold, not 3-tier.** The original Tier 1/2/3 system collapses to:
   - **Supported** (first meet + early repeats): "hear it" button active, owl says the
     full word on first reveal, each tile speaks its phoneme when placed.
   - **Recall** (later repeats — the real challenge): all of the above pre-build hints
     are off. Build + say from memory. *Clearing Recall is what locks a word in.*
   - Old Tier 1 and Tier 2 both map to **Supported**; old Tier 3 maps to **Recall**. This
     concentrates "real challenge" in Recall, per the design goal that pre-placing or
     over-scaffolding the puzzle defeats the point of a free word-building game.
2. **Tiles always start in the tray, never pre-placed in slots, in all modes.**
3. **Tiles are never shuffled, in any of Supported/Recall/Echo.** Shuffling and
   distractor tiles are reserved as a **boss-only** difficulty lever (not built in this
   prototype, but the data model accounts for it — see "Forward-compat" below).
4. **Echo** (sound-only prompt, no picture) is orthogonal to Supported/Recall and can
   pair with either, giving 4 combos: Supported, Recall, Supported+Echo, Recall+Echo.
5. **Post-build echo, always on, every mode.** Once the child finishes building *any*
   word (correct, wrong, or non-word), the owl voices the blended pronunciation of *what
   they built*, once, before the mic opens. This is what keeps Recall+Echo from being a
   blind guess — every attempt gets one clean pronunciation model immediately before
   recitation.
6. **`/grade` fires on every attempt**, not just correct builds. Gameplay branching is
   decided purely by the local tile-build check (Correct / NonWord / WrongWord),
   independent of and not blocked by the `/grade` response:
   - **Correct** → `/grade` targets the encounter's word (`targetWordId = config.target.id`).
   - **NonWord** → `/grade` still targets the encounter's intended word (so PAR reflects
     how far off the attempt was), tagged locally `outcome: non_word`.
   - **WrongWord** (built a different real word) → `/grade` targets *the word the child
     actually built* (grading their pronunciation of what they attempted), tagged
     locally `outcome: wrong_word`.
   - The `outcome` tag is logged client-side (`Debug.Log` + included as an extra
     multipart form field on the `/grade` POST for forward-compat); the backend
     `GradeResponse` model does not currently consume it — no backend change is in scope
     here.

## Data model (ScriptableObjects)

Mirrors the backend `Word`/`Stone` models (`gateway/app/models.py`) so these assets can
later be populated from `GET /api/v1/words` / `GET /api/v1/stones`.

- **`StoneTileData`** — `id` (string, e.g. `"stone_yo"`), `grapheme` (e.g. `"ย"`),
  nullable `AudioClip phonemeAudio`, nullable `Sprite icon`.
- **`WordEncounterData`** — `id` (backend wordId, e.g. `"yaa"`), `thai` (e.g. `"ยา"`),
  ordered `List<StoneTileData> tiles`, `ipa`, `meaning`, nullable `AudioClip wordAudio`.
- **`WordDatabase`** — flat `List<WordEncounterData>` containing all 7 Region-1 words
  (กา ปา ตา ยา ขา ตี ปี per `wordflow-region-1.md`'s locked content). Used for the local
  outcome lookup (does `builtString` match any known word?).
- **`EncounterConfig`** — `mode` (enum `Supported | Recall`), `echo` (bool), `target`
  (`WordEncounterData`), `questId`, `sceneId`, `childId` (defaults to `"kid_demo_01"`).

All `AudioClip`/`Sprite` fields are nullable. A shared `TryPlay(AudioClip?)` /
`TrySetSprite(Sprite?)` helper no-ops on null, so the prototype is fully testable with
zero audio/art and assets can be dropped in later with zero code changes.

### Forward-compat for boss-only difficulty (not built now)
`EncounterConfig` is the single place tray population reads from. Adding
`shuffleTray: bool` and `distractorTiles: List<StoneTileData>` fields later (for boss
encounters) requires no restructuring of the controller — the tray-build step just reads
two more fields.

## Controller flow (`WordBuildEncounterController`)

1. **Init(`EncounterConfig`)** — instantiate tray tiles for `config.target.tiles` in
   their fixed order (left→right); instantiate N empty slots (N = tile count, 2 for all
   Region-1 words). Tiles are never pre-placed into slots.
2. **Reveal** (Supported mode only) — play `config.target.wordAudio` once via the owl.
   If `echo`, this is the *only* prompt (no picture); otherwise it accompanies the
   context picture.
3. **Tap-to-place** — tapping a tray tile moves it to the next empty slot; in Supported
   mode this also plays that tile's `phonemeAudio`. Tapping an occupied slot returns its
   tile to its original tray position. The tray's left-to-right order never changes
   regardless of mode.
4. **Build complete** when every slot is filled. Concatenate the slots' graphemes
   left-to-right into `builtString`.
5. **Post-build echo** (always, every mode/combo) — play the blended pronunciation of
   `builtString`: if it matches a `WordDatabase` entry, play that entry's `wordAudio`;
   otherwise concatenate each placed tile's `phonemeAudio` back-to-back as a best-effort
   blend. Then open the mic.
6. **Record + grade** (always fires) — record via `Microphone.Start` + the `EncodeWav`
   16kHz mono WAV pattern from `MagicStonePuzzleController`, then `POST /grade` (multipart:
   `audio`, `targetWordId`, `childId`, `questId`, `sessionId`, `sceneId`,
   `Authorization: Bearer demo-token`). The response is logged only
   (`Debug.Log(GradeResponse)`) — it never blocks or delays step 7/8.
7. **Local outcome** (decided at step 4, independent of step 6):
   - `builtString == config.target.thai` → **Correct**
   - `builtString` matches a different `WordDatabase` entry → **WrongWord**
   - otherwise → **NonWord**
8. **Branch**:
   - **Correct** → success FX placeholder (green full-screen flash + SFX), stub
     `+1 level` log, encounter ends.
   - **WrongWord** → that matched word's FX placeholder plays (blue flash), owl
     "try again" (Debug log), slots reset to tray, encounter continues.
   - **NonWord** → fail FX placeholder (grey/smoke flash), owl "try again", slots reset
     to tray, encounter continues.

## File / scene plan

New scene: `Assets/Scenes/region 1/adventure/word_build_prototype.unity` — standalone,
does not modify `quest_map1.unity`, `cut_scene1.unity`, or `practice.unity`.

New scripts under `Assets/Scripts/Adventure/`:
- `StoneTileData.cs`, `WordEncounterData.cs`, `WordDatabase.cs`, `EncounterConfig.cs`
  (ScriptableObjects)
- `WordBuildEncounterController.cs` (flow above)
- `StoneTile.cs` (tray/slot tile component, tap handler)
- `TileSlot.cs` (slot component)
- `GradeApiClient.cs` — mic recording + WAV encoding + `/grade` POST, extracted and
  generalized from the inline logic in `MagicStonePuzzleController.cs` so it's reusable
  across encounters.

## Placeholder visuals (no art required)

- **Tiles**: UGUI `Image` (solid color square, ~120x120) + TMP text showing the Thai
  grapheme.
- **Slots**: UGUI `Image`, outlined/empty square, laid out in a horizontal row.
- **"Hear it" button**: standard UGUI `Button` + TMP label "🔊 Hear it" — active only in
  Supported mode.
- **Outcome FX**: full-screen solid-color flash — green (Correct), grey (NonWord), blue
  (WrongWord) — plus a TMP debug label showing the outcome and, once `/grade` responds,
  the PAR/grade.
- **Mode debug toggle**: runtime buttons/dropdown to switch Supported/Recall and
  Echo on/off, so all 4 combos are testable without rebuilding the scene.

## Prototype data

`WordEncounterData` assets for all 7 Region-1 words (กา ปา ตา ยา ขา ตี ปี), built from 7
`StoneTileData` assets (ก ป ต ย ข า ี), per the locked content in `wordflow-region-1.md`.
All audio/sprite fields left null.

## Test criteria

- Scene boots: tray shows the target word's tiles in fixed order, slots empty.
- Tap-to-place places tiles into slots left-to-right; tapping a filled slot returns its
  tile to the tray; tray order never changes.
- Completing the word in correct order → green flash, "Correct" logged, `/grade` POST
  returns 200 (verify against the local backend on `demo-token`).
- Completing the word in reversed order → grey flash, "NonWord" logged, `/grade` still
  fires with `targetWordId = config.target.id`, tagged `outcome: non_word`.
- **Known limitation**: with only the target word's own 2 tiles in the tray and no
  Region-1 word being the reverse of another, **WrongWord is not reachable in this
  prototype** — it becomes reachable once boss-only distractor tiles are introduced. The
  3-way check is implemented now for forward-compat regardless.
- All 4 Supported/Recall × Echo-on/off combos are switchable via the debug toggle and
  produce the expected hint/audio differences (Supported plays `wordAudio` on reveal +
  per-tile `phonemeAudio` on placement; Recall plays neither; both always play the
  post-build echo before the mic opens).

## Related

- Obsidian vault: `wordflow-game-design.md` (§Content engine, §Encounter outcome),
  `wordflow-region-1.md` (§Content engine, Day 1–5 schedule)
- `Assets/Scripts/MagicStonePuzzleController.cs` — source of the mic/WAV/`/grade` pattern
  being generalized into `GradeApiClient.cs`
- `gateway/app/models.py` (`wordflow-backend`) — `Word`, `Stone`, `GradeResponse` shapes
