using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Sits on the car's front trigger collider and forwards contact to the CarController,
    /// which decides (by KateVictim component) whether it struck the patient. Mirrors
    /// TreeCanopyTrigger so the hazard stays decoupled from Kate.
    /// </summary>
    public class CarImpactTrigger : MonoBehaviour
    {
        public CarController controller;

        private void OnTriggerEnter(Collider other)
        {
            if (controller) controller.NotifyHit(other, transform.position);
        }
    }
}
