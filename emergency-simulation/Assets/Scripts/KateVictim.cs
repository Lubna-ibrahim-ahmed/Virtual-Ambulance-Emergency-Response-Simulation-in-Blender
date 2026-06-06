using System;
using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Kate's hit reaction. Driven by the tree's impact event (never the other way round),
    /// so the tree and Kate stay decoupled. Halts her walk, plays the Fall clip (which the
    /// KateAC holds on its last frame so she stays down), and bursts debris.
    /// </summary>
    public class KateVictim : MonoBehaviour
    {
        public Animator animator;
        public WaypointFollower follower;
        public ParticleSystem debrisBurst;
        public string fallTrigger = "Fall";
        [Tooltip("Seconds for the fall clip to settle before laying her flat on the street.")]
        public float fallSettleTime = 2.2f;
        [Tooltip("Slide her toward the road (+x) so she clears the fallen tree canopy.")]
        public float groundSlideX = 3f;
        [Tooltip("Slide her down the street (-z) so she clears the canopy and lies in the open.")]
        public float groundSlideZ = -2.5f;
        [Tooltip("World Y of her body once down — on the asphalt (ground top is ~-0.02).")]
        public float finalY = 0.06f;
        [Tooltip("Lying orientation: -90 about X = flat on her back (supine), face up.")]
        public Vector3 lieEuler = new Vector3(-90f, 0f, 0f);

        public event Action OnKnockedDown;

        private bool _down;
        private float _droppedBy;
        public bool IsDown => _down;

        public void Knockdown()
        {
            if (_down) return;
            _down = true;
            if (follower) follower.Halt();
            if (animator) animator.SetTrigger(fallTrigger);
            if (debrisBurst) debrisBurst.Play();
            OnKnockedDown?.Invoke();
            StartCoroutine(GroundAfterFall());
        }

        // The retargeted fall clip ends arched and floating, tangled in the tree canopy. Once it
        // settles, move her clear of the canopy and lay her FLAT (supine) on the road surface.
        private IEnumerator GroundAfterFall()
        {
            yield return new WaitForSeconds(fallSettleTime);
            Vector3 p = transform.position;
            p.x += groundSlideX;
            p.z += groundSlideZ;
            p.y = finalY;
            transform.position = p;
            // Relax out of the arched fall pose into a calm flat pose so she lies on the street.
            if (animator && animator.runtimeAnimatorController != null)
            {
                animator.Play("Idle", 0, 0f);
                animator.Update(0f);
            }
            transform.rotation = Quaternion.Euler(lieEuler);
            _droppedBy = 0f;
        }

        public void ResetState()
        {
            // The director repositions Kate to her start absolutely, so just clear flags here.
            _droppedBy = 0f;
            _down = false;
        }
    }
}
