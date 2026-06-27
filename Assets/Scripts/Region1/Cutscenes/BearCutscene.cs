using System.Collections;
using UnityEngine;

// Starter bear cutscene: plays a sequence of bear animation states in order,
// holding each for a set time before switching to the next. Drive it from the
// new Bear.prefab (UGUI Image + Animator). Edit the Sequence in the Inspector;
// state names must match the clips in BearController.controller:
//   bear_breathing, bear_jump, bear_run2, bear_run_back, Thow
public sealed class BearCutscene : MonoBehaviour
{
    [System.Serializable]
    public struct Step
    {
        public string state;     // Animator state name to play
        public float holdSeconds; // how long to stay before the next step
        public float speed;      // playback speed multiplier (1 = normal, 0.5 = slower, 2 = faster; <=0 treated as 1)
    }

    [Header("References")]
    [Tooltip("Bear Animator. Leave empty to use the Animator on this GameObject.")]
    [SerializeField] private Animator bear;

    [Header("Flow")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = false;
    [SerializeField] private Step[] sequence =
    {
        new Step { state = "bear_jump",      holdSeconds = 1.5f, speed = 1f },
        new Step { state = "bear_run2",      holdSeconds = 2.0f, speed = 1f },
        new Step { state = "bear_breathing", holdSeconds = 0f,   speed = 1f },
    };

    private Coroutine routine;

    private void Awake()
    {
        if (bear == null) bear = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (playOnStart) Play();
    }

    private void OnDisable()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }

    public void Play()
    {
        if (bear == null) { Debug.LogWarning("[BearCutscene] no Animator assigned"); return; }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        do
        {
            foreach (Step step in sequence)
            {
                bear.speed = step.speed > 0f ? step.speed : 1f;
                if (!string.IsNullOrEmpty(step.state)) bear.Play(step.state);
                if (step.holdSeconds > 0f) yield return new WaitForSeconds(step.holdSeconds);
            }
        }
        while (loop);

        routine = null;
    }
}
