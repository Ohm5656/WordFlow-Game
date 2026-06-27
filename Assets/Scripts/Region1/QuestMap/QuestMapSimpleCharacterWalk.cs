using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class QuestMapSimpleCharacterWalk : MonoBehaviour
{
    private const string QuestMapSceneName = "quest_map1";
    private const string PlayerRootName = "playerRoot";
    private const string CharacterName = "base_basic_pbr";

    [SerializeField] private bool playOnStart = true;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Vector3 walkOffset = new Vector3(1.45f, 1.35f, 0f);
    [SerializeField] private float startDelay = 0.35f;
    [SerializeField] private float walkDuration = 3.1f;
    [SerializeField] private float stepFrequency = 2.65f;
    [SerializeField] private float bobHeight = 0.065f;
    [SerializeField] private float sideSway = 0.035f;
    [SerializeField] private float bodyRollDegrees = 3.2f;
    [SerializeField] private float bodyYawDegrees = 4.5f;
    [SerializeField] private float travelYawDegrees = 7f;
    [SerializeField] private float squashAmount = 0.018f;
    [SerializeField] private bool idleAfterWalk = true;
    [SerializeField] private float idleBobHeight = 0.018f;
    [SerializeField] private float idleRollDegrees = 1.1f;
    [SerializeField] private float idleCycleDuration = 1.65f;

    private Vector3 visualBaseLocalPosition;
    private Quaternion visualBaseLocalRotation;
    private Vector3 visualBaseLocalScale;
    private Coroutine activeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryAttachToQuestMapCharacter();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAttachToQuestMapCharacter();
    }

    private static void TryAttachToQuestMapCharacter()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != QuestMapSceneName)
        {
            return;
        }

        GameObject playerRootObject = GameObject.Find(PlayerRootName);
        if (playerRootObject == null)
        {
            return;
        }

        QuestMapSimpleCharacterWalk walker = playerRootObject.GetComponent<QuestMapSimpleCharacterWalk>();
        if (walker == null)
        {
            walker = playerRootObject.AddComponent<QuestMapSimpleCharacterWalk>();
        }

        if (walker.visualRoot == null)
        {
            GameObject visualObject = GameObject.Find(CharacterName);
            walker.visualRoot = visualObject != null
                ? visualObject.transform
                : playerRootObject.transform.childCount > 0
                    ? playerRootObject.transform.GetChild(0)
                    : playerRootObject.transform;
        }
    }

    private void Awake()
    {
        ResolveVisualRoot();
    }

    private void Start()
    {
        CacheBasePose();
        if (playOnStart)
        {
            PlayWalkFromCurrentPosition();
        }
    }

    public void PlayWalkFromCurrentPosition()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        CacheBasePose();
        activeRoutine = StartCoroutine(WalkRoutine());
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform namedChild = transform.Find(CharacterName);
        if (namedChild != null)
        {
            visualRoot = namedChild;
            return;
        }

        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    private void CacheBasePose()
    {
        ResolveVisualRoot();
        if (visualRoot == null)
        {
            return;
        }

        visualBaseLocalPosition = visualRoot.localPosition;
        visualBaseLocalRotation = visualRoot.localRotation;
        visualBaseLocalScale = visualRoot.localScale;
    }

    private IEnumerator WalkRoutine()
    {
        if (visualRoot == null)
        {
            yield break;
        }

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + walkOffset;
        float duration = Mathf.Max(0.1f, walkDuration);
        float directionYaw = Mathf.Clamp(walkOffset.normalized.x, -1f, 1f) * travelYawDegrees;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = SmootherStep(normalized);
            transform.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);
            ApplyWalkPose(elapsed, directionYaw);
            yield return null;
        }

        transform.position = endPosition;
        RestoreBaseVisualPose();

        activeRoutine = idleAfterWalk ? StartCoroutine(IdleRoutine()) : null;
    }

    private void ApplyWalkPose(float elapsed, float directionYaw)
    {
        float cycle = elapsed * stepFrequency * Mathf.PI * 2f;
        float step = Mathf.Sin(cycle);
        float liftedStep = Mathf.Abs(step);
        float alternatingStep = Mathf.Sin(cycle * 0.5f);

        Vector3 localPosition = visualBaseLocalPosition;
        localPosition.y += liftedStep * bobHeight;
        localPosition.x += alternatingStep * sideSway;
        visualRoot.localPosition = localPosition;

        float yaw = directionYaw + alternatingStep * bodyYawDegrees;
        float roll = -step * bodyRollDegrees;
        visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, yaw, roll);

        float squash = liftedStep * squashAmount;
        visualRoot.localScale = new Vector3(
            visualBaseLocalScale.x * (1f + squash * 0.45f),
            visualBaseLocalScale.y * (1f - squash),
            visualBaseLocalScale.z * (1f + squash * 0.45f));
    }

    private IEnumerator IdleRoutine()
    {
        float cycleDuration = Mathf.Max(0.1f, idleCycleDuration);
        float clock = 0f;

        while (visualRoot != null)
        {
            clock += Time.deltaTime;
            float cycle = clock / cycleDuration * Mathf.PI * 2f;
            float bob = Mathf.Sin(cycle);

            Vector3 localPosition = visualBaseLocalPosition;
            localPosition.y += bob * idleBobHeight;
            visualRoot.localPosition = localPosition;
            visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, 0f, bob * idleRollDegrees);
            visualRoot.localScale = visualBaseLocalScale;
            yield return null;
        }
    }

    private void RestoreBaseVisualPose()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localRotation = visualBaseLocalRotation;
        visualRoot.localScale = visualBaseLocalScale;
    }

    private static float SmootherStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
}
