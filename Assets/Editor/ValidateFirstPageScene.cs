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
        ok &= CheckClipPair("Assets/Video/idle.mp4", "Assets/Video/idle_rev.mp4");
        ok &= CheckClipExists("Assets/Video/intro_rev.mp4");
        ok &= CheckGone("Assets/Video/intro.mp4");
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

    private static bool CheckGone(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            Debug.LogError($"[FirstPage] {path} should have been deleted");
            return false;
        }

        return true;
    }

    private static bool CheckClipPair(string forwardPath, string reversePath)
    {
        var forward = AssetDatabase.LoadAssetAtPath<VideoClip>(forwardPath);
        var reverse = AssetDatabase.LoadAssetAtPath<VideoClip>(reversePath);

        if (forward == null || reverse == null)
        {
            Debug.LogError($"[FirstPage] Missing clip: {forwardPath} or {reversePath}");
            return false;
        }

        if (forward.frameCount != reverse.frameCount)
        {
            Debug.LogError($"[FirstPage] Frame count mismatch: {forwardPath} ({forward.frameCount}) vs {reversePath} ({reverse.frameCount})");
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
                "playerA", "playerB", "videoSurface",
                "introReverseClip", "idleClip", "idleReverseClip",
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

        bool logoFramesWired = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<UISpriteLoop>(true))
            .Any(loop => new SerializedObject(loop).FindProperty("frames").arraySize > 0);
        if (!logoFramesWired)
        {
            Debug.LogError("[FirstPage] UISpriteLoop has no logo frames wired");
            ok = false;
        }

        if (!wasOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return ok;
    }
}
