using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NasaHack
{
    // Legacy interaction: the player tries to wake the rover, like the real team did
    // for eight months after the 2018 dust storm. It never answers.
    public class WakeUpCommand : MonoBehaviour
    {
        [TextArea(2, 4)] public string idleText = "Opportunity has been silent since June 10, 2018.";
        public string realCommandsNote = "The real team sent more than a thousand.";
        [Tooltip("Continues the story after the first 'No response'.")]
        public NarrationPanel narration;
        public AudioClip transmitSound;

        [Header("Wiring (built by the scene builder)")]
        public Button button;
        public TMP_Text buttonLabel;
        public TMP_Text status;

        int sent;
        bool busy;
        bool reported;

        void Awake()
        {
            button.onClick.AddListener(Send);
            status.text = idleText;
        }

        public void Send()
        {
            if (!busy) StartCoroutine(SendRoutine());
        }

        IEnumerator SendRoutine()
        {
            busy = true;
            button.interactable = false;
            sent++;

            var cam = Camera.main;
            if (transmitSound != null && cam != null) AudioSource.PlayClipAtPoint(transmitSound, cam.transform.position);

            status.text = "Transmitting wake-up command from Earth...";
            yield return new WaitForSeconds(2f);
            status.text = "Signal traveling to Mars. A real signal takes about 3 to 22 minutes, one way. We'll speed it up.";
            yield return new WaitForSeconds(4f);
            for (int i = 0; i < 8; i++)
            {
                status.text = "Listening for a reply" + new string('.', i % 4);
                yield return new WaitForSeconds(0.5f);
            }

            status.text = sent == 1
                ? "No response."
                : $"No response. Commands sent: {sent}. {realCommandsNote}";
            buttonLabel.text = "Try again";
            button.interactable = true;
            busy = false;

            if (!reported && narration != null)
            {
                reported = true;
                yield return new WaitForSeconds(2.5f);
                narration.ContinueIfWaiting();
            }
        }
    }
}
