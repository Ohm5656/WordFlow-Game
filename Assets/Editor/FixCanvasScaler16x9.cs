using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot fix: the cutscene/success scenes use Canvas = Screen Space - Overlay with a
// CanvasScaler in ConstantPixelSize mode. Their ~3840x2160 background image is then drawn
// at a fixed pixel size, so on any display smaller/narrower than 4K it overflows the screen
// and looks "zoomed in". Switching every CanvasScaler in these scenes to Scale With Screen
// Size (ref 3840x2160, match Width) makes the whole 16:9 composition scale to fit: full on
// 16:9 screens, letterboxed (never cropped) on taller 16:10 laptops.
//
// WorldMap / reference_forest are world-space (orthographic camera) and already correct, so
// they are intentionally left out.
public static class FixCanvasScaler16x9
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/region 1/CutScene_bear.unity",
        "Assets/Scenes/region 1/CutScene_ga.unity",
        "Assets/Scenes/region 1/Success_pa.unity",
        "Assets/Scenes/region 1/Success_ga.unity",
        "Assets/Scenes/region 1/Success_ga_correct.unity",
        "Assets/Scenes/region 1/Success_ta_incorrect.unity",
    };

    [MenuItem("Tools/Fix/CanvasScaler 16:9")]
    public static void Run()
    {
        int scenesChanged = 0, scalersChanged = 0;
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                    scaler.referenceResolution == new Vector2(3840, 2160) &&
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                    Mathf.Approximately(scaler.matchWidthOrHeight, 0f))
                    continue; // already fixed

                Undo.RecordObject(scaler, "Fix CanvasScaler 16:9");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(3840, 2160);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f; // match Width -> full-width, letterbox on taller screens
                EditorUtility.SetDirty(scaler);
                dirty = true;
                scalersChanged++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesChanged++;
            }
        }
        Debug.Log($"[FixCanvasScaler16x9] Updated {scalersChanged} CanvasScaler(s) across {scenesChanged} scene(s).");
    }
}
