# Word-craft effects + hover rate-limit — design

Date: 2026-06-20
Branch: feat/word-build-encounter-prototype
Scope: Adventure word-build encounter (`WordBuildEncounterController`), the live demo
encounter (target ปา, distractor กา).

## Goals

1. **Hover-to-hear rate-limit.** Hovering a tray stone replays its phoneme on every
   `OnPointerEnter`, spamming the audio. Add a per-stone cooldown + no-overlap so a held/jittery
   pointer can't machine-gun the sound.
2. **Per-word craft effects.** When the child builds a real word, play a word-specific visual:
   - **ปา** ("to throw") → a thrown object arcs across the screen and the bear runs away.
   - **กา** ("crow", the real-but-wrong-context word) → a flock of crows flies in (reveals its
     meaning — the "wrong word reveals meaning" beat).

## Non-goals

- No skeletal/Animator animation — procedural translate/lerp coroutines only (matches the
  existing `RevealStone`/`SmokePuff` style and the old-flow convention).
- No change to the locked encounter loop, grading, telemetry, or cutscene paths. Effects are
  purely additive at the resolve step.
- No new Core logic → no new EditMode tests (effects are untested view code, like `RevealStone`).

## Assets (already in repo)

| Role | Path |
|------|------|
| Thrown object | `Assets/Art/visaul_novel/background/thrown object.png` |
| Crow | `Assets/Art/visaul_novel/background/crow transparent.png` |
| Bear | `Assets/Art/visaul_novel/character/bear/bear-cutscence.png` |

These pngs must be imported as **Sprite (2D and UI)** to drive uGUI `Image`s.

## Design

### 1. Hover rate-limit — `WordBuildEncounterController.OnTileHovered`

- New: `[SerializeField] float hoverCooldown = 0.6f`; state `_lastHoverIndex = -1`,
  `_lastHoverFreeAt = 0f`.
- On hover (Supported mode, Building/Confirm phase, as today):
  - If `trayIndex == _lastHoverIndex && Time.realtimeSinceStartup < _lastHoverFreeAt` → ignore.
  - Else `audioSource.Stop()` (cut any prior hover sound = no overlap) then
    `audioSource.PlayOneShot(phonemeAudio)`; set
    `_lastHoverFreeAt = now + clip.length + hoverCooldown`, `_lastHoverIndex = trayIndex`.
  - A *different* stone plays immediately — still responsive.
- Phoneme clips are local/baked, so no live `/tts` call sits in this path.

### 2. Effect data — `WordEncounterData`

- `enum WordEffectKind { None, ProjectileBearFlees, Birds }` + field `WordEffectKind effect`.
- `Sprite effectSprite` (nullable): the thrown object for ปา, the crow for กา.
- Authoring: ปา word asset → `ProjectileBearFlees` + `thrown object.png`; กา word asset →
  `Birds` + `crow transparent.png`.

### 3. Effect rendering — new coroutines in `WordBuildEncounterController`

- Lazily create `_fxLayer`: a full-rect `RectTransform` under the root canvas
  (`trayContainer.GetComponentInParent<Canvas>()`), `raycastTarget` off, top sibling — so a
  projectile can travel across the whole screen above the build UI.
- Serialized: `Sprite bearSprite`; tunables `projectileArcSeconds`, `projectileArcHeight`,
  `bearFleeSeconds`, `crowCount`, `crowFlySeconds`, plus effect sprite sizes.
- `PlayWordEffect(WordEncounterData)` — `switch` on `effect`:
  - `ProjectileBearFlees`: bear `Image` enters/sits, then a thrown-object `Image` arcs on a
    parabola from one side to the other while the bear `Image` slides off-screen. Destroy both.
  - `Birds`: spawn `crowCount` crow `Image`s that fly across with a per-bird stagger/offset,
    then destroy.
  - `None`/null sprite → no-op.

### 4. Wiring — `WordBuildEncounterController.ResolveBuild`

In the real-word branch, after `yield return RevealStone(builtWord)` and before the
correct/wrong cutscene branch:

```
yield return PlayWordEffect(builtWord);
```

Runs for both Correct (ปา) and WrongWord (กา). Non-word branch unchanged (smoke). Existing
cutscene, +1-level, retry, telemetry, and grade logic untouched.

## Verification

- EditMode suite still green (no Core change).
- Play-mode eyeball on `word_build_paa_polished.unity`: build ปา → thrown object arcs + bear
  flees; build กา → crows fly; hover a stone repeatedly → no audio spam, switching stones is
  responsive.
