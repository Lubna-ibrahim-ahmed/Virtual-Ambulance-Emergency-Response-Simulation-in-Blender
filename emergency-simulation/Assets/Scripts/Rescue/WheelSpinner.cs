using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Spins wheel transforms proportional to how far this object moved since last frame, so a
    /// translated vehicle/stretcher reads as rolling rather than sliding (the project's
    /// "no gliding statues" rule). Attach to the moving root and assign the wheel transforms.
    /// </summary>
    public class WheelSpinner : MonoBehaviour
    {
        public Transform[] wheels;
        [Tooltip("Wheel radius in metres; smaller spins faster.")]
        public float wheelRadius = 0.35f;
        [Tooltip("Local axis each wheel spins about.")]
        public Vector3 spinAxis = Vector3.right;

        private Vector3 _lastPos;

        private void OnEnable() { _lastPos = transform.position; }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - _lastPos;
            _lastPos = transform.position;
            if (wheels == null || wheels.Length == 0 || wheelRadius <= 0.0001f) return;

            // Distance travelled along the forward axis maps to wheel rotation.
            float dist = Vector3.Dot(delta, transform.forward);
            float deg = (dist / (2f * Mathf.PI * wheelRadius)) * 360f;
            foreach (var w in wheels)
                if (w) w.Rotate(spinAxis, deg, Space.Self);
        }
    }
}
