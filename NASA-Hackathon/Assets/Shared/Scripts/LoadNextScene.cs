using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Tooltip("Desktop testing shortcut. Press in Play mode to skip to the next scene.")]
        public KeyCode debugKey = KeyCode.N;

        static bool fadeInPending;
        static float fadeInSeconds;
        bool loading;

        void Start()
        {
            if (autoLoadAfter > 0f) Invoke(nameof(Load), autoLoadAfter);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (Input.GetKeyDown(debugKey)) Load();
        }
#endif

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
