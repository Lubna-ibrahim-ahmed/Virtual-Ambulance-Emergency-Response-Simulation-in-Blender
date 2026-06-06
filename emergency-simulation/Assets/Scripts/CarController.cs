using System;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Drives the "hitting_car" along a waypoint path at car-like speed. The car is a KINEMATIC
    /// Rigidbody; transform translation is authoritative (mirrors WaypointFollower), and the wheel
    /// transforms are spun so it reads as DRIVING — not sliding sideways.
    ///
    /// Flow: cruise → enter the brake zone → decelerate to a crawl with a tyre-screech (it can't
    /// stop in time) → a front trigger (CarImpactTrigger) reports contact with Kate → OnHitPatient
    /// fires ONCE, the debris Rigidbodies are released with an impulse (the graded physics piece),
    /// and the car brakes the rest of the way to a halt. Fully decoupled from Kate: it only knows
    /// it struck "a KateVictim".
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public Rigidbody body;
        public Transform[] waypoints;
        [Tooltip("Cruising speed in m/s (9 ~ 32 km/h).")]
        public float cruiseSpeed = 9f;
        [Tooltip("Deceleration once braking, m/s^2.")]
        public float brakeDecel = 6f;
        [Tooltip("Pre-impact braking won't drop below this, so the car still rolls into Kate.")]
        public float crawlSpeed = 2.2f;
        [Tooltip("Harder deceleration applied after the impact, m/s^2.")]
        public float impactDecel = 14f;
        public float arriveThreshold = 0.25f;

        [Header("Wheels (read as driving)")]
        public Transform[] wheels;
        public float wheelRadius = 0.35f;
        public Vector3 wheelSpinAxisLocal = Vector3.right;

        [Header("Impact / debris (graded physics)")]
        public Rigidbody[] debris;            // mirror/bumper-like pieces, kinematic until impact
        public Vector3 debrisLocalImpulse = new Vector3(0f, 3.5f, 4.5f);
        public float debrisJitter = 2.5f;
        public AudioSource screech;

        public event Action<Vector3> OnHitPatient;

        private int _index = -1;
        private bool _driving, _braking, _hit;
        private float _speed;
        private Quaternion _startRot; private Vector3 _startPos;

        private void Reset() { body = GetComponent<Rigidbody>(); }

        private void Awake()
        {
            if (!body) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            _startPos = transform.position;
            _startRot = transform.rotation;
            if (debris != null) foreach (var d in debris) if (d) d.isKinematic = true;
        }

        public void Begin()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            _index = 0; _driving = true; _braking = false; _hit = false; _speed = cruiseSpeed;
        }

        public void ResetCar()
        {
            _driving = false; _braking = false; _hit = false; _speed = 0f; _index = -1;
            transform.SetPositionAndRotation(_startPos, _startRot);
            if (screech) screech.Stop();
        }

        /// <summary>Called by the brake-zone trigger when the car reaches the crossing.</summary>
        public void BeginBrake()
        {
            if (_braking) return;
            _braking = true;
            if (screech && !screech.isPlaying) screech.Play();
        }

        /// <summary>Called by the front CarImpactTrigger. Fires once, for the patient only.</summary>
        public void NotifyHit(Collider other, Vector3 point)
        {
            if (_hit || other == null) return;
            if (other.GetComponentInParent<KateVictim>() == null) return;
            _hit = true;
            _braking = true;
            if (screech && !screech.isPlaying) screech.Play();
            ReleaseDebris();
            OnHitPatient?.Invoke(point);
        }

        private void ReleaseDebris()
        {
            if (debris == null) return;
            for (int i = 0; i < debris.Length; i++)
            {
                var d = debris[i];
                if (!d) continue;
                d.transform.SetParent(null, true);
                d.isKinematic = false;
                Vector3 jit = new Vector3(
                    UnityEngine.Random.Range(-debrisJitter, debrisJitter),
                    UnityEngine.Random.Range(0f, debrisJitter),
                    UnityEngine.Random.Range(-debrisJitter, debrisJitter));
                Vector3 imp = transform.TransformDirection(debrisLocalImpulse) + jit;
                d.AddForce(imp, ForceMode.Impulse);
                d.AddTorque(jit, ForceMode.Impulse);
            }
        }

        private void Update()
        {
            if (!_driving) return;

            if (_braking)
            {
                float decel = _hit ? impactDecel : brakeDecel;
                float floor = _hit ? 0f : crawlSpeed;     // keep rolling into Kate until contact
                _speed = Mathf.Max(floor, _speed - decel * Time.deltaTime);
            }

            if (_speed <= 0.001f)
            {
                _driving = false;
                SpinWheels(0f);
                return;
            }

            if (_index < 0 || _index >= waypoints.Length) { _driving = false; return; }
            var target = waypoints[_index];
            if (target == null) { _index++; return; }

            Vector3 flatTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            Vector3 dir = flatTarget - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            transform.position = Vector3.MoveTowards(transform.position, flatTarget, _speed * Time.deltaTime);
            SpinWheels(_speed);

            if ((flatTarget - transform.position).magnitude <= arriveThreshold)
            {
                _index++;
                if (_index >= waypoints.Length) _driving = false;
            }
        }

        private void SpinWheels(float speed)
        {
            if (wheels == null) return;
            float deg = (speed / Mathf.Max(0.01f, wheelRadius)) * Mathf.Rad2Deg * Time.deltaTime;
            foreach (var w in wheels) if (w) w.Rotate(wheelSpinAxisLocal, deg, Space.Self);
        }
    }
}
