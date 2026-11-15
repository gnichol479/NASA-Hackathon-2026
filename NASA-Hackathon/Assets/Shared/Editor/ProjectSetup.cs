using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace NasaHack.EditorTools
{
    // One-time project setup: URP, OpenXR, XR rig prefab, Opportunity scenes, Build Settings.
    // Safe to re-run: existing assets are reused, not duplicated.
    // Phase1 imports samples and settings. Phase2 needs the sample scripts compiled, so it runs after.
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Shared/Settings";
        const string RigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
        const string TitlePath = "Assets/Shared/Scenes/TitleScreen.unity";
        const string Rover = "Opportunity";
        static readonly string[] Parts = { "Arrival", "Origin", "Launch", "Landing", "Legacy" };

        static string ScenePath(string part) => $"Assets/{Rover}/{part}/Scenes/{Rover}-{part}.unity";

        [MenuItem("NasaHack/Setup/Phase 1 (packages, URP, OpenXR)")]
        public static void Phase1()
        {
            ImportXriSamples();
            SetupUrp();
            SetupOpenXR();
            AssetDatabase.SaveAssets();
            Debug.Log("[NasaHack] Phase 1 done.");
        }

        [MenuItem("NasaHack/Setup/Phase 2 (rig, scenes, build settings)")]
        public static void Phase2()
        {
            SetupOpenXRFeatures();
            var rig = CreateRigPrefab();
            ConfigureSharedRigForQuestTracking();
            SetupDeviceSimulator();
            var ground = CreateGroundMaterial();
            CreateScenes(rig, ground);
            CleanupOldAssets();
            SetBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[NasaHack] Phase 2 done.");
        }

        static void ImportXriSamples()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(XRBaseInteractable).Assembly);
            foreach (var sample in Sample.FindByPackage(info.name, info.version))
            {
                if (sample.displayName != "Starter Assets" && sample.displayName != "XR Device Simulator") continue;
                if (sample.isImported) continue;
                sample.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
                Debug.Log($"[NasaHack] Imported XRI sample: {sample.displayName}");
            }
            AssetDatabase.Refresh();
        }

        static void SetupUrp()
        {
            EnsureFolder(SettingsDir);
            var assetPath = SettingsDir + "/URP-Asset.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                // Mirrors URP's own "Create > Rendering > URP Asset (with Universal Renderer)".
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(data, SettingsDir + "/URP-Renderer.asset");
                ResourceReloader.ReloadAllNullIn(data, UniversalRenderPipelineAsset.packagePath);

                urp = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(urp, assetPath);
            }

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[NasaHack] URP assigned.");
        }

        static void SetupOpenXR()
        {
            var group = BuildTargetGroup.Standalone;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            if (!perTarget.HasManagerSettingsForBuildTarget(group))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            perTarget.SettingsForBuildTarget(group).InitManagerOnStart = true;
            var ok = XRPackageMetadataStore.AssignLoader(perTarget.ManagerSettingsForBuildTarget(group), typeof(OpenXRLoader).FullName, group);
            EditorUtility.SetDirty(perTarget);
            Debug.Log($"[NasaHack] OpenXR loader for Standalone: {(ok ? "assigned" : "already assigned or failed")}.");
        }

        static void SetupOpenXRFeatures()
        {
            ConfigureOpenXRControllerProfiles(BuildTargetGroup.Standalone, includeDesktopProfiles: true);
            ConfigureOpenXRControllerProfiles(BuildTargetGroup.Android, includeDesktopProfiles: false);
        }

        [MenuItem("NasaHack/VR/Enable Quest 2 Touch controllers")]
        static void EnableQuest2TouchControllers()
        {
            SetupOpenXRFeatures();
            AssetDatabase.SaveAssets();
            Debug.Log("[NasaHack] Oculus Touch controller profile enabled for Quest Link and Android Quest builds.");
        }

        static void ConfigureOpenXRControllerProfiles(BuildTargetGroup group, bool includeDesktopProfiles)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                Debug.LogWarning($"[NasaHack] OpenXR settings not found for {group}. Enable controller profiles in Project Settings > XR Plug-in Management > OpenXR.");
                return;
            }

            var profiles = includeDesktopProfiles
                ? new[]
                {
                    typeof(OculusTouchControllerProfile),
                    typeof(MetaQuestTouchPlusControllerProfile),
                    typeof(KHRSimpleControllerProfile),
                    typeof(ValveIndexControllerProfile),
                    typeof(HTCViveControllerProfile),
                }
                : new[] { typeof(OculusTouchControllerProfile) };

            foreach (var type in profiles)
            {
                var feature = settings.GetFeature(type);
                if (feature == null) continue;
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(settings);
            Debug.Log($"[NasaHack] OpenXR controller profiles enabled for {group}.");
        }

        static GameObject CreateRigPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (existing != null) return existing;

            var source = FindPrefab("XR Interaction Setup");
            if (source == null)
                throw new System.Exception("[NasaHack] 'XR Interaction Setup' prefab not found. Import XRI Starter Assets (run Phase 1).");

            EnsureFolder(Path.GetDirectoryName(RigPath).Replace('\\', '/'));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "XR Rig";

            // Post-processing on, for the flashback effect.
            foreach (var cam in instance.GetComponentsInChildren<Camera>(true))
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, RigPath);
            Object.DestroyImmediate(instance);
            Debug.Log("[NasaHack] Created " + RigPath);
            return prefab;
        }

        [MenuItem("NasaHack/VR/Configure shared XR rig for Quest floor tracking")]
        static void ConfigureSharedRigForQuestTracking()
        {
            if (!File.Exists(RigPath))
            {
                Debug.LogError("[NasaHack] Shared XR Rig prefab is missing: " + RigPath);
                return;
            }

            var rig = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var origin = rig.GetComponentInChildren<XROrigin>(true);
                if (origin == null)
                {
                    Debug.LogError("[NasaHack] Shared XR Rig has no XROrigin.");
                    return;
                }

                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                origin.CameraYOffset = 0f;
                if (origin.CameraFloorOffsetObject != null)
                    origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;

                if (origin.Camera != null)
                {
                    var pose = origin.Camera.GetComponent<TrackedPoseDriver>();
                    if (pose != null)
                    {
                        pose.enabled = true;
                        pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                        pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
                        pose.ignoreTrackingState = false;
                    }
                    origin.Camera.tag = "MainCamera";
                }

                PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
                Debug.Log("[NasaHack] Shared XR Rig now requests floor tracking with no artificial camera-height offset; TrackedPoseDriver owns head pose.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }

        // XRI keeps these settings internal, so set them by reflection. Editor-only simulator, never in builds.
        static void SetupDeviceSimulator()
        {
            ConfigureDeviceSimulator(false);
        }

        [MenuItem("NasaHack/VR/Use Quest Link (disable simulator)")]
        static void UseQuestLink()
        {
            ConfigureDeviceSimulator(false);
        }

        [MenuItem("NasaHack/VR/Use desktop XR simulator")]
        static void UseDesktopSimulator()
        {
            ConfigureDeviceSimulator(true);
        }

        static void ConfigureDeviceSimulator(bool enabled)
        {
            var simulator = FindPrefab("XR Device Simulator");
            var type = typeof(XRBaseInteractable).Assembly.GetType("UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulatorSettings");
            var settings = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null) as Object;
            if (simulator == null || settings == null)
            {
                Debug.LogWarning("[NasaHack] Could not configure XR Device Simulator. Set it in Project Settings > XR Plug-in Management > XR Interaction Toolkit.");
                return;
            }

            SetInternal(settings, "automaticallyInstantiateSimulatorPrefab", enabled);
            SetInternal(settings, "automaticallyInstantiateInEditorOnly", true);
            SetInternal(settings, "simulatorPrefab", simulator);
            EditorUtility.SetDirty(settings);
            Debug.Log(enabled
                ? "[NasaHack] XR Device Simulator enabled for desktop Editor testing. Turn it off before using Quest Link."
                : "[NasaHack] XR Device Simulator disabled so Quest Link supplies the live head and controller poses.");
        }

        static Material CreateGroundMaterial()
        {
            var path = SettingsDir + "/Mars Ground.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(GraphicsSettings.currentRenderPipeline.defaultShader);
            mat.color = new Color(0.62f, 0.36f, 0.22f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void CreateScenes(GameObject rig, Material ground)
        {
            // TitleScreen: move the existing one into Shared, then give it the rig.
            if (!File.Exists(TitlePath) && File.Exists("Assets/Scenes/TitleScreen.unity"))
            {
                EnsureFolder("Assets/Shared/Scenes");
                var err = AssetDatabase.MoveAsset("Assets/Scenes/TitleScreen.unity", TitlePath);
                if (!string.IsNullOrEmpty(err)) Debug.LogError("[NasaHack] Move TitleScreen failed: " + err);
            }
            if (File.Exists(TitlePath))
            {
                var title = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
                if (Object.FindObjectOfType<LoadNextScene>() == null)
                {
                    var oldCam = GameObject.Find("Main Camera");
                    if (oldCam != null) Object.DestroyImmediate(oldCam);
                    PrefabUtility.InstantiatePrefab(rig);
                    AddSceneFlow(ScenePathName(Parts[0]));
                    EditorSceneManager.SaveScene(title);
                }
            }

            for (int i = 0; i < Parts.Length; i++)
            {
                var path = ScenePath(Parts[i]);
                if (File.Exists(path)) continue;

                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                PrefabUtility.InstantiatePrefab(rig);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Ground (teleport)";
                floor.transform.localScale = new Vector3(10f, 1f, 10f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
                floor.AddComponent<TeleportationArea>();

                bool last = i == Parts.Length - 1;
                AddSceneFlow(last ? "TitleScreen" : ScenePathName(Parts[i + 1]));

                EditorSceneManager.SaveScene(scene, path);
                Debug.Log("[NasaHack] Created " + path);
            }
        }

        static string ScenePathName(string part) => $"{Rover}-{part}";

        static void AddSceneFlow(string next)
        {
            var flow = new GameObject("Scene Flow").AddComponent<LoadNextScene>();
            flow.nextScene = next;
        }

        static void CleanupOldAssets()
        {
            foreach (var path in new[]
            {
                "Assets/Scenes/Opportunity-Rover.unity",
                "Assets/Scenes/InSight-Lander.unity",
                "Assets/Scenes/Ingenuity-Helicopter.unity",
            })
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }

            if (AssetDatabase.IsValidFolder("Assets/Scenes") && !AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Any())
                AssetDatabase.DeleteAsset("Assets/Scenes");
            if (AssetDatabase.IsValidFolder("Assets/Rover-Models") && !AssetDatabase.FindAssets("", new[] { "Assets/Rover-Models" }).Any())
                AssetDatabase.DeleteAsset("Assets/Rover-Models");
        }

        static void SetBuildSettings()
        {
            var paths = new[] { TitlePath }.Concat(Parts.Select(ScenePath)).Where(File.Exists);
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            Debug.Log("[NasaHack] Build Settings: " + string.Join(", ", paths));
        }

        static GameObject FindPrefab(string name)
        {
            return AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault(g => g != null);
        }

        static void SetInternal(Object target, string property, object value)
        {
            var prop = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) prop.SetValue(target, value);
            else Debug.LogWarning($"[NasaHack] Could not set {property} on {target.GetType().Name}.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
