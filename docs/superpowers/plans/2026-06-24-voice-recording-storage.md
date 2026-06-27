# Voice-Recording Storage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a child's graded word-attempt audio to cloud object storage, consent-gated, and let authorized adults play it back via signed URLs — without overwhelming the gateway.

**Architecture:** A swappable `IRecordingStore` seam (mirrors the existing `recognizer_impl`/`database_impl`/`auth_impl` pattern) with `LocalDiskStore` (dev/test) and `GcsStore` (proxy write + signed-URL read). `/grade` reuses the audio bytes it already receives: it always grades, and when the child has `recordingConsent == true`, best-effort stores the bytes and records the object ref in the attempt's `audioUrl`. A new `recordings` router serves listing, signed playback URLs (with an access-audit row), and erasure. Reads stream direct from GCS, never through the gateway.

**Tech Stack:** Python 3.11+, FastAPI, pydantic v2, pytest. `google-cloud-storage` for the GCS impl only.

**Repo:** All paths are in `D:\Gimme\wordflow-backend`, relative to repo root.

## Global Constraints

- Match the existing seam pattern: `Literal` field in `Settings`, a `build_*` factory, injection via `app.state` in `create_app`. (`gateway/app/config.py`, `gateway/app/main.py`)
- `/grade` MUST always grade and return — storage is **best-effort and never fails or blocks the grade** (wrap in try/except, log on failure). Gameplay is consent-independent.
- **Privacy-by-default:** no audio is stored unless `child.recordingConsent is True`.
- `audioUrl` on the attempt record holds the **object ref** (e.g. `gs://bucket/...` or a local path), never a public URL. Signed URLs are minted on demand only.
- Child-scoped endpoints reuse `require_kid_access(db, kid_id, uid)` (returns the child doc).
- New deps (`google-cloud-storage`) are imported lazily inside `GcsStore` so the test suite and `local` impl never require them.
- Tests use the existing fixtures in `gateway/tests/conftest.py` (`app`, `client`, `auth_headers`) and the `seeded_client` pattern from `test_grade.py`.
- Run tests from `gateway/`: `cd gateway && python -m pytest`.

---

## File Structure

- Create `gateway/app/storage/__init__.py` — package marker.
- Create `gateway/app/storage/base.py` — `IRecordingStore` Protocol + `RecordingRef` type.
- Create `gateway/app/storage/local.py` — `LocalDiskStore`.
- Create `gateway/app/storage/gcs.py` — `GcsStore` (lazy `google-cloud-storage`).
- Create `gateway/app/storage/factory.py` — `build_recording_store(settings)`.
- Modify `gateway/app/config.py` — recording settings.
- Modify `gateway/app/repositories.py` — `RecordingsRepo` (audit), `AttemptsRepo.get`/`clear_audio_for_kid`.
- Modify `gateway/app/services.py` — store hook in `grade_attempt`; `mint_playback_url`, `erase_recordings`.
- Modify `gateway/app/models.py` — `RecordingItem`, `RecordingsListResponse`, `PlaybackUrlResponse`.
- Create `gateway/app/routers/recordings.py` — list / playback-url / erasure endpoints.
- Modify `gateway/app/routers/grade.py` — read consent, pass store into `grade_attempt`.
- Modify `gateway/app/main.py` — build + inject store; include `recordings` router.
- Create `gateway/tests/test_storage_local.py`, `gateway/tests/test_recordings.py`; extend `gateway/tests/test_grade.py`.

---

## Task 1: Storage seam + LocalDiskStore

**Files:**
- Create: `gateway/app/storage/__init__.py`, `gateway/app/storage/base.py`, `gateway/app/storage/local.py`
- Test: `gateway/tests/test_storage_local.py`

**Interfaces:**
- Produces:
  - `IRecordingStore` (Protocol): `store(child_id: str, attempt_id: str, wav_bytes: bytes) -> str` (returns object ref); `playback_url(object_ref: str, ttl_seconds: int) -> str`; `delete(object_ref: str) -> None`; `delete_for_child(child_id: str) -> None`.
  - `LocalDiskStore(root: str)` implementing it (object ref = absolute file path; `playback_url` returns `file://` + path).

