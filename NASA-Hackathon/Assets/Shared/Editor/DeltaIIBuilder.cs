using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace NasaHack.EditorTools
{
    // Builds a Delta II 7925H (the rocket that launched Opportunity) from generated meshes at real size,
    // plus a launch pad, umbilical tower, LAUNCH button console and countdown board in Opportunity-Launch.
    //
    //   NasaHack > Opportunity > Build Delta II Launch      (safe to re-run; replaces what it built before)
    //
    // Real dimensions used (metres):
    //   Thor first stage: 2.44 dia, ~26 long, "Delta blue".  Nine GEM-46 boosters: 1.17 dia, 14.7 long, white.
    //   Interstage / Delta-K second stage: 2.44 dia.  9.5-ft composite fairing: 2.9 dia.  Overall height ~39.
    [InitializeOnLoad]
    public static partial class DeltaIIBuilder
    {
        // Bump when the builder changes, so Unity rebuilds the launch once on this machine.
        const int BuildVersion = 4;
        static string VersionKey => "NasaHack.DeltaII.BuildVersion." + Application.dataPath.GetHashCode();

        const string Root = "Assets/Opportunity/Launch";
        const string ScenePath = Root + "/Scenes/Opportunity-Launch.unity";
        const string MeshPath = Root + "/Models/DeltaII-Meshes.asset";
        const string PrefabPath = Root + "/Prefabs/Delta II 7925H.prefab";
        const string MatDir = Root + "/Materials";
        const string ParticleTexPath = Root + "/Textures/SoftParticle.png";

        static readonly Vector3 PadPosition = new Vector3(0f, 0f, 90f);
        const float PadTop = 0.8f, MountHeight = 3.2f;

        // Run once automatically when this script first compiles (if nothing is built yet).
        static DeltaIIBuilder()
        {
            string ranKey = "NasaHack.DeltaII.AutoRan.v" + BuildVersion;
            if (Application.isBatchMode || SessionState.GetBool(ranKey, false)) return;
            if (File.Exists(PrefabPath) && EditorPrefs.GetInt(VersionKey, 1) >= BuildVersion) return;
            SessionState.SetBool(ranKey, true);
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += AutoRun; return; }
            Build();
        }

        [MenuItem("NasaHack/Opportunity/Build Delta II Launch")]
        public static void Build()
        {
            try
            {
                var active = EditorSceneManager.GetActiveScene();
                if (active.path != ScenePath)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                var scene = EditorSceneManager.GetActiveScene();

                foreach (var d in new[] { "Models", "Materials", "Prefabs", "Textures" }) EnsureFolder($"{Root}/{d}");
                var mats = new Mats();
                var meshes = BuildMeshes();
                var rocketPrefab = BuildRocketPrefab(meshes, mats);

                foreach (var name in new[] { "Launch Complex", "Launch Console", "Countdown Board" })
                    foreach (var g in scene.GetRootGameObjects().Where(g => g.name == name)) Object.DestroyImmediate(g);

                // ---- Cape Canaveral surroundings, sky and post-processing (DeltaIIBuilder.Environment.cs)
                BuildEnvironment(scene, mats, meshes);

                // ---- Launch complex
                var complex = new GameObject("Launch Complex");
                complex.transform.position = PadPosition;
                BuildPad(complex.transform, mats);
                BuildTower(complex.transform, mats);

                var rocket = (GameObject)PrefabUtility.InstantiatePrefab(rocketPrefab, scene);
                rocket.transform.SetParent(complex.transform, false);
                rocket.transform.localPosition = new Vector3(0f, PadTop + MountHeight + 1.25f, 0f);

                var smoke = MakeSmoke(complex.transform, mats);

                var launch = complex.AddComponent<DeltaIILaunch>();
                launch.rocket = rocket.transform;
                launch.mainEngineFlames = FindAll<ParticleSystem>(rocket, "Flame Main", "Flame Vernier");
                launch.groundLitFlames = FindAll<ParticleSystem>(rocket, "Flame GroundLit");
                launch.airLitFlames = FindAll<ParticleSystem>(rocket, "Flame AirLit");
                launch.groundLitBoosters = rocket.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("GEM-46 Ground-lit")).ToArray();
                launch.engineLight = rocket.GetComponentsInChildren<Light>(true).FirstOrDefault();
                launch.padSmoke = smoke;
                launch.sceneFlow = Object.FindObjectOfType<LoadNextScene>();

                var audio = complex.AddComponent<AudioSource>();
                audio.playOnAwake = true; audio.loop = true; audio.spatialBlend = 0f;
                launch.rumble = complex.AddComponent<RocketRumble>();

                // ---- Player-side props (player starts at the origin facing +Z)
                var (clock, status) = BuildBoard(scene);
                launch.clockText = clock;
                launch.statusText = status;
                BuildConsole(launch, mats);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                EditorPrefs.SetInt(VersionKey, BuildVersion);
                Selection.activeGameObject = complex;
                Debug.Log("[Delta II] Built the Delta II 7925H launch in " + ScenePath +
                          ". Press Play, then press the red LAUNCH button (or L on the keyboard).");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Delta II] Build failed: " + e);
            }
        }

        // =====================================================================================
        // Rocket
        // =====================================================================================

        const float CoreR = 1.22f, BoosterR = 0.585f, FairingR = 1.45f;

        static Dictionary<string, Mesh> BuildMeshes()
        {
            var list = new Dictionary<string, Mesh>
            {
                ["Thrust Section"] = Lathe("Thrust Section", P(0.95f, 0f, CoreR, 1.0f), true, false),
                ["Core"] = Lathe("Core", P(CoreR, 1.0f, CoreR, 26.0f), false, false),
                ["Core Band"] = Lathe("Core Band", P(CoreR + 0.01f, 0f, CoreR + 0.01f, 0.25f), false, false),
                ["Interstage"] = Lathe("Interstage", P(CoreR, 26.0f, CoreR, 29.6f), false, false),
                ["Fairing Boattail"] = Lathe("Fairing Boattail", P(CoreR, 29.6f, FairingR, 30.4f), false, false),
                ["Fairing Body"] = Lathe("Fairing Body", P(FairingR, 30.4f, FairingR, 35.4f), false, false),
                ["Fairing Nose"] = Lathe("Fairing Nose", Ogive(FairingR, 35.4f, 3.5f, 14), false, false),
                ["Booster Skirt"] = Lathe("Booster Skirt", P(0.66f, 0f, BoosterR, 0.6f), true, false),
                ["Booster Body"] = Lathe("Booster Body", P(BoosterR, 0.6f, BoosterR, 12.7f), false, false),
                ["Booster Nose"] = Lathe("Booster Nose", Ogive(BoosterR, 12.7f, 2.0f, 10), false, false),
                ["Nozzle RS-27A"] = Lathe("Nozzle RS-27A", new[] { V(0.62f, -1.25f), V(0.5f, -0.8f), V(0.34f, -0.3f), V(0.22f, 0.1f), V(0.3f, 0.35f) }, false, false),
                ["Nozzle GEM Ground"] = Lathe("Nozzle GEM Ground", new[] { V(0.42f, -0.6f), V(0.32f, -0.3f), V(0.2f, 0f), V(0.24f, 0.15f) }, false, false),
                ["Nozzle GEM Air"] = Lathe("Nozzle GEM Air", new[] { V(0.55f, -1.15f), V(0.42f, -0.65f), V(0.26f, -0.2f), V(0.2f, 0f), V(0.24f, 0.15f) }, false, false),
            };

            if (File.Exists(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);
            bool first = true;
            foreach (var m in list.Values)
            {
                if (first) { AssetDatabase.CreateAsset(m, MeshPath); first = false; }
                else AssetDatabase.AddObjectToAsset(m, MeshPath);
            }
            AssetDatabase.ImportAsset(MeshPath);
            return list;
        }

        static GameObject BuildRocketPrefab(Dictionary<string, Mesh> m, Mats mats)
        {
            var deltaBlue = mats.Get("Delta Blue", new Color(0.36f, 0.62f, 0.70f), 0.45f);
            var white = mats.Get("Rocket White", new Color(0.92f, 0.92f, 0.9f), 0.5f);
            var dark = mats.Get("Interstage Black", new Color(0.09f, 0.09f, 0.1f), 0.35f);
            var metal = mats.Get("Nozzle Metal", new Color(0.22f, 0.2f, 0.19f), 0.6f, 0.85f, doubleSided: true);
            var flameMat = mats.Particle("Flame", additive: true);

            var rocket = new GameObject("Delta II 7925H");
            Part(rocket.transform, "Thrust Section", m, dark, Vector3.zero);
            Part(rocket.transform, "Core", m, deltaBlue, Vector3.zero);
            Part(rocket.transform, "Core Band", m, dark, new Vector3(0f, 25.75f, 0f));
            Part(rocket.transform, "Interstage", m, dark, Vector3.zero);
            Part(rocket.transform, "Fairing Boattail", m, white, Vector3.zero);
            Part(rocket.transform, "Fairing Body", m, white, Vector3.zero);
            Part(rocket.transform, "Fairing Nose", m, white, Vector3.zero);

            // RS-27A main engine + two vernier engines
            Part(rocket.transform, "Nozzle RS-27A", m, metal, Vector3.zero);
            MakeFlame(rocket.transform, "Flame Main", new Vector3(0f, -1.25f, 0f), 0.5f, 1.6f, flameMat);
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                var v = Part(rocket.transform, "Nozzle RS-27A", m, metal, new Vector3(x, 0.3f, 0.45f));
                v.name = "Vernier Nozzle";
                v.transform.localScale = Vector3.one * 0.22f;
                MakeFlame(rocket.transform, "Flame Vernier", new Vector3(x, 0.02f, 0.45f), 0.1f, 0.35f, flameMat);
            }

            // Nine GEM-46 strap-ons around the core: six lit on the ground, three air-lit (every third one).
            float ring = CoreR + BoosterR + 0.03f;
            int ground = 0, air = 0;
            for (int i = 0; i < 9; i++)
            {
                bool airLit = i % 3 == 0;
                float a = (i * 40f + 20f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * ring, 0.55f, Mathf.Cos(a) * ring);
                var b = new GameObject(airLit ? $"GEM-46 Air-lit {++air}" : $"GEM-46 Ground-lit {++ground}");
                b.transform.SetParent(rocket.transform, false);
                b.transform.localPosition = pos;
                Part(b.transform, "Booster Skirt", m, white, Vector3.zero);
                Part(b.transform, "Booster Body", m, white, Vector3.zero);
                Part(b.transform, "Booster Nose", m, white, Vector3.zero);
                Part(b.transform, airLit ? "Nozzle GEM Air" : "Nozzle GEM Ground", m, metal, Vector3.zero);
                MakeFlame(b.transform, airLit ? "Flame AirLit" : "Flame GroundLit",
                          new Vector3(0f, airLit ? -1.15f : -0.6f, 0f), 0.35f, 1.15f, flameMat);
            }

            var lightGo = new GameObject("Engine Glow");
            lightGo.transform.SetParent(rocket.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 400f; // bright enough to light up the whole complex at night
            light.intensity = 0f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(rocket, PrefabPath);
            Object.DestroyImmediate(rocket);
            return prefab;
        }

        static GameObject Part(Transform parent, string mesh, Dictionary<string, Mesh> m, Material mat, Vector3 pos)
        {
            var go = new GameObject(mesh);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = m[mesh];
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static ParticleSystem MakeFlame(Transform parent, string name, Vector3 pos, float radius, float size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // emit straight down

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(22f, 32f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.3f);
            main.startColor = new Color(1f, 0.85f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;

            var em = ps.emission; em.rateOverTime = 160f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = radius;

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.6f, 0.2f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.6f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static ParticleSystem MakeSmoke(Transform parent, Mats mats)
        {
            var go = new GameObject("Pad Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, PadTop + 0.5f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward/outward

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // Smoke is unlit, so at night it's tinted darker, with an orange glow where the flames light it.
            main.startColor = NightLaunch ? new Color(0.5f, 0.45f, 0.43f) : new Color(0.82f, 0.8f, 0.78f);
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;

            var em = ps.emission; em.rateOverTime = 90f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 82f; sh.radius = 3f;

            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 3f; lim.dampen = 0.08f;

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                NightLaunch
                    ? new[] { new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0f), new GradientColorKey(new Color(0.9f, 0.62f, 0.45f), 0.25f), new GradientColorKey(new Color(0.35f, 0.35f, 0.4f), 1f) }
                    : new[] { new GradientColorKey(new Color(1f, 0.75f, 0.5f), 0f), new GradientColorKey(Color.white, 0.15f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mats.Particle("Smoke", additive: false);
            return ps;
        }

        // =====================================================================================
        // Pad, tower, console, board
        // =====================================================================================

        static void BuildPad(Transform complex, Mats mats)
        {
            var concrete = mats.Get("Pad Concrete", new Color(0.62f, 0.61f, 0.58f), 0.15f);
            var steel = mats.Get("Mount Steel", new Color(0.25f, 0.26f, 0.28f), 0.4f, 0.6f);
            var black = mats.Get("Flame Pit", new Color(0.03f, 0.03f, 0.03f), 0.05f);

            Box(complex, "Pad Deck", new Vector3(0f, PadTop / 2f, 0f), new Vector3(30f, PadTop, 30f), concrete);
            Box(complex, "Flame Pit", new Vector3(0f, PadTop + 0.01f, 0f), new Vector3(6.5f, 0.02f, 6.5f), black);
            // Launch mount: four legs and a ring frame the rocket stands on.
            float h = MountHeight, y = PadTop + h / 2f;
            foreach (var (x, z) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
                Box(complex, "Mount Leg", new Vector3(x, y, z), new Vector3(0.7f, h, 0.7f), steel);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, PadTop + h - 0.3f, -3f), new Vector3(6.7f, 0.6f, 0.6f)),
                (new Vector3(0f, PadTop + h - 0.3f,  3f), new Vector3(6.7f, 0.6f, 0.6f)),
                (new Vector3(-3f, PadTop + h - 0.3f, 0f), new Vector3(0.6f, 0.6f, 6.7f)),
                (new Vector3( 3f, PadTop + h - 0.3f, 0f), new Vector3(0.6f, 0.6f, 6.7f)),
            })
                Box(complex, "Mount Frame", pos, size, steel);
        }

        static void BuildTower(Transform complex, Mats mats)
        {
            var red = mats.Get("Tower Steel", new Color(0.62f, 0.22f, 0.14f), 0.3f);
            var tower = new GameObject("Umbilical Tower").transform;
            tower.SetParent(complex, false);
            tower.localPosition = new Vector3(-10f, PadTop, 0f);

            const float W = 3f, H = 44f;
            foreach (var (x, z) in new[] { (-W / 2, -W / 2), (W / 2, -W / 2), (-W / 2, W / 2), (W / 2, W / 2) })
                Box(tower, "Column", new Vector3(x, H / 2f, z), new Vector3(0.35f, H, 0.35f), red);
            for (float y = 3f; y <= H; y += 3f)
            {
                Box(tower, "Beam", new Vector3(0f, y, -W / 2), new Vector3(W, 0.2f, 0.2f), red);
                Box(tower, "Beam", new Vector3(0f, y, W / 2), new Vector3(W, 0.2f, 0.2f), red);
                Box(tower, "Beam", new Vector3(-W / 2, y, 0f), new Vector3(0.2f, 0.2f, W), red);
                Box(tower, "Beam", new Vector3(W / 2, y, 0f), new Vector3(0.2f, 0.2f, W), red);
            }
            // Swing arms reaching toward the rocket (stop short of it so they don't block liftoff).
            foreach (float y in new[] { 18f, 29f, 34f })
                Box(tower, "Swing Arm", new Vector3(4.2f, y, 0f), new Vector3(5.4f, 0.6f, 1.2f), red);
        }

        static (Text clock, Text status) BuildBoard(UnityEngine.SceneManagement.Scene scene)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var board = new GameObject("Countdown Board", typeof(RectTransform));
            var pos = new Vector3(-2.4f, 2.3f, 4.2f);
            board.transform.position = pos;
            board.transform.rotation = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z).normalized);
            board.transform.localScale = Vector3.one * 0.002f;
            var canvas = board.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)board.transform).sizeDelta = new Vector2(1300f, 700f);

            var bg = UiChild(board.transform, "Background", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
            bg.raycastTarget = false;

            var clock = UiChild(board.transform, "Clock", new Vector2(0f, 0.58f), new Vector2(1f, 0.98f)).gameObject.AddComponent<Text>();
            clock.font = font; clock.fontSize = 220; clock.fontStyle = FontStyle.Bold;
            clock.alignment = TextAnchor.MiddleCenter; clock.color = new Color(1f, 0.78f, 0.25f);
            clock.text = "T-10"; clock.raycastTarget = false;

            var status = UiChild(board.transform, "Status", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.58f)).gameObject.AddComponent<Text>();
            status.font = font; status.fontSize = 46; status.alignment = TextAnchor.UpperCenter;
            status.color = Color.white; status.raycastTarget = false;
            status.horizontalOverflow = HorizontalWrapMode.Wrap; status.verticalOverflow = VerticalWrapMode.Overflow;
            status.text = "Press the red LAUNCH button.";
            return (clock, status);
        }

        static void BuildConsole(DeltaIILaunch launch, Mats mats)
        {
            var body = mats.Get("Console Gray", new Color(0.3f, 0.32f, 0.35f), 0.35f, 0.3f);
            var yellow = mats.Get("Hazard Yellow", new Color(0.95f, 0.75f, 0.1f), 0.4f);
            var red = mats.Get("Button Red", new Color(0.85f, 0.05f, 0.04f), 0.7f, 0f, false, new Color(0.6f, 0.02f, 0.0f));

            var console = new GameObject("Launch Console").transform;
            var pos = new Vector3(0.75f, 0f, 1.0f);
            console.position = pos;
            console.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0f, pos.z).normalized); // face the player

            Box(console, "Pedestal", new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 0.4f), body);
            var top = Box(console, "Top Panel", new Vector3(0f, 0.93f, 0f), new Vector3(0.62f, 0.06f, 0.48f), body);
            top.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // front edge (player side) lower

            var buttonRoot = new GameObject("LAUNCH Button");
            buttonRoot.transform.SetParent(top.transform, false);
            buttonRoot.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            buttonRoot.transform.localScale = new Vector3(1f / 0.62f, 1f / 0.06f, 1f / 0.48f); // undo panel scale

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Guard Ring";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(buttonRoot.transform, false);
            ring.transform.localScale = new Vector3(0.2f, 0.008f, 0.2f);
            ring.GetComponent<Renderer>().sharedMaterial = yellow;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Button Cap";
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(buttonRoot.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            cap.transform.localScale = new Vector3(0.13f, 0.022f, 0.13f);
            cap.GetComponent<Renderer>().sharedMaterial = red;

            var col = buttonRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.03f, 0f);
            col.size = new Vector3(0.2f, 0.08f, 0.2f);

            var interactable = buttonRoot.AddComponent<XRSimpleInteractable>();
            var poke = buttonRoot.AddComponent<XRPokeFilter>();
            poke.pokeInteractable = interactable;
            // Press straight down into the button. (pokeConfiguration returns a copy, so set the serialized field.)
            var so = new SerializedObject(poke);
            so.FindProperty("m_PokeConfiguration.m_ConstantValue.m_PokeDirection").intValue = (int)PokeAxis.NegativeY;
            so.ApplyModifiedPropertiesWithoutUndo();
            var button = buttonRoot.AddComponent<LaunchButton>();
            button.launch = launch;
            button.cap = cap.transform;

            // Label
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(console, false);
            label.transform.localPosition = new Vector3(0f, 1.12f, -0.2f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.transform.localScale = Vector3.one * 0.001f;
            label.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)label.transform).sizeDelta = new Vector2(600f, 120f);
            var t = label.AddComponent<Text>();
            t.font = font; t.fontSize = 90; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.85f, 0.2f); t.text = "LAUNCH"; t.raycastTarget = false;
        }

        // =====================================================================================
        // Helpers
        // =====================================================================================

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static RectTransform UiChild(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        static T[] FindAll<T>(GameObject root, params string[] names) where T : Component =>
            root.GetComponentsInChildren<T>(true).Where(c => names.Any(n => c.name.StartsWith(n))).ToArray();

        static Vector2 V(float r, float y) => new Vector2(r, y);
        static Vector2[] P(float r0, float y0, float r1, float y1) => new[] { V(r0, y0), V(r1, y1) };

        static Vector2[] Ogive(float r, float y0, float length, int steps)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float s = i / (float)steps;
                pts.Add(V(r * Mathf.Sqrt(Mathf.Max(0f, 1f - s * s)) * (1f - 0.15f * s), y0 + length * s));
            }
            return pts.ToArray();
        }

        // Revolve a (radius, height) profile around the Y axis.
        static Mesh Lathe(string name, Vector2[] profile, bool capBottom, bool capTop, int seg = 40)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int rows = profile.Length;
            for (int i = 0; i < rows; i++)
            {
                Vector2 a = profile[Mathf.Max(0, i - 1)], b = profile[Mathf.Min(rows - 1, i + 1)];
                Vector2 tangent = (b - a).normalized;
                Vector2 n2 = new Vector2(tangent.y, -tangent.x); // outward for a bottom-to-top profile
                for (int s = 0; s <= seg; s++)
                {
                    float ang = s / (float)seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    verts.Add(new Vector3(profile[i].x * c, profile[i].y, profile[i].x * sn));
                    norms.Add(new Vector3(n2.x * c, n2.y, n2.x * sn).normalized);
                    uvs.Add(new Vector2(s / (float)seg, i / (float)(rows - 1)));
                }
            }
            for (int i = 0; i < rows - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a = i * (seg + 1) + s, b = a + seg + 1;
                    tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }

            void Cap(Vector2 p, bool up)
            {
                if (p.x <= 0.0001f) return;
                int center = verts.Count;
                verts.Add(new Vector3(0f, p.y, 0f)); norms.Add(up ? Vector3.up : Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= seg; s++)
                {
                    float ang = s / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(p.x * Mathf.Cos(ang), p.y, p.x * Mathf.Sin(ang)));
                    norms.Add(up ? Vector3.up : Vector3.down);
                    uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang)));
                }
                for (int s = 0; s < seg; s++)
                {
                    if (up) tris.AddRange(new[] { center, center + s + 2, center + s + 1 });
                    else tris.AddRange(new[] { center, center + s + 1, center + s + 2 });
                }
            }
            if (capBottom) Cap(profile[0], false);
            if (capTop) Cap(profile[rows - 1], true);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // URP materials, created once and updated on re-run.
        class Mats
        {
            public Material Get(string name, Color color, float smoothness, float metallic = 0f, bool doubleSided = false, Color? emission = null)
            {
                var path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
                if (emission.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", emission.Value);
                }
                EditorUtility.SetDirty(mat);
                return mat;
            }

            public Material Particle(string name, bool additive)
            {
                var path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetTexture("_BaseMap", SoftTexture());
                // Flames are HDR-bright so the bloom post effect makes them glow.
                mat.SetColor("_BaseColor", additive ? new Color(2.6f, 2.4f, 2.2f, 1f) : Color.white);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", additive ? 2f : 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Texture2D SoftTexture()
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexPath);
                if (tex) return tex;
                const int N = 128;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f - 0.5f, N / 2f - 0.5f)) / (N / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(ParticleTexPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(ParticleTexPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(ParticleTexPath);
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexPath);
            }
        }
    }
}
