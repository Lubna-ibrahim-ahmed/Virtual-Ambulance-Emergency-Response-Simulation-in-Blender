using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Lightweight cinematic camera (no Cinemachine). Holds a list of shot marker
    /// transforms and Snaps (hard cut) or Blends (eased move) a single Camera to them.
    /// Called from ScenarioDirector's coroutine so cuts land exactly on story beats.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public Camera cam;
        public Transform[] shots;

        public void Snap(int i)
        {
            if (cam == null || shots == null || i < 0 || i >= shots.Length || shots[i] == null) return;
            cam.transform.SetPositionAndRotation(shots[i].position, shots[i].rotation);
        }

        public IEnumerator Blend(int i, float duration)
        {
            if (cam == null || shots == null || i < 0 || i >= shots.Length || shots[i] == null) yield break;
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            Vector3 p1 = shots[i].position;
            Quaternion r1 = shots[i].rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            cam.transform.SetPositionAndRotation(p1, r1);
        }
    }
}
