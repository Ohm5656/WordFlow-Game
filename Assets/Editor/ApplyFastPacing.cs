// Assets/Editor/ApplyFastPacing.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stamps the fast-pacing timing values into scene-serialized component fields.
/// Scene values override C# defaults, so this (not code-default edits) is how the
/// game actually speeds up. Idempotent — safe to re-run after any scene edit.
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
            ("dimFadeInDuration", 0.2f),
            ("dimFadeOutDuration", 0.25f),
            ("zoomDuration", 0.4f),
            ("holdAfterZoom", 0f),
            ("restoreZoomDuration", 0.35f),
            ("talkingFadeDuration", 0.2f),
            ("holdFrozenAfterRound", 0.15f),
            ("bearFocusDelay", 0f),
            ("bearGrowDuration", 0.3f),
            ("bookRevealDelay", 0.1f),
            ("bookRevealDuration", 0.7f),
            ("owlFadeOutBeforeBookDuration", 0.35f),
        },
        ["OwlHelloSequence"] = new (string, float)[]
        {
            ("playbackSpeed", 1f), // natural, undistorted; wave now plays smooth at its own ~10fps
            ("holdAfterPlay", 0.1f),
            ("zoomInDuration", 0.15f),
            ("zoomOutDuration", 0.15f),
        },
        ["MagicStonePuzzleController"] = new (string, float)[]
        {
            ("revealDuration", 0.3f),
            ("revealDelayBetweenStones", 0.08f),
            ("craftResultFadeDuration", 0.25f),
            ("crowCraftHoldDuration", 0.2f),
            ("actionIconFadeDelay", 0f),
            ("actionIconFadeDuration", 0.2f),
            ("ritualShakeDuration", 0.8f),
            ("whiteFlashFadeInDuration", 0.18f),
            ("whiteFlashHoldDuration", 0.1f),
            ("whiteFlashFadeOutDuration", 0.45f),
            ("recordingSuccessPopDuration", 0.3f),
            ("recordingSuccessHoldDuration", 0.25f),
        },
        ["SuccessPaOwlEpilogue"] = new (string, float)[]
        {
            ("fallbackHoldSeconds", 2f),
            ("holdFrozenAfterRound", 0.15f),
        },
        ["SuccessGaReturn"] = new (string, float)[]
        {
            ("playSeconds", 4f), // ga clip is ~4s; 4.5 held on 0.5s of nothing
        },
        ["CrowEntranceCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.25f),
            ("leg1Duration", 2f),
            ("leg2Duration", 1.6f),
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
            ("startDelay", 0.15f),
            ("moveSpeed", 1.5f),           // was 1
            ("walkAnimSpeedParam", 1.5f),  // keep feet in sync with the faster walk
            ("questAutoHold", 0.6f),       // was 1.5, hit 5 times per walk
            ("faceHoldBeforeQuest", 0.7f), // was 2
            ("sceneExitCoverDuration", 0.6f),
        },
        // QuestMapIntroFlow intentionally omitted: it is not present in any scene or prefab
        // (verified via search_by_component + grep), so there is nothing to stamp. The quest map
        // uses QuestPointInteractable / QuestMapSimpleCharacterWalk, which were not flagged slow.
        ["WorldMapProblemIslands"] = new (string, float)[]
        {
            ("unlockAnimationDelay", 0.2f),      // was 0.45
            ("unlockAnimationDuration", 1f),     // was 1.8
            ("unlockHoldDuration", 0.5f),        // was 1.1
            ("unlockFadeOutDuration", 0.5f),
            ("playableIslandPromptDuration", 1.2f), // was 2.1
            ("sceneExitCoverDuration", 0.5f),
        },
        ["CutScene2ChaseController"] = new (string, float)[]
        {
            ("startDelayAfterFade", 0.1f),   // was 0.35
            ("whiteFadeOutDuration", 0.6f),  // was 1.05
            ("bearRunDuration", 1f),         // was 1.35
            ("stoneChaseDuration", 0.9f),    // was 1.25
            ("completeHoldDuration", 0.15f), // was 0.3
            ("blackFadeInDuration", 0.5f),   // was 0.85
        },
        ["CrowCutsceneController"] = new (string, float)[]
        {
            ("blackFadeOutDuration", 0.35f),
            ("flyDuration", 1.5f),           // was 2.25
            ("blackFadeInDuration", 0.35f),
        },
        ["CrowSetFreeCutscene"] = new (string, float)[]
        {
            ("fadeInDuration", 0.25f),
            ("fadeOutDuration", 0.6f),       // was 1
            ("clip2FadeInDuration", 0.4f),
        },
    };

    private static readonly string[] WorldScenes =
    {
        "Assets/Scenes/region 1/reference_forest.unity",
        "Assets/Scenes/WorldMap.unity",
        "Assets/Scenes/region 1/cut_scene2.unity",
        "Assets/Scenes/region 1/cut_scene3.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity", // CrowSetFreeCutscene lives here
    };

    [MenuItem("Tools/Pacing/Apply Fast Pacing (Core Loop)")]
    public static void ApplyCore() => Apply(CoreScenes, CoreValues);

    [MenuItem("Tools/Pacing/Apply Fast Pacing (World)")]
    public static void ApplyWorld() => Apply(WorldScenes, WorldValues);

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
                    Debug.LogError($"[FastPacing] Unknown component type '{kv.Key}'");
                    continue;
                }

                // Include inactive objects — several book/icon roots start disabled.
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
                            Debug.LogError($"[FastPacing] {kv.Key}.{field} not found in {scenePath}");
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
            Debug.Log($"[FastPacing] {scene.name}: updated {touched} component(s)");
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
