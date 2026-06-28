using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// One-shot: build the owl cutscene animation the same way as the bear/Thow pipeline —
// one sprite-keyframe clip bound to a UGUI Image, a single-state controller, and a prefab
// (Image + Animator). Frames are already capped to 512 via Tools/Optimize/Cap Anim Frames 512.
// Run via Tools/Owl/Build Owl Anim + Prefab.
public static class OwlAnimSetup
{
    const string FramesDir = "Assets/Art/quest_map/owl";
    const string OutDir = "Assets/Art/quest_map/owl/Animations";
    const string ClipPath = "Assets/Art/quest_map/owl/Animations/owl.anim";
    const string ControllerPath = "Assets/Art/quest_map/owl/owlController.controller";
    const string PrefabPath = "Assets/Art/quest_map/owl/Owl.prefab";
    const float Fps = 30f;
    const bool Loop = true; // owl idle/flap loops; flip to false if it's a one-shot gesture

    [MenuItem("Tools/Owl/Build Owl Anim + Prefab")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder(OutDir)) Directory.CreateDirectory(OutDir);

        // 1. collect frames in numeric order, load as sprites (import already Sprite/Single/512)
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { FramesDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png"))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToArray();
        if (paths.Length == 0) { Debug.LogError($"[Owl] no frames in {FramesDir}"); return; }
        var sprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        if (sprites.Length != paths.Length)
            Debug.LogWarning($"[Owl] {paths.Length} frames but {sprites.Length} sprites loaded (reimport owl as Sprite)");

        // 2. clip — one sprite keyframe per frame, bound to Image.m_Sprite (like bear)
        var clip = new AnimationClip { frameRate = Fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        var binding = new EditorCurveBinding { type = typeof(Image), path = "", propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.DeleteAsset(ClipPath);
        AssetDatabase.CreateAsset(clip, ClipPath);

        // 3. controller with a single default Owl state
        AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var st = ctrl.layers[0].stateMachine.AddState("Owl");
        st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;
        EditorUtility.SetDirty(ctrl);

        // 4. prefab: RectTransform + CanvasRenderer + Image + Animator (clip binds Image.m_Sprite)
        var go = new GameObject("Owl", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
        var img = go.GetComponent<Image>();
        if (sprites.Length > 0) { img.sprite = sprites[0]; img.SetNativeSize(); }
        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Owl] DONE — clip {sprites.Length} frames loop={Loop}, controller + {PrefabPath}");
    }
}
