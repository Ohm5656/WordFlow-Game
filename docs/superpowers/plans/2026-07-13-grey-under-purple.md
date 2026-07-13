# Grey Under Purple — Draw-Order Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the grey coat from swallowing the purple smoke. Move `CloudOverlay` *below* `FogOverlay` so the purple draws last and stays the dominant read, with the grey showing as an outer fringe and bleeding through the purple's wisps.

**Architecture:** Pure draw-order change plus a material retune. The Canvas blends with `SrcAlpha OneMinusSrcAlpha`; whatever draws last covers what is beneath it in proportion to its own alpha. With grey on top at `_Density` 0.65, the purple only reaches the eye at `0.35 × 0.75 ≈ 0.26` — a 2.5:1 loss no tunable can undo. Flipping the order gives the grey an outer band that is *only* grey (visible, earning its place) while the purple core draws last and stays purple.

**Tech Stack:** Unity (built-in RP), UGUI ScreenSpaceOverlay Canvas (sibling index = draw order), Unity Editor `MenuItem` scripts, Unity MCP (`anklebreaker`).

Spec (updated): `docs/superpowers/specs/2026-07-13-cloud-fog-second-layer-design.md` — requirement 7 flipped, "Why the grey goes underneath" added.

## Context — what exists right now

Layer 2 already runs `WordFlow/EdgeFog` (layer 1's own shader) through `CloudFogMaterial.mat`, in grey. That part is correct and stays. The bug is that it sits **on top** of the purple.

Current sibling order under the Canvas: `background`(0) → `FogOverlay`(1) → `CloudOverlay`(2) → `Bear` → `book_craft*` → … → `time_root`.

Target: `background`(0) → **`CloudOverlay`(1)** → **`FogOverlay`(2)** → `Bear` → `book_craft*` → … → `time_root`.

The user has been hand-tuning `_MaxReach` / `_FogColor` in the Inspector, so **the working tree may hold uncommitted material edits — check `git status` first and keep them if present** (they are the user's, not stale state).

## Global Constraints

- **Do not modify** `EdgeFox.shader`, `EdgeFogMaterial.mat`, `FogController.cs`, `WordAssemblyTimer.cs`, or the `FogOverlay` GameObject's own components/values. Layer 1's *look* stays identical — only its sibling index shifts by one, as a side effect of `CloudOverlay` moving beneath it.
- Do not write or edit any shader. This is a draw-order + material-values change.
- Keep `_MaxReach + _Softness < 0.5` on **both** materials. Above that the front stops clearing the screen centre and the smoke degrades into a full-screen wash instead of an edge band. (This already bit us once.)
- Every scene edit happens atomically inside one editor script ending in `EditorSceneManager.SaveScene` — scene GameObjects edited via separate MCP calls vanish on domain reload. `Tools/Quest/Fix Fog Layer` already does this; use it rather than hand-editing the scene.
- `mcp__anklebreaker__unity_execute_code` does not work in this project. Drive Unity with `MenuItem` scripts + `unity_execute_menu_item`.
- Before any MCP call: `unity_list_instances`, then `unity_select_instance`. **The port hops between 7890 and 7891 on domain reload** — if calls time out, re-list and re-select. Pass `port:` on every call.
- **The Game view must be open or `unity_screenshot_game` silently captures a flat ~100KB image with no UI.** Run `unity_execute_menu_item("Window/General/Game")` once first, and sanity-check the PNG is multi-MB. Screenshots land ~60-75s after the call returns — poll for the file.
- The `[Telemetry] 404` / `[Session] open failed` ngrok warnings are pre-existing. Ignore them.
- Commit after each task.

---

### Task 1: Put the grey layer underneath the purple

**Files:**
- Modify: `Assets/Editor/AddCloudLayer.cs:84`
- Modify: `Assets/Editor/FixFogLayer.cs:47`
- Modify (via the editor tool, not by hand): `Assets/Scenes/region 1/CutScene_bear.unity`

**Interfaces:**
- Consumes: the existing `CloudOverlay` and `FogOverlay` GameObjects and the `Tools/Quest/Fix Fog Layer` menu item.
- Produces: sibling order `background` → `CloudOverlay` → `FogOverlay` → gameplay UI, saved into the scene. Both editor tools now agree on that order, so neither can silently undo it on a later run.

- [ ] **Step 0: Preserve the user's in-progress tuning**

```bash
git status --short
```

If `Assets/Scenes/region 1/CloudFogMaterial.mat` shows as modified, those are the user's Inspector edits. Do **not** revert them. Note the values (`git diff` on that file) — Task 2 builds on top of whatever is there.

- [ ] **Step 1: Flip the sibling index in `AddCloudLayer.cs`**

In `Assets/Editor/AddCloudLayer.cs`, replace:

```csharp
        // Directly above the purple smoke, still below book_craft* / time_root / stones — one
        // ScreenSpaceOverlay Canvas, so sibling index is the draw order.
        go.transform.SetSiblingIndex(fogT.GetSiblingIndex() + 1);
```

with:

```csharp
        // Directly BELOW the purple smoke (taking its index pushes it up one), still above the
        // background — one ScreenSpaceOverlay Canvas, so sibling index is the draw order.
        // Underneath, not on top: the Canvas blends SrcAlpha/OneMinusSrcAlpha, so a grey coat drawn
        // last at any workable density mathematically buries the purple (0.65 grey leaves the purple
        // 0.35 x 0.75 = 0.26 — a 2.5:1 loss). Below, the grey gets an outer band that is only grey,
        // and the purple draws last and stays the dominant read.
        go.transform.SetSiblingIndex(fogT.GetSiblingIndex());
```

- [ ] **Step 2: Flip the same index in `FixFogLayer.cs`**

In `Assets/Editor/FixFogLayer.cs`, replace:

```csharp
        // The grey cloud layer (Tools/Quest/Add Cloud Layer) rides directly above the purple smoke.
        // Re-anchor it here or every run of this tool would leave it stranded at its old index.
        Transform cloud = parent.Find("CloudOverlay");
        if (cloud != null)
        {
            cloud.SetSiblingIndex(fogT.GetSiblingIndex() + 1);
        }
```

with:

```csharp
        // The grey smoke coat (Tools/Quest/Add Cloud Layer) sits directly BELOW the purple front, so
        // the purple draws last and is not buried by it. Taking the fog's index pushes the fog up one.
        // Re-anchor it here or every run of this tool would leave it stranded at its old index.
        Transform cloud = parent.Find("CloudOverlay");
        if (cloud != null)
        {
            cloud.SetSiblingIndex(fogT.GetSiblingIndex());
        }
```

Also fix the class doc comment on line 7 — it says the CloudOverlay "rides above" the FogOverlay. It now rides below.

- [ ] **Step 3: Apply the new order to the scene**

```
mcp__anklebreaker__unity_list_instances
mcp__anklebreaker__unity_select_instance     port: <from the list>
mcp__anklebreaker__unity_console_clear       port: <port>
mcp__anklebreaker__unity_get_compilation_errors   severity: "all"   port: <port>
```

Expected: zero compilation errors. Then:

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fix Fog Layer"   port: <port>
mcp__anklebreaker__unity_console_log         port: <port>
```

Expected: `[FixFogLayer] DONE — ... scene saved`, no errors.

- [ ] **Step 4: Verify the order actually flipped**

```
mcp__anklebreaker__unity_scene_hierarchy   parentPath: "Canvas"   maxDepth: 1   port: <port>
```

Expected child order: `background`, **`CloudOverlay`**, **`FogOverlay`**, `Bear`, `book_craft`, `book_craft_pa`, `book_craft_ga`, … `time_root`.

If `FogOverlay` still comes before `CloudOverlay`, the flip did not take — re-check Step 2. If either overlay ends up above `book_craft*`, stop: that would cover the book.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/AddCloudLayer.cs Assets/Editor/FixFogLayer.cs "Assets/Scenes/region 1/CutScene_bear.unity"
git commit -m "fix(fog): draw the grey coat under the purple front, not over it

Alpha-over means the top layer buries what is beneath it in proportion to its
own alpha: grey at _Density 0.65 left the purple reaching the eye at only
0.35 x 0.75 = 0.26, a 2.5:1 loss no tunable could undo. Underneath, the grey
gets an outer band past the purple's reach where it is the only thing drawn,
and the purple draws last and stays the dominant read."
```

(Do **not** stage `CloudFogMaterial.mat` here if the user has uncommitted tuning in it — that lands in Task 2.)

---

### Task 2: Retune the grey now that it is underneath

**Files:**
- Modify: `Assets/Scenes/region 1/CloudFogMaterial.mat`

**Interfaces:**
- Consumes: Task 1's draw order.
- Produces: the shipped look. This task is the gate.

Underneath, the grey no longer has to be timid — its job is (a) to own the outer band past the purple's `_MaxReach`, and (b) to bleed through the purple's wisps. So it can carry more density than it could on top.

- [ ] **Step 1: Set the starting values**

In `Assets/Scenes/region 1/CloudFogMaterial.mat`, set these (leave every other key as it is — including any the user hand-tuned, unless it is listed here):

```yaml
    - _Density: 0.55
    - _MaxReach: 0.3
    - _Softness: 0.14
```

and

```yaml
    - _FogColor: {r: 0.72, g: 0.73, b: 0.77, a: 1}
```

Check the invariant: `_MaxReach + _Softness` = `0.30 + 0.14` = `0.44` < `0.5`. Good. Layer 1 sits at `0.23 + 0.15 = 0.38`, so the grey reaches `0.07` further than the purple — that gap **is** the grey-only outer band.

- [ ] **Step 2: Shoot the 60% preview**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Window/General/Game"        port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Assets/Refresh"             port: <port>
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 60%"  port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/GreyUnder_60.png"   port: <port>
```

Poll for the file (~60-75s, must be multi-MB), then read it.

**Expected — the pass/fail criterion:** the purple is **clearly purple** again — it is the dominant colour of the front, not a grey wash. A grey fringe is visible outside/around it, and grey shows through the purple's thin wisps. Compare against `Assets/Screenshots/GreyCoat_100_v5.png` (the swallowed version) if you want a direct before/after.

**Fail modes:**
- Purple still washed out → the flip did not take. Re-run Task 1 Step 4.
- Grey invisible → raise `_Density` (0.55 → 0.7) or widen the reach gap (`_MaxReach` 0.30 → 0.34, keeping `+_Softness < 0.5`).
- Grey band too wide / crowds the book → lower `_MaxReach` (0.30 → 0.26).

- [ ] **Step 3: Shoot 100%**

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview 100%"   port: <port>
mcp__anklebreaker__unity_screenshot_game     path: "Assets/Screenshots/GreyUnder_100.png"   port: <port>
```

Expected: both fronts at max reach, purple still dominant, centre clear, book / stones / timer readable.

- [ ] **Step 4: Tune if needed, then clear the preview**

Edit `CloudFogMaterial.mat` and re-shoot. Never edit a shader for these.

| Symptom | Knob |
|---|---|
| Purple still looks washed | lower grey `_Density` (0.55 → 0.4) |
| Grey barely there | raise `_Density` (0.55 → 0.7) |
| Want more grey visible as its own band | raise `_MaxReach` (0.30 → 0.34) — keep `+_Softness < 0.5` |
| Grey crowds the book | lower `_MaxReach` (0.30 → 0.26) |
| Grey reads too blue/cold | warm `_FogColor` toward `(0.76, 0.74, 0.71)` |
| Grey and purple move in lockstep | pull `_Speed` / `_BoilSpeed` further from layer 1's `0.05` / `0.16` |

```
mcp__anklebreaker__unity_execute_menu_item   menuPath: "Tools/Quest/Fog Preview Off"   port: <port>
```

Expected console: `[FogPreview] _Progress = 0`.

- [ ] **Step 5: Play-mode check**

```
mcp__anklebreaker__unity_play_mode   action: "play"   port: <port>
```

Confirm `isPlaying: true` via `unity_editor_state` (the first `play` after a domain reload sometimes does not take — call `play` again if it reports `false`). The bear intro runs ~60s before the word build; wait it out, screenshot, then:

```
mcp__anklebreaker__unity_console_log   type: "error"   port: <port>
mcp__anklebreaker__unity_play_mode     action: "stop"   port: <port>
```

Expected: both layers arrive together from the edges at 25s remaining, advance together, peak together at 0s. **The purple front is clearly purple throughout** — this is the whole point. Book, stones and `time_root` legible. No console errors, no Error Pause.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/region 1/CloudFogMaterial.mat"
git commit -m "feat(fog): retune the grey coat for its new spot under the purple"
```

---

## Done when

- Hierarchy under the Canvas reads `background`, `CloudOverlay`, `FogOverlay`, then the gameplay UI.
- In play mode the purple front is clearly purple — grey is a visible outer fringe and a tint in the wisps, not a coat of paint over the top.
- Both layers still arrive and peak together with the countdown; book, stones and timer stay readable.
- `_MaxReach + _Softness < 0.5` on the grey material.
- `git diff` touches only `AddCloudLayer.cs`, `FixFogLayer.cs`, `CloudFogMaterial.mat` and `CutScene_bear.unity` (sibling indices only). `EdgeFox.shader` / `EdgeFogMaterial.mat` / `FogController.cs` / `WordAssemblyTimer.cs` untouched.
