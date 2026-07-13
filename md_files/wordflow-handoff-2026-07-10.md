---
name: wordflow-handoff-2026-07-10
type: handoff
tags: [work, product, game-design, handoff]
lang: en
date: 2026-07-10
---

# WordFlow — Regional scope handoff (10 Jul 2026)

Follow-up to [[wordflow-handoff-2026-07-09]]. Supervising advisor (พี่ไอดิน) directive:
regional round = **show potential, don't overcomplicate**. Star system instead of pet
system; keep the time/feel layer as specced; polish UI; night practice on
not-full-starred stages. Full spec + to-do: [[wordflow-regional-scope-2026-07-10]].

## What changed (one paragraph)

The smoke clock (9 Jul final spec) is untouched — it is the feel layer the advisor asked
to keep. The **verdict at the end renamed**: instead of pet-joins/wanders-off, the same
controller flag now yields **0–3 stars** — ⭐ healed (always), ⭐ no wrong builds,
⭐ before the smoke ceiling. Stars pop after the focus cam in the world scene (same slot
the pet verdict used) and sit as a 3-slot badge under each map node. At night, <3-star
nodes glow → replayable, best stars kept, attempts tagged `night_redo`. Pets, roster,
buddy skills, collection book, festival ending: **cut from regional, parked for
nationals** — design docs keep them in full.

## Rules that still bind

1. Build screen = book + stone only; stars never shown during the build.
2. Time is only the 3rd star — careful slow reader still gets 2. Latency = webapp
   metric, never in-game punishment.
3. Stone law, ceiling + release, continuous smokeT — all unchanged from 9 Jul.
4. Icon-only for every child-facing affordance (stars, moon marker included).
5. Daytime clinical 25-quest set locked; night redo is the only replay path.

## Why stars (defense for judges/team)

- Same verdict logic as the pet design — zero design throwaway, one rename.
- Judges decode filled/empty stars instantly (Angry Birds literacy); pets need
  explanation time the 7-minute demo doesn't have.
- Empty star slots on the map = the pull-back motivation that silhouettes provided,
  at a fraction of the art cost.
- Night redo of <3-star stages gives the "practice loop" story clinically and visibly.

## Superseded

- [[wordflow-handoff-2026-07-09]]: gift/creature-is-reward verdict → star verdict
  (regional only; returns at nationals).
- [[wordflow-regional-sprint-2026-07]] §4 "15–17 Jul challenge pack" line → to-do list
  in the scope doc.
