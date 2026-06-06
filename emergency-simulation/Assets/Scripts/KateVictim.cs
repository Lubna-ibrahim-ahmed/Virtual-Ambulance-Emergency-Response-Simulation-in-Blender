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
        [Tooltip("Seconds for the fall clip to play before she settles into the supine lying pose.")]
        public float fallSettleTime = 2.2f;
        [Tooltip("(legacy, unused) old fixed drop — grounding is now rig-aware via RestOnGround().")]
        public float groundDrop = 0.5f;
        [Tooltip("Slide her clear of the canopy toward the road (-z) so she's visible lying beside the tree, not buried under the foliage.")]
        public float groundSlideZ = -1.6f;

        [Header("Lying pose")]
        [Tooltip("Animator state for the relaxed lying pose (Idle clip); the transform is rotated supine on top.")]
        public string lieState = "Lie";
        [Tooltip("Pitch (deg about X) that lays her face-up supine. -90 = flat on her back.")]
        public float liePitch = -90f;
        [Tooltip("World Y of the ground her body rests on.")]
        public float groundY = 0f;
        [Tooltip("Seconds to smoothly tip from the fall pose into the supine lying pose.")]
        public float settleLerp = 0.45f;

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
            if (debrisBurst) { debrisBurst.Play(); StartCoroutine(StopDebrisOnLift()); }
            OnKnockedDown?.Invoke();
            StartCoroutine(GroundAfterFall());
        }

        // The debris/blood burst emits while she's on the ground, then STOPS once the rescue lifts
        // her onto the stretcher (the wound area is vacated) — so nothing is still animating after the
        // ambulance departs. Looping particle systems otherwise run forever. Reusable: if the scene
        // has no rescue, it simply leaves the burst as authored.
        private IEnumerator StopDebrisOnLift()
        {
            var rescue = UnityEngine.Object.FindFirstObjectByType<RescueSequenceController>();
            if (rescue == null) yield break;
            while (true)
            {
                string beat = rescue.CurrentBeat;
                if (beat == "lift" || beat == "load" || beat == "depart" || beat == "done") break;
                yield return null;
            }
            if (debrisBurst) debrisBurst.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Debug.Log("[KateVictim] Debris/blood burst stopped at rescue lift.");
        }

        // After the fall clip, settle her from the frozen fall pose into a relaxed supine
        // lying pose (matches the rescue's bed approach): play the relaxed Lie/Idle pose,
        // tip her face-up, and rest her ON the ground (rig-aware — no more fixed drop that
        // buried or floated her). She holds this pose through the witness call and CPR approach.
        private IEnumerator GroundAfterFall()
        {
            yield return new WaitForSeconds(fallSettleTime);

            if (animator) animator.CrossFadeInFixedTime(lieState, 0.3f);
            transform.position += Vector3.forward * groundSlideZ;   // clear the canopy/debris (per-scene)

            float yaw = transform.eulerAngles.y;
            Quaternion from = transform.rotation;
            Quaternion to = Quaternion.Euler(liePitch, yaw, 0f);     // face-up supine, heading preserved
            float t = 0f;
            float dur = Mathf.Max(0.01f, settleLerp);
            while (t < dur)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(from, to, t / dur);
                if (animator) animator.Update(0f);
                RestOnGround();
                yield return null;
            }
            transform.rotation = to;
            RestOnGround();

            // Reveal the static blood decal under her final resting position.
            if (bloodDecal)
            {
                bloodDecal.transform.position = new Vector3(transform.position.x, groundY + 0.01f, transform.position.z);
                bloodDecal.SetActive(true);
            }
        }

        // Rest her lowest rendered point on the ground (rig-aware), so the supine pose lies flat
        // on the asphalt regardless of where the retargeted skeleton sits relative to the pivot.
        private void RestOnGround()
        {
            var rends = GetComponentsInChildren<Renderer>();
            bool first = true; Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in rends)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            if (!first) transform.position += Vector3.up * (groundY + 0.03f - b.min.y);
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
