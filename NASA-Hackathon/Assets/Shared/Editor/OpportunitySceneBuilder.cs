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
                "The arm carried a rock grinder and science instruments. These tools examined rocks and soil for clues to past water.",
                new Vector3(bounds.center.x, bounds.min.y + 0.35f * h, bounds.min.z - 0.05f));
            AddHotspot(hotspots, hotspotMat, "Wheels",
                "Six wheels carried Opportunity 45.16 km, the first marathon on another world. It also climbed a 32-degree slope, a record for a Mars rover at the time in 2016.",
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
                "Status: Silent since June 10, 2018\nLanded: January 25, 2004 UTC (January 24 local time)\nPlanned mission: 90 sols. Actual: almost 15 years.",
                32f, FontStyles.Normal);
            var button = UiFactory.AddButton(status, "See How I Got Here", "See how I got here", out _);
            UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(flashback.Play));

            var narration = CreateNarration(new Vector3(-1.7f, 1.5f, 2.3f), hideButtonOnLastBeat: true);
            narration.beats = new[]
            {
                Beat("Perseverance Valley, Mars",
                    "You're on the rim of Endeavour Crater. The rover in front of you is NASA's Opportunity. Its last communication with Earth was on June 10, 2018."),
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
                    "Opportunity was a robotic geologist. NASA sent it to study Martian rocks and soil for evidence of past water. Its mission was surface exploration, which it carried out on Mars."),
                Beat("Why did my mission end?",
                    "In June 2018, a dust storm covered the whole planet and blocked the sunlight my solar panels needed. On June 10, 2018, I went quiet.\n\nTry to wake me up, the way my team did.",
                    waitForEvent: true, showWhileActive: wake.gameObject),
                Beat("My team never gave up",
                    "Over eight months they sent more than a thousand commands. The last ones went out on February 12, 2019. The next day, NASA declared my mission complete."),
                Beat("Why remember me?",
                    "I was built for a 90-sol mission. I lasted almost 15 years and drove 45.16 kilometers. At Meridiani Planum, I found evidence that liquid water had altered rocks long ago.\n\nOn October 11, 2026, NASA's Perseverance rover surpassed my driving-distance record."),
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

        // ---------- Title screen ----------

        const string TitleScenePath = "Assets/Shared/Scenes/TitleScreen.unity";

        [MenuItem("NasaHack/Build Title Screen")]
        public static void BuildTitleScreen()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(TitleScenePath);
            if (GameObject.Find("Title Panel") != null)
            {
                Debug.Log("[NasaHack] Title screen already built, skipped.");
                return;
            }

            SetupMars(SkyMaterial());

            var ground = AssetDatabase.LoadAssetAtPath<Material>(SettingsDir + "/Mars Ground.mat");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground (teleport)";
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            if (ground != null) floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
            floor.AddComponent<TeleportationArea>();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model != null)
            {
                var root = new GameObject("Opportunity").transform;
                root.position = new Vector3(2.2f, 0f, 5f);
                root.rotation = Quaternion.Euler(0f, -35f, 0f);
                AddModel(root, model, 0.5f, out _, HiddenParts(model), fitWidth: false);
            }

            var flow = Object.FindObjectOfType<LoadNextScene>();
            var panel = UiFactory.CreatePanel("Title Panel", null, 900f);
            Place(panel, new Vector3(-0.6f, 1.6f, 2.6f));
            var narration = panel.gameObject.AddComponent<NarrationPanel>();
            narration.titleText = UiFactory.AddText(panel, "Title", "", 64f, FontStyles.Bold);
            narration.titleText.color = UiFactory.Accent;
            narration.bodyText = UiFactory.AddText(panel, "Body", "", 34f, FontStyles.Normal);
            narration.nextButton = UiFactory.AddButton(panel, "Begin Button", "Begin", out var label);
            narration.buttonLabel = label;
            narration.finishLabel = "Begin";
            narration.beats = new[]
            {
                Beat("Abandoned but not Forgotten",
                    "A VR field trip to the machines NASA left behind.\n\n" +
                    "Your first stop: Mars. Opportunity was designed for 90 sols and worked for nearly 15 years. " +
                    "It last communicated with Earth on June 10, 2018. Find out what it discovered and how it got there.\n\n" +
                    "NASA Space Apps 2026. Team: Yayo, Gibson, Dominic, Susan."),
            };
            if (flow != null) UnityEventTools.AddVoidPersistentListener(narration.onFinished, new UnityAction(flow.Load));

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + TitleScenePath);
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

            // Deployed rover = panels spread out = biggest footprint.
            clip.SampleAnimation(instance, 0f);
            var start = WorldBounds(instance).size;
            clip.SampleAnimation(instance, clip.length);
            var end = WorldBounds(instance).size;
            float time = end.x * end.z >= start.x * start.z ? clip.length : 0f;
            clip.SampleAnimation(instance, time);
            instance.transform.localRotation = ModelRotation;
            Debug.Log($"[NasaHack] Posed '{clip.name}' at {time:0.##}s of {clip.length:0.##}s " +
                      $"(start {start.x:0.00}x{start.y:0.00}x{start.z:0.00} m, end {end.x:0.00}x{end.y:0.00}x{end.z:0.00} m)");
        }


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

            var hidden = HiddenParts(model);

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

        const string FlashbackPath = "Assets/Opportunity/NASA-Headquarters/LastSignalFlashback/Scenes/Opportunity-Flashback.unity";
        const string OriginPath = "Assets/Opportunity/Origin/Scenes/Opportunity-Origin.unity";

        // Points Arrival's "See how I got here" at Gibson's flashback scene and puts that scene
        // in Build Settings in place of the empty Origin placeholder.
        [MenuItem("NasaHack/Link Arrival to Gibson's Flashback")]
        public static void LinkFlashback()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!File.Exists(FlashbackPath))
            {
                Debug.LogError("[NasaHack] Gibson's scene not found at " + FlashbackPath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ArrivalPath);
            var flow = Object.FindObjectOfType<LoadNextScene>();
            if (flow == null)
            {
                Debug.LogError("[NasaHack] No Scene Flow (LoadNextScene) in " + ArrivalPath);
                return;
            }
            flow.nextScene = Path.GetFileNameWithoutExtension(FlashbackPath);
            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == OriginPath || s.path == FlashbackPath);
            int arrival = scenes.FindIndex(s => s.path == ArrivalPath);
            scenes.Insert(arrival >= 0 ? arrival + 1 : scenes.Count, new EditorBuildSettingsScene(FlashbackPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[NasaHack] Arrival now loads " + flow.nextScene + ". Build Settings: " +
                      string.Join(" -> ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        // Leftover Blender objects in the detailed FBX (proxy cubes and a big "_root" block).
        // Copied from what Dominic hid in his Launch scene.
        static readonly string[] KnownHiddenParts =
        {
            "_root",
            "_root/body/arm_mount/arm_base/Cube.010",
            "_root/body/arm_mount/arm_base/Cube.010/arm_joint1/Cube.008",
            "_root/body/arm_mount/arm_base/Cube.010/arm_joint1/Cube.008/arm_joint2/Cube.007",
            "_root/body/mast/Cube.012",
            "_root/body/panels_left/panels_left_front/Cube.006",
            "_root/body/panels_right/panels_right_front/Cube.029",
            "_root/body/suspension_left_front/Cube.025",
            "_root/body/suspension_left_front/Cube.025/suspension_left_front_rocker/Cube.022",
            "_root/body/suspension_left_front/Cube.025/suspension_left_front_rocker/Cube.022/suspension_left_front_axl/Cube.005",
            "_root/body/suspension_left_middle_rocker/suspension_left_rear_rocker1/Cube.003",
            "_root/body/suspension_left_middle_rocker/suspension_left_rear_rocker1/suspension_left_rear_rocker2/Cube.021",
            "_root/body/suspension_left_middle_rocker/suspension_left_rear_rocker1/suspension_left_rear_rocker2/Cube.021/suspension_left_rear_axl/Cube.004",
            "_root/body/suspension_right_front/Cube.000",
            "_root/body/suspension_right_front/Cube.000/suspension_right_front_rocker/Cube.001",
            "_root/body/suspension_right_front/Cube.000/suspension_right_front_rocker/Cube.001/suspension_right_front_axl/Cube.009",
            "_root/body/suspension_right_middle_rocker/suspension_right_rear_rocker1/Cube.002",
            "_root/body/suspension_right_middle_rocker/suspension_right_rear_rocker1/suspension_right_rear_rocker2/Cube.020",
            "_root/body/suspension_right_middle_rocker/suspension_right_rear_rocker1/suspension_right_rear_rocker2/Cube.020/suspension_right_rear_axl/Cube",
        };

        static string[] HiddenParts(GameObject model)
        {
            var paths = KnownHiddenParts.Where(p => model.transform.Find(p) != null).ToArray();
            Debug.Log($"[NasaHack] Hiding {paths.Length}/{KnownHiddenParts.Length} leftover parts.");
            return paths;
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
