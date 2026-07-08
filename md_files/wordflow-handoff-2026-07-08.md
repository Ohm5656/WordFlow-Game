---
name: wordflow-handoff-2026-07-08
type: handoff
tags: [work, product, game-design, handoff]
lang: en
date: 2026-07-08
---

# WordFlow — Design update handoff (8 Jul 2026)

One-page summary of the redesign decided 8 Jul, answering the judge/advisor feedback:
**story too dull · gameplay too easy · UI/UX loose ends**. Full detail lives in
[[wordflow-story]], [[wordflow-game-design]] ("Challenge & pressure system"), and
[[wordflow-region-1]] (Amendments). Sprint impact in [[wordflow-regional-sprint-2026-07]].

## 1. Story (what changed)

- **Straw Village = the hero's own home.** The Silence hits it in front of them — personal
  stake, not wandering do-gooder.
- **New game intro:** ~30–40s wordless cutscene, 4 beats — happy noisy village → Silence
  sweeps (colour/sound drain) → hero hesitates, owl nudges → first word spoken, one flower
  recolours. *Speaking heals the world*, shown not told.
- **Each region gets a unique signature twist** (R1 surprise-ally bear stays; R2
  misdirection, R3 undo-your-mistake, R4 give-away — never reused).
- **B-story:** each boss drops one note of a **lost song**; the finale (Silver Castle =
  the king's residence) is won by *singing the song TO* the Silence — a being no one ever
  spoke to.
- **Region 1 anti-filler:** the bell quest now *calls the boss out*; the boss scene stages
  every healed creature returning (bear charge, crows mob, villagers gather, hayfield
  battlefield). No quest is filler.

## 2. Gameplay challenge (what changed)

Principle: **real pressure aimed at the fiction, never at the reading verdict** (LD
psychology). Grading stays invisible and never branches — untouched.

- **Smoke clock:** Silence-smoke seeps onto the book page during Build — the diegetic
  timer (advisor's "add time"). No HUD, map stays colourful. Never blocks completion.
- **Reading-order slot lock:** tiles place left→right; placed tiles lock ("ink dries");
  pull-back costs smoke progress. Committed decisions, no free shuffling.
- **Wrong builds:** non-word → smoke surge swallows placed stones; real word (กา) → the
  meaning-reveal teaching moment first time, surge on repeats.
- **Gifts = the stakes:** clear before the smoke closes → the creature hands a memento
  (collection book). Too slow → quest still won, **no gift**. Real, visible loss — zero
  shame.
- **Night redo:** missed gifts re-earnable at night practice. Redo attempts ARE saved
  (same invisible /grade + telemetry) but tagged `night_redo` — the daytime 25-quest
  clinical dataset stays cleanly filterable; night volume = a motivation signal for the
  doctor. The "punishment" is more reading practice.
- **Friends — pick-a-buddy send-off:** each morning the child picks ONE buddy for the day
  (Pokémon-follower: walks the trail, reacts, grants its unique skill for the day —
  every skill must read as **icon + number** (e.g. 🕐+5 slower smoke, 💨+1 surge-save);
  build ONE skill for real, window-dress the rest; no rarity tiers). Send-off
  shows the **buddy circle of ~6** (full roster in the collection book) so the scene never
  bloats. Friends travel across regions; the **bear joins the roster after the R1 boss**.
  Full Region-1 set → secret **festival ending**.
- **Difficulty ladder:** R1 kindest (smoke = visual + gift clock only) → R2 tile-muffling
  → R3+ swallowed stones/shuffle/waves. Rules grow up with the child.

## 3. Sprint impact (before 21 Jul freeze)

| Item | Owner | Notes |
|---|---|---|
| **15–17 Jul challenge pack**: ก-reveal + smoke clock + slot lock + gift drop, one controller change | Claude | Flow gold-glow only if time permits |
| **Game intro cutscene** on existing cutscene system | Claude + **Ohm: 3–4 stills** (noisy village / Silence sweep / hushed square / first-word burst) | wordless, ≤40s |
| **UI/UX checklist**: adventure-start button, nightfall transition, night menu, back/home everywhere, tile/mic juice, region-lock map, EXP popup, icon-only audit | Claude + Ohm | script-filtered |
| Friends / night redo / festival ending / regions 2–5 | — | **nationals track**, design only for now |

## 4. Rejected on the way (don't re-propose)

Stars-as-keys gating (fights adventure flow) · colour tug-of-war (greys the colourful
map) · tile-degradation as main pressure (rage-quits weak kids, ignores strong) ·
positive-only flow (no real stakes) · raw timers (latency IS the LD deficit).
