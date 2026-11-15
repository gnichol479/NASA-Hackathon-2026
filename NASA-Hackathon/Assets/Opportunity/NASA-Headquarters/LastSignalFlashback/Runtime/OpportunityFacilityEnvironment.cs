using UnityEngine;

namespace LastSignal.OpportunityFlashback
{
    /// <summary>The saved room is also the playable room. Only the edit-mode assembly mockup is hidden.</summary>
    public sealed class OpportunityFacilityEnvironment : MonoBehaviour
    {
        [SerializeField] private int layoutVersion;
        [SerializeField] private Material[] palette;
        [SerializeField] private GameObject assemblyPreview;

        public int LayoutVersion => layoutVersion;
        public Material[] Palette => palette;
        public GameObject AssemblyPreview => assemblyPreview;

        public void Initialize(Material[] materials)
        {
            palette = materials;
            layoutVersion = OpportunityFacilityLayout.Version;
        }

        public void SetAssemblyPreview(GameObject preview) => assemblyPreview = preview;

        public void HideAssemblyPreview()
        {
            if (assemblyPreview != null)
                assemblyPreview.SetActive(false);
        }
    }
}
