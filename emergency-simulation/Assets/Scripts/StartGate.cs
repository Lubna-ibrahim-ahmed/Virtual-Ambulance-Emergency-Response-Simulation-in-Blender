using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EmergencySim
{
    /// <summary>
    /// One interactive gate for Scenario2. The car does NOT drive on scenario start; a centered
    /// "Press D to start the car" prompt is shown while Kate walks. On D, the prompt hides and the
    /// car's existing approach begins (Scenario2Director.LaunchCar) — everything downstream
    /// (brake zone, impact, debris, blood, witness, handoff) is unchanged.
    ///
    /// So the collision still lands at the same spot no matter when D is pressed, Kate pauses at
    /// the pre-crossing waypoint (the curb) and only steps into the road once the started car has
    /// driven close to the crossing — i.e. she waits at the curb until the car is upon her.
    /// </summary>
    public class StartGate : MonoBehaviour
    {
        public Scenario2Director director;
        public CarController car;
        public WaypointFollower kateFollower;
        public Transform curbWaypoint;     // pre-crossing waypoint
        public Transform impactPoint;      // where the car hits (for the proximity release)
        public GameObject prompt;          // centered UI prompt object (toggled)
        [Tooltip("How close to the curb counts as 'reached the curb'.")]
        public float curbReachDist = 0.7f;
        [Tooltip("Release Kate to step into the road once the car is within this distance of the impact point.")]
        public float releaseDist = 16f;

        private bool _carStarted;
        private bool _katePaused;
        private bool _kateReleased;

        private void Start()
        {
            if (prompt) prompt.SetActive(true);
        }

        private void Update()
        {
            // Hold Kate at the curb (pre-crossing) until she's released, so her crossing always
            // syncs with the car regardless of when D is pressed.
            if (!_katePaused && !_kateReleased && kateFollower && curbWaypoint &&
                FlatDist(kateFollower.transform.position, curbWaypoint.position) < curbReachDist)
            {
                kateFollower.Halt();
                _katePaused = true;
            }

            // D starts the car's approach.
            if (!_carStarted && DPressed()) StartCar();

            // Once the started car is bearing down on the crossing, Kate steps off the curb into it.
            if (_carStarted && _katePaused && !_kateReleased && car && impactPoint &&
                FlatDist(car.transform.position, impactPoint.position) <= releaseDist)
            {
                kateFollower.Resume();
                _kateReleased = true;
            }
        }

        /// <summary>Hides the prompt and starts the car. Public so a debug menu item can trigger it too.</summary>
        public void StartCar()
        {
            if (_carStarted) return;
            _carStarted = true;
            if (prompt) prompt.SetActive(false);
            if (director) director.LaunchCar();
            else if (car) car.Begin();
        }

        private bool DPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.D);
#endif
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
