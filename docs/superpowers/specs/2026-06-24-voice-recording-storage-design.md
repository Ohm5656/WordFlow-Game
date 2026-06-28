# Voice-Recording Storage — Design

**Date:** 2026-06-24
**Status:** Approved (design), pending implementation plan
**Author:** solo (backend-focused for the NSC 2026 build)

## Problem

Doctors and parents need to **listen back to a child's spoken word-attempts** for diagnosis.
Today the child's pronunciation audio flows to the backend `/grade` endpoint, gets scored, and
is **discarded** — the attempt record even carries a deliberate `"audioUrl": None` placeholder
(`gateway/app/services.py:78`) that was never filled. This feature persists those clips and
lets authorized adults play them back later, from a different device, days later.

Because this is **minors' voice data used for clinical diagnosis**, it is sensitive personal
data under Thai PDPA. The design builds the *technical controls* that make a future PDPA process
truthful (access control, consent gating, encryption at rest, retention, erasure, audit) without
over-building consent UI/paperwork that is legal process rather than code.

## Decisions (locked during brainstorming)

1. **Cloud storage, not local.** Access is **remote / different device** (parent on their phone,
   clinician at a clinic PC — not the tablet the child played on), which rules out local-only
   storage.
2. **A recording is exactly a graded word-attempt (1:1 with `/grade`).** ~5s pronunciation clips
   (e.g. ปอ–อา–ปา). No free-form / standalone speech capture in scope. This is why the schema
   hangs `audioUrl` on the *attempt* record, not its own collection.
3. **Ingest at `/grade` (no Unity change), architect storage as its own backend module, keep a
   swappable store seam.** The bytes already reach the gateway for grading, so we reuse them; the
   storage handling lives in a clean, separated module; the store backend is swappable so a future
   direct-to-GCS upload is a config swap, not a rewrite. (We explicitly dropped a client-side
   durable WAV upload queue as speculative complexity — recordings are 1:1 with grading, and the
   bytes are already server-side the moment grading succeeds.)
4. **Proxy write now; reads always direct-from-GCS.** Confirmed acceptable; revisit on upgrade.

## Separation model (the "don't overwhelm the backend" constraint)

Three distinct concerns, separated deliberately:

| Concern | Separated? | How |
|---|---|---|
| **Storage tier** (where bytes live) | Yes | Audio lives in a **GCS bucket** — never in Firestore, never on gateway disk. |
| **Read / playback path** (adults listening) | Yes — the load-bearing one | Listeners stream **direct from GCS via short-lived signed URLs**. Audio egress never transits the gateway, so the read flood that would overwhelm the backend is fully offloaded. |
| **Write path** (bytes into storage) | Proxy now | The gateway forwards bytes it **already holds for grading** to GCS in a **background task** — async, never blocks the grade response. |

Key realization: because recordings are 1:1 with grading, **every clip already passes through the
gateway** (the recognizer needs the bytes). "The gateway never touches audio" is therefore
impossible while grading is server-side. What actually causes overwhelm is the **read path**
(many adults re-streaming many clips) and **storing blobs in the app DB** — both eliminated here.
The proxy write is a 160 KB background handoff of bytes already in memory, once per spoken word —
not an overwhelm vector at any realistic scale, and reversible via the seam.

## Architecture

### The storage seam (mirrors existing `*_impl` pattern)

The backend already swaps `recognizer_impl` / `database_impl` / `auth_impl` via `Literal`
config injected through `app.state` (`gateway/app/config.py`). The recording store matches that
exact pattern:

```
IRecordingStore (Protocol):
    store(child_id, attempt_id, wav_bytes) -> object_ref      # e.g. gs://<bucket>/children/<id>/<attempt>.wav
    playback_url(object_ref, ttl_seconds)  -> signed_url
    delete(object_ref)
    delete_for_child(child_id)                                # erasure
```

Selected by `recording_store_impl: Literal["local", "gcs"]`:

- **`LocalDiskStore`** — dev/tests, fully offline (the `fake`/`memory` equivalent). `object_ref`
  is a file path; `playback_url` returns a local `file://` or a dev HTTP path.
- **`GcsStore`** — proxy write + signed-URL read against a real bucket.

A future **`GcsSignedUploadStore`** (direct-to-GCS write, "true C") is an *additive* third impl;
the data model, dashboard API, consent gate, retention, and audit do not change. **Not built now.**

### Write flow (ingest at `/grade`)

`/grade` is **unchanged on the wire** — Unity already sends `audio + childId + targetWordId`.
After scoring, the gateway:

1. **Always grades and returns.** Gameplay-critical and consent-independent — the grade response
   is **never blocked or failed by storage**.
2. **If** the child's `recordingConsent == true`: schedule a **background task** to `store()` the
   bytes, then write the returned `object_ref` into the attempt record's `audioUrl` field.