- [ ] **Step 1: Write the failing test**

```python
# gateway/tests/test_storage_local.py
import os
from app.storage.local import LocalDiskStore


def test_store_then_playback_and_delete(tmp_path):
    store = LocalDiskStore(str(tmp_path))
    ref = store.store("kid1", "att_abc", b"\x00\x01\x02")
    assert os.path.isfile(ref)
    assert store.playback_url(ref, 900).startswith("file://")
    store.delete(ref)
    assert not os.path.exists(ref)


def test_delete_for_child_removes_all(tmp_path):
    store = LocalDiskStore(str(tmp_path))
    store.store("kid1", "att_a", b"a")
    store.store("kid1", "att_b", b"b")
    store.store("kid2", "att_c", b"c")
    store.delete_for_child("kid1")
    assert store.playback_url  # sanity
    remaining = [p for _, _, fs in os.walk(str(tmp_path)) for p in fs]
    assert remaining == ["att_c.wav"]
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd gateway && python -m pytest tests/test_storage_local.py -v`
Expected: FAIL (`ModuleNotFoundError: app.storage.local`).

- [ ] **Step 3: Write minimal implementation**

```python
# gateway/app/storage/__init__.py
```

```python
# gateway/app/storage/base.py
from __future__ import annotations

from typing import Protocol, runtime_checkable


@runtime_checkable
class IRecordingStore(Protocol):
    def store(self, child_id: str, attempt_id: str, wav_bytes: bytes) -> str:
        """Persist a WAV clip; return an opaque object ref stored on the attempt."""
        ...

    def playback_url(self, object_ref: str, ttl_seconds: int) -> str:
        """Return a short-lived URL the dashboard can read directly."""
        ...

    def delete(self, object_ref: str) -> None: ...

    def delete_for_child(self, child_id: str) -> None: ...
```

```python
# gateway/app/storage/local.py
from __future__ import annotations

import os
import shutil


class LocalDiskStore:
    """Dev/test recording store. Object ref is the absolute file path.
    Layout: <root>/<child_id>/<attempt_id>.wav"""

    def __init__(self, root: str):
        self._root = root

    def _child_dir(self, child_id: str) -> str:
        return os.path.join(self._root, child_id)

    def store(self, child_id: str, attempt_id: str, wav_bytes: bytes) -> str:
        os.makedirs(self._child_dir(child_id), exist_ok=True)
        path = os.path.join(self._child_dir(child_id), f"{attempt_id}.wav")
        with open(path, "wb") as f:
            f.write(wav_bytes)
        return path

    def playback_url(self, object_ref: str, ttl_seconds: int) -> str:
        return "file://" + object_ref

    def delete(self, object_ref: str) -> None:
        if os.path.exists(object_ref):
            os.remove(object_ref)

    def delete_for_child(self, child_id: str) -> None:
        d = self._child_dir(child_id)
        if os.path.isdir(d):
            shutil.rmtree(d)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd gateway && python -m pytest tests/test_storage_local.py -v`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add gateway/app/storage gateway/tests/test_storage_local.py
git commit -m "feat(storage): IRecordingStore seam + LocalDiskStore"
```

---

## Task 2: GcsStore + config + factory

**Files:**
- Create: `gateway/app/storage/gcs.py`, `gateway/app/storage/factory.py`
- Modify: `gateway/app/config.py`
- Test: extend `gateway/tests/test_config.py` (factory selection)

**Interfaces:**
- Consumes: `IRecordingStore` (Task 1).
- Produces: `build_recording_store(settings) -> IRecordingStore`; `GcsStore(bucket: str)`; `Settings` fields `recording_store_impl`, `recordings_bucket`, `recordings_local_dir`, `signed_url_ttl_seconds`, `recording_retention_days`.

- [ ] **Step 1: Write the failing test**

```python
# append to gateway/tests/test_config.py
from app.config import Settings
from app.storage.factory import build_recording_store
from app.storage.local import LocalDiskStore


