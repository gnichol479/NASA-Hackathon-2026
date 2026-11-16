using UnityEngine;

namespace NasaHack
{
    /// <summary>One intelligible narration at a time, independent of world distance and effects filters.</summary>
    public static class NarrationPlayback
    {
        static AudioSource current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => current = null;

        public static AudioSource CreateSource(Component owner)
        {
            var child = new GameObject("Narration Audio");
            child.transform.SetParent(owner.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 32;
            return source;
        }

        public static void Play(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            if (!ElevenLabsFeatureSettings.IsEnabled)
            {
                StopAll();
                source.Stop();
                return;
            }
            if (current != null && current != source) current.Stop();
            current = source;
            source.Stop();
            source.clip = clip;
            source.Play();
        }

        public static void StopAll()
        {
            if (current != null)
                current.Stop();
            current = null;
        }
    }
}
