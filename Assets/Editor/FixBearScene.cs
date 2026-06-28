using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot fixes for CutScene_bear after swapping in the new Bear.prefab:
//  - put Bear behind the book (sibling index just after background)
//  - point OwlGreetingCutscene.bearRoot at the new Bear (owl bear-focus had a dangling ref
//    to the deleted BearRoot)
//  - clear the stuck "retry after crow" PlayerPrefs flag that was skipping the owl intro
public static class FixBearScene
{
    [MenuItem("Tools/Scenes/Fix Bear Scene Issues")]
    public static void Run()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[FixBearScene] no Canvas"); return; }

        Transform bear = canvas.transform.Find("Bear");
        if (bear != null)
        {
            bear.SetSiblingIndex(1); // 0 = background, 1 = just above it / below the book
            Debug.Log("[FixBearScene] Bear sibling index -> 1 (behind book)");
        }
        else Debug.LogWarning("[FixBearScene] Canvas/Bear not found");

        var owl = Object.FindObjectOfType<OwlGreetingCutscene>(true);
        if (owl != null && bear != null)
        {
            var so = new SerializedObject(owl);
            var prop = so.FindProperty("bearRoot");
            if (prop != null)
            {
                prop.objectReferenceValue = bear.GetComponent<RectTransform>();
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[FixBearScene] OwlGreetingCutscene.bearRoot -> Canvas/Bear");
            }
        }
        else Debug.LogWarning("[FixBearScene] OwlGreetingCutscene not found");

        // clear the stuck flag so a fresh play shows the owl intro instead of the retry path
        bool wasSet = MagicStonePuzzleController.IsRetryAfterCrowRequested;
        MagicStonePuzzleController.ConsumeRetryAfterCrow();
        Debug.Log($"[FixBearScene] retry-after-crow flag was {(wasSet ? "SET -> cleared" : "already clear")}");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[FixBearScene] DONE (scene saved)");
    }
}
