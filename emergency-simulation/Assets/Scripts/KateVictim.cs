using System;
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

        public event Action OnKnockedDown;

        private bool _down;
        public bool IsDown => _down;

        public void Knockdown()
        {
            if (_down) return;
            _down = true;
            if (follower) follower.Halt();
            if (animator) animator.SetTrigger(fallTrigger);
            if (debrisBurst) debrisBurst.Play();
            OnKnockedDown?.Invoke();
        }

        public void ResetState()
        {
            _down = false;
        }
    }
}
