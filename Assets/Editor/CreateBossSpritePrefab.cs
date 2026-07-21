using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateBossSpritePrefab
{
    private const string BossScenePath = "Assets/Scenes/region 1/boss.unity";
    private const string AttackFramesPath = "Assets/Art/boss/boss_attack";
    private const string IdleFramesPath = "Assets/Art/boss/boss_idle";
    private const string AnimationFolder = "Assets/Art/boss/Animations";
    private const string AttackClipPath = AnimationFolder + "/BossAttack.anim";
    private const string IdleClipPath = AnimationFolder + "/BossIdle.anim";
    private const string ControllerPath = "Assets/Prefabs/Boss/BossSprite.controller";
    private const string PrefabPath = "Assets/Prefabs/Boss/BossSprite.prefab";
    private const float FrameRate = 12f;

    [MenuItem("Tools/Boss/Create Sprite Boss Prefab")]
    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder(AnimationFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/boss", "Animations");
        }

        List<Sprite> attackFrames = LoadFrames(AttackFramesPath);
        List<Sprite> idleFrames = LoadFrames(IdleFramesPath);
        AnimationClip attackClip = CreateSpriteClip(AttackClipPath, attackFrames, false);
        AnimationClip idleClip = CreateSpriteClip(IdleClipPath, idleFrames, true);

        AnimationUtility.SetAnimationEvents(attackClip, new[]
        {
            new AnimationEvent
            {
                functionName = nameof(BossSpriteIntro.RevealBookAfterIntro),
                time = Mathf.Max(0.01f, attackClip.length - 0.001f)
            }
        });

        AnimatorController controller = CreateController(attackClip, idleClip);
        GameObject prefab = CreatePrefab(controller, attackFrames[0]);
        AddPrefabToActiveBossScene(prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateBossSpritePrefab] Created {PrefabPath} with {attackFrames.Count} attack frames and {idleFrames.Count} idle frames.");
    }

    private static List<Sprite> LoadFrames(string framesFolder)
    {
        string absoluteFolder = Path.GetFullPath(framesFolder);
        string[] frameFiles = Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(frameFiles, StringComparer.Ordinal);

        if (frameFiles.Length == 0)
        {
            throw new InvalidOperationException($"No PNG frames were found in {framesFolder}.");
        }

        List<Sprite> frames = new List<Sprite>(frameFiles.Length);
        for (int index = 0; index < frameFiles.Length; index++)
        {
            string assetPath = framesFolder + "/" + Path.GetFileName(frameFiles[index]);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer is unavailable for {assetPath}.");
            }

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (frame == null)
            {
                throw new InvalidOperationException($"Sprite import failed for {assetPath}.");
            }

            frames.Add(frame);
        }

        return frames;
    }

    private static AnimationClip CreateSpriteClip(string clipPath, List<Sprite> frames, bool shouldLoop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = FrameRate;
        clip.wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Count];
        for (int index = 0; index < frames.Count; index++)
        {
            keyframes[index] = new ObjectReferenceKeyframe
            {
                time = index / FrameRate,
                value = frames[index]
            };
        }

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("Boss", typeof(Image), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = shouldLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(AnimationClip attackClip, AnimationClip idleClip)
    {
        AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existingController != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState attackState = stateMachine.AddState("BossAttack");
        AnimatorState idleState = stateMachine.AddState("BossIdle");
        attackState.motion = attackClip;
        idleState.motion = idleClip;
        stateMachine.defaultState = attackState;

        AnimatorStateTransition transition = attackState.AddTransition(idleState);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject CreatePrefab(AnimatorController controller, Sprite firstFrame)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        GameObject root = new GameObject(
            "Boss Sprite",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(Animator),
            typeof(BossSpriteIntro));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -500;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Animator animator = root.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        GameObject visual = new GameObject("Boss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visual.transform.SetParent(root.transform, false);
        RectTransform visualRect = visual.GetComponent<RectTransform>();
        visualRect.anchorMin = new Vector2(0.5f, 0.5f);
        visualRect.anchorMax = new Vector2(0.5f, 0.5f);
        visualRect.pivot = new Vector2(0.5f, 0.5f);

        Image image = visual.GetComponent<Image>();
        image.sprite = firstFrame;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.SetNativeSize();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void AddPrefabToActiveBossScene(GameObject prefab)
    {
        if (prefab == null)
        {
            throw new InvalidOperationException("Boss prefab could not be loaded after creation.");
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (!string.Equals(scene.path, BossScenePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Open {BossScenePath} before creating the boss scene instance.");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Boss Sprite" || root.name == "BossSprite")
            {
                UnityEngine.Object.DestroyImmediate(root);
                break;
            }
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "Boss Sprite";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
