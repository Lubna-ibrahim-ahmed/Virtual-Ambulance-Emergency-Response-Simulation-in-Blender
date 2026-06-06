using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace EmergencySim
{
    /// <summary>
    /// Reusable ambulance rescue cinematic. Subscribes to the RescueRequested channel; when it
    /// fires (carrying the patient's world position) the whole sequence plays exactly once:
    /// the ambulance drives in front-first (flashing lights + siren) and parks with its REAR
    /// toward the patient, Chad gets out and walks AROUND the vehicle to the patient and performs
    /// CPR, fetches the stretcher, lays the patient flat ON it, then ROLLS it (pushing from behind)
    /// to the rear doors, loads, closes the doors, boards, and the ambulance drives away.
    /// RescueCompleted is raised once so scenarios can show endings.
    ///
    /// Drop the RescueSequence prefab into ANY scenario scene — zero edits to scenario scripts.
    /// Everything is derived from the patient position in the event plus a few serialized offsets
    /// and the ambulance's own bounds, so nothing is hard-coded per scene. A dedicated set of
    /// runtime camera cuts (arrival / CPR / load / departure) makes the rescue watchable without
    /// touching the scene's own shot markers.
    /// </summary>
    public class RescueSequenceController : MonoBehaviour
    {
        [Header("Channels")]
        public Vector3GameEvent rescueChannel;          // RescueRequested (in)
        public Vector3GameEvent rescueCompletedChannel; // RescueCompleted (out)

        [Header("Rescue camera (its own shots, never the scene's)")]
        public Camera rescueCamera;                     // usually the scene Main Camera; null = no cuts

        [Header("Interactive prompts (keypress gates)")]
        public GameObject promptCanvas;                 // overlay shown while waiting for a key
        public Text promptText;                         // the centered prompt label

        [Header("Ambulance")]
        public Transform ambulance;
        public WaypointFollower ambulanceFollower;
        public AmbulanceLightBar lightBar;
        public SirenTone siren;
        public Transform rearDoorPoint;                 // load point at the REAR (child of ambulance)
        public Transform[] rearDoors;                   // door panels authored OPEN; closed = flush

        [Header("Chad (paramedic)")]
        public Transform chad;
        public WaypointFollower chadFollower;
        public ProceduralMedic chadMedic;
        public Animator chadAnimator;
        public bool useProceduralCrouch = true;
        [Tooltip("How far behind the stretcher Chad stands while pushing it.")]
        public float pushHandleBack = 1.25f;

        [Header("Stretcher")]
        public Transform stretcher;
        public WaypointFollower stretcherFollower;
        public Transform patientAnchor;                 // on the mattress; patient lies here
        public Vector3 patientLocalEuler = new Vector3(90f, 0f, 0f);
        [Tooltip("Local offset on the deck to CENTER the patient (her pivot is at the feet, so shift her along the bed).")]
        public Vector3 patientLocalOffset = new Vector3(0f, 0f, 0.85f);
        public float liftDuration = 0.8f;

        [Header("Layout (relative to patient position from the event)")]
        public Vector3 roadAxis = new Vector3(0f, 0f, 1f);
        [Tooltip("Park this far PAST the patient along the road so the rear faces the patient.")]
        public float stopAhead = 5f;
        [Tooltip("Lane offset onto the road, away from the sidewalk where bystanders gather.")]
        public float laneOffset = 4.5f;
        public float approachRunIn = 18f;
        public float departRunOut = 26f;
        public float chadSideOffset = 1.1f;
        public float groundLevel = 0f;
        [Tooltip("Gap from the patient to the stretcher when it parks BESIDE her (never across her body).")]
        public float stretcherSideGap = 0.95f;
        [Tooltip("Departure: distance driven straight down the road before turning the corner.")]
        public float departForward = 16f;
        [Tooltip("Departure: distance driven around the corner (off the road) before vanishing out of sight.")]
        public float departTurnSide = 16f;

        [Header("Timing / feel")]
        public float driveSpeed = 7f;
        public float chadWalkSpeed = 1.7f;
        public float stretcherSpeed = 1.4f;
        public float cprSeconds = 3.5f;
        public float doorCloseTime = 1.0f;
        public float patientPickupRadius = 4f;
        public float bystanderWatchRadius = 14f;

        [Header("Camera framing")]
        public float camWideSide = 9f;
        public float camWideUp = 5f;
        public float camMedSide = 3f;
        public float camMedUp = 1.6f;
        public float camMedFwd = 1.8f;
        [Header("Two-shot (Chad + patient)")]
        [Tooltip("Angle off the perpendicular of the Chad↔patient line, so neither blocks the other.")]
        public float twoShotAngle = 38f;
        public float twoShotDist = 4.3f;
        public float twoShotHeight = 2.0f;
        public float twoShotLookUp = 0.55f;
        public float twoShotBlend = 1.6f;
        [Header("Pathing / patient body")]
        [Tooltip("Chad/stretcher keep at least this clearance from the patient's body when routing.")]
        public float pathClearance = 1.0f;
        [Tooltip("Half-length of her ~2 m lying body (for the routing obstacle).")]
        public float bodyHalfLength = 1.0f;
        [Tooltip("Half-width of her ~0.6 m body (for the routing obstacle).")]
        public float bodyRadius = 0.3f;
        [Tooltip("How far to the SIDE of her body axis Chad kneels for CPR (beside her chest).")]
        public float cprKneelOffset = 0.55f;
        public float patientClearance = 1.6f;   // (legacy — unused by the new body-aware routing)

        [System.NonSerialized] public string CurrentBeat = "idle";   // verification hook (drive/cpr/rollup/lift/load/depart/done)
        private bool _started;
        private bool _completed;
        private bool _watching;
        private readonly List<Transform> _temp = new List<Transform>();
        private readonly List<Transform> _watchers = new List<Transform>();
        private Transform _patient;
        private System.Func<Vector3> _focus;
        // Patient body frame (computed from her real transform/bones), road = side toward the ambulance.
        private Vector3 _bodyCenter, _chestGround;
        private Vector3 _bodyAxis = Vector3.forward, _bodyRoadSide = Vector3.right;

        private void Awake()
        {
            if (ambulance) ambulance.gameObject.SetActive(false);
            if (chad) chad.gameObject.SetActive(false);
            if (stretcher) stretcher.gameObject.SetActive(false);
            if (promptCanvas) promptCanvas.SetActive(false);
        }

        // --- Interactive keypress gates (New Input System) ---
        private IEnumerator WaitForKey(string message, Key key)
        {
            ShowPrompt(message);
            yield return null;                                  // let the prompt render a frame
            while (Keyboard.current == null || !Keyboard.current[key].wasPressedThisFrame)
                yield return null;                              // wait indefinitely for the key
            HidePrompt();
        }

        private void ShowPrompt(string message)
        {
            if (promptText) promptText.text = message;
            if (promptCanvas) promptCanvas.SetActive(true);
        }

        private void HidePrompt()
        {
            if (promptCanvas) promptCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            if (rescueChannel != null) rescueChannel.OnRaised += StartRescue;
        }

        private void OnDisable()
        {
            if (rescueChannel != null) rescueChannel.OnRaised -= StartRescue;
        }

        public void StartRescue(Vector3 patientPosition)
        {
            if (_started) return;       // once-only — re-raises are ignored
            _started = true;
            StartCoroutine(Run(patientPosition));
        }

        private IEnumerator Run(Vector3 patientPos)
        {
            Vector3 fwd = roadAxis.sqrMagnitude > 0.0001f ? roadAxis.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            float groundY = groundLevel;
            Vector3 patientFlat = new Vector3(patientPos.x, groundY, patientPos.z);

            // Park PAST the patient (rear toward patient) and out on the road lane (clear of bystanders).
            Vector3 stopPos  = patientFlat + fwd * stopAhead + side * laneOffset;
            Vector3 startPos = stopPos - fwd * approachRunIn;
            Vector3 exitPos  = stopPos + fwd * departRunOut;
            Quaternion driveRot = Quaternion.LookRotation(fwd, Vector3.up);

            Vector3 toP = patientFlat - stopPos; toP.y = 0f;
            Vector3 sideToPatient = toP.sqrMagnitude > 0.001f ? toP.normalized : -side;

            // Acquire the patient up front and read her REAL body frame (chest, axis, road side) so
            // every Chad/stretcher position and path derives from her transform, not a fixed point.
            _patient = AcquirePatient(patientPos);
            GetPatientFrame(patientFlat, side);

            // GATE 1 — wait for the player to dispatch the ambulance.
            yield return WaitForKey("Press E to dispatch the ambulance", Key.E);

            // Witness (and any standing bystanders) will turn to watch the action.
            CaptureWatchers(patientFlat);
            _focus = () => ambulance ? ambulance.position : patientFlat;
            _watching = true;
            StartCoroutine(WatchRoutine());

            // --- 1. Ambulance drives in front-first, lights + siren on. ---
            if (ambulance) { ambulance.SetPositionAndRotation(startPos, driveRot); ambulance.gameObject.SetActive(true); }
            float halfW, halfL, halfH;
            GetAmbulanceHalfExtents(out halfW, out halfL, out halfH);
            if (lightBar) lightBar.On();
            if (siren) { siren.Play(); Debug.Log("[Rescue] siren ON"); }
            Debug.Log($"[Rescue] StartRescue at {patientPos:F2} — ambulance driving in.");
            CutTo(patientFlat + side * camWideSide + Vector3.up * (camWideUp * 0.55f) - fwd * 4f,
                  (startPos + stopPos) * 0.5f + Vector3.up * 1.5f);   // ARRIVAL WIDE — lower, tilted up
            yield return FollowPath(ambulanceFollower, driveSpeed, stopPos);
            Debug.Log("[Rescue] Ambulance parked (rear toward patient).");

            // --- 2. Chad exits OUTSIDE the body and walks AROUND the vehicle to the patient. ---
            Vector3 chadSpawn   = stopPos + sideToPatient * (halfW + 0.9f) + fwd * (halfL * 0.55f);
            chadSpawn.y = groundY;
            Vector3 chadAround  = stopPos + sideToPatient * (halfW + 1.3f);   // alongside, clear of the body
            chadAround.y = groundY;
            // Stop point: beside her CHEST, on the ROAD side of her body (never on/across her).
            Vector3 chadStand   = _chestGround + _bodyRoadSide * cprKneelOffset;
            chadStand.y = groundY;
            if (chad) { chad.gameObject.SetActive(true); chad.position = chadSpawn; FaceXZ(chad, chadAround); }
            _focus = () => patientFlat + Vector3.up * 1.2f;
            // Two-shot: frame Chad AND the patient together (neither blocking the other), blended in
            // smoothly as he approaches and HELD through CPR and the lift (no hard cut).
            Vector3 tsPos, tsLook; TwoShotCam(chadStand, patientFlat, fwd, out tsPos, out tsLook);
            StartCoroutine(BlendTo(tsPos, tsLook, twoShotBlend));
            yield return FollowPath(chadFollower, chadWalkSpeed, Route(chadSpawn, chadAround, chadStand));
            FaceXZ(chad, _chestGround);
            Debug.Log("[Rescue] Chad reached patient.");

            // GATE 2 — wait for the player to start CPR.
            yield return WaitForKey("Press C to start CPR", Key.C);

            // --- 3. Assessment / CPR — kneel directly beside her chest, facing her, before the clip. ---
            if (chad) { chad.position = chadStand; FaceXZ(chad, _chestGround); }
            Debug.Log($"[Rescue] CPR kneel at {chadStand:F2}, facing chest {_chestGround:F2}.");
            CurrentBeat = "cpr";
            if (useProceduralCrouch && chadMedic) yield return chadMedic.CrouchAndCPR(cprSeconds);
            else yield return CPRViaClips();
            Debug.Log("[Rescue] CPR / assessment done.");

            // GATE 3 — wait for the player to lift the patient onto the stretcher.
            yield return WaitForKey("Press F to lift the patient onto the stretcher", Key.F);

            // --- 4. Chad fetches the stretcher from the rear and rolls it up beside the patient. ---
            Vector3 rearPos = (rearDoorPoint ? rearDoorPoint.position : stopPos - fwd * (halfL + 0.5f));
            rearPos.y = groundY;
            // Walk to the ambulance rear, curving AROUND her body on every leg (never over her).
            yield return FollowPath(chadFollower, chadWalkSpeed, Route(chadStand, rearPos));
            // Park the stretcher BESIDE her on the road side, aligned with HER body axis (never across her).
            Vector3 besideKate = _chestGround + _bodyRoadSide * stretcherSideGap; besideKate.y = groundY;
            if (stretcher)
            {
                stretcher.gameObject.SetActive(true);
                stretcher.position = rearPos;
                Vector3 toBeside = besideKate - rearPos; toBeside.y = 0f;
                stretcher.rotation = Quaternion.LookRotation(toBeside.sqrMagnitude > 0.01f ? toBeside.normalized : fwd, Vector3.up);
            }
            // Two-shot is HELD here (no cut) so Chad and the patient stay framed through the transfer.
            yield return Push(Route(rearPos, besideKate));   // roll up, curving around her body
            if (stretcher) stretcher.rotation = Quaternion.LookRotation(_bodyAxis, Vector3.up);   // align with her length
            Debug.Log("[Rescue] Stretcher rolled up beside patient.");

            // --- 5. Chad lifts the patient onto the adjacent stretcher; she ends lying flat. ---
            CurrentBeat = "lift";
            if (chad)
            {
                // Kneel on the ROAD side (same side he worked — never crossing her), beside the stretcher
                // head so the patient doesn't lerp through him.
                Vector3 liftSpot = chadStand + _bodyAxis * 0.6f; liftSpot.y = groundY;
                chad.position = liftSpot;
                FaceXZ(chad, besideKate);      // face the stretcher and place her onto it
            }
            Coroutine gesture = null;
            if (chadMedic != null && useProceduralCrouch) gesture = StartCoroutine(chadMedic.CrouchAndCPR(liftDuration));
            else if (chadAnimator) chadAnimator.SetTrigger("Lift");    // Tender Placement clip — kneel & place
            yield return LiftOnto(_patient, patientAnchor);
            if (gesture != null) yield return gesture;
            Debug.Log("[Rescue] Patient on stretcher.");

            // --- 6. Chad pushes the loaded stretcher to the open rear and loads it (no doors). ---
            CurrentBeat = "load";
            Vector3 loadPos = (rearDoorPoint ? rearDoorPoint.position : rearPos); loadPos.y = groundY;
            CutTo(loadPos + sideToPatient * (camMedSide + 0.8f) + Vector3.up * 1.3f - fwd * 2f,
                  loadPos + Vector3.up * 0.8f);                // REAR LOAD — framed on the bay
            yield return Push(besideKate, loadPos);
            if (stretcher && rearDoorPoint) stretcher.position = rearDoorPoint.position;
            if (stretcher) stretcher.gameObject.SetActive(false);   // slid into the bay
            Debug.Log("[Rescue] Patient loaded at the rear.");

            // --- 7. Chad boards. ---
            Vector3 boardPos = stopPos + sideToPatient * (halfW + 0.7f) - fwd * (halfL * 0.2f); boardPos.y = groundY;
            if (chad && chadFollower)
            {
                yield return FollowPath(chadFollower, chadWalkSpeed, boardPos);
                chad.SetParent(ambulance, true);
                chad.gameObject.SetActive(false);
                Debug.Log("[Rescue] Chad boarded.");
            }

            // --- 8. Depart: the ambulance leaves down the road and around the corner. The camera
            //         HOLDS on the scene (the witness + the street); the ambulance drives OUT of
            //         frame rather than being chased into the empty map, then deactivates out of sight.
            CurrentBeat = "depart";
            Transform witnessT = _watchers.Count > 0 ? _watchers[0] : null;
            Vector3 holdLook = (witnessT ? witnessT.position : patientFlat) + Vector3.up * 1.1f;
            CutTo(holdLook - fwd * 5f + side * 4.5f + Vector3.up * 1.6f, holdLook);  // HOLD on the scene
            _focus = () => ambulance ? ambulance.position : exitPos;                 // witness watches it leave
            Vector3 d1 = stopPos + fwd * departForward;                              // straight down the road
            Vector3 d2 = d1 + side * departTurnSide;                                 // around the corner, off the road
            yield return FollowPath(ambulanceFollower, driveSpeed, d1, d2);
            if (siren) { siren.Stop(); Debug.Log("[Rescue] siren OFF"); }
            if (lightBar) lightBar.Off();
            if (ambulance) ambulance.gameObject.SetActive(false);                    // gone — out of sight

            _watching = false;
            CurrentBeat = "done";
            RaiseCompletedOnce(patientPos);
            CleanupTemp();
        }

        // Roll the stretcher to the targets while keeping Chad planted at the push handle —
        // BEHIND the actual direction of motion each frame (so he always pushes, never pulls,
        // regardless of how the cart is oriented), legs animating, moving WITH the cart.
        private IEnumerator Push(params Vector3[] targets)
        {
            if (stretcherFollower == null || stretcher == null || targets == null || targets.Length == 0)
            {
                yield return FollowPath(stretcherFollower, stretcherSpeed, targets);
                yield break;
            }

            stretcherFollower.speed = stretcherSpeed;
            stretcherFollower.loop = false;
            var arr = new Transform[targets.Length];
            for (int i = 0; i < targets.Length; i++) arr[i] = MakeMarker(targets[i]);
            stretcherFollower.waypoints = arr;

            if (chad)
            {
                chad.SetParent(transform, true);     // not parented — positioned each frame
                if (chadAnimator) { chadAnimator.SetBool("IsWalking", true); chadAnimator.CrossFadeInFixedTime("Walk", 0.15f); }  // force walk out of any pose
            }

            bool done = false;
            void Handler() { done = true; }
            stretcherFollower.OnPathComplete += Handler;
            Vector3 lastPos = stretcher.position;
            stretcherFollower.Begin();
            while (!done)
            {
                Vector3 vel = stretcher.position - lastPos; vel.y = 0f;
                lastPos = stretcher.position;
                if (chad && vel.sqrMagnitude > 1e-7f)
                {
                    Vector3 dir = vel.normalized;
                    Vector3 behind = stretcher.position - dir * pushHandleBack;
                    behind.y = stretcher.position.y;
                    chad.position = behind;
                    chad.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
                yield return null;
            }
            stretcherFollower.OnPathComplete -= Handler;
            if (chad && chadAnimator) { chadAnimator.SetBool("IsWalking", false); chadAnimator.CrossFadeInFixedTime("Idle", 0.2f); }
        }

        // A short, controlled lift from the ground onto the adjacent stretcher, then parent flat
        // in a calm supine "sleeping" pose (relaxed idle, lying face-up on the bed).
        private IEnumerator LiftOnto(Transform patient, Transform anchor)
        {
            if (patient == null || anchor == null) yield break;

            // Relax the patient out of the fall/stunned pose into a neutral idle so she reads as
            // resting rather than contorted once laid flat.
            var pa = patient.GetComponentInChildren<Animator>();
            if (pa != null && pa.runtimeAnimatorController != null && HasIdleState(pa))
            {
                pa.Play("Idle", 0, 0f);
                pa.Update(0f);
            }

            float surfaceY = MattressTopY();   // true top of the mattress mesh — no guessed number

            Vector3 from = patient.position;
            Quaternion r0 = patient.rotation;
            Quaternion lie = Quaternion.Euler(patientLocalEuler);       // supine, face-up
            Quaternion r1 = anchor.rotation * lie;
            // The character's pivot is at the feet, so offset along the deck to CENTER her body on it.
            Vector3 target = anchor.TransformPoint(patientLocalOffset);
            float t = 0f;
            while (t < liftDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / liftDuration));
                patient.position = Vector3.Lerp(from, target, k);
                patient.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            // Parent to the bed, aligned with its length, supine.
            patient.SetParent(anchor, true);
            patient.localPosition = patientLocalOffset;
            patient.localRotation = lie;
            // Rest her ON TOP of the mattress: measure her lowest point and lift so it meets the surface
            // (instead of clipping into the mattress). Computed from the mesh bound, not a magic number.
            if (pa != null) pa.Update(0f);
            Bounds b = RendererBounds(patient);
            float delta = surfaceY - b.min.y;
            patient.position += Vector3.up * delta;
            Debug.Log($"[Rescue] Stretcher surface Y={surfaceY:F3}; patient back was {b.min.y:F3}, raised +{delta:F3} to rest on top.");
        }

        private static bool HasIdleState(Animator a)
        {
            // Avoid the "state does not exist" warning for non-Kate patients.
            return a.HasState(0, Animator.StringToHash("Idle"));
        }

        // Reuse WaypointFollower (translate + IsWalking if it has an animator) via temp markers.
        // Completion is the follower's OnPathComplete event — never a settle timer.
        private IEnumerator FollowPath(WaypointFollower f, float speed, params Vector3[] targets)
        {
            if (f == null || targets == null || targets.Length == 0) yield break;
            f.speed = speed;
            f.loop = false;
            var arr = new Transform[targets.Length];
            for (int i = 0; i < targets.Length; i++) arr[i] = MakeMarker(targets[i]);
            f.waypoints = arr;

            bool done = false;
            void Handler() { done = true; }
            f.OnPathComplete += Handler;
            f.Begin();
            while (!done) yield return null;
            f.OnPathComplete -= Handler;
        }

        private IEnumerator CPRViaClips()
        {
            if (chadAnimator) chadAnimator.SetTrigger("Kneel");
            yield return new WaitForSeconds(1.2f);
            if (chadAnimator) chadAnimator.SetTrigger("DoCPR");
            yield return new WaitForSeconds(cprSeconds);
            if (chadAnimator) chadAnimator.SetTrigger("StandUp");
            yield return new WaitForSeconds(1.0f);
        }

        // Closest non-rig Animator to the event position (the real victim). Falls back to a covered
        // stand-in only when no body exists (e.g. an empty test scene).
        private Transform AcquirePatient(Vector3 pos)
        {
            Animator best = null;
            float bestD = patientPickupRadius * patientPickupRadius;
            foreach (var a in FindObjectsOfType<Animator>())
            {
                if (a == null) continue;
                if (chadAnimator && a == chadAnimator) continue;
                if (a.transform.IsChildOf(transform)) continue;
                float d = (a.transform.position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a; }
            }
            if (best != null) { Debug.Log($"[Rescue] Patient acquired: {best.name}"); return best.transform; }

            var standin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            standin.name = "PatientStandIn";
            standin.transform.position = pos;
            standin.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var col = standin.GetComponent<Collider>();
            if (col) Destroy(col);
            Debug.Log("[Rescue] No patient body found — spawned stand-in.");
            return standin.transform;
        }

        private IEnumerator CloseDoors()
        {
            if (rearDoors == null || rearDoors.Length == 0) { yield return null; yield break; }
            var open = new Quaternion[rearDoors.Length];
            for (int i = 0; i < rearDoors.Length; i++)
                open[i] = rearDoors[i] ? rearDoors[i].localRotation : Quaternion.identity;
            float t = 0f;
            while (t < doorCloseTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / doorCloseTime));
                for (int i = 0; i < rearDoors.Length; i++)
                    if (rearDoors[i]) rearDoors[i].localRotation = Quaternion.Slerp(open[i], Quaternion.identity, k);
                yield return null;
            }
            for (int i = 0; i < rearDoors.Length; i++)
                if (rearDoors[i]) rearDoors[i].localRotation = Quaternion.identity;
        }

        // --- bystander watching ---
        private void CaptureWatchers(Vector3 near)
        {
            _watchers.Clear();
            var w = FindObjectOfType<WitnessController>();
            if (w) _watchers.Add(w.transform);   // the witness always turns to watch
            // plus any other standing character near the scene, but never the patient.
            foreach (var a in FindObjectsOfType<Animator>())
            {
                if (a == null) continue;
                if (chadAnimator && a == chadAnimator) continue;
                if (a.transform.IsChildOf(transform)) continue;
                Transform t = a.transform;
                if (_watchers.Contains(t)) continue;
                float d2 = (t.position - near).sqrMagnitude;
                if (d2 > bystanderWatchRadius * bystanderWatchRadius) continue;
                if (d2 < 1.6f * 1.6f) continue;   // this one is (or is on top of) the patient
                if (t.GetComponent<WaypointFollower>() && t.GetComponent<WaypointFollower>().IsMoving) continue; // let walkers walk
                _watchers.Add(t);
            }
        }

        private IEnumerator WatchRoutine()
        {
            while (_watching)
            {
                if (_focus != null)
                {
                    Vector3 f = _focus();
                    foreach (var b in _watchers)
                    {
                        if (b == null) continue;
                        if (_patient != null && (b == _patient || b.IsChildOf(_patient))) continue;
                        Vector3 d = f - b.position; d.y = 0f;
                        if (d.sqrMagnitude > 0.04f)
                        {
                            Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
                            b.rotation = Quaternion.RotateTowards(b.rotation, want, 110f * Time.deltaTime);
                        }
                    }
                }
                yield return null;
            }
        }

        private void CutTo(Vector3 pos, Vector3 look)
        {
            if (rescueCamera == null) return;
            Vector3 dir = look - pos;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            rescueCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        // Smoothly ease the camera to a new pose (no hard cut).
        private IEnumerator BlendTo(Vector3 pos, Vector3 look, float dur)
        {
            if (rescueCamera == null) yield break;
            Vector3 p0 = rescueCamera.transform.position;
            Quaternion r0 = rescueCamera.transform.rotation;
            Vector3 d = look - pos; if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
            Quaternion r1 = Quaternion.LookRotation(d.normalized, Vector3.up);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rescueCamera.transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            rescueCamera.transform.SetPositionAndRotation(pos, r1);
        }

        // Frame BOTH points (Chad + patient) in one shot: offset off the line between them so neither
        // blocks the other, angled ~twoShotAngle off the perpendicular, on the OPEN side (away from the
        // ambulance/+fwd), and elevated.
        private void TwoShotCam(Vector3 a, Vector3 b, Vector3 fwd, out Vector3 pos, out Vector3 look)
        {
            a.y = groundLevel; b.y = groundLevel;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 axis = b - a; axis.y = 0f;
            axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, axis).normalized;
            if (Vector3.Dot(perp, fwd) > 0f) perp = -perp;                 // open side, away from the vehicle
            float rad = twoShotAngle * Mathf.Deg2Rad;
            Vector3 dir = (perp * Mathf.Cos(rad) + axis * Mathf.Sin(rad)).normalized;
            pos = mid + dir * twoShotDist + Vector3.up * twoShotHeight;
            look = mid + Vector3.up * twoShotLookUp;
        }

        // If the straight path from->to passes within `clear` of the obstacle, return a curved route
        // [via, to] that keeps `clear` away from it; otherwise the direct [to]. WaypointFollower only.
        private Vector3[] RouteAround(Vector3 from, Vector3 to, Vector3 obstacle, float clear)
        {
            Vector3 f = from; f.y = 0f; Vector3 t = to; t.y = 0f; Vector3 o = obstacle; o.y = 0f;
            Vector3 dir = t - f; float len = dir.magnitude;
            if (len < 0.01f) return new[] { to };
            dir /= len;
            float proj = Mathf.Clamp(Vector3.Dot(o - f, dir), 0f, len);
            Vector3 closest = f + dir * proj;
            if (Vector3.Distance(closest, o) >= clear) return new[] { to };  // already clear
            Vector3 away = closest - o; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.Cross(Vector3.up, dir);
            away.Normalize();
            Vector3 via = o + away * clear; via.y = from.y;
            return new[] { via, to };
        }

        // Compute the patient's body frame from her real transform/bones: chest (kneel anchor), the
        // head→toe body axis, the centre, and the "road side" (the horizontal perpendicular pointing
        // toward the ambulance). Falls back to the event position if no humanoid is found.
        private void GetPatientFrame(Vector3 patientFlat, Vector3 roadDir)
        {
            _bodyCenter = patientFlat; _chestGround = patientFlat;
            _bodyAxis = (Vector3.Cross(Vector3.up, roadDir)).normalized;
            _bodyRoadSide = roadDir;
            if (_patient == null) return;

            var pa = _patient.GetComponentInChildren<Animator>();
            Transform chest = null, head = null, hips = null;
            if (pa != null && pa.isHuman)
            {
                chest = pa.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null) chest = pa.GetBoneTransform(HumanBodyBones.Spine);
                head = pa.GetBoneTransform(HumanBodyBones.Head);
                hips = pa.GetBoneTransform(HumanBodyBones.Hips);
            }
            Vector3 chestW = chest ? chest.position : _patient.position;
            _chestGround = new Vector3(chestW.x, groundLevel, chestW.z);
            Vector3 mid = (head && hips) ? (head.position + hips.position) * 0.5f : _patient.position;
            _bodyCenter = new Vector3(mid.x, groundLevel, mid.z);
            Vector3 axis = (head && hips) ? (head.position - hips.position) : _patient.up;
            axis.y = 0f;
            if (axis.sqrMagnitude > 0.01f) _bodyAxis = axis.normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, _bodyAxis).normalized;
            _bodyRoadSide = Vector3.Dot(perp, roadDir) >= 0f ? perp : -perp;
        }

        // Build a Chad/stretcher path from `start` through `wps`, inserting curve-around waypoints for
        // any leg that would pass within (bodyRadius + pathClearance) of her body. WaypointFollower only.
        private Vector3[] Route(Vector3 start, params Vector3[] wps)
        {
            var res = new List<Vector3>();
            Vector3 cur = start;
            foreach (var wp in wps)
            {
                foreach (var p in RouteAroundBody(cur, wp)) res.Add(p);
                cur = wp;
            }
            return res.ToArray();
        }

        // Treat the patient as a horizontal capsule (segment along her body axis, radius bodyRadius).
        // If from→to passes within bodyRadius+pathClearance of it, detour via a point pushed out to that
        // distance on the path's side; otherwise go direct.
        private Vector3[] RouteAroundBody(Vector3 from, Vector3 to)
        {
            if (_patient == null) return new[] { to };
            float R = bodyRadius + pathClearance;
            Vector3 bA = _bodyCenter + _bodyAxis * bodyHalfLength;
            Vector3 bB = _bodyCenter - _bodyAxis * bodyHalfLength;
            Vector3 cPath, cBody;
            float dist = ClosestSegSegXZ(from, to, bA, bB, out cPath, out cBody);
            if (dist >= R) return new[] { to };
            Vector3 outDir = cPath - cBody; outDir.y = 0f;
            if (outDir.sqrMagnitude < 1e-4f) outDir = _bodyRoadSide;   // path straight over her → push to the road side
            outDir.Normalize();
            Vector3 via = cBody + outDir * R; via.y = from.y;
            return new[] { via, to };
        }

        // Closest distance between two segments on the ground (XZ) plus the closest points.
        private static float ClosestSegSegXZ(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, out Vector3 cA, out Vector3 cB)
        {
            Vector2 a1 = new Vector2(p1.x, p1.z), a2 = new Vector2(p2.x, p2.z), b1 = new Vector2(p3.x, p3.z), b2 = new Vector2(p4.x, p4.z);
            Vector2 d1 = a2 - a1, d2 = b2 - b1, r = a1 - b1;
            float A = Vector2.Dot(d1, d1), E = Vector2.Dot(d2, d2), F = Vector2.Dot(d2, r);
            float s = 0f, t = 0f;
            if (A <= 1e-6f && E <= 1e-6f) { s = 0f; t = 0f; }
            else if (A <= 1e-6f) { s = 0f; t = Mathf.Clamp01(F / E); }
            else
            {
                float C = Vector2.Dot(d1, r);
                if (E <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-C / A); }
                else
                {
                    float B = Vector2.Dot(d1, d2); float denom = A * E - B * B;
                    s = denom > 1e-6f ? Mathf.Clamp01((B * F - C * E) / denom) : 0f;
                    t = (B * s + F) / E;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-C / A); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((B - C) / A); }
                }
            }
            Vector2 ca = a1 + d1 * s, cb = b1 + d2 * t;
            cA = new Vector3(ca.x, 0f, ca.y); cB = new Vector3(cb.x, 0f, cb.y);
            return Vector2.Distance(ca, cb);
        }

        // World-space top of the stretcher mattress mesh (no guessed number).
        private float MattressTopY()
        {
            if (stretcher != null)
            {
                var m = stretcher.Find("Mattress");
                if (m != null) { var r = m.GetComponent<Renderer>(); if (r != null) return r.bounds.max.y; }
            }
            return patientAnchor ? patientAnchor.position.y : groundLevel + 0.7f;
        }

        private static Bounds RendererBounds(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            bool first = true; Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (var r in rends)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private void GetAmbulanceHalfExtents(out float halfW, out float halfL, out float halfH)
        {
            halfW = 1.4f; halfL = 3.2f; halfH = 1.3f;
            if (ambulance == null) return;
            var rends = ambulance.GetComponentsInChildren<Renderer>();
            bool first = true; Bounds b = new Bounds(ambulance.position, Vector3.one);
            foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            if (first) return;
            // The ambulance is parked facing +Z, so its world AABB lines up with its own axes.
            Vector3 e = b.extents;
            halfW = Mathf.Max(0.5f, Mathf.Abs(e.x));
            halfL = Mathf.Max(0.5f, Mathf.Abs(e.z));
            halfH = Mathf.Max(0.5f, Mathf.Abs(e.y));
        }

        private void RaiseCompletedOnce(Vector3 pos)
        {
            if (_completed) return;
            _completed = true;
            if (rescueCompletedChannel) rescueCompletedChannel.Raise(pos);
            Debug.Log($"[Rescue] RescueCompleted raised at {pos:F2}.");
        }

        private Transform MakeMarker(Vector3 pos)
        {
            var go = new GameObject("rescue_wp");
            go.transform.position = pos;
            go.transform.SetParent(transform, true);
            _temp.Add(go.transform);
            return go.transform;
        }

        private void CleanupTemp()
        {
            foreach (var t in _temp) if (t) Destroy(t.gameObject);
            _temp.Clear();
        }

        private static void FaceXZ(Transform who, Vector3 target)
        {
            if (who == null) return;
            Vector3 dir = target - who.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) who.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
