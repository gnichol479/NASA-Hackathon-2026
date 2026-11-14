using UnityEngine;

namespace NasaHack
{
    // Generates a low rocket roar (filtered brown noise), so no audio file is needed.
    // Set 'target' from 0 (silent) to 1 (full roar); volume eases toward it.
    [RequireComponent(typeof(AudioSource))]
    public class RocketRumble : MonoBehaviour
    {
        [Range(0f, 1f)] public float target;
        public float maxVolume = 0.8f;
        public float easeSeconds = 1.5f;

        volatile float level;
        float current;
        float brown;
        float lowpass;
        System.Random rng = new System.Random();

        void Awake()
        {
            var src = GetComponent<AudioSource>();
            src.playOnAwake = true;
            src.loop = true;
            src.spatialBlend = 0f;
            if (!src.isPlaying) src.Play();
        }

        void Update()
        {
            current = Mathf.MoveTowards(current, target, Time.deltaTime / Mathf.Max(0.01f, easeSeconds));
            level = current * maxVolume;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float lv = level;
            for (int i = 0; i < data.Length; i += channels)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
                lowpass += (brown - lowpass) * 0.08f;
                float crackle = white * 0.06f * lv; // a little grit on top of the roar
                float s = (lowpass * 3.5f + crackle) * lv;
                for (int c = 0; c < channels; c++) data[i + c] = s;
            }
        }
    }
}
