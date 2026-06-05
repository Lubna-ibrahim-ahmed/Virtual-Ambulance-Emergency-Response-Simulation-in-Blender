using System.Collections;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Storm flourish: spikes the moonlight to fake a lightning flash (a quick double-strike)
    /// and plays thunder a beat later. The director fires TriggerLightning() just before the
    /// tree topples so the storm visibly "causes" the fall.
    /// </summary>
    public class StormController : MonoBehaviour
    {
        public Light moonLight;
        public AudioSource thunderSource;
        [Tooltip("Normal moonlight intensity to return to between/after flashes.")]
        public float baseIntensity = 0.45f;
        [Tooltip("Peak intensity during a lightning strike.")]
        public float flashIntensity = 2.6f;
        public Color flashColor = new Color(0.8f, 0.85f, 1f);

        private Color _baseColor;
        private bool _flashing;

        private void Awake()
        {
            if (moonLight) { _baseColor = moonLight.color; baseIntensity = moonLight.intensity; }
        }

        public void TriggerLightning()
        {
            if (!_flashing) StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _flashing = true;
            if (moonLight) moonLight.color = flashColor;

            // Quick flicker: bright, dim, bright again.
            yield return Strike(flashIntensity, 0.06f);
            yield return Strike(baseIntensity * 0.4f, 0.05f);
            yield return Strike(flashIntensity * 0.75f, 0.05f);
            yield return Strike(baseIntensity, 0.12f);

            if (moonLight) { moonLight.color = _baseColor; moonLight.intensity = baseIntensity; }

            // Thunder lags the flash slightly, like distance.
            yield return new WaitForSeconds(0.35f);
            if (thunderSource) thunderSource.Play();
            _flashing = false;
        }

        private IEnumerator Strike(float intensity, float hold)
        {
            if (moonLight) moonLight.intensity = intensity;
            yield return new WaitForSeconds(hold);
        }
    }
}
