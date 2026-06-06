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
/// Builds the scenario-selection menu (MainMenu.unity): dark canvas, title, five large buttons
/// wired to MainMenu's methods, and registers all scenes in Build Settings with MainMenu at
/// index 0. Run: Tools > MainMenu > Build.
/// </summary>
public static class MainMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/MainMenu/Build")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera (dark clear) + audio listener.
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
        camGo.AddComponent<AudioListener>();

        // EventSystem (new Input System UI module so the buttons are clickable).
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var module = esGo.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();

        // Canvas (rendered through the camera so it's a full-screen menu) + scaler + raycaster.
        var canvasGo = new GameObject("MenuCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Dark full-screen background.
        var bg = new GameObject("Background");
        bg.transform.SetParent(canvasGo.transform, false);
        var brt = bg.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        var bImg = bg.AddComponent<Image>();
        bImg.color = new Color(0.07f, 0.08f, 0.11f, 1f);

        // Title.
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        var trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 380f);
        trt.sizeDelta = new Vector2(1500f, 160f);
        var title = titleGo.AddComponent<Text>();
        title.text = "Virtual Ambulance Emergency Response Simulation";
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.fontSize = 52;
        title.fontStyle = FontStyle.Bold;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // MainMenu logic component.
        var menuGo = new GameObject("MainMenu");
        var menu = menuGo.AddComponent<MainMenu>();

        // Five large vertical buttons, wired to the menu methods.
        var parent = canvasGo.transform;
        MakeButton(parent, "Btn_Scenario1", "Scenario 1 - Gas Explosion (Press X to trigger)", 170f, new UnityAction(menu.LoadScenario1));
        MakeButton(parent, "Btn_Scenario2", "Scenario 2 - Car Collision", 80f, new UnityAction(menu.LoadScenario2));
        MakeButton(parent, "Btn_Scenario3", "Scenario 3 - Stretcher Rescue", -10f, new UnityAction(menu.LoadScenario3));
        MakeButton(parent, "Btn_Scenario4", "Scenario 4 - Falling Tree", -100f, new UnityAction(menu.LoadScenario4));
        MakeButton(parent, "Btn_Quit", "Quit", -210f, new UnityAction(menu.QuitApp));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Build Settings: MainMenu at index 0, then the four scenarios.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario1.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario2.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario3.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scenario4.unity", true),
        };

        AssetDatabase.SaveAssets();
        Debug.Log("[MainMenuBuilder] BUILD COMPLETE — MainMenu.unity created, Build Settings set (MainMenu index 0).");
    }

    static void MakeButton(Transform parent, string name, string label, float y, UnityAction action)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(880f, 78f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.2f, 0.27f, 1f);
        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = new Color(0.16f, 0.2f, 0.27f, 1f);
        cb.highlightedColor = new Color(0.24f, 0.32f, 0.44f, 1f);
        cb.pressedColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = 30;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        UnityEventTools.AddPersistentListener(btn.onClick, action);
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

    static void ClickScenario(int sceneNum)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (b.name == "Btn_Scenario" + sceneNum) { b.onClick.Invoke(); Debug.Log($"[MainMenu] Invoked {b.name}"); return; }
        Debug.LogError($"[MainMenu] Btn_Scenario{sceneNum} not found");
    }

    [MenuItem("Tools/MainMenu/DEBUG Click S1")] static void C1() => ClickScenario(1);
    [MenuItem("Tools/MainMenu/DEBUG Click S2")] static void C2() => ClickScenario(2);
    [MenuItem("Tools/MainMenu/DEBUG Click S3")] static void C3() => ClickScenario(3);
    [MenuItem("Tools/MainMenu/DEBUG Click S4")] static void C4() => ClickScenario(4);
}
