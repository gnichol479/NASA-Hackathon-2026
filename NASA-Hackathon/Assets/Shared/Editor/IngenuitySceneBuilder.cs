using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack.EditorTools
{
    // Builds Ingenuity's present-day scenes (Arrival, Legacy) the same way as Opportunity's.
    // Skips a scene that already has an "Ingenuity" object, so re-running never overwrites edits.
    // Run NasaHack > Mars Environment afterwards to give them the shared Mars terrain and sky.
    public static class IngenuitySceneBuilder
    {
        const string ArrivalPath = "Assets/Ingenuity/Arrival/Scenes/Ingenuity-Arrival.unity";
        const string LegacyPath = "Assets/Ingenuity/Legacy/Scenes/Ingenuity-Legacy.unity";
        const string LandingPath = "Assets/Ingenuity/Landing/Scenes/Ingenuity-Landing.unity";
        const string LaunchPath = "Assets/Ingenuity/Launch/Scenes/Ingenuity-Launch.unity";
        const string TitlePath = "Assets/Shared/Scenes/TitleScreen-Night.unity";
        const string RoverPath = "Assets/Ingenuity/Models/Perseverance.glb";
        const string ModelPath = "Assets/Ingenuity/Models/Ingenuity-Helicopter.glb";
        const string RigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
        const string GroundMatPath = "Assets/Shared/Settings/Mars Ground.mat";
        static readonly Vector3 HeliPosition = new Vector3(0f, 0f, 1.5f);
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        [MenuItem("NasaHack/Ingenuity/Build Arrival + Legacy")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (model == null || rig == null)
            {
                Debug.LogError("[NasaHack] Missing " + (model == null ? ModelPath : RigPath));
                return;
            }

            BuildArrival(model, rig);
            BuildLegacy(model, rig);
            AddToBuildSettings(ArrivalPath, LegacyPath);
            EditorSceneManager.OpenScene(ArrivalPath);
            Debug.Log("[NasaHack] Ingenuity Arrival + Legacy built. Next: NasaHack > Mars Environment > NASA terrain + Mars Landscape 3D textures and rocks.");
        }

        // ---------- Arrival ----------

        static void BuildArrival(GameObject model, GameObject rig)
        {
            if (!OpenOrCreate(ArrivalPath, rig, "Ingenuity-Flashback", out var scene)) return;

            var heli = PlaceHelicopter(model, 0.35f);
            var mat = OpportunitySceneBuilder.HotspotMaterial();
            var hotspots = new GameObject("Hotspots").transform;
            hotspots.SetParent(heli, false);
            var m = heli.Find("Model");
            Hotspot(hotspots, mat, m, "rotors_01", false, "Rotor blades",
                "Two rotors, 1.2 m across, spin in opposite directions at about 2,400 rpm. Mars air is about 1% as dense as Earth's, so they spin far faster than a helicopter's on Earth.");
            Hotspot(hotspots, mat, m, "solar_panel", true, "Solar panel",
                "Sitting above the rotors, it recharged Ingenuity's batteries every day. Keeping warm through the freezing Martian night used much of that energy.");
            Hotspot(hotspots, mat, m, NearestLeg(m), false, "Landing legs",
                "Four legs absorbed every touchdown. On flight 72 a rough landing damaged its rotor blades and ended its flying days.");
            Hotspot(hotspots, mat, m, "cams", false, "Cameras",
                "A black-and-white navigation camera watched the ground to steer each flight. A color camera took photos of Mars from the air.");

            var flow = Object.FindObjectOfType<LoadNextScene>();
            var flashback = new GameObject("Flashback").AddComponent<FlashbackTransition>();
            flashback.startMode = FlashbackTransition.StartMode.Present;
            flashback.sceneFlow = flow;

            var status = UiFactory.CreatePanel("Status Panel", null, 700f);
            OpportunitySceneBuilder.Place(status, new Vector3(1.3f, 1.3f, 2.0f));
            UiFactory.AddText(status, "Title", "Ingenuity Mars Helicopter", 50f, FontStyles.Bold).color = UiFactory.Accent;
            UiFactory.AddText(status, "Body",
                "Status: Grounded since January 18, 2024\nFirst flight: April 19, 2021\nPlanned: 5 flights. Flown: 72.",
                32f, FontStyles.Normal);
            var button = UiFactory.AddButton(status, "See How I Got Here", "See how I got here", out _);
            UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(flashback.Play));

            var narration = OpportunitySceneBuilder.CreateNarration(new Vector3(-1.3f, 1.4f, 2.0f), hideButtonOnLastBeat: true);
            narration.beats = new[]
            {
                OpportunitySceneBuilder.Beat("Jezero Crater, Mars",
                    "This small helicopter is NASA's Ingenuity. It rode to Mars folded under the belly of the Perseverance rover, and became the first aircraft to make a powered, controlled flight on another planet."),
                OpportunitySceneBuilder.Beat("Explore",
                    "Point at the glowing markers to learn what each part did. When you're ready, ask Ingenuity how it got here."),
            };

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + ArrivalPath);
        }

        // ---------- Legacy ----------

        static void BuildLegacy(GameObject model, GameObject rig)
        {
            if (!OpenOrCreate(LegacyPath, rig, "TitleScreen-Night", out var scene)) return;

            var heli = PlaceHelicopter(model, 0.55f);
            var spinner = heli.GetComponentInChildren<RotorSpinner>();

            var flow = Object.FindObjectOfType<LoadNextScene>();
            var flashback = new GameObject("Flashback").AddComponent<FlashbackTransition>();
            flashback.startMode = FlashbackTransition.StartMode.Returning;

            var narration = OpportunitySceneBuilder.CreateNarration(new Vector3(-1.3f, 1.4f, 2.0f), hideButtonOnLastBeat: false);
            narration.finishLabel = "Return to Earth";
            UnityEventTools.AddVoidPersistentListener(narration.onFinished, new UnityAction(flow.Load));

            var panel = UiFactory.CreatePanel("Flight Control Panel", null, 640f);
            OpportunitySceneBuilder.Place(panel, new Vector3(1.3f, 1.3f, 2.0f));
            UiFactory.AddText(panel, "Title", "Flight control", 44f, FontStyles.Bold).color = UiFactory.Accent;
            var statusText = UiFactory.AddText(panel, "Status", "", 32f, FontStyles.Normal);
            var button = UiFactory.AddButton(panel, "Spin Up Button", "Spin up the rotors", out var label);
            var attempt = panel.gameObject.AddComponent<RotorSpinUpAttempt>();
            attempt.narration = narration;
            attempt.rotors = spinner;
            attempt.button = button;
            attempt.buttonLabel = label;
            attempt.status = statusText;

            narration.beats = new[]
            {
                OpportunitySceneBuilder.Beat("Why was I left behind?",
                    "I was a technology test, built to prove that flight on Mars was possible. Like the rover that carried me here, I was never meant to come home."),
                OpportunitySceneBuilder.Beat("Why did my mission end?",
                    "On January 18, 2024, I made my 72nd flight. When I came down, one or more of my rotor blades was damaged.\n\nTry to fly me, the way my team hoped I still could.",
                    waitForEvent: true, showWhileActive: panel.gameObject),
                OpportunitySceneBuilder.Beat("Grounded for good",
                    "On January 25, 2024, NASA announced that my flying days were over. I was planned for 5 flights in 30 days. I flew 72, over almost three years."),
                OpportunitySceneBuilder.Beat("Why remember me?",
                    "I flew for more than two hours in total and covered about 17 km, 14 times farther than planned. I proved that flying is a way to explore other worlds. NASA's Dragonfly rotorcraft, headed for Saturn's moon Titan, follows the path I opened."),
            };

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + LegacyPath);
        }

        // ---------- Title screen models ----------

        const string InSightModelPath = "Assets/InSight/Models/InSight-Lander.glb";

        // Adds InSight and Ingenuity to the existing title screen next to Opportunity, without
        // rebuilding it (a rebuild would also re-point the mission cards). Re-running replaces them.
        [MenuItem("NasaHack/Title: Add InSight + Ingenuity Models")]
        public static void AddTitleModels()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(TitlePath);

            // InSight: far right, mirroring Opportunity on the left. Real width with solar arrays ~6 m.
            TitleModel(InSightModelPath, "InSight", new Vector3(2.7f, 0f, 8.5f), 160f, 6.0f, 0.55f, 4f);
            // Ingenuity: real size (0.5 m tall), so closer in, visible under the cards on the right.
            TitleModel(ModelPath, "Ingenuity", new Vector3(1.2f, 0f, 3.5f), -20f, 0f, 0.45f, 1.2f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Title: InSight and Ingenuity added. Drag them in the Scene view to fine-tune.");
        }

        // fitWidth 0 = keep the model's native (real) size.
        static void TitleModel(string modelPath, string name, Vector3 pos, float yaw, float fitWidth, float dust, float clearRadius)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("[NasaHack] Title: model not found " + modelPath);
                return;
            }
            var objName = name + " (title)";
            var old = GameObject.Find(objName);
            if (old != null) Object.DestroyImmediate(old);

            // Several terrains overlap here (title landscape, near and far Mars terrain). Stand on the highest surface.
            float ground = float.NegativeInfinity;
            foreach (var t in Terrain.activeTerrains)
            {
                if (!t.gameObject.activeInHierarchy || t.terrainData == null) continue;
                var o = t.transform.position;
                var size = t.terrainData.size;
                if (pos.x < o.x || pos.x > o.x + size.x || pos.z < o.z || pos.z > o.z + size.z) continue;
                ground = Mathf.Max(ground, t.SampleHeight(pos) + o.y);
            }
            pos.y = float.IsNegativeInfinity(ground) ? 0f : ground;

            var root = new GameObject(objName).transform;
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            instance.name = "Model";
            var b = OpportunitySceneBuilder.WorldBounds(instance);
            if (fitWidth > 0f)
            {
                instance.transform.localScale *= fitWidth / Mathf.Max(b.size.x, b.size.z);
                b = OpportunitySceneBuilder.WorldBounds(instance);
            }
            instance.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            instance.AddComponent<DustCover>().amount = dust;
            b = OpportunitySceneBuilder.WorldBounds(instance);
            Debug.Log($"[NasaHack] Title: {name} at {pos}, size {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m");

            // Hide scattered rocks that would poke through the model.
            foreach (var t in Object.FindObjectsOfType<Transform>())
            {
                if (!t.name.StartsWith("Rock") || t.IsChildOf(root)) continue;
                var flat = new Vector3(t.position.x - pos.x, 0f, t.position.z - pos.z);
                if (flat.magnitude < clearRadius) t.gameObject.SetActive(false);
            }
        }

        // ---------- Landing / first flight ----------

        [MenuItem("NasaHack/Ingenuity/Build Landing + Link Chain")]
        public static void BuildLandingAndLink()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var roverModel = AssetDatabase.LoadAssetAtPath<GameObject>(RoverPath);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (model == null || roverModel == null || rig == null)
            {
                Debug.LogError("[NasaHack] Missing " + (model == null ? ModelPath : roverModel == null ? RoverPath : RigPath));
                return;
            }

            BuildLanding(model, roverModel, rig);
            AddToBuildSettings(LandingPath);
            // Chain: Title -> Arrival -> Flashback -> Launch -> Landing -> Legacy -> Title
            SetNext(LaunchPath, "TitleScreen-Night", "Ingenuity-Landing");
            SetNext(TitlePath, "Ingenuity-Flashback", "Ingenuity-Arrival");
            EditorSceneManager.OpenScene(LandingPath);
            Debug.Log("[NasaHack] Ingenuity Landing built and chain linked. Next: NasaHack > Mars Environment > NASA terrain + Mars Landscape 3D textures and rocks.");
        }

        static void BuildLanding(GameObject model, GameObject roverModel, GameObject rig)
        {
            if (!OpenOrCreate(LandingPath, rig, "Ingenuity-Legacy", out var scene)) return;

            var spot = new Vector3(0f, 0f, 3.2f);
            // Perseverance, scaled to its real ~3 m length, parked over the helicopter.
            var roverRoot = new GameObject("Perseverance").transform;
            roverRoot.position = spot;
            roverRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            var rover = (GameObject)PrefabUtility.InstantiatePrefab(roverModel, roverRoot);
            rover.name = "Model";
            var rb = OpportunitySceneBuilder.WorldBounds(rover);
            float scale = 3.0f / Mathf.Max(rb.size.x, rb.size.z);
            rover.transform.localScale *= scale;
            rb = OpportunitySceneBuilder.WorldBounds(rover);
            rover.transform.position += new Vector3(spot.x - rb.center.x, spot.y - rb.min.y, spot.z - rb.center.z);
            rb = OpportunitySceneBuilder.WorldBounds(rover);
            Debug.Log($"[NasaHack] Perseverance scale x{scale:0.###}, size {rb.size.x:0.00} x {rb.size.y:0.00} x {rb.size.z:0.00} m");

            // Ingenuity on the ground under the rover's belly, as it was dropped on April 3, 2021.
            var heliRoot = new GameObject("Ingenuity").transform;
            heliRoot.position = spot;
            var heli = (GameObject)PrefabUtility.InstantiatePrefab(model, heliRoot);
            heli.name = "Model";
            var hb = OpportunitySceneBuilder.WorldBounds(heli);
            heli.transform.position += new Vector3(spot.x - hb.center.x, spot.y - hb.min.y, spot.z - hb.center.z);
            var spinner = heli.AddComponent<RotorSpinner>();

            // No flashback color effect here: it made the panel text hard to read (same as Opportunity's Landing).
            var flow = Object.FindObjectOfType<LoadNextScene>();
            var narration = OpportunitySceneBuilder.CreateNarration(new Vector3(-1.6f, 1.5f, 2.3f), hideButtonOnLastBeat: false);
            narration.finishLabel = "Continue";
            UnityEventTools.AddVoidPersistentListener(narration.onFinished, new UnityAction(flow.Load));

            var panel = UiFactory.CreatePanel("Flight Panel", null, 640f);
            OpportunitySceneBuilder.Place(panel, new Vector3(1.6f, 1.4f, 2.3f));
            UiFactory.AddText(panel, "Title", "First flight", 44f, FontStyles.Bold).color = UiFactory.Accent;
            var statusText = UiFactory.AddText(panel, "Status", "", 32f, FontStyles.Normal);
            var button = UiFactory.AddButton(panel, "Fly Button", "Spin up and fly", out var label);
            var flight = panel.gameObject.AddComponent<IngenuityFirstFlight>();
            flight.narration = narration;
            flight.helicopter = heliRoot;
            flight.rotors = spinner;
            flight.rover = roverRoot;
            flight.button = button;
            flight.buttonLabel = label;
            flight.status = statusText;

            narration.beats = new[]
            {
                OpportunitySceneBuilder.Beat("Jezero Crater, February 2021",
                    "I landed on Mars folded under Perseverance's belly. On April 3, 2021, the rover set me down on the crater floor and drove away.",
                    waitForEvent: true),
                OpportunitySceneBuilder.Beat("April 19, 2021",
                    "Now it's your turn. Spin up my rotors and make the first powered, controlled flight on another planet.",
                    waitForEvent: true),
                OpportunitySceneBuilder.Beat("From 5 flights to 72",
                    "I was built to try just five flights. After this one worked, I became Perseverance's aerial scout, flying ahead to map the way."),
            };

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NasaHack] Built " + LandingPath);
        }

        // Changes one scene's LoadNextScene target (only the one that currently points at `from`).
        static void SetNext(string scenePath, string from, string to)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath);
            bool changed = false;
            foreach (var flow in Object.FindObjectsOfType<LoadNextScene>())
            {
                if (flow.nextScene != from) continue;
                flow.nextScene = to;
                EditorUtility.SetDirty(flow);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[NasaHack] {Path.GetFileNameWithoutExtension(scenePath)}: {from} -> {to}");
            }
            else Debug.Log($"[NasaHack] {Path.GetFileNameWithoutExtension(scenePath)}: no flow pointing at {from}, left unchanged.");
        }

        // LoadNextScene skips on ANY key unless a scene opts out. Arrival and Legacy use F/Enter/U
        // (and the XR Device Simulator uses many keys), so they opt out: only N skips there.
        [MenuItem("NasaHack/Reserve Gameplay Keys in Arrival + Legacy Scenes")]
        public static void ReserveGameplayKeys()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var paths = new[]
            {
                "Assets/Opportunity/Arrival/Scenes/Opportunity-Arrival.unity",
                "Assets/Opportunity/Legacy/Scenes/Opportunity-Legacy.unity",
                ArrivalPath,
                LegacyPath,
                LandingPath,
            };
            foreach (var path in paths.Where(File.Exists))
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var flow in Object.FindObjectsOfType<LoadNextScene>())
                {
                    flow.requireDebugKeyForDemoSkip = true;
                    EditorUtility.SetDirty(flow);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[NasaHack] Gameplay keys reserved (only N skips) in " + path);
            }
            EditorSceneManager.OpenScene(ArrivalPath);
        }

        // ---------- shared ----------

        // Opens the scene, or creates it with light, XR rig, floor and Scene Flow. False if already built.
        static bool OpenOrCreate(string path, GameObject rig, string nextScene, out Scene scene)
        {
            if (File.Exists(path))
            {
                scene = EditorSceneManager.OpenScene(path);
                if (GameObject.Find("Ingenuity") != null)
                {
                    Debug.Log("[NasaHack] " + path + " already built, skipped.");
                    return false;
                }
            }
            else
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                PrefabUtility.InstantiatePrefab(rig);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Ground (teleport)";
                floor.transform.localScale = new Vector3(10f, 1f, 10f);
                var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
                if (ground != null) floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
                floor.AddComponent<TeleportationArea>();
                new GameObject("Scene Flow").AddComponent<LoadNextScene>();
                EditorSceneManager.SaveScene(scene, path);
            }

            var flow = Object.FindObjectOfType<LoadNextScene>();
            if (flow == null) flow = new GameObject("Scene Flow").AddComponent<LoadNextScene>();
            flow.nextScene = nextScene;
            flow.requireDebugKeyForDemoSkip = true; // F/Enter/U are gameplay keys here, only N skips
            EditorUtility.SetDirty(flow);
            return true;
        }

        // Real-size NASA model (rotors 1.2 m), standing on the ground in front of the player.
        static Transform PlaceHelicopter(GameObject model, float dust)
        {
            var root = new GameObject("Ingenuity").transform;
            root.position = HeliPosition;
            root.rotation = Quaternion.Euler(0f, -30f, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            instance.name = "Model";
            var b = OpportunitySceneBuilder.WorldBounds(instance);
            instance.transform.position += new Vector3(HeliPosition.x - b.center.x, HeliPosition.y - b.min.y, HeliPosition.z - b.center.z);
            instance.AddComponent<DustCover>().amount = dust;
            instance.AddComponent<RotorSpinner>();
            return root;
        }

        // onTop: just above the part. Otherwise on the side of the part facing the player.
        static void Hotspot(Transform parent, Material mat, Transform model, string part, bool onTop, string title, string body)
        {
            var t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == part);
            var renderers = t != null ? t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray() : new Renderer[0];
            Vector3 pos;
            if (renderers.Length == 0)
            {
                pos = HeliPosition + Vector3.up * 0.4f;
            }
            else
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
                if (onTop) pos = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
                else
                {
                    var toPlayer = Head - b.center;
                    toPlayer.y = 0f;
                    pos = b.center + toPlayer.normalized * (Mathf.Min(Mathf.Max(b.extents.x, b.extents.z), 0.6f) + 0.05f);
                }
            }
            OpportunitySceneBuilder.AddHotspot(parent, mat, title, body, pos);
        }

        static string NearestLeg(Transform model)
        {
            return model.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("leg_"))
                .OrderBy(t => Vector3.Distance(t.position, Head))
                .Select(t => t.name)
                .FirstOrDefault() ?? "leg_01";
        }

        static void AddToBuildSettings(params string[] paths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var p in paths)
                if (File.Exists(p) && scenes.All(s => s.path != p))
                    scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
