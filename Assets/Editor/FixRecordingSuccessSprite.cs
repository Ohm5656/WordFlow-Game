using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools/Quest/Fix Recording Success Sprite
/// magic_stone's MagicStonePuzzleController pointed recordingSuccessSprite at a sub-sprite from when
/// the green-check texture was imported as Sprite Mode = Multiple. The texture is Single now, so that
/// sub-sprite no longer exists and the reference reads as "Missing" — the success image silently
/// never appeared (the code null-guards it, so nothing crashed, the art just went missing).
/// Re-points it at the texture's actual Single sprite.
public static class FixRecordingSuccessSprite
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";
    const string SpritePath = "Assets/Art/visaul_novel/background/ChatGPT Image Jun 3, 2026, 05_19_29 PM.png";

    [MenuItem("Tools/Quest/Fix Recording Success Sprite")]
    public static void Fix()
    {
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty)
        {
            Debug.LogError($"[FixSuccessSprite] '{open.name}' has unsaved changes. Save or discard it first.");
            return;
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[FixSuccessSprite] no Sprite at {SpritePath} — is the texture's Texture Type set to Sprite?");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var go = GameObject.Find("Canvas/magic_stone");
        if (go == null) { Debug.LogError("[FixSuccessSprite] Canvas/magic_stone not found"); return; }

        var controller = go.GetComponent<MagicStonePuzzleController>();
        if (controller == null) { Debug.LogError("[FixSuccessSprite] no MagicStonePuzzleController on magic_stone"); return; }

        var so = new SerializedObject(controller);
        so.FindProperty("recordingSuccessSprite").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[FixSuccessSprite] recordingSuccessSprite -> '{sprite.name}' on Canvas/magic_stone");
    }
}
