using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WordFlow.Adventure.Net;

// One-shot wiring for cut_scene1: assigns per-stone placement voice clips + the result echo
// clip, and enables backend upload on every MagicStonePuzzleController in the scene, then saves.
// Runs atomically in a single menu invocation (edit mode) so nothing is lost to a domain reload.
public static class CutScene1VoiceWiring
{
    private const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    private const string GradeUrl = "http://127.0.0.1:8001/api/v1/grade";

    // Phoneme clips reused from the word_build_paa_polished stone data (by GUID).
    private const string GuidKo = "84e36abdc70fcd14a8ddb5a9f6fe6d0c";       // ก
    private const string GuidPo = "d7a2c37d70965a94fa6e9b14b1ac13c4";       // ป
    private const string GuidAa = "ed1fdd0f2155ae94ab94f68014b01343";       // า
    private const string GuidSoundOut = "fa7a152750660944ea6b99e77fb90a8f"; // ปา sound-out (result echo)

    [MenuItem("Tools/CutScene1/Wire Voice + Upload")]
    public static void Wire()
    {
        var scene = EditorSceneManager.GetActiveScene();
        string activePath = (scene.path ?? "").Replace("\\", "/");
        if (!activePath.EndsWith("CutScene_bear.unity"))
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        AudioClip clipKo = LoadClip(GuidKo);
        AudioClip clipPo = LoadClip(GuidPo);
        AudioClip clipAa = LoadClip(GuidAa);
        AudioClip clipSoundOut = LoadClip(GuidSoundOut);

        var controllers = Object.FindObjectsByType<MagicStonePuzzleController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int wired = 0;
        foreach (var c in controllers)
        {
            var so = new SerializedObject(c);

            string[] letters = ReadLetters(so.FindProperty("stoneLetters"));
            var clipsProp = so.FindProperty("stonePlacementClips");
            clipsProp.arraySize = letters.Length;
            for (int i = 0; i < letters.Length; i++)
            {
                clipsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    ClipForLetter(letters[i], clipKo, clipPo, clipAa);
            }

            so.FindProperty("soundPlaybackClip").objectReferenceValue = clipSoundOut;
            so.FindProperty("uploadRecordingToBackend").boolValue = true;
            so.FindProperty("blockPlacementWhileVoicePlaying").boolValue = true;
            so.FindProperty("autoPlayResultClip").boolValue = true;

            var urlProp = so.FindProperty("recordingUploadUrl");
            urlProp.stringValue = GradeUrl;

            // White-flash tuning: the screen shakes gentle->strong while the white charges, peaking
            // at FULL white synced with the strongest shake, then holds fully white before fade-out.
            so.FindProperty("ritualChargeFlashMaxAlpha").floatValue = 0.45f; // buildup ceiling during shake
            so.FindProperty("ritualFinalPulseAlpha").floatValue = 1f;        // peak = fully white
            so.FindProperty("whiteFlashHoldDuration").floatValue = 0.35f;    // hold full white longer

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
            wired++;

            Debug.Log($"[CutScene1VoiceWiring] '{GetPath(c.transform)}' letters=[{string.Join(",", letters)}] " +
                      $"clips=[ko:{clipKo != null}, po:{clipPo != null}, aa:{clipAa != null}] " +
                      $"soundOut:{clipSoundOut != null} url:{urlProp.stringValue}");
        }

        EnsureSessionContext();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CutScene1VoiceWiring] Wired {wired} MagicStonePuzzleController instance(s); saved {scene.path}.");
    }

    // cut_scene1 has no SessionContext, so /grade attempts went up with no sessionId and never
    // grouped under a session in the webapp. Create one (configured for the same backend) so it
    // opens a session on play and the controller attaches its SessionId to every grade.
    private static void EnsureSessionContext()
    {
        var existing = Object.FindObjectsByType<SessionContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0)
        {
            Debug.Log("[CutScene1VoiceWiring] SessionContext already present; left as-is.");
            return;
        }

        var go = new GameObject("SessionContext");
        var session = go.AddComponent<SessionContext>();
        var so = new SerializedObject(session);
        so.FindProperty("kidId").stringValue = "kid_demo_01";
        so.FindProperty("island").intValue = 1;
        so.FindProperty("baseUrl").stringValue = "http://127.0.0.1:8001/api/v1";
        so.FindProperty("authorization").stringValue = "Bearer demo-token";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(session);
        Debug.Log("[CutScene1VoiceWiring] Added SessionContext (baseUrl 8001, kid_demo_01).");
    }

    private static string[] ReadLetters(SerializedProperty lettersProp)
    {
        if (lettersProp != null && lettersProp.isArray && lettersProp.arraySize > 0)
        {
            var arr = new string[lettersProp.arraySize];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = lettersProp.GetArrayElementAtIndex(i).stringValue;
            }
            return arr;
        }
        return new[] { "ก", "ป", "า" };
    }

    private static AudioClip ClipForLetter(string letter, AudioClip ko, AudioClip po, AudioClip aa)
    {
        switch (letter)
        {
            case "ก": return ko;
            case "ป": return po;
            case "า": return aa;
            default: return null;
        }
    }

    private static AudioClip LoadClip(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[CutScene1VoiceWiring] clip guid {guid} not found");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
