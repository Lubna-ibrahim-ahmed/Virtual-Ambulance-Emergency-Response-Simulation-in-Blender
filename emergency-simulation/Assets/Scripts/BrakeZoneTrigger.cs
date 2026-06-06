using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// A trigger volume placed across the road at the crossing. When the car enters it, the
    /// driver "sees" Kate and slams the brakes (tyre-screech) — but too late to stop in time.
    /// Contact/event-based, not a timer.
    /// </summary>
    public class BrakeZoneTrigger : MonoBehaviour
    {
        public CarController car;

        private void OnTriggerEnter(Collider other)
        {
            var c = other.GetComponentInParent<CarController>();
            if (c != null && (car == null || c == car)) c.BeginBrake();
        }
    }
}