3. **On storage failure** (GCS hiccup): log + best-effort retry in the background. Never surfaced
   to the child; never affects the grade. (Retry is server-side, where connectivity to GCS is
   reliable — strictly better than a flaky client-side queue.)

**Unity is untouched.** Consent is enforced backend-side, so the game build does not change.

### Read / dashboard API (doctor/parent surface)

- `GET /api/v1/children/{id}/recordings` — list attempt metadata that has audio (word, date,
  grade, whether an object is present). Guarded by the existing `require_kid_access`.
- `GET /api/v1/children/{id}/recordings/{attemptId}/playback-url` — mints a **signed URL**
  (default **15 min** TTL) **and writes an audit row**. The dashboard / Flutter shell plays
  straight from GCS; audio bytes never transit the gateway.

The dashboard/Flutter playback UI is a **consumer** of this API and is out of scope for this spec
(separate surface); the contract above is what it needs.

## Data model (Firestore)

- **Attempt record:** `audioUrl` now holds the **object ref** (the `gs://…` path), **not** a
  public URL. Signed URLs are minted on demand and never stored. (Fills the existing
  `services.py:78` placeholder.)
- **Child doc:** `recordingConsent: bool` — **defaults to `false` (privacy-by-default,
  PDPA-aligned).** No clip is ever stored unless this is explicitly `true`. Flipped on for demo
  children.
- **Audit:** `children/{id}/recordingAccess` collection — one row per playback-URL mint:
  `{ accessorUid, attemptId, ts }`.

## Retention & erasure

- **Auto-delete:** a **GCS bucket lifecycle rule** purges objects after `retention_days`
  (default **90 days**, configurable). Configured on the bucket, not in code.
- **Right-to-erasure:** `DELETE /api/v1/children/{id}/recordings` → `delete_for_child()` purges
  all objects for the child and clears their `audioUrl` refs. The headline PDPA control.

## Encryption at rest

Automatic on GCS (Google-managed keys). Nothing to build; the requirement is simply not to
undermine it (no plaintext copies on gateway disk; `LocalDiskStore` is dev/test only).

## Mobile / deployment notes

Mobile deployment of the Unity client before competition day **does not change the storage
architecture** (Unity is untouched for this feature; no on-device audio is persisted; reads
bypass the app). It does affect the **connectivity this feature rides on** — captured here so it
is not a surprise on the day:

1. **Gateway URL must be network-reachable, not `127.0.0.1`.** On a phone, `127.0.0.1` is the
   phone itself. Unity's `gradeUrl` / telemetry `baseUrl` must point at the laptop's LAN IP (local
   demo) or a deployed URL. No reachable gateway → nothing graded **or** stored.
2. **Android/iOS block plain HTTP.** Android 9+ blocks cleartext `http://` without a
   network-security-config exception; iOS ATS expects HTTPS. Run the gateway over HTTPS or
   whitelist cleartext for the demo, or `/grade` and telemetry POSTs silently fail.
3. **Mic permission is already handled** in `GradeApiClient` under `UNITY_IOS || UNITY_ANDROID`
   (`Application.RequestUserAuthorization(UserAuthorization.Microphone)`). No on-device audio
   storage is added, so the app sandbox footprint is unchanged.
4. **GCS is unaffected** — it is a gateway↔GCS hop; the phone never talks to the bucket on the
   write path.

## Failure handling (summary)

- No consent → silently skip storage; grade still runs and returns.
- GCS write fails → log + background best-effort retry; never fails `/grade`.
- GCS unreachable on read → playback-url mint returns an error to the dashboard (adult-facing,
  fine to surface); never affects gameplay.

## Testing (pytest, matches existing backend suite)

`LocalDiskStore` keeps the feature fully offline-testable:

- store → retrieve → delete round-trip via the seam.
- **consent gate:** `recordingConsent == false` ⇒ nothing stored, grade still returns normally.
- **erasure:** `delete_for_child` removes objects **and** clears `audioUrl` refs.
- **audit:** a row is written on every playback-url mint.
- signed-URL TTL is honored.
- **storage failure never breaks `/grade`** (inject a failing store; assert grade still 200s).

## Out of scope (deliberately — simplicity-first)

- Consent-revocation workflows, data-subject-access portals, multi-role clinician management.
- The direct-to-GCS upload impl (`GcsSignedUploadStore`, "true C"). The seam permits it; we add
  it only when write volume justifies it.
- The dashboard/Flutter playback UI (separate surface; this spec defines its API contract).

## Open tunables (defaults chosen, easy to change)

- **Consent default:** `false` (privacy-by-default). Recommended.
- **Retention:** **90 days.** Longer (e.g. 180) adds longitudinal diagnostic value at trivial
  cost.
- **Signed-URL TTL:** 15 minutes.
