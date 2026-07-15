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

    // Steps contained in each direction's loop (measured from the renders via head-bob):
    // front/up clips hold a full gait cycle (2 steps), left/right loop on a single stride.
    // Tempo is normalized per STEP so legs pace identically in every direction.
    private static readonly int[] stepsPerLoop = { 2, 0, 1, 0, 2, 0, 1 }; // idx 0 up, 2 left, 4 front, 6 right

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        lastPosition = transform.position;
        baseScale = transform.localScale;

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

        float scaleMul = o == 0 ? scaleUp : o == 2 ? scaleLeft : o == 6 ? scaleRight : scaleFront;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * scaleMul, baseScale.z);
    }
}
