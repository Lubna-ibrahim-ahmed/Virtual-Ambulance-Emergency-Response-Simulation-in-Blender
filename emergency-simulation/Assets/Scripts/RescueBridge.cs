using System.Collections;
using UnityEngine;
using EmergencySim;

/// <summary>
/// Minimal, observation-only bridge from the gas-explosion scenario to the ambulance
/// rescue. It watches the witness's Phone object — which WitnessReaction.CallAmbulance()
/// activates during the 911 call — and, on the moment that object becomes active, raises
/// the shared RescueRequested channel at the patient's position. The RescueSequence prefab
/// listens on that same channel and plays the rescue.
///
/// It does NOT modify or reference any existing Scenario1 script. It only READS the Phone's
/// active state, so the original explosion/fall/witness logic is completely untouched.
/// </summary>
public class RescueBridge : MonoBehaviour
{
    [Tooltip("The witness's phone, activated when the 911 call is placed (WitnessReaction.phoneObject).")]
    public GameObject phoneObject;

    [Tooltip("Shared channel the rescue sequence listens on (RescueRequested.asset).")]
    public Vector3GameEvent rescueChannel;

    [Tooltip("The patient. The rescue is dispatched to this position (Character_Ch21).")]
    public Transform patient;

    [Tooltip("Delay after the phone lights up before the rescue is dispatched, so the call reads naturally.")]
    public float dispatchDelay = 1.0f;

    private bool _raised;
    private bool _wasActive;

    void Start()
    {
        _wasActive = phoneObject != null && phoneObject.activeInHierarchy;
    }

    void Update()
    {
        if (_raised || rescueChannel == null || phoneObject == null) return;

        bool active = phoneObject.activeInHierarchy;
        if (active && !_wasActive)
        {
            _raised = true;
            StartCoroutine(Dispatch());
        }
        _wasActive = active;
    }

    private IEnumerator Dispatch()
    {
        if (dispatchDelay > 0f) yield return new WaitForSeconds(dispatchDelay);

        Vector3 pos = patient != null ? patient.position : transform.position;
        rescueChannel.Raise(pos);
        Debug.Log("[RescueBridge] Phone active -> RescueRequested raised at " + pos);
    }
}
