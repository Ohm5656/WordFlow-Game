using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Turns the authored adventure card in <c>reference_forest</c> into the daytime entry screen.
/// It pauses the existing quest route without changing it, then enables that route only after the
/// player presses the card's start button. The same Canvas is kept completely out of night-redo
/// runs and out of mid-day returns from the puzzle scenes.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class ReferenceForestAdventureIntro : MonoBehaviour
{
    private const string ForestSceneName = "reference_forest";
    private const string WorldMapSceneName = "WorldMap";
    private const string CanvasName = "ui";
    private const string StartButtonName = "before-start (1)";
    private const string BackButtonName = "before-start (3)";
    private const float UiFadeSeconds = 0.16f;
    private const float ExitCoverSeconds = 0.45f;

    private static ReferenceForestAdventureIntro activeInstance;

    private QuestPathSequence questSequence;
    private CanvasGroup canvasGroup;
    private bool inputLocked;

    public static bool ShouldDeferQuestSequenceStart
    {
        get
        {
            return SceneManager.GetActiveScene().name == ForestSceneName
                && !NightMode.RedoActive
                && !BearEncounterFlow.ResumeAtBeat2
                && !BearEncounterFlow.ResumeAtBeat3;
        }
    }

    // Register before the scene starts loading. SceneManager.sceneLoaded runs after Awake but
    // before Start, which lets the card pause QuestPathSequence before it can begin walking.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateForScene(scene);
    }

    private static void CreateForScene(Scene scene)
    {
        if (scene.name != ForestSceneName || activeInstance != null)
        {
            return;
        }

        GameObject canvasObject = FindRootObject(scene, CanvasName);
        if (canvasObject == null)
        {
            Debug.LogWarning("[ReferenceForestAdventureIntro] Could not find the 'ui' Canvas in reference_forest.");
            return;
        }

        ReferenceForestAdventureIntro controller = canvasObject.AddComponent<ReferenceForestAdventureIntro>();
        controller.Initialize();
    }

    private void Awake()
    {
        activeInstance = this;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    private void Initialize()
    {
        // Returning from either daytime puzzle must continue exactly where the existing route left
        // off. A proper night retry has its own start state and must never show the day card.
        bool skipIntro = NightMode.RedoActive
            || BearEncounterFlow.ResumeAtBeat2
            || BearEncounterFlow.ResumeAtBeat3;
        if (skipIntro)
        {
            gameObject.SetActive(false);
            return;
        }

        questSequence = FindAnyObjectByType<QuestPathSequence>();
        if (questSequence == null)
        {
            Debug.LogWarning("[ReferenceForestAdventureIntro] QuestPathSequence is missing; the start card cannot pause the route.");
        }
        else
        {
            // QuestPathSequence.Awake still prepares the actors normally, but disabling the
            // component here prevents its Start() from beginning the walk underneath the card.
            questSequence.enabled = false;
            questSequence.SetInitialIdleFacingRight();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Image startImage = FindDescendantImage(StartButtonName);
        Image backImage = FindDescendantImage(BackButtonName);
        if (startImage == null || backImage == null)
        {
            Debug.LogWarning("[ReferenceForestAdventureIntro] Start/back artwork is missing; the card was left visible but not wired.");
            return;
        }

        AddDimmer();
        DisableDecorativeRaycasts(startImage, backImage);
        WireButton(startImage, StartAdventure, pulseWhenIdle: true);
        WireButton(backImage, ReturnToWorldMap, pulseWhenIdle: false);
    }

    private void AddDimmer()
    {
        Transform existing = transform.Find("Adventure Intro Dimmer");
        if (existing != null)
        {
            return;
        }

        // RawImage has a guaranteed built-in white texture.  A plain Image without an assigned
        // source sprite can be skipped by the UI renderer in this project, leaving the map bright
        // even though the dimmer object exists in the hierarchy.
        GameObject dimmerObject = new GameObject("Adventure Intro Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        dimmerObject.transform.SetParent(transform, false);
        dimmerObject.transform.SetSiblingIndex(0);

        RectTransform rect = dimmerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage dimmer = dimmerObject.GetComponent<RawImage>();
        // Assign this explicitly rather than relying on RawImage's editor default. This keeps the
        // dimmer visible in a player build as well as in the Editor.
        dimmer.texture = Texture2D.whiteTexture;
        // A warm charcoal dimmer makes the intro card read as the active focus without
        // tinting the daytime map blue behind it.
        dimmer.color = new Color(0.04f, 0.03f, 0.02f, 0.7f);
        dimmer.raycastTarget = true;
    }

    private void DisableDecorativeRaycasts(Image startImage, Image backImage)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null
                && image != startImage
                && image != backImage
                && image.gameObject.name != "Adventure Intro Dimmer")
            {
                image.raycastTarget = false;
            }
        }
    }

    private static void WireButton(Image image, UnityEngine.Events.UnityAction action, bool pulseWhenIdle)
    {
        image.raycastTarget = true;
        Button button = image.GetComponent<Button>();
        if (button == null)
        {
            button = image.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            GameAudio.PlayClick();
            action();
        });

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.72f, 1f);
        colors.pressedColor = new Color(0.9f, 0.8f, 0.46f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        ReferenceForestCardButtonFeedback feedback = image.GetComponent<ReferenceForestCardButtonFeedback>();
        if (feedback == null)
        {
            feedback = image.gameObject.AddComponent<ReferenceForestCardButtonFeedback>();
        }

        feedback.Configure(pulseWhenIdle);
    }

    private Image FindDescendantImage(string objectName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == objectName)
            {
                return all[i].GetComponent<Image>();
            }
        }

        return null;
    }

    private void StartAdventure()
    {
        if (inputLocked)
        {
            return;
        }

        StartCoroutine(StartAdventureRoutine());
    }

    private IEnumerator StartAdventureRoutine()
    {
        inputLocked = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        for (float elapsed = 0f; elapsed < UiFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / UiFadeSeconds);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        if (questSequence != null)
        {
            questSequence.enabled = true;
            questSequence.BeginFromAdventureCard();
        }

        gameObject.SetActive(false);
    }

    private void ReturnToWorldMap()
    {
        if (!inputLocked)
        {
            StartCoroutine(ReturnToWorldMapRoutine());
        }
    }

    private IEnumerator ReturnToWorldMapRoutine()
    {
        inputLocked = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        yield return SceneFadeController.Cover(ExitCoverSeconds);
        // Returning from the entry card is not a new game start. Keep the already-unlocked
        // island ready on the map so the player can simply enter it again.
        WorldMapProblemIslands.SkipFirstUnlockReplayOnce();
        SceneManager.LoadScene(WorldMapSceneName);
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }
}

