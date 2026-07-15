using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Makes the crow word-build round match CutScene_bear's feedback systems while keeping
/// its own story target: gaa is correct and paa is the real-but-wrong branch.
public static class UpgradeCutSceneGaParity
{
    const string BearScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string GaScenePath = "Assets/Scenes/region 1/CutScene_ga.unity";
    const string IncorrectScenePath = "Assets/Scenes/region 1/Success_ta_incorrect.unity";
    const string ThowPrefabPath = "Assets/Art/quest_map/bear/Thow.prefab";
    const string CrowPrefabPath = "Assets/Art/quest_map/Crow.prefab";
    const string StoneCrowFramesPath = "Assets/Art/quest_map/quest_ga/ga_stone_cropped";
    const string CountdownClipPath = "Assets/Audio/countdown_beep.wav";

    [MenuItem("Tools/Crow/Upgrade CutScene_ga Parity")]
    public static void Run()
    {
        if (!OpenScenesAreClean()) return;

        UpgradePuzzleScene();
        UpgradeResultScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[GaParity] DONE: CutScene_ga puzzle systems + paa incorrect scene upgraded");
    }

    [MenuItem("Tools/Crow/Upgrade Ga Success Results")]
    public static void RunGaSuccessResults()
    {
        if (!OpenScenesAreClean()) return;

        UpgradeResultScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[GaParity] DONE: ga result scenes upgraded");
    }

    [MenuItem("Tools/Crow/Repair Ga Smoke + Timer")]
    public static void RunGaSmokeTimerRepair()
    {
        if (!OpenScenesAreClean()) return;

        RepairGaSmokeAndTimer();
        AssetDatabase.SaveAssets();
        Debug.Log("[GaParity] DONE: CutScene_ga smoke timing repaired without time_root");
    }

    static void UpgradeResultScenes()
    {
        UpgradeIncorrectScene();
        AddStarHudToSuccessScenes.InstallGaFlow();
    }