def test_factory_builds_local_store(tmp_path):
    s = Settings(_env_file=None, recording_store_impl="local", recordings_local_dir=str(tmp_path))
    assert isinstance(build_recording_store(s), LocalDiskStore)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd gateway && python -m pytest tests/test_config.py::test_factory_builds_local_store -v`
Expected: FAIL (`recording_store_impl` not a field / module missing).

- [ ] **Step 3: Write minimal implementation**

Add to `gateway/app/config.py` `Settings` (after the TTS block, before `def get_settings`):

```python
    # Recording storage (voice clips for clinician/parent playback)
    recording_store_impl: Literal["local", "gcs"] = "local"
    recordings_bucket: str = ""              # GCS bucket name (gcs impl)
    recordings_local_dir: str = "recordings_cache"  # local impl root
    signed_url_ttl_seconds: int = 900        # 15 min playback URLs
    recording_retention_days: int = 90       # informational; enforced by a GCS lifecycle rule
```

```python
# gateway/app/storage/gcs.py
from __future__ import annotations

from datetime import timedelta


class GcsStore:
    """Proxy-write + signed-URL-read store. google-cloud-storage is imported lazily so the
    test suite and the local impl never need it."""

    def __init__(self, bucket: str):
        from google.cloud import storage  # lazy
        self._client = storage.Client()
        self._bucket = self._client.bucket(bucket)
        self._bucket_name = bucket

    def _blob_name(self, child_id: str, attempt_id: str) -> str:
        return f"children/{child_id}/{attempt_id}.wav"

    def store(self, child_id: str, attempt_id: str, wav_bytes: bytes) -> str:
        name = self._blob_name(child_id, attempt_id)
        self._bucket.blob(name).upload_from_string(wav_bytes, content_type="audio/wav")
        return f"gs://{self._bucket_name}/{name}"

    def playback_url(self, object_ref: str, ttl_seconds: int) -> str:
        name = object_ref.split(f"gs://{self._bucket_name}/", 1)[-1]
        return self._bucket.blob(name).generate_signed_url(
            expiration=timedelta(seconds=ttl_seconds), method="GET"
        )

    def delete(self, object_ref: str) -> None:
        name = object_ref.split(f"gs://{self._bucket_name}/", 1)[-1]
        self._bucket.blob(name).delete()

    def delete_for_child(self, child_id: str) -> None:
        prefix = f"children/{child_id}/"
        for blob in self._client.list_blobs(self._bucket_name, prefix=prefix):
            blob.delete()
```

```python
# gateway/app/storage/factory.py
from __future__ import annotations

from app.config import Settings
from app.storage.base import IRecordingStore
from app.storage.local import LocalDiskStore


def build_recording_store(settings: Settings) -> IRecordingStore:
    impl = settings.recording_store_impl
    if impl == "local":
        return LocalDiskStore(settings.recordings_local_dir)
    if impl == "gcs":
        from app.storage.gcs import GcsStore  # lazy: avoids google-cloud-storage in tests
        return GcsStore(settings.recordings_bucket)
    raise ValueError(f"unknown recording_store_impl: {impl}")
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd gateway && python -m pytest tests/test_config.py -v`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add gateway/app/storage/gcs.py gateway/app/storage/factory.py gateway/app/config.py gateway/tests/test_config.py
git commit -m "feat(storage): GcsStore + recording_store factory + settings"
```

---

## Task 3: Consent-gated storage hook in grade flow

**Files:**
- Modify: `gateway/app/repositories.py` (`AttemptsRepo.get`, `clear_audio_for_kid`)
- Modify: `gateway/app/services.py` (`grade_attempt` stores audio when consent)
- Modify: `gateway/app/routers/grade.py` (read consent, pass store)
- Modify: `gateway/app/main.py` (inject `app.state.recording_store`)
- Test: extend `gateway/tests/test_grade.py`

**Interfaces:**
- Consumes: `IRecordingStore` (Task 1), `build_recording_store` (Task 2).
- Produces: `grade_attempt(..., store: IRecordingStore | None = None, consent: bool = False)`; `AttemptsRepo.get(attempt_id) -> Optional[dict]`; `AttemptsRepo.clear_audio_for_kid(kid_id) -> None`.