/// <summary>
/// Lightweight feedback for the authored adventure-card artwork. The main call-to-action has a
/// restrained idle pulse; both card buttons grow on focus and compress on press, which works for
/// mouse, touch and controller/EventSystem selection without replacing the existing visuals.
/// </summary>
[DisallowMultipleComponent]
public sealed class ReferenceForestCardButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private const float HoverScale = 1.055f;
    private const float PressScale = 0.93f;
    private const float PulseAmplitude = 0.022f;
    private const float PulseFrequency = 2.6f;
    private const float ResponseSpeed = 18f;

    private RectTransform target;
    private Vector3 baseScale;
    private bool pulseWhenIdle;
    private bool hovered;
    private bool selected;
    private bool pressed;
    private float currentMultiplier = 1f;

    public void Configure(bool shouldPulseWhenIdle)
    {
        target = transform as RectTransform;
        if (target == null)
        {
            enabled = false;
            return;
        }

        baseScale = target.localScale;
        pulseWhenIdle = shouldPulseWhenIdle;
        currentMultiplier = 1f;
    }

    private void OnDisable()
    {
        if (target != null)
        {
            target.localScale = baseScale;
        }
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        float targetMultiplier;
        if (pressed)
        {
            targetMultiplier = PressScale;
        }
        else if (hovered || selected)
        {
            targetMultiplier = HoverScale;
        }
        else if (pulseWhenIdle)
        {
            targetMultiplier = 1f + Mathf.Sin(Time.unscaledTime * PulseFrequency) * PulseAmplitude;
        }
        else
        {
            targetMultiplier = 1f;
        }

        currentMultiplier = Mathf.MoveTowards(
            currentMultiplier,
            targetMultiplier,
            ResponseSpeed * Time.unscaledDeltaTime);
        target.localScale = baseScale * currentMultiplier;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }
}
