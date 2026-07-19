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
        ok &= CheckClipPair("Assets/Video/intro.mp4", "Assets/Video/intro_rev.mp4");
        ok &= CheckClipPair("Assets/Video/idle.mp4", "Assets/Video/idle_rev.mp4");
        ok &= CheckBuildSettings();
        ok &= CheckSceneWiring();

        Debug.Log(ok ? "[FirstPage] Validate: ALL CHECKS PASSED" : "[FirstPage] Validate: FAILED — see errors above");
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
            foreach (var field in new[] { "playerA", "playerB", "videoSurface", "pressToStartGroup", "introClip", "introReverseClip", "idleClip", "idleReverseClip" })
            {
                var p = so.FindProperty(field);
                if (p == null || p.objectReferenceValue == null)
                {
                    Debug.LogError($"[FirstPage] FirstPageIntro.{field} is unassigned");
                    ok = false;
                }
            }
        }

        if (!wasOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return ok;
    }
}
