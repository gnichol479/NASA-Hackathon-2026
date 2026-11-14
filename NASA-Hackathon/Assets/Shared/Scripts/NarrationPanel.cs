using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NasaHack
{
    // Shows story beats one at a time, with optional voice lines.
    // A beat with "Wait For Event" hides Next until something calls ContinueIfWaiting().
    public class NarrationPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Beat
        {
            public string title;
            [TextArea(3, 10)] public string text;
            public AudioClip voice;
            [Tooltip("Hide Next. Something else continues the story, e.g. the wake-up command.")]
            public bool waitForEvent;
            [Tooltip("Shown only while this beat is on screen.")]
            public GameObject showWhileActive;
        }

        public Beat[] beats = new Beat[0];
        public string nextLabel = "Next";
        public string finishLabel = "Finish";
        [Tooltip("Hide the button on the last beat instead of showing Finish.")]
        public bool hideButtonOnLastBeat;
        public UnityEvent onFinished = new UnityEvent();

        [Header("Wiring (built by the scene builder)")]
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public Button nextButton;
        public TMP_Text buttonLabel;

        AudioSource voice;
        int index = -1;
        bool finished;

        void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            nextButton.onClick.AddListener(Continue);
            foreach (var beat in beats)
                if (beat.showWhileActive != null) beat.showWhileActive.SetActive(false);
        }

        void Start()
        {
            if (beats.Length > 0) Show(0);
        }

        public void Continue()
        {
            if (finished) return;
            if (index >= beats.Length - 1)
            {
                finished = true;
                nextButton.interactable = false;
                onFinished.Invoke();
                return;
            }
            Show(index + 1);
        }

        public void ContinueIfWaiting()
        {
            if (index >= 0 && beats[index].waitForEvent) Continue();
        }

        void Show(int i)
        {
            if (index >= 0 && beats[index].showWhileActive != null)
                beats[index].showWhileActive.SetActive(false);

            index = i;
            var beat = beats[i];
            titleText.text = beat.title;
            bodyText.text = beat.text;
            if (beat.showWhileActive != null) beat.showWhileActive.SetActive(true);

            bool last = i == beats.Length - 1;
            nextButton.gameObject.SetActive(!beat.waitForEvent && !(last && hideButtonOnLastBeat));
            buttonLabel.text = last ? finishLabel : nextLabel;

            voice.Stop();
            if (beat.voice != null)
            {
                voice.clip = beat.voice;
                voice.Play();
            }
        }
    }
}
