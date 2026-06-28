using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WordFlow.Adventure.Net;

// One-shot: CutScene_bear has a SessionContext but no TelemetryClient, so build-attempts and the
// session-aggregate PATCH never send. This attaches a TelemetryClient (baseUrl 8001, matching
// word_build_paa_polished) onto the SessionContext GameObject and wires SessionContext.telemetry.
public static class AddBearTelemetry
{
    private const string ScenePath = "Assets/Scenes/region 1/CutScene_bear.unity";

    [MenuItem("Tools/Bear/Add TelemetryClient To Scene")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SessionContext session = Object.FindFirstObjectByType<SessionContext>(FindObjectsInactive.Include);
        if (session == null)
        {
            Debug.LogError("[AddBearTelemetry] No SessionContext in CutScene_bear.");
            return;
        }

        TelemetryClient tele = session.GetComponent<TelemetryClient>();
        if (tele == null) tele = session.gameObject.AddComponent<TelemetryClient>();

        // Match word_build_paa_polished's TelemetryClient (default baseUrl is 8000).
        var teleSo = new SerializedObject(tele);
        teleSo.FindProperty("baseUrl").stringValue = "http://127.0.0.1:8001/api/v1";
        teleSo.FindProperty("authorization").stringValue = "Bearer demo-token";
        teleSo.ApplyModifiedPropertiesWithoutUndo();

        var sessSo = new SerializedObject(session);
        sessSo.FindProperty("telemetry").objectReferenceValue = tele;
        sessSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AddBearTelemetry] TelemetryClient added + wired (baseUrl 8001). Scene saved.");
    }
}
