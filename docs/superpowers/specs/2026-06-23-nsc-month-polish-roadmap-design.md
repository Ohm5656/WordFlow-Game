# NSC 2026 — One-Month Polish Roadmap (Region-1 Day-1 Vertical Slice)

**Date:** 2026-06-23
**Status:** Design locked (brainstorm). Implementation plans to be written separately.
**Context:** WordFlow won an *honorable mention* (not grand prize) at a recent demo
competition. NSC is the real target, ~1 month out. This doc captures (a) the diagnosis of
why we placed where we did and (b) the locked plan for the month.

> Scope note: the deliverable is **not** new subsystems. The skeletons are strong and
> mostly working (full dashboard exists, backend + encounter loop + cutscene system are
> built). The job this month is **finish quality + demonstrated efficacy** — polish the
> playable loop until it feels shippable, close rough edges, add the boss, and validate at
> the partner clinic.

---

## 1. Diagnosis — why honorable mention, not grand prize

Judge feedback was generic, so this is reasoned from NSC-style criteria (NECTEC: working
software, technical merit, impact/usefulness, presentation) and the gap between the
proposal's promise and the demo's reality.

1. **Promise-vs-delivery gap (the "completeness" problem, made precise).** The proposal
   sells four integrated subsystems (word-build game, AI Thai-phoneme checking, voice
   reading practice, progress dashboards). The demo could show one rough encounter. Judges
   read that as *risk* — "great pitch, can they build it?" — which caps a project at
   honorable-mention.
2. **No demonstrated efficacy.** For an assistive/clinical product, the grand-prize
   differentiator is *evidence it helps a real LD child*. We showed a concept, not a
   result. This is the highest-value, most fixable gap — we have a partner clinic.
3. **The actual technical differentiator wasn't visibly working.** The AI Thai-phoneme
   scoring (`revoiceai` / `/grade`) is the engineering "wow" NECTEC judges reward — and in
   the demo it was invisible/half-broken (the mic→grade issue). We got credit for the
   *idea* of AI, not the *demonstration* of it.
4. **Visible polish defects.** White build page, Thai glyphs rendering as □, slow/stalling
   TTS, uncomposed cutscene frames. Each live-demo glitch silently subtracts "finished"
   credibility.
5. **The teaching loop may not read in 60 seconds.** The no-text design is elegant but can
   be opaque to a judge watching: "what is the child learning, and how do you know it
   worked?" If that isn't instantly visible, the pedagogy advantage gets discounted.

Reasons #1, #4, #5 are completeness/polish (the dominant axis — the team's own instinct is
correct). Reasons #2 and #3 are the cheap-for-us levers that *convert* a polished demo into
a winner.

## 2. Strategy

Polish a **single complete vertical slice** to a winning finish rather than widening scope.
The slice is **Region 1, Day 1**: intro cutscene → 5 encounters (4 build + 1 Echo) →
nightfall/+stone → boss → win. Content stays Day-1-as-designed; the *polish bar* is
"backend visibly working + dashboard wired to real data + it looks shippable + a judge gets
the teaching loop in 60 seconds." Then prove it at the clinic.

This directly attacks all five diagnosis reasons and avoids repeating the over-scope mistake
(a one-month project crushed into five days).

## 3. The month, phase by phase

**Phase 0 — Current-state audit (do first, ~½ day).** The 2026-06-19 KNOWN ISSUES list in
the repo CLAUDE.md is stale; many items are reportedly fixed. Confirm what is *actually*
open vs. closed (grading firing, TTS responsiveness, Thai font, white build page, owl+bear
single-frame composition) and produce a short live punch-list. Polish reality, not memory.

**Phase 1 — Gameplay polish (the headline; most of the month).** Make the Day-1 arc *feel
finished*. Art-direction consistency (faded-daylight, never dark/scary — per the 2026-06-13
correction), reward FX, screen transitions, audio timing, and the moment-to-moment feel of
the tile→echo→mic loop. **Acceptance bar:** a judge watching 60 seconds instantly
understands "this is how it teaches a kid to read," and it looks shippable.

**Phase 2 — Close the rough edges (tweaks; parallel with Phase 1).** Whatever the audit
finds open — Thai font □, `/grade` actually firing on every attempt, TTS responsiveness,
owl+bear composed in one frame. Tweaks on strong skeletons, not rebuilds.

**Phase 3 — Boss fight (the one big net-new beat).** One boss, complete epic: **Heart 1**
(picture→word, ตา) + **Heart 4 finale** (ปี + bear bursts in + win + village re-saturates +
Region 2 teaser). Per the Region-1 design doc, those two phases alone read as a complete
epic — skip Hearts 2–3 this month.

**Phase 4 — Dashboard tweak (light).** The full dashboard already exists. Ensure it is wired
to *real* session data and reads as a compelling efficacy artifact for judges and the clinic
(session PAR, response latency, per-word progress). Tweak, don't rebuild.

**Phase 5 — Clinic validation (the grand-prize converter).** Formative clinician look
mid-month once Phase 1 is presentable; summative evaluation at the end; a tiny kid pilot if
polish allows. Output: an expert-endorsement / kid-data evidence artifact for the pitch.
This is the thing other teams cannot replicate.

**Phase 6 — Presentation cut.** A tight demo script + trailer that leads with the teaching
loop and the validation evidence.

## 4. Sequencing (≈4 weeks)

| Week | Focus |
|---|---|
| **W1** | Phase 0 audit → edge-fixes (Phase 2) + polish push begins (Phase 1) |
| **W2** | Phase 1 polish continues + **formative clinic look** (Phase 5a) |
| **W3** | Boss fight (Phase 3) + dashboard wiring (Phase 4) |
| **W4** | Final polish + **summative validation** (Phase 5b) + trailer (Phase 6) + buffer |

## 5. Explicitly parked (out of scope this month)

- **"Wrong-word-reveals-meaning" evolution** (the 2026-06-20 backlog item). It *changes the
  locked encounter loop* (adds a curated distractor stone so a real-but-wrong build becomes
  reachable and teaches its meaning) and needs its own brainstorm + spec. Strong post-NSC
  teaching upgrade; scope risk now.
- **Days 2–5 content (the remaining 20 quests)** and **boss Hearts 2–3.** Production target,
  not demo-critical.
- **Flutter shell integration.** Stays off the demo critical path (Unity-only on
  `demo-token`, `AUTH_IMPL=fake`).

## 6. Success criteria

- Day-1 arc plays start-to-finish with **zero visible glitches** (no white page, no □ Thai,
  no TTS stalls, owl+bear composed).
- `/grade` fires on every attempt and produces **real PAR + latency** that surfaces in the
  dashboard.
- Boss reads as a complete, winnable, non-scary epic (Heart 1 + Heart 4 finale).
- At least one **clinician evaluation** completed and captured as pitch evidence; a kid
  pilot if polish allows.
- A 60-second segment of the demo makes the teaching loop self-evident to a non-expert.

## 7. Related

- Design: `D:\Jarvis\Luk Nong Pong\wiki\wordflow-region-1.md`,
  `wordflow-game-design.md`, `wordflow-story.md`
- Proposal: `wiki/nsc-wordflow-proposal.md`
- Prior specs: `docs/superpowers/specs/2026-06-14-word-build-encounter-design.md`,
  `2026-06-17-encounter-cutscene-design.md`, `2026-06-19-authorable-encounter-design.md`
