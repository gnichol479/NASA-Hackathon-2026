using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NasaHack
{
    /// <summary>Reads a mission card when the player's UI ray or mouse points at its button.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class TitleCardNarrator : MonoBehaviour, IPointerEnterHandler
    {
        [TextArea(2, 6)] public string narrationText;
        public AudioClip narrationClip;

        AudioSource source;

        void Awake()
        {
            source = NarrationPlayback.CreateSource(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (narrationClip == null || source == null) return;
            NarrationPlayback.Play(source, narrationClip);
        }
    }
}
