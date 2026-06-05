using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Sits on the tree canopy's trigger collider and forwards impact to the
    /// TreeFallController, which decides (by tag) whether it hit the patient.
    /// </summary>
    public class TreeCanopyTrigger : MonoBehaviour
    {
        public TreeFallController controller;

        private void OnTriggerEnter(Collider other)
        {
            if (controller) controller.NotifyHit(other, transform.position);
        }
    }
}
