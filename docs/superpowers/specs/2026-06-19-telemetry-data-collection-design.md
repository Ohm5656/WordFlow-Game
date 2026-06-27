# Telemetry / Child Data Collection — Design

**Date:** 2026-06-19
**Status:** Brainstorm / design (not yet implemented)
**Goal (user):** "Collect data of the kids — both *where they are* in the game and *their grading*."

## Decisions locked (2026-06-19)
- **Scope:** position events **+ per-build attempts** (what the child built each try, not just the final clear).
- **Identity:** **hardcoded demo child** (`kidId = "demo-child-01"`) for the laptop demo; real picker comes from the Flutter shell post-demo.
- **Reliability:** **durable queue now** — buffer to disk + retry on reconnect so nothing is lost offline.

## What already exists (do NOT rebuild)
The backend (`D:\Gimme\wordflow-backend`, gateway on `:8001`, `AUTH_IMPL=fake`, `Bearer demo-token`)
already persists most of this to Firestore. Real routes (all `prefix=/api/v1`):

| Need | Endpoint | Notes |
|------|----------|-------|
| Pronunciation grading | `POST /grade` | Persists an `Attempt` (par, grade, IPA, latencyMs) under `children/{kid}/sessions/{sid}/attempts`. **Unity already calls this** (`GradeApiClient`). |
| Read attempts | `GET /children/{kid}/attempts` | — |
| Progression | `POST /children/{kid}/progress/events` | Body `{type, questId?, region?}`, server-authoritative, returns `ProgressResponse`. **Unity never calls this.** |
| Read progress | `GET /children/{kid}/progress` | — |
| Session open | `POST /children/{kid}/sessions` | Body `{island:int}` → `{sessionId, startedAt}`. |
| Session close | `PATCH /children/{kid}/sessions/{sid}` | Body `{avgAccuracy, avgLatency, wordsAttempted, level}` → 204. Feeds the doctor/parent dashboard. |
| Dashboard | `GET /children/{kid}/dashboard` | Aggregates per region, trend, struggling/strong phonemes. |

**Two real gaps, not a missing pipeline:**
1. **"Where are they"** — Unity never POSTs `/progress/events` and never opens/closes a session, so `childId`/`sessionId`
   on `GradeContext` are always empty and attempts don't group into sessions.
2. **Per-build attempts** — the `Attempt` schema is *pronunciation only*. It has **no `builtString` / `outcome` / `buildLatencyMs`**.
   `GradeApiClient` already sends `outcome` + `buildLatencyMs` as form fields, but `grade.py` drops them.
   `ProgressEvent.type` is a closed `Literal` and can't carry them either. **So build-outcome data has nowhere to land today.**

## Design

### Unity (`Assets/Scripts/Adventure/Net/`)
- **`SessionContext`** (small SO or static holder): hardcoded `kidId`; on app start `POST /sessions {island:1}` → cache `sessionId`.
  Populates the already-present-but-empty `childId`/`sessionId` on `GradeContext`.
- **`TelemetryClient`** (twin of `GradeApiClient`, durable):
  - `Enqueue(endpointPath, jsonBody)` → append one line to an on-disk journal
    (`Application.persistentDataPath/telemetry_queue.jsonl`).
  - Pump coroutine drains the journal: POST each; on 2xx drop the line; on failure keep + exponential backoff.
    Flush on **app start** (recover last session's unsent lines), periodically, and on **pause/quit**.
  - Convenience wrappers: `PostProgressEvent(type, questId/region)`, `PostBuildAttempt(record)`.
- **Wire into `WordBuildEncounterController`** (no Core changes — it already computes `Outcome` + has `buildLatencyMs`):
  - each build attempt resolved → `PostBuildAttempt` (built string, outcome, latency).
  - encounter cleared (Correct) → `PostProgressEvent("quest_completed", questId)`.
  - app quit → `PATCH /sessions/{sid}` with the sitting's aggregates.

### Backend (the addition the "per-build attempts" decision forces)
Minimal: a non-audio sibling to `/grade` so build attempts are recorded even when the mic captures nothing
(the current "0 samples" path otherwise loses them):
- `POST /children/{kid}/sessions/{sid}/build-attempts` with `{wordId, builtString, outcome, buildLatencyMs}`
  → persist to a `build_attempts` subcollection; optionally fold into the dashboard.
- Alternative (smaller, but mic-coupled): add `builtString/outcome/buildLatencyMs` to `Attempt` + persist the
  form fields `grade.py` already receives. Rejected as primary because it only fires when `/grade` fires (mic-dependent).

## Build order
1. Backend: add build-attempts endpoint + persistence (+ test). *(blocks per-build telemetry)*
2. Unity: `TelemetryClient` durable queue (unit-test the journal drain/retry in Core if extracted).
3. Unity: `SessionContext` + session open/close.
4. Wire controller call sites; verify in Play mode that events land (check `GET /attempts` / dashboard).

## Open / dependencies
- Build-attempt persistence depends on the backend addition above (step 1).
- `childId`/`sessionId` only become non-empty once `SessionContext` lands — until then `/grade` records have blank session grouping.
- Durable queue is the bulk of the Unity work; keep the journal format dead-simple (one JSON object per line).
