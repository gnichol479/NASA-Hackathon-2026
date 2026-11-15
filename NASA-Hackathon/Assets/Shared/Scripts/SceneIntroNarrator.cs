using UnityEngine;

namespace NasaHack
{
    /// <summary>Plays a short, authored scene introduction when the scene opens.</summary>
    public sealed class SceneIntroNarrator : MonoBehaviour
    {
        [TextArea(2, 6)] public string narrationText;
        public AudioClip narrationClip;

        AudioSource source;

        void Awake()
        {
            source = NarrationPlayback.CreateSource(this);
        }

        void Start()
        {
            if (narrationClip == null || source == null) return;
            NarrationPlayback.Play(source, narrationClip);
        }
    }
}
