using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack
{
    // Glowing marker on a part of the artifact. Point at it to read about it,
    // click (select) to keep the card open. Edit Title/Body here, not on the card.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class InfoHotspot : MonoBehaviour
    {
        public string title = "Part name";
        [TextArea(3, 8)] public string body = "What it did.";

        [Header("Narration")]
        [Tooltip("Optional offline voice clip. It plays when the player selects this hotspot, not while merely pointing at it.")]
        public AudioClip narrationClip;

        [Header("Wiring (built by the scene builder)")]
        public GameObject card;
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public Transform marker;
        public Vector3 cardOffset = new Vector3(0f, 0.32f, 0f);
        [Tooltip("Size of the clickable area around the marker, in meters.")]
        public float hitRadius = 0.15f;

        bool pinned;
        Vector3 markerScale;
        AudioSource narrationSource;

        void Awake()
        {
            if (card == null) BuildCard(this);
            titleText.text = title;
            bodyText.text = body;
            card.SetActive(false);
            narrationSource = NarrationPlayback.CreateSource(this);
            if (marker != null) markerScale = marker.localScale;
            var hitArea = GetComponent<SphereCollider>();
            if (hitArea != null) hitArea.radius = Mathf.Max(hitArea.radius, hitRadius);

            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.hoverEntered.AddListener(_ => card.SetActive(true));
            interactable.hoverExited.AddListener(_ => card.SetActive(pinned));
            interactable.selectEntered.AddListener(_ =>
            {
                pinned = !pinned;
                card.SetActive(true);
                if (pinned) PlayNarration();
                else narrationSource.Stop();
            });
        }

        // Desktop testing: hover and click with the mouse in the Game view.
        void OnMouseEnter() => card.SetActive(true);
        void OnMouseExit() => card.SetActive(pinned);
        void OnMouseDown()
        {
            pinned = !pinned;
            card.SetActive(true);
            if (narrationSource == null) return;
            if (pinned) PlayNarration();
            else narrationSource.Stop();
        }

        void PlayNarration()
        {
            if (narrationSource == null || narrationClip == null) return;
            NarrationPlayback.Play(narrationSource, narrationClip);
        }

        // Gentle pulse so players notice the markers.
        void Update()
        {
            if (marker == null) return;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 3f);
            marker.localScale = markerScale * pulse;
        }

        public static void BuildCard(InfoHotspot hotspot)
        {
            var panel = UiFactory.CreatePanel("Info Card", hotspot.transform, 560f);
            panel.localPosition = hotspot.cardOffset;
            panel.gameObject.AddComponent<Billboard>();
            hotspot.titleText = UiFactory.AddText(panel, "Title", hotspot.title, 44f, FontStyles.Bold);
            hotspot.titleText.color = UiFactory.Accent;
            hotspot.bodyText = UiFactory.AddText(panel, "Body", hotspot.body, 32f, FontStyles.Normal);
            hotspot.card = panel.gameObject;
        }
    }
}
