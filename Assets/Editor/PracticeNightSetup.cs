using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot scene setup for practice_night: removes clone leftovers (BearIntro*, BossUI, the
/// reference_forest quest-path waypoints), strips the stray MagicStonePuzzleController instances
/// the WordAssembly prefab carries over from Boss.unity, wires a PracticeWordAssembly onto
/// magic_stone, and instantiates + wires the Crow and OwlGuide actors onto a new
/// PracticeNightController. Also (R2) sets the hero to an idle facing-camera pose with movement
/// disabled, wires the assembly/book intro fields, applies the existing listen/record icon art
/// to the result books, and ensures
/// practice_night + WorldMap are in Build Settings.
///
/// Open practice_night first, then run Tools/Practice/Setup Practice Night, nudge
/// Crow/OwlGuide/hero positions to taste, and save.
/// See docs/superpowers/specs/2026-07-21-practice-night-design.md.
/// </summary>
public static class PracticeNightSetup
{
    private const string CrowPrefabPath = "Assets/Art/quest_map/Crow.prefab";
    private const string OwlGuidePrefabPath = "Assets/Prefabs/OwlGuide.prefab";
    private const string CrowFlyControllerPath = "Assets/Art/quest_map/CrowController.controller";
    private const string CrowSetFreeControllerPath = "Assets/Art/quest_map/GaSetFreeController.controller";
    private const string KaaWordClipPath = "Assets/Audio/Adventure/kaa_word.wav";
    private const string PaaWordClipPath = "Assets/Audio/Adventure/paa_word.wav";
    private const string KaaOwlClipPath = "Assets/Audio/Adventure/kaa_sound_out.wav";
    private const string PaaOwlClipPath = "Assets/Audio/Adventure/paa_sound_out.wav";
    private const string PracticeScarecrowSpritePath =
        "Assets/Art/boss/pratice/ChatGPT Image Jul 18, 2026, 12_27_39 PM.png";
    private const string PracticeScarecrowPrefabPath = "Assets/Prefabs/Practice/PracticeScarecrow.prefab";
    private const string ActionIconSheetPath =
        "Assets/Art/visaul_novel/quest/ChatGPT Image Jun 3, 2026, 03_33_58 PM.png";
    private const string SoundIconSpriteName = "ChatGPT Image Jun 3, 2026, 03_33_58 PM_8";
    private const string MicIconSpriteName = "ChatGPT Image Jun 3, 2026, 03_33_58 PM_9";
    private const string PracticeNightScenePath = "Assets/Scenes/region 1/practice_night.unity";
    private const string WorldMapScenePath = "Assets/Scenes/WorldMap.unity";

    [MenuItem("Tools/Practice/Setup Practice Night")]
    public static void Setup()
    {
        GameObject wordAssembly = GameObject.Find("WordAssembly");
        if (wordAssembly == null)
        {
            Debug.LogError("[PracticeNightSetup] 'WordAssembly' not found — open practice_night first.");
            return;
        }

        GameObject objectsRootGo = GameObject.Find("Objects");
        Transform objectsRoot = objectsRootGo != null ? objectsRootGo.transform : null;

        RemoveLeftovers(objectsRoot);
        RectTransform magicStoneRect = StripStrayPuzzleControllers(wordAssembly.transform);
        if (magicStoneRect == null)
        {
            Debug.LogError("[PracticeNightSetup] 'WordAssembly/magic_stone' not found.");
            return;
        }

        PracticeWordAssembly wordAssemblyComponent = magicStoneRect.GetComponent<PracticeWordAssembly>();
        if (wordAssemblyComponent == null)
        {
            wordAssemblyComponent = magicStoneRect.gameObject.AddComponent<PracticeWordAssembly>();
        }
        WireWordAssembly(wordAssemblyComponent, wordAssembly.transform, magicStoneRect);

        Transform actorCanvasRoot = GetOrCreateActorCanvas(wordAssembly);
        GameObject crow = InstantiateCrow(actorCanvasRoot, wordAssembly.transform);
        GameObject owl = InstantiateOwl(actorCanvasRoot, wordAssembly.transform);
        Animator heroAnimator = SetupHeroIdle(objectsRoot);
        Transform scarecrow = EnsurePracticeScarecrow(objectsRoot);

        GameObject controllerObject = GameObject.Find("PracticeNightController");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("PracticeNightController");
        }

        PracticeNightController controller = controllerObject.GetComponent<PracticeNightController>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<PracticeNightController>();
        }

        WireController(controller, wordAssemblyComponent, crow, owl, wordAssembly.transform, heroAnimator, scarecrow);
        EnsureBuildScenes();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PracticeNightSetup] Done. Position Crow/OwlGuide/hero visually, then save the scene.");
    }

    private static void RemoveLeftovers(Transform objectsRoot)
    {
        string[] names = { "BearIntroSpotlight", "BearIntroCanvas", "BossUI", "path", "frame" };
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        if (objectsRoot != null)
        {
            Transform questSequence = objectsRoot.Find("quest_sequence");
            if (questSequence != null) Object.DestroyImmediate(questSequence.gameObject);

            Transform quest = objectsRoot.Find("quest");
            if (quest != null) Object.DestroyImmediate(quest.gameObject);
        }
    }

    private static RectTransform StripStrayPuzzleControllers(Transform wordAssembly)
    {
        string[] carriers = { "book_craft", "book_craft_pa", "book_craft_ga", "magic_stone" };
        RectTransform magicStoneRect = null;

        foreach (string carrierName in carriers)
        {
            Transform carrier = wordAssembly.Find(carrierName);
            if (carrier == null) continue;

            MagicStonePuzzleController stray = carrier.GetComponent<MagicStonePuzzleController>();
            if (stray != null) Object.DestroyImmediate(stray);

            if (carrierName == "magic_stone") magicStoneRect = carrier as RectTransform;
        }

        return magicStoneRect;
    }

    private static Transform GetOrCreateActorCanvas(GameObject wordAssembly)
    {
        GameObject actorCanvasObject = GameObject.Find("Practice Night Actors");
        if (actorCanvasObject == null)
        {
            actorCanvasObject = new GameObject(
                "Practice Night Actors",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
        }

        RectTransform root = actorCanvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Canvas sourceCanvas = wordAssembly.GetComponent<Canvas>();
        Canvas actorCanvas = actorCanvasObject.GetComponent<Canvas>();
        actorCanvas.renderMode = sourceCanvas != null ? sourceCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        actorCanvas.overrideSorting = true;
        actorCanvas.sortingOrder = sourceCanvas != null ? sourceCanvas.sortingOrder - 1 : 9;

        CanvasScaler sourceScaler = wordAssembly.GetComponent<CanvasScaler>();
        CanvasScaler actorScaler = actorCanvasObject.GetComponent<CanvasScaler>();
        if (sourceScaler != null)
        {
            actorScaler.uiScaleMode = sourceScaler.uiScaleMode;
            actorScaler.referenceResolution = sourceScaler.referenceResolution;
            actorScaler.screenMatchMode = sourceScaler.screenMatchMode;
            actorScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            actorScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        }

        return root;
    }

    private static GameObject InstantiateCrow(Transform parent, Transform wordAssembly)
    {
        Transform existing = parent.Find("Crow");
        if (existing == null) existing = wordAssembly.Find("Crow");
        if (existing != null)
        {
            existing.SetParent(parent, false);
            ConfigureCrowImage(existing.gameObject);
            return existing.gameObject;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefabPath);
        GameObject crow = InstantiatePrefabRobust(prefab, parent);
        crow.name = "Crow";
        crow.transform.SetSiblingIndex(0); // draw behind book_craft/magic_stone, matching the reference art

        RectTransform rect = crow.transform as RectTransform;
        rect.anchoredPosition = new Vector2(0f, 260f); // starting guess — nudge to match the reference screenshot

        CrowEntranceCutscene entrance = crow.GetComponent<CrowEntranceCutscene>();
        if (entrance != null) entrance.enabled = false; // PracticeNightController drives the Animator directly

        ConfigureCrowImage(crow);
        crow.SetActive(false);
        return crow;
    }

    private static void ConfigureCrowImage(GameObject crow)
    {
        Image image = crow != null ? crow.GetComponent<Image>() : null;
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private static GameObject InstantiateOwl(Transform parent, Transform wordAssembly)
    {
        Transform existing = parent.Find("OwlGuide");
        if (existing == null) existing = wordAssembly.Find("OwlGuide");
        if (existing != null)
        {
            existing.SetParent(parent, false);
            return existing.gameObject;
        }

        // ponytail: this project's Library cache intermittently serves a stale/broken artifact for
        // this specific prefab (AssetDatabase.LoadAssetAtPath<GameObject> returns null with a "PPtr
        // cast failed... Casting from GameObject to Prefab" console error) — a force+synchronous
        // reimport right before loading reliably clears it. Drop this once the underlying Library
        // corruption is fixed upstream (or after a full Editor restart makes it a non-issue).
        GameObject prefab = LoadPrefabWithReimportFallback(OwlGuidePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PracticeNightSetup] Could not load '{OwlGuidePrefabPath}' even after a forced reimport — leaving OwlGuide unwired. Restart the Unity Editor and re-run Tools/Practice/Setup Practice Night.");
            return null;
        }

        GameObject owl = InstantiatePrefabRobust(prefab, parent);
        if (owl == null)
        {
            Debug.LogError($"[PracticeNightSetup] Could not instantiate '{OwlGuidePrefabPath}' — leaving OwlGuide unwired. Restart the Unity Editor and re-run Tools/Practice/Setup Practice Night.");
            return null;
        }
        owl.name = "OwlGuide";
        return owl;
    }

    private static GameObject LoadPrefabWithReimportFallback(string path)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go != null) return go;

        AssetDatabase.ImportAsset(path,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive | ImportAssetOptions.DontDownloadFromCacheServer);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // ponytail: PrefabUtility.InstantiatePrefab intermittently fails on this project's cached
    // artifacts (PPtr cast error) even when the source GameObject loaded fine; falling back to a
    // plain clone keeps the setup working (loses the blue "prefab instance" link in the Inspector,
    // but every component/child/value is identical). Drop the fallback once the underlying Library
    // corruption is fixed upstream.
    private static GameObject InstantiatePrefabRobust(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;

        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance != null) return instance;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PracticeNightSetup] PrefabUtility.InstantiatePrefab('{prefab.name}') failed ({e.Message}); falling back to Object.Instantiate.");
        }

        return Object.Instantiate(prefab, parent);
    }

    private static void WireController(PracticeNightController controller, PracticeWordAssembly wordAssembly,
        GameObject crow, GameObject owl, Transform wordAssemblyRoot, Animator heroAnimator, Transform scarecrowTransform)
    {
        if (crow == null)
        {
            Debug.LogError("[PracticeNightSetup] Crow could not be instantiated; controller wiring was skipped.");
            return;
        }

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("wordAssembly").objectReferenceValue = wordAssembly;
        so.FindProperty("heroAnimator").objectReferenceValue = heroAnimator;
        so.FindProperty("crowAnimator").objectReferenceValue = crow.GetComponent<Animator>();
        so.FindProperty("crowRect").objectReferenceValue = crow.transform as RectTransform;
        so.FindProperty("crowFlyController").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowFlyControllerPath);
        so.FindProperty("crowSetFreeController").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowSetFreeControllerPath);
        so.FindProperty("crowRestOffsetFromScarecrow").vector2Value = new Vector2(320f, 180f);
        so.FindProperty("crowScale").floatValue = 0.48f;
        so.FindProperty("crowCircleRadius").floatValue = 130f;

        so.FindProperty("scarecrowWorldTarget").objectReferenceValue = scarecrowTransform;

        so.FindProperty("bookPopRoot").objectReferenceValue = wordAssemblyRoot.Find("book_craft") as RectTransform;

        SerializedProperty owlProperty = so.FindProperty("owl");
        SerializedProperty owlAudioProperty = so.FindProperty("owlAudioSource");
        if (owl != null && (owlProperty != null || owlAudioProperty != null))
        {
            if (owlProperty != null) owlProperty.objectReferenceValue = owl.GetComponent<OwlGuideAnimator>();
            AudioSource owlAudio = owl.GetComponent<AudioSource>();
            if (owlAudio == null) owlAudio = owl.AddComponent<AudioSource>();
            owlAudio.playOnAwake = false;
            if (owlAudioProperty != null) owlAudioProperty.objectReferenceValue = owlAudio;
        }
        else if (owl == null && (owlProperty != null || owlAudioProperty != null))
        {
            Debug.LogWarning("[PracticeNightSetup] OwlGuide failed to instantiate — controller.owl/owlAudioSource left unwired. Re-run the setup after fixing the OwlGuide.prefab load (see error above).");
        }

        SerializedProperty events = so.FindProperty("events");
        events.arraySize = 2;

        SerializedProperty eventA = events.GetArrayElementAtIndex(0);
        eventA.FindPropertyRelative("kind").enumValueIndex = 0; // CrowPetrified
        eventA.FindPropertyRelative("targetWord").stringValue = "กา";
        eventA.FindPropertyRelative("resultPage").objectReferenceValue = wordAssemblyRoot.Find("book_craft_ga") as RectTransform;
        eventA.FindPropertyRelative("wordClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(KaaWordClipPath);
        SetOptionalRelativeObject(eventA, "owlLineClip", AssetDatabase.LoadAssetAtPath<AudioClip>(KaaOwlClipPath));

        SerializedProperty eventB = events.GetArrayElementAtIndex(1);
        eventB.FindPropertyRelative("kind").enumValueIndex = 1; // CrowCircling
        eventB.FindPropertyRelative("targetWord").stringValue = "ปา";
        eventB.FindPropertyRelative("resultPage").objectReferenceValue = wordAssemblyRoot.Find("book_craft_pa") as RectTransform;
        eventB.FindPropertyRelative("wordClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PaaWordClipPath);
        SetOptionalRelativeObject(eventB, "owlLineClip", AssetDatabase.LoadAssetAtPath<AudioClip>(PaaOwlClipPath));

        so.ApplyModifiedProperties();
    }

    private static void SetOptionalRelativeObject(SerializedProperty parent, string propertyName, Object value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    // Assembly UI: only book_craft, magic stones and the two slots show while building. The result
    // books remain hidden until a word is completed, then expose their own listen/record controls.
    private static void WireWordAssembly(PracticeWordAssembly wordAssemblyComponent, Transform wordAssemblyRoot, RectTransform magicStoneRect)
    {
        SerializedObject so = new SerializedObject(wordAssemblyComponent);

        RectTransform uiRoot = magicStoneRect.parent as RectTransform;
        so.FindProperty("assemblyUiRoot").objectReferenceValue = uiRoot;
        so.FindProperty("bookCraftRoot").objectReferenceValue = wordAssemblyRoot.Find("book_craft") as RectTransform;
        so.FindProperty("inputSlot1").objectReferenceValue = wordAssemblyRoot.Find("inputSlot1") as RectTransform;
        so.FindProperty("inputSlot2").objectReferenceValue = wordAssemblyRoot.Find("inputSlot2") as RectTransform;
        so.FindProperty("slideOutDuration").floatValue = 0.25f;

        AudioSource wordAudio = magicStoneRect.GetComponent<AudioSource>();
        if (wordAudio == null) wordAudio = magicStoneRect.gameObject.AddComponent<AudioSource>();
        wordAudio.playOnAwake = false;
        wordAudio.spatialBlend = 0f;
        so.FindProperty("wordAudioSource").objectReferenceValue = wordAudio;

        Transform bookCraft = wordAssemblyRoot.Find("book_craft");
        Button soundButton = null;
        if (bookCraft != null)
        {
            Transform soundChild = bookCraft.Find("sound");
            if (soundChild == null) soundChild = bookCraft.Find("sound (1)");
            soundButton = CreateIconButton(
                bookCraft,
                soundChild != null ? soundChild.name : "sound",
                LoadActionIcon(SoundIconSpriteName),
                new Vector2(0f, -896f),
                new Vector2(430.9751f, 421.2532f));
        }
        so.FindProperty("assemblySoundButton").objectReferenceValue = soundButton;
        if (soundButton == null)
        {
            Debug.Log("[PracticeNightSetup] No 'sound'/'sound (1)' child under book_craft — " +
                "assemblySoundButton left null (feature inert until an art button exists).");
        }

        CreateResultControls(wordAssemblyRoot.Find("book_craft_pa"));
        CreateResultControls(wordAssemblyRoot.Find("book_craft_ga"));

        so.ApplyModifiedProperties();
    }

    private static void CreateResultControls(Transform resultPage)
    {
        if (resultPage == null) return;

        CreateIconButton(
            resultPage,
            "result_sound",
            LoadActionIcon(SoundIconSpriteName),
            new Vector2(-259.6886f, -859.2484f),
            new Vector2(430.9751f, 421.2532f));
        CreateIconButton(
            resultPage,
            "result_mic",
            LoadActionIcon(MicIconSpriteName),
            new Vector2(302.8691f, -859.25f),
            new Vector2(431.4417f, 421.25f));
    }

    private static Button CreateIconButton(
        Transform parent,
        string buttonName,
        Sprite icon,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Transform existing = parent.Find(buttonName);
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        bool wasCreated = existing == null;
        if (wasCreated) buttonObject.transform.SetParent(parent, false);
        buttonObject.layer = parent.gameObject.layer;

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        if (wasCreated)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;
        }

        Transform labelTransform = buttonObject.transform.Find("Label");
        if (labelTransform != null) Object.DestroyImmediate(labelTransform.gameObject);

        Image image = buttonObject.GetComponent<Image>();
        if (image == null) image = buttonObject.AddComponent<Image>();
        image.sprite = icon;
        image.color = Color.white;
        image.raycastTarget = true;
        image.preserveAspect = false;

        Button button = buttonObject.GetComponent<Button>();
        if (button == null) button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Sprite LoadActionIcon(string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ActionIconSheetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == spriteName) return sprite;
        }

        Debug.LogError($"[PracticeNightSetup] Could not find icon sprite '{spriteName}' in {ActionIconSheetPath}.");
        return null;
    }

    private static Transform EnsurePracticeScarecrow(Transform objectsRoot)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PracticeScarecrowSpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[PracticeNightSetup] Practice scarecrow sprite not found at {PracticeScarecrowSpritePath}.");
            GameObject fallback = GameObject.Find("dummy_idle_DOWN_0");
            return fallback != null ? fallback.transform : null;
        }

        GameObject existing = GameObject.Find("PracticeScarecrow");
        GameObject previousScarecrow = GameObject.Find("dummy_idle_DOWN_0");
        bool created = existing == null;

        if (created)
        {
            GameObject prefab = GetOrCreatePracticeScarecrowPrefab(sprite);
            Transform parent = previousScarecrow != null ? previousScarecrow.transform.parent : objectsRoot;
            existing = InstantiatePrefabRobust(prefab, parent);
            if (existing == null)
            {
                return null;
            }
            existing.name = "PracticeScarecrow";

            if (previousScarecrow != null)
            {
                existing.transform.SetPositionAndRotation(
                    previousScarecrow.transform.position,
                    previousScarecrow.transform.rotation);

                SpriteRenderer previousRenderer = previousScarecrow.GetComponent<SpriteRenderer>();
                float sourceHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
                float targetHeight = previousRenderer != null
                    ? Mathf.Max(0.1f, previousRenderer.bounds.size.y)
                    : 1.4f;
                float scale = targetHeight / sourceHeight;
                existing.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = existing.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        if (previousScarecrow != null)
        {
            SpriteRenderer previousRenderer = previousScarecrow.GetComponent<SpriteRenderer>();
            if (previousRenderer != null)
            {
                renderer.sortingLayerID = previousRenderer.sortingLayerID;
                renderer.sortingOrder = previousRenderer.sortingOrder;
            }
            previousScarecrow.SetActive(false);
        }

        return existing.transform;
    }

    private static GameObject GetOrCreatePracticeScarecrowPrefab(Sprite sprite)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PracticeScarecrowPrefabPath);
        if (prefab != null) return prefab;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Practice"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Practice");
        }

        GameObject template = new GameObject("PracticeScarecrow", typeof(SpriteRenderer));
        template.GetComponent<SpriteRenderer>().sprite = sprite;
        prefab = PrefabUtility.SaveAsPrefabAsset(template, PracticeScarecrowPrefabPath);
        Object.DestroyImmediate(template);
        return prefab;
    }

    // R2 Delta B: idle hero, movement disabled. Objects/character has one child in this scene
    // (spritesheet_7, with CharacterRunDirection) — disable known walk/input drivers on it and log
    // its path. Final facing/position is a manual nudge, same as Crow/OwlGuide.
    private static Animator SetupHeroIdle(Transform objectsRoot)
    {
        Transform characterGroup = objectsRoot != null ? objectsRoot.Find("character") : null;
        if (characterGroup == null || characterGroup.childCount == 0)
        {
            Debug.LogWarning("[PracticeNightSetup] No usable hero found under Objects/character — skipping hero idle setup.");
            return null;
        }

        characterGroup.gameObject.SetActive(true);
        Transform hero = characterGroup.GetChild(0);
        hero.gameObject.SetActive(true);

        int disabled = 0;
        disabled += DisableAll<QuestAutoWalker>(hero);
        disabled += DisableAll<PatrolWalk>(hero);
        disabled += DisableAll<CharacterRunDirection>(hero);

        Animator animator = hero.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetInteger("orientation", 4);
            animator.SetFloat("speed", 0f);
        }

        Debug.Log($"[PracticeNightSetup] Hero: {GetPath(hero)} — disabled {disabled} movement component(s). " +
            "Nudge position/idle-facing-down sprite manually if needed.");
        return animator;
    }

    private static int DisableAll<T>(Transform root) where T : Behaviour
    {
        T[] comps = root.GetComponentsInChildren<T>(true);
        foreach (T c in comps) c.enabled = false;
        return comps.Length;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    // R2 Delta C: make sure both scenes the boss's WorldMap button / OnWordSuccess need are in
    // Build Settings. Safe to re-run.
    [MenuItem("Tools/Practice/Ensure Build Scenes")]
    public static void EnsureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        changed |= EnsureSceneInList(scenes, PracticeNightScenePath);
        changed |= EnsureSceneInList(scenes, WorldMapScenePath);

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        List<string> logged = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            logged.Add(s.path + (s.enabled ? "" : " (disabled)"));
        }
        Debug.Log("[PracticeNightSetup] Build scenes: " + string.Join(", ", logged));
    }

    [MenuItem("Tools/Practice/Debug Reimport Owl")]
    public static void DebugReimportOwl()
    {
        AssetDatabase.ImportAsset(OwlGuidePrefabPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive | ImportAssetOptions.DontDownloadFromCacheServer);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject owlGo = AssetDatabase.LoadAssetAtPath<GameObject>(OwlGuidePrefabPath);
        GameObject crowGo = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefabPath);
        Object mainAsset = AssetDatabase.LoadMainAssetAtPath(OwlGuidePrefabPath);
        Debug.Log($"[Debug] owlGo={(owlGo != null ? owlGo.name : "NULL")} crowGo={(crowGo != null ? crowGo.name : "NULL")} mainAsset={(mainAsset != null ? mainAsset.name : "NULL")} owlGuid={AssetDatabase.AssetPathToGUID(OwlGuidePrefabPath)}");
    }

    private static bool EnsureSceneInList(List<EditorBuildSettingsScene> scenes, string path)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path)
            {
                if (!scenes[i].enabled)
                {
                    scenes[i] = new EditorBuildSettingsScene(path, true);
                    return true;
                }
                return false;
            }
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        return true;
    }
}
