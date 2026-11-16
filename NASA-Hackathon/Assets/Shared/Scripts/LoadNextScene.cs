using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NasaHack
{
    // The one hook every part uses: when your part is done, call Load().
    // Hook it to an XR Simple Interactable's "Select Entered" event, a UI Button's
    // OnClick, or set Auto Load After to move on after a video or narration.
    public class LoadNextScene : MonoBehaviour
    {
        [Tooltip("Exact scene name, e.g. Opportunity-Launch. Must be added in File > Build Settings.")]
        public string nextScene;

        public float fadeSeconds = 0.75f;

        [Tooltip("Load automatically after this many seconds. 0 = only when Load() is called.")]
        public float autoLoadAfter = 0f;

        [Tooltip("Legacy desktop shortcut. Any keyboard key also skips scenes with one unambiguous next destination.")]
        public KeyCode debugKey = KeyCode.N;

        [Tooltip("Interactive scenes can reserve gameplay keys and use only the debug key to skip.")]
        public bool requireDebugKeyForDemoSkip;

        static bool fadeInPending;
        static float fadeInSeconds;
        static int lastKeyboardSkipFrame = -1;
        bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetKeyboardSkip()
        {
            lastKeyboardSkipFrame = -1;
        }

        void Start()
        {
            if (autoLoadAfter > 0f) Invoke(nameof(Load), autoLoadAfter);
        }

        void Update()
        {
            if (FindObjectOfType<DeltaIILaunch>() != null)
                return;

            if (DemoSkipInput.WasKeyPressedThisFrame(debugKey))
            {
                if (lastKeyboardSkipFrame == Time.frameCount) return;
                lastKeyboardSkipFrame = Time.frameCount;
                LoadSelectedOrSingleDestination();
                return;
            }

            if (requireDebugKeyForDemoSkip || !DemoSkipInput.WasAnyKeyboardKeyPressedThisFrame() ||
                lastKeyboardSkipFrame == Time.frameCount)
                return;

            lastKeyboardSkipFrame = Time.frameCount;
            LoadSelectedOrSingleDestination();
        }

        void LoadSelectedOrSingleDestination()
        {
            LoadNextScene[] flows = FindObjectsOfType<LoadNextScene>();
            var destinations = new HashSet<string>(
                flows.Where(flow => flow != null && !string.IsNullOrWhiteSpace(flow.nextScene))
                    .Select(flow => flow.nextScene));

            if (destinations.Count == 0) return;
            if (destinations.Count > 1)
            {
                // On a multi-route menu, advance the currently selected option if there is one.
                GameObject selected = EventSystem.current != null
                    ? EventSystem.current.currentSelectedGameObject : null;
                Button selectedButton = selected != null ? selected.GetComponentInParent<Button>() : null;
                if (selectedButton != null && selectedButton.interactable)
                    selectedButton.onClick.Invoke();
                return;
            }

            string target = destinations.First();
            LoadNextScene flow = flows.FirstOrDefault(candidate => candidate != null && candidate.nextScene == target);
            if (flow != null) flow.Load();
        }

        public void Load()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError($"LoadNextScene: '{nextScene}' not found. Check the name and add it to File > Build Settings.", this);
                return;
            }
            loading = true;
            StartCoroutine(FadeAndLoad());
        }

        IEnumerator FadeAndLoad()
        {
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.Fade(1f, fadeSeconds);
            fadeInPending = true;
            fadeInSeconds = fadeSeconds;
            SceneManager.LoadScene(nextScene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // New scene starts black and fades in.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!fadeInPending) return;
            fadeInPending = false;
            var fader = ScreenFader.Instance;
            if (fader == null) return;
            fader.SetAlpha(1f);
            fader.StartCoroutine(fader.Fade(0f, fadeInSeconds));
        }
    }
}
