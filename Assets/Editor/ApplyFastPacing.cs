// Assets/Editor/ApplyFastPacing.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stamps balanced pacing values into scene-serialized component fields.
/// Scene values override C# defaults, so this editor tool is the reliable way to
/// tune pacing across the playable build. Idempotent: safe to re-run after scene edits.
/// </summary>
public static class ApplyFastPacing
{
    // component type name -> (field, value)
    private static readonly Dictionary<string, (string field, float value)[]> CoreValues =
        new Dictionary<string, (string, float)[]>
    {
        ["OwlGreetingCutscene"] = new (string, float)[]
        {
            ("startDelay", 0.05f),
            ("dimFadeInDuration", 0.22f),
            ("dimFadeOutDuration", 0.25f),
            ("zoomDuration", 0.4f),
            ("holdAfterZoom", 0.04f),
            ("restoreZoomDuration", 0.35f),
            ("talkingFadeDuration", 0.22f),
            ("holdFrozenAfterRound", 0.15f),
            ("voiceStartDelay", 0.05f),
            ("bearFocusVoiceGap", 0.2f),
            ("bearFocusDelay", 0.05f),
            ("bearGrowDuration", 0.3f),
            ("bookRevealDelay", 0.1f),
            ("bookRevealDuration", 0.65f),
            ("owlFadeOutBeforeBookDuration", 0.35f),
        },
        ["OwlHelloSequence"] = new (string, float)[]
        {
            ("fps", 18f), // 42-frame wave ~= 2.4s: quick enough to feel alive, not rushed
            ("playbackSpeed", 1f), // natural, undistorted; timing comes from fps
            ("holdAfterPlay", 0.06f),
            ("zoomInDuration", 0.16f),
            ("zoomOutDuration", 0.16f),
            ("zoomScale", 1f),
        },
        ["MagicStonePuzzleController"] = new (string, float)[]
        {
            ("revealDuration", 0.38f),
            ("revealDelayBetweenStones", 0.12f),
            ("craftResultFadeDuration", 0.32f),
            ("crowCraftHoldDuration", 0.35f),
            ("actionIconFadeDelay", 0.08f),
            ("actionIconFadeDuration", 0.28f),
            ("ritualShakeDuration", 1.2f),
            ("whiteFlashFadeInDuration", 0.22f),
            ("whiteFlashHoldDuration", 0.14f),
            ("whiteFlashFadeOutDuration", 0.55f),
            ("recordingSuccessPopDuration", 0.34f),
            ("recordingSuccessHoldDuration", 0.35f),
            ("ttsEchoGapSeconds", 0.45f),
            ("ttsEchoFinalWordGapSeconds", 0.7f),
            ("placementVoicePostGapSeconds", 0.25f),
        },
        ["WordAssemblyTimer"] = new (string, float)[]
        {
            ("totalSeconds", 30f),
            ("countdownAt", 10f),
            ("popDuration", 0.5f),
        },
        ["SuccessPaOwlEpilogue"] = new (string, float)[]
        {
            ("fadeInDuration", 0.25f),
            ("fadeOutDuration", 0.25f),
            ("fallbackHoldSeconds", 1.5f),
            ("holdFrozenAfterRound", 0.15f),
            ("phraseGap", 0.25f),
        },
        ["SuccessGaReturn"] = new (string, float)[]
        {
            ("playSeconds", 2.9f),
            ("fadeDuration", 0.45f),
        },
        ["CrowEntranceCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.35f),
            ("leg1Duration", 2.4f),
            ("leg2Duration", 1.9f),
        },
    };

    private static readonly string[] CoreScenes =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity",
        "Assets/Scenes/region 1/CutScene_ga.unity",
        "Assets/Scenes/region 1/CutScene_ta.unity",
        "Assets/Scenes/region 1/Success_pa.unity",
        "Assets/Scenes/region 1/Success_ga.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity",
        "Assets/Scenes/region 1/Success_ta_incorrect.unity",
    };

    private static readonly Dictionary<string, (string field, float value)[]> WorldValues =
        new Dictionary<string, (string, float)[]>
    {
        ["QuestPathSequence"] = new (string, float)[]
        {
            ("startDelay", 0.1f),
            ("moveSpeed", 1.55f),
            ("fadeDuration", 0.35f),
            ("walkAnimSpeedParam", 1.5f),
            ("questAutoHold", 0.45f),
            ("nightEndHold", 0.6f),
            ("sceneExitCoverDuration", 0.65f),
        },
        ["WorldMapProblemIslands"] = new (string, float)[]
        {
            ("sceneFadeFallbackWait", 1.1f),
            ("unlockAnimationDelay", 0.15f),
            ("unlockAnimationDuration", 0.95f),
            ("unlockHoldDuration", 0.35f),
            ("unlockFadeOutDuration", 0.35f),
            ("playableIslandPromptDuration", 1.5f),
            ("sceneExitCoverDuration", 0.65f),
        },
        ["CutScene2ChaseController"] = new (string, float)[]
        {
            ("startDelayAfterFade", 0.16f),
            ("whiteFadeOutDuration", 0.75f),
            ("bearRunDuration", 1.15f),
            ("stoneChaseDuration", 1.05f),
            ("completeHoldDuration", 0.22f),
            ("blackFadeInDuration", 0.6f),
        },
        ["CrowCutsceneController"] = new (string, float)[]
        {
            ("blackFadeOutDuration", 0.45f),
            ("flyDuration", 1.8f),
            ("blackFadeInDuration", 0.45f),
        },
        ["CrowSetFreeCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.3f),
            ("fadeOutDuration", 0.45f),
            ("clip2FadeInDuration", 0.35f),
        },
    };

    private static readonly string[] WorldScenes =
    {
        "Assets/Scenes/region 1/reference_forest.unity",
        "Assets/Scenes/WorldMap.unity",
        "Assets/Scenes/region 1/cut_scene2.unity",
        "Assets/Scenes/region 1/cut_scene3.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity",
    };

    [MenuItem("Tools/Pacing/Apply Balanced Pacing (Core Loop)")]
    public static void ApplyCore() => Apply(CoreScenes, CoreValues);

    [MenuItem("Tools/Pacing/Apply Balanced Pacing (World)")]
    public static void ApplyWorld() => Apply(WorldScenes, WorldValues);

    [MenuItem("Tools/Pacing/Apply Balanced Pacing (Build Scenes)")]
    public static void ApplyBuildScenes()
    {
        Apply(CoreScenes, CoreValues);
        Apply(WorldScenes, WorldValues);
    }

    private static void Apply(string[] scenes, Dictionary<string, (string field, float value)[]> table)
    {
        foreach (string scenePath in scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int touched = 0;

            foreach (var kv in table)
            {
                Type type = FindType(kv.Key);
                if (type == null)
                {
                    Debug.LogError($"[BalancedPacing] Unknown component type '{kv.Key}'");
                    continue;
                }

                // Include inactive objects: several book/icon roots start disabled.
                foreach (var comp in UnityEngine.Object.FindObjectsByType(
                    type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(comp);
                    bool changed = false;
                    foreach (var (field, value) in kv.Value)
                    {
                        SerializedProperty p = so.FindProperty(field);
                        if (p == null)
                        {
                            Debug.LogError($"[BalancedPacing] {kv.Key}.{field} not found in {scenePath}");
                            continue;
                        }

                        if (!Mathf.Approximately(p.floatValue, value))
                        {
                            p.floatValue = value;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty((UnityEngine.Object)comp);
                        touched++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BalancedPacing] {scene.name}: updated {touched} component(s)");
        }
    }

    private static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
