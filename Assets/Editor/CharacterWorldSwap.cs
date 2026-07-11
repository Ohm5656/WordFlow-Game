using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SuperRetroMainBundle;

// One-shot tool: puts the new hand-drawn run character into reference_forest.
// 1. Adds a SpriteRenderer.m_Sprite curve to the 4 run clips (they only bound
//    UGUI Image; world scenes render with SpriteRenderer — both curves coexist,
//    the Animator applies whichever component exists).
// 2. Builds CharacterRunWorldController: params orientation(int)+speed(float)
//    matching the Super_Retro pack contract QuestPathSequence already drives
//    (0 up / 2 left / 4 down / 6 right, walk threshold 0.01). 4 run states +
//    4 idle states (same clip, state speed 0 = freeze on frame 0).
// 3. In reference_forest: removes CharacterAppearance from the body (it swaps
//    sprites by sheet name every LateUpdate and would fight the new clips),
//    swaps the Animator controller, adds CharacterRunDirection, sets the
//    SpriteRenderer to run-front frame 0. Scale left alone (user adjusts).
// Run via Tools/Character/Swap Into Reference Forest.
public static class CharacterWorldSwap
{
    const string AnimDir = "Assets/Art/quest_map/character/Animations";
    const string WorldControllerPath = "Assets/Art/quest_map/character/CharacterRunWorldController.controller";
    const string ScenePath = "Assets/Scenes/region 1/reference_forest.unity";

    // clip name -> pack orientation value
    static readonly (string clip, int orientation)[] Dirs =
    {
        ("character_run_up",    0),
        ("character_run_left",  2),
        ("character_run_front", 4),
        ("character_run_right", 6),
    };

    [MenuItem("Tools/Character/Swap Into Reference Forest")]
    public static void Swap()
    {
        // Clips already carry both Image + SpriteRenderer curves (CharacterRunBuilder).

        // --- 2. world controller with the pack's param contract ---
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(WorldControllerPath);
        ctrl.AddParameter("orientation", AnimatorControllerParameterType.Int);
        ctrl.AddParameter("speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;

        // States only — NO transitions. CharacterRunDirection plays states directly
        // (phase-preserving turns + speed-synced tempo need Play(hash, 0, phase),
        // which transitions would immediately fight). Params stay as the data
        // channel QuestPathSequence writes to.
        // Front has a real standing animation (character_idle_front, 4f breathing loop);
        // other directions freeze frame 0 of their run clip.
        var idleFrontClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/character_idle_front.anim");
        AnimatorState idleFront = null;
        foreach (var (clipName, orientation) in Dirs)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/{clipName}.anim");
            string dir = clipName.Replace("character_run_", "");

            var run = sm.AddState($"run_{dir}");
            run.motion = clip;
            var idle = sm.AddState($"idle_{dir}");
            if (orientation == 4 && idleFrontClip != null)
            {
                idle.motion = idleFrontClip;   // plays at its own clip frameRate
            }
            else
            {
                idle.motion = clip;
                idle.speed = 0f; // freeze on frame 0 = standing pose
            }
            if (orientation == 4) idleFront = idle;
        }
        if (idleFront != null) sm.defaultState = idleFront;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // --- 3. swap the character in reference_forest (idempotent — safe to re-run) ---
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Find the body whether or not it was already swapped last run.
        var appearance = Object.FindFirstObjectByType<CharacterAppearance>(FindObjectsInactive.Include);
        GameObject body = appearance != null ? appearance.gameObject
            : Object.FindFirstObjectByType<CharacterRunDirection>(FindObjectsInactive.Include)?.gameObject;
        if (body == null)
        {
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.gameObject.name == "spritesheet_7") { body = r.gameObject; break; }
        }
        if (body == null) { Debug.LogError("[CharSwap] could not find character body in scene"); return; }

        if (appearance != null) Object.DestroyImmediate(appearance); // stops sprite-name swapping

        var animator = body.GetComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;

        if (body.GetComponent<CharacterRunDirection>() == null)
            body.AddComponent<CharacterRunDirection>(); // auto-grabs the Animator

        // Initial edit-mode sprite = front clip's frame 0 (robust to frame renaming).
        var sr = body.GetComponent<SpriteRenderer>();
        var frontClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimDir}/character_run_front.anim");
        Sprite frame0 = frontClip != null ? FirstSpriteOf(frontClip) : null;
        if (sr != null && frame0 != null) sr.sprite = frame0;

        // Keep scale uniform (symmetric) — user tunes the value, not the aspect.
        var sc = body.transform.localScale;
        float uni = Mathf.Abs(sc.y) > 1e-4f ? sc.y : 0.33f;
        body.transform.localScale = new Vector3(uni, uni, 1f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CharSwap] DONE — {body.name} now runs CharacterRunWorldController, CharacterAppearance removed, scene saved");
    }

    static Sprite FirstSpriteOf(AnimationClip clip)
    {
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var k = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (k != null && k.Length > 0) return k[0].value as Sprite;
        }
        return null;
    }
}
