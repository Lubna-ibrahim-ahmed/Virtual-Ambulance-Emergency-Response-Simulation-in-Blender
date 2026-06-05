using System;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// The graded physics component. The hero tree is a separate Rigidbody pinned at its
    /// base by a HingeJoint (connectedBody = null → pinned to the world). It is held
    /// upright as kinematic until Topple(): physics is enabled and a torque impulse tips
    /// it past balance, after which gravity rotates it about the hinge like a real tree.
    /// Impact is reported by a TreeCanopyTrigger child (decoupled from Kate).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TreeFallController : MonoBehaviour
    {
        public Rigidbody body;
        public HingeJoint hinge;
        [Tooltip("Impulse torque applied about the world fall axis to start the topple.")]
        public float toppleTorque = 600f;
        [Tooltip("Local axis the tree rotates about when falling (horizontal, perpendicular to fall dir).")]
        public Vector3 torqueAxisLocal = Vector3.forward;

        public event Action<Vector3> OnHitPatient;

        private bool _toppled, _hit;
        private Quaternion _startRot;
        private Vector3 _startPos;

        private void Reset() { body = GetComponent<Rigidbody>(); }

        private void Awake()
        {
            if (!body) body = GetComponent<Rigidbody>();
            _startRot = transform.rotation;
            _startPos = transform.position;
            HoldUpright();
        }

        public void HoldUpright()
        {
            _toppled = false;
            _hit = false;
            if (body)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
        }

        /// <summary>Full reset for an idempotent scenario restart.</summary>
        public void ResetUpright()
        {
            transform.SetPositionAndRotation(_startPos, _startRot);
            HoldUpright();
        }

        public void Topple()
        {
            if (_toppled) return;
            _toppled = true;
            if (body)
            {
                body.isKinematic = false;
                Vector3 axis = transform.TransformDirection(torqueAxisLocal).normalized;
                body.AddTorque(axis * toppleTorque, ForceMode.Impulse);
            }
        }

        /// <summary>Called by the canopy trigger child when something enters it.
        /// Fires only for the patient (detected by KateVictim component), so the tree
        /// stays decoupled from Kate yet ignores buildings/ground/other characters.</summary>
        public void NotifyHit(Collider other, Vector3 point)
        {
            if (_hit || other == null) return;
            if (other.GetComponentInParent<KateVictim>() == null) return;
            _hit = true;
            OnHitPatient?.Invoke(point);
        }
    }
}