    static bool OpenScenesAreClean()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isDirty) continue;
            Debug.LogError($"[GaParity] Save or discard the open scene '{scene.name}' before running this tool.");
            return false;
        }

        return true;
    }

    static void UpgradePuzzleScene()
    {
        Scene targetScene = EditorSceneManager.OpenScene(GaScenePath, OpenSceneMode.Single);
        Transform targetCanvas = FindInScene(targetScene, "Canvas")?.transform;
        if (targetCanvas == null) throw new MissingReferenceException("[GaParity] Canvas missing in CutScene_ga");

        Scene bearScene = EditorSceneManager.OpenScene(BearScenePath, OpenSceneMode.Additive);
        GameObject sourceStone = FindInScene(bearScene, "stone2");
        GameObject sourceFog = FindInScene(bearScene, "FogOverlay");
        GameObject sourceCloud = FindInScene(bearScene, "CloudOverlay");
        GameObject sourceStars = FindInScene(bearScene, "star_hud");
        GameObject sourceLock = FindInScene(bearScene, "lock_overlay");
        GameObject sourceTimerRoot = FindTimerRoot(bearScene);

        if (sourceStone == null || sourceFog == null || sourceCloud == null ||
            sourceStars == null || sourceLock == null || sourceTimerRoot == null)
        {
            EditorSceneManager.CloseScene(bearScene, true);
            throw new MissingReferenceException("[GaParity] CutScene_bear is missing a required source object");
        }

        CopyStoneVisual(sourceStone, FindInScene(targetScene, "stone2"));
        GameObject cloud = ReplaceWithClone(sourceCloud, targetCanvas, targetScene, "CloudOverlay");
        GameObject fog = ReplaceWithClone(sourceFog, targetCanvas, targetScene, "FogOverlay");
        GameObject stars = ReplaceWithClone(sourceStars, targetCanvas, targetScene, "star_hud");
        GameObject lockOverlay = ReplaceWithClone(sourceLock, targetCanvas, targetScene, "lock_overlay");
        GameObject timerRoot = ReplaceWithClone(sourceTimerRoot, targetCanvas, targetScene, "star_root");
        RemoveDirectChildren(targetCanvas, "time_root");

        EditorSceneManager.CloseScene(bearScene, true);

        ConfigureRecipe(targetScene, targetCanvas);
        ConfigureTimer(targetScene, targetCanvas, timerRoot);
        ConfigureFogOrder(targetCanvas, cloud, fog);
        ConfigureStars(stars);
        ConfigureLock(lockOverlay);

        // Award art should sit over gameplay; the red alert should cover everything while locked.
        stars.transform.SetSiblingIndex(targetCanvas.childCount - 1);
        lockOverlay.transform.SetSiblingIndex(targetCanvas.childCount - 1);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        ValidateTimerStructure(targetScene, targetCanvas);
        Debug.Log("[GaParity] CutScene_ga: gaa/paa recipe, star_root timer, stars, two smoke layers and lock saved");
    }

    static void RepairGaSmokeAndTimer()
    {
        Scene targetScene = EditorSceneManager.OpenScene(GaScenePath, OpenSceneMode.Single);
        Transform targetCanvas = FindInScene(targetScene, "Canvas")?.transform;
        if (targetCanvas == null) throw new MissingReferenceException("[GaParity] Canvas missing in CutScene_ga");

        Scene bearScene = EditorSceneManager.OpenScene(BearScenePath, OpenSceneMode.Additive);
        GameObject sourceTimerRoot = FindTimerRoot(bearScene);
        GameObject sourceCloud = FindInScene(bearScene, "CloudOverlay");
        GameObject sourceFog = FindInScene(bearScene, "FogOverlay");
        if (sourceTimerRoot == null || sourceCloud == null || sourceFog == null)
        {
            EditorSceneManager.CloseScene(bearScene, true);
            throw new MissingReferenceException("[GaParity] CutScene_bear smoke/timer source is incomplete");
        }

        GameObject cloud = FindInScene(targetScene, "CloudOverlay");
        GameObject fog = FindInScene(targetScene, "FogOverlay");
        if (cloud == null) cloud = ReplaceWithClone(sourceCloud, targetCanvas, targetScene, "CloudOverlay");
        if (fog == null) fog = ReplaceWithClone(sourceFog, targetCanvas, targetScene, "FogOverlay");

        CopyFogTiming(sourceCloud, cloud);
        CopyFogTiming(sourceFog, fog);
        GameObject timerRoot = ReplaceWithClone(sourceTimerRoot, targetCanvas, targetScene, "star_root");
        RemoveDirectChildren(targetCanvas, "time_root");

        EditorSceneManager.CloseScene(bearScene, true);

        ConfigureTimer(targetScene, targetCanvas, timerRoot);
        ConfigureFogOrder(targetCanvas, cloud, fog);
        ValidateTimerStructure(targetScene, targetCanvas);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        Debug.Log("[GaParity] CutScene_ga: smoke now starts with word assembly via star_root timer");
    }

    static GameObject ReplaceWithClone(GameObject source, Transform parent, Scene targetScene, string name)
    {
        RemoveDirectChildren(parent, name);

        GameObject clone = Object.Instantiate(source);
        clone.name = name;
        clone.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        clone.transform.SetParent(parent, false);
        CopyRect(source.transform as RectTransform, clone.transform as RectTransform);
        clone.SetActive(true);
        return clone;
    }

    static void RemoveDirectChildren(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) Object.DestroyImmediate(child.gameObject);
        }
    }

    static void CopyStoneVisual(GameObject source, GameObject target)
    {
        if (target == null) throw new MissingReferenceException("[GaParity] stone2 missing in CutScene_ga");

        CopyRect(source.transform as RectTransform, target.transform as RectTransform);
        Image sourceImage = source.GetComponent<Image>();
        Image targetImage = target.GetComponent<Image>();
        if (sourceImage == null || targetImage == null)
            throw new MissingReferenceException("[GaParity] stone2 Image missing");

        targetImage.sprite = sourceImage.sprite;
        targetImage.material = sourceImage.material;
        targetImage.color = sourceImage.color;
        targetImage.type = sourceImage.type;
        targetImage.preserveAspect = sourceImage.preserveAspect;
        EditorUtility.SetDirty(targetImage);
        EditorUtility.SetDirty(target.transform);
    }

    static void ConfigureRecipe(Scene scene, Transform canvas)
    {
        Transform gaaBook = canvas.Find("book_craft_ga");
        Transform paaBook = canvas.Find("book_craft_pa") ?? canvas.Find("book_craft_ta");
        if (gaaBook == null || paaBook == null)
            throw new MissingReferenceException("[GaParity] gaa/paa result roots missing in CutScene_ga");

        paaBook.name = "book_craft_pa";

        foreach (MagicStonePuzzleController puzzle in ComponentsInScene<MagicStonePuzzleController>(scene))
        {
            SerializedObject so = new SerializedObject(puzzle);
            SetString(so, "targetWord", "กา");
            SetStringArray(so, "knownWords", "ปา");
            SetStringArray(so, "stoneObjectNames", "stone1", "stone2", "stone3");
            SetStringArray(so, "stoneLetters", "ก", "ป", "า");
            SetString(so, "nextSceneName", "Assets/Scenes/region 1/Success_ga_correct.unity");
            SetObject(so, "bookCraftCrowRoot", gaaBook as RectTransform);
            SetObject(so, "bookCraftAltRoot", paaBook as RectTransform);
            SetString(so, "altWord", "ปา");
            SetString(so, "altWordId", "paa");
            SetString(so, "altSceneName", "Assets/Scenes/region 1/Success_ta_incorrect.unity");
            SetString(so, "targetWordId", "kaa");
            SetStringArray(so, "stonePlacementLineIds", "gameplay_ko", "gameplay_po", "gameplay_aa");
            SetStringArray(so, "soundPlaybackLineIds", "gameplay_ko", "gameplay_aa", "gameplay_kaa");
            SetStringArray(so, "altSoundPlaybackLineIds", "gameplay_po", "gameplay_aa", "gameplay_paa");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(puzzle);
        }
    }

    static void ConfigureTimer(Scene scene, Transform canvas, GameObject timerRoot)
    {
        if (timerRoot == null) throw new MissingReferenceException("[GaParity] star_root timer holder missing");
        timerRoot.name = "star_root";
        timerRoot.transform.SetParent(canvas, false);
        RemoveDirectChildren(canvas, "time_root");

        WordAssemblyTimer timer = timerRoot.GetComponent<WordAssemblyTimer>();
        if (timer == null) timer = timerRoot.AddComponent<WordAssemblyTimer>();
        foreach (WordAssemblyTimer other in ComponentsInScene<WordAssemblyTimer>(scene).ToArray())
        {
            if (other != timer) Object.DestroyImmediate(other);
        }

        AudioSource[] audioSources = timerRoot.GetComponents<AudioSource>();
        AudioSource audio = audioSources.Length > 0 ? audioSources[0] : timerRoot.AddComponent<AudioSource>();
        for (int i = 1; i < audioSources.Length; i++) Object.DestroyImmediate(audioSources[i]);
        audio.playOnAwake = false;
        audio.loop = false;

        SerializedObject so = new SerializedObject(timer);
        SetFloat(so, "totalSeconds", 30f);
        SetFloat(so, "countdownAt", 10f);
        SetFloat(so, "popDuration", 0.5f);
        SetObject(so, "audioSource", audio);
        SetObject(so, "countdownClip", AssetDatabase.LoadAssetAtPath<AudioClip>(CountdownClipPath));
        SetObject(so, "popRoot", timerRoot.transform as RectTransform);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(timer);
        EditorUtility.SetDirty(audio);
        EditorUtility.SetDirty(timerRoot);
    }

    static GameObject FindTimerRoot(Scene scene)
    {
        WordAssemblyTimer named = ComponentsInScene<WordAssemblyTimer>(scene)
            .FirstOrDefault(timer => timer.gameObject.name == "star_root");
        if (named != null) return named.gameObject;

        return ComponentsInScene<WordAssemblyTimer>(scene).FirstOrDefault()?.gameObject;
    }

    static void CopyFogTiming(GameObject source, GameObject target)
    {
        FogController sourceController = source.GetComponent<FogController>();
        FogController targetController = target.GetComponent<FogController>();
        if (sourceController == null || targetController == null)
            throw new MissingReferenceException("[GaParity] fog controller missing while copying timing");

        SerializedObject sourceSo = new SerializedObject(sourceController);
        SerializedObject targetSo = new SerializedObject(targetController);
        foreach (string field in new[] { "fogDuration", "urgency", "pulseWindow", "pulseDecay" })
        {
            SerializedProperty sourceProperty = sourceSo.FindProperty(field);
            SerializedProperty targetProperty = targetSo.FindProperty(field);
            if (sourceProperty != null && targetProperty != null)
                targetProperty.floatValue = sourceProperty.floatValue;
        }

        SerializedProperty sourceCurve = sourceSo.FindProperty("fogCurve");
        SerializedProperty targetCurve = targetSo.FindProperty("fogCurve");
        if (sourceCurve != null && targetCurve != null)
            targetCurve.animationCurveValue = sourceCurve.animationCurveValue;

        targetSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(targetController);
    }

    static void ValidateTimerStructure(Scene scene, Transform canvas)
    {
        int starRootCount = 0;
        int timeRootCount = 0;
        for (int i = 0; i < canvas.childCount; i++)
        {
            string childName = canvas.GetChild(i).name;
            if (childName == "star_root") starRootCount++;
            if (childName == "time_root") timeRootCount++;
        }

        WordAssemblyTimer[] timers = ComponentsInScene<WordAssemblyTimer>(scene).ToArray();
        bool timerOnStarRoot = timers.Length == 1 && timers[0].gameObject.name == "star_root";
        if (starRootCount != 1 || timeRootCount != 0 || !timerOnStarRoot)
        {
            throw new System.InvalidOperationException(
                $"[GaParity] invalid timer structure: star_root={starRootCount}, " +
                $"time_root={timeRootCount}, timers={timers.Length}, timerOnStarRoot={timerOnStarRoot}");
        }
    }

    static void ConfigureFogOrder(Transform canvas, GameObject cloud, GameObject fog)
    {
        ConfigureFog(cloud);
        ConfigureFog(fog);

        Transform background = canvas.Find("background");
        int firstFogIndex = background != null ? background.GetSiblingIndex() + 1 : 1;
        cloud.transform.SetSiblingIndex(firstFogIndex);
        fog.transform.SetSiblingIndex(cloud.transform.GetSiblingIndex() + 1);
    }

    static void ConfigureFog(GameObject overlay)
    {
        Stretch(overlay.transform as RectTransform);
        RawImage image = overlay.GetComponent<RawImage>();
        FogController controller = overlay.GetComponent<FogController>();
        if (image == null || controller == null)
            throw new MissingReferenceException($"[GaParity] {overlay.name} fog components missing");

        image.raycastTarget = false;
        SerializedObject so = new SerializedObject(controller);
        SetObject(so, "fogImage", image);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(controller);
    }

    static void ConfigureStars(GameObject stars)
    {
        StarHud hud = stars.GetComponent<StarHud>();
        if (hud == null) throw new MissingReferenceException("[GaParity] cloned star_hud has no StarHud");

        SerializedObject so = new SerializedObject(hud);
        SetBool(so, "resetOnAwake", true);
        SetBool(so, "recapOnStart", false);
        SetBool(so, "fadeOutAfterRecap", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);
    }

    static void ConfigureLock(GameObject overlay)
    {
        Stretch(overlay.transform as RectTransform);
        RawImage vignette = overlay.GetComponent<RawImage>();
        AudioSource audio = overlay.GetComponent<AudioSource>();
        MisassemblyLock lockComponent = overlay.GetComponent<MisassemblyLock>();
        if (vignette == null || audio == null || lockComponent == null)
            throw new MissingReferenceException("[GaParity] cloned lock_overlay components missing");

        vignette.raycastTarget = false;
        audio.playOnAwake = false;
        audio.loop = false;

        SerializedObject so = new SerializedObject(lockComponent);
        SetObject(so, "vignette", vignette);
        SetObject(so, "audioSource", audio);
        SetObject(so, "lockIcon", null);
        SetObject(so, "unlockIcon", null);
        SetFloat(so, "lockoutDuration", 3f);
        SerializedProperty burst = so.FindProperty("burst");
        if (burst != null) burst.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lockComponent);
    }

    static void UpgradeIncorrectScene()
    {
        Scene scene = EditorSceneManager.OpenScene(IncorrectScenePath, OpenSceneMode.Single);
        Transform canvas = FindInScene(scene, "Canvas")?.transform;
        if (canvas == null) throw new MissingReferenceException("[GaParity] Canvas missing in Success_ta_incorrect");

        WireThowIncorrectScene(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GaParity] Success_ta_incorrect: Thow + stone crow + one-star no-owl retry return saved");
    }

    internal static void WireThowIncorrectScene(Transform canvas)
    {
        foreach (string oldName in new[] { "Bear", "Eye", "Thow", "ga_left", "ga_right", "OwlRoot", "OwlEpilogue" })
        {
            Transform old = canvas.Find(oldName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        foreach (SuccessPaOwlEpilogue oldEpilogue in ComponentsInScene<SuccessPaOwlEpilogue>(canvas.gameObject.scene).ToArray())
            Object.DestroyImmediate(oldEpilogue);

        foreach (SuccessGaReturn oldReturn in ComponentsInScene<SuccessGaReturn>(canvas.gameObject.scene).ToArray())
            Object.DestroyImmediate(oldReturn);

        GameObject crow = EnsureStoneCrow(canvas);
        CanvasGroup crowFade = crow.GetComponent<CanvasGroup>();

        GameObject thowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThowPrefabPath);
        if (thowPrefab == null) throw new MissingReferenceException("[GaParity] Thow.prefab missing");
        GameObject thow = (GameObject)PrefabUtility.InstantiatePrefab(thowPrefab, canvas);
        thow.name = "Thow";
        thow.layer = canvas.gameObject.layer;

        RectTransform rect = thow.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = new Vector3(2f, 2f, 1f);

        Image image = thow.GetComponent<Image>();
        if (image != null) image.raycastTarget = false;
        CanvasGroup thowFade = thow.GetComponent<CanvasGroup>();
        if (thowFade == null) thowFade = thow.AddComponent<CanvasGroup>();

        BearCutscene cutscene = thow.GetComponent<BearCutscene>();
        if (cutscene == null) cutscene = thow.AddComponent<BearCutscene>();
        SerializedObject so = new SerializedObject(cutscene);
        SetObject(so, "bear", thow.GetComponent<Animator>());
        SetObject(so, "path", null);
        SetObject(so, "owlGreeting", null);
        SetBool(so, "playOnStart", true);
        SetBool(so, "playPaaThrowSfxOnStart", true);
        SetInt(so, "startWaypoint", -1);
        SetFloat(so, "fadeInDuration", 0f);
        SetFloat(so, "cropLeftPixels", 0f);
        SetObjectArray(so, "alsoFade", crowFade);
        SetFloat(so, "endFadeOutDuration", 0.6f);
        SetString(so, "nextScene", "CutScene_ga");
        SetBool(so, "resumeForestAtBeat2", false);
        SetObject(so, "owlEpilogue", null);
        SetBool(so, "setPuzzleRetryFlags", true);
        SerializedProperty sequence = so.FindProperty("sequence");
        sequence.arraySize = 1;
        SetStep(sequence.GetArrayElementAtIndex(0), "Thow");
        so.ApplyModifiedPropertiesWithoutUndo();

        crow.transform.SetSiblingIndex(1);
        thow.transform.SetSiblingIndex(Mathf.Min(crow.transform.GetSiblingIndex() + 1, canvas.childCount - 1));
        EditorUtility.SetDirty(cutscene);
        EditorUtility.SetDirty(thowFade);
    }

    static GameObject EnsureStoneCrow(Transform canvas)
    {
        Transform existing = canvas.Find("Crow");
        GameObject crow;
        if (existing != null)
        {
            crow = existing.gameObject;
        }
        else
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefabPath);
            if (prefab == null) throw new MissingReferenceException("[GaParity] Crow.prefab missing");
            crow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            crow.name = "Crow";
        }

        CrowEntranceCutscene entrance = crow.GetComponent<CrowEntranceCutscene>();
        if (entrance != null)
        {
            entrance.enabled = false;
            EditorUtility.SetDirty(entrance);
        }
        Animator animator = crow.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
            EditorUtility.SetDirty(animator);
        }
        CanvasGroup fade = crow.GetComponent<CanvasGroup>();
        if (fade == null) fade = crow.AddComponent<CanvasGroup>();
        fade.alpha = 1f;
        fade.blocksRaycasts = false;
        EditorUtility.SetDirty(fade);

        Sprite lastStone = AssetDatabase.FindAssets("t:Sprite", new[] { StoneCrowFramesPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .LastOrDefault(sprite => sprite != null);
        Image image = crow.GetComponent<Image>();
        if (image != null && lastStone != null)
        {
            image.sprite = lastStone;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }

        RectTransform rect = crow.transform as RectTransform;
        rect.anchoredPosition = Vector2.zero;
        if (rect != null) EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(crow);
        crow.SetActive(true);
        return crow;
    }

    static void SetStep(SerializedProperty step, string state)
    {
        step.FindPropertyRelative("state").stringValue = state;
        step.FindPropertyRelative("toWaypoint").intValue = -1;
        step.FindPropertyRelative("timing").enumValueIndex = 1; // PlayClip
        step.FindPropertyRelative("seconds").floatValue = 0f;
        step.FindPropertyRelative("speed").floatValue = 1f;
        step.FindPropertyRelative("speedEnd").floatValue = 0f;
        step.FindPropertyRelative("scale").floatValue = 1f;
        step.FindPropertyRelative("fadeSeconds").floatValue = 0f;
        step.FindPropertyRelative("moveDelay").floatValue = 0f;
    }

    static IEnumerable<T> ComponentsInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void CopyRect(RectTransform source, RectTransform target)
    {
        if (source == null || target == null) return;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    static void Stretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.stringValue = value;
    }

    static void SetStringArray(SerializedObject so, string name, params string[] values)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    static void SetObjectArray(SerializedObject so, string name, params Object[] values)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }
}
