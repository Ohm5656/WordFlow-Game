using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tools/Quest/Fix Fog Layer
/// Puts the CutScene_bear smoke FogOverlay (and the CloudOverlay layer riding above it) just above
/// the background so the gameplay UI — book_craft / book_craft_pa / book_craft_ga and the time_root
/// clock — always render on top of both smoke layers. Also makes sure FogController.fogImage points
/// at its own RawImage. Idempotent.
public static class FixFogLayer
{
    const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    [MenuItem("Tools/Quest/Fix Fog Layer")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var fog = Object.FindFirstObjectByType<FogController>(FindObjectsInactive.Include);
        if (fog == null) { Debug.LogError("[FixFogLayer] FogController not found in CutScene_bear"); return; }

        // Wire fogImage -> its own RawImage if missing.
        var so = new SerializedObject(fog);
        var imgProp = so.FindProperty("fogImage");
        if (imgProp != null && imgProp.objectReferenceValue == null)
        {
            var raw = fog.GetComponent<RawImage>();
            if (raw != null) { imgProp.objectReferenceValue = raw; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        Transform fogT = fog.transform;
        Transform parent = fogT.parent;
        if (parent == null) { Debug.LogError("[FixFogLayer] FogOverlay has no parent Canvas"); return; }

        // Sit right above the background (first sibling if no background found), so everything else —
        // book crafts + time_root + stones + owl — draws over the fog.
        Transform background = parent.Find("background");
        int target = background != null ? background.GetSiblingIndex() + 1 : 1;
        fogT.SetSiblingIndex(target);

        // The grey cloud layer (Tools/Quest/Add Cloud Layer) rides directly above the purple smoke.
        // Re-anchor it here or every run of this tool would leave it stranded at its old index.
        Transform cloud = parent.Find("CloudOverlay");
        if (cloud != null)
        {
            cloud.SetSiblingIndex(fogT.GetSiblingIndex() + 1);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FixFogLayer] DONE — FogOverlay moved to sibling index {fogT.GetSiblingIndex()} " +
                  $"(book_craft*/time_root now render above the fog), scene saved");
    }
}
