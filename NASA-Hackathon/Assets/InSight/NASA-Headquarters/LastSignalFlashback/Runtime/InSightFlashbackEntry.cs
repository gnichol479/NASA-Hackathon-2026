using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace LastSignal.InSightFlashback
{
    /// <summary>
    /// Entry point for the Mars scene's “See how I got here” action. The Mars planet and selection
    /// UI are owned by that scene; this entry method loads only the flashback facility.
    /// </summary>
    public static class InSightFlashbackEntry
    {
        public const string SceneName = "InSight-Flashback";
        public const string SceneAssetPath = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Scenes/InSight-Flashback.unity";

        public static void EnterFlashback()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
                return;
            }
#endif
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        }
    }
}
