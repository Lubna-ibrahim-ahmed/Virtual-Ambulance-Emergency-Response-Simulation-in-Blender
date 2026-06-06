using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Tier-1 stand-in for paramedic kneel/CPR when no Mixamo clips are present. Leans the body
    /// FORWARD over the patient (pivoting at the feet, so nothing sinks below the road) and adds a
    /// small compression lean to read as chest compressions. A pure transform effect layered over
    /// the Idle pose — NOT a looped clip (the project once hung on a looped clip). No-op when
    /// <see cref="useProceduralCrouch"/> is false, so Tier-2 clips drive the pose with no code change.
    /// </summary>
    public class ProceduralMedic : MonoBehaviour
    {
        [Tooltip("The body to pitch (the character root, whose pivot is at the feet).")]
        public Transform body;
        public bool useProceduralCrouch = true;
        [Tooltip("Kept at 0 so the feet stay planted ON the road — never sink below it.")]
        public float crouchDrop = 0f;
        [Tooltip("Forward lean over the patient (pivots at the feet, so nothing drops underground).")]
        public float crouchPitchDeg = 46f;
        public float crouchTime = 0.45f;
        [Tooltip("Extra forward lean at the bottom of each chest compression.")]
        public float cprLeanDeg = 7f;
        public float cprBobHz = 2f;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private bool _captured;

        private void Capture()
        {
            if (_captured || body == null) return;
            _baseLocalPos = body.localPosition;
            _baseLocalRot = body.localRotation;
            _captured = true;
        }

        /// <summary>Lean over the patient, pump compressions for cprSeconds, then straighten up.</summary>
        public IEnumerator CrouchAndCPR(float cprSeconds)
        {
            if (!useProceduralCrouch || body == null) { yield return new WaitForSeconds(cprSeconds); yield break; }
            Capture();
            yield return Lerp(0f, 1f, crouchTime);          // lean in over the patient
            float t = 0f;
            while (t < cprSeconds)                           // compressions = small extra lean
            {
                t += Time.deltaTime;
                float comp = 0.5f * (1f - Mathf.Cos(t * cprBobHz * Mathf.PI * 2f)); // 0..1..0
                ApplyPose(1f, comp);
                yield return null;
            }
            yield return Lerp(1f, 0f, crouchTime);          // straighten back up
            ApplyPose(0f, 0f);
        }

        private IEnumerator Lerp(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                ApplyPose(Mathf.Lerp(from, to, k), 0f);
                yield return null;
            }
            ApplyPose(to, 0f);
        }

        // k = lean amount (0..1); comp = compression phase (0..1). Feet stay planted: the only
        // vertical move is the optional crouchDrop (kept 0), so nothing goes under the road.
        private void ApplyPose(float k, float comp)
        {
            if (body == null) return;
            body.localPosition = _baseLocalPos + Vector3.down * (crouchDrop * k);
            float pitch = crouchPitchDeg * k + cprLeanDeg * comp;
            body.localRotation = _baseLocalRot * Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
