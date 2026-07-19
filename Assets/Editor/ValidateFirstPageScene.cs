using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class ValidateFirstPageScene
{
    [MenuItem("Tools/FirstPage/Validate")]
    public static void Validate()
    {
        bool ok = true;
        ok &= CheckClipExists("Assets/Video/intro_forward.mp4");
        ok &= CheckClipExists("Assets/Video/intro_rev.mp4");
        ok &= CheckClipExists("Assets/Video/idle_loop.mp4");
        ok &= CheckBuildSettings();
        ok &= CheckSceneWiring();

        Debug.Log(ok ? "[FirstPage] Validate: ALL CHECKS PASSED" : "[FirstPage] Validate: FAILED — see errors above");
    }

    private static bool CheckClipExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VideoClip>(path) == null)
        {
            Debug.LogError($"[FirstPage] Missing clip: {path}");
            return false;
        }

        return true;
    }

    private static bool CheckBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length == 0 || scenes[0].path != "Assets/Scenes/first_page.unity" || !scenes[0].enabled)
        {
            Debug.LogError("[FirstPage] first_page.unity is not enabled at Build Settings index 0");
            return false;
        }

        return true;
    }

    private static bool CheckSceneWiring()
    {
        bool wasOpen = SceneManager.GetSceneByPath("Assets/Scenes/first_page.unity").isLoaded;
        var scene = wasOpen
            ? SceneManager.GetSceneByPath("Assets/Scenes/first_page.unity")
            : EditorSceneManager.OpenScene("Assets/Scenes/first_page.unity", OpenSceneMode.Additive);

        var intro = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<FirstPageIntro>(true))
            .FirstOrDefault();

        bool ok = intro != null;
        if (!ok)
        {
            Debug.LogError("[FirstPage] No FirstPageIntro component found in first_page scene");
        }
        else
        {
            var so = new SerializedObject(intro);
            foreach (var field in new[]
            {
                "introPlayer", "idlePlayer", "videoSurface",
                "introForwardClip", "introReverseClip", "idleLoopClip",
                "logoGroup", "logoRect",
                "pressToStartGroup", "pressToStartRect", "logoutGroup", "logoutButton",
                "authButtonsGroup", "authButtonsRect", "loginButton", "signupButton",
            })
            {
                var p = so.FindProperty(field);
                if (p == null || p.objectReferenceValue == null)
                {
                    Debug.LogError($"[FirstPage] FirstPageIntro.{field} is unassigned");
                    ok = false;
                }
            }
        }

        bool hasEventSystem = scene.GetRootGameObjects()
            .Any(go => go.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null);
        if (!hasEventSystem)
        {
            Debug.LogError("[FirstPage] No EventSystem in first_page scene — UI buttons will not click");
            ok = false;
        }

        bool hasAuth = scene.GetRootGameObjects()
            .Any(go => go.GetComponentInChildren<WordFlow.Adventure.Net.AuthSession>(true) != null);
        if (!hasAuth)
        {
            Debug.LogError("[FirstPage] No AuthSession in first_page scene — auto-login cannot run");
            ok = false;
        }

        var loop = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<UISpriteLoop>(true))
            .FirstOrDefault();
        if (loop == null || new SerializedObject(loop).FindProperty("frames").arraySize < 2)
        {
            Debug.LogError("[FirstPage] UISpriteLoop needs at least two logo frames");
            ok = false;
        }
        else if (PrefabUtility.GetCorrespondingObjectFromSource(loop.gameObject) == null)
        {
            // Without the link, size/position edits made in the Prefab editor never reach the scene.
            Debug.LogError("[FirstPage] Logo is not a prefab instance — prefab edits will not propagate");
            ok = false;
        }

        if (!wasOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return ok;
    }
}
