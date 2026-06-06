using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using EmergencySim;

/// <summary>
/// One-shot, re-runnable builder for the "Car Collision on Kate" cinematic (Scenario2, DAYTIME).
/// Mirrors Scenario4Builder but swaps the hazard (falling tree → driving car). Build from the
/// menu: Tools > Scenario2 > Build All. (Used instead of the MCP snippet compiler, which is
/// unavailable here due to a compiler command-line length limit.)
/// </summary>
public static class Scenario2Builder
{
    const string ModelDir = "Assets/Models/";
    const string PedDir = "Assets/Models/Pedestrians/";
    const string CtrlDir = "Assets/Animations/Controllers/";
    const string ChannelPath = "Assets/Scripts/Channels/RescueRequested.asset";

    // ---- Tunable layout (on the Environment's existing `Road`, which runs along Z) ----
    // Kate walks SW_R (x≈-9.5) north, steps off the curb (x≈-6), and crosses east (+x) into the
    // road. The car drives south (-z) down the lane at x≈-2.5; impact where her path meets it.
    static readonly Vector3 KateStart   = new Vector3(-9.5f, 0f, -4f);  // SW_R sidewalk start
    static readonly Vector3 KateWalk    = new Vector3(-9.5f, 0f, 3f);   // walks north along the sidewalk
    static readonly Vector3 KateCurb    = new Vector3(-6.3f, 0f, 4f);   // steps off the curb → launches the car
    static readonly Vector3 KateImpact  = new Vector3(-2.5f, 0f, 4f);   // car's lane (where she's hit)
    static readonly Vector3 KateCross   = new Vector3(0.8f, 0f, 4f);    // continues across (she won't reach it)
    static readonly Vector3 CarStart    = new Vector3(-2.5f, 0f, 28f);  // car spawns up the road (north)
    static readonly Vector3 CarEnd       = new Vector3(-2.5f, 0f, -26f);// drives south, off-frame
    static readonly Vector3 BrakeZonePos = new Vector3(-2.5f, 0.6f, 9f);// car slams brakes here (just N of the crossing)

    // Car mesh fix-ups (tuned after a screenshot).
    const float CarTargetLength = 4.4f;   // metres; scales the extracted mesh to match Kate
    const float CarYawOffset = 0f;        // deg; align the car's nose to root +z (travel dir)

