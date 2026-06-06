using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using EmergencySim;

/// <summary>
/// Builds the scenario-selection menu (MainMenu.unity): deep-navy vignette background, a bold
/// title with a soft-red cross + underline accent, and large rounded buttons in a pastel palette
/// (each scenario its own accent). Buttons are wired to MainMenu's methods. All scenes are
/// registered in Build Settings with MainMenu at index 0. Run: Tools > MainMenu > Build.
/// </summary>
public static class MainMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    // Pastel palette.
    static readonly Color Navy      = new Color(0.07f, 0.08f, 0.13f);   // dark text / deep base
    static readonly Color SoftRed   = new Color(0.96f, 0.50f, 0.50f);   // title accent (ambulance red, pastel)
    static readonly Color Amber     = new Color(0.99f, 0.82f, 0.55f);   // Gas Explosion
    static readonly Color Coral     = new Color(0.97f, 0.66f, 0.64f);   // Car Collision
    static readonly Color Mint      = new Color(0.66f, 0.88f, 0.80f);   // Falling Tree
    static readonly Color GrayPastel= new Color(0.80f, 0.82f, 0.86f);   // Quit

    [MenuItem("Tools/MainMenu/Build")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
        camGo.AddComponent<AudioListener>();

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var module = esGo.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();

        var canvasGo = new GameObject("MenuCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        // Deep navy / near-black background.
        var bg = MakeImage(root, "Background", Vector2.zero, Vector2.zero, new Color(0.05f, 0.06f, 0.12f), null);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var rounded = GetRoundedSprite();

        // Title: red cross glyph above, bold white title, soft-red underline bar below.
        MakeImage(root, "CrossV", new Vector2(0f, 452f), new Vector2(16f, 62f), SoftRed, null);
        MakeImage(root, "CrossH", new Vector2(0f, 452f), new Vector2(62f, 16f), SoftRed, null);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(root, false);
        var trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 350f);
        trt.sizeDelta = new Vector2(1600f, 150f);
        var title = titleGo.AddComponent<Text>();
        title.text = "Virtual Ambulance Emergency Response Simulation";
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.fontSize = 54;
        title.fontStyle = FontStyle.Bold;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        MakeImage(root, "TitleUnderline", new Vector2(0f, 285f), new Vector2(900f, 9f), SoftRed, rounded);

        // Menu logic.
        var menuGo = new GameObject("MainMenu");
        var menu = menuGo.AddComponent<MainMenu>();

        // Four pastel buttons. (Label "Scenario 3 - Falling Tree" loads the Scenario4 scene.)
        MakeButton(root, "Btn_Scenario1", "Scenario 1 - Gas Explosion", 150f, Amber, rounded, new UnityAction(menu.LoadScenario1));
        MakeButton(root, "Btn_Scenario2", "Scenario 2 - Car Collision", 52f, Coral, rounded, new UnityAction(menu.LoadScenario2));
        MakeButton(root, "Btn_Scenario4", "Scenario 3 - Falling Tree", -46f, Mint, rounded, new UnityAction(menu.LoadScenario4));
        MakeButton(root, "Btn_Quit", "Quit", -178f, GrayPastel, rounded, new UnityAction(menu.QuitApp));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Build Settings: MainMenu index 0, then all four scenario scenes (Scenario3 stays listed).
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario1.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario2.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario3.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario4.unity", true),
        };

        AssetDatabase.SaveAssets();
        Debug.Log("[MainMenuBuilder] BUILD COMPLETE — pastel menu (4 buttons), Build Settings set (MainMenu index 0).");
    }

    static void MakeButton(Transform parent, string name, string label, float y, Color accent, Sprite rounded, UnityAction action)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(900f, 88f);

        var img = go.AddComponent<Image>();
        img.color = Color.white;            // tinted by the Button's ColorBlock
        if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }

        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = accent;
        cb.highlightedColor = Color.Lerp(accent, Color.white, 0.22f);
        cb.pressedColor = Color.Lerp(accent, Color.black, 0.22f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Navy;                   // dark text reads on the pastel buttons
        txt.fontSize = 32;
        txt.fontStyle = FontStyle.Bold;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        UnityEventTools.AddPersistentListener(btn.onClick, action);
    }

    static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        return img;
    }

    static Sprite GetRoundedSprite()
    {
        // Built-in rounded UI sprite (no external asset).
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    // --- verification helpers ---
    [MenuItem("Tools/MainMenu/DEBUG Verify Buttons")]
    public static void VerifyButtons()
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            int n = b.onClick.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
            {
                var tgt = b.onClick.GetPersistentTarget(i);
                Debug.Log($"[MainMenu] '{b.name}' -> {(tgt != null ? tgt.GetType().Name : "null")}.{b.onClick.GetPersistentMethodName(i)}");
            }
        }
    }

    static void ClickButton(string name)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (b.name == name) { b.onClick.Invoke(); Debug.Log($"[MainMenu] Invoked {b.name}"); return; }
        Debug.LogError($"[MainMenu] {name} not found");
    }

    [MenuItem("Tools/MainMenu/DEBUG Click S1")] static void C1() => ClickButton("Btn_Scenario1");
    [MenuItem("Tools/MainMenu/DEBUG Click S2")] static void C2() => ClickButton("Btn_Scenario2");
    [MenuItem("Tools/MainMenu/DEBUG Click Tree")] static void C4() => ClickButton("Btn_Scenario4");
}
