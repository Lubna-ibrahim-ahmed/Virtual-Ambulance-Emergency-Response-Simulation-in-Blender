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
        [Tooltip("Optional static blood decal; shown under her once she settles on the ground.")]
        public GameObject bloodDecal;
        public string fallTrigger = "Fall";
        [Tooltip("Seconds for the fall clip to settle into the lying pose before grounding her.")]
        public float fallSettleTime = 2.2f;
        [Tooltip("How far to drop her onto the asphalt (the Stunned end pose floats after retargeting).")]
        public float groundDrop = 0.5f;
        [Tooltip("Slide her clear of the canopy toward the road (-z) so she's visible lying beside the tree, not buried under the foliage.")]
        public float groundSlideZ = -1.6f;

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

        // The retargeted Stunned clip ends ~0.5 m above the street; once it settles, drop her
        // onto the asphalt so she lies on the ground.
        private IEnumerator GroundAfterFall()
        {
            yield return new WaitForSeconds(fallSettleTime);
            transform.position += Vector3.down * groundDrop + Vector3.forward * groundSlideZ;
            _droppedBy = groundDrop;
            // Reveal the static blood decal under her final resting position.
            if (bloodDecal)
            {
                bloodDecal.transform.position = new Vector3(transform.position.x, 0.01f, transform.position.z);
                bloodDecal.SetActive(true);
            }
        }

        public void ResetState()
        {
            // The director repositions Kate to her start absolutely, so just clear flags here.
            _droppedBy = 0f;
            _down = false;
            if (bloodDecal) bloodDecal.SetActive(false);
        }
    }
}