- [ ] **Step 1: Write the failing test**

```python
# append to gateway/tests/test_grade.py
def test_grade_stores_audio_when_consent(seeded_client, auth_headers):
    seeded_client.app.state.db.set_doc(
        "children", "kid1", {"parentId": "uid_demo", "name": "Demo", "recordingConsent": True}
    )
    files = {"audio": ("rec.wav", b"\x00\x01\x02", "audio/wav")}
    data = {"targetWordId": "kaa", "childId": "kid1"}
    r = seeded_client.post("/api/v1/grade", files=files, data=data, headers=auth_headers)
    attempt_id = r.json()["attemptId"]
    stored = seeded_client.app.state.db.get_doc("attempts", attempt_id)
    assert stored["audioUrl"] is not None


def test_grade_skips_audio_without_consent(seeded_client, auth_headers):
    # default seeded child has no recordingConsent -> treated as False
    files = {"audio": ("rec.wav", b"\x00\x01\x02", "audio/wav")}
    data = {"targetWordId": "kaa", "childId": "kid1"}
    r = seeded_client.post("/api/v1/grade", files=files, data=data, headers=auth_headers)
    stored = seeded_client.app.state.db.get_doc("attempts", r.json()["attemptId"])
    assert stored["audioUrl"] is None


def test_grade_succeeds_even_if_store_raises(seeded_client, auth_headers):
    seeded_client.app.state.db.set_doc(
        "children", "kid1", {"parentId": "uid_demo", "name": "Demo", "recordingConsent": True}
    )

    class _BoomStore:
        def store(self, *a): raise RuntimeError("bucket down")
        def playback_url(self, *a): return ""
        def delete(self, *a): pass
        def delete_for_child(self, *a): pass

    seeded_client.app.state.recording_store = _BoomStore()
    files = {"audio": ("rec.wav", b"\x00\x01\x02", "audio/wav")}
    data = {"targetWordId": "kaa", "childId": "kid1"}
    r = seeded_client.post("/api/v1/grade", files=files, data=data, headers=auth_headers)
    assert r.status_code == 200  # grade never fails because storage did
    stored = seeded_client.app.state.db.get_doc("attempts", r.json()["attemptId"])
    assert stored["audioUrl"] is None
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd gateway && python -m pytest tests/test_grade.py -k "consent or store_raises" -v`
Expected: FAIL (audio never stored; `recording_store` not on app.state).

- [ ] **Step 3: Write minimal implementation**

In `gateway/app/repositories.py` `AttemptsRepo`, add:

```python
    def get(self, attempt_id: str) -> Optional[dict]:
        doc = self.db.get_doc("attempts", attempt_id)
        return {"id": attempt_id, **doc} if doc else None

    def clear_audio_for_kid(self, kid_id: str) -> None:
        for row in self.list_for_kid(kid_id):
            if row.get("audioUrl") is not None:
                doc = {k: v for k, v in row.items() if k != "id"}
                doc["audioUrl"] = None
                self.db.set_doc("attempts", row["id"], doc)
```

In `gateway/app/services.py`, change `grade_attempt`'s signature to add params (keep all existing params) and replace the `"audioUrl": None` write. Add to the parameter list:

```python
    store=None,            # IRecordingStore | None
    consent: bool = False,
```

Replace the attempt write block (`services.py:70-79`) so the ref is computed first:

```python
    attempt_id = "att_" + uuid.uuid4().hex[:12]
    created_at = _now()
    audio_ref = None
    if consent and store is not None and audio:
        try:
            audio_ref = store.store(kid_id, attempt_id, audio)
        except Exception:  # storage is best-effort: never break grading
            logging.getLogger(__name__).warning("recording store failed for %s", attempt_id, exc_info=True)
    attempts.add(attempt_id, {
        "kidId": kid_id, "wordId": target_word_id, "questId": quest_id,
        "region": word.get("difficulty", 1),
        "par": par, "grade": grade,
        "predictedIpa": predicted_ipa_out, "expectedIpa": expected_ipa,
        "perPhoneme": per_phoneme, "degraded": degraded,
        "latencyMs": latency_ms, "createdAt": created_at, "audioUrl": audio_ref,
    })
```

