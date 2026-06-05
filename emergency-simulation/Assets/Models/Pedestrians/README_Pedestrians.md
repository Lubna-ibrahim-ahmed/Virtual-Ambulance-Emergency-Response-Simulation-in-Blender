# Background Pedestrians — How to use

Three ready-to-use background pedestrian characters. **No Blender work needed** —
pull the repo and they're textured, rigged (Humanoid), and carry Walk + Idle clips.

Files: `Ch01.fbx`, `Ch02.fbx`, `Ch33.fbx`

Each is already configured:
- **Rig:** Humanoid (avatar auto-created from the model)
- **Textures:** embedded (Diffuse/Normal/Specular/Glossiness) — not gray
- **Clips:** `Walk` and `Idle`, both set to loop

## Add one to a scene (≈1 minute)

1. **Drag** `Ch01` (or `Ch02` / `Ch33`) from `Assets/Models/Pedestrians/` into the scene.
2. On the new object's **Animator** component:
   - Set **Controller** → `Assets/Animations/Controllers/BackgroundAC.controller`
   - Leave **Apply Root Motion** = **off** (movement comes from the script, not the clip).
   - (Avatar is already filled in from the model.)
3. **Add Component → Waypoint Follower** (`EmergencySim.WaypointFollower`), then set:
   - **Animator** → drag in the same object's Animator component
   - **Waypoints** → create a few empty GameObjects where the pedestrian should walk,
     and assign them (in order) to the `Waypoints` array
   - **Speed** (default 1.4), **Loop** (tick if it should patrol the waypoints forever)

That's it. The pedestrian idles until it's told to walk, then follows the waypoints,
turning to face travel direction, with the leg cycle driven by the `IsWalking` bool.

## Starting them walking

`WaypointFollower` begins **idle** and starts moving when its `Begin()` method is called
(it sets `IsWalking = true`). Options:
- Let your scenario/director script call `Begin()` on the followers, **or**
- For a quick test, call `Begin()` from a tiny script's `Start()`, **or**
- Tick **Loop** and call `Begin()` once for continuous patrol.

(`Halt()` stops them and returns to Idle.)

## Notes
- BackgroundAC plays its own `Walk`/`Idle` clips (`Assets/Animations/Mixamo/`) and
  **retargets** them onto each pedestrian via the Humanoid avatar — so all three share
  one controller. The `Walk`/`Idle` clips embedded in each FBX are extras and aren't
  required for this workflow.
- These are Mixamo "nonPBR" characters (Specular/Glossiness). The **diffuse** texture
  shows correctly in URP/Standard. If a character looks flat, that's the spec/gloss
  workflow — assign a URP Lit material using the embedded Diffuse if you want full PBR.
