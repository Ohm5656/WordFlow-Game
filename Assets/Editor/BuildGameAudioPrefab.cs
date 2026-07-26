using UnityEditor;
using UnityEngine;

// Rebuilds Resources/GameAudio.prefab from scratch with every Assets/Audio clip wired.
// DESTRUCTIVE: it deletes and recreates the prefab, so any value tuned by hand in the
// Inspector (gains, volumes, scene lists) is lost. Prefer "Wire Music Tracks" below,
// which only touches the shared title-world / forest / quest music clip slots.
public static class BuildGameAudioPrefab
{
    const string PrefabPath = "Assets/Resources/GameAudio.prefab";
    const string MainThemeFile = "main_theme.ogg";
    const string MenuThemeFile = MainThemeFile;
    const string QuestThemeFile = "quest_theme.ogg";

    // Non-destructive: point the existing prefab at the three music tracks and leave
    // every other field alone. This is the one to run after dropping the clips in.
    [MenuItem("Tools/Audio/Wire Music Tracks")]
    public static void WireMusicTracks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[GameAudioPrefab] missing " + PrefabPath); return; }

        var so = new SerializedObject(prefab.GetComponent<GameAudio>());
        Set(so, "menuTheme", MenuThemeFile);
        Set(so, "mainTheme", MainThemeFile);
        Set(so, "questTheme", QuestThemeFile);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[GameAudioPrefab] music tracks wired");
    }

    [MenuItem("Tools/Audio/Build GameAudio Prefab (destructive)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild GameAudio.prefab?",
                "This deletes the prefab and recreates it, discarding every hand-tuned value in it.",
                "Rebuild", "Cancel"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var go = new GameObject("GameAudio");
        var audio = go.AddComponent<GameAudio>();

        var so = new SerializedObject(audio);
        Set(so, "menuTheme", MenuThemeFile);
        Set(so, "mainTheme", MainThemeFile);
        Set(so, "questTheme", QuestThemeFile);
        Set(so, "click", "click.mp3");
        Set(so, "unlockSting", "unlock.wav");
        Set(so, "questEnter", "click-quest.mp3");
        Set(so, "afterQuest", "after-quest.mp3");
        Set(so, "winSting", "win.wav");
        Set(so, "loseSting", "lose.wav");
        Set(so, "bearRun", "Bear.mp3");
        Set(so, "bearRoar", "bear-kamram.mp3");
        Set(so, "crowFly", "Crow.mp3");
        Set(so, "paaThrow", "paa.wav");
        Set(so, "rockBreak", "rock-break.mp3");
        SetArray(so, "forestFootsteps", "stepTree.ogg", "stepTree2.ogg", "stepTree3.ogg");
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log("[GameAudioPrefab] DONE -> " + PrefabPath);
    }

    static void Set(SerializedObject so, string field, string fileName)
    {
        so.FindProperty(field).objectReferenceValue = Load(fileName);
    }

    static void SetArray(SerializedObject so, string field, params string[] fileNames)
    {
        var prop = so.FindProperty(field);
        prop.arraySize = fileNames.Length;
        for (int i = 0; i < fileNames.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = Load(fileNames[i]);
    }

    static AudioClip Load(string fileName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + fileName);
        // The two music tracks are supplied by hand and may not exist yet — warn, don't error.
        if (clip == null) Debug.LogWarning("[GameAudioPrefab] missing clip: " + fileName);
        return clip;
    }
}
