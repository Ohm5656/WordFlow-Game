using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot: Success_ta_incorrect (wrong-word "ตา" beat) gets a petrified stone crow next to
// the eye, matching how CutScene_ga leaves the crow (last ga_stone frame, held).
//   - Bear GO (already the eye: EyeController + BearCutscene) -> renamed "Eye", moved beside crow.
//   - Crow.prefab instance -> frozen static on the last ga_stone sprite (Animator + entrance
//     disabled), centred. Added to BearCutscene.alsoFade so it fades out with the eye at the end,
//     then the owl epilogue plays and returns to CutScene_ga (retry).
// Atomic scene edit + SaveScene (domain-reload safety).
public static class BuildTaIncorrectStoneCrow
{
    const string ScenePath = "Assets/Scenes/region 1/Success_ta_incorrect.unity";
    const string CrowPrefab = "Assets/Art/quest_map/Crow.prefab";
    const string StoneDir = "Assets/Art/quest_map/quest_ga/ga_stone_cropped";

    [MenuItem("Tools/Success/Build Ta Incorrect Stone Crow")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[TaStoneCrow] no Canvas"); return; }

        // 1. Eye = the existing Bear GO (already EyeController + eye sequence). Rename + move aside.
        var eyeT = canvas.transform.Find("Bear") ?? canvas.transform.Find("Eye");
        if (eyeT == null) { Debug.LogError("[TaStoneCrow] no Bear/Eye under Canvas"); return; }
        eyeT.name = "Eye";
        var eyeRect = eyeT as RectTransform;
        eyeRect.anchoredPosition = new Vector2(900f, eyeRect.anchoredPosition.y); // beside crow (placeholder)

        // 2. Stone crow: fresh Crow.prefab instance, frozen on the last ga_stone frame.
        var old = canvas.transform.Find("Crow");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefab);
        if (prefab == null) { Debug.LogError("[TaStoneCrow] no Crow.prefab"); return; }
        var crow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        crow.name = "Crow";
        var crowRect = crow.GetComponent<RectTransform>();
        crowRect.anchoredPosition = Vector2.zero; // centred, like CutScene_ga's wp_center

        // Freeze: kill the flight + the animator so the Image keeps the sprite we set.
        var entrance = crow.GetComponent<CrowEntranceCutscene>();
        if (entrance != null) entrance.enabled = false;
        var animator = crow.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        var cg = crow.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        var lastStone = AssetDatabase.FindAssets("t:Sprite", new[] { StoneDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .LastOrDefault(s => s != null);
        var img = crow.GetComponent<Image>();
        if (lastStone != null && img != null) img.sprite = lastStone;
        else Debug.LogWarning("[TaStoneCrow] last ga_stone sprite not found");

        // Render the crow behind the eye, above the background.
        crow.transform.SetSiblingIndex(1);

        // 3. Fade the crow out with the eye at the end (BearCutscene.alsoFade = CanvasGroup[]).
        var bear = eyeT.GetComponent<BearCutscene>();
        if (bear != null && cg != null)
        {
            var so = new SerializedObject(bear);
            var alsoFade = so.FindProperty("alsoFade");
            alsoFade.arraySize = 1;
            alsoFade.GetArrayElementAtIndex(0).objectReferenceValue = cg;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TaStoneCrow] DONE: Eye renamed + moved, stone Crow added (last ga_stone frame), alsoFade wired");
    }
}
