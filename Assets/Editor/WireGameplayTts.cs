using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: wires the gameplay TTS line ids (backend tts_lines.json) into every
// MagicStonePuzzleController of the word-assembly scenes, so stone-snap phonemes and the
// assembled-word echo come from /tts instead of baked wav files (which stay assigned as
// offline fallback). Line ids are derived from each controller's own serialized
// stoneLetters / targetWord / altWord — no per-scene table.
public static class WireGameplayTts
{
    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity",
        "Assets/Scenes/region 1/CutScene_ga.unity",
        "Assets/Scenes/region 1/CutScene_ta.unity",
    };

    static readonly Dictionary<string, string> LetterLine = new Dictionary<string, string>
    {
        { "ก", "gameplay_ko" },  // ก
        { "ป", "gameplay_po" },  // ป
        { "ต", "gameplay_to" },  // ต
        { "า", "gameplay_aa" },  // า
    };

    static readonly Dictionary<string, string[]> WordLines = new Dictionary<string, string[]>
    {
        { "กา", new[] { "gameplay_ko", "gameplay_aa", "gameplay_kaa" } },  // กา
        { "ปา", new[] { "gameplay_po", "gameplay_aa", "gameplay_paa" } },  // ปา
        { "ตา", new[] { "gameplay_to", "gameplay_aa", "gameplay_taa" } },  // ตา
    };

    [MenuItem("Tools/Audio/Wire Gameplay TTS")]
    public static void Run()
    {
        foreach (var scenePath in ScenePaths)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath);
            int wired = WireLoadedScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameplayTts] {scenePath}: wired {wired} controllers, saved");
        }
        Debug.Log("[GameplayTts] DONE");
    }

    // Wires every MagicStonePuzzleController in the CURRENTLY LOADED scene (does not save) —
    // also called from BuildCrowCutscene.WireScene as part of the CutScene_ga rebuild.
    public static int WireLoadedScene()
    {
        int wired = 0;
        foreach (var puzzle in Object.FindObjectsOfType<MagicStonePuzzleController>(true))
        {
            var so = new SerializedObject(puzzle);

            var letters = so.FindProperty("stoneLetters");
            var placementIds = so.FindProperty("stonePlacementLineIds");
            placementIds.arraySize = letters.arraySize;
            for (int i = 0; i < letters.arraySize; i++)
            {
                LetterLine.TryGetValue(letters.GetArrayElementAtIndex(i).stringValue, out string id);
                placementIds.GetArrayElementAtIndex(i).stringValue = id ?? "";
            }

            SetWordLines(so, "targetWord", "soundPlaybackLineIds");
            SetWordLines(so, "altWord", "altSoundPlaybackLineIds");

            so.ApplyModifiedPropertiesWithoutUndo();
            wired++;
        }
        return wired;
    }

    static void SetWordLines(SerializedObject so, string wordProp, string idsProp)
    {
        string word = so.FindProperty(wordProp).stringValue;
        var ids = so.FindProperty(idsProp);
        if (string.IsNullOrEmpty(word) || !WordLines.TryGetValue(word, out string[] lines))
        {
            ids.arraySize = 0;
            return;
        }
        ids.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
            ids.GetArrayElementAtIndex(i).stringValue = lines[i];
    }
}
