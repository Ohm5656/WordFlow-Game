using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class BossSpriteIntro : MonoBehaviour
{
    [Header("Existing boss-scene UI")]
    [SerializeField] private string wordAssemblyObjectName = "WordAssembly";
    [SerializeField] private string bookObjectName = "book_craft";

    [Header("Boss entrance")]
    [SerializeField, Min(0f)] private float backgroundPreviewDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float bossFadeInDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float attackPlaybackSpeed = 1.25f;
    [SerializeField, Min(0.1f)] private float idlePlaybackSpeed = 1f;

    [Header("Book reveal")]
    [SerializeField, Min(0f)] private float idleBeforeBookDelay = 1f;
    [SerializeField, Min(0f)] private float bookRevealDelay = 0.1f;
    [SerializeField, Range(0.01f, 1f)] private float bookStartScale = 0.08f;
    [SerializeField, Min(0.01f)] private float bookRevealDuration = 0.65f;

    private static readonly int BossAttackState = Animator.StringToHash("Base Layer.BossAttack");

    private Animator bossAnimator;
    private CanvasGroup bossCanvasGroup;
    private GameObject wordAssembly;
    private MagicStonePuzzleController puzzle;
    private RectTransform bookRoot;
    private CanvasGroup bookCanvasGroup;
    private Vector2 bookTargetPosition;
    private Vector3 bookTargetScale;
    private Coroutine entranceRoutine;
    private Coroutine revealRoutine;
    private bool revealStarted;

    private void Awake()
    {
        bossAnimator = GetComponent<Animator>();
        bossCanvasGroup = GetComponent<CanvasGroup>();
        if (bossCanvasGroup == null)
        {
            bossCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        bossCanvasGroup.alpha = 0f;
        bossCanvasGroup.blocksRaycasts = false;
        bossCanvasGroup.interactable = false;
        bossAnimator.enabled = false;
        PrepareWordAssembly();
    }

    private void Start()
    {
        entranceRoutine = StartCoroutine(PlayBossEntrance());
    }

    private void OnDisable()
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
    }

    // Called by the final frame of the non-looping BossAttack clip.
    public void RevealBookAfterIntro()
    {
        if (revealStarted)
        {
            return;
        }

        revealStarted = true;
        bossAnimator.speed = idlePlaybackSpeed;
        revealRoutine = StartCoroutine(RevealBookRoutine());
    }

    private IEnumerator PlayBossEntrance()
    {
        yield return new WaitUntil(() => SceneFadeController.RevealComplete);

        if (backgroundPreviewDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(backgroundPreviewDelay);
        }

        float duration = Mathf.Max(0.01f, bossFadeInDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            bossCanvasGroup.alpha = SmoothStep(elapsed / duration);
            yield return null;
        }

        bossCanvasGroup.alpha = 1f;
        bossAnimator.speed = attackPlaybackSpeed;
        bossAnimator.enabled = true;
        bossAnimator.Play(BossAttackState, 0, 0f);
        entranceRoutine = null;
    }

    private void PrepareWordAssembly()
    {
        wordAssembly = GameObject.Find(wordAssemblyObjectName);
        if (wordAssembly == null)
        {
            Debug.LogWarning($"[BossSpriteIntro] '{wordAssemblyObjectName}' was not found; the book reveal is skipped.", this);
            return;
        }

        BossWordAssemblyStarter starter = wordAssembly.GetComponent<BossWordAssemblyStarter>();
        if (starter != null)
        {
            starter.enabled = false;
        }

        ResolvePuzzle();
        CacheBookTarget();
        wordAssembly.SetActive(false);
    }

    private void ResolvePuzzle()
    {
        if (wordAssembly == null)
        {
            return;
        }

        MagicStonePuzzleController[] controllers = wordAssembly.GetComponentsInChildren<MagicStonePuzzleController>(true);
        for (int index = 0; index < controllers.Length; index++)
        {
            MagicStonePuzzleController controller = controllers[index];
            if (controller != null && controller.gameObject.name == "magic_stone")
            {
                puzzle = controller;
                break;
            }
        }

        for (int index = 0; index < controllers.Length; index++)
        {
            MagicStonePuzzleController controller = controllers[index];
            if (controller != null && controller != puzzle)
            {
                controller.enabled = false;
            }
        }

        if (puzzle != null)
        {
            puzzle.PrepareForIntro();
        }
    }

    private void CacheBookTarget()
    {
        if (wordAssembly == null)
        {
            return;
        }

        Transform bookTransform = FindDescendant(wordAssembly.transform, bookObjectName);
        bookRoot = bookTransform as RectTransform;
        if (bookRoot == null)
        {
            Debug.LogWarning($"[BossSpriteIntro] '{bookObjectName}' was not found in '{wordAssemblyObjectName}'.", this);
            return;
        }

        bookTargetPosition = bookRoot.anchoredPosition;
        bookTargetScale = bookRoot.localScale;
        bookCanvasGroup = bookRoot.GetComponent<CanvasGroup>();
        if (bookCanvasGroup == null)
        {
            bookCanvasGroup = bookRoot.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private IEnumerator RevealBookRoutine()
    {
        if (wordAssembly == null)
        {
            PrepareWordAssembly();
        }

        if (wordAssembly == null)
        {
            yield break;
        }

        wordAssembly.SetActive(true);
        ResolvePuzzle();
        CacheBookTarget();

        if (puzzle == null || bookRoot == null || bookCanvasGroup == null)
        {
            Debug.LogError("[BossSpriteIntro] WordAssembly setup is incomplete.", this);
            yield break;
        }

        bookRoot.gameObject.SetActive(true);
        bookRoot.anchoredPosition = Vector2.zero;
        bookRoot.localScale = bookTargetScale * bookStartScale;
        bookCanvasGroup.alpha = 0f;
        bookCanvasGroup.blocksRaycasts = false;
        bookCanvasGroup.interactable = false;

        if (idleBeforeBookDelay > 0f)
        {
            yield return new WaitForSeconds(idleBeforeBookDelay);
        }

        if (bookRevealDelay > 0f)
        {
            yield return new WaitForSeconds(bookRevealDelay);
        }

        float duration = Mathf.Max(0.01f, bookRevealDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float smooth = SmoothStep(progress);
            float scale = Mathf.LerpUnclamped(bookStartScale, 1f, EaseOutBack(progress));
            bookRoot.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, bookTargetPosition, smooth);
            bookRoot.localScale = bookTargetScale * scale;
            bookCanvasGroup.alpha = smooth;
            yield return null;
        }

        bookRoot.anchoredPosition = bookTargetPosition;
        bookRoot.localScale = bookTargetScale;
        bookCanvasGroup.alpha = 1f;

        puzzle.PrepareForIntro();
        yield return puzzle.PlayIntroReveal();
        revealRoutine = null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Queue<Transform> pending = new Queue<Transform>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            Transform current = pending.Dequeue();
            if (current.name == objectName)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                pending.Enqueue(current.GetChild(index));
            }
        }

        return null;
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutBack(float value)
    {
        const float overshoot = 1.70158f;
        float inverse = value - 1f;
        return 1f + (overshoot + 1f) * inverse * inverse * inverse + overshoot * inverse * inverse;
    }
}
