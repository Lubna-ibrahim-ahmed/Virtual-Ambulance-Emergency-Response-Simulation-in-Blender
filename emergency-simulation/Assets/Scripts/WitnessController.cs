using System;
using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// The background character nearest the tree. On cue: stop, play Shock, then PhoneCall
    /// (calling 911, with a phone prop in hand). The call plays ONCE; when it finishes the
    /// witness lowers the phone, turns to Idle facing Kate, and OnPhoneCallFinished fires
    /// exactly once (handoff). No looping, no walking-in-place at the end.
    /// </summary>
    public class WitnessController : MonoBehaviour
    {
        public Animator animator;
        public WaypointFollower follower;
        public GameObject phoneProp;        // small cuboid parented to the right hand; shown only during the call
        public Transform lookAtTarget;      // Kate — the witness faces her after the call
        public string shockTrigger = "Shock";
        public string phoneTrigger = "PhoneCall";
        [Tooltip("Seconds of shock reaction before raising the phone.")]
        public float shockDuration = 2.5f;
        [Tooltip("Seconds on the phone before the 911 call is considered complete.")]
        public float callDuration = 6f;

        public event Action OnPhoneCallFinished;

        private bool _fired;

        private void Awake()
        {
            if (phoneProp) phoneProp.SetActive(false);
        }

        public void ReactAndCall()
        {
            _fired = false;
            StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            if (follower) follower.Halt();
            if (animator) animator.SetTrigger(shockTrigger);
            yield return new WaitForSeconds(shockDuration);

            if (animator) animator.SetTrigger(phoneTrigger);
            if (phoneProp) phoneProp.SetActive(true);
            yield return new WaitForSeconds(callDuration);

            FinishCall(); // raises the handoff exactly once

            // Calm ending: lower the phone, face Kate, settle into Idle (not walking in place).
            if (phoneProp) phoneProp.SetActive(false);
            FaceTarget();
            if (animator) animator.CrossFadeInFixedTime("Idle", 0.4f);
        }

        // Optional: hook this as an Animation Event on the last frame of the phone clip.
        public void PhoneCallAnimationEnded() => FinishCall();

        private void FinishCall()
        {
            if (_fired) return;   // once-only guard — handoff must fire a single time
            _fired = true;
            OnPhoneCallFinished?.Invoke();
        }

        private void FaceTarget()
        {
            if (!lookAtTarget) return;
            Vector3 d = lookAtTarget.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
