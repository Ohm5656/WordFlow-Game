# Character 4-Direction Run Animation Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the reference_forest quest-map character's run animation look consistent across all 4 directions (up/down/left/right) — matching leg cadence and matching on-screen character height — instead of `left`/`up` visibly running faster and `up` looking shorter than `front`/`right`.

**Architecture:** This is a runtime tuning fix, not an art re-crop. The 4-direction sprite frame sets already differ in frame count and per-frame character size (confirmed by measurement below); `CharacterRunDirection.cs` already normalizes leg tempo via a per-direction speed multiplier and already has per-direction inspector fields — the bug is that those multipliers were only tuned for `front`/`right` (0.6) and left at the default (1) for `up`/`left`, and there is currently no per-direction scale compensation at all. Fix is: (1) a one-line data correction to existing scene field values, (2) a new per-direction scale multiplier field in the same script, applied the same way the existing speed multiplier is.

**Tech Stack:** Unity 2D (sprite animation clips + Animator), C# MonoBehaviour (`CharacterRunDirection.cs`), scene-embedded component data (`reference_forest.unity`).

## Global Constraints

- Do not touch the source PNG frame folders (`Assets/Art/quest_map/character/character_run_*_cropped/`) or the `.anim` clips — they are generated art assets from a pipeline whose raw (pre-crop) sources no longer exist in the repo (per project memory `character-run-anim-pipeline`); re-cropping is out of scope.
- Do not change `unitsPerStep` or `stepsPerLoop` — those are independently correct (see Investigation Findings). Only the per-direction multiplier fields are wrong/missing.
- All verification in this plan is visual (Unity Play Mode), not automated tests — there is no existing test harness for scene-driven animation tuning in this project. Each task's "test" step is an explicit visual check with a pass/fail criterion.
- Follow project memory `unity-mcp-scene-edits-vanish`: any GameObject/component edit made live in the Unity Editor must be followed by an explicit Save Scene before ending a session, or it is lost on domain reload.

---

## Investigation Findings (context — do not re-derive, just use)

Measured directly from the shipped assets (frame counts via `ls`, dimensions/bbox via a Python+PIL+scipy script, confirmed visually with a side-by-side composite):

**Frame counts (`Assets/Art/quest_map/character/character_run_*_cropped/`, all @ 30fps per `Animations/character_run_*.anim` `m_SampleRate`):**

| Direction | Frames | Clip length |
|---|---|---|
| front | 21 | 0.700s |
| left | 13 | 0.433s |
| right | 12 | 0.400s |
| up | 24 | 0.800s |

**Character-only bounding box height, averaged across all frames, largest-connected-component per frame (excludes the owl companion sprite that shares each canvas):**

| Direction | Height (px) | vs front |
|---|---|---|
| front | 320.8 | baseline |
| right | 294.7 | -8% (normal — side view foreshortens) |
| left | 282.2 | -12% (normal — side view foreshortens, matches right) |
| up | 254.4 | **-21%** (visibly shorter, confirmed in side-by-side composite) |

Conclusion: `up` is the only direction with a real size problem. `left` is fine size-wise (within 4% of `right`).

**Root cause of the speed problem — found in `Assets/Scripts/Common/Movement/CharacterRunDirection.cs`:**

The script computes leg tempo per-step, not per-loop (comment at line 52-55: front/up hold a full 2-step gait cycle, left/right hold a single 1-step stride). The formula (lines 143-146):

```csharp
float unitsPerLoop = Mathf.Max(0.05f, unitsPerStep) * Mathf.Max(1, stepsPerLoop[o]);
float mul = o == 0 ? animSpeedUp : o == 2 ? animSpeedLeft : o == 6 ? animSpeedRight : animSpeedFront;
animator.speed = run
    ? Mathf.Clamp(smoothSpeed * clipLen[o] / unitsPerLoop, 0.3f, 3f) * Mathf.Max(0.05f, mul)
    : 1f;
```

Working out time-per-step from this formula: `stepsPerLoop` cancels out algebraically (it appears in both `unitsPerLoop` and the loop-phase→step-phase mapping), leaving:

