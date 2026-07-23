using UnityEngine.Networking;

namespace WordFlow.Adventure.Net
{
    // Single source of truth for the backend host used by every Net client (TTS, grade, session,
    // telemetry, auth). ngrok-free rotates its URL on each restart, so when it changes, edit
    // BaseUrl here ONLY.
    //
    // Prepare() adds the header that stops ngrok-free from returning its HTML "You are about to
    // visit..." interstitial page instead of the real API response.
    public static class BackendConfig
    {
        // Include /api/v1, no trailing slash.
        // Local gateway (current): the grpc/Firestore TLS block on this machine is worked around by
        // forcing Firestore over REST (gateway .env FIRESTORE_TRANSPORT=rest) — local login verified
        // working end-to-end. To use the remote teammate gateway instead, swap back to:
        //   "https://germinate-debug-designed.ngrok-free.dev/api/v1"  (ngrok-free rotates its URL on restart).
        public const string BaseUrl = "http://127.0.0.1:8000/api/v1";

        public static void Prepare(UnityWebRequest req)
        {
            if (req != null) req.SetRequestHeader("ngrok-skip-browser-warning", "true");
        }
    }
}
