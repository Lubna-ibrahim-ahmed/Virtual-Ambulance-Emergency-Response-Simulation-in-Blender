using System;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Moves a character through the world along an ordered list of waypoints while an
    /// in-place Walk clip plays. Because the Mixamo clips have no root motion, the
    /// transform translation here is the source of truth; the Animator only supplies the
    /// leg cycle (toggled via the IsWalking bool).
    /// </summary>
    public class WaypointFollower : MonoBehaviour
    {
        public Transform[] waypoints;
        public float speed = 1.4f;
        public float turnSpeedDeg = 540f;
        public float arriveThreshold = 0.1f;
        public bool loop = false;
        public Animator animator;
        public string walkBool = "IsWalking";

        public event Action<int> OnReachedIndex;
        public event Action OnPathComplete;

        private int _index = -1;
        private bool _moving;

        public bool IsMoving => _moving;

        public void Begin()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            _index = 0;
            _moving = true;
            if (animator) animator.SetBool(walkBool, true);
        }

        public void Halt()
        {
            _moving = false;
            if (animator) animator.SetBool(walkBool, false);
        }

        /// <summary>Continue from the current waypoint (after a Halt), without restarting the path.</summary>
        public void Resume()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            if (_index < 0) _index = 0;
            _moving = true;
            if (animator) animator.SetBool(walkBool, true);
        }

        private void Update()
        {
            if (!_moving || _index < 0 || _index >= waypoints.Length) return;
            var target = waypoints[_index];
            if (target == null) { Advance(); return; }

            // Face travel direction (yaw only).
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeedDeg * Time.deltaTime);
            }

            // Move on the XZ plane, keep current Y (stay grounded).
            Vector3 flatTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);

            Vector3 remaining = flatTarget - transform.position;
            if (remaining.magnitude <= arriveThreshold)
            {
                int reached = _index;
                OnReachedIndex?.Invoke(reached);
                Advance();
            }
        }

        private void Advance()
        {
            _index++;
            if (_index >= waypoints.Length)
            {
                if (loop)
                {
                    _index = 0;
                }
                else
                {
                    _moving = false;
                    if (animator) animator.SetBool(walkBool, false);
                    OnPathComplete?.Invoke();
                }
            }
        }
    }
}