```
time_per_step = unitsPerStep / (travel_speed * mul)
```

This does **not** depend on clip length or steps-per-loop — it depends only on `unitsPerStep` (a single shared value, currently `0.3`) and `mul`. That means **`mul` must be the same value for all 4 directions** for leg cadence to match; it's a pure "how many world-units does one step take" knob per direction, and there's no in-formula reason for it to differ.

In the actual scene (`Assets/Scenes/region 1/reference_forest.unity`, the `CharacterRunDirection` component on the character GameObject, fileID `1659720018`), the values are:

```yaml
unitsPerStep: 0.3
idleDelay: 0.15
animSpeedUp: 1
animSpeedLeft: 1
animSpeedFront: 0.6
animSpeedRight: 0.6
```

`animSpeedFront`/`animSpeedRight` were tuned down to `0.6` at some point (presumably to fix visible foot-sliding), but `animSpeedUp`/`animSpeedLeft` were left at the untouched default `1`. Since `mul` should be uniform, `up` and `left` currently play their steps `1/0.6 ≈ 1.67×` faster (in real time) than `front`/`right` — this is exactly the "left and up look faster" symptom.

This component is a plain scene-embedded MonoBehaviour, not a prefab override (`m_CorrespondingSourceObject: {fileID: 0}` on that component) — the source prefab (`Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_03/spritesheet_7.prefab`) only provides the base sprite/Transform; `CharacterRunDirection` and the `Animator` (controller: `Assets/Art/quest_map/character/CharacterRunWorldController.controller`) were added directly on the scene instance. So this is a direct, safe field edit on the scene file/GameObject — no prefab propagation to worry about.

---

## Task 1: Fix the leg-cadence speed imbalance (data-only)

**Files:**
- Modify: `Assets/Scenes/region 1/reference_forest.unity` (the `CharacterRunDirection` component on GameObject fileID `1659720016`, MonoBehaviour fileID `1659720018`)

**Interfaces:**
- Consumes: nothing new — this only changes existing serialized field values (`animSpeedUp`, `animSpeedLeft`) that `CharacterRunDirection.cs` already reads every `LateUpdate()`.
- Produces: nothing new for later tasks.

- [ ] **Step 1: Open the scene in Unity and select the character GameObject**

Use the Unity MCP tools (see project memory `mcp-three-servers-session-setup` for which MCP/instance to target). Open `Assets/Scenes/region 1/reference_forest.unity` if not already open, then find the GameObject that holds the `CharacterRunDirection` component (it also holds the `Animator` with `CharacterRunWorldController`). If using `mcp__UnityMCP__find_gameobjects` or `mcp__anklebreaker__unity_search_by_component`, search for component type `CharacterRunDirection`.

- [ ] **Step 2: Change `animSpeedUp` and `animSpeedLeft` from `1` to `0.6`**

Use `mcp__UnityMCP__manage_components` (or `mcp__anklebreaker__unity_component_set_property`) to set on the `CharacterRunDirection` component:

```
animSpeedUp: 0.6
animSpeedLeft: 0.6
```

Leave `animSpeedFront` (0.6), `animSpeedRight` (0.6), `unitsPerStep` (0.3), and `idleDelay` (0.15) unchanged. Do **not** hand-edit the `.unity` YAML text directly while the Editor has the scene open — go through the Editor/MCP so the Editor's in-memory scene state and the file stay in sync (per memory `unity-mcp-scene-edits-vanish`).

- [ ] **Step 3: Save the scene**

Call `mcp__UnityMCP__manage_scene` save (or `mcp__anklebreaker__unity_scene_save`) for `reference_forest`. This step is required — unsaved GameObject/component edits vanish on the next domain reload.

- [ ] **Step 4: Visual verification — leg cadence**

