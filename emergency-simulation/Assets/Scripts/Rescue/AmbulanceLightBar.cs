using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Pulses the ambulance's own red/blue point lights (and optional emissive renderers) in
    /// antiphase to read as an emergency light bar. Deliberately small light Range so it stays
    /// a LOCAL glow on the vehicle — never scene-wide lighting (scenes own their lighting).
    /// </summary>
    public class AmbulanceLightBar : MonoBehaviour
    {
        public Light redLight;
        public Light blueLight;
        public Renderer redEmissive;
        public Renderer blueEmissive;
        [Tooltip("Flashes per second per colour.")]
        public float flashHz = 2f;
        public float maxIntensity = 3f;

        private bool _on;
        private float _t;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake() { _mpb = new MaterialPropertyBlock(); SetAll(0f, 0f); }

        public void On() { _on = true; }

        public void Off() { _on = false; _t = 0f; SetAll(0f, 0f); }

        private void Update()
        {
            if (!_on) return;
            _t += Time.deltaTime * flashHz * Mathf.PI * 2f;
            float r = Mathf.Clamp01(Mathf.Sin(_t));
            float b = Mathf.Clamp01(Mathf.Sin(_t + Mathf.PI));   // antiphase
            SetAll(r, b);
        }

        private void SetAll(float r, float b)
        {
            if (redLight) redLight.intensity = r * maxIntensity;
            if (blueLight) blueLight.intensity = b * maxIntensity;
            SetEmissive(redEmissive, Color.red * r);
            SetEmissive(blueEmissive, new Color(0.2f, 0.4f, 1f) * b);
        }

        private void SetEmissive(Renderer rend, Color c)
        {
            if (rend == null || _mpb == null) return;
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColor, c);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
