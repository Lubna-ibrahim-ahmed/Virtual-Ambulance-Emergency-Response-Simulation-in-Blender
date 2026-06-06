using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Orchestrates the "Car Collision on Kate" cinematic (Scenario2, daytime) as one
    /// event-driven coroutine. Mirrors ScenarioDirector but swaps the hazard (falling tree →
    /// driving car).
    ///
    /// MENU INTEGRATION: StartScenario() is the single public entry point the scenario-selection
    /// menu calls after loading this scene. It also self-starts (autoStartOnLoad). Idempotent.
    ///
    /// HANDOFF: when the witness's 911 call finishes, the director raises the shared
    /// rescueChannel (a ScriptableObject Vector3 event) with Kate's position — EXACTLY ONCE.
    /// It never references the rescue implementation; the teammate's sequence listens on the
    /// same asset.
    /// </summary>
    public class Scenario2Director : MonoBehaviour
    {
        [Header("Actors")]
        public Transform kate;
        public WaypointFollower kateFollower;
        public WaypointFollower[] backgroundFollowers;
        public KateVictim kateVictim;
        public WitnessController witness;
        public CarController car;
        public CameraDirector cameraDirector;

        [Header("Handoff")]
        public Vector3GameEvent rescueChannel;

        [Header("Flow")]
        public bool autoStartOnLoad = true;
        [Tooltip("Kate waypoint index whose arrival launches the car. -1 = launch the car at the start.")]
        public int carLaunchWaypointIndex = 1;
        public float postImpactHold = 1.2f;
        public float twoShotDelay = 1.8f;
        public float impactSafetyTimeout = 6f;

        private bool _running;

        // Recorded start poses for idempotent restart.
        private Vector3 _kateStartPos; private Quaternion _kateStartRot;
        private Vector3[] _bgStartPos; private Quaternion[] _bgStartRot;

        // Beat flags set by named handlers.
        private bool _carLaunched;
        private bool _hit;
        private bool _callDone;
        private bool _handedOff;

        private void Awake()
        {
            // Keep play mode ticking even when the editor lacks OS focus (MCP-driven sessions).
            Application.runInBackground = true;

            if (kate)
            {
                _kateStartPos = kate.position;
                _kateStartRot = kate.rotation;
            }
            if (backgroundFollowers != null)
            {
                _bgStartPos = new Vector3[backgroundFollowers.Length];
                _bgStartRot = new Quaternion[backgroundFollowers.Length];
                for (int i = 0; i < backgroundFollowers.Length; i++)
                {
                    if (backgroundFollowers[i] == null) continue;
                    _bgStartPos[i] = backgroundFollowers[i].transform.position;
                    _bgStartRot[i] = backgroundFollowers[i].transform.rotation;
                }
            }

            if (kateFollower != null)
            {
                kateFollower.OnReachedIndex += OnKateReachedIndex;
                kateFollower.OnPathComplete += OnKatePathComplete;
            }
            if (car != null) car.OnHitPatient += OnCarHitPatient;
            if (witness != null) witness.OnPhoneCallFinished += OnPhoneCallFinished;
        }

        private void OnDestroy()
        {
            if (kateFollower != null)
            {
                kateFollower.OnReachedIndex -= OnKateReachedIndex;
                kateFollower.OnPathComplete -= OnKatePathComplete;
            }
            if (car != null) car.OnHitPatient -= OnCarHitPatient;
            if (witness != null) witness.OnPhoneCallFinished -= OnPhoneCallFinished;
        }

        private void Start()
        {
            if (autoStartOnLoad) StartScenario();
        }

        /// <summary>Single public entry point — call this from the scenario-selection menu.</summary>
        public void StartScenario()
        {
            if (_running) return;
            _running = true;
            ResetState();
            StartCoroutine(RunSequence());
        }

        private void ResetState()
        {
            StopAllCoroutines();
            _carLaunched = false;
            _hit = false;
            _callDone = false;
            _handedOff = false;

            if (kate) kate.SetPositionAndRotation(_kateStartPos, _kateStartRot);
            if (kateFollower) kateFollower.Halt();
            if (kateVictim) kateVictim.ResetState();
            if (car) car.ResetCar();

            if (backgroundFollowers != null)
            {
                for (int i = 0; i < backgroundFollowers.Length; i++)
                {
                    if (backgroundFollowers[i] == null) continue;
                    backgroundFollowers[i].transform.SetPositionAndRotation(_bgStartPos[i], _bgStartRot[i]);
                    backgroundFollowers[i].Halt();
                }
            }

            if (cameraDirector) cameraDirector.Snap(0);
        }

        private IEnumerator RunSequence()
        {
            Debug.Log("[Scenario2] RunSequence START");
            // Shot 0: wide establishing on the crossing.
            if (cameraDirector) cameraDirector.Snap(0);

            // Everyone starts walking.
            if (backgroundFollowers != null)
                foreach (var f in backgroundFollowers) if (f) f.Begin();
            if (kateFollower) kateFollower.Begin();

            // If the car launches at the very start, do it now.
            if (carLaunchWaypointIndex < 0) LaunchCar();

            // Wait until Kate is hit (the car fires it) — with a safety fallback.
            float guard = 0f;
            while (!_hit && guard < impactSafetyTimeout + 6f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[Scenario2] Impact phase done (hit={_hit}) → knockdown");
            if (!_hit && kateVictim) kateVictim.Knockdown();

            // Brief beat on the impact, then cut to the witness — he reacts and calls quickly.
            yield return new WaitForSeconds(postImpactHold);
            Debug.Log("[Scenario2] Cut to witness → ReactAndCall");
            if (cameraDirector) cameraDirector.Snap(2);     // WITNESS reacts
            if (witness) witness.ReactAndCall();

            // After his reaction, the TWO-SHOT showing both him and Kate.
            yield return new WaitForSeconds(twoShotDelay);
            if (cameraDirector) cameraDirector.Snap(3);     // BOTH witness + Kate

            // Wait for the 911 call to finish, then hand off.
            while (!_callDone) yield return null;

            if (!_handedOff)
            {
                _handedOff = true;
                Vector3 patientPos = kate ? kate.position : transform.position;
                if (rescueChannel) rescueChannel.Raise(patientPos);
                Debug.Log($"[Handoff] RescueRequested({patientPos:F2}) — witness finished the 911 call. " +
                          "Awaiting teammate's rescue sequence.");
            }

            // (Background pedestrians keep walking — not halted — so the street stays alive.)
            _running = false;
        }

        private void LaunchCar()
        {
            if (_carLaunched) return;
            _carLaunched = true;
            if (cameraDirector) cameraDirector.Snap(1);     // cut to the approaching car
            if (car) car.Begin();
        }

        // --- named beat handlers ---
        private void OnKateReachedIndex(int i)
        {
            if (carLaunchWaypointIndex >= 0 && i == carLaunchWaypointIndex) LaunchCar();
        }
        private void OnKatePathComplete()
        {
            Debug.Log("[Scenario2] OnKatePathComplete fired");
            // If the car never launched (short path), launch it now as a fallback.
            if (!_carLaunched) LaunchCar();
        }
        private void OnCarHitPatient(Vector3 point)
        {
            Debug.Log($"[Scenario2] OnCarHitPatient at {point:F2}");
            _hit = true;
            if (kateFollower) kateFollower.Halt();
            if (kateVictim) kateVictim.Knockdown();
        }
        private void OnPhoneCallFinished() => _callDone = true;
    }
}
