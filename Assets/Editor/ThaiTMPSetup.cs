using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class ThaiTMPSetup
{
    const string FontTtfPath  = "Assets/Fonts/LeelawUI.ttf";
    const string FontAssetPath = "Assets/Fonts/LeelawUI SDF.asset";
    const string LoginScenePath = "Assets/Scenes/Login.unity";

    [MenuItem("Tools/WordFlow/Setup Thai TMP Font")]
    public static void SetupThaiFont()
    {
        // 1. Load the system font we copied in
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
        if (font == null)
        {
            Debug.LogError("[WordFlow] LeelawUI.ttf not found at " + FontTtfPath);
            return;
        }

        // 2. Create (or reload) the SDF asset
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            // Dynamic mode: Unity bakes glyphs on demand at runtime — Thai chars render as soon as
            // they're needed without a pre-bake step, so no character-set spec required here.
            fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                90,   // sampling point size
                9,    // atlas padding
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024,
                TMPro.AtlasPopulationMode.Dynamic,
                true  // multi-atlas support for large character sets
            );
            fontAsset.name = "LeelawUI SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[WordFlow] Created " + FontAssetPath);
        }
        else
        {
            Debug.Log("[WordFlow] Reusing existing " + FontAssetPath);
        }

        // 3. Open Login scene additively if not already loaded
        bool loginWasOpen = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).path == LoginScenePath) { loginWasOpen = true; break; }

        Scene loginScene;
        if (!loginWasOpen)
            loginScene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Additive);
        else
            loginScene = SceneManager.GetSceneByPath(LoginScenePath);

        // 4. Wire every TMP text + input field in the Login scene
        int count = 0;
        foreach (var root in loginScene.GetRootGameObjects())
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.font = fontAsset;
                EditorUtility.SetDirty(tmp);
                count++;
            }
            foreach (var inp in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (inp.textComponent != null)
                {
                    inp.textComponent.font = fontAsset;
                    EditorUtility.SetDirty(inp.textComponent);
                    count++;
                }
                if (inp.placeholder is TMP_Text ph)
                {
                    ph.font = fontAsset;
                    EditorUtility.SetDirty(ph);
                    count++;
                }
            }
        }

        EditorSceneManager.SaveScene(loginScene);

        if (!loginWasOpen)
            EditorSceneManager.CloseScene(loginScene, true);

        AssetDatabase.Refresh();
        Debug.Log($"[WordFlow] Thai font wired to {count} TMP components in Login scene.");
    }
}
