using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NasaHack
{
    // Ingenuity Legacy interaction: the player tries to fly the helicopter.
    // The rotors spin up, the damaged blade wobbles, and it never lifts off.
    public class RotorSpinUpAttempt : MonoBehaviour
    {
        [TextArea(2, 4)] public string idleText = "Ingenuity has been grounded since its 72nd flight on January 18, 2024.";
        [Tooltip("Continues the story after the first failed attempt.")]
        public NarrationPanel narration;
        public RotorSpinner rotors;
        [Tooltip("Desktop testing shortcut for the button.")]
        public KeyCode sendKey = KeyCode.U;

        [Header("Wiring (built by the scene builder)")]
        public Button button;
        public TMP_Text buttonLabel;
        public TMP_Text status;

        int tries;
        bool busy;
        bool reported;

        void Awake()
        {
            button.onClick.AddListener(Try);
            status.text = idleText;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (Input.GetKeyDown(sendKey) && button.interactable) Try();
        }
#endif

        public void Try()
        {
            if (!busy) StartCoroutine(TryRoutine());
        }

        IEnumerator TryRoutine()
        {
            busy = true;
            button.interactable = false;
            tries++;

            status.text = "Arming motors...";
            yield return new WaitForSeconds(1.2f);

            status.text = "Spinning up. In air about 1% as dense as Earth's, the blades need about 2,400 rpm to lift off.";
            if (rotors != null) rotors.SetTarget(1f);
            yield return new WaitForSeconds(3.2f);

            status.text = "Vibration detected: a damaged rotor blade.";
            if (rotors != null) rotors.wobbleDegrees = 5f;
            yield return new WaitForSeconds(2.5f);

            status.text = "Lift-off aborted.";
            if (rotors != null) rotors.SetTarget(0f);
            yield return new WaitForSeconds(2.5f);
            if (rotors != null) rotors.wobbleDegrees = 0f;

            status.text = tries == 1 ? "Ingenuity stays grounded." : $"Attempts: {tries}. Ingenuity stays grounded.";
            buttonLabel.text = "Try again";
            button.interactable = true;
            busy = false;

            if (!reported && narration != null)
            {
                reported = true;
                yield return new WaitForSeconds(2f);
                narration.ContinueIfWaiting();
            }
        }
    }
}
