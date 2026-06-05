using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Orchestrates the "Falling Tree on Kate" cinematic as one event-driven coroutine.
    ///
    /// MENU INTEGRATION: StartScenario() is the single public entry point the
    /// scenario-selection menu calls after loading this scene. It also self-starts on load
    /// (autoStartOnLoad) so opening the scene directly works too. The call is idempotent
    /// (guarded), so both paths are safe.
    ///
    /// HANDOFF: when the witness's 911 call finishes, the director raises the rescueChannel
    /// (a ScriptableObject Vector3 event) with Kate's position. It never references the
    /// rescue implementation — the teammate's reusable sequence listens on the same channel.
    /// </summary>
    public class ScenarioDirector : MonoBehaviour
    {
        [Header("Actors")]
        public Transform kate;
        public WaypointFollower kateFollower;
        public WaypointFollower[] backgroundFollowers;
        public KateVictim kateVictim;
        public WitnessController witness;
        public TreeFallController heroTree;
        public CameraDirector cameraDirector;
        public StormController storm;

        [Header("Handoff")]
        public Vector3GameEvent rescueChannel;

        [Header("Flow")]
        public bool autoStartOnLoad = true;
        [Tooltip("Kate waypoint index whose arrival topples the tree. -1 = end of her path.")]
        public int kateToppleWaypointIndex = -1;
        public float toppleDelay = 0.15f;
        public float postImpactHold = 1.2f;
        public float impactSafetyTimeout = 5f;

        private bool _running;

        // Recorded start poses for idempotent restart.
        private Vector3 _kateStartPos; private Quaternion _kateStartRot;
        private Vector3[] _bgStartPos; private Quaternion[] _bgStartRot;

        // Beat flags set by named handlers.
        private bool _kateReachedTree;
        private bool _treeHit;
        private bool _callDone;

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
            if (heroTree != null) heroTree.OnHitPatient += OnTreeHitPatient;
            if (witness != null) witness.OnPhoneCallFinished += OnPhoneCallFinished;
        }

        private void OnDestroy()
        {
            if (kateFollower != null)
            {
                kateFollower.OnReachedIndex -= OnKateReachedIndex;
                kateFollower.OnPathComplete -= OnKatePathComplete;
            }
            if (heroTree != null) heroTree.OnHitPatient -= OnTreeHitPatient;
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
            _kateReachedTree = false;
            _treeHit = false;
            _callDone = false;

            if (kate) kate.SetPositionAndRotation(_kateStartPos, _kateStartRot);
            if (kateFollower) kateFollower.Halt();
            if (kateVictim) kateVictim.ResetState();

            if (backgroundFollowers != null)
            {
                for (int i = 0; i < backgroundFollowers.Length; i++)
                {
                    if (backgroundFollowers[i] == null) continue;
                    backgroundFollowers[i].transform.SetPositionAndRotation(_bgStartPos[i], _bgStartRot[i]);
                    backgroundFollowers[i].Halt();
                }
            }

            if (heroTree) heroTree.ResetUpright();
            if (cameraDirector) cameraDirector.Snap(0);
        }

        private IEnumerator RunSequence()
        {
            Debug.Log("[Director] RunSequence START");
            // Shot 1: wide on the sidewalk + tree.
            if (cameraDirector) cameraDirector.Snap(0);

            // Everyone starts walking.
            if (backgroundFollowers != null)
                foreach (var f in backgroundFollowers) if (f) f.Begin();
            if (kateFollower) kateFollower.Begin();

            // Wait until Kate reaches the tree.
            while (!_kateReachedTree) yield return null;
            Debug.Log("[Director] Kate reached tree → lightning + toppling");

            // Storm "causes" it: a lightning strike just before the tree goes.
            if (storm) storm.TriggerLightning();
            yield return new WaitForSeconds(0.8f);

            yield return new WaitForSeconds(toppleDelay);
            // Cut to the tight fall angle as the tree comes down.
            if (cameraDirector) cameraDirector.Snap(1);
            if (heroTree) heroTree.Topple();

            // Wait for the canopy to hit Kate (with a safety fallback).
            float guard = 0f;
            while (!_treeHit && guard < impactSafetyTimeout) { guard += Time.deltaTime; yield return null; }
            Debug.Log($"[Director] Impact phase done (treeHit={_treeHit}) → knockdown");
            if (!_treeHit && kateVictim) kateVictim.Knockdown();

            yield return new WaitForSeconds(postImpactHold);

            // Cut to the witness and let them react + call 911.
            Debug.Log("[Director] Cut to witness → ReactAndCall");
            if (cameraDirector) cameraDirector.Snap(2);
            if (witness) witness.ReactAndCall();

            // Partway through the 911 call, cut to an alternate witness angle, then hand off.
            float altT = 0f; bool altCut = false;
            while (!_callDone)
            {
                altT += Time.deltaTime;
                if (!altCut && altT > 4f) { if (cameraDirector) cameraDirector.Snap(3); altCut = true; }
                yield return null;
            }

            Vector3 patientPos = kate ? kate.position : transform.position;
            if (rescueChannel) rescueChannel.Raise(patientPos);
            Debug.Log($"[ScenarioDirector] Sequence complete — handed off to rescue at {patientPos:F2}.");

            // Calm final state: stop the background pedestrians (they settle to Idle).
            if (backgroundFollowers != null)
                foreach (var f in backgroundFollowers) if (f) f.Halt();

            _running = false;
        }

        // --- named beat handlers ---
        private void OnKateReachedIndex(int i)
        {
            if (kateToppleWaypointIndex >= 0 && i == kateToppleWaypointIndex) _kateReachedTree = true;
        }
        private void OnKatePathComplete()
        {
            Debug.Log("[Director] OnKatePathComplete fired");
            if (kateToppleWaypointIndex < 0) _kateReachedTree = true;
        }
        private void OnTreeHitPatient(Vector3 point)
        {
            Debug.Log($"[Director] OnTreeHitPatient at {point:F2}");
            _treeHit = true;
            if (kateVictim) kateVictim.Knockdown();
        }
        private void OnPhoneCallFinished() => _callDone = true;
    }
}