Ensure `import logging` exists at the top of `services.py` (add if missing).

In `gateway/app/routers/grade.py`, capture the child doc from `require_kid_access` and pass store + consent:

```python
    kid = require_kid_access(db, childId, uid)
    audio_bytes = await audio.read()
    try:
        result = grade_attempt(
            words=WordsRepo(db),
            attempts=AttemptsRepo(db),
            recognizer=request.app.state.recognizer,
            kid_id=childId,
            target_word_id=targetWordId,
            quest_id=questId,
            audio=audio_bytes,
            b_min=settings.grade_b_min,
            a_min=settings.grade_a_min,
            sessions=SessionsRepo(db) if sessionId else None,
            session_id=sessionId,
            store=request.app.state.recording_store,
            consent=bool(kid.get("recordingConsent", False)),
        )
```

In `gateway/app/main.py`, after `app.state.recognizer = ...`:

```python
    from app.storage.factory import build_recording_store
    app.state.recording_store = build_recording_store(settings)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd gateway && python -m pytest tests/test_grade.py -v`
Expected: PASS (all prior grade tests + 3 new).

- [ ] **Step 5: Commit**

```bash
git add gateway/app/services.py gateway/app/repositories.py gateway/app/routers/grade.py gateway/app/main.py gateway/tests/test_grade.py
git commit -m "feat(storage): consent-gated audio persistence in grade flow"
```

---

## Task 4: Recordings router — list, playback URL (audited), erasure

**Files:**
- Modify: `gateway/app/repositories.py` (`RecordingsRepo`)
- Modify: `gateway/app/models.py` (response models)
- Modify: `gateway/app/services.py` (`mint_playback_url`, `erase_recordings`)
- Create: `gateway/app/routers/recordings.py`
- Modify: `gateway/app/main.py` (include router)
- Test: `gateway/tests/test_recordings.py`

**Interfaces:**
- Consumes: `AttemptsRepo` (Tasks 3), `IRecordingStore`, `require_kid_access`.
- Produces:
  - `RecordingsRepo.add_access(child_id, accessor_uid, attempt_id) -> None`; `RecordingsRepo.list_access(child_id) -> list[dict]`.
  - `mint_playback_url(attempts, store, recordings, kid_id, attempt_id, accessor_uid, ttl) -> str`.
  - `erase_recordings(attempts, store, kid_id) -> None`.
  - Endpoints: `GET /api/v1/children/{kid_id}/recordings`, `GET /api/v1/children/{kid_id}/recordings/{attempt_id}/playback-url`, `DELETE /api/v1/children/{kid_id}/recordings`.

- [ ] **Step 1: Write the failing test**

```python
# gateway/tests/test_recordings.py
import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def seeded(app):
    app.state.db.set_doc("words", "kaa", {"thai": "กา", "stones": ["ก", "า"], "ipa": "kaː", "meaning": "crow", "difficulty": 1})
    app.state.db.set_doc("children", "kid1", {"parentId": "uid_demo", "name": "Demo", "recordingConsent": True})
    return TestClient(app)


def _grade_once(client, headers):
    files = {"audio": ("rec.wav", b"\x00\x01\x02", "audio/wav")}
    data = {"targetWordId": "kaa", "childId": "kid1"}
    return client.post("/api/v1/grade", files=files, data=data, headers=headers).json()["attemptId"]


def test_list_recordings_shows_stored_attempt(seeded, auth_headers):
    _grade_once(seeded, auth_headers)
    r = seeded.get("/api/v1/children/kid1/recordings", headers=auth_headers)
    assert r.status_code == 200
    items = r.json()["recordings"]
    assert len(items) == 1
    assert items[0]["wordId"] == "kaa"
    assert items[0]["hasAudio"] is True


def test_playback_url_minted_and_audited(seeded, auth_headers):
    attempt_id = _grade_once(seeded, auth_headers)
    r = seeded.get(f"/api/v1/children/kid1/recordings/{attempt_id}/playback-url", headers=auth_headers)
    assert r.status_code == 200
    assert r.json()["url"].startswith("file://")
    audit = seeded.app.state.db.list_collection("children/kid1/recordingAccess")
    assert len(audit) == 1
    assert audit[0]["attemptId"] == attempt_id
    assert audit[0]["accessorUid"] == "uid_demo"


def test_erase_removes_audio_refs(seeded, auth_headers):
    attempt_id = _grade_once(seeded, auth_headers)
    r = seeded.delete("/api/v1/children/kid1/recordings", headers=auth_headers)
    assert r.status_code == 200
    stored = seeded.app.state.db.get_doc("attempts", attempt_id)
    assert stored["audioUrl"] is None


def test_recordings_forbidden_for_non_owner(seeded, auth_headers):
    seeded.app.state.db.set_doc("children", "kid_other", {"parentId": "someone_else"})
    r = seeded.get("/api/v1/children/kid_other/recordings", headers=auth_headers)
    assert r.status_code == 403
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd gateway && python -m pytest tests/test_recordings.py -v`
Expected: FAIL (router not mounted → 404).

