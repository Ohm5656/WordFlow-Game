using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds a standalone "Thow" (stone-throw) prefab — its own object, separate from the bear,
// so the scene can show the bear (run_back) AND the stone throw at the same time.
// Then (if Success_pa is open) sets the bear to bear_run_back and drops a Thow instance in.
public static class ThowPrefabSetup
{
    const string ThowClipPath = "Assets/Art/quest_map/bear/Animations/Thow.anim";
    const string ControllerPath = "Assets/Art/quest_map/bear/ThowController.controller";
    const string PrefabPath = "Assets/Art/quest_map/bear/Thow.prefab";

    [MenuItem("Tools/Bear/Build Thow Prefab + Place")]
    public static void Run()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThowClipPath);
        if (clip == null) { Debug.LogError("[Thow] Thow.anim not found"); return; }

        // 1. controller with a single default Thow state
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var st = ctrl.layers[0].stateMachine.AddState("Thow");
        st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;
        EditorUtility.SetDirty(ctrl);

        // 2. prefab: UGUI Image + Animator (clip binds Image.m_Sprite, path="")
        var go = new GameObject("Thow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
        var img = go.GetComponent<Image>();
        var firstSprite = FirstSpriteOf(clip);
        if (firstSprite != null) { img.sprite = firstSprite; img.SetNativeSize(); }
        go.GetComponent<Animator>().runtimeAnimatorController = ctrl;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Thow] built {PrefabPath}");

        // 3. if Success_pa is open: bear -> run_back, drop a Thow instance under Canvas
        if (EditorSceneManager.GetActiveScene().name == "Success_pa")
        {
            var canvas = GameObject.Find("Canvas");
            Transform bear = canvas != null ? canvas.transform.Find("Bear") : null;
            if (bear != null)
            {
                var bc = bear.GetComponent<BearCutscene>();
                if (bc != null)
                {
                    var so = new SerializedObject(bc);
                    var seq = so.FindProperty("sequence");
                    seq.arraySize = 1;
                    var el = seq.GetArrayElementAtIndex(0);
                    el.FindPropertyRelative("state").stringValue = "bear_run_back";
                    el.FindPropertyRelative("toWaypoint").intValue = -1;
                    el.FindPropertyRelative("timing").enumValueIndex = 1; // PlayClip
                    el.FindPropertyRelative("seconds").floatValue = 0f;
                    el.FindPropertyRelative("speed").floatValue = 1f;
                    el.FindPropertyRelative("speedEnd").floatValue = 0f;
                    el.FindPropertyRelative("scale").floatValue = 1f;
                    el.FindPropertyRelative("fadeSeconds").floatValue = 0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[Thow] bear sequence -> [bear_run_back]");
                }
            }

            if (canvas != null && canvas.transform.Find("Thow") == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                inst.name = "Thow";
                inst.transform.position = new Vector3(1500f, 700f, 0f); // placeholder spot — reposition in scene
                Debug.Log("[Thow] placed Thow instance under Canvas (reposition as needed)");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("[Thow] DONE");
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
