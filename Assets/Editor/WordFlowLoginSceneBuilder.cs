using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using WordFlow.Adventure.Net;
using WordFlow.Adventure.UI;

/// <summary>
/// One-click builder for the WordFlow Login scene. Run via Tools > WordFlow > Build Login Scene.
/// Creates a Canvas with the auth form, the AuthBootstrap object (AuthSession + FirebaseAuthClient),
/// the AuthScreenController, wires every serialized reference, saves the scene and registers it at
/// build index 0. Idempotent: re-running rebuilds the scene from scratch.
/// </summary>
public static class WordFlowLoginSceneBuilder
{
    [MenuItem("Tools/WordFlow/Build Login Scene")]
    public static void BuildLoginScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas (scaled for portrait mobile) + EventSystem
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Background panel
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.18f);

        CreateText(canvas.transform, "Title", "WordFlow", 72, new Vector2(0, 800), new Vector2(900, 140),
            new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 560));
        var password = CreateInput(canvas.transform, "PasswordField", "รหัสผ่าน / Password", new Vector2(0, 440),
            TMP_InputField.ContentType.Password);
        var childName = CreateInput(canvas.transform, "ChildNameField", "ชื่อของเด็ก / Child name", new Vector2(0, 320));
        var birth = CreateInput(canvas.transform, "BirthDateField", "วันเกิด YYYY-MM-DD", new Vector2(0, 200));
        var code = CreateInput(canvas.transform, "DoctorCodeField", "รหัสแพทย์ WF-XXXX (ไม่บังคับ)", new Vector2(0, 80));

        var registerBtn = CreateButton(canvas.transform, "RegisterButton", "สร้างบัญชี / Register",
            new Vector2(0, -70), new Color(0.16f, 0.5f, 0.9f));
        var loginBtn = CreateButton(canvas.transform, "LoginButton", "เข้าสู่ระบบ / Login",
            new Vector2(0, -190), new Color(0.22f, 0.6f, 0.4f));
        var forgotBtn = CreateButton(canvas.transform, "ForgotPasswordButton", "ลืมรหัสผ่าน / Forgot",
            new Vector2(0, -300), new Color(0.3f, 0.3f, 0.36f));

        var status = CreateText(canvas.transform, "StatusText", "", 34, new Vector2(0, -430), new Vector2(900, 150),
            new Color(0.9f, 0.4f, 0.4f));

        // Auth runtime objects
        var bootstrap = new GameObject("AuthBootstrap");
        var fac = bootstrap.AddComponent<FirebaseAuthClient>();
        var session = bootstrap.AddComponent<AuthSession>();

        var controllerGO = new GameObject("LoginController");
        var controller = controllerGO.AddComponent<AuthScreenController>();

        // Wire AuthSession.auth -> FirebaseAuthClient
        var soSession = new SerializedObject(session);
        SetRef(soSession, "auth", fac);
        soSession.ApplyModifiedPropertiesWithoutUndo();

        // Wire the controller's serialized references
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "passwordField", password);
        SetRef(so, "statusText", status);
        SetRef(so, "childNameField", childName);
        SetRef(so, "birthDateField", birth);
        SetRef(so, "doctorCodeField", code);
        SetRef(so, "registerButton", registerBtn);
        SetRef(so, "loginButton", loginBtn);
        SetRef(so, "forgotPasswordButton", forgotBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Save scene
        System.IO.Directory.CreateDirectory(Application.dataPath + "/Scenes");
        const string path = "Assets/Scenes/Login.unity";
        EditorSceneManager.SaveScene(scene, path);

        // Register at build index 0, keep existing scenes after it
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != path) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log("[WordFlow] Login scene built and registered at build index 0: " + path);
    }

    private static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[WordFlow] missing serialized property: " + prop); return; }
        p.objectReferenceValue = value;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 pos,
        TMP_InputField.ContentType content = TMP_InputField.ContentType.Standard)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(820, 96);
        go.GetComponent<Image>().color = Color.white;
        var input = go.AddComponent<TMP_InputField>();
        input.contentType = content;

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var art = area.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(20, 8); art.offsetMax = new Vector2(-20, -8);

        var ph = new GameObject("Placeholder", typeof(RectTransform));
        ph.transform.SetParent(area.transform, false);
        var pht = ph.AddComponent<TextMeshProUGUI>();
        pht.text = placeholder; pht.fontSize = 34; pht.color = new Color(0.5f, 0.5f, 0.5f);
        pht.alignment = TextAlignmentOptions.Left;
        Stretch(ph.GetComponent<RectTransform>());

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(area.transform, false);
        var tt = txt.AddComponent<TextMeshProUGUI>();
        tt.fontSize = 34; tt.color = Color.black; tt.alignment = TextAlignmentOptions.Left;
        Stretch(txt.GetComponent<RectTransform>());

        input.textViewport = art;
        input.textComponent = tt;
        input.placeholder = pht;
        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(820, 100);
        go.GetComponent<Image>().color = color;
        var labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        Stretch(labelGO.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
