using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NasaHack
{
    // Ingenuity Landing: Perseverance drives away from the helicopter it just dropped,
    // then the player flies the first flight (April 19, 2021): spin up, rise ~3 m, hover, land.
    public class IngenuityFirstFlight : MonoBehaviour
    {
        public NarrationPanel narration;
        public Transform helicopter;
        public RotorSpinner rotors;
        public Transform rover;
        [Tooltip("Where Perseverance drives to after dropping the helicopter (world offset).")]
        public Vector3 roverDriveOffset = new Vector3(5f, 0f, 0f);
        public float roverDriveSeconds = 5f;
        [Tooltip("Hover height in meters. The real first flight climbed to about 3 m.")]
        public float hoverHeight = 3f;
        [Tooltip("Hover time, compressed from the real 39-second flight.")]
        public float hoverSeconds = 6f;
        [Tooltip("Desktop testing shortcut for the button.")]
        public KeyCode sendKey = KeyCode.U;

        [Header("Wiring (built by the scene builder)")]
        public Button button;
        public TMP_Text buttonLabel;
        public TMP_Text status;

        bool busy;
        bool flown;

        void Awake()
        {
            button.onClick.AddListener(Fly);
            status.text = "Ingenuity is on the ground. Perseverance must drive away first, so its shade doesn't drain the solar panel.";
        }

        IEnumerator Start()
        {
            button.interactable = false;
            if (rover != null)
            {
                var from = rover.position;
                var to = from + roverDriveOffset;
                for (float t = 0f; t < roverDriveSeconds; t += Time.deltaTime)
                {
                    rover.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / roverDriveSeconds));
                    yield return null;
                }
                rover.position = to;
            }
            status.text = "Perseverance has backed away. Ingenuity is ready for its first flight.";
            button.interactable = true;
            if (narration != null) narration.ContinueIfWaiting();
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (Input.GetKeyDown(sendKey) && button.interactable) Fly();
        }
#endif

        public void Fly()
        {
            if (!busy) StartCoroutine(FlyRoutine());
        }

        IEnumerator FlyRoutine()
        {
            busy = true;
            button.interactable = false;
            var ground = helicopter.position;

            status.text = "Spinning up to about 2,400 rpm...";
            if (rotors != null) rotors.SetTarget(1f);
            yield return new WaitForSeconds(3f);

            status.text = "Lift-off!";
            yield return Move(ground, ground + Vector3.up * hoverHeight, 3f);

            status.text = "Hovering 3 m above Mars: the first powered, controlled flight on another planet.";
            for (float t = 0f; t < hoverSeconds; t += Time.deltaTime)
            {
                helicopter.position = ground + Vector3.up * (hoverHeight + 0.06f * Mathf.Sin(t * 2.2f));
                yield return null;
            }

            status.text = "Descending...";
            yield return Move(helicopter.position, ground, 3f);
            if (rotors != null) rotors.SetTarget(0f);
            yield return new WaitForSeconds(1.5f);

            status.text = "Touchdown. The real flight lasted 39 seconds. You can fly it again.";
            buttonLabel.text = "Fly again";
            button.interactable = true;
            busy = false;

            if (!flown && narration != null)
            {
                flown = true;
                yield return new WaitForSeconds(1.5f);
                narration.ContinueIfWaiting();
            }
        }

        IEnumerator Move(Vector3 from, Vector3 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                helicopter.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            helicopter.position = to;
        }
    }
}
