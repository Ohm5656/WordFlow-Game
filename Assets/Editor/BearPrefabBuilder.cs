using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// One-shot tool: builds BearController.controller from the .anim clips and a
// Bear.prefab (UGUI Image + Animator) the cutscene drives via animator.Play("name").
// States only — no params/transitions; Play() ignores transitions, and scripted
// cutscenes pick the next action explicitly. Run via Tools/Bear/Build Prefab.
public static class BearPrefabBuilder
{
    const string BearDir = "Assets/Art/quest_map/bear";
    const string AnimDir = "Assets/Art/quest_map/bear/Animations";
    const string ControllerPath = "Assets/Art/quest_map/bear/BearController.controller";
    const string PrefabPath = "Assets/Art/quest_map/bear/Bear.prefab";
    const string DefaultState = "bear_breathing"; // idle loop

    [MenuItem("Tools/Bear/Build Prefab")]
    public static void Build()
    {
        AssetDatabase.Refresh();

        // 1. collect clips
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".anim"))
            .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .Where(c => c != null)
            .OrderBy(c => c.name)
            .ToArray();
        if (clips.Length == 0) { Debug.LogError($"[BearPrefab] no clips in {AnimDir}"); return; }

        // 2. controller — one state per clip, default = breathing
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState defState = null;
        foreach (var clip in clips)
        {
            var st = sm.AddState(clip.name);
            st.motion = clip;
            if (clip.name == DefaultState) defState = st;
        }
        if (defState != null) sm.defaultState = defState;
        else Debug.LogWarning($"[BearPrefab] default state '{DefaultState}' not found; using first clip");
        EditorUtility.SetDirty(ctrl);

        // 3. prefab — UGUI Image + Animator. Initial sprite = breathing frame 0 so it
        // renders before the cutscene calls Play().
        var go = new GameObject("Bear", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
        var img = go.GetComponent<Image>();
        var idle = clips.FirstOrDefault(c => c.name == DefaultState) ?? clips[0];
        var firstSprite = FirstSpriteOf(idle);
        if (firstSprite != null)
        {
            img.sprite = firstSprite;
            img.SetNativeSize();
        }
        var anim = go.GetComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BearPrefab] DONE — {clips.Length} states, default={DefaultState}, prefab={PrefabPath}");
    }

    static Sprite FirstSpriteOf(AnimationClip clip)
    {
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (keys != null && keys.Length > 0) return keys[0].value as Sprite;
        }
        return null;
    }
}
