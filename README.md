# WordFlow

WordFlow is a 2D educational adventure game built with Unity. It combines story-driven exploration, animated cutscenes, Thai word-building activities, microphone recording, and backend-assisted pronunciation grading in a child-friendly experience.

This repository contains the Unity frontend. The backend service is maintained separately.

## Current Content

- World map and Region 1 forest exploration
- Bear and crow quest storylines
- Frame-based character and owl animations
- Data-driven Thai word-building encounters
- Microphone capture and WAV encoding
- Pronunciation grading through the `/grade` API
- Backend text-to-speech playback through the `/tts` API
- Firebase-based account authentication and session restoration
- Quest, session, and telemetry integration
- Persistent music and sound effects across scenes
- 16:9 presentation with letterboxing on other display ratios

## Requirements

- Unity `6000.4.3f1`
- Git
- Git LFS
- A microphone for pronunciation activities
- Access to a compatible WordFlow backend for online features

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/Ohm5656/WordFlow-Game.git
   cd WordFlow-Game
   ```

2. Download the Git LFS assets:

   ```bash
   git lfs install
   git lfs pull
   ```

3. Add the project to Unity Hub and open it with Unity `6000.4.3f1`.

4. Allow Unity to import the assets and compile the scripts.

5. Open `Assets/Scenes/Login.unity` and enter Play Mode.

The normal player flow starts at the login scene, continues to the world map, and then enters the Region 1 quest sequence.

## Backend Configuration

All Unity network clients use one shared backend address:

`Assets/Scripts/Region1/Adventure/Net/BackendConfig.cs`

Set `BackendConfig.BaseUrl` to the API gateway before running online features. The value must include `/api/v1` and must not end with a slash.

Example for local development:

```csharp
public const string BaseUrl = "http://127.0.0.1:8000/api/v1";
```

The current implementation also supports an ngrok endpoint. `BackendConfig.Prepare` adds the ngrok browser-warning bypass header to each request.

The frontend expects services for authentication bootstrap, child profiles, grading, text-to-speech, sessions, and telemetry. Authenticated requests use the Firebase ID token managed by `AuthSession`.

## Main Scene Flow

```text
Login
  -> WorldMap
  -> reference_forest
  -> CutScene_bear
  -> word-building and result scenes
  -> reference_forest
  -> CutScene_ga
  -> crow result scenes
```

Scene transitions are driven by quest progress and `PlayerPrefs` flags. The active build scene list is stored in `ProjectSettings/EditorBuildSettings.asset`.

## Project Structure

```text
Assets/
  Art/                         Character, environment, and quest artwork
  Audio/                       Music, voice, and sound effects
  Data/Adventure/              Word, stone, encounter, and cutscene assets
  Editor/                      Unity Editor builders and maintenance tools
  Resources/                   Runtime-loaded shared assets
  Scenes/                      Login, world map, Region 1, and result scenes
  Scripts/Common/              Shared audio, fades, movement, and utilities
  Scripts/WorldMap/            World map progression and presentation
  Scripts/Region1/Cutscenes/   Story and puzzle cutscene controllers
  Scripts/Region1/Adventure/   Word-building gameplay and backend clients
  Scripts/Region1/Success/     Correct and incorrect result sequences
  Tests/Adventure/             Edit Mode unit tests
```

Additional implementation notes are available in `Assets/Scripts/README.md` and `PROJECT_STRUCTURE_DIAGRAM.md`.

## Tests

The Edit Mode test suite covers the data-driven gameplay core, including:

- Encounter state management
- Outcome evaluation
- Pronunciation target resolution
- WAV encoding
- Telemetry queue behavior
- Word database validation
- Build latency tracking

Run the tests in Unity from `Window > General > Test Runner`, select Edit Mode, and choose `Run All`.

## Building

1. Open `File > Build Profiles` in Unity.
2. Select the required platform profile.
3. Confirm that all required scenes are enabled.
4. Configure microphone permissions for the target platform.
5. Build and test with the backend address intended for that environment.

Windows is the current desktop build profile. Android support is also configured in the project settings.

## Notes for Contributors

- Commit Unity `.meta` files together with their assets.
- Keep large images, audio, video, and binary assets in Git LFS.
- Do not commit `Library`, `Temp`, `Logs`, local recovery scenes, or test screenshots.
- Keep backend URLs centralized in `BackendConfig`.
- Preserve existing scene and prefab GUIDs when moving assets.
- Run the Edit Mode tests after changing gameplay core or network request logic.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
