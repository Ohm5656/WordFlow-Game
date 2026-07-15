using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using WordFlow.Adventure.Net;
using WordFlow.Adventure.UI;

/// <summary>
/// One-click builders for the three WordFlow auth scenes (Login / Register / ForgotPassword).
/// Each is idempotent: re-running rebuilds that scene from scratch and re-registers it in
/// EditorBuildSettings. Login is always build index 0. Run via Tools > WordFlow > ...
/// </summary>
public static class WordFlowLoginSceneBuilder
{
    // ---- menu items ----

    [MenuItem("Tools/WordFlow/Build Login Scene")]
    public static void BuildLoginScene()
    {
        var canvas = NewAuthScene(out _);
        CreateText(canvas.transform, "Title", "WordFlow", 56, new Vector2(0, 220), new Vector2(700, 90),
            new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 135));
        var password = CreateInput(canvas.transform, "PasswordField", "รหัสผ่าน / Password", new Vector2(0, 55),
            TMP_InputField.ContentType.Password);

        var loginBtn = CreateButton(canvas.transform, "LoginButton", "เข้าสู่ระบบ / Login",
            new Vector2(0, -25), new Color(0.22f, 0.6f, 0.4f));
        var toRegister = CreateLinkButton(canvas.transform, "ToRegisterLink",
            "ยังไม่มีบัญชี? สมัครสมาชิก / Register", new Vector2(0, -95));
        var toForgot = CreateLinkButton(canvas.transform, "ToForgotPasswordLink",
            "ลืมรหัสผ่าน? / Forgot Password", new Vector2(0, -155));