Enter Play Mode on `reference_forest` (or use whatever quest-path trigger walks the character in all 4 directions — see project memory `quest-path-sequence-reference-forest`). Watch the character walk/run in each of the 4 directions in turn. Pass criterion: the leg/foot movement rate (steps per second) looks the same in all 4 directions — no direction should look like it's "running" while another "jogs". If `left` or `up` still looks off, nudge only that direction's multiplier by ±0.05 and re-check (don't touch `front`/`right`, they're already the tuned reference).

- [ ] **Step 5: Exit Play Mode and re-save if you nudged any value in Step 4**

If Step 4 required a tweak, repeat Step 3 (save scene) after exiting Play Mode — Unity discards Play Mode edits to scene objects on stop.

---

## Task 2: Add per-direction scale compensation for the `up` direction

**Files:**
- Modify: `Assets/Scripts/Common/Movement/CharacterRunDirection.cs`
- Modify: `Assets/Scenes/region 1/reference_forest.unity` (same `CharacterRunDirection` component as Task 1, to set the new field's value)

**Interfaces:**
- Consumes: existing private fields `playingOrient` (int, 0/2/4/6), the `Awake()`/`LateUpdate()` structure already in the file (see full current listing below).
- Produces: nothing new for later tasks — this is the last code change in this plan.

### Background for this task

`up` is the only direction with a real character-height problem: measured at ~254px average character height vs. ~283-321px for the other 3 directions (see Investigation Findings) — about 21% shorter than `front`. The `up` frames' width (~192px average) is already close to `front`'s (~195px), so compensating by scaling only the Y axis (not X) avoids distorting the character's silhouette width. Scale on the same Transform the `Animator` lives on (this is the sprite root — the source prefab is `Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_03/spritesheet_7.prefab`).

### Current full contents of `Assets/Scripts/Common/Movement/CharacterRunDirection.cs`

```csharp
using UnityEngine;

/// <summary>
/// Drives the 4-direction run character in world scenes, replacing
/// SuperRetroMainBundle.CharacterAppearance. Keeps the pack's param contract —
/// 'orientation' int (0 up / 2 left / 4 down / 6 right) + 'speed' float — as a
/// pure data channel (QuestPathSequence's facingLock/forced-walk still write it),
/// but plays states DIRECTLY instead of via Animator transitions, which buys the
/// two things transitions can't do for sprite-swap clips:
///   1. Phase-preserving turns: switching run direction carries normalizedTime
///      over, so the legs keep their stride phase instead of restarting at
///      frame 0 (the visible "hitch" on every turn).
///   2. Tempo sync: animator.speed is scaled so one run cycle covers
///      unitsPerLoop world units at the measured movement speed — feet stop
///      sliding ("moonwalk") when clip tempo and travel speed disagree.
/// Runs after QuestPathSequence's LateUpdate (execution order) so it always
/// reads the final param values for the frame.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class CharacterRunDirection : MonoBehaviour
{
    [Tooltip("Animator with CharacterRunWorldController; auto-grabbed from this GameObject if empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("World units one STEP should cover. Lower = faster leg tempo. Tune until feet stop sliding.")]
    [SerializeField] private float unitsPerStep = 0.5f;

    [Tooltip("Stay in the run state this long after movement stops — hides the 1-2 frame gaps between path legs so turns don't flash an idle pose.")]
    [SerializeField] private float idleDelay = 0.15f;

    [Header("Per-direction animation speed (multiplier on the auto tempo; travel speed unaffected)")]
    [SerializeField] private float animSpeedUp = 1f;
    [SerializeField] private float animSpeedLeft = 1f;
    [SerializeField] private float animSpeedFront = 1f;
    [SerializeField] private float animSpeedRight = 1f;

    private static readonly int SpeedParam = Animator.StringToHash("speed");
    private static readonly int OrientParam = Animator.StringToHash("orientation");

    private Vector3 lastPosition;
    private float smoothSpeed;   // EMA of measured world speed, units/sec
    private float stillTime;     // seconds since movement stopped (for idleDelay)
    private int playingHash;     // state we last told the Animator to play
    private bool playingRun;
    private int playingOrient = 4; // orientation of the playing state (for step-phase mapping)

    // orientation value (0/2/4/6) -> state hashes + clip length
    private readonly int[] runHash = new int[7];
    private readonly int[] idleHash = new int[7];
    private readonly float[] clipLen = new float[7];

    // Steps contained in each direction's loop (measured from the renders via head-bob):
    // front/up clips hold a full gait cycle (2 steps), left/right loop on a single stride.
    // Tempo is normalized per STEP so legs pace identically in every direction.
    private static readonly int[] stepsPerLoop = { 2, 0, 1, 0, 2, 0, 1 }; // idx 0 up, 2 left, 4 front, 6 right

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        lastPosition = transform.position;

        foreach (var (o, dir) in new[] { (0, "up"), (2, "left"), (4, "front"), (6, "right") })
        {
            runHash[o] = Animator.StringToHash($"run_{dir}");
            idleHash[o] = Animator.StringToHash($"idle_{dir}");
            clipLen[o] = 0.5f; // fallback until the clip is found below
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                switch (clip.name)
                {
                    case "character_run_up": clipLen[0] = clip.length; break;
                    case "character_run_left": clipLen[2] = clip.length; break;
                    case "character_run_front": clipLen[4] = clip.length; break;
                    case "character_run_right": clipLen[6] = clip.length; break;
                }
            }
        }
    }

    private void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        if (animator == null || Time.deltaTime <= 0f) return;

        float instSpeed = delta.magnitude / Time.deltaTime;
        // EMA so one hitchy frame doesn't jerk the leg tempo around
        smoothSpeed = Mathf.Lerp(smoothSpeed, instSpeed, 0.3f);

        animator.SetFloat(SpeedParam, instSpeed);
        // Only steer while actually moving so QuestPathSequence's facingLock
        // (written in its LateUpdate while stopped) is never fought over.
        if (instSpeed > 0.01f)
        {
            int orientation = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? 6 : 2)
                : (delta.y >= 0f ? 0 : 4);
            animator.SetInteger(OrientParam, orientation);
        }
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        // Final say for this frame: params as written by us + QuestPathSequence
        // (its coroutine forces speed while walking; facingLock overrides when stopped.)
        int o = animator.GetInteger(OrientParam);
        if (o != 0 && o != 2 && o != 4 && o != 6) o = 4;
        bool moving = animator.GetFloat(SpeedParam) > 0.01f;

        // Grace period before dropping to idle: waypoint corners produce 1-2 zero-delta
        // frames between path legs; without this the character flashes an idle pose on
        // every turn (reads as a stutter) and loses its stride phase.
        stillTime = moving ? 0f : stillTime + Time.deltaTime;
        bool run = moving || stillTime < idleDelay;
        int target = run ? runHash[o] : idleHash[o];

        if (target != playingHash)
        {
            // Turning while running: carry stride phase into the new direction.
            // Clips hold different step counts, so map loop-phase -> step-phase -> loop-phase.
            float phase = 0f;
            if (run && playingRun)
            {
                float oldLoopPhase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
                float stepPhase = (oldLoopPhase * Mathf.Max(1, stepsPerLoop[playingOrient])) % 1f;
                phase = stepPhase / Mathf.Max(1, stepsPerLoop[o]);
            }
            animator.Play(target, 0, phase);
            playingHash = target;
            playingRun = run;
            playingOrient = o;
        }

        // Leg tempo follows real travel speed: one STEP per unitsPerStep world units.
        // (clips hold different step counts per loop — see stepsPerLoop.)
        // Then a per-direction inspector multiplier for taste; travel speed is unaffected.
        float unitsPerLoop = Mathf.Max(0.05f, unitsPerStep) * Mathf.Max(1, stepsPerLoop[o]);
        float mul = o == 0 ? animSpeedUp : o == 2 ? animSpeedLeft : o == 6 ? animSpeedRight : animSpeedFront;
        animator.speed = run
            ? Mathf.Clamp(smoothSpeed * clipLen[o] / unitsPerLoop, 0.3f, 3f) * Mathf.Max(0.05f, mul)
            : 1f;
    }
}
```

- [ ] **Step 1: Add a `baseScale` field and per-direction scale multiplier fields**

In `Assets/Scripts/Common/Movement/CharacterRunDirection.cs`, replace the `Header("Per-direction animation speed...")` block through the `stepsPerLoop` field declaration with:

```csharp
    [Header("Per-direction animation speed (multiplier on the auto tempo; travel speed unaffected)")]
    [SerializeField] private float animSpeedUp = 1f;
    [SerializeField] private float animSpeedLeft = 1f;
    [SerializeField] private float animSpeedFront = 1f;
    [SerializeField] private float animSpeedRight = 1f;

    [Header("Per-direction vertical scale (compensates for source art being shorter in some directions; X is untouched)")]
    [SerializeField] private float scaleUp = 1f;
    [SerializeField] private float scaleLeft = 1f;
    [SerializeField] private float scaleFront = 1f;
    [SerializeField] private float scaleRight = 1f;

    private static readonly int SpeedParam = Animator.StringToHash("speed");
    private static readonly int OrientParam = Animator.StringToHash("orientation");

    private Vector3 lastPosition;
    private Vector3 baseScale;   // transform.localScale as authored, before per-direction Y compensation
    private float smoothSpeed;   // EMA of measured world speed, units/sec
    private float stillTime;     // seconds since movement stopped (for idleDelay)
    private int playingHash;     // state we last told the Animator to play
    private bool playingRun;
    private int playingOrient = 4; // orientation of the playing state (for step-phase mapping)

    // orientation value (0/2/4/6) -> state hashes + clip length
    private readonly int[] runHash = new int[7];
    private readonly int[] idleHash = new int[7];
    private readonly float[] clipLen = new float[7];
```

(This keeps every existing field and comment as-is, only inserting the new `Header`/fields and the new `baseScale` field.)

- [ ] **Step 2: Capture `baseScale` in `Awake()`**

In `Awake()`, right after `lastPosition = transform.position;`, add:

```csharp
        baseScale = transform.localScale;
```

- [ ] **Step 3: Apply the per-direction Y scale in `LateUpdate()`**

At the end of `LateUpdate()`, after the existing `animator.speed = ...` block, add:

```csharp
        float scaleMul = o == 0 ? scaleUp : o == 2 ? scaleLeft : o == 6 ? scaleRight : scaleFront;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * scaleMul, baseScale.z);
```

- [ ] **Step 4: Save the script and let Unity recompile**

Use `mcp__UnityMCP__refresh_unity` (or trigger a recompile the normal way) then `read_console` to confirm there are no compile errors before proceeding, per the UnityMCP server's own workflow guidance.

- [ ] **Step 5: Set the initial `scaleUp` value on the scene component**

On the same `CharacterRunDirection` component edited in Task 1 (`reference_forest.unity`, GameObject fileID `1659720016`), set:

```
scaleUp: 1.15
```

Leave `scaleLeft`, `scaleFront`, `scaleRight` at `1` (default — those directions don't need compensation per the Investigation Findings measurements). `1.15` is a starting estimate (roughly midway between "match front's 320.8px" [needs ×1.26] and "match left/right's ~288px average" [needs ×1.13]) — Step 7 below is where you actually tune it by eye.

- [ ] **Step 6: Save the scene**

Same as Task 1 Step 3 — save `reference_forest` so the field value persists.

- [ ] **Step 7: Visual verification — character height, tune by eye**

Enter Play Mode, walk the character in the `up` direction, and compare its on-screen height against `front`/`left`/`right` (walk in a small square so you see all 4 back-to-back). Pass criterion: the character's head-to-toe height when facing away (`up`) should look the same as when facing the other 3 directions — not visibly shorter/squashed, not visibly stretched. If it looks off, adjust `scaleUp` in increments of `0.05` and re-check. Exit Play Mode and re-save the scene (Step 6) after any change, since Play Mode edits don't persist.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Common/Movement/CharacterRunDirection.cs "Assets/Scenes/region 1/reference_forest.unity"
git commit -m "fix(character): normalize per-direction run speed and up-facing height"
```
