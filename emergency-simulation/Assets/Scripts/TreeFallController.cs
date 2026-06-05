using System;
using System.Collections;
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
        [Tooltip("(unused) legacy timer field; freezing now triggers on ground contact.")]
        public float settleDelay = 2.5f;
        [Tooltip("Seconds to keep settling after the tree reaches flat, before freezing.")]
        public float settleAfterContact = 0.5f;
        [Tooltip("Safety: freeze after this long even if the tree never reaches flat.")]
        public float fallbackFreeze = 8f;
        [Tooltip("Tilt from vertical (deg) after which physics has 'committed' the topple.")]
        public float commitAngle = 30f;
        [Tooltip("Seconds to smoothly settle the tree to a clean flat pose once committed.")]
        public float settleLerp = 0.7f;

        public event Action<Vector3> OnHitPatient;

        private bool _toppled, _hit, _settling, _frozen;
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
            _settling = false;
            _frozen = false;
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
            _settling = false;
            _frozen = false;
            if (body)
            {
                body.isKinematic = false;
                Vector3 axis = transform.TransformDirection(torqueAxisLocal).normalized;
                body.AddTorque(axis * toppleTorque, ForceMode.Impulse);
            }
            StartCoroutine(FreezeWhenFlat());
        }

        // Let the Rigidbody physically topple the tree (real fall about the hinge). Once it's
        // clearly committed to falling, take over and settle it to a clean FLAT pose on the
        // ground reaching Kate — guaranteeing it never freezes leaning mid-air.
        private IEnumerator FreezeWhenFlat()
        {
            float t = 0f;
            while (t < fallbackFreeze && Vector3.Angle(transform.up, Vector3.up) < commitAngle)
            {
                t += Time.deltaTime;
                yield return null;
            }
            // Take over from physics and lay it flat (trunk pointing -z, down the path onto Kate).
            if (body) body.isKinematic = true;
            Quaternion start = transform.rotation;
            Quaternion flat = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
            float lt = 0f;
            while (lt < settleLerp)
            {
                lt += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, flat, lt / settleLerp);
                yield return null;
            }
            transform.rotation = flat;
            _frozen = true;
        }

        private void FreezeBody()
        {
            if (_frozen) return;
            _frozen = true;
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
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