        var status = CreateText(canvas.transform, "StatusText", "", 30, new Vector2(0, -225),
            new Vector2(700, 70), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("LoginController");
        var controller = controllerGO.AddComponent<LoginController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "passwordField", password);
        SetRef(so, "statusText", status);
        SetRef(so, "loginButton", loginBtn);
        SetRef(so, "toRegisterLink", toRegister);
        SetRef(so, "toForgotPasswordLink", toForgot);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/Login.unity", forceIndexZero: true);
        Debug.Log("[WordFlow] Login scene built at build index 0.");
    }

    [MenuItem("Tools/WordFlow/Build Register Scene")]
    public static void BuildRegisterScene()
    {
        var canvas = NewAuthScene(out _);
        var fieldSize = new Vector2(620, 58);
        CreateText(canvas.transform, "Title", "สมัครสมาชิก / Register", 44, new Vector2(0, 250),
            new Vector2(700, 80), new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 170),
            size: fieldSize);
        var password = CreateInput(canvas.transform, "PasswordField", "รหัสผ่าน / Password", new Vector2(0, 105),
            TMP_InputField.ContentType.Password, fieldSize);
        var childName = CreateInput(canvas.transform, "ChildNameField", "ชื่อของเด็ก / Child name",
            new Vector2(0, 35), size: fieldSize);
        var birth = CreateInput(canvas.transform, "BirthDateField", "วันเกิด YYYY-MM-DD", new Vector2(0, -30),
            size: fieldSize);
        var code = CreateInput(canvas.transform, "DoctorCodeField", "รหัสแพทย์ WF-XXXX (ไม่บังคับ)",
            new Vector2(0, -100), size: fieldSize);

        var registerBtn = CreateButton(canvas.transform, "RegisterButton", "สร้างบัญชี / Register",
            new Vector2(0, -175), new Color(0.16f, 0.5f, 0.9f), new Vector2(620, 68));
        var toLogin = CreateLinkButton(canvas.transform, "ToLoginLink",
            "มีบัญชีอยู่แล้ว? เข้าสู่ระบบ / Login", new Vector2(0, -235), new Vector2(620, 40));

        var status = CreateText(canvas.transform, "StatusText", "", 28, new Vector2(0, -300),
            new Vector2(700, 64), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("RegisterController");
        var controller = controllerGO.AddComponent<RegisterController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "passwordField", password);
        SetRef(so, "childNameField", childName);
        SetRef(so, "birthDateField", birth);
        SetRef(so, "doctorCodeField", code);
        SetRef(so, "statusText", status);
        SetRef(so, "registerButton", registerBtn);
        SetRef(so, "toLoginLink", toLogin);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/Register.unity", forceIndexZero: false);
        Debug.Log("[WordFlow] Register scene built.");
    }

    [MenuItem("Tools/WordFlow/Build Forgot Password Scene")]
    public static void BuildForgotPasswordScene()
    {
        var canvas = NewAuthScene(out _);
        CreateText(canvas.transform, "Title", "ลืมรหัสผ่าน / Forgot Password", 44, new Vector2(0, 250),
            new Vector2(940, 90), new Color(0.96f, 0.96f, 0.92f));

        var email = CreateInput(canvas.transform, "EmailField", "อีเมล / Email", new Vector2(0, 140));

        var sendBtn = CreateButton(canvas.transform, "SendResetButton", "ส่งรีเซ็ตรหัสผ่าน / Send Reset",
            new Vector2(0, 40), new Color(0.16f, 0.5f, 0.9f), new Vector2(620, 72));
        var toLogin = CreateLinkButton(canvas.transform, "ToLoginLink",
            "กลับไปเข้าสู่ระบบ / Back to Login", new Vector2(0, -40));

        var status = CreateText(canvas.transform, "StatusText", "", 30, new Vector2(0, -160),
            new Vector2(700, 120), new Color(0.9f, 0.4f, 0.4f));

        CreateAuthBootstrap();
        var controllerGO = new GameObject("ForgotPasswordController");
        var controller = controllerGO.AddComponent<ForgotPasswordController>();
        var so = new SerializedObject(controller);
        SetRef(so, "emailField", email);
        SetRef(so, "statusText", status);
        SetRef(so, "sendResetButton", sendBtn);
        SetRef(so, "toLoginLink", toLogin);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndRegister("Assets/Scenes/ForgotPassword.unity", forceIndexZero: false);
        Debug.Log("[WordFlow] ForgotPassword scene built.");
    }

    [MenuItem("Tools/WordFlow/Build All Auth Scenes")]
    public static void BuildAllAuthScenes()
    {
        // Order matters: build the two secondary scenes first, then Login last so it lands at
        // index 0 and the secondaries are appended after it.
        BuildRegisterScene();
        BuildForgotPasswordScene();
        BuildLoginScene();
        Debug.Log("[WordFlow] All three auth scenes built and registered.");
    }

    // ---- shared scene scaffolding ----

    private static Canvas NewAuthScene(out UnityEngine.SceneManagement.Scene scene)
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.18f);

        return canvas;
    }

    private static void CreateAuthBootstrap()
    {
        var bootstrap = new GameObject("AuthBootstrap");
        var fac = bootstrap.AddComponent<FirebaseAuthClient>();
        var session = bootstrap.AddComponent<AuthSession>();
        var soSession = new SerializedObject(session);
        SetRef(soSession, "auth", fac);
        soSession.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SaveAndRegister(string path, bool forceIndexZero)
    {
        System.IO.Directory.CreateDirectory(Application.dataPath + "/Scenes");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);

        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        existing.RemoveAll(s => s.path == path);
        if (forceIndexZero)
            existing.Insert(0, new EditorBuildSettingsScene(path, true));
        else
            existing.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = existing.ToArray();
    }

    // ---- element helpers (unchanged from the original builder) ----

    private static TMP_FontAsset _thaiFont;
    private static TMP_FontAsset ThaiFont =>
        _thaiFont != null ? _thaiFont : (_thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LeelawUI SDF.asset"));

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
        t.font = ThaiFont;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 pos,
        TMP_InputField.ContentType content = TMP_InputField.ContentType.Standard, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size ?? new Vector2(620, 96);
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
        pht.alignment = TextAlignmentOptions.Left; pht.font = ThaiFont;
        Stretch(ph.GetComponent<RectTransform>());

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(area.transform, false);
        var tt = txt.AddComponent<TextMeshProUGUI>();
        tt.fontSize = 34; tt.color = Color.black; tt.alignment = TextAlignmentOptions.Left; tt.font = ThaiFont;
        Stretch(txt.GetComponent<RectTransform>());

        input.textViewport = art;
        input.textComponent = tt;
        input.placeholder = pht;
        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color color,
        Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size ?? new Vector2(620, 100);
        go.GetComponent<Image>().color = color;
        var labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.font = ThaiFont;
        Stretch(labelGO.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    // A clickable text link: transparent background Image (so Button raycasts) + coloured label.
    private static Button CreateLinkButton(Transform parent, string name, string label, Vector2 pos,
        Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size ?? new Vector2(620, 70);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent but still raycasts
        var labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 32; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.55f, 0.75f, 1f); t.font = ThaiFont;
        Stretch(labelGO.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
