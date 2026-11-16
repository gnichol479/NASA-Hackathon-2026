using UnityEngine;

namespace NasaHack
{
    /// <summary>Project-wide reversible switch for ElevenLabs-generated narration and requests.</summary>
    [CreateAssetMenu(fileName = "ElevenLabsFeatureSettings", menuName = "Last Signal/ElevenLabs Settings")]
    public sealed class ElevenLabsFeatureSettings : ScriptableObject
    {
        public const string ResourcesAssetPath = "Assets/Resources/LastSignal/ElevenLabsFeatureSettings.asset";
        private const string ResourcesLoadPath = "LastSignal/ElevenLabsFeatureSettings";

        [Tooltip("When disabled, generated narration is muted and Editor generators cannot send new ElevenLabs requests.")]
        public bool enableElevenLabs;

        public static ElevenLabsFeatureSettings Current => Resources.Load<ElevenLabsFeatureSettings>(ResourcesLoadPath);
        public static bool IsEnabled => Current != null && Current.enableElevenLabs;

        private void OnValidate()
        {
            if (!enableElevenLabs)
                NarrationPlayback.StopAll();
        }
    }
}
