using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-shot scene setup for practice_night: removes clone leftovers (BearIntro*, BossUI, the
/// reference_forest quest-path waypoints), strips the stray MagicStonePuzzleController instances
/// the WordAssembly prefab carries over from Boss.unity, wires a PracticeWordAssembly onto
/// magic_stone, and instantiates + wires the Crow and OwlGuide actors onto a new
/// PracticeNightController. Also (R2) sets the hero to an idle facing-camera pose with movement
/// disabled, wires the R2 fields (assemblyUiRoot/assemblySoundButton/scarecrowRenderer/
/// bookPopRoot), and ensures practice_night + WorldMap are in Build Settings.
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
    private const string ThaiFontPath = "Assets/Fonts/LeelawUI SDF.asset";
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

        WireController(controller, wordAssemblyComponent, crow, owl, wordAssembly.transform, heroAnimator);
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

        crow.SetActive(false);
        return crow;
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
        GameObject crow, GameObject owl, Transform wordAssemblyRoot, Animator heroAnimator)
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

        GameObject scarecrowGo = GameObject.Find("dummy_idle_DOWN_0");
        Transform scarecrowTransform = scarecrowGo != null ? scarecrowGo.transform : null;
        so.FindProperty("scarecrowWorldTarget").objectReferenceValue = scarecrowTransform;

        // R2: scarecrow SpriteRenderer (fade-in) and the book_craft RectTransform (pop-in).
        so.FindProperty("scarecrowRenderer").objectReferenceValue =
            scarecrowGo != null ? scarecrowGo.GetComponent<SpriteRenderer>() : null;
        so.FindProperty("bookPopRoot").objectReferenceValue = wordAssemblyRoot.Find("book_craft") as RectTransform;

        if (owl != null)
        {
            so.FindProperty("owl").objectReferenceValue = owl.GetComponent<OwlGuideAnimator>();
            AudioSource owlAudio = owl.GetComponent<AudioSource>();
            if (owlAudio == null) owlAudio = owl.AddComponent<AudioSource>();
            owlAudio.playOnAwake = false;
            so.FindProperty("owlAudioSource").objectReferenceValue = owlAudio;
        }
        else
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
        eventA.FindPropertyRelative("owlLineClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(KaaOwlClipPath);

        SerializedProperty eventB = events.GetArrayElementAtIndex(1);
        eventB.FindPropertyRelative("kind").enumValueIndex = 1; // CrowCircling
        eventB.FindPropertyRelative("targetWord").stringValue = "ปา";
        eventB.FindPropertyRelative("resultPage").objectReferenceValue = wordAssemblyRoot.Find("book_craft_pa") as RectTransform;
        eventB.FindPropertyRelative("wordClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PaaWordClipPath);
        eventB.FindPropertyRelative("owlLineClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PaaOwlClipPath);

        so.ApplyModifiedProperties();
    }

    // R2 Delta A: assemblyUiRoot (slide-out root) + assemblySoundButton (hint button, if art exists).
    private static void WireWordAssembly(PracticeWordAssembly wordAssemblyComponent, Transform wordAssemblyRoot, RectTransform magicStoneRect)
    {
        SerializedObject so = new SerializedObject(wordAssemblyComponent);

        RectTransform uiRoot = magicStoneRect.parent as RectTransform;
        so.FindProperty("assemblyUiRoot").objectReferenceValue = uiRoot;

        Transform bookCraft = wordAssemblyRoot.Find("book_craft");
        Button soundButton = null;
        if (bookCraft != null)
        {
            Transform soundChild = bookCraft.Find("sound");
            if (soundChild == null) soundChild = bookCraft.Find("sound (1)");
            soundButton = soundChild != null
                ? soundChild.GetComponent<Button>() ?? soundChild.gameObject.AddComponent<Button>()
                : CreateAssemblySoundButton(bookCraft);
        }
        so.FindProperty("assemblySoundButton").objectReferenceValue = soundButton;
        if (soundButton == null)
        {
            Debug.Log("[PracticeNightSetup] No 'sound'/'sound (1)' child under book_craft — " +
                "assemblySoundButton left null (feature inert until an art button exists).");
        }

        so.ApplyModifiedProperties();
    }

    private static Button CreateAssemblySoundButton(Transform bookCraft)
    {
        GameObject buttonObject = new GameObject("sound", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(bookCraft, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.84f, 0.86f);
        buttonRect.anchorMax = new Vector2(0.84f, 0.86f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(360f, 150f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontPath);
        label.text = "ฟังเสียง";
        label.fontSize = 86f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.12f, 0.28f, 0.68f, 1f);
        label.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = label;
        return button;
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
