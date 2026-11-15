#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Object = UnityEngine.Object;

namespace LastSignal.IngenuityFlashback.EditorTools
{
    public static class IngenuityQuestSetup
    {
        public const string ScenePath = "Assets/Ingenuity/NASA-Headquarters/LastSignalFlashback/Scenes/Ingenuity-Flashback.unity";
        private const string LaunchScenePath = "Assets/Opportunity/Launch/Scenes/Opportunity-Launch.unity";
        private const string Menu = "Last Signal/Ingenuity Flashback/VR/";
        private const string PendingBuild = "LastSignal.QuestBuildPending";
        private const string SimulatorSettings = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";

        [MenuItem(Menu + "Configure Quest 2 and Link")]
        public static void Configure()
        {
            Require(!EditorApplication.isPlaying, "Exit Play mode before configuring XR.");
            Require(EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget targets), "XR settings asset is missing.");
            foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!targets.HasManagerSettingsForBuildTarget(group))
                    targets.CreateDefaultManagerSettingsForBuildTarget(group);
                XRGeneralSettings general = targets.SettingsForBuildTarget(group);
                general.InitManagerOnStart = true;
                XRManagerSettings manager = targets.ManagerSettingsForBuildTarget(group);
                if (!manager.activeLoaders.Any(loader => loader is OpenXRLoader))
                    Require(XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, group),
                        "Could not assign OpenXR to " + group);
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(general);
                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                Require(settings != null, "OpenXR settings missing for " + group);
                var touch = settings.GetFeature<OculusTouchControllerProfile>();
                Require(touch != null, "Oculus Touch profile missing for " + group);
                touch.enabled = true;
                // Features are separate subassets: dirtying only the settings loses this change.
                EditorUtility.SetDirty(touch);
                settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                if (group == BuildTargetGroup.Android)
                {
                    var quest = settings.GetFeature<MetaQuestFeature>();
                    Require(quest != null, "Meta Quest Android feature missing.");
                    quest.enabled = true;
                    EditorUtility.SetDirty(quest);
                }
                EditorUtility.SetDirty(settings);
            }
            EditorUtility.SetDirty(targets);

            var simulator = AssetDatabase.LoadMainAssetAtPath(SimulatorSettings);
            Require(simulator != null, "XR simulator settings missing.");
            var serialized = new SerializedObject(simulator);
            serialized.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = false;
            serialized.FindProperty("m_AutomaticallyInstantiateInEditorOnly").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(simulator);

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            // Dedicated id for the isolated flashback APK; preserve any existing team's id.
            string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (string.IsNullOrEmpty(identifier) || identifier.Contains("DefaultCompany"))
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.lastsignal.opportunity.flashback");
            // Both input backends stay enabled because teammates still use legacy Input APIs.
            Type inputPreferences = typeof(UnityEngine.InputSystem.InputDevice).Assembly.GetType(
                "UnityEngine.InputSystem.Editor.InputEditorUserSettings");
            inputPreferences?.GetProperty("lockInputToGameView", BindingFlags.Static | BindingFlags.Public)
                ?.SetValue(null, true);

            Scene scene = SceneManager.GetActiveScene();
            Require(scene.path == ScenePath, "Open Ingenuity-Flashback before saving its VR rig.");
            ApplySceneRig(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Verify();
            Debug.Log("[Ingenuity XR] Saved Quest 2 / Link configuration and flashback scene. Oculus Touch registered for Windows and Android; Android ARM64/IL2CPP, Vulkan, Meta Quest support; automatic simulator OFF.");
        }

        public static void ApplySceneRig(Scene scene)
        {
            XROrigin origin = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).Single();
            GameObject rig = origin.transform.root.gameObject;
            rig.transform.SetPositionAndRotation(IngenuityFacilityLayout.Arrival, Quaternion.identity);
            var setup = rig.GetComponent<IngenuityFlashbackXRPlayerSetup>();
            if (setup == null) setup = rig.AddComponent<IngenuityFlashbackXRPlayerSetup>();
            setup.Configure(rig, origin.Camera);
            var cameraData = origin.Camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            // Persist nested prefab overrides, without changing the shared rig used by teammates.
            foreach (var component in rig.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        [MenuItem(Menu + "Verify VR Setup")]
        public static void Verify()
        {
            foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                Require(general != null && general.InitManagerOnStart && general.Manager.activeLoaders.Any(x => x is OpenXRLoader),
                    group + " needs an automatically initialized OpenXR loader.");
                Require(OpenXRSettings.GetSettingsForBuildTargetGroup(group).GetFeature<OculusTouchControllerProfile>().enabled,
                    group + " needs the Oculus Touch interaction profile.");
            }
            Require(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaQuestFeature>().enabled,
                "Android Meta Quest support must be enabled.");
            Require(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP &&
                PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "Quest requires ARM64 IL2CPP.");
            var simulator = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SimulatorSettings));
            Require(!simulator.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue, "Automatic simulator conflicts with real Link input.");
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            XROrigin[] origins = roots.SelectMany(root => root.GetComponentsInChildren<XROrigin>(true)).ToArray();
            Require(origins.Length == 1, "Flashback needs exactly one XR Origin.");
            XROrigin origin = origins[0];
            Require(origin.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.Floor, "Use floor tracking.");
            Require(origin.transform.lossyScale == Vector3.one, "XR Origin must have unit scale.");
            var pose = origin.Camera.GetComponent<TrackedPoseDriver>();
            Require(pose != null && pose.enabled && pose.trackingType == TrackedPoseDriver.TrackingType.RotationAndPosition,
                "Camera requires a position-and-rotation TrackedPoseDriver.");
            Require(pose.positionInput.action != null && pose.rotationInput.action != null && pose.trackingStateInput.action != null,
                "Head position, rotation and tracking-state actions must be assigned.");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Count(camera => camera.enabled && camera.CompareTag("MainCamera")) == 1,
                "Exactly one enabled MainCamera must render the scene.");
            var move = origin.transform.root.GetComponentInChildren<DynamicMoveProvider>(true);
            var turn = origin.transform.root.GetComponentInChildren<ActionBasedSnapTurnProvider>(true);
            Require(move != null && move.leftHandMoveAction.action != null && turn != null && turn.rightHandSnapTurnAction.action != null,
                "Left movement and right snap-turn actions must be assigned.");
            Physics.SyncTransforms();
            Require(Physics.Raycast(IngenuityFacilityLayout.Arrival + Vector3.up * .4f, Vector3.down, out RaycastHit hit, 1f,
                ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y) < .03f, "Arrival must be above the facility floor at y=0.");
            Debug.Log("[Ingenuity XR] PASS: loaders, Touch profiles, Quest Android settings, single tracked camera, floor spawn, left move and right snap-turn bindings, simulator disabled.");
        }

        [MenuItem(Menu + "Log Live XR Status")]
        public static void LogStatus()
        {
            foreach (var player in Object.FindObjectsOfType<IngenuityFlashbackXRPlayerSetup>())
                Debug.Log("[Ingenuity XR] " + player.DescribeStatus(), player);
        }

        [MenuItem(Menu + "Build Quest 2 Flashback APK")]
        public static void BuildQuest()
        {
            Configure();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                SessionState.SetBool(PendingBuild, true);
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    SessionState.SetBool(PendingBuild, false);
                    throw new InvalidOperationException("Could not switch to the installed Android build module.");
                }
            }
            else BuildAndroid();
        }

        [InitializeOnLoadMethod]
        private static void ResumeBuild()
        {
            if (SessionState.GetBool(PendingBuild, false)) EditorApplication.delayCall += () =>
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return;
                SessionState.SetBool(PendingBuild, false);
                BuildAndroid();
            };
        }

        private static void BuildAndroid()
        {
            string output = "Builds/Quest2/LastSignal-Flashback.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, LaunchScenePath }, locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            Require(report.summary.result == BuildResult.Succeeded, "Quest build failed: " + report.summary.result);
            Debug.Log("[Ingenuity XR] Quest APK built: " + Path.GetFullPath(output));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Ingenuity XR] " + message);
        }
    }
}
#endif
