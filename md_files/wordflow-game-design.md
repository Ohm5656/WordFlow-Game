---
name: wordflow-game-design
type: synthesis
tags: [work, product, game-design, education, thailand]
lang: en
sources: 1
date: 2026-06-07
---

# WordFlow — Adventure Redesign (auto-run + word-building encounters)

Supersedes the original point-and-click "daily quest" loop. Driven by the
post-approval clinic feedback: the clinics approved the concept but flagged
**one must-fix — progression was too fast** — and the point-and-click quest
structure felt like a chore. This page is the locked design for the encounter
loop, session structure, and night practice mode. See [[wordflow-tech-stack]]
for the engine decision.

## Core loop — the encounter

The canonical loop, end to end:

> **Stop at a problem → build the target Thai word from phoneme/grapheme tiles
> (ย + า → ยา) → pronounce it → PAR + latency grade → visual reward → run on.
> ×5 → nightfall.**

- **Auto-run adventure.** The character runs automatically toward the region's
  main area — an old-Pokémon-route feel. The **player has zero control over
  movement.** This is deliberate: it removes navigation cognitive load (good for
  LD children) and keeps every child on the same rails, so the clinical data is
  comparable across players and sessions.
- **Random encounters interrupt the run.** An encounter is a problem that must
  be cleared to proceed. Examples:
  - Sick villager → build **ยา** (medicine)
  - Monster → build **ปา** (attack spell)
- **Resolution = word-building (THE selling point).** The child constructs the
  target word from phoneme/grapheme building-block tiles (e.g. **ย + า → ยา**),
  then pronounces it. This is the project's core innovation — learner-driven
  word construction, grounded in [[active-learning]] and
  [[grapheme-to-phoneme-g2p]]. The word *is* the puzzle.
- **Grading.** The spoken word is scored by [[phoneme-accuracy-assessment]]
  (PAR + Response Latency via [[revoiceai]], over the backend `/grade`
  endpoint). Grade A = 60–79% PAR, Grade B = >80%, plus retry.
- **Reward.** On success, a [[visual-reward-system]] beat fires (item drop,
  sparkle, level effect), then the run resumes.
- **Minimal text by design.** Words are short and icon-driven because the users
  are LD/dyslexic children. Reading sentences is not the task — building and
  saying the short target word is.

### Phonics & listening — the word-building is multimodal

Making magic from words is **not silent tile-assembly — it is auditory-first.**
This is the whole point for dyslexic children, whose weak link is exactly the
grapheme↔phoneme mapping:

- **Every letter tile sounds out phonically when used.** Each Thai consonant and
  vowel carries its own sound; the child *hears* the phoneme of each tile, not
  just sees the glyph (ย → /j/, ป → /p/, า → /aː/, …).
- **The child also hears the target word** — what the finished spell should sound
  like. The loop is: see the glyph → hear its sound → blend the sounds into the
  word → pronounce it for grading.
- This trains [[grapheme-to-phoneme-g2p]] in both directions (see→hear and
  hear→say) inside a single encounter.

### Example spells (consonant + า)

| Tiles | Word | Sound | Meaning / use |
|---|---|---|---|
| ย + า | ยา | yaa | medicine — heal the sick villager |
| ป + า | ปา | paa | to throw / hurl — attack the monster |
| ม + า | มา | maa | to come |
| ต + า | ตา | taa | eye |
| ก + า | กา | kaa | crow |
| น + า | นา | naa | rice field |

