using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NasaHack
{
    /// <summary>Disables black locomotion tunnel masks and full-screen vignettes in every scene.</summary>
    public static class GlobalVREffectCleanup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.IndexOf("TunnelingVignette", StringComparison.OrdinalIgnoreCase) >= 0)
                        child.gameObject.SetActive(false);
                }

                foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
                {
                    if (volume == null || volume.sharedProfile == null)
                        continue;

                    VolumeProfile runtimeProfile = volume.profile;
                    if (runtimeProfile != null && runtimeProfile.TryGet(out Vignette vignette))
                    {
                        vignette.intensity.Override(0f);
                        vignette.active = false;
                    }
                }
            }
        }
    }
}
