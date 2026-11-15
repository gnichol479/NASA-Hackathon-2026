using System.IO;
using System.Linq;
using System.Text;
using LastSignal.OpportunityFlashback.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastSignal.OpportunityFlashback.Editor
{
    /// <summary>Authors the shared facility into the scene, and renders repeatable eye-level reviews.</summary>
    public static class OpportunityFacilityPreviewBuilder
    {
        private const string RootFolder = "Assets/Opportunity/NASA-Headquarters/LastSignalFlashback";
        private const string ScenePath = RootFolder + "/Scenes/Opportunity-Flashback.unity";
        private const string MaterialsFolder = RootFolder + "/Materials/FacilityPreview";
        private const string ReviewFolder = RootFolder + "/Documentation/Review";
        private const string SharedRigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
        private const string RoverModelPath = "Assets/Opportunity/Models/Mars Exploration Rover - Spirit and Opportunity.fbx";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.delayCall += EnsureCurrentLayout;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        private static void OnSceneChanged(Scene previous, Scene current)
        {
            EditorApplication.delayCall += EnsureCurrentLayout;
        }

        private static OpportunityFlashbackSceneController FindController()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path.Replace('\\','/') != ScenePath)
                return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<OpportunityFlashbackSceneController>(true);
                if (controller != null) return controller;
            }
            return null;
        }

        private static void EnsureCurrentLayout()
        {
            var controller = FindController();
            if (controller == null) return;
            var environment = controller.GetComponentInChildren<OpportunityFacilityEnvironment>(true);
            // Only migrate the previous version. Opening the scene must not overwrite subsequent hand edits.
            if (environment == null || environment.LayoutVersion < OpportunityFacilityLayout.Version)
            {
                BuildPreview();
                EditorApplication.delayCall += VerifyAndCapture;
            }
        }

        private static void VerifyAndCapture()
        {
            OpportunityFlashbackVerification.VerifyAssemblyFlow();
            CaptureReview();
        }

        [MenuItem("Last Signal/Opportunity Flashback/Build Visible Facility Preview")]
        public static void BuildPreview()
        {
            var controller = FindController();
            if (controller == null)
            {
                Debug.LogError("Open the Opportunity-Flashback scene in Edit mode first.");
                return;
            }
            EnsureFolder(MaterialsFolder);
            var palette = new Material[OpportunityFacilityLayout.MaterialNames.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                string path = MaterialsFolder + "/" + OpportunityFacilityLayout.MaterialNames[i] + ".mat";
                palette[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (palette[i] == null)
                {
                    palette[i] = OpportunityFacilityLayout.MakeMaterial(i);
                    AssetDatabase.CreateAsset(palette[i],path);
                }
                else OpportunityFacilityLayout.ConfigureMaterial(palette[i],i);
                EditorUtility.SetDirty(palette[i]);
            }
            for (int i = controller.transform.childCount-1; i >= 0; i--)
            {
                Transform child = controller.transform.GetChild(i);
                if (child.name.StartsWith("EDITOR PREVIEW") || child.GetComponent<OpportunityFacilityEnvironment>() != null)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
            var facility = OpportunityFacilityLayout.BuildEnvironment(controller.transform,palette);
            Undo.RegisterCreatedObjectUndo(facility.gameObject,"Rebuild Opportunity facility");
            foreach (MeshRenderer renderer in facility.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial == palette[(int)FacilitySurface.Glass]) continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            if (controller.OpportunityModelAnchor != null)
                controller.OpportunityModelAnchor.localPosition = OpportunityFacilityLayout.RoverOrigin;
            var roverModel = AssetDatabase.LoadAssetAtPath<GameObject>(RoverModelPath);
            var serialized = new SerializedObject(controller);
            AnimationClip roverPose = serialized.FindProperty("deployedRoverPose").objectReferenceValue as AnimationClip;
            if (roverPose == null)
                roverPose = AssetDatabase.LoadAllAssetsAtPath(RoverModelPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal));
            float modelScale = serialized.FindProperty("finalOpportunityModelScale").floatValue;
            Vector3 modelEuler = serialized.FindProperty("finalOpportunityModelEuler").vector3Value;
            Transform preview = OpportunityFacilityLayout.Group("ASSEMBLY PREVIEW - Edit mode only",facility.transform);
            preview.gameObject.tag = "EditorOnly";
            facility.SetAssemblyPreview(preview.gameObject);
            GameObject scaffoldVisuals;
            GameObject scaffoldRoot = OpportunityFacilityLayout.BuildScaffold(preview,palette,out scaffoldVisuals);
            bool hasRoverModelPreview = BuildRoverModelPreview(roverModel,roverPose,modelScale,modelEuler,
                preview,scaffoldRoot,scaffoldVisuals,controller.OpportunityModelAnchor);
            for (int i = 0; i < 3; i++)
            {
                if (!hasRoverModelPreview)
                {
                    Transform part = OpportunityFacilityLayout.Group(((OpportunityPartKind)i) + " - preview",preview);
                    part.localPosition = OpportunityFacilityLayout.PartSpawns[i];
                    part.localRotation = OpportunityFacilityLayout.PartSpawnRotation;
                    OpportunityFacilityLayout.BuildPartVisuals(part,(OpportunityPartKind)i,palette);
                }
                TMP_Text label;
                var button = OpportunityFacilityLayout.BuildConsoleButton(preview,i,palette,out label);
                button.GetComponent<Collider>().enabled = false;
                if (i > 0) button.GetComponentInChildren<MeshRenderer>().sharedMaterial = palette[(int)FacilitySurface.Dark];
            }
            OpportunityFacilityLayout.BuildStatusDisplay(preview);
            // Preserve the anchor object and all assignments/events; the imported prefab is only instantiated during play.
            var voice = serialized.FindProperty("narrationVoiceId");
            if (voice.stringValue == "nPczcJzI2devNBz1zQrb") voice.stringValue = OpportunityFlashbackSceneController.ElevenLabsBrianVoiceId;
            if (roverModel != null)
                serialized.FindProperty("finalOpportunityModelPrefab").objectReferenceValue = roverModel;
            if (roverPose != null)
                serialized.FindProperty("deployedRoverPose").objectReferenceValue = roverPose;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddSavedPlayerRig(controller.gameObject.scene);
            EditorUtility.SetDirty(facility);
            AssetDatabase.SaveAssets();
            Scene scene = controller.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = null;
            SetArrivalView();
            Debug.Log("[Last Signal] High-bay Opportunity facility rebuilt and saved. Play mode reuses this exact room.");
        }

        private static bool BuildRoverModelPreview(GameObject roverModel,AnimationClip roverPose,float modelScale,
            Vector3 modelEuler,Transform preview,GameObject scaffoldRoot,GameObject scaffoldVisuals,Transform fixtureAnchor)
        {
            if (roverModel == null || fixtureAnchor == null) return false;
            GameObject model = Object.Instantiate(roverModel);
            model.name = "Opportunity complete rover reference — edit-mode preview";
            model.transform.SetParent(preview,false);
            OpportunityFacilityLayout.PrepareDetailedOpportunityModel(model,roverPose,modelEuler);
            OpportunityFacilityLayout.PlaceRoverOnFixture(model.transform,fixtureAnchor,modelScale);
            Transform[][] sources = OpportunityFacilityLayout.FindRoverComponentSources(model.transform);
            if (sources == null)
            {
                Object.DestroyImmediate(model);
                Debug.LogWarning("[Last Signal] The updated FBX did not expose named solar panel, mast, and instrument-arm groups. Keeping the primitive preview.");
                return false;
            }

            Bounds[] bounds = new Bounds[3];
            for (int i = 0; i < bounds.Length; i++)
            {
                if (OpportunityFacilityLayout.TryGetComponentBounds(sources[i],out bounds[i])) continue;
                Object.DestroyImmediate(model);
                Debug.LogWarning("[Last Signal] A rover component had no renderable geometry. Keeping the primitive preview.");
                return false;
            }

            scaffoldVisuals.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                Transform socket = scaffoldRoot.transform.Find(OpportunityFacilityLayout.SocketNames[i]);
                Transform guide = socket.Find("Mount guide");
                OpportunityFacilityLayout.PlaceSocketOnModelPart(socket,guide,bounds[i],model.transform.rotation);
                guide.gameObject.SetActive(false);
                Transform part = OpportunityFacilityLayout.Group(((OpportunityPartKind)i) + " — imported rover part on tray",preview);
                part.position = OpportunityFacilityLayout.GetComponentTraySpawn(i,bounds[i]);
                part.rotation = model.transform.rotation;
                OpportunityFacilityLayout.BuildExtractedModelPart(model.transform,sources[i],bounds[i],part);
                // The edit-mode room preview mirrors the initial assembly state: parts live on
                // their preparation bench until the player snaps them onto the rover.
                foreach (Transform source in sources[i]) source.gameObject.SetActive(false);
            }
            return true;
        }

        private static void AddSavedPlayerRig(Scene scene)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SharedRigPath);
            if (source == null)
            {
                Debug.LogError("The shared tracked XR rig prefab could not be found at " + SharedRigPath);
                return;
            }
            GameObject rig = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                if (sceneRoot.name.StartsWith("Flashback Player")) { rig = sceneRoot; break; }
            if (rig == null)
            {
                rig = PrefabUtility.InstantiatePrefab(source,scene) as GameObject;
                if (rig == null)
                {
                    Debug.LogError("Unity could not add the shared XR rig prefab to the flashback scene.");
                    return;
                }
            }
            rig.name = "Flashback Player - shared tracked XR rig";
            rig.transform.SetPositionAndRotation(OpportunityFacilityLayout.Arrival,Quaternion.identity);
            EditorUtility.SetDirty(rig);
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.tag = "MainCamera";
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 80f;
                camera.stereoTargetEye = StereoTargetEyeMask.Both;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Last Signal] Scene now contains the shared XR player, tracked camera and controller interactors at the arrival point.");
        }

        [MenuItem("Last Signal/Opportunity Flashback/Inspect Imported Rover Parts")]
        public static void InspectImportedRoverParts()
        {
            foreach (string path in new[] { RoverModelPath, "Assets/Opportunity/Models/Mars Exploration Rover - Spirit and Opportunity.fbx" })
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Debug.LogWarning("[Last Signal] No model root was imported at " + path);
                    continue;
                }
                GameObject instance = Object.Instantiate(asset);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    Bounds bounds = renderers.Length == 0 ? new Bounds(instance.transform.position,Vector3.zero) : renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    var report = new StringBuilder("[Last Signal] Rover asset: ").Append(path)
                        .Append(" | root: ").Append(asset.name).Append(" | renderers: ").Append(renderers.Length)
                        .Append(" | bounds: ").Append(bounds.size).AppendLine();
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                        if (child.GetComponent<Renderer>() != null)
                        {
                            var renderer = child.GetComponent<Renderer>();
                            string chain = child.name;
                            for (Transform ancestor = child.parent; ancestor != null && ancestor != instance.transform; ancestor = ancestor.parent)
                                chain = ancestor.name + "/" + chain;
                            report.Append("  ").Append(chain).Append(" local ")
                                .Append(child.localPosition).Append(" scale ").Append(child.localScale)
                                .Append(" mesh bounds ").Append(renderer.localBounds.size).Append(" world center ")
                                .Append(renderer.bounds.center).Append(" size ").Append(renderer.bounds.size).AppendLine();
                        }
                    Debug.Log(report.ToString());
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        [MenuItem("Last Signal/Opportunity Flashback/View From Arrival")]
        public static void SetArrivalView()
        {
            if (SceneView.lastActiveSceneView == null) return;
            var view = SceneView.lastActiveSceneView;
            Vector3 eye = OpportunityFacilityLayout.Arrival + Vector3.up*1.62f;
            Quaternion rotation = Quaternion.LookRotation(new Vector3(0f,1.7f,1.5f)-eye);
            view.orthographic = false;
            view.LookAtDirect(eye+rotation*Vector3.forward*5f,rotation,5f);
            view.Repaint();
        }

        [MenuItem("Last Signal/Opportunity Flashback/Capture Facility Review")]
        public static void CaptureReview()
        {
            if (FindController() == null) return;
            EnsureFolder(ReviewFolder);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>()) text.ForceMeshUpdate();
            Capture("01-arrival",new Vector3(0f,1.62f,-6.1f),new Vector3(0f,1.8f,1.5f),72f);
            Capture("02-assembly",new Vector3(0f,1.62f,-.6f),new Vector3(0f,1.23f,1.5f),76f);
            Capture("03-room-and-gallery",new Vector3(-6.8f,1.62f,-5.9f),new Vector3(.6f,2.1f,2.2f),72f);
            Capture("04-reverse-room",new Vector3(5.8f,1.62f,6.6f),new Vector3(-1.1f,1.9f,-2f),72f);
            Capture("05-systems-console",new Vector3(4.4f,1.62f,-1.45f),new Vector3(4.4f,1.38f,.45f),60f);
            Capture("06-component-benches",new Vector3(-2.85f,1.62f,1.25f),new Vector3(-4.65f,1.1f,1.25f),82f);
            Capture("07-observation-windows",new Vector3(5.4f,1.62f,4.2f),new Vector3(9f,1.8f,3.5f),80f);
            AssetDatabase.Refresh();
            Debug.Log("[Last Signal] Rendered seven facility review views at 1.62 m eye height to " + ReviewFolder);
        }

        private static void Capture(string name, Vector3 eye, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary facility review camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = fov;
            camera.aspect = 1.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.58f,.64f,.69f);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.allowHDR = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            var rt = new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                rt.Create();
                camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(ReviewFolder,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
        }
    }
}
