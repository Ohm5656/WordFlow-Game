using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot:
//  - book_craft_pa wouldn't show because all 3 of its children (the book image + the two
//    example stones) were inactive. ShowCrowCraftRoutine only toggles the ROOT + its
//    CanvasGroup, so the inactive children rendered nothing. Activate them; the root's
//    CanvasGroup still gates visibility (hidden until the word is solved).
//  - restore the owl frames to the user's authored positions (a previous pass wrongly
//    zeroed their local X). Values = original world X minus OwlRoot world X (2043.5),
//    applied by child index.
public static class FixBookAndRestoreOwl
{
    // original local X per owl frame, in OwlRoot child order
    static readonly float[] OwlLocalX = { 1415.0f, 1412.7f, 1412.7f, 1433.38f, 1407.3f, 1412.7f, 1434.9f, 1406.5f, 1402.65f };

    [MenuItem("Tools/Scenes/Fix Book + Restore Owl")]
    public static void Run()
    {
        // 1. activate book_craft_pa's children so the page renders when revealed
        var bookPa = GameObject.Find("Canvas/book_craft_pa");
        if (bookPa != null)
        {
            int activated = 0;
            foreach (Transform child in bookPa.transform)
            {
                if (!child.gameObject.activeSelf) { child.gameObject.SetActive(true); activated++; }
            }
            Debug.Log($"[FixBook] activated {activated} child(ren) under book_craft_pa");
        }
        else Debug.LogWarning("[FixBook] Canvas/book_craft_pa not found");

        // 2. restore owl frame X positions
        var owl = GameObject.Find("OwlRoot");
        if (owl != null)
        {
            int i = 0;
            foreach (Transform child in owl.transform)
            {
                if (i < OwlLocalX.Length)
                {
                    Vector3 lp = child.localPosition;
                    child.localPosition = new Vector3(OwlLocalX[i], lp.y, lp.z);
                }
                i++;
            }
            Debug.Log($"[FixOwl] restored X on {Mathf.Min(i, OwlLocalX.Length)} owl frames");
        }
        else Debug.LogWarning("[FixOwl] OwlRoot not found");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[FixBookAndRestoreOwl] DONE (saved)");
    }
}