- [ ] **Step 3: Write minimal implementation**

In `gateway/app/repositories.py`, add:

```python
class RecordingsRepo:
    """Append-only access audit for recording playback (PDPA accountability)."""

    def __init__(self, db: Database):
        self.db = db

    def _col(self, child_id: str) -> str:
        return f"children/{child_id}/recordingAccess"

    def add_access(self, child_id: str, accessor_uid: str, attempt_id: str) -> None:
        import uuid
        access_id = "acc_" + uuid.uuid4().hex[:12]
        self.db.set_doc(self._col(child_id), access_id, {
            "accessorUid": accessor_uid, "attemptId": attempt_id, "ts": _now(),
        })

    def list_access(self, child_id: str) -> list[dict]:
        return self.db.list_collection(self._col(child_id))
```

In `gateway/app/models.py`, add:

```python
class RecordingItem(BaseModel):
    attemptId: str
    wordId: str
    grade: str = ""
    createdAt: str = ""
    hasAudio: bool = False


class RecordingsListResponse(BaseModel):
    recordings: list[RecordingItem]


class PlaybackUrlResponse(BaseModel):
    url: str
    expiresInSeconds: int
```

In `gateway/app/services.py`, add:

```python
def mint_playback_url(attempts, store, recordings, kid_id, attempt_id, accessor_uid, ttl):
    attempt = attempts.get(attempt_id)
    if attempt is None or attempt.get("kidId") != kid_id or not attempt.get("audioUrl"):
        raise KeyError(attempt_id)
    url = store.playback_url(attempt["audioUrl"], ttl)
    recordings.add_access(kid_id, accessor_uid, attempt_id)
    return url


def erase_recordings(attempts, store, kid_id) -> None:
    try:
        store.delete_for_child(kid_id)
    except Exception:
        logging.getLogger(__name__).warning("store erase failed for %s", kid_id, exc_info=True)
    attempts.clear_audio_for_kid(kid_id)
```