    [MenuItem("Tools/Scenario2/Build All")]
    public static void BuildAll()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("Scenario2"))
        {
            Debug.LogError("[Scenario2Builder] Active scene is not Scenario2. Open Assets/Scenes/Scenario2.unity first.");
            return;
        }

        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        Debug.Log("[Scenario2Builder] Cleared scene, building (daytime car collision)...");

        var kateAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "KateAC.controller");
        var witnessAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "WitnessAC.controller");
        var bgAC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlDir + "BackgroundAC.controller");

        // ---------- Camera ----------
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        camGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 55f;
        cam.farClipPlane = 300f;

        // ---------- Lighting (all 4 types, DAYTIME) ----------
        // 1) Directional sun — bright, warm.
        var sunGo = new GameObject("Directional Light (Sun)");
        sunGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.86f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;

        // 2) Two spot lights justified as shop-sign / awning lights over storefronts.
        var signsParent = new GameObject("ShopLights").transform;
        AddSpot(signsParent, "ShopSignSpot_L", new Vector3(-8.5f, 4.2f, 8.5f), new Vector3(70f, 35f, 0f), new Color(1f, 0.9f, 0.7f), 9f, 12f, 46f);
        AddSpot(signsParent, "ShopSignSpot_R", new Vector3(7.5f, 4.2f, 8.5f), new Vector3(70f, -35f, 0f), new Color(0.85f, 0.92f, 1f), 9f, 12f, 46f);

        // 3) Point light — warm glow under a shop awning by the crossing.
        var pointGo = new GameObject("AwningPointLight");
        pointGo.transform.position = new Vector3(5.5f, 2.6f, 2.5f);
        var point = pointGo.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = new Color(1f, 0.85f, 0.65f);
        point.intensity = 6f;
        point.range = 9f;

        // 4) Ambient — bright daylight from a procedural sky.
        var skyMat = new Material(Shader.Find("Skybox/Procedural"));
        skyMat.SetFloat("_SunSize", 0.04f);
        skyMat.SetFloat("_AtmosphereThickness", 1.0f);
        skyMat.SetColor("_SkyTint", new Color(0.55f, 0.7f, 1f));
        skyMat.SetColor("_GroundColor", new Color(0.46f, 0.46f, 0.5f));
        skyMat.SetFloat("_Exposure", 1.25f);
        RenderSettings.skybox = skyMat;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.fog = false;
        DynamicGI.UpdateEnvironment();

        // ---------- Ground ----------
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.02f, 0f);
        ground.transform.localScale = new Vector3(8f, 1f, 8f);
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.52f));
        groundMat.SetFloat("_Smoothness", 0.1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // ---------- Environment (backdrop buildings) ----------
        var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Environment.fbx");
        var env = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
        env.name = "Environment";
        env.transform.position = Vector3.zero;
        foreach (Transform t in env.transform)
            if (t.name.StartsWith("Bldg")) t.gameObject.AddComponent<BoxCollider>();
        // Hide the environment's own crashed-car prop (base + cabin) so only our driveable copy shows.
        var envMfs = env.GetComponentsInChildren<MeshFilter>(true);
        Vector3 envCarC = Vector3.zero; bool foundEnvCar = false;
        foreach (var mfc in envMfs)
        {
            string mn = mfc.sharedMesh != null ? mfc.sharedMesh.name.ToLower() : "";
            if (mn.Contains("car") || mfc.name.ToLower().Contains("car"))
            {
                var rr = mfc.GetComponent<Renderer>();
                envCarC = rr != null ? rr.bounds.center : mfc.transform.position; foundEnvCar = true; break;
            }
        }
        if (foundEnvCar)
            foreach (var mfc in envMfs)
            {
                var rr = mfc.GetComponent<Renderer>();
                if (rr == null) continue;
                string nn = mfc.name.ToLower();
                if (nn.Contains("cone") || nn.Contains("cylinder")) continue;
                Vector3 c = rr.bounds.center;
                if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(envCarC.x, envCarC.z)) <= 1.9f)
                    mfc.gameObject.SetActive(false);
            }

        // No custom road geometry — the action uses the Environment's existing street:
        // `Road` runs along Z (centre x=0, ~14 wide x..-7..+7, 60 long), curbs at x≈±6.1,
        // sidewalks SW_R (x≈-10) and SW_L (x≈+10). Kate crosses it and the car drives down it.

        // ---------- The driveable car (extracted from Environment.fbx) ----------
        Transform[] wheels;
        var carGo = ExtractCar(envPrefab, out wheels);
        if (carGo == null) carGo = BuildSimpleCar(out wheels);
        carGo.name = "hitting_car";
        carGo.transform.position = CarStart;
        carGo.transform.rotation = Quaternion.LookRotation((CarEnd - CarStart).normalized, Vector3.up);

        var carRb = carGo.GetComponent<Rigidbody>();
        if (!carRb) carRb = carGo.AddComponent<Rigidbody>();
        carRb.isKinematic = true;
        carRb.useGravity = false;

        // The car root carries a uniform scale (mesh fitted to ~CarTargetLength). Child colliders
        // inherit it, so divide sizes/offsets by the scale to keep them in true world metres.
        float carS = Mathf.Max(0.001f, carGo.transform.localScale.x);
        float invS = 1f / carS;

        // Rough body collider (root local: car length along +z).
        var bodyCol = carGo.AddComponent<BoxCollider>();
        bodyCol.center = new Vector3(0f, 0.7f * invS, 0f);
        bodyCol.size = new Vector3(1.9f * invS, 1.4f * invS, CarTargetLength * invS);

        // Front impact trigger at the nose (+z local = travel direction).
        var noseGo = new GameObject("ImpactTrigger");
        noseGo.transform.SetParent(carGo.transform, false);
        noseGo.transform.localPosition = new Vector3(0f, 0.7f * invS, CarTargetLength * 0.5f * invS);
        var noseCol = noseGo.AddComponent<BoxCollider>();
        noseCol.size = new Vector3(2.0f * invS, 1.4f * invS, 1.2f * invS);
        noseCol.isTrigger = true;
        var impactTrigger = noseGo.AddComponent<CarImpactTrigger>();

        // Tyre-screech audio on the car.
        var screech = carGo.AddComponent<AudioSource>();
        screech.clip = GetOrCreateScreechClip();
        screech.loop = false; screech.playOnAwake = false; screech.volume = 0.8f; screech.spatialBlend = 0f;

        // Debris Rigidbodies (graded physics) parked at the car's front bumper.
        var debris = MakeDebrisPieces(carGo.transform, new Vector3(0f, 0.5f * invS, CarTargetLength * 0.45f * invS), invS, new Color(0.62f, 0.13f, 0.12f));

        var carCtrl = carGo.AddComponent<CarController>();
        carCtrl.body = carRb;
        carCtrl.cruiseSpeed = 9f;
        carCtrl.wheels = wheels;
        carCtrl.screech = screech;
        carCtrl.debris = debris;
        carCtrl.debrisLocalImpulse = new Vector3(0f, 4f, 2.5f);  // up + slightly forward → scatters around Kate
        carCtrl.debrisJitter = 3.2f;
        impactTrigger.controller = carCtrl;
        carCtrl.waypoints = MakePath("Car", new[] { CarStart, CarEnd }, new GameObject("Paths_Car").transform);

        // Brake zone across the lane before the crossing.
        var brakeGo = new GameObject("BrakeZone");
        brakeGo.transform.position = BrakeZonePos;
        var brakeCol = brakeGo.AddComponent<BoxCollider>();
        brakeCol.size = new Vector3(2.6f, 2f, 3f);   // spans the lane (x), thin across the road (z)
        brakeCol.isTrigger = true;
        var brakeTrig = brakeGo.AddComponent<BrakeZoneTrigger>();
        brakeTrig.car = carCtrl;

        // ---------- Characters ----------
        // Kate (Ch21) — the victim, crossing the road.
        var kate = MakeCharacter(ModelDir + "Ch21.fbx", "Kate", KateStart, kateAC);
        var kateCap = kate.GetComponent<CapsuleCollider>();
        kateCap.isTrigger = true;   // detected by the car's front trigger; not physically shoved
        var kateAnim = kate.GetComponent<Animator>();
        var kateFollower = kate.AddComponent<WaypointFollower>();
        kateFollower.animator = kateAnim;
        kateFollower.speed = 1.4f;
        var kateVictim = kate.AddComponent<KateVictim>();
        kateVictim.animator = kateAnim;
        kateVictim.follower = kateFollower;
        kateVictim.groundSlideZ = 0.6f;   // knocked slightly forward into the road

        // Witness (textured pedestrian Ch01) — nearest the crossing, on the SW_R curb just north.
        var witnessPos = new Vector3(-6.6f, 0f, 8f);
        var witness = MakeCharacter(PedDir + "Ch01.fbx", "Witness", witnessPos, witnessAC);
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

        // Background pedestrians — walk the sidewalks (along Z), keep going.
        var bg1 = MakeCharacter(PedDir + "Ch02.fbx", "Background1", new Vector3(10f, 0f, -12f), bgAC);
        ApplyExtractedTextures(bg1, "Ch02");
        var bg1Follower = bg1.AddComponent<WaypointFollower>();
        bg1Follower.animator = bg1.GetComponent<Animator>();
        bg1Follower.speed = 1.4f;

        var bg2 = MakeCharacter(PedDir + "Ch33.fbx", "Background2", new Vector3(-12f, 0f, 16f), bgAC);
        ApplyExtractedTextures(bg2, "Ch33");
        var bg2Follower = bg2.AddComponent<WaypointFollower>();
        bg2Follower.animator = bg2.GetComponent<Animator>();
        bg2Follower.speed = 1.4f;

        // ---------- Paths ----------
        var paths = new GameObject("Paths").transform;
        kateFollower.waypoints = MakePath("Kate", new[] { KateStart, KateWalk, KateCurb, KateImpact, KateCross }, paths);
        witnessFollower.waypoints = MakePath("Witness", new[] { witnessPos }, paths); // stays put, watching
        bg1Follower.waypoints = MakePath("BG1", new[] { new Vector3(10f, 0f, -12f), new Vector3(10f, 0f, 28f) }, paths);
        bg2Follower.waypoints = MakePath("BG2", new[] { new Vector3(-12f, 0f, 16f), new Vector3(-12f, 0f, -18f) }, paths);

        kate.transform.rotation = Quaternion.LookRotation((KateWalk - KateStart).normalized);
        witness.transform.rotation = Quaternion.LookRotation((KateImpact - witnessPos).normalized);
        bg1.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        bg2.transform.rotation = Quaternion.LookRotation(Vector3.back);

        // ---------- Static clinical blood decal under Kate (activated when she's down) ----------
        kateVictim.bloodDecal = MakeBloodDecal(KateImpact);

        // ---------- Debris particle burst (alongside the Rigidbody pieces) ----------
        var debrisGo = new GameObject("DebrisBurst");
        debrisGo.transform.position = KateImpact + Vector3.up * 0.8f;
        var ps = debrisGo.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = 1.1f; main.startSpeed = 4f; main.startSize = 0.12f;
        main.gravityModifier = 1.4f; main.maxParticles = 120;
        main.startColor = new Color(0.6f, 0.6f, 0.62f);
        main.playOnAwake = false;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 42f; shape.radius = 0.3f;
        var psr = debrisGo.GetComponent<ParticleSystemRenderer>();
        var debrisMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        debrisMat.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.67f));
        psr.sharedMaterial = debrisMat;
        kateVictim.debrisBurst = ps;

        // ---------- Camera shots (road runs along Z; action around (-2.5, 0, 4)) ----------
        var shotsParent = new GameObject("CameraShots").transform;
        var sWide = MakeShot("Shot0_Wide", shotsParent, new Vector3(7f, 5f, -9f), new Vector3(-2.5f, 1f, 8f));
        var sImpact = MakeShot("Shot1_Impact", shotsParent, new Vector3(5.5f, 1.7f, 3.5f), new Vector3(-2.5f, 0.9f, 6f));
        var sWitness = MakeShot("Shot2_Witness", shotsParent, new Vector3(-3f, 1.7f, 10.5f), new Vector3(-6.6f, 1.4f, 8f));
        var sTwo = MakeShot("Shot3_TwoShot", shotsParent, new Vector3(3.5f, 3f, 10f), new Vector3(-4.5f, 0.5f, 6f));

        var camDir = camGo.AddComponent<CameraDirector>();
        camDir.cam = cam;
        camDir.shots = new[] { sWide, sImpact, sWitness, sTwo };
        camGo.transform.SetPositionAndRotation(sWide.position, sWide.rotation);

        // ---------- Rescue channel + stub (shared with the rescue sequence) ----------
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
        var dir = dirGo.AddComponent<Scenario2Director>();
        dir.kate = kate.transform;
        dir.kateFollower = kateFollower;
        dir.backgroundFollowers = new[] { bg1Follower, bg2Follower };
        dir.kateVictim = kateVictim;
        dir.witness = witnessCtrl;
        dir.car = carCtrl;
        dir.cameraDirector = camDir;
        dir.rescueChannel = channel;
        dir.autoStartOnLoad = true;
        dir.carLaunchWaypointIndex = 2;   // launch the car when Kate steps off the curb (waypoint 2)

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Scenario2Builder] BUILD COMPLETE.");
    }

    [MenuItem("Tools/Scenario2/Diagnose Car Nodes")]
    public static void DiagnoseCarNodes()
    {
        var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Environment.fbx");
        var src = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
        var mfs = src.GetComponentsInChildren<MeshFilter>(true);
        Debug.Log($"[Scenario2Builder] Car dump START — total MeshFilters in env: {mfs.Length}");
        int hits = 0;
        foreach (var mfc in mfs)
        {
            string meshName = mfc.sharedMesh != null ? mfc.sharedMesh.name : "(null mesh)";
            string ml = meshName.ToLower();
            string nl = mfc.name.ToLower();
            if (!ml.Contains("car") && !ml.Contains("tire") && !ml.Contains("wheel") &&
                !nl.Contains("car") && !nl.Contains("tire") && !nl.Contains("wheel")) continue;
            var r = mfc.GetComponent<Renderer>();
            string bnds = r != null ? r.bounds.ToString("F1") : "(no renderer)";
            Debug.Log($"[CarNode] node='{mfc.name}' mesh='{meshName}' path={GetPath(mfc.transform)} bounds={bnds}");
            hits++;
        }
        Debug.Log($"[Scenario2Builder] Car dump END — {hits} car-ish nodes.");
        // Also list every mesh sitting near the crashed-car footprint (-2.5, 0.5, -12).
        Vector3 carLoc = new Vector3(-2.5f, 0.5f, -12f);
        int near = 0;
        foreach (var mfc in mfs)
        {
            var r = mfc.GetComponent<Renderer>();
            if (r == null) continue;
            Vector3 c = r.bounds.center;
            if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(carLoc.x, carLoc.z)) > 3.5f) continue;
            string meshName = mfc.sharedMesh != null ? mfc.sharedMesh.name : "(null)";
            Debug.Log($"[NearCar] node='{mfc.name}' mesh='{meshName}' size={r.bounds.size:F2} center={c:F1}");
            near++;
        }
        Debug.Log($"[Scenario2Builder] Near-car meshes: {near}");
        Object.DestroyImmediate(src);
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    [MenuItem("Tools/Scenario2/Diagnose Streets")]
    public static void DiagnoseStreets()
    {
        var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Environment.fbx");
        var src = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
        Debug.Log("[Scenario2Builder] Street dump START (large flat low meshes = roads/sidewalks):");
        int n = 0;
        foreach (var mfc in src.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mfc.GetComponent<Renderer>();
            if (r == null) continue;
            Vector3 s = r.bounds.size, c = r.bounds.center;
            if (c.y > 1.5f) continue;
            if (s.y > 1.2f) continue;
            if (s.x < 5f && s.z < 5f) continue;
            string mn = mfc.sharedMesh != null ? mfc.sharedMesh.name : "?";
            Debug.Log($"[Street] node='{mfc.name}' mesh='{mn}' center={c:F1} size={s:F1}");
            if (++n > 40) break;
        }
        Debug.Log($"[Scenario2Builder] Street dump END — {n} flat pieces.");
        Object.DestroyImmediate(src);
    }

    static void AddSpot(Transform parent, string name, Vector3 pos, Vector3 euler, Color col, float intensity, float range, float angle)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(euler);
        var l = g.AddComponent<Light>();
        l.type = LightType.Spot; l.color = col; l.intensity = intensity; l.range = range; l.spotAngle = angle;
        l.shadows = LightShadows.Soft;
    }

    static Transform MakeShot(string name, Transform parent, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        t.position = pos;
        t.LookAt(lookAt);
        return t;
    }

    // ---------- Car extraction ----------
    static GameObject ExtractCar(GameObject envPrefab, out Transform[] wheels)
    {
        wheels = new Transform[0];
        if (envPrefab == null) return null;
        // Plain instantiate (NOT a connected prefab instance) so we can reparent the car mesh out.
        var src = (GameObject)Object.Instantiate(envPrefab);
        src.transform.position = Vector3.zero;

        // The env car is multi-mesh: a base (CrashedCar_Body) + a cabin (a generically-named Cube)
        // stacked on top, with traffic cones nearby. Anchor on the named car mesh, then grab every
        // mesh whose footprint sits within the car's body radius (captures base+cabin, skips cones).
        var mfsAll = src.GetComponentsInChildren<MeshFilter>(true);
        Transform anchor = null; Vector3 anchorC = Vector3.zero;
        foreach (var mfc in mfsAll)
        {
            string mn = mfc.sharedMesh != null ? mfc.sharedMesh.name.ToLower() : "";
            if (mn.Contains("car") || mfc.name.ToLower().Contains("car"))
            {
                var rr = mfc.GetComponent<Renderer>();
                anchor = mfc.transform; anchorC = rr != null ? rr.bounds.center : mfc.transform.position; break;
            }
        }
        if (anchor == null) { Object.DestroyImmediate(src); return null; }

        const float carBodyRadius = 1.9f;   // base+cabin are ~0.2m apart; nearest cone is 2.0m away
        var parts = new List<Transform>();
        var tyres = new List<Transform>();
        foreach (var mfc in mfsAll)
        {
            var rr = mfc.GetComponent<Renderer>();
            if (rr == null) continue;
            Vector3 c = rr.bounds.center;
            if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(anchorC.x, anchorC.z)) > carBodyRadius) continue;
            // Skip the small traffic-cone bits explicitly by name.
            string nn = mfc.name.ToLower();
            if (nn.Contains("cone") || nn.Contains("cylinder")) continue;
            parts.Add(mfc.transform);
        }
        if (parts.Count == 0) { Object.DestroyImmediate(src); return null; }

        // Combined world bounds of the car parts.
        Bounds b = new Bounds(parts[0].position, Vector3.zero);
        bool init = false;
        foreach (var p in parts)
        {
            var r = p.GetComponent<Renderer>();
            if (r == null) continue;
            if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
        }

        // The env car was parked at an angle; cancel its yaw (pivoting about the car centre) so the
        // rig drives straight down the road, not sliding sideways. CarYawOffset trims the result.
        float parkedYaw = anchor.eulerAngles.y;
        Debug.Log($"[Scenario2Builder] Parked car yaw = {parkedYaw:F1} deg.");

        var root = new GameObject("hitting_car");
        root.transform.position = b.center;          // pivot at the car centre
        root.transform.rotation = Quaternion.identity;
        var mesh = new GameObject("CarMesh").transform;
        mesh.SetParent(root.transform, false);        // world at b.center, identity

        foreach (var p in parts) p.SetParent(mesh, true);   // keep world; local offsets around the centre
        mesh.localRotation = Quaternion.Euler(0f, -parkedYaw + CarYawOffset, 0f); // rotate about the centre
        Object.DestroyImmediate(src);

        // Scale (about the root pivot = car centre) so the longest horizontal dimension is a real car length.
        Bounds ab = new Bounds(root.transform.position, Vector3.zero); bool ainit = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        { if (!ainit) { ab = r.bounds; ainit = true; } else ab.Encapsulate(r.bounds); }
        float longest = Mathf.Max(ab.size.x, ab.size.z);
        if (longest < 0.001f) longest = 1f;
        float scale = CarTargetLength / longest;
        root.transform.localScale = new Vector3(scale, scale, scale);

        // Recompute bounds after scaling to seat the wheels on y=0.
        Bounds wb = new Bounds(root.transform.position, Vector3.zero);
        bool winit = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!winit) { wb = r.bounds; winit = true; } else wb.Encapsulate(r.bounds);
        }
        float bottom = wb.min.y;
        root.transform.position += new Vector3(0f, -bottom, 0f);

        wheels = tyres.ToArray();
        Debug.Log($"[Scenario2Builder] Extracted car: {parts.Count} parts, {tyres.Count} wheels, scale {scale:F2}.");
        return root;
    }

    static GameObject BuildSimpleCar(out Transform[] wheels)
    {
        Debug.LogWarning("[Scenario2Builder] No car found in Environment.fbx — building a simple primitive car.");
        var root = new GameObject("hitting_car");
        var mesh = new GameObject("CarMesh").transform; mesh.SetParent(root.transform, false);
        var paint = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        paint.SetColor("_BaseColor", new Color(0.7f, 0.12f, 0.12f)); paint.SetFloat("_Metallic", 0.6f); paint.SetFloat("_Smoothness", 0.7f);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "Car_Body"; body.transform.SetParent(mesh);
        body.transform.localScale = new Vector3(1.8f, 0.8f, 4.2f); body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<MeshRenderer>().sharedMaterial = paint;
        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube); cabin.name = "CarRoof"; cabin.transform.SetParent(mesh);
        cabin.transform.localScale = new Vector3(1.6f, 0.7f, 2f); cabin.transform.localPosition = new Vector3(0f, 1.3f, -0.2f);
        Object.DestroyImmediate(cabin.GetComponent<Collider>()); cabin.GetComponent<MeshRenderer>().sharedMaterial = paint;
        var tyreMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); tyreMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
        var ws = new List<Transform>();
        float[] xs = { -0.9f, 0.9f }; float[] zs = { 1.4f, -1.4f };
        int wi = 0;
        foreach (var z in zs) foreach (var x in xs)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder); w.name = "CarTire_" + (++wi); w.transform.SetParent(mesh);
            w.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f); w.transform.localPosition = new Vector3(x, 0.35f, z);
            w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Object.DestroyImmediate(w.GetComponent<Collider>()); w.GetComponent<MeshRenderer>().sharedMaterial = tyreMat;
            ws.Add(w.transform);
        }
        wheels = ws.ToArray();
        return root;
    }

    // Debris pieces shaped like car parts (still primitives): 2 thin slabs (panel + glass),
    // 1 flat cylinder (hubcap), 3 irregular chunks. Physics is unchanged — kinematic until the
    // CarController releases them with an impulse. Sizes are in real metres (× invScale to undo
    // the car root's scale). Most match the body colour; one is glass-grey, one metal.
    static Rigidbody[] MakeDebrisPieces(Transform parent, Vector3 localPos, float invScale, Color bodyColor)
    {
        var bodyMat = LitColor(bodyColor, 0.6f, 0.5f);
        var glassMat = LitColor(new Color(0.62f, 0.68f, 0.72f), 0.1f, 0.85f);
        var metalMat = LitColor(new Color(0.6f, 0.6f, 0.63f), 0.8f, 0.6f);

        // (primitive, real-world size, material). Cylinder = flat hubcap.
        var specs = new (PrimitiveType prim, Vector3 size, Material mat)[]
        {
            (PrimitiveType.Cube,     new Vector3(0.52f, 0.04f, 0.34f), bodyMat),   // body panel slab
            (PrimitiveType.Cube,     new Vector3(0.40f, 0.03f, 0.50f), glassMat),  // glass shard slab
            (PrimitiveType.Cylinder, new Vector3(0.32f, 0.05f, 0.32f), metalMat),  // hubcap (flat disc)
            (PrimitiveType.Cube,     new Vector3(0.20f, 0.16f, 0.24f), bodyMat),   // chunk
            (PrimitiveType.Cube,     new Vector3(0.27f, 0.10f, 0.15f), bodyMat),   // chunk
            (PrimitiveType.Cube,     new Vector3(0.15f, 0.20f, 0.13f), bodyMat),   // chunk
        };

        var pieces = new List<Rigidbody>();
        for (int i = 0; i < specs.Length; i++)
        {
            var sp = specs[i];
            var p = GameObject.CreatePrimitive(sp.prim);
            p.name = "Debris_" + i;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = localPos + new Vector3((i - specs.Length * 0.5f) * 0.18f * invScale, i * 0.06f * invScale, 0f);
            // Cylinder primitive is 2 units tall, so halve its Y to hit the requested thickness.
            Vector3 s = sp.size; if (sp.prim == PrimitiveType.Cylinder) s.y *= 0.5f;
            p.transform.localScale = s * invScale;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.AddComponent<BoxCollider>();   // dynamic-safe collider for every shape
            p.GetComponent<MeshRenderer>().sharedMaterial = sp.mat;
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = 0.4f; rb.isKinematic = true;
            pieces.Add(rb);
        }
        return pieces.ToArray();
    }

    static Material LitColor(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    // Static, clinical blood: a main flat dark red-brown disc + two smaller irregular ones, flush
    // on the asphalt (+0.01 Y). Built hidden; KateVictim moves it under her and shows it when down.
    static GameObject MakeBloodDecal(Vector3 center)
    {
        var root = new GameObject("BloodPool");
        root.transform.position = new Vector3(center.x, 0.01f, center.z);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.22f, 0.045f, 0.035f)); // dark, desaturated red-brown
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.22f);
        AddBloodDisc(root.transform, mat, new Vector3(0f, 0f, 0f),       new Vector3(1.5f, 0.012f, 1.28f));
        AddBloodDisc(root.transform, mat, new Vector3(0.72f, 0f, -0.5f), new Vector3(0.56f, 0.012f, 0.64f));
        AddBloodDisc(root.transform, mat, new Vector3(-0.58f, 0f, 0.5f), new Vector3(0.42f, 0.012f, 0.36f));
        root.SetActive(false);
        return root;
    }

    static void AddBloodDisc(Transform parent, Material mat, Vector3 localPos, Vector3 scale)
    {
        var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // flat cylinder = disc
        d.name = "Blood";
        d.transform.SetParent(parent, false);
        d.transform.localPosition = localPos;
        d.transform.localScale = scale;
        Object.DestroyImmediate(d.GetComponent<Collider>());
        d.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static AudioClip GetOrCreateScreechClip()
    {
        const string path = "Assets/Audio/Screech.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;
        System.IO.Directory.CreateDirectory("Assets/Audio");
        int sr = 22050, n = (int)(sr * 1.4f);
        var samples = new float[n];
        var rnd = new System.Random(42);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            // Two squealing tones + noise, fading out — reads as a tyre screech.
            float f1 = 1100f + 220f * Mathf.Sin(t * 18f);
            float f2 = 1650f + 140f * Mathf.Sin(t * 11f);
            float tone = 0.5f * Mathf.Sin(2f * Mathf.PI * f1 * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f2 * t);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.25f;
            float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-2.2f * t);
            samples[i] = (tone + noise) * env * 0.8f;
        }
        WriteWav(path, samples, sr);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static void WriteWav(string path, float[] samples, int sampleRate)
    {
        using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
        using (var bw = new System.IO.BinaryWriter(fs))
        {
            int byteCount = samples.Length * 2;
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + byteCount);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1); bw.Write((short)1);
            bw.Write(sampleRate); bw.Write(sampleRate * 2);
            bw.Write((short)2); bw.Write((short)16);
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(byteCount);
            foreach (var s in samples) bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }

    // ---------- Character helpers (copied from Scenario4Builder) ----------
    static GameObject MakeCharacter(string fbxPath, string name, Vector3 pos, RuntimeAnimatorController ac)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) prefab = AssetDatabase.LoadMainAssetAtPath(fbxPath) as GameObject;
        if (prefab == null)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        }
        if (prefab == null) { Debug.LogError($"[Scenario2Builder] Could not load model at {fbxPath}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) go = (GameObject)Object.Instantiate(prefab);
        go.name = name;
        go.transform.position = pos;

        Avatar avatar = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath)) if (a is Avatar av) avatar = av;
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

    static Texture2D LoadTex(string folder, string baseName)
    {
        foreach (var ext in new[] { ".png", ".PNG", ".jpg", ".tga" })
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + baseName + ext);
            if (t != null) return t;
        }
        return null;
    }

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
            Debug.LogWarning($"[Scenario2Builder] No extracted textures in {fbm} for {charName} — run 'Tools/Scenario4/Prepare Pedestrians' first.");
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

    static GameObject MakePhoneProp(GameObject witness)
    {
        var anim = witness.GetComponent<Animator>();
        Transform hand = (anim != null && anim.avatar != null && anim.isHuman)
            ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) hand = FindDeep(witness.transform, "mixamorig:RightHand");
        if (hand == null) hand = FindDeepContains(witness.transform, "righthand");
        if (hand == null) { Debug.LogWarning("[Scenario2Builder] Right-hand bone not found; no phone prop."); return null; }

        Vector3 fingerDir = witness.transform.up;
        if (hand.childCount > 0)
        {
            Vector3 avg = Vector3.zero; int n = 0;
            foreach (Transform c in hand) { avg += c.position; n++; }
            if (n > 0) { avg /= n; var d = avg - hand.position; if (d.sqrMagnitude > 1e-6f) fingerDir = d.normalized; }
        }

        var phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        phone.name = "Phone";
        phone.transform.SetParent(hand, false);
        phone.transform.position = hand.position + fingerDir * 0.075f;
        phone.transform.rotation = Quaternion.LookRotation(fingerDir, witness.transform.forward);
        Vector3 ls = phone.transform.lossyScale;
        Vector3 want = new Vector3(0.085f, 0.16f, 0.014f);
        phone.transform.localScale = new Vector3(
            phone.transform.localScale.x * want.x / Mathf.Max(Mathf.Abs(ls.x), 1e-4f),
            phone.transform.localScale.y * want.y / Mathf.Max(Mathf.Abs(ls.y), 1e-4f),
            phone.transform.localScale.z * want.z / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
        Object.DestroyImmediate(phone.GetComponent<Collider>());
        var pmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pmat.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.04f));
        pmat.SetFloat("_Smoothness", 0.6f);
        pmat.EnableKeyword("_EMISSION");
        pmat.SetColor("_EmissionColor", new Color(0.4f, 0.6f, 1f) * 1.2f);
        phone.GetComponent<MeshRenderer>().sharedMaterial = pmat;
        phone.SetActive(false);
        return phone;
    }
}
