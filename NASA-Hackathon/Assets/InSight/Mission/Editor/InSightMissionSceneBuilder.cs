#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using LastSignal.InSightFlashback;
using LastSignal.InSightMission;
using NasaHack;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastSignal.InSightMission.EditorTools
{
    public static class InSightMissionSceneBuilder
    {
        const string MissionRoot = "Assets/InSight/Mission";
        const string LandingScenePath = MissionRoot + "/Scenes/InSight-Landing.unity";
        const string ScienceScenePath = MissionRoot + "/Scenes/InSight-Arrival.unity";
        const string LegacyScenePath = MissionRoot + "/Scenes/InSight-Legacy.unity";
        const string FlashbackScenePath = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Scenes/InSight-Flashback.unity";
        const string LaunchScenePath = "Assets/InSight/Launch/Scenes/InSight-Launch.unity";
        const string TitleScenePath = "Assets/Shared/Scenes/TitleScreen-Night.unity";
        const string RigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
        const string LanderPath = "Assets/InSight/Models/InSight-Lander.glb";
        const string GroundPath = "Assets/Opportunity/Landing/Textures/Mars_Soil.mat";
        const string OpportunityTerrainPath = "Assets/Shared/Mars/PerseveranceValley.asset";
        const string OpportunityFarTerrainPath = "Assets/Shared/Mars/EndeavourFar.asset";
        const string SkyPath = "Assets/Shared/Settings/Mars Sky.mat";
        const string BluePath = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Materials/FacilityPreview/MissionBlue.mat";
        const string OrangePath = "Assets/InSight/NASA-Headquarters/LastSignalFlashback/Materials/FacilityPreview/SafetyOrange.mat";

        [MenuItem("Last Signal/InSight/Build Mission Flow Scenes")]
        public static void BuildMissionFlow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("InSight mission", "Stop Play mode before rebuilding InSight scenes.", "OK");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if ((File.Exists(Path.GetFullPath(LandingScenePath)) || File.Exists(Path.GetFullPath(ScienceScenePath)) || File.Exists(Path.GetFullPath(LegacyScenePath))) &&
                !EditorUtility.DisplayDialog("Rebuild InSight mission scenes?",
                    "This replaces the generated InSight Landing, Arrival, and Legacy scenes. Their scripts and source models are not modified.",
                    "Rebuild", "Cancel"))
                return;

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            GameObject landerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanderPath);
            Material ground = AssetDatabase.LoadAssetAtPath<Material>(GroundPath);
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            Material blue = AssetDatabase.LoadAssetAtPath<Material>(BluePath);
            Material orange = AssetDatabase.LoadAssetAtPath<Material>(OrangePath);
            TerrainData opportunityTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(OpportunityTerrainPath);
            TerrainData opportunityFarTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(OpportunityFarTerrainPath);

            if (rigPrefab == null || landerPrefab == null || ground == null || sky == null ||
                opportunityTerrain == null || opportunityFarTerrain == null)
            {
                EditorUtility.DisplayDialog("InSight mission", "A required XR, InSight model, or shared Opportunity Mars terrain asset could not be loaded. See the Unity Console.", "OK");
                Debug.LogError("InSight mission builder asset lookup failed. Check the XR Rig, InSight-Lander.glb, Mars_Soil, Mars Sky, PerseveranceValley, and EndeavourFar asset paths.");
                return;
            }

            BuildScene(LandingScenePath, InSightChapter.Landing, "InSight-Arrival", rigPrefab, landerPrefab, ground, sky, blue, orange, opportunityTerrain, opportunityFarTerrain, LandingCues());
            BuildScene(ScienceScenePath, InSightChapter.Science, "InSight-Legacy", rigPrefab, landerPrefab, ground, sky, blue, orange, opportunityTerrain, opportunityFarTerrain, ScienceCues());
            BuildScene(LegacyScenePath, InSightChapter.Legacy, "TitleScreen-Night", rigPrefab, landerPrefab, ground, sky, blue, orange, opportunityTerrain, opportunityFarTerrain, LegacyCues());

            UpdateLaunchHandoff();
            UpdateFlashbackScript();
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrWhiteSpace(originalScenePath) && File.Exists(Path.GetFullPath(originalScenePath)))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.OpenScene(LandingScenePath, OpenSceneMode.Single);

            Debug.Log("InSight flow built: Flashback → Launch → Landing → Arrival → Legacy → mission select. The Opportunity Mars terrain assets are reused read-only.");
            EditorUtility.DisplayDialog("InSight mission flow ready",
                "Built and connected InSight-Landing, InSight-Arrival, and InSight-Legacy. The existing Flashback and Launch handoffs now feed the complete flow.\n\nNext: Last Signal → InSight → Narration → Generate Brian Clips.", "OK");
        }

        static void BuildScene(string path, InSightChapter chapter, string nextScene,
            GameObject rigPrefab, GameObject landerPrefab, Material ground, Material sky,
            Material blue, Material orange, TerrainData opportunityTerrain,
            TerrainData opportunityFarTerrain, InSightVoiceCue[] cues)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(path);

            GameObject root = new GameObject("InSight Mission — " + chapter);
            InSightMissionSceneController controller = root.AddComponent<InSightMissionSceneController>();

            GameObject rig = PrefabUtility.InstantiatePrefab(rigPrefab, scene) as GameObject;
            GameObject lander = PrefabUtility.InstantiatePrefab(landerPrefab, scene) as GameObject;
            if (rig == null || lander == null)
                throw new InvalidOperationException("Unity failed to instantiate the shared XR rig or InSight lander model.");

            controller.BuildSceneInEditor(chapter, nextScene, rig, lander, ground, sky, blue, orange,
                opportunityTerrain, opportunityFarTerrain, cues);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        [MenuItem("Last Signal/InSight/Apply Opportunity Mars Terrain to Mission Scenes")]
        public static void ApplyOpportunityMarsTerrainToMissionScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("InSight terrain", "Stop Play mode before updating the mission scenes.", "OK");
                return;
            }

            TerrainData opportunityTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(OpportunityTerrainPath);
            TerrainData opportunityFarTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(OpportunityFarTerrainPath);
            if (opportunityTerrain == null || opportunityFarTerrain == null)
            {
                Debug.LogError("Could not load the shared Opportunity Mars terrain assets. Check: " +
                    OpportunityTerrainPath + " and " + OpportunityFarTerrainPath);
                return;
            }

            Scene originalScene = SceneManager.GetActiveScene();
            string[] scenePaths = { LandingScenePath, ScienceScenePath, LegacyScenePath };
            int updated = 0;
            foreach (string scenePath in scenePaths)
            {
                if (!File.Exists(Path.GetFullPath(scenePath)))
                {
                    Debug.LogWarning("InSight mission scene was not found: " + scenePath);
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool sceneChanged = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    InSightMissionSceneController controller = root.GetComponentInChildren<InSightMissionSceneController>(true);
                    if (controller == null) continue;
                    controller.ApplyOpportunityTerrainInEditor(opportunityTerrain, opportunityFarTerrain);
                    EditorUtility.SetDirty(controller);
                    sceneChanged = true;
                    break;
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    updated++;
                }
                else
                {
                    Debug.LogWarning("No InSight mission controller found in " + scenePath);
                }

                EditorSceneManager.CloseScene(scene, true);
                if (originalScene.IsValid() && originalScene.isLoaded)
                    SceneManager.SetActiveScene(originalScene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Applied the shared Opportunity Mars terrain to " + updated + " InSight mission scenes.");
            EditorUtility.DisplayDialog("InSight terrain", "Shared Opportunity Mars terrain is now in the InSight Landing, Arrival, and Legacy scenes.", "OK");
        }

        static void UpdateLaunchHandoff()
        {
            if (!File.Exists(Path.GetFullPath(LaunchScenePath)))
            {
                Debug.LogError("The existing InSight Launch scene was not found: " + LaunchScenePath);
                return;
            }

            Scene launch = EditorSceneManager.OpenScene(LaunchScenePath, OpenSceneMode.Additive);
            bool changed = false;
            foreach (GameObject root in launch.GetRootGameObjects())
            {
                foreach (LoadNextScene flow in root.GetComponentsInChildren<LoadNextScene>(true))
                {
                    if (flow.nextScene == "TitleScreen-Night" || string.IsNullOrWhiteSpace(flow.nextScene))
                    {
                        flow.nextScene = "InSight-Landing";
                        EditorUtility.SetDirty(flow);
                        changed = true;
                        if (flow.GetComponent<InSightVREffectCleanup>() == null)
                        {
                            flow.gameObject.AddComponent<InSightVREffectCleanup>();
                            changed = true;
                        }
                    }
                }
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(launch);
                EditorSceneManager.SaveScene(launch);
            }
            EditorSceneManager.CloseScene(launch, true);
        }

        static void UpdateFlashbackScript()
        {
            if (!File.Exists(Path.GetFullPath(FlashbackScenePath))) return;
            Scene flashback = EditorSceneManager.OpenScene(FlashbackScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in flashback.GetRootGameObjects())
            {
                InSightFlashbackSceneController controller = root.GetComponentInChildren<InSightFlashbackSceneController>(true);
                if (controller == null) continue;

                SerializedObject serialized = new SerializedObject(controller);
                SetString(serialized, "introNarrationText", "InSight was built to listen to Mars and study its crust, mantle, and core.");
                SetString(serialized, "solarPanelNarrationText", "Two circular solar arrays supplied the lander's electricity on Mars.");
                SetString(serialized, "cameraMastNarrationText", "SEIS listened for marsquakes; the robotic arm placed it on the Martian ground.");
                SetString(serialized, "instrumentArmNarrationText", "The arm placed SEIS and HP3 on the surface so they could measure shaking and heat flow.");
                SetString(serialized, "systemsCheckNarrationText", "Solar power, seismometer, robotic arm: InSight is ready to begin its mission.");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(flashback);
                EditorSceneManager.SaveScene(flashback);
                break;
            }
            EditorSceneManager.CloseScene(flashback, true);
        }

        static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.stringValue = value;
        }

        static void AddScenesToBuildSettings()
        {
            string[] paths = { TitleScenePath, FlashbackScenePath, LaunchScenePath, LandingScenePath, ScienceScenePath, LegacyScenePath };
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in paths)
            {
                if (!File.Exists(Path.GetFullPath(path))) continue;
                if (scenes.Exists(scene => scene.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static InSightVoiceCue[] LandingCues()
        {
            return new[]
            {
                Cue("landing_intro", "205 days to Mars", "InSight launched May 5, 2018, and traveled 205 days before reaching Mars."),
                Cue("landing_entry", "Atmospheric entry", "A heat shield protected the spacecraft as it entered Mars's atmosphere at high speed."),
                Cue("landing_parachute", "Parachute descent", "A supersonic parachute slowed the descent. The heat shield separated and the three legs deployed."),
                Cue("landing_powered_descent", "Powered descent", "After separating from the parachute and back shell, pulsed retrorockets controlled the final descent."),
                Cue("landing_touchdown", "Touchdown at Elysium Planitia", "InSight touched down on November 26, 2018. Unlike Opportunity, it used three shock-absorbing legs, not airbags.")
            };
        }

        static InSightVoiceCue[] ScienceCues()
        {
            return new[]
            {
                Cue("arrival_intro", "A stationary geophysics lander", "InSight stayed in one place. Its robotic arm set science packages onto the Martian surface."),
                Cue("seis", "SEIS — Mars's pulse", "SEIS measured marsquakes and ground vibrations. Its readings helped reveal Mars's crust, mantle, and core."),
                Cue("hp3", "HP³ — heat from below", "HP3 was designed to hammer nearly five meters underground, but unexpected soil stopped it short."),
                Cue("rise", "RISE — Mars's wobble", "RISE tracked subtle changes in Mars's rotation using radio signals between Earth and the lander."),
                Cue("science_summary", "Three clues to Mars's interior", "Quakes, heat flow, and rotation offered three different clues about Mars's hidden interior.")
            };
        }

        static InSightVoiceCue[] LegacyCues()
        {
            return new[]
            {
                Cue("legacy_intro", "A quieting lander", "InSight worked for more than four years, revealing Mars's interior and recording its weather."),
                Cue("legacy_retry", "Try another explanation", "Look at how the lander's power changed as dust collected on the solar panels."),
                Cue("legacy_end", "The final signal", "Dust reduced solar power. NASA received InSight's last signal on December 15, 2022, and ended the mission December 21."),
                Cue("legacy_science", "A lasting record", "SEIS detected 1,319 marsquakes. InSight's data continues to help us understand rocky planets.")
            };
        }

        static InSightVoiceCue Cue(string id, string heading, string caption)
        {
            return new InSightVoiceCue { id = id, heading = heading, caption = caption, audioClip = null };
        }
    }
}
#endif
