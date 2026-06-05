using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using EmergencySim;

/// <summary>
/// One-shot, re-runnable builder for the "Falling Tree on Kate" cinematic (Scenario4).
/// Encodes the entire scene deterministically so it can be rebuilt from the menu:
/// Tools > Scenario4 > Build All. (Used instead of the MCP snippet compiler, which is
/// unavailable in this project due to a compiler command-line length limit.)
/// </summary>
public static class Scenario4Builder
{
    // ---- Tunable layout ----
    // Kate walks the LEFT SIDEWALK (x≈-5.5, where the street trees are — not the road at x≈0).
    // The hero tree stands on the sidewalk AHEAD of her and topples back DOWN the path toward
    // her, so the long trunk lands ahead (+z, past her) and only the wide canopy comes onto her.
    static readonly Vector3 TreeBase = new Vector3(-6.5f, 0f, 18f); // canopy lands ~z=10, near-edge over Kate (z=9)
    static readonly Vector3 KateStop = new Vector3(-6.5f, 0f, 9f);

    const string ModelDir = "Assets/Models/";
    const string PedDir = "Assets/Models/Pedestrians/"; // textured, Humanoid background pedestrians
    const string CtrlDir = "Assets/Animations/Controllers/";
    const string ChannelPath = "Assets/Scripts/Channels/RescueRequested.asset";

