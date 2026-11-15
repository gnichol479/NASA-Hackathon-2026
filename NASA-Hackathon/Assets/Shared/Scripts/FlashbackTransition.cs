using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NasaHack
{
    // The "memory" look: warm, faded color, subtle color fringing and glow.
    //   Present scene (Arrival): call Play() to dive into the flashback, then it loads the next scene.
    //   Flashback scenes (Origin, Launch, Landing): Start Mode = In Flashback keeps the look on.
    //   Back in the present (Legacy): Start Mode = Returning fades the look out.
    // Only color/vignette effects, never camera motion, so it stays comfortable in VR.
    public class FlashbackTransition : MonoBehaviour
    {
        public enum StartMode { Present, InFlashback, Returning }

        public StartMode startMode = StartMode.Present;
        [Tooltip("Loaded after Play() finishes. Leave empty to stay in this scene.")]
        public LoadNextScene sceneFlow;
        public float seconds = 1.5f;
        public AudioClip whoosh;
        [Tooltip("Desktop testing shortcut for Play(). Only in Present mode.")]
        public KeyCode playKey = KeyCode.F;

        Volume volume;
        bool playing;

        void Awake()
        {
            volume = CreateVolume(transform);
            volume.weight = startMode == StartMode.Present ? 0f : 1f;
        }

        void Start()
        {
            if (startMode == StartMode.Returning) StartCoroutine(Ramp(0f));
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (startMode == StartMode.Present && Input.GetKeyDown(playKey)) Play();
        }
#endif

        public void Play()
        {
            if (!playing) StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            playing = true;
            var cam = Camera.main;
            if (whoosh != null && cam != null) AudioSource.PlayClipAtPoint(whoosh, cam.transform.position);
            yield return Ramp(1f);
            if (sceneFlow != null) sceneFlow.Load();
        }

        IEnumerator Ramp(float to)
        {
            float from = volume.weight;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                volume.weight = Mathf.SmoothStep(from, to, t / seconds);
                yield return null;
            }
            volume.weight = to;
        }

        static Volume CreateVolume(Transform parent)
        {
            var go = new GameObject("Flashback Volume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Flashback (runtime)";

            var color = profile.Add<ColorAdjustments>();
            color.saturation.Override(-60f);
            color.colorFilter.Override(new Color(1f, 0.86f, 0.68f));
            color.postExposure.Override(0.3f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0f);
            vignette.active = false;

            profile.Add<ChromaticAberration>().intensity.Override(0.6f);

            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(1.2f);
            bloom.threshold.Override(0.8f);

            volume.sharedProfile = profile;
            return volume;
        }
    }
}
