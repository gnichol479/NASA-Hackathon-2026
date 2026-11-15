using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastSignal.InSightMission
{
    /// <summary>Disables the XRI tunnel mask and post-process vignette for InSight VR scenes only.</summary>
    [DisallowMultipleComponent]
    public sealed class InSightVREffectCleanup : MonoBehaviour
    {
        void Awake() => ApplyToScene(gameObject.scene);

        public static void ApplyToScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.IndexOf("TunnelingVignette", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        child.gameObject.SetActive(false);
                }

                foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
                {
                    if (volume == null || volume.sharedProfile == null) continue;
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
