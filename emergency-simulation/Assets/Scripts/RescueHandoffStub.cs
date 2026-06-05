using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// STUB handoff target. Listens on the RescueRequested channel and receives the
    /// patient position when the witness finishes the 911 call.
    ///
    /// >>> TEAMMATE MERGE POINT <<<
    /// Replace the body of StartRescue (or replace this whole listener with your own
    /// component subscribing to the same Vector3GameEvent asset). ScenarioDirector never
    /// references this class, so the swap requires no changes to my scene logic.
    /// </summary>
    public class RescueHandoffStub : MonoBehaviour
    {
        [SerializeField] private Vector3GameEvent rescueChannel;

        private void OnEnable()
        {
            if (rescueChannel != null) rescueChannel.OnRaised += StartRescue;
        }

        private void OnDisable()
        {
            if (rescueChannel != null) rescueChannel.OnRaised -= StartRescue;
        }

        public void StartRescue(Vector3 patientPosition)
        {
            // STUB — the reusable rescue sequence (ambulance / CPR / stretcher) plugs in here at merge.
            Debug.Log($"[Handoff] StartRescue({patientPosition:F2}) — witness finished the 911 call. " +
                      "Awaiting teammate's rescue sequence.");
        }
    }
}