    [MenuItem("Tools/Scenario4/Build All")]
    public static void BuildAll()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("Scenario4"))
        {
            Debug.LogError("[Scenario4Builder] Active scene is not Scenario4. Open Assets/Scenes/Scenario4.unity first.");
            return;
        }

        // Clear and rebuild from scratch.
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        Debug.Log("[Scenario4Builder] Cleared scene, building...");

        // Load controllers.
        var kateAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "KateAC.controller");
        var witnessAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "WitnessAC.controller");
        var bgAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "BackgroundAC.controller");

        // ---------- Camera ----------
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        camGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
        cam.fieldOfView = 55f;
        cam.farClipPlane = 200f;

        // ---------- Lighting (4 reqs) ----------
        // 1) Directional moonlight
        var moonGo = new GameObject("Directional Light (Moon)");
        moonGo.transform.rotation = Quaternion.Euler(48f, -150f, 0f);
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 1f);
        moon.intensity = 0.45f;
        moon.shadows = LightShadows.Soft;

        // 2) Spot street-lamps along the path
        var lampsParent = new GameObject("StreetLamps").transform;
        float[] lampZ = { -4f, 2f, 8f };
        foreach (var z in lampZ)
        {
            var lg = new GameObject($"StreetLamp_{z}");
            lg.transform.SetParent(lampsParent);
            lg.transform.position = new Vector3(-5f, 4.5f, z);
            lg.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // point down
            var l = lg.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.85f, 0.6f);
            l.intensity = 14f;
            l.range = 14f;
            l.spotAngle = 68f;
            l.shadows = LightShadows.Soft;
        }

        // 3) Point light at the accident
        var pointGo = new GameObject("AccidentPointLight");
        pointGo.transform.position = new Vector3(-6.5f, 2.6f, 9f);
        var point = pointGo.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = new Color(1f, 0.78f, 0.6f);
        point.intensity = 9f;
        point.range = 10f;

        // 4) Ambient / environment night tuning
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.14f);
        RenderSettings.fog = false;

        // ---------- Ground ----------
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.02f, 0f);
        ground.transform.localScale = new Vector3(8f, 1f, 8f);
        // Wet-look ground: darker + glossy so the lamps/lightning reflect (rainy night).
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.09f));
        groundMat.SetFloat("_Smoothness", 0.85f);
        groundMat.SetFloat("_Metallic", 0.1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // ---------- Environment + obstacle colliders ----------
        var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Environment.fbx");
        var env = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
        env.name = "Environment";
        env.transform.position = Vector3.zero;
        int colliders = 0;
        foreach (Transform t in env.transform)
        {
            if (t.name.StartsWith("Bldg") || t.name.StartsWith("Trunk"))
            {
                t.gameObject.AddComponent<BoxCollider>();
                colliders++;
            }
        }
        Debug.Log($"[Scenario4Builder] Environment obstacle colliders: {colliders}");

        // ---------- Characters ----------
        // Kate (Ch21) — the victim, walking the LEFT SIDEWALK (x≈-5.5)
        var kate = MakeCharacter(ModelDir + "Ch21.fbx", "Kate", new Vector3(-6.5f, 0f, -9f), kateAC);
        var kateCap = kate.GetComponent<CapsuleCollider>();
        kateCap.isTrigger = true; // detected by tree trigger; doesn't physically block the fall
        var kateAnim = kate.GetComponent<Animator>();
        var kateFollower = kate.AddComponent<WaypointFollower>();
        kateFollower.animator = kateAnim;
        kateFollower.speed = 1.5f;
        var kateVictim = kate.AddComponent<KateVictim>();
        kateVictim.animator = kateAnim;
        kateVictim.follower = kateFollower;

        // Witness (textured pedestrian Ch01) — stands on the curb/road near the accident, watching.
        // NOTE: Ch16/Chad is intentionally NOT in this scene — he's the paramedic, saved for the rescue.
        var witness = MakeCharacter(PedDir + "Ch01.fbx", "Witness", new Vector3(-3.5f, 0f, 5f), witnessAC);
        ApplyExtractedTextures(witness, "Ch01");
        var witnessAnim = witness.GetComponent<Animator>();
        var witnessFollower = witness.AddComponent<WaypointFollower>();
        witnessFollower.animator = witnessAnim;
        witnessFollower.speed = 1.3f;
        var witnessCtrl = witness.AddComponent<WitnessController>();
        witnessCtrl.animator = witnessAnim;
        witnessCtrl.follower = witnessFollower;
        witnessCtrl.lookAtTarget = kate.transform;
        witnessCtrl.phoneProp = MakePhoneProp(witness);

        // Background pedestrians (textured, from Pedestrians folder): Ch02 + Ch33, on the right sidewalk
        var bg1 = MakeCharacter(PedDir + "Ch02.fbx", "Background1", new Vector3(5f, 0f, -9f), bgAC);
        ApplyExtractedTextures(bg1, "Ch02");
        var bg1Follower = bg1.AddComponent<WaypointFollower>();
        bg1Follower.animator = bg1.GetComponent<Animator>();
        bg1Follower.speed = 1.4f; bg1Follower.loop = true;

        var bg2 = MakeCharacter(PedDir + "Ch33.fbx", "Background2", new Vector3(6.5f, 0f, 12f), bgAC);
        ApplyExtractedTextures(bg2, "Ch33");
        var bg2Follower = bg2.AddComponent<WaypointFollower>();
        bg2Follower.animator = bg2.GetComponent<Animator>();
        bg2Follower.speed = 1.4f; bg2Follower.loop = true;

        // ---------- Paths ----------
        var paths = new GameObject("Paths").transform;
        kateFollower.waypoints = MakePath("Kate", new[] {
            new Vector3(-6.5f,0f,-9f), new Vector3(-6.5f,0f,-3f), new Vector3(-6.5f,0f,3f), KateStop }, paths);
        witnessFollower.waypoints = MakePath("Witness", new[] {
            new Vector3(-3.5f,0f,5f), new Vector3(-4f,0f,8.5f) }, paths);
        bg1Follower.waypoints = MakePath("BG1", new[] {
            new Vector3(5f,0f,-9f), new Vector3(5f,0f,12f) }, paths);
        bg2Follower.waypoints = MakePath("BG2", new[] {
            new Vector3(6.5f,0f,12f), new Vector3(6.5f,0f,-9f) }, paths);

        // Face start directions.
        kate.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        witness.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        bg1.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        bg2.transform.rotation = Quaternion.LookRotation(Vector3.back);

        // ---------- Hero tree (physics) ----------
        var tree = new GameObject("HeroTree");
        tree.transform.position = TreeBase;
        var rb = tree.AddComponent<Rigidbody>();
        rb.mass = 300f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var barkMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        barkMat.SetColor("_BaseColor", new Color(0.33f, 0.22f, 0.12f));
        var leafMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        leafMat.SetColor("_BaseColor", new Color(0.16f, 0.34f, 0.14f));

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0f, 4f, 0f);
        trunk.transform.localScale = new Vector3(0.6f, 4f, 0.6f);
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        var trunkCol = trunk.AddComponent<CapsuleCollider>();
        trunkCol.direction = 1; trunkCol.height = 2f; trunkCol.radius = 0.5f;
        trunk.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

        // Bubbly canopy — a cluster of overlapping foliage spheres (like the env street trees),
        // not one smooth ball. Parent group at the trunk top so it topples with the tree.
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(tree.transform);
        canopy.transform.localPosition = new Vector3(0f, 8f, 0f);
        // Tighter, smaller bubbles — a compact canopy on the scale of the street trees.
        var bubbles = new[] {
            new Vector4( 0.0f,  0.15f,  0.0f, 1.3f),
            new Vector4( 0.6f, -0.10f,  0.3f, 1.0f),
            new Vector4(-0.6f, -0.10f, -0.3f, 1.05f),
            new Vector4( 0.3f,  0.05f, -0.6f, 0.95f),
            new Vector4(-0.3f,  0.10f,  0.6f, 1.0f),
            new Vector4( 0.1f,  0.65f, -0.1f, 0.9f),
        };
        foreach (var b in bubbles)
        {
            var bub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bub.name = "Foliage";
            bub.transform.SetParent(canopy.transform);
            bub.transform.localPosition = new Vector3(b.x, b.y, b.z);
            bub.transform.localScale = new Vector3(b.w, b.w, b.w);
            Object.DestroyImmediate(bub.GetComponent<Collider>());
            bub.GetComponent<MeshRenderer>().sharedMaterial = leafMat;
        }
        // One solid collider for the whole canopy so it rests on the ground when fallen.
        var canopySolid = canopy.AddComponent<SphereCollider>();
        canopySolid.radius = 1.4f;

        var trigGo = new GameObject("ImpactTrigger");
        trigGo.transform.SetParent(tree.transform);
        trigGo.transform.localPosition = new Vector3(0f, 5f, 0f);
        var trig = trigGo.AddComponent<CapsuleCollider>();
        trig.direction = 1; trig.height = 11f; trig.radius = 1.4f; trig.isTrigger = true;
        var canopyTrigger = trigGo.AddComponent<TreeCanopyTrigger>();

        var hinge = tree.AddComponent<HingeJoint>();
        hinge.anchor = Vector3.zero;
        hinge.axis = new Vector3(1f, 0f, 0f); // rotates in the y-z plane → falls along z
        hinge.useLimits = false;
        hinge.connectedBody = null;

        var treeFall = tree.AddComponent<TreeFallController>();
        treeFall.body = rb;
        treeFall.hinge = hinge;
        treeFall.toppleTorque = 500f;
        treeFall.torqueAxisLocal = new Vector3(-1f, 0f, 0f); // tips top toward -z (down the path onto Kate)
        canopyTrigger.controller = treeFall;

        // Dedicated canopy trigger so the wide canopy reliably registers the hit on Kate
        // even when she lies slightly off the thin trunk's centerline.
        var canopyTrigGo = new GameObject("CanopyTrigger");
        canopyTrigGo.transform.SetParent(tree.transform);
        canopyTrigGo.transform.localPosition = new Vector3(0f, 8f, 0f);
        var canopyTrigCol = canopyTrigGo.AddComponent<SphereCollider>();
        canopyTrigCol.radius = 1.9f; canopyTrigCol.isTrigger = true;
        var canopyTrigger2 = canopyTrigGo.AddComponent<TreeCanopyTrigger>();
        canopyTrigger2.controller = treeFall;

        // ---------- Debris particles ----------
        var debrisGo = new GameObject("DebrisBurst");
        debrisGo.transform.position = new Vector3(-6.5f, 1.2f, 9f);
        var ps = debrisGo.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = 1.3f; main.startSpeed = 3.5f; main.startSize = 0.13f;
        main.gravityModifier = 1.3f; main.maxParticles = 150;
        main.startColor = new Color(0.45f, 0.33f, 0.16f);
        main.playOnAwake = false;
        var em = ps.emission;
        em.enabled = true; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
        var shape = ps.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f; shape.radius = 0.35f;
        var psr = debrisGo.GetComponent<ParticleSystemRenderer>();
        var debrisMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        debrisMat.SetColor("_BaseColor", new Color(0.5f, 0.38f, 0.2f));
        psr.sharedMaterial = debrisMat;
        kateVictim.debrisBurst = ps;

        // ---------- Audio (wind + rain ambience; thunder one-shot) ----------
        var audioRoot = new GameObject("Audio").transform;
        var windGo = new GameObject("Wind"); windGo.transform.SetParent(audioRoot);
        var windSrc = windGo.AddComponent<AudioSource>();
        windSrc.clip = GetOrCreateWindClip(); windSrc.loop = true; windSrc.playOnAwake = true;
        windSrc.volume = 0.28f; windSrc.spatialBlend = 0f;

        var rainAudioGo = new GameObject("RainAudio"); rainAudioGo.transform.SetParent(audioRoot);
        var rainSrc = rainAudioGo.AddComponent<AudioSource>();
        rainSrc.clip = GetOrCreateRainClip(); rainSrc.loop = true; rainSrc.playOnAwake = true;
        rainSrc.volume = 0.42f; rainSrc.spatialBlend = 0f;

        var thunderGo = new GameObject("Thunder"); thunderGo.transform.SetParent(audioRoot);
        var thunderSrc = thunderGo.AddComponent<AudioSource>();
        thunderSrc.clip = GetOrCreateThunderClip(); thunderSrc.loop = false; thunderSrc.playOnAwake = false;
        thunderSrc.volume = 0.85f; thunderSrc.spatialBlend = 0f;

        // ---------- Rain particles (thin fast streaks, density over size) ----------
        var rainGo = new GameObject("Rain");
        rainGo.transform.position = new Vector3(-3f, 15f, 6f);
        var rain = rainGo.AddComponent<ParticleSystem>();
        rain.Stop();
        var rmain = rain.main;
        rmain.startLifetime = 1.1f;
        rmain.startSpeed = 0f;                 // velocity comes from velocityOverLifetime (fast + angled)
        rmain.startSize = 0.02f;               // very thin drops
        rmain.startColor = new Color(0.78f, 0.84f, 0.98f, 0.35f); // pale blue-white, low alpha
        rmain.maxParticles = 6000;
        rmain.gravityModifier = 0f;
        rmain.playOnAwake = true;
        rmain.simulationSpace = ParticleSystemSimulationSpace.World;
        var rem = rain.emission; rem.rateOverTime = 900f;          // density sells it
        var rshape = rain.shape; rshape.shapeType = ParticleSystemShapeType.Box; rshape.scale = new Vector3(44f, 1f, 44f);
        var rvel = rain.velocityOverLifetime;
        rvel.enabled = true; rvel.space = ParticleSystemSimulationSpace.World;
        rvel.x = new ParticleSystem.MinMaxCurve(-2.5f);            // slight wind slant
        rvel.y = new ParticleSystem.MinMaxCurve(-18f);             // fast downward
        var rrend = rainGo.GetComponent<ParticleSystemRenderer>();
        rrend.renderMode = ParticleSystemRenderMode.Stretch;
        rrend.cameraVelocityScale = 0f;
        rrend.velocityScale = 0.09f;           // stretch with speed → long thin streaks
        rrend.lengthScale = 6f;
        var rainMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        rainMat.SetColor("_BaseColor", new Color(0.8f, 0.86f, 1f, 0.35f));
        rainMat.SetFloat("_Surface", 1f);          // transparent
        rainMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        rainMat.renderQueue = 3000;
        rrend.sharedMaterial = rainMat;
        rain.Play();

        // ---------- Splash particles at ground level (subtle polish) ----------
        var splashGo = new GameObject("RainSplashes");
        splashGo.transform.position = new Vector3(-4f, 0.03f, 6f);
        var splash = splashGo.AddComponent<ParticleSystem>();
        splash.Stop();
        var smain = splash.main;
        smain.startLifetime = 0.35f;
        smain.startSpeed = 0.6f;
        smain.startSize = 0.05f;
        smain.startColor = new Color(0.8f, 0.86f, 1f, 0.3f);
        smain.gravityModifier = 1.2f;
        smain.maxParticles = 1500;
        smain.playOnAwake = true;
        smain.simulationSpace = ParticleSystemSimulationSpace.World;
        var sem = splash.emission; sem.rateOverTime = 250f;
        var sshape = splash.shape; sshape.shapeType = ParticleSystemShapeType.Box; sshape.scale = new Vector3(30f, 0.1f, 30f);
        var srend = splashGo.GetComponent<ParticleSystemRenderer>();
        srend.sharedMaterial = rainMat;
        splash.Play();

        // ---------- Storm controller (lightning flash + thunder) ----------
        var stormGo = new GameObject("StormController");
        var storm = stormGo.AddComponent<StormController>();
        storm.moonLight = moon;
        storm.thunderSource = thunderSrc;
        storm.baseIntensity = moon.intensity;

        // ---------- Camera shots (markers are user-adjustable in the Scene view) ----------
        var shotsParent = new GameObject("CameraShots").transform;

        // 0: wide establishing — Kate walking the left sidewalk toward the looming tree
        var shot1 = new GameObject("Shot1_Wide").transform;
        shot1.SetParent(shotsParent);
        shot1.position = new Vector3(1f, 4.5f, 0f);
        shot1.LookAt(new Vector3(-6.5f, 2.2f, 11f));

        // 1: fall close-up — tighter, low angle on Kate + the tree coming down
        var shot2 = new GameObject("Shot2_FallCloseup").transform;
        shot2.SetParent(shotsParent);
        shot2.position = new Vector3(-2.5f, 1.5f, 5.5f);
        shot2.LookAt(new Vector3(-6.5f, 1.8f, 9.5f));

        // 2: witness — medium on the pedestrian reacting / calling 911
        var shot3 = new GameObject("Shot3_Witness").transform;
        shot3.SetParent(shotsParent);
        shot3.position = new Vector3(0.5f, 1.9f, 6f);
        shot3.LookAt(new Vector3(-4f, 1.5f, 8.5f));

        // 3: witness alt — opposite/closer angle during the call, accident visible behind
        var shot4 = new GameObject("Shot4_WitnessAlt").transform;
        shot4.SetParent(shotsParent);
        shot4.position = new Vector3(-8.5f, 1.7f, 11f);
        shot4.LookAt(new Vector3(-4f, 1.4f, 8.5f));

        var camDir = camGo.AddComponent<CameraDirector>();
        camDir.cam = cam;
        camDir.shots = new[] { shot1, shot2, shot3, shot4 };
        camGo.transform.SetPositionAndRotation(shot1.position, shot1.rotation);

        // ---------- Rescue channel + stub ----------
        var channel = AssetDatabase.LoadAssetAtPath<Vector3GameEvent>(ChannelPath);
        if (channel == null)
        {
            channel = ScriptableObject.CreateInstance<Vector3GameEvent>();
            AssetDatabase.CreateAsset(channel, ChannelPath);
        }
        var stubGo = new GameObject("RescueHandoff");
        var stub = stubGo.AddComponent<RescueHandoffStub>();
        var soStub = new SerializedObject(stub);
        soStub.FindProperty("rescueChannel").objectReferenceValue = channel;
        soStub.ApplyModifiedProperties();

        // ---------- Director ----------
        var dirGo = new GameObject("ScenarioDirector");
        var dir = dirGo.AddComponent<ScenarioDirector>();
        dir.kate = kate.transform;
        dir.kateFollower = kateFollower;
        dir.backgroundFollowers = new[] { bg1Follower, bg2Follower, witnessFollower };
        dir.kateVictim = kateVictim;
        dir.witness = witnessCtrl;
        dir.heroTree = treeFall;
        dir.cameraDirector = camDir;
        dir.storm = storm;
        dir.rescueChannel = channel;
        dir.autoStartOnLoad = true;
        dir.kateToppleWaypointIndex = -1;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Scenario4Builder] BUILD COMPLETE.");
    }

    /// <summary>
    /// Ensures all 5 characters import as Humanoid (for retargeting) and extracts the
    /// embedded textures for the restored nonPBR background characters (Ch01/02/33).
    /// Run this before Build All if any character FBX was (re)added.
    /// </summary>
    [MenuItem("Tools/Scenario4/Prepare Characters")]
    public static void PrepareCharacters()
    {
        AssetDatabase.Refresh();
        foreach (var c in new[] { "Ch01", "Ch02", "Ch16", "Ch21", "Ch33" })
        {
            var p = ModelDir + c + ".fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogError($"[Scenario4Builder] No importer for {p}"); continue; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        foreach (var c in new[] { "Ch01", "Ch02", "Ch33" })
        {
            var p = ModelDir + c + ".fbx";
            var folder = ModelDir + c + ".fbm";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Models", c + ".fbm");
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp != null) imp.ExtractTextures(folder);
        }
        AssetDatabase.Refresh();

        foreach (var c in new[] { "Ch01", "Ch02", "Ch33" })
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ModelDir + c + ".fbm" }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                if (ti != null && tp.Contains("_Normal") && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
        }
        Debug.Log("[Scenario4Builder] Characters prepared (Humanoid + textures).");
    }

    static GameObject MakeCharacter(string fbxPath, string name, Vector3 pos, RuntimeAnimatorController ac)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) prefab = AssetDatabase.LoadMainAssetAtPath(fbxPath) as GameObject;
        if (prefab == null)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        }
        if (prefab == null)
        {
            Debug.LogError($"[Scenario4Builder] Could not load model at {fbxPath}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) go = (GameObject)Object.Instantiate(prefab);
        go.name = name;
        go.transform.position = pos;

        Avatar avatar = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is Avatar av) avatar = av;

        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        if (avatar != null) animator.avatar = avatar;
        animator.runtimeAnimatorController = ac;
        animator.applyRootMotion = false;

        var cap = go.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 0.9f, 0f);
        cap.height = 1.8f;
        cap.radius = 0.3f;
        return go;
    }

    static Transform[] MakePath(string name, Vector3[] pts, Transform parent)
    {
        var arr = new Transform[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            var g = new GameObject($"{name}_wp{i}");
            g.transform.SetParent(parent);
            g.transform.position = pts[i];
            arr[i] = g.transform;
        }
        return arr;
    }

    static AudioClip GetOrCreateWindClip()
    {
        const string path = "Assets/Audio/Wind.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;

        Directory.CreateDirectory("Assets/Audio");
        int sr = 22050, seconds = 6, n = sr * seconds;
        var samples = new float[n];
        float last = 0f;
        var rnd = new System.Random(1234);
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            last = (last + 0.04f * white) / 1.04f;      // brown-ish noise (low rumble = wind)
            float gust = 0.6f + 0.4f * Mathf.Sin(i * 2f * Mathf.PI / n * 3f); // slow swell
            samples[i] = last * 4f * gust;
        }
        // Normalize and cross-fade the loop seam.
        float max = 0.0001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
        for (int i = 0; i < n; i++) samples[i] /= max;
        int fade = sr / 2;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            samples[i] *= k;
            samples[n - 1 - i] *= k;
        }
        WriteWav(path, samples, sr);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void WriteWav(string path, float[] samples, int sampleRate)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int byteCount = samples.Length * 2;
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + byteCount);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);            // PCM
            bw.Write((short)1);            // mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);      // byte rate
            bw.Write((short)2);            // block align
            bw.Write((short)16);           // bits per sample
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(byteCount);
            foreach (var s in samples)
                bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }

    static AudioClip GetOrCreateRainClip()
    {
        const string path = "Assets/Audio/Rain.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Audio");
        int sr = 22050, n = sr * 4;
        var samples = new float[n];
        var rnd = new System.Random(99);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float hp = white - prev * 0.5f; prev = white; // hissy high-passed noise = rain
            samples[i] = hp * 0.5f;
        }
        float max = 0.0001f; for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
        for (int i = 0; i < n; i++) samples[i] /= max;
        int fade = sr / 4; for (int i = 0; i < fade; i++) { float k = i / (float)fade; samples[i] *= k; samples[n - 1 - i] *= k; }
        WriteWav(path, samples, sr);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static AudioClip GetOrCreateThunderClip()
    {
        const string path = "Assets/Audio/Thunder.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Audio");
        int sr = 22050, n = sr * 3;
        var samples = new float[n];
        var rnd = new System.Random(7);
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            last = (last + 0.06f * white) / 1.06f;       // deep brown-noise rumble
            float t = i / (float)n;
            float env = Mathf.Exp(-3f * t) * (1f + 0.6f * Mathf.Exp(-40f * t)); // crack + long decay
            samples[i] = last * 6f * env;
        }
        float max = 0.0001f; for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
        for (int i = 0; i < n; i++) samples[i] = Mathf.Clamp(samples[i] / max, -1f, 1f);
        WriteWav(path, samples, sr);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static Texture2D LoadTex(string folder, string baseName)
    {
        foreach (var ext in new[] { ".png", ".PNG", ".jpg", ".tga" })
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + baseName + ext);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>
    /// Builds explicit URP/Lit materials from the extracted .fbm textures and assigns them
    /// to each renderer — guarantees the pedestrian shows its diffuse (no gray), independent
    /// of FBX embedded-material quirks. Requires "Prepare Pedestrians" to have run first.
    /// </summary>
    static void ApplyExtractedTextures(GameObject root, string charName)
    {
        if (root == null) return;
        string fbm = PedDir + charName + ".fbm";
        Texture2D bodyDiff = LoadTex(fbm, charName + "_1001_Diffuse");
        Texture2D bodyNorm = LoadTex(fbm, charName + "_1001_Normal");
        Texture2D hairDiff = LoadTex(fbm, charName + "_1002_Diffuse") ?? bodyDiff;
        Texture2D hairNorm = LoadTex(fbm, charName + "_1002_Normal") ?? bodyNorm;
        if (bodyDiff == null)
        {
            Debug.LogWarning($"[Scenario4Builder] No extracted textures in {fbm} for {charName} — run 'Tools/Scenario4/Prepare Pedestrians' first. ({charName} may render gray.)");
            return;
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = (r.name + (r.sharedMaterial != null ? r.sharedMaterial.name : "")).ToLower();
            bool isHair = n.Contains("hair");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", isHair ? hairDiff : bodyDiff);
            var norm = isHair ? hairNorm : bodyNorm;
            if (norm != null) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Smoothness", 0.1f);
            r.sharedMaterial = mat;
        }
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    static Transform FindDeepContains(Transform root, string lowerNeedle)
    {
        if (root.name.ToLower().Contains(lowerNeedle)) return root;
        foreach (Transform c in root) { var r = FindDeepContains(c, lowerNeedle); if (r != null) return r; }
        return null;
    }

    /// <summary>Creates a small dark phone cuboid parented to the witness's right hand bone, hidden by default.</summary>
    static GameObject MakePhoneProp(GameObject witness)
    {
        Transform hand = null;
        var anim = witness.GetComponent<Animator>();
        if (anim != null && anim.avatar != null && anim.isHuman)
            hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) hand = FindDeep(witness.transform, "mixamorig:RightHand");
        if (hand == null) hand = FindDeepContains(witness.transform, "righthand");
        if (hand == null) { Debug.LogWarning("[Scenario4Builder] Right-hand bone not found; no phone prop."); return null; }
        var phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        phone.name = "Phone";
        phone.transform.SetParent(hand, false);
        phone.transform.localPosition = Vector3.zero;
        phone.transform.localRotation = Quaternion.identity;
        // Compensate for the bone's world scale so the phone is ~0.07 x 0.15 x 0.012 m in world space.
        Vector3 ls = hand.lossyScale;
        phone.transform.localScale = new Vector3(
            0.07f / Mathf.Max(Mathf.Abs(ls.x), 1e-4f),
            0.15f / Mathf.Max(Mathf.Abs(ls.y), 1e-4f),
            0.012f / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
        Object.DestroyImmediate(phone.GetComponent<Collider>());
        var pmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pmat.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.05f));
        pmat.SetFloat("_Smoothness", 0.6f);
        phone.GetComponent<MeshRenderer>().sharedMaterial = pmat;
        phone.SetActive(false);
        return phone;
    }

    /// <summary>
    /// Extract the embedded textures for the Pedestrians-folder FBX (so they're not gray) and
    /// set the phone clip to play once. Run this BEFORE Build All when pedestrians need texturing.
    /// </summary>
    [MenuItem("Tools/Scenario4/Prepare Pedestrians")]
    public static void PreparePedestrians()
    {
        AssetDatabase.Refresh();
        foreach (var c in new[] { "Ch01", "Ch02", "Ch33" })
        {
            var p = PedDir + c + ".fbx";
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) { Debug.LogError($"[Scenario4Builder] No importer for {p}"); continue; }
            imp.animationType = ModelImporterAnimationType.Human;
            var folder = PedDir + c + ".fbm";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Models/Pedestrians", c + ".fbm");
            imp.ExtractTextures(folder);
        }
        AssetDatabase.Refresh();
        foreach (var c in new[] { "Ch01", "Ch02", "Ch33" })
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PedDir + c + ".fbm" }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                if (ti != null && tp.Contains("_Normal") && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
        }
        // Phone clip plays ONCE (no endless re-calling).
        var phoneImp = AssetImporter.GetAtPath("Assets/Animations/Mixamo/PhoneCall.fbx") as ModelImporter;
        if (phoneImp != null)
        {
            var clips = phoneImp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = phoneImp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = false;
            phoneImp.clipAnimations = clips;
            AssetDatabase.ImportAsset("Assets/Animations/Mixamo/PhoneCall.fbx", ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[Scenario4Builder] Pedestrians prepared (textures extracted) + PhoneCall set to play once.");
    }
}
