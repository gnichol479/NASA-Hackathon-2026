#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NasaHack.EditorTools
{
    public static class ElevenLabsFeatureSettingsMenu
    {
        private const string MenuPath = "Last Signal/ElevenLabs/Toggle Narration and Generation";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            ElevenLabsFeatureSettings settings = GetOrCreateSettings();
            Undo.RecordObject(settings, "Toggle ElevenLabs");
            settings.enableElevenLabs = !settings.enableElevenLabs;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            if (!settings.enableElevenLabs)
                NarrationPlayback.StopAll();

            Debug.Log("ElevenLabs narration and generation are now " +
                (settings.enableElevenLabs ? "enabled." : "disabled."));
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, ElevenLabsFeatureSettings.IsEnabled);
            return true;
        }

        private static ElevenLabsFeatureSettings GetOrCreateSettings()
        {
            ElevenLabsFeatureSettings settings = ElevenLabsFeatureSettings.Current;
            if (settings != null)
                return settings;

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/LastSignal");
            settings = ScriptableObject.CreateInstance<ElevenLabsFeatureSettings>();
            settings.enableElevenLabs = false;
            AssetDatabase.CreateAsset(settings, ElevenLabsFeatureSettings.ResourcesAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            int separator = folder.LastIndexOf('/');
            string parent = folder.Substring(0, separator);
            string name = folder.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
