using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Drives the ambulance siren. If <see cref="clip"/> is assigned it loops that; otherwise
    /// it synthesizes a two-tone wail at runtime via AudioClip.Create, so the rescue has an
    /// audible siren with zero imported assets. Drop a real siren .wav into <see cref="clip"/>
    /// later and it is used instead — no code change.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SirenTone : MonoBehaviour
    {
        [Tooltip("Optional real siren clip. If null, a wail is synthesized at runtime.")]
        public AudioClip clip;
        public float volume = 0.5f;
        [Tooltip("Seconds for one full low->high->low wail sweep.")]
        public float wailPeriod = 1.2f;
        public float lowHz = 600f;
        public float highHz = 900f;
        public int sampleRate = 44100;

        private AudioSource _src;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 1f;   // 3D so the siren tracks the ambulance
            _src.volume = volume;
            if (clip == null) clip = BuildWail();
            _src.clip = clip;
        }

        public void Play()
        {
            if (_src != null && !_src.isPlaying) _src.Play();
        }

        public void Stop()
        {
            if (_src != null) _src.Stop();
        }

        // Synthesize one seamless wail period: a sine carrier whose frequency sweeps
        // low->high->low, so looping it yields a continuous two-tone siren. The frequency is
        // equal (low) at both ends of the period to keep the loop seam quiet.
        private AudioClip BuildWail()
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * wailPeriod));
            var data = new float[samples];
            double phase = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;                              // 0..1 over the period
                float sweep = 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI));  // 0->1->0 smooth
                float freq = Mathf.Lerp(lowHz, highHz, sweep);
                phase += 2.0 * Mathf.PI * freq / sampleRate;
                data[i] = 0.6f * Mathf.Sin((float)phase);
            }
            var c = AudioClip.Create("SirenWail", samples, 1, sampleRate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
