# Handoff — WordFlow in-app login (`feat/unity-login`)

This branch adds a real login to the Unity app. **The C# is done; the Unity scene/Canvas work is
not.** This doc is the starting point for finishing it.

## The model (decided with the team)

An **adult** (parent/teacher) registers once with **email + password** and enters the **child's name
+ birthdate** (and optionally a doctor code). The device is **personal to one child** and stays
**permanently logged in** — the child just opens the app and plays. Linking to a doctor is optional
and can be done anytime in settings. Auth uses **Firebase Auth REST** (no Firebase SDK); the Web API
key is embedded (it's public config, not a secret).

Registration is implicit on the backend: the first `GET /api/v1/bootstrap` auto-creates the
user + child. The same account also works on the **web dashboard** (the backend records the child in
`users/{uid}.childIds`), so no separate parent↔child linking.

## What's already done (this branch)

**Scripts** (`Assets/Scripts/Region1/Adventure/`):
- `Net/FirebaseAuthClient.cs` — Firebase REST: sign-up / sign-in / token refresh / password-reset.
- `Net/AuthSession.cs` — session singleton (`DontDestroyOnLoad`): register / login / auto-login /
  logout; persists the refresh token; auto-refresh; `GET /bootstrap` → `ChildId`; plus
  `SetChildProfile` (PUT `/me/child`) and `LinkByCode` (POST `/link-by-code`). Exposes
  `BearerHeader` and `ChildId`.
- `UI/AuthScreenController.cs` — login/register/auto-login screen logic (TMP).
- `UI/DoctorLinkController.cs` — in-game settings: doctor-code link + logout.
- Wired out the old hardcoded `"Bearer demo-token"` / `"kid_demo_01"` from `SessionContext`,
  `GradeApiClient`, `TelemetryClient`, `TtsApiClient`, `EncounterConfig`,
  `MagicStonePuzzleController` — they now read the real token + child id from `AuthSession`.

**Backend** (separate repo `Rurtt/WordFlow-Backend`, branch `feat/unity-login`): the
`POST /api/v1/link-by-code`, `PUT /api/v1/me/child`, and `childIds` changes this app calls. 127 tests
green. Check out that branch to run the gateway.

## What's left (editor — do this in Unity)

### 0. Open the project & let it compile
Open `NSC-Game` in Unity 6 (6000.4.3f1). It will generate `.meta` files for the new scripts —
**commit those generated `.meta` files** so GUIDs stay consistent for everyone. Confirm there are no
compile errors in the Console.

### 1. Add a persistent Auth object
Create an empty GameObject (e.g. `AuthBootstrap`) and add **both** `FirebaseAuthClient` and
`AuthSession` components.
- `AuthSession.auth` → drag the `FirebaseAuthClient` on the same object.
- `AuthSession.baseUrl` → defaults to `http://127.0.0.1:8000/api/v1` (change for device/cloud).
- `FirebaseAuthClient.apiKey` → already filled (project `wordflow-61d5a`).
`AuthSession` calls `DontDestroyOnLoad` on itself, so put it in the **Login** scene.

### 2. Build the Login scene (Thai)
New scene `Login` with a Canvas containing input fields + buttons, then add an
`AuthScreenController` and wire its slots:

| Field | Bind to |
|---|---|
| `emailField`, `passwordField` | TMP InputFields |
| `childNameField`, `birthDateField`, `doctorCodeField` | TMP InputFields (register-only; birthdate `YYYY-MM-DD`, code optional) |
| `statusText` | a TMP Text for errors/status |
| `busyIndicator` | optional spinner GameObject |
| `registerButton`, `loginButton`, `forgotPasswordButton` | UI Buttons |
| `nextSceneName` | the game scene to load after login (e.g. `WorldMap`) |

(The controller wires the button `onClick`s itself; you only set the references.)

### 3. Build the settings / doctor-link panel
Add `DoctorLinkController` to an in-game settings panel and wire: `codeField` (TMP InputField),
`statusText`, `linkButton`, `logoutButton`, and `authSceneName` = `Login`.

### 4. Build Settings
Add the `Login` scene at **index 0** in File → Build Settings, with the existing game scenes after it.

## Run it end-to-end (verification)

1. Backend: check out `Rurtt/WordFlow-Backend` `feat/unity-login`, then
   `cd gateway && uvicorn app.main:app --reload --port 8000` with `AUTH_IMPL=firebase` and
   `DATABASE_IMPL=firebase` in `gateway/.env` (needs `serviceAccount.json`).
2. Press Play in the Login scene → register a new email + child name + birthdate → should land in the
   game.
3. Web dashboard → the child should appear **by name**.
4. Settings → enter a real doctor code `WF-XXXX` → child appears under that doctor.
5. Stop & Play again → should **auto-login** (no typing).
6. Confirm `/grade` and session/telemetry calls carry the real bearer token + real `childId`
   (gateway logs).

## Notes / gotchas
- The gateway runs on `localhost:8000`; a **physical device** can't reach it until it's cloud-deployed
  or tunneled. Editor + localhost proves the flow.
- The refresh token is stored in `PlayerPrefs` (plaintext) — fine for the demo; harden later.
- The old serialized `authorization` / `childId` fields still exist but default to empty — they're
  optional offline overrides; leave them blank in real scenes.
- Full design spec lives in the backend repo:
  `docs/superpowers/specs/2026-06-28-unity-login-design.md`.
