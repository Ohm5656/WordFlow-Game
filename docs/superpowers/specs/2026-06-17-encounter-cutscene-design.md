# Encounter Cutscene System — Design

**Date:** 2026-06-17
**Status:** BUILT 2026-06-17 (backend `/tts` tested; Unity side implemented + play-verified).
Content authored for **กา** (owl) and **ปา** (owl+bear); ปา bear lines flagged for polish.
Live `/tts` voice smoke (backend on :8001) still pending.
**Supersedes/extends:** `2026-06-14-word-build-encounter-design.md`
**Branch (backend):** `feat/tts-cutscene-voice` (wordflow-backend)

## Problem

When the player reaches an encounter, the game drops straight into the word-build
UI with no framing. We want a brief **visual-novel-style cutscene** that introduces
the situation ("you found someone who needs help") before the build, and a short
resolution after a correct build (the NPC reacts, thanks, foreshadows). Voice +
visuals only — **no readable dialogue text**, because the audience is Thai LD
children who cannot read fluently (the project's core design rule).

## Goals

- A **reusable, data-driven** cutscene system any encounter can reference.
- **NPC-only** presentation (villager, owl, bear…). The player avatar is never shown.
- **Voice + visuals only**, no on-screen sentences.
- Voice via the backend **`/tts`** endpoint (Gemini TTS), cached.
- **Auto-advancing** playback (story is "nice to have", not the selling point), with
  tap-to-advance / tap-to-skip.
- Plays as an **in-scene overlay** inside `word_build_prototype.unity` — no scene change.
- **Backward compatible:** an encounter with no cutscene assigned behaves exactly as today.
- Validated end-to-end on **Region 1 / Encounter 1** (ยา, the sick villager).

## Non-goals

- No branching/choices, no player dialogue, no FX/particle system in cutscenes.
- No on-screen captions or subtitles (explicitly excluded per the LD rule).
- No new scene / Build Settings entry (overlay only).
- No Core-layer state machine for the frame cursor (a list index in the player does
  not earn a separate unit-tested model the way `EncounterModel` did).

## Locked decisions (from brainstorming 2026-06-17)

| Decision | Choice |
|---|---|
| Voice source | Backend `/tts` (Gemini TTS), hybrid: disk-cached + pre-warmed for demo safety |
| On-screen text | None — voice + visuals only |
| Integration | In-scene overlay before build (intro) and after correct build (outro) |
| Scope | Reusable data-driven system |
| Characters | NPCs only; no player avatar |
| Pacing | Auto-advance; tap to advance early / skip |
| Beats | Two slots per encounter: `introCutscene` + `outroCutscene` |

## Encounter 1 narrative (grounding case)

From `wordflow-region-1.md`, Day 1 / Quest 1 — **ยา /jaː/ medicine**, first hut past
the entrance arch:

- **Intro:** villager slumped grey against a straw house, breath shallow ("The
  Silence" opener). Owl nudges the child to help.
- *(word-build: child builds ยา to heal him)*
- **Outro:** villager rises, *wais* in thanks, points down the path, **warns of the
  dark ahead**, waves the player on.

## Backend `/tts` — DONE (reference)

Already built, tested, and committed in `wordflow-backend` (`feat/tts-cutscene-voice`):

- `GET /api/v1/tts?line_id=<id>` — bearer auth (same as `/grade`). Returns `audio/wav`.
  `404` unknown line, `503` synthesis unavailable.
- Model: `gemini-3.1-flash-tts-preview`. Returns 24kHz/16-bit/mono PCM → wrapped to WAV.
- Server-side **sha1 disk cache**; `scripts/prewarm_tts.py` pre-generates all lines.
- Line registry `gateway/tts_lines.json` (client sends only a `line_id`, never raw text).
- Env key `Google_TTS` (a Gemini API key from Google AI Studio, **not** Cloud TTS).

### Encounter 1 lines (already authored in `tts_lines.json`)

Voices: owl = `Leda`, villager = `Charon`.

| line_id | Thai | EN gloss | delivery |
|---|---|---|---|
| `e1_intro_villager_1` | ช่วย... ช่วยด้วย... | "Help... please, help..." | weak, trembling |
| `e1_intro_owl_1` | ดูสิ! ชาวบ้านไม่สบาย เราต้องช่วยเขา | "Look! The villager is sick. We must help him." | warm, gentle |
| `e1_intro_owl_2` | เสกคำว่า ยา สิ แล้วเขาจะหายดี | "Cast the word ยา, and he'll get better." | encouraging |
| `e1_outro_villager_1` | ขอบคุณมากนะหนู หนูช่วยลุงไว้ | "Thank you so much, little one. You saved me." | warm relief |
| `e1_outro_villager_2` | ข้างหน้า... มีความมืดรออยู่ ระวังตัวด้วยนะ | "Ahead... darkness is waiting. Be careful." | soft warning |
| `e1_outro_owl_1` | ไปกันต่อเถอะ ยังมีคนรอเราอยู่อีก | "Let's keep going. Others are still waiting." | cheerful |

## Unity side — to build

All new code lives in the existing `WordFlow.Adventure` assembly, matching the
layered View/Data/Net structure and the defensive null-safe style.

### 1. Data — `Assets/Scripts/Adventure/Data/CutsceneData.cs`

```
[CreateAssetMenu(menuName = "WordFlow/Cutscene")]
CutsceneData : ScriptableObject
  List<CutsceneFrame> frames

[Serializable] CutsceneFrame
  Sprite        background    // nullable; e.g. background_cut_scene.png
  Sprite        npc           // nullable; villager/owl/bear art
  NpcSide       side          // Left | Center | Right
  AnimationClip npcAnim       // nullable; e.g. Bear_Entrance.anim
  string        voiceLineId   // -> GET /tts?line_id=...
  AudioClip     voiceClip     // nullable override (offline/baked); wins over voiceLineId
  float         holdSeconds   // minimum on-screen time (default ~1.5)
```

No caption field — voice + visuals only.

Authored assets under `Assets/Data/Adventure/Cutscenes/` (e.g. `e1_intro.asset`,
`e1_outro.asset`).

### 2. Net — `Assets/Scripts/Adventure/Net/TtsApiClient.cs`

Mirrors `GradeApiClient` (same base URL `http://127.0.0.1:8000`, `Bearer demo-token`).

```
GetLine(string lineId, Action<AudioClip> callback)
  - GET {base}/api/v1/tts?line_id={lineId}
  - in-memory cache keyed by lineId; optional disk cache in Application.persistentDataPath
  - decodes WAV -> AudioClip (UnityWebRequestMultimedia / DownloadHandlerAudioClip)
  - null-safe: any failure -> callback(null); caller shows the frame silently
```

### 3. View — `Assets/Scripts/Adventure/View/CutscenePlayer.cs`

Thin MonoBehaviour; overlay built on the encounter's existing Canvas/EventSystem.

- `Play(CutsceneData data, Action onFinished)`.
- Per frame: set background + NPC sprite (fade/slide in by `side`); resolve audio
  (`voiceClip` if set, else `TtsApiClient.GetLine(voiceLineId)`); play; wait
  `max(clipLength, holdSeconds)`; advance.
- **Auto-advance** on audio finish. **Tap anywhere** = advance early; tap during the
  last frame (or a skip affordance) = skip to `onFinished`.
- Defensive: null sprite/clip → show/advance silently, never throw.
- Raises `onFinished` exactly once.

### 4. Data wiring — `EncounterConfig`

Add two nullable fields:

```
public CutsceneData introCutscene;   // nullable
public CutsceneData outroCutscene;   // nullable
```

### 5. Integration — `WordBuildEncounterController`

Surgical, backward-compatible:

- `Start()`: if `config.introCutscene != null` → build UI hidden, `CutscenePlayer.Play(intro, BeginEncounter)`. Else `BeginEncounter()` immediately (today's behavior).
- On `Outcome.Correct` (in `ResolveBuild`): if `config.outroCutscene != null` → play it
  before the existing "+1 level" beat; else proceed as today.
- No cutscene assigned anywhere → identical to current flow.

## Testing strategy

- **Core/pure:** none required (no new Core logic; the frame cursor is trivial).
- **Net:** `TtsApiClient` WAV→AudioClip decode is Unity-runtime; cover with a PlayMode
  smoke (or manual) since `UnityWebRequest` needs the engine. Keep logic minimal.
- **Manual / MCP:** load `word_build_prototype.unity`, assign `e1_intro`/`e1_outro`,
  Play, screenshot each frame, confirm audio plays and hand-off to the build works.
- **Backend:** already covered (8 tests + live smoke; full suite 106 passed).

## Open questions / assumptions

1. **Thai TMP font** — the existing open follow-up (LiberationSans renders ก/า as □)
   does **not** block cutscenes (no on-screen text), but the build HUD still needs it.
2. **Transition polish** — fade in/out between frames and into the build is assumed
   minimal (alpha lerp), matching the old flow's hand-rolled coroutine style.
3. **NPC art** — `visaul_novel/character/{bear,owl}` exist; a **villager** sprite for
   Encounter 1 may need sourcing (placeholder colored Image acceptable for first pass).

## Success criteria

- Reaching the prototype encounter plays `e1_intro` (villager + owl, voiced, no text),
  auto-advancing, then hands off to the word-build.
- Building ยา correctly plays `e1_outro` (thanks + warning), then the +1 beat.
- An encounter with no cutscene assigned is byte-for-byte the current experience.
- Voice comes from `/tts`, served from cache on repeat (no per-play network wait).
