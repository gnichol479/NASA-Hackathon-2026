using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack.EditorTools
{
    // Builds the present-day Opportunity scenes (Arrival, Legacy): rover, Mars sky,
    // hotspots, story panels, wake-up command, flashback wiring.
    // Only builds into a scene that doesn't already have an "Opportunity" object,
    // so re-running never overwrites your edits.
    public static class OpportunitySceneBuilder
    {
        const string ArrivalPath = "Assets/Opportunity/Arrival/Scenes/Opportunity-Arrival.unity";
        const string LegacyPath = "Assets/Opportunity/Legacy/Scenes/Opportunity-Legacy.unity";
        // Detailed textured model (from Dominic). Faces the player when turned 180°.
        const string ModelPath = "Assets/Opportunity/Models/Mars Exploration Rover - Spirit and Opportunity.fbx";
        static readonly Quaternion ModelRotation = Quaternion.Euler(0f, 180f, 0f);
        const string SettingsDir = "Assets/Shared/Settings";
        const string TmpEssentials = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";

        // MER rovers: about 2.3 m wide with solar arrays deployed.
        const float RoverWidth = 2.3f;
        static readonly Vector3 RoverPosition = new Vector3(0f, 0f, 3f);
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        [MenuItem("NasaHack/Build Opportunity Arrival + Legacy")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first, then run the builder.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                AssetDatabase.importPackageCompleted += OnTmpImported;
                AssetDatabase.ImportPackage(Path.GetFullPath(TmpEssentials), false);
                return;
            }
            BuildScenes();
        }

        static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += BuildScenes;
        }

        static void BuildScenes()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[NasaHack] Opportunity model not found at " + ModelPath);
                return;
            }

            var sky = SkyMaterial();
            var hotspot = HotspotMaterial();
            BuildArrival(model, sky, hotspot);
            BuildLegacy(model, sky);
            EditorSceneManager.OpenScene(ArrivalPath);
            Debug.Log("[NasaHack] Opportunity Arrival + Legacy built.");
        }

        // ---------- Arrival ----------

        static void BuildArrival(GameObject model, Material sky, Material hotspotMat)
        {
            var scene = EditorSceneManager.OpenScene(ArrivalPath);
            if (GameObject.Find("Opportunity") != null)
            {
                Debug.Log("[NasaHack] Arrival already built, skipped.");
                return;
            }

            SetupMars(sky);
            var rover = PlaceRover(model, 0.45f, out var bounds);

            var hotspots = new GameObject("Hotspots").transform;
            hotspots.SetParent(rover, false);
            float h = bounds.size.y;
            AddHotspot(hotspots, hotspotMat, "Solar panels",
                "Opportunity ran on sunlight. Dust settling on these panels cut its power. In 2018 a planet-wide dust storm blocked the Sun.",
                SolarPanelPoint(rover, bounds));
            AddHotspot(hotspots, hotspotMat, "Pancam (panoramic camera)",
                "This mast held Opportunity's main camera. Its last 360° panorama was taken right here, from 354 images, between May 13 and June 10, 2018.",
                new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z));
            AddHotspot(hotspots, hotspotMat, "Robotic arm",
                "The arm carried a rock grinder and science instruments. They helped find hematite, a mineral that usually forms in water.",
                new Vector3(bounds.center.x, bounds.min.y + 0.35f * h, bounds.min.z - 0.05f));
            AddHotspot(hotspots, hotspotMat, "Wheels",
                "Six wheels carried Opportunity 45.16 km, the first marathon on another world. It also climbed a 32-degree slope, the steepest of any Mars rover.",
                new Vector3(bounds.min.x + 0.15f, bounds.min.y + 0.2f, bounds.min.z + 0.2f));

            var flow = Object.FindObjectOfType<LoadNextScene>();
            var flashback = new GameObject("Flashback").AddComponent<FlashbackTransition>();
            flashback.startMode = FlashbackTransition.StartMode.Present;
            flashback.sceneFlow = flow;

            // Status card with the flashback trigger.
            var status = UiFactory.CreatePanel("Status Panel", null, 700f);
            Place(status, new Vector3(1.7f, 1.4f, 2.3f));
            UiFactory.AddText(status, "Title", "Opportunity rover", 50f, FontStyles.Bold).color = UiFactory.Accent;
            UiFactory.AddText(status, "Body",
                "Status: Silent since June 10, 2018\nLanded: January 25, 2004\nPlanned mission: 90 days. Actual: almost 15 years.",
                32f, FontStyles.Normal);
            var button = UiFactory.AddButton(status, "See How I Got Here", "See how I got here", out _);
            UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(flashback.Play));

            var narration = CreateNarration(new Vector3(-1.7f, 1.5f, 2.3f), hideButtonOnLastBeat: true);
            narration.beats = new[]
            {
                Beat("Perseverance Valley, Mars",
                    "You're on the rim of Endeavour Crater. The rover in front of you is NASA's Opportunity. It hasn't moved since 2018."),
                Beat("Explore",
                    "Point at the glowing markers to learn what each part did. When you're ready, ask Opportunity how it got here."),
            };

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + ArrivalPath);
        }

        // ---------- Legacy ----------

        static void BuildLegacy(GameObject model, Material sky)
        {
            var scene = EditorSceneManager.OpenScene(LegacyPath);
            if (GameObject.Find("Opportunity") != null)
            {
                Debug.Log("[NasaHack] Legacy already built, skipped.");
                return;
            }

            SetupMars(sky);
            PlaceRover(model, 0.6f, out _);

            var flow = Object.FindObjectOfType<LoadNextScene>();
            var flashback = new GameObject("Flashback").AddComponent<FlashbackTransition>();
            flashback.startMode = FlashbackTransition.StartMode.Returning;

            var narration = CreateNarration(new Vector3(-1.6f, 1.5f, 2.2f), hideButtonOnLastBeat: false);
            narration.finishLabel = "Return to Earth";
            UnityEventTools.AddVoidPersistentListener(narration.onFinished, new UnityAction(flow.Load));

            var wake = CreateWakeUpPanel(new Vector3(1.5f, 1.4f, 2.2f), narration);

            narration.beats = new[]
            {
                Beat("Why was I left behind?",
                    "There was never a ticket home. Getting to Mars took a rocket. Leaving would have taken another one. Every kilogram was spent on science, so Mars was always where I would finish."),
                Beat("Why did my mission end?",
                    "In June 2018, a dust storm covered the whole planet and blocked the sunlight my solar panels needed. On June 10, 2018, I went quiet.\n\nTry to wake me up, the way my team did.",
                    waitForEvent: true, showWhileActive: wake.gameObject),
                Beat("My team never gave up",
                    "Over eight months they sent more than a thousand commands. The last ones went out on February 12, 2019. The next day, NASA declared my mission complete."),
                Beat("Why remember me?",
                    "I was built to last 90 days. I lasted almost 15 years. I drove 45.16 km and found signs of ancient water, like a pond or lake on Earth.\n\nOn October 11, 2026, a rover named Perseverance passed my driving record. Every explorer that comes next starts from what I found."),
            };

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + LegacyPath);
        }

        static WakeUpCommand CreateWakeUpPanel(Vector3 position, NarrationPanel narration)
        {
            var panel = UiFactory.CreatePanel("Wake-Up Panel", null, 640f);
            Place(panel, position);
            UiFactory.AddText(panel, "Title", "Mission control", 44f, FontStyles.Bold).color = UiFactory.Accent;
            var status = UiFactory.AddText(panel, "Status", "", 32f, FontStyles.Normal);
            var button = UiFactory.AddButton(panel, "Send Button", "Send wake-up command", out var label);

            var wake = panel.gameObject.AddComponent<WakeUpCommand>();
            wake.narration = narration;
            wake.button = button;
            wake.buttonLabel = label;
            wake.status = status;
            return wake;
        }

        // ---------- Shared pieces ----------

        static NarrationPanel CreateNarration(Vector3 position, bool hideButtonOnLastBeat)
        {
            var panel = UiFactory.CreatePanel("Narration Panel", null, 760f);
            Place(panel, position);
            var narration = panel.gameObject.AddComponent<NarrationPanel>();
            narration.titleText = UiFactory.AddText(panel, "Title", "", 50f, FontStyles.Bold);
            narration.titleText.color = UiFactory.Accent;
            narration.bodyText = UiFactory.AddText(panel, "Body", "", 34f, FontStyles.Normal);
            narration.nextButton = UiFactory.AddButton(panel, "Next Button", "Next", out var label);
            narration.buttonLabel = label;
            narration.hideButtonOnLastBeat = hideButtonOnLastBeat;
            return narration;
        }

        static NarrationPanel.Beat Beat(string title, string text, bool waitForEvent = false, GameObject showWhileActive = null)
        {
            return new NarrationPanel.Beat { title = title, text = text, waitForEvent = waitForEvent, showWhileActive = showWhileActive };
        }

        static void Place(Transform panel, Vector3 position)
        {
            panel.position = position;
            UiFactory.FacePoint(panel, Head);
        }

        static Transform PlaceRover(GameObject model, float dust, out Bounds bounds)
        {
            var root = new GameObject("Opportunity").transform;
            root.position = RoverPosition;
            AddModel(root, model, dust, out bounds);
            return root;
        }

        // Instantiates the model under root, centered on root, standing on the ground.
        // hiddenParts: renderer paths to switch off (leftover Blender objects in the FBX).
        // Models with an animation are posed at whichever end of the clip is flatter (panels deployed).
        // fitWidth: scale to real rover width. The detailed FBX is already in meters, so it stays native.
        static GameObject AddModel(Transform root, GameObject model, float dust, out Bounds bounds, string[] hiddenParts = null, bool fitWidth = true)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            instance.name = "Model";
            instance.transform.localRotation = ModelRotation;

            foreach (var part in hiddenParts ?? new string[0])
            {
                var t = instance.transform.Find(part);
                var r = t != null ? t.GetComponent<Renderer>() : null;
                if (r != null) r.enabled = false;
            }

            PoseFlattest(instance, model);

            bounds = WorldBounds(instance);
            float scale = fitWidth ? RoverWidth / Mathf.Max(bounds.size.x, bounds.size.z) : 1f;
            instance.transform.localScale *= scale;
            bounds = WorldBounds(instance);
            instance.transform.position += new Vector3(root.position.x - bounds.center.x, root.position.y - bounds.min.y, root.position.z - bounds.center.z);
            bounds = WorldBounds(instance);
            Debug.Log($"[NasaHack] Model scale x{scale:0.###}, size {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m");

            instance.AddComponent<DustCover>().amount = dust;
            return instance;
        }

        static void PoseFlattest(GameObject instance, GameObject model)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model))
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) return;

            clip.SampleAnimation(instance, 0f);
            float startHeight = WorldBounds(instance).size.y;
            clip.SampleAnimation(instance, clip.length);
            float endHeight = WorldBounds(instance).size.y;
            float time = endHeight <= startHeight ? clip.length : 0f;
            clip.SampleAnimation(instance, time);
            instance.transform.localRotation = ModelRotation;
            Debug.Log($"[NasaHack] Posed '{clip.name}' at {time:0.##}s (height start {startHeight:0.00} m, end {endHeight:0.00} m)");
        }

        const string LaunchPath = "Assets/Opportunity/Launch/Scenes/Opportunity-Launch.unity";

        // Re-runnable: always replaces Opportunity/Model with a fresh detailed model.
        [MenuItem("NasaHack/Swap Arrival + Legacy to Detailed Rover")]
        public static void SwapToDetailedModel()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[NasaHack] Detailed model not found at " + ModelPath);
                return;
            }

            var hidden = HiddenPartsFromLaunch(model);

            foreach (var path in new[] { ArrivalPath, LegacyPath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var root = GameObject.Find("Opportunity");
                var old = root != null ? root.transform.Find("Model") : null;
                if (old == null)
                {
                    Debug.LogWarning("[NasaHack] No Opportunity/Model in " + path + ", skipped.");
                    continue;
                }

                var oldDust = old.GetComponent<DustCover>();
                float dust = oldDust != null ? oldDust.amount : 0.5f;
                int index = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);

                var instance = AddModel(root.transform, model, dust, out _, hidden, fitWidth: false);
                instance.transform.SetSiblingIndex(index);
                SnapHotspotsToParts(root.transform, instance.transform);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[NasaHack] Swapped to detailed model: " + path);
            }
            EditorSceneManager.OpenScene(ArrivalPath);
        }

        // Dominic already hid the leftover Blender objects in Launch. Reuse his choices.
        // Falls back to hiding "Cube*" and "_Hemi*" objects.
        static string[] HiddenPartsFromLaunch(GameObject model)
        {
            var launch = EditorSceneManager.OpenScene(LaunchPath, OpenSceneMode.Additive);
            try
            {
                foreach (var go in launch.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go) || PrefabUtility.GetCorrespondingObjectFromSource(go) != model) continue;
                    var paths = go.GetComponentsInChildren<Renderer>(true)
                        .Where(r => !r.enabled)
                        .Select(r => AnimationUtility.CalculateTransformPath(r.transform, go.transform))
                        .ToArray();
                    Debug.Log($"[NasaHack] Hiding {paths.Length} parts, copied from Launch: {string.Join(", ", paths)}");
                    return paths;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(launch, true);
            }

            var fallback = model.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Cube") || r.name.StartsWith("_Hemi"))
                .Select(r => AnimationUtility.CalculateTransformPath(r.transform, model.transform))
                .ToArray();
            Debug.Log($"[NasaHack] Launch model not found. Hiding {fallback.Length} Cube/_Hemi parts.");
            return fallback;
        }

        // Moves the Arrival hotspots onto the named parts of the detailed model.
        static void SnapHotspotsToParts(Transform rover, Transform model)
        {
            var hotspots = rover.Find("Hotspots");
            if (hotspots == null) return;
            Snap(hotspots, "Hotspot - Solar panels", NearestPart(model, "panels_"), onTop: true);
            Snap(hotspots, "Hotspot - Pancam (panoramic camera)", FindPart(model, "mast_cam") ?? FindPart(model, "mast_top"), onTop: true);
            Snap(hotspots, "Hotspot - Robotic arm", FindPart(model, "arm_turret") ?? FindPart(model, "arm_wrist"), onTop: false);
            Snap(hotspots, "Hotspot - Wheels", NearestPart(model, "_wheel"), onTop: false);
        }

        // onTop: just above the part. Otherwise on the side of the part facing the player.
        static void Snap(Transform hotspots, string name, Transform part, bool onTop)
        {
            var hotspot = hotspots.Find(name);
            if (hotspot == null || part == null) return;
            var renderers = part.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);

            if (onTop)
            {
                hotspot.position = new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
                return;
            }
            var toPlayer = Head - b.center;
            toPlayer.y = 0f;
            toPlayer.Normalize();
            hotspot.position = b.center + toPlayer * (Mathf.Max(b.extents.x, b.extents.z) + 0.06f);
        }

        static Transform FindPart(Transform model, string name)
        {
            return model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        // The part whose name contains `fragment` that is closest to the player.
        static Transform NearestPart(Transform model, string fragment)
        {
            return model.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.Contains(fragment) && t.GetComponentsInChildren<Renderer>().Any(r => r.enabled))
                .OrderBy(t => Vector3.Distance(t.position, Head))
                .FirstOrDefault();
        }

        // Top of the submesh that uses the "SolarArrays" material, if the model has one.
        static Vector3 SolarPanelPoint(Transform rover, Bounds fallback)
        {
            foreach (var mf in rover.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                if (mr == null || mesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains("Solar")) continue;
                    var b = mesh.GetSubMesh(i).bounds;
                    var top = mf.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                    return top + Vector3.up * 0.08f;
                }
            }
            return new Vector3(fallback.center.x, fallback.center.y + 0.2f * fallback.size.y, fallback.center.z);
        }

        static void AddHotspot(Transform parent, Material mat, string title, string body, Vector3 position)
        {
            var go = new GameObject("Hotspot - " + title);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = 0.09f;
            go.AddComponent<XRSimpleInteractable>();

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = Vector3.one * 0.08f;
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var hotspot = go.AddComponent<InfoHotspot>();
            hotspot.title = title;
            hotspot.body = body;
            hotspot.marker = marker.transform;
            InfoHotspot.BuildCard(hotspot);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        static void SetupMars(Material sky)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.78f, 0.6f, 0.45f);

            var sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.color = new Color(1f, 0.9f, 0.78f);
                sun.intensity = 1.1f;
                sun.shadows = LightShadows.Soft;
            }
        }

        static Material SkyMaterial()
        {
            var path = SettingsDir + "/Mars Sky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Skybox/Procedural"));
            mat.SetColor("_SkyTint", new Color(0.85f, 0.6f, 0.4f));
            mat.SetColor("_GroundColor", new Color(0.45f, 0.3f, 0.2f));
            mat.SetFloat("_AtmosphereThickness", 1.6f);
            mat.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material HotspotMaterial()
        {
            var path = SettingsDir + "/Hotspot.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", UiFactory.Accent);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
