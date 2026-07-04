using UnityEditor;
using UnityEngine;

// One-shot: builds Resources/GameAudio.prefab with all 10 Assets/Audio clips wired.
// Re-runnable (deletes + recreates the prefab).
public static class BuildGameAudioPrefab
{
    const string PrefabPath = "Assets/Resources/GameAudio.prefab";

    [MenuItem("Tools/Audio/Build GameAudio Prefab")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var go = new GameObject("GameAudio");
        var audio = go.AddComponent<GameAudio>();

        var so = new SerializedObject(audio);
        Set(so, "music", "Golden Gleam.ogg");
        Set(so, "click", "click.mp3");
        Set(so, "unlockSting", "unlock.wav");
        Set(so, "questEnter", "click-quest.mp3");
        Set(so, "afterQuest", "after-quest.mp3");
        Set(so, "winSting", "win.wav");
        Set(so, "loseSting", "lose.wav");
        Set(so, "bearRun", "Bear.mp3");
        Set(so, "bearRoar", "bear-kamram.mp3");
        Set(so, "crowFly", "Crow.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log("[GameAudioPrefab] DONE -> " + PrefabPath);
    }

    static void Set(SerializedObject so, string field, string fileName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + fileName);
        if (clip == null) Debug.LogError("[GameAudioPrefab] missing clip: " + fileName);
        so.FindProperty(field).objectReferenceValue = clip;
    }
}