The tile set is data-driven, so new spells are new data rows (target word +
phoneme decomposition + each tile's sound), not new art — see the asset strategy
below.

## Session structure — the clinic fix

- **After 5 encounters, nightfall falls and the daytime adventure ends.** This
  hard-caps a daytime session to roughly the **15–30 minute attention span** of
  the target children and yields cleaner, comparable clinical data — a fixed
  number of graded attempts per session instead of an open-ended grind.
- **Daytime encounters are the clinically-measured data.** Nothing after
  nightfall counts toward assessment.
- **A region spans several in-game days, paced by one rule: at most ONE new magic
  stone per night.** This is the deepest part of the clinic "too fast" fix — the
  child cannot rush ahead of mastery, because new graphemes drip in one per day and
  earlier words keep recurring at a harder scaffold tier. A region also ends with
  **one extra review/approach day** (all stones owned, no new stone — "earn the
  right to fight") before the boss. Region 1 = **5 encounter-days (25 graded
  quests) → boss day**; concrete schedule in [[wordflow-region-1]].

## Night practice mode — no stakes

- After nightfall the child can keep playing, but it **does not affect daytime
  clinical performance data.** They cast spells built from words onto a **hay /
  training dummy** — free, low-pressure practice that sustains engagement
  without polluting the assessment dataset.

## Asset strategy — breaking the bottleneck

The real production bottleneck is that one teammate hand-generates every asset.
The redesign attacks this by making **content data-driven, not bespoke art**:

- The phoneme/grapheme **tiles and word-build UI are reusable and data-driven.**
  Adding vocabulary = adding a data row (target word + phoneme decomposition +
  icon), **not** new art or a new scene.
- This means the non-asset teammates can produce content (word lists, phoneme
  maps, encounter tables) in parallel **without waiting on the asset owner.**

### Demo asset manifest (minimum viable)

| Asset | Notes |
|---|---|
| Runner character | run cycle + idle |
| Region background | parallax loop; daytime + nightfall recolor |
| Encounter actors | 1 sick villager, 1 monster (demo) |
| Phoneme/grapheme tile UI | reusable, data-driven — art cost amortizes to ~0 per word |
| Word-build panel + mic/record button | grade result visuals (A / B / retry) |
| Visual reward FX | item drop / sparkle / level-up |
| Night practice | hay dummy sprite + spell-cast FX (reuse encounter FX) |

## Art direction — visual tone (2026-06-13 correction)

**Feedback received:** an early reviewer said the art style reads **too dark for kids** —
gloomy and a little scary. That's fair: the "Silence" was over-rendered toward *night /
horror* instead of *faded daylight*. The intent was always a world that's **a bit
colourless**, not one that's dark, grim, or frightening. This section is the governing
correction — when any scenery/atmosphere note below says "desaturated," "gloom," "dread,"
"oppressive," or "darkest," read it through these rules, not literally.

**The one principle: the Silence drains _saturation_, not _light_.** Separate the two
axes — *value/brightness* stays HIGH; only *colour saturation* drops. A silenced scene
should look like a **soft, faded watercolour under gentle overcast daylight**, not like
night, shadow, or fog-after-dark.

- **Keep it bright.** Backgrounds stay light and airy (soft sky, pale straw-cream, light
  warm-grey). No black, no heavy shadow, no murk. If a scene feels *dim*, it's wrong.
- **"Desaturated" = muted pastels, not grey-dark.** Think washed-out colour that's still
  clearly *colour* — gentle, storybook, friendly. The recolor reward then re-saturates
  and warms it back to vivid + cheerful.
- **No scary.** The corrupted creatures read **sad / sleepy / mopey / under-the-weather**,
  not menacing or creepy. The child should want to *help* them, not fear them.
- **Tension via stillness & quiet, never via darkness.** The "something's wrong here"
  feeling comes from *frozen motion, hush, and missing colour* — a held breath — not from
  shadows or a horror palette.
- **The boss is a gentle climax, not a nightmare.** A big grumpy/gloomy scarecrow, not a
  monster — challenging and a little dramatic, but never frightening for a young child.
- **Reference vibe:** sunny-storybook / *Animal Crossing*–soft / early-*Pokémon* warmth —
  the silenced state is just "the colour turned down," and the child turns it back up.

## Region/encounter systems (2026-06-10 detail)

The concrete Region 1 instantiation is in [[wordflow-region-1]]; the story rules in
[[wordflow-story]]. This section adds the reusable systems the encounter loop relies on.

### Camera — 6 phases
- **World map:** top-down region select (click an unlocked region).
- **Hub:** 3rd-person *fixed* behind the magician (not free-look — a free camera adds
  navigation load + motion risk for some LD children); home base that previews the region.
- **Travel:** top-down (old-Pokémon) — ideal for auto-run, lowest cognitive load.
- **Encounter ("over-the-book" focus cam):** the clinical moment = the *quietest* screen
  — stone panel dominant, the troubled actor small, owl at side, background frozen &
  colour-muted (still bright — see the art-direction correction above).
- **Boss:** focused; the boss looms upper-frame with heart HP + meaning-picture weak points.
- **Reward/FX beat:** ≤3s on the magic effect + local recolor, then resume.

### Magic-stone progression
Stones = graphemes. The child starts a region with a few stones and earns the rest by
repetition; collecting all of a region's stones unlocks its boss. **Progress % = stones
collected ÷ region total** drives both the always-on **progression bar** (Bookworm-
Adventures style) and the **hub recolor**. **Auto-save to the latest point** (no
waypoints — the player can't move). Region 1 goes 3 → 7 stones; see [[wordflow-region-1]].

### Content engine — many quests from few words (2026-06-14 revision)
"Same word, new situation, less help": (1) multiple **contexts** per word (breadth,
data-driven); (2) a **2-mode fading scaffold** across repeats — **Supported** (first meet
+ early repeats: "hear it" button, owl says the full word on first reveal, each tile
speaks its phoneme when placed) → **Recall** (later repeats, all pre-build hints off —
clearing Recall is where mastery is built); (3) an **Echo** format (sound-only prompt, no
picture) for ear training, orthogonal to Supported/Recall and pairable with either.
Tiles **always start in the tray** — never pre-placed in slots — in a fixed,
never-shuffled order; shuffling/distractor tiles are a **boss-only** difficulty lever.
Region 1 instantiates exactly **25 quest instances** from its 7 words across 5 days
without new code — see [[wordflow-region-1]].

### Encounter outcome — by the word built (pronunciation is never punished)
The child builds a word from stones, the owl echoes back what they built (the post-build
echo from the Content engine above), then they say it aloud. `POST /grade` (PAR +
latency) fires on **every** attempt — correct, non-word, or real-but-wrong — for clinical
data; it never blocks or delays the gameplay branch, which is decided purely by *what was
built*:
- **Correct target word** → full magic FX, quest resolves, **+1 level** (1 quest = 1
  level), run resumes. No pronunciation retry. `/grade` targets the encounter word's
  **sound-out reference** (see below): the child says the full ปอ อา ปา, so it is scored
  against the sounded-out sequence, not the whole word.
- **Non-word (no meaning)** → black smoke + soft fail SFX + owl "try again" → back to the
  magic-book screen. No effect. `/grade` still fires against the intended target's sound-out
  reference (so PAR shows how far off the attempt was), tagged `outcome: non_word`.
- **Real but irrelevant word** → its own magic FX plays but with no effect on the quest +
  owl "try again." `/grade` targets *the word the child actually built* (grading their
  pronunciation of what they attempted), tagged `outcome: wrong_word`.

Audio scaffolding: in **Supported** mode, a "hear it" button plays the target word and the
owl says it on first reveal, and each tile speaks its phoneme when placed; **Recall** mode
turns these off. In every mode, once the child finishes building, the owl voices the
blend of what they built once, before the mic opens (the post-build echo).

**Implemented encounter beats (Unity, 2026-06-17).** The prototype controller
(`WordBuildEncounterController`) runs the loop as an explicit phase machine, matching
this design and the cutscene framing in [[wordflow-region-1]]:

> intro cutscene → **Build** (tap tiles tray→slots) → **Confirm** (review the built word;
> a tile can still be pulled back to revise) → **post-build Echo** (owl voices what they
> built, always) → **Mic** (child taps to say it) → **`/grade` fires invisibly** — the
> child sees *nothing* about pronunciation (the LD no-text rule applied to scoring too) →
> branch by what was built: **Correct/suited** → magic FX + outro cutscene + "+1 level";
> **wrong_word / non_word (not suited)** → back to Build, "try again".

Two refinements over the older prose: a distinct **Confirm** beat (the child commits the
word rather than auto-resolving the instant the slots fill) and an explicit **Mic** button
that is the only thing that starts recitation — so grading never begins until the child
chooses to speak, and its result is never surfaced.

### Planned evolution (2026-06-20) — trial-and-error: a wrong word reveals its meaning
*2026-07-08: confirmed and folded into the Challenge & pressure system below (first
occurrence reveals, repeats surge). Kept for the original rationale.*

Today a real-but-wrong word (`wrong_word`) just plays its FX, gives "try again", and sends
the child back to Build — the mistake teaches nothing. The new direction: **a wrong but real
word should reveal its meaning** before sending the child back, so a mistake still *teaches*
("learning through trial and error"). Build กา (crow) when the quest wanted ปา → the child
sees/hears what กา **means** (the crow), then tries again. The encounter stays unwinnable
until the correct word is built, but no attempt is wasted.

This requires a **distractor stone in the tray**, which the as-built prototype lacks: the ปา
encounter holds only ป + า, so `WrongWord` is currently unreachable. The showcase adds a **ก**
magic stone as a distractor tile in that encounter, making กา a reachable wrong-but-real build.
(Distractor/shuffled tiles were previously scoped boss-only; this pulls a single curated
distractor into a normal encounter for the teaching moment.)

Paired refinement (also planned): the post-build **echo sounds the word out grapheme-by-
grapheme then blends** — ปา is voiced **"ปอ อา ปา"** (consonant name → vowel name → syllable),
matching the syllable-phonics naming used in the spell tables above, rather than only speaking
the finished word.

### Grading the sound-out (decided 2026-06-29)
The child says the **full ปอ อา ปา** at the mic (the owl models it, the child repeats the whole
blend) — kept deliberately, because the blend *is* the decoding skill, and because the doctor's
voice recording of the full sound-out is far more clinically useful than a recording of just the
whole word (the recording the doctor hears **is** the clip POSTed to `/grade`). The problem this
solves: scoring that 3-part utterance against the whole-word reference `/paː/` misaligns and
under-scores (saying ปา grades fine; saying ปอ อา ปา does not).

**Fix — grade against a sound-out reference, no backend logic change.** The recogniser
transcribes audio→IPA target-agnostically; `/grade` only compares it to the reference IPA of the
`targetWordId` it is handed. So a per-word **sound-out content entry** is added (e.g.
`paa_soundout`, ipa `pɔː ʔaː paː`) and the client sends *that* id for Correct/NonWord builds.
The same audio now aligns cleanly, and the per-phoneme scorecard breaks the score down across
ปอ / อา / ปา — *richer* doctor data (it shows *which* sound was missed), not just a single number.

Carries **no gameplay risk**: grading stays invisible and never branches the loop (the branch is
decided by the *built* word, not PAR), so a child who ignores the model and just says ปา simply
scores low against the sound-out reference while the loop continues and the doctor hears the
clip. Backend = one seed entry per word + re-seed; Unity adds a per-word `soundOutWordId` the
grade call resolves through. The reference IPA is a best-guess (the `ʔ` vowel onset in particular)
until validated against one real recording — non-blocking, given invisible grading. Latency stays
the active **build-time** clock summed across all tries (per-attempt telemetry rows, summed
backend-side), separate from the pronunciation PAR. Spec + plan:
`docs/superpowers/specs/2026-06-29-soundout-grading-design.md`,
`docs/superpowers/plans/2026-06-29-soundout-grading.md` (in the game repo). See also
[[phoneme-accuracy-assessment]].

## Challenge & pressure system — "the Silence fights back" (2026-07-08)

Driven by judge/advisor feedback (7 Jul): *the gameplay is too easy — it must be fun, and
challenge is the biggest source of fun; add time* (advisor, verbatim intent). This section
is the designed answer. Three principles governed every choice:

1. **Real pressure, aimed at the fiction — never at the decode verdict.** LD children live
   under constant real-world pressure *on the reading act itself*; anxiety narrows working
   memory, the exact dyslexic bottleneck. Stakes therefore threaten *game outcomes* the
   child cares about, never label the reading as failed. Grading stays invisible and never
   branches (settled, unchanged).
2. **Loss must be felt** — visible, concrete, on-screen — but always **recoverable with
   effort** (never punitive, never permanent).
3. **Difficulty and pedagogy ride the same axis** wherever possible: the challenge
   mechanics themselves generate more reading repetitions.

### The smoke clock — time pressure, in fiction

The universal antagonist is the **Silence itself** (present in every quest by definition —
no per-quest aggressor needed; the bear is a Region-1 skin, not the pattern). During the
Build phase, pale **silence-smoke seeps onto the book page** — the same smoke that shields
the boss, so every encounter foreshadows the boss fight.

- Smoke encroaches slowly with **time** (the advisor's clock, diegetic — no HUD timer).
- Music tightens as smoke thickens; clearing the word **before the smoke closes in** is
  what earns the quest's **gift** (see below).
- The world map and scene stay colourful — the pressure plays out **on the book page
  only**, never as world desaturation (the recolor channel is already owned by
  progression; the map stays inviting).
- **No-fail floor:** smoke never covers the slots, never makes a tile untappable, and the
  encounter can always be completed regardless of smoke state. If the child stalls long,
  the **owl wing-flaps the page clean once** (extends the existing hint ladder).

### Build rules (amends Build/Confirm phases)

- **Reading-order slot lock:** tiles are placed **left → right in reading order** — like
  writing the spell. A placed tile **locks** ("the ink dries"); pulling a locked tile back
  costs smoke progress. Placement becomes a deliberate, committed decision instead of free
  shuffling — this constraint *is* the game (and drills Thai reading order).
- **Each tile still chimes its phoneme when touched** (Supported tier) — information to
  decide *before* committing.
- **Wrong build, non-word** → **smoke surge**: the Silence swallows the placed stones back
  to the tray and thickens. (Re-fictions the existing "black smoke + try again" beat.)
- **Wrong build, real word (e.g. กา for ปา)** → **first time: the meaning-reveal moment**
  (per the 2026-06-20 planned evolution below — now confirmed): the word's art + meaning
  play, smoke **pauses** during the reveal (learning is never punished by the clock), then
  back to Build. **Repeats of the same wrong word** → treated as a normal surge (the
  teaching moment fired once; no exploit).

### Flow — the ceiling for strong players

Positive tempo layer on top: correct, prompt tile placements build **Flow** — the page
glows toward gold, music layers up, the owl leans in, casts at full Flow produce a
spectacular golden burst and a richer creature reaction. Hesitation or a wrong build calms
Flow back down; nothing is taken. The floor state is the normal game — Flow only adds a
top to chase (racing-game perfect-lap model), so slow children never meet a punishment
they can't ignore.

### Creature gifts — the missable stake

- Every healed creature *can* hand over a **memento** (the crow's feather, a bell charm,
  the lamb's flower — icon, drops into a **collection book**).
- The gift is earned only if the encounter is cleared **before the smoke closes in**
  (time + accuracy threshold). Too slow / too many wrong builds → the creature is still
  healed, the quest still wins, the level still grants — but it just waves goodbye.
  **No gift.** Real, visible, felt loss — with zero reading-verdict attached.
- **Recovery path (the psychological keystone): night redo.** At night practice the owl
  offers a redo of **any cleared quest with an unearned gift** — win it back. A miss costs
  extra effort later, never permanence. The "punishment" is literally more voluntary
  reading practice — difficulty axis and clinical axis on the same mechanic.
  Daytime redos do **not** exist: the 5-quest day and the comparable 25-quest dataset are
  locked clinic constraints. Night redos respect the existing nightly XP cap.
  **Night-redo data is saved, tagged, never mixed (2026-07-08):** redo attempts run the
  same invisible `/grade` + telemetry pipeline, recorded with a `night_redo` tag so the
  daytime clinical dataset stays cleanly filterable. Voluntary night practice volume is
  itself a motivation signal for the doctor, plus extra speech samples per weak word —
  dashboard defaults to daytime, night is a secondary view.
- **Collection value, per gift: the pick-a-buddy send-off (2026-07-08 refinement).** The
  creature becomes a **friend**. The trail is forward-only (each day resumes from
  yesterday's save point), so friends come to the hero — but the send-off is a **choice,
  not a parade**: each morning the child **picks ONE buddy for the day**
  (Pokémon-follower style). The buddy walks the trail all day, reacts to encounters
  (cheers casts, cowers at surges), sits at the night camp, and grants its **skill** for
  the day. This closes the economy loop: effort under pressure earns friends; friends
  ease pressure.
  Picking the buddy is also the one choice the child owns in a zero-control auto-run —
  autonomy as motivator.
  - **Roster scaling — the buddy circle:** the send-off scene shows only the child's
    **circle of ~6 friends** (arranged in the collection book; book = the full box,
    circle = the party), plus 1–2 rotating cameos. Scene length stays constant no matter
    how large the collection grows.
  - **Per-friend unique skills — icon + number only (2026-07-08 rev).** Friends DO get
    unique skills, under one hard constraint: every skill must be fully describable as
    **an icon and a number** (LD no-text rule) — e.g. 🕐+5 = smoke creeps 5s slower
    today; 💨+1 = one extra surge-save; 🌙+1 = one extra night redo; 🔍 = peek the next
    encounter's meaning-picture. The buddy's skill card (icon+number) shows at the
    send-off and in the collection book. **Build ONE skill for real; window-dress the
    rest** — every friend displays its card from day one, effects get wired over time.
    Presentation of depth without the systems cost. Flavor animations on top (crow
    circles overhead, dog barks at surges, bear is just huge). No rarity tiers ever —
    gifts are effort-based and every friend is earnable by every child (rarity feeling
    comes from story significance instead: the **bear joins the roster after the
    Region-1 boss** — the legendary buddy, no dice).
  - **Friends travel across regions** (the collection is game-long; each region adds its
    own roster on top). Friends still appear in the boss-scene crowd.
- **Collection value, full set:** all Region-1 gifts collected → the post-boss recolour
  plays as the **secret festival ending** — every friend parades through the restored
  village, the freed scarecrow waves from his pole. Missing some → the standard (still
  joyful) ending. Discoverable, replay-motivating, no gating anywhere.

### Region difficulty ladder — the rules grow up with the child

One new rule per region; Region 1 stays kind:

| Region | Pressure | New rule introduced |
|---|---|---|
| 1 | Flow + smoke as **ambient visual only** (mood + gift clock, no mechanical bite on tiles) | reading-order slot lock, single curated distractor (ก) with meaning-reveal |
| 2 | Smoke gets teeth: **muffles tray tiles** (glyph hazy, phoneme chime muted; tap-and-rub to clear) | look-alike distractor pairs |
| 3+ | **Surges swallow placed stones**; faster smoke; multi-cast "wave" quests (word ×2–3 at rising tempo) | shuffled trays |
| Boss (any region) | phase mechanics as designed | — |

**The pressure palette (2026-07-08, advisor note: time is not the only pressure):** future
regions pick deliberately from four pressure types — **time** (smoke clock), **pacing**
(surge rhythm, wave quests), **items** (carrying something fragile/precious, e.g. the R4
give-away), **scenarios** (rescue framing, order-matters choices). Region 1 uses time
only; the ladder adds one type at a time.

**Supersessions (2026-07-08):** "tiles in a fixed, never-shuffled order / distractors are
boss-only" (2026-06-14) is amended by this ladder — one curated distractor enters normal
encounters in Region 1 (the กา teaching moment), look-alikes in Region 2, shuffling from
Region 3. Tiles still always start in the tray, never pre-placed.

**Demo scope (24 Jul regionals):** smoke clock visual + reading-order lock + ก-distractor
meaning-reveal + gift drop, in the one ปา encounter. Flow = simple gold-glow tier if time
permits. Friends, night redo, festival ending = designed here, built on the nationals
track.

### Boss — picture→word, hearts, safe failure
The boss shows a meaning-picture; the child builds the matching word and says it. Correct
→ −1 heart (Region 1 boss has 3–4 hearts). **Wrong → no heart lost, no counterattack** (a
non-word fizzles to black smoke; a real-but-wrong word plays its FX but deals no damage),
owl "try again" — the child can never lose. Tiered hints: request → owl phonics + the
correct stone brightens; 10s idle → a correct stone auto-brightens (boss-only hint).

### Rewards & leveling
Reward = the **visual magic FX** (no item-drop system). **1 daytime quest = 100 XP = 1
level.** Night practice (the hay/scarecrow dummy) is optional and unscored: **5 XP/cast,
nightly cap ≈ ⅓ quest (33 XP), ~7 casts to cap** — it never advances stones, story, or
clinical data. The owl assists sparingly; the child discovers mechanics.

## Demo-scoping recommendation

Ship the demo **Unity-only**, booting end to end on `demo-token` (backend
`AUTH_IMPL=fake`). Keep the Flutter shell + `flutter_embed_unity` integration
**off the demo's critical path** — see [[wordflow-tech-stack]] for why.

**Demo backend runbook (verified 2026-06-17).** Unity's `GradeApiClient` and
`TtsApiClient` both point at **`http://127.0.0.1:8001`** with `Authorization: Bearer
demo-token`. That token only validates under **`AUTH_IMPL=fake`** — running the gateway
with the committed `AUTH_IMPL=firebase` returns `401 invalid_token` and looks to Unity
like "can't connect to the backend." Boot it from `wordflow-backend/gateway/` with:
`AUTH_IMPL=fake ../.venv/Scripts/python.exe -m uvicorn app.main:app --host 127.0.0.1
--port 8001`. Confirmed end to end: `GET /api/v1/tts?line_id=…` returns `200 audio/wav`
from the disk cache. (Live Gemini synthesis of any *uncached* line is still gated by the
free-tier daily TTS quota — pre-warm the cache before a demo.)

## Related

- [[wordflow-region-1]]
- [[wordflow-story]]
- [[wordflow]]
- [[wordflow-tech-stack]]
- [[active-learning]]
- [[grapheme-to-phoneme-g2p]]
- [[phoneme-accuracy-assessment]]
- [[visual-reward-system]]
