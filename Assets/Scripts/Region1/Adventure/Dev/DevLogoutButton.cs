#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;
using WordFlow.Adventure.Net;

namespace WordFlow.Adventure.Dev
{
    /// <summary>
    /// TESTING ONLY. A small logout button in the bottom-right of the WorldMap scene.
    ///
    /// It self-injects at runtime (no scene or prefab edits needed) and is compiled out of
    /// release builds by the surrounding #if. Clicking it clears the stored session via
    /// <see cref="AuthSession.Logout"/> (drops the in-memory tokens and deletes the persisted
    /// refresh token) and returns to the Login scene, so the login form can be re-tested without
    /// clearing PlayerPrefs by hand.
    ///
    /// Drawn with IMGUI (OnGUI) on purpose: it needs no Canvas, EventSystem, input module or font
    /// asset, so it works even though the WorldMap scene has no EventSystem.
    /// </summary>
    public sealed class DevLogoutButton : MonoBehaviour
    {
        private const string TargetScene = "WorldMap";
        private const string LoginScene = "Login";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, _) =>
            {
                if (scene.name == TargetScene) Spawn();
            };
            // Covers entering Play with WorldMap already the active scene (sceneLoaded may have
            // fired before this hook registered).
            if (SceneManager.GetActiveScene().name == TargetScene) Spawn();
        }

        private static void Spawn()
        {
            if (FindFirstObjectByType<DevLogoutButton>() != null) return;
            new GameObject("[DevLogoutButton]").AddComponent<DevLogoutButton>();
        }

        private void OnGUI()
        {
            const float w = 150f, h = 44f, pad = 16f;
            var rect = new Rect(Screen.width - w - pad, Screen.height - h - pad, w, h);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.80f, 0.20f, 0.20f, 0.95f);
            var style = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };

            if (GUI.Button(rect, "Logout (dev)", style))
            {
                AuthSession.Instance?.Logout();
                SceneManager.LoadScene(LoginScene);
            }

            GUI.backgroundColor = prevBg;
        }
    }
}
#endif
