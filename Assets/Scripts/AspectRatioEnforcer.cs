using UnityEngine;

// Locks every camera to a 16:9 letterbox so the game frames identically on any
// monitor (16:9, 16:10, ultrawide) instead of zooming/cropping. Auto-installs at
// startup via RuntimeInitializeOnLoadMethod, so no per-scene wiring is needed.
// ponytail: applies to ALL cameras. Screen Space - Overlay canvases ignore the
// camera rect and will still draw into the black bars; switch such canvases to
// Screen Space - Camera if HUD bleeding into the bars becomes a problem.
public class AspectRatioEnforcer : MonoBehaviour
{
    const float TargetAspect = 16f / 9f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var go = new GameObject("~AspectRatioEnforcer");
        go.AddComponent<AspectRatioEnforcer>();
        DontDestroyOnLoad(go);
    }

    // Apply every frame: cameras enabled after scene load (cutscenes, etc.) and any
    // code that resets a rect still get corrected. Cheap for a handful of cameras.
    void LateUpdate() => Apply();

    void Apply()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / TargetAspect;

        Rect rect;
        if (scaleHeight < 1f) // window taller than 16:9 -> bars top/bottom
            rect = new Rect(0f, (1f - scaleHeight) / 2f, 1f, scaleHeight);
        else                  // window wider than 16:9 -> bars left/right
        {
            float scaleWidth = 1f / scaleHeight;
            rect = new Rect((1f - scaleWidth) / 2f, 0f, scaleWidth, 1f);
        }

        foreach (var cam in Camera.allCameras)
        {
            if (cam.targetTexture != null) continue; // leave RenderTexture cams (minimap/portrait) alone
            cam.rect = rect;
        }
    }
}