```python
# gateway/app/routers/recordings.py
from fastapi import APIRouter, Depends, HTTPException, Request

from app.auth import get_current_uid, require_kid_access
from app.models import PlaybackUrlResponse, RecordingItem, RecordingsListResponse
from app.repositories import AttemptsRepo, RecordingsRepo
from app.services import erase_recordings, mint_playback_url

router = APIRouter(prefix="/api/v1")


@router.get("/children/{kid_id}/recordings", response_model=RecordingsListResponse)
def list_recordings(kid_id: str, request: Request, uid: str = Depends(get_current_uid)):
    db = request.app.state.db
    require_kid_access(db, kid_id, uid)
    rows = AttemptsRepo(db).list_for_kid(kid_id)
    items = [
        RecordingItem(
            attemptId=r["id"], wordId=r.get("wordId", ""), grade=r.get("grade", ""),
            createdAt=r.get("createdAt", ""), hasAudio=r.get("audioUrl") is not None,
        )
        for r in rows if r.get("audioUrl") is not None
    ]
    return RecordingsListResponse(recordings=items)


@router.get("/children/{kid_id}/recordings/{attempt_id}/playback-url", response_model=PlaybackUrlResponse)
def get_playback_url(kid_id: str, attempt_id: str, request: Request, uid: str = Depends(get_current_uid)):
    db = request.app.state.db
    require_kid_access(db, kid_id, uid)
    settings = request.app.state.settings
    try:
        url = mint_playback_url(
            AttemptsRepo(db), request.app.state.recording_store, RecordingsRepo(db),
            kid_id, attempt_id, uid, settings.signed_url_ttl_seconds,
        )
    except KeyError:
        raise HTTPException(status_code=404, detail={"error": {"code": "recording_not_found", "message": "No audio for this attempt"}})
    return PlaybackUrlResponse(url=url, expiresInSeconds=settings.signed_url_ttl_seconds)


@router.delete("/children/{kid_id}/recordings")
def erase(kid_id: str, request: Request, uid: str = Depends(get_current_uid)):
    db = request.app.state.db
    require_kid_access(db, kid_id, uid)
    erase_recordings(AttemptsRepo(db), request.app.state.recording_store, kid_id)
    return {"status": "erased"}
```

In `gateway/app/main.py`: add `recordings` to the routers import line and `app.include_router(recordings.router)` with the others.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd gateway && python -m pytest tests/test_recordings.py -v`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add gateway/app/routers/recordings.py gateway/app/repositories.py gateway/app/models.py gateway/app/services.py gateway/app/main.py gateway/tests/test_recordings.py
git commit -m "feat(storage): recordings list/playback-url(audited)/erasure endpoints"
```

---

## Task 5: Full-suite regression + docs

**Files:**
- Modify: `gateway/.env.example` (document new settings) if present, else skip.

- [ ] **Step 1: Run the whole suite**

Run: `cd gateway && python -m pytest -q`
Expected: PASS (all prior tests + new storage/recordings/grade tests).

- [ ] **Step 2: Document the GCS lifecycle retention rule (operational, not code)**

Add a short note to `gateway/README.md` (or create `gateway/docs/recordings.md`): the bucket needs a lifecycle rule deleting objects older than `recording_retention_days`; signed-URL reads require the runtime service account to have `roles/storage.objectViewer` + `iam.serviceAccountTokenCreator` (for `generate_signed_url`).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs(storage): recordings retention + IAM operational notes"
```

---

## Self-Review

**Spec coverage:**
- Storage tier separation (GCS) → Task 2 `GcsStore`. ✓
- Read path direct-from-GCS via signed URL → Task 4 `playback-url`. ✓
- Proxy write at `/grade`, never blocks/fails grade → Task 3 (`try/except`, `test_grade_succeeds_even_if_store_raises`). ✓
- Swappable seam matching `*_impl` pattern → Tasks 1–2 (`recording_store_impl`, factory, `app.state`). ✓
- `audioUrl` holds object ref, signed URLs minted on demand → Task 3 (write) + Task 4 (mint). ✓
- Consent gate, privacy-by-default → Task 3 (`consent=bool(kid.get("recordingConsent", False))`, default False). ✓
- Audit on playback → Task 4 `RecordingsRepo.add_access` + `test_playback_url_minted_and_audited`. ✓
- Retention (lifecycle rule) + erasure endpoint → Task 5 note + Task 4 `DELETE`. ✓
- Access control via `require_kid_access` → Tasks 3–4 (`test_recordings_forbidden_for_non_owner`). ✓
- `google-cloud-storage` lazy so tests/local don't need it → Tasks 2 (factory + GcsStore lazy import). ✓

**Placeholder scan:** none — every step has concrete code/commands.

**Type consistency:** `store(child_id, attempt_id, wav_bytes) -> str` used identically in Tasks 1/3/4; `audioUrl` holds the ref everywhere; `mint_playback_url`/`erase_recordings` signatures match their router call sites.

**Mobile/deployment note (spec §Mobile):** no backend code task — it's Unity client config (reachable HTTPS gateway URL). Recorded in the spec; surfaced to the user separately. No plan task required.
