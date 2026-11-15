using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NasaHack.EditorTools
{
    // InSight's launch, built with the same tools as Opportunity's (the class keeps its Delta II name).
    //
    //   NasaHack > InSight > Build Atlas V Launch     ->  Assets/InSight/Launch/Scenes/InSight-Launch.unity
    //
    // What really happened: May 5, 2018, 4:05 a.m. PDT, Vandenberg Air Force Base, Space Launch Complex 3E.
    // Rocket: Atlas V 401 (no strap-on boosters). First interplanetary launch from the U.S. West Coast,
    // before dawn and in thick coastal fog.
    //
    // Layout: the player stands at the origin facing +Z. The pad is 110 m ahead and the Pacific is beyond it.
    // Real sizes used (metres): Atlas booster 3.81 dia x 32.5 long, Centaur 3.05 dia, 4-m fairing 4.2 dia, 58.3 tall.
    public static partial class DeltaIIBuilder
    {
        const string LanderSource = "Assets/InSight/Models/InSight-Lander.glb";

        // Waning gibbous moon ahead and to the right of the pad, haloed by the fog. Stars only show high up.
        static readonly SkyLook VandenbergSky = new SkyLook
        {
            moonDir = new Vector3(0.42f, 0.47f, 0.78f).normalized,
            moonSunAngle = 55f,
            zenith = new Color(0.012f, 0.016f, 0.03f),
            horizon = new Color(0.085f, 0.1f, 0.13f),
            cityGlow = new Color(0.03f, 0.022f, 0.012f),
            cityDir = new Vector3(-0.7f, 0f, -0.7f),
            starLow = 0.25f, starHigh = 0.8f, starGain = 0.6f, milkyWayGain = 0.25f,
            haloScale = 5f, halo = new Color(0.06f, 0.066f, 0.082f),
        };

        static readonly Vector3 AtlasAim = new Vector3(0f, 37f, 110f);
        static readonly Vector3 DisplayPos = new Vector3(10.5f, 0f, 2.5f);
        static readonly Vector3[] VandenbergLamps =
        {
            new Vector3(-5.6f, 0f, 18f), new Vector3(5.6f, 0f, 18f),
            new Vector3(-5.6f, 0f, 34f), new Vector3(5.6f, 0f, 34f),
            new Vector3(-5.6f, 0f, 62f), new Vector3(5.6f, 0f, 62f),
        };

        // =====================================================================================
        // Atlas V 401
        // =====================================================================================

        const float AtlasR = 1.905f, CentaurR = 1.525f, AtlasFairingR = 2.1f;

        static Dictionary<string, Mesh> BuildAtlasMeshes()
        {
            var list = new Dictionary<string, Mesh>
            {
                ["Aft Skirt"] = Lathe("Aft Skirt", P(1.7f, 0f, AtlasR, 1.3f), true, false),
                ["Booster"] = Lathe("Booster", P(AtlasR, 1.3f, AtlasR, 32.5f), false, false),
                ["Booster Band"] = Lathe("Booster Band", P(AtlasR + 0.012f, 0f, AtlasR + 0.012f, 0.35f), false, false),
                ["Interstage"] = Lathe("Interstage", new[] { V(AtlasR, 32.5f), V(AtlasR, 33.6f), V(CentaurR, 36.2f) }, false, false),
                ["Centaur"] = Lathe("Centaur", P(CentaurR, 36.2f, CentaurR, 45.4f), false, false),
                ["Centaur Band"] = Lathe("Centaur Band", P(CentaurR + 0.012f, 0f, CentaurR + 0.012f, 0.25f), false, false),
                ["Fairing Boattail"] = Lathe("Fairing Boattail", P(CentaurR, 45.4f, AtlasFairingR, 46.4f), false, false),
                ["Fairing Body"] = Lathe("Fairing Body", P(AtlasFairingR, 46.4f, AtlasFairingR, 52.6f), false, false),
                ["Fairing Nose"] = Lathe("Fairing Nose", Ogive(AtlasFairingR, 52.6f, 5.7f, 16), false, false),
                ["Fairing Band"] = Lathe("Fairing Band", P(AtlasFairingR + 0.012f, 0f, AtlasFairingR + 0.012f, 0.16f), false, false),
                ["Feedline"] = Lathe("Feedline", P(0.2f, 3f, 0.2f, 31.5f), true, true, 14),
                ["Nozzle RD-180"] = Lathe("Nozzle RD-180", new[] { V(0.74f, -1.9f), V(0.62f, -1.25f), V(0.44f, -0.55f), V(0.28f, 0.05f), V(0.36f, 0.4f) }, false, false),
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

        static GameObject BuildAtlasPrefab(Dictionary<string, Mesh> m, Mats mats)
        {
            var bronze = mats.Get("Atlas Bronze", new Color(0.62f, 0.39f, 0.21f), 0.5f, 0.55f);
            var white = mats.Get("Rocket White", new Color(0.92f, 0.92f, 0.9f), 0.5f);
            var dark = mats.Get("Interstage Black", new Color(0.09f, 0.09f, 0.1f), 0.35f);
            var trim = mats.Get("Rocket Trim", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.3f);
            var metal = mats.Get("Nozzle Metal", new Color(0.22f, 0.2f, 0.19f), 0.6f, 0.85f, doubleSided: true);
            var flameMat = mats.Particle("Flame", additive: true);

            var rocket = new GameObject("Atlas V 401");
            var t = rocket.transform;
            Part(t, "Aft Skirt", m, trim, Vector3.zero);
            Part(t, "Booster", m, bronze, Vector3.zero);
            Part(t, "Booster Band", m, dark, new Vector3(0f, 1.3f, 0f));
            Part(t, "Booster Band", m, white, new Vector3(0f, 30.6f, 0f));
            Part(t, "Booster Band", m, dark, new Vector3(0f, 32.15f, 0f));
            Part(t, "Interstage", m, white, Vector3.zero);
            Part(t, "Centaur", m, white, Vector3.zero);
            Part(t, "Centaur Band", m, dark, new Vector3(0f, 36.2f, 0f));
            Part(t, "Centaur Band", m, trim, new Vector3(0f, 45.1f, 0f));
            Part(t, "Fairing Boattail", m, white, Vector3.zero);
            Part(t, "Fairing Body", m, white, Vector3.zero);
            Part(t, "Fairing Nose", m, white, Vector3.zero);
            Part(t, "Fairing Band", m, trim, new Vector3(0f, 46.4f, 0f));
            Part(t, "Fairing Band", m, trim, new Vector3(0f, 52.5f, 0f));

            // The liquid-oxygen feedline and a cable raceway run up the outside of the booster.
            var feed = Part(t, "Feedline", m, trim, Vector3.zero);
            feed.transform.localPosition = Quaternion.Euler(0f, 205f, 0f) * new Vector3(0f, 0f, AtlasR + 0.24f);
            var raceway = Box(t, "Raceway", Vector3.zero, new Vector3(0.3f, 29f, 0.14f), trim);
            NoCollider(raceway);
            raceway.transform.localRotation = Quaternion.Euler(0f, 150f, 0f);
            raceway.transform.localPosition = raceway.transform.localRotation * new Vector3(0f, 17f, AtlasR + 0.05f);

            // One RD-180 engine with two nozzles.
            foreach (float x in new[] { -0.88f, 0.88f })
            {
                Part(t, "Nozzle RD-180", m, metal, new Vector3(x, 0f, 0f));
                MakeFlame(t, "Flame Main", new Vector3(x, -1.9f, 0f), 0.6f, 2.0f, flameMat);
            }

            // Kerosene and liquid oxygen burn much cleaner than solid boosters: a thinner trail.
            var trail = MakeExhaustTrail(t, mats);
            var em = trail.emission; em.rateOverTime = 24f; em.rateOverDistance = 0.16f;
            MakeEngineFlare(t, flameMat);

            var lightGo = new GameObject("Engine Glow");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, -4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 450f;
            light.intensity = 0f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(rocket, PrefabPath);
            Object.DestroyImmediate(rocket);
            return prefab;
        }

        static void BuildAtlasTower(Transform complex, Mats mats)
        {
            var steel = mats.Get("Tower Gray", new Color(0.6f, 0.62f, 0.65f), 0.35f, 0.3f);
            var tower = new GameObject("Umbilical Tower").transform;
            tower.SetParent(complex, false);
            tower.localPosition = new Vector3(-12f, PadTop, 0f);

            const float W = 4f, H = 60f;
            foreach (var (x, z) in new[] { (-W / 2, -W / 2), (W / 2, -W / 2), (-W / 2, W / 2), (W / 2, W / 2) })
                Box(tower, "Column", new Vector3(x, H / 2f, z), new Vector3(0.4f, H, 0.4f), steel);
            for (float y = 4f; y <= H; y += 4f)
            {
                Box(tower, "Beam", new Vector3(0f, y, -W / 2), new Vector3(W, 0.22f, 0.22f), steel);
                Box(tower, "Beam", new Vector3(0f, y, W / 2), new Vector3(W, 0.22f, 0.22f), steel);
                Box(tower, "Beam", new Vector3(-W / 2, y, 0f), new Vector3(0.22f, 0.22f, W), steel);
                Box(tower, "Beam", new Vector3(W / 2, y, 0f), new Vector3(0.22f, 0.22f, W), steel);
            }
            foreach (float y in new[] { 22f, 40f, 50f })
                Box(tower, "Swing Arm", new Vector3(5.6f, y, 0f), new Vector3(7.4f, 0.7f, 1.4f), steel);
        }

        static void ConfigureInSightLaunch(DeltaIILaunch launch)
        {
            launch.liftoffAcceleration = 3f;   // an Atlas V 401 leaves the pad slowly
            launch.maxAcceleration = 26f;
            launch.flightSeconds = 30f;
            launch.engineLightIntensity = 4500f;
            launch.idleMessage =
                "ATLAS V 401  •  INSIGHT MARS LANDER\n" +
                "Vandenberg Air Force Base, Space Launch Complex 3E  •  May 5, 2018, 4:05 a.m. PDT\n\n" +
                "Press the red LAUNCH button to start the countdown.";
            launch.ignitionMessage = "RD-180 main engine start!";
            launch.liftoffMessage = "LIFTOFF!\nAtlas V lifts off carrying the InSight lander to Mars.";
            launch.pitchMessage = "Heading south over the Pacific Ocean.\nThe first launch to another planet from the West Coast.";
            launch.stagingMessage = "Climbing into the fog.\nOn launch morning, thick fog hid the rocket from most people watching.";
            launch.cruiseMessage = "InSight is on its way to Mars!\nLanding: Elysium Planitia, November 26, 2018.\n\n(Real launch timeline sped up.)";
        }

        // =====================================================================================
        // Vandenberg: foggy coastal hills before dawn
        // =====================================================================================

        static void BuildVandenbergEnvironment(UnityEngine.SceneManagement.Scene scene, Mats mats, Dictionary<string, Mesh> meshes)
        {
            foreach (var name in new[] { EnvRoot, "Post Processing" })
                foreach (var g in scene.GetRootGameObjects().Where(g => g.name == name)) Object.DestroyImmediate(g);
            EnsureFolder(Root + "/Settings");

            var noise = NoiseTexture();
            var env = new GameObject(EnvRoot).transform;

            // ---------------------------------------------------------------- Ground and ocean
            var groundMat = Textured(mats.Get("Coastal Ground", new Color(0.36f, 0.37f, 0.27f), 0.08f), noise, new Vector2(90f, 90f));
            var ground = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Ground"));
            if (ground)
            {
                ground.transform.position = new Vector3(0f, 0f, 20f);
                ground.transform.localScale = new Vector3(90f, 1f, 90f); // 900 m x 900 m, the teleport surface
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            }
            var farLand = Textured(mats.Get("Far Land", new Color(0.3f, 0.32f, 0.24f), 0.05f), noise, new Vector2(600f, 600f));
            Quad(env, "Far Land", farLand, new Vector3(-7000, -0.6f, -9000), new Vector3(7000, -0.6f, -9000), new Vector3(-7000, -0.6f, 560), new Vector3(7000, -0.6f, 560), 0f);
            var water = mats.Get("Pacific Water", new Color(0.05f, 0.14f, 0.2f), 0.93f);
            water.SetTexture("_BumpMap", WaterNormalTexture());
            water.SetFloat("_BumpScale", 0.35f);
            water.SetTextureScale("_BaseMap", new Vector2(1500f, 1000f));
            water.EnableKeyword("_NORMALMAP");
            Quad(env, "Pacific Ocean", water, new Vector3(-7000, -0.4f, 540f), new Vector3(7000, -0.4f, 540f), new Vector3(-7000, -0.4f, 9000f), new Vector3(7000, -0.4f, 9000f), 0f);

            // ---------------------------------------------------------------- Roads, apron, deck
            var asphalt = Textured(mats.Get("Asphalt", new Color(0.17f, 0.17f, 0.18f), 0.2f), noise, Vector2.one);
            var concrete = Textured(mats.Get("Concrete Light", new Color(0.72f, 0.71f, 0.68f), 0.15f), noise, Vector2.one);
            var paint = mats.Get("Road Paint", new Color(0.95f, 0.8f, 0.2f), 0.3f);

            Flat(env, "Viewing Deck", concrete, 0f, 1.5f, 11f, 9f, 0.025f);
            Flat(env, "Access Road", asphalt, 0f, 40.5f, 8f, 69f, 0.03f);
            Flat(env, "Cross Road", asphalt, 0f, 48f, 880f, 8f, 0.031f);
            Flat(env, "Pad Apron", concrete, 0f, 110f, 80f, 80f, 0.035f);
            Flat(env, "Service Tower Track", concrete, -43f, 110f, 54f, 12f, 0.034f);
            for (float z = 10f; z < 70f; z += 6f)
                if (z < 42f || z > 53f) Flat(env, "Center Line", paint, 0f, z, 0.15f, 3f, 0.04f);
            for (float x = -430f; x < 430f; x += 6f) Flat(env, "Center Line", paint, x, 48f, 3f, 0.15f, 0.041f);

            var railMat = mats.Get("Railing Metal", new Color(0.55f, 0.57f, 0.6f), 0.5f, 0.6f);
            for (float x = -5f; x <= 5.01f; x += 1.25f) NoCollider(Box(env, "Railing Post", new Vector3(x, 0.52f, 5.8f), new Vector3(0.05f, 1.05f, 0.05f), railMat));
            NoCollider(Box(env, "Railing Top", new Vector3(0f, 1.05f, 5.8f), new Vector3(10.1f, 0.06f, 0.06f), railMat));
            NoCollider(Box(env, "Railing Mid", new Vector3(0f, 0.55f, 5.8f), new Vector3(10.1f, 0.04f, 0.04f), railMat));

            var pole = mats.Get("Pole Gray", new Color(0.5f, 0.5f, 0.52f), 0.4f, 0.4f);
            var lamp = mats.Get("Lamp", new Color(1f, 0.95f, 0.8f), 0.6f, 0f, false, new Color(3.2f, 2.7f, 1.8f));
            foreach (var p in VandenbergLamps)
            {
                float side = Mathf.Sign(p.x);
                NoCollider(Box(env, "Light Pole", new Vector3(p.x, LampHeight / 2f, p.z), new Vector3(0.14f, LampHeight, 0.14f), pole));
                NoCollider(Box(env, "Light Arm", new Vector3(p.x - side * 0.8f, LampHeight - 0.05f, p.z), new Vector3(1.7f, 0.08f, 0.08f), pole));
                NoCollider(Box(env, "Lamp Housing", new Vector3(p.x - side * 1.6f, LampHeight - 0.05f, p.z), new Vector3(0.7f, 0.12f, 0.3f), pole));
                NoCollider(Box(env, "Lamp Head", new Vector3(p.x - side * 1.6f, LampHeight - 0.13f, p.z), new Vector3(0.55f, 0.04f, 0.22f), lamp));
            }

            // ---------------------------------------------------------------- Buildings
            // The mobile service tower encloses the rocket until launch day, then rolls back along its track.
            var towerWhite = mats.Get("Service Tower", new Color(0.83f, 0.84f, 0.82f), 0.35f, 0.2f);
            var towerDark = mats.Get("Tower Panel Dark", new Color(0.35f, 0.37f, 0.4f), 0.3f, 0.3f);
            var window = mats.Get("Lit Window", new Color(1f, 0.85f, 0.55f), 0.5f, 0f, false, new Color(1.6f, 1.2f, 0.7f));
            Box(env, "Mobile Service Tower", new Vector3(-82f, 33f, 110f), new Vector3(24f, 66f, 24f), towerWhite);
            for (float y = 8f; y < 66f; y += 9f)
            {
                NoCollider(Box(env, "Service Tower Band", new Vector3(-82f, y, 110f), new Vector3(24.2f, 1.4f, 24.2f), towerDark));
                NoCollider(Box(env, "Service Tower Lights", new Vector3(-82f, y + 1.1f, 110f), new Vector3(24.3f, 0.3f, 24.3f), window));
            }

            var metal = mats.Get("Hangar Metal", new Color(0.74f, 0.76f, 0.78f), 0.45f, 0.35f);
            var roof = mats.Get("Hangar Roof", new Color(0.38f, 0.4f, 0.43f), 0.3f, 0.4f);
            var buildings = new[]
            {
                (new Vector3(-150f, 0f, 14f), new Vector3(52f, 9f, 24f)),
                (new Vector3(160f, 0f, -30f), new Vector3(38f, 7f, 22f)),
                (new Vector3(120f, 0f, 150f), new Vector3(26f, 6f, 18f)),
            };
            foreach (var (pos, size) in buildings)
            {
                Box(env, "Operations Building", pos + Vector3.up * size.y / 2f, size, metal);
                NoCollider(Box(env, "Building Roof", pos + Vector3.up * (size.y + 0.3f), new Vector3(size.x + 1f, 0.6f, size.z + 1f), roof));
                float sx = pos.x < 0f ? 1f : -1f;
                NoCollider(Box(env, "Windows", pos + new Vector3(sx * (size.x / 2f + 0.06f), size.y * 0.62f, 0f), new Vector3(0.1f, 1.3f, size.z * 0.8f), window));
                NoCollider(Box(env, "Windows", pos + new Vector3(0f, size.y * 0.62f, -size.z / 2f - 0.06f), new Vector3(size.x * 0.8f, 1.3f, 0.1f), window));
            }

            // ---------------------------------------------------------------- Hills and brush
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);

            var hill = mats.Get("Chaparral Hill", new Color(0.2f, 0.23f, 0.16f), 0.05f);
            var sage = mats.Get("Sage Brush", new Color(0.3f, 0.34f, 0.27f), 0.1f);
            var brush = mats.Get("Coyote Brush", new Color(0.2f, 0.27f, 0.16f), 0.1f);
            foreach (var mat in new[] { hill, sage, brush }) mat.enableInstancing = true;

            var veg = new GameObject("Hills and Brush").transform;
            veg.SetParent(env, false);

            // Rolling hills around the valley, fading into the fog. (centre x, centre z, width, peak height, depth)
            foreach (var h in new[]
            {
                new[] { -370f, -400f, 520f, 85f, 380f }, new[] { 160f, -470f, 640f, 115f, 420f }, new[] { 540f, -300f, 460f, 75f, 400f },
                new[] { -620f, -190f, 480f, 95f, 540f }, new[] { -580f, 170f, 420f, 65f, 480f }, new[] { -640f, 470f, 520f, 105f, 400f },
                new[] { 620f, 130f, 460f, 80f, 520f }, new[] { 700f, 440f, 500f, 90f, 440f },
                new[] { 0f, -850f, 1500f, 210f, 520f }, new[] { -950f, -620f, 950f, 170f, 620f }, new[] { 950f, -660f, 950f, 190f, 620f },
            })
                Blob(veg, "Hill", sphere, hill, new Vector3(h[0], -4f, h[1]), new Vector3(h[2], h[3] * 2f, h[4]), 0f);

            var rng = new System.Random(2018);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Paved(Vector3 p, float margin)
            {
                bool In(float cx, float cz, float w, float d) =>
                    Mathf.Abs(p.x - cx) < w / 2f + margin && Mathf.Abs(p.z - cz) < d / 2f + margin;
                return In(0f, 40.5f, 8f, 69f) || In(0f, 48f, 880f, 8f) || In(0f, 110f, 80f, 80f) || In(-43f, 110f, 54f, 12f) ||
                       In(0f, 1.5f, 14f, 12f) || In(DisplayPos.x, DisplayPos.z, 11f, 11f) || In(-8.5f, 15f, 9f, 9f) ||
                       In(-82f, 110f, 28f, 28f) || buildings.Any(b => In(b.Item1.x, b.Item1.z, b.Item2.x + 4f, b.Item2.z + 4f)) ||
                       In(-36f, 74f, 3f, 3f) || In(38f, 78f, 3f, 3f) || In(36f, 148f, 3f, 3f);
            }
            int placed = 0;
            for (int i = 0; i < 2400 && placed < 460; i++)
            {
                var p = new Vector3(R(-430f, 430f), 0f, R(-420f, 460f));
                if (Paved(p, 2f)) continue;
                float s = R(1.0f, 3.0f);
                Blob(veg, "Brush", sphere, rng.NextDouble() < 0.55 ? sage : brush,
                     p + Vector3.up * s * 0.12f, new Vector3(s * R(1f, 1.7f), s * R(0.45f, 0.75f), s * R(1f, 1.7f)), R(0f, 360f));
                placed++;
            }

            foreach (var tr in env.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);

            BuildVandenbergLights(env, mats);
            BuildGroundFog(env, mats);
            BuildVandenbergAtmosphere(mats);
        }

        static void BuildVandenbergLights(Transform env, Mats mats)
        {
            var steel = mats.Get("Pole Gray", new Color(0.5f, 0.5f, 0.52f), 0.4f, 0.4f);
            var floodGlow = mats.Get("Floodlight Glow", Color.white, 0.5f, 0f, false, new Color(4f, 3.8f, 3.4f));
            var beacon = mats.Get("Red Beacon", new Color(0.8f, 0.05f, 0.02f), 0.5f, 0f, false, new Color(4f, 0.25f, 0.1f));
            var beamMat = mats.Particle("Light Beam", additive: true);
            beamMat.SetColor("_BaseColor", new Color(0.66f, 0.72f, 0.88f, 1f)); // fog makes the beams stand out
            beamMat.SetFloat("_Cull", 0f);
            var poolMat = mats.Particle("Light Pool", additive: true);
            poolMat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.55f, 0.24f));

            var lights = new GameObject("Night Lights").transform;
            lights.SetParent(env, false);

            foreach (var basePos in new[] { new Vector3(-36f, 0f, 74f), new Vector3(38f, 0f, 78f), new Vector3(36f, 0f, 148f) })
            {
                const float H = 30f;
                Box(lights, "Floodlight Tower", basePos + Vector3.up * H / 2f, new Vector3(0.6f, H, 0.6f), steel);
                var head = new GameObject("Floodlight").transform;
                head.SetParent(lights, false);
                head.position = basePos + Vector3.up * (H + 0.8f);
                head.rotation = Quaternion.LookRotation(AtlasAim - head.position);
                NoCollider(Box(head, "Light Bank", Vector3.zero, new Vector3(3.2f, 1.6f, 0.3f), floodGlow));
                var spot = new GameObject("Spot").AddComponent<Light>();
                spot.transform.SetParent(head, false);
                spot.type = LightType.Spot;
                spot.color = new Color(1f, 0.97f, 0.9f);
                spot.intensity = 2100f;
                spot.range = 190f;
                spot.spotAngle = 84f;
                spot.innerSpotAngle = 62f;
                spot.shadows = LightShadows.None;
                AddBeam(head, Vector3.Distance(head.position, AtlasAim) + 16f, 17f, beamMat);
            }

            var deck = new GameObject("Deck Light").AddComponent<Light>();
            deck.transform.SetParent(lights, false);
            deck.transform.position = new Vector3(0f, 3.2f, 1.2f);
            deck.type = LightType.Point;
            deck.color = new Color(1f, 0.88f, 0.7f);
            deck.intensity = 9f;
            deck.range = 10f;

            foreach (var p in new[] { new Vector3(-12f, PadTop + 60.8f, 110f), new Vector3(-82f, 66.6f, 110f), new Vector3(-36f, 32f, 74f), new Vector3(38f, 32f, 78f), new Vector3(36f, 32f, 148f) })
                Sphere(lights, "Red Beacon", p, 0.55f, beacon);

            foreach (var lampPos in VandenbergLamps)
            {
                float side = Mathf.Sign(lampPos.x);
                var under = new Vector3(lampPos.x - side * 1.6f, 0.07f, lampPos.z);
                const float R = 9f;
                Quad(lights, "Light Pool", poolMat,
                    under + new Vector3(-R, 0f, -R), under + new Vector3(R, 0f, -R),
                    under + new Vector3(-R, 0f, R), under + new Vector3(R, 0f, R), 0f);
            }
        }

        // Slow banks of fog drifting in from the ocean, on top of the distance fog.
        static void BuildGroundFog(Transform env, Mats mats)
        {
            var go = new GameObject("Ground Fog");
            go.transform.SetParent(env, false);
            go.transform.position = new Vector3(0f, 9f, 150f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.duration = 30f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(28f, 40f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(70f, 130f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.6f, 0.66f, 0.76f, 0.06f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;

            var em = ps.emission; em.rateOverTime = 5.5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(760f, 10f, 620f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.2f, -1.2f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(-0.8f, -0.2f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mats.Particle("Fog Bank", additive: false);
            r.sortingFudge = 60f;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildVandenbergAtmosphere(Mats mats)
        {
            var moon = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (moon == null) moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.name = "Moonlight";
            moon.transform.rotation = Quaternion.LookRotation(-MoonDir);
            moon.color = new Color(0.66f, 0.74f, 1f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.5f;

            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Night Sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, $"{MatDir}/Night Sky.mat");
            }
            sky.SetTexture("_MainTex", NightSkyTexture());
            sky.SetFloat("_Mapping", 1f);
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Rotation", 0f);
            sky.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.16f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.115f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = Sky.horizon.gamma; // distant things fade into the sky's horizon colour

            BuildPostProcessing(bloomThreshold: 0.95f, bloomIntensity: 1.4f, exposure: 0.5f, saturation: 0f);
        }

        // =====================================================================================
        // The lander on show beside the viewing deck
        // =====================================================================================

        static void BuildLanderDisplay(UnityEngine.SceneManagement.Scene scene, Mats mats)
        {
            var plinthMat = mats.Get("Display Plinth", new Color(0.16f, 0.17f, 0.19f), 0.5f, 0.2f);
            var yellow = mats.Get("Hazard Yellow", new Color(0.95f, 0.75f, 0.1f), 0.4f);
            var pole = mats.Get("Pole Gray", new Color(0.5f, 0.5f, 0.52f), 0.4f, 0.4f);
            var glow = mats.Get("Floodlight Glow", Color.white, 0.5f, 0f, false, new Color(4f, 3.8f, 3.4f));

            var root = new GameObject("Lander Display").transform;
            root.position = DisplayPos;

            const float Radius = 4.3f, Top = 0.35f;
            foreach (var (name, radius, height, y, mat) in new[]
            {
                ("Plinth", Radius, Top, Top / 2f, plinthMat), ("Plinth Rim", Radius + 0.12f, 0.06f, 0.03f, yellow),
            })
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = name;
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(root, false);
                c.transform.localPosition = new Vector3(0f, y, 0f);
                c.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
                c.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // ---- The lander model, scaled to its real size (about 6 m across with the solar panels open).
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LanderSource);
            if (model == null)
            {
                Debug.LogWarning("[Delta II] Lander model not found at " + LanderSource + ". The display stand is empty.");
            }
            else
            {
                var lander = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                lander.name = "InSight Lander";
                lander.transform.SetParent(root, false);
                lander.transform.localPosition = Vector3.zero;
                var renderers = lander.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds B() { var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
                    var b0 = B();
                    float widest = Mathf.Max(b0.size.x, b0.size.z);
                    if (widest > 0.001f) lander.transform.localScale *= 6f / widest;
                    var b1 = B();
                    lander.transform.position += new Vector3(DisplayPos.x - b1.center.x, Top - b1.min.y, DisplayPos.z - b1.center.z);
                    Debug.Log($"[Delta II] Lander: imported {b0.size}, shown at {B().size}");
                }
            }

            // ---- Two display lights
            foreach (var local in new[] { new Vector3(-3.6f, 0f, -3.4f), new Vector3(3.8f, 0f, -2.6f) })
            {
                NoCollider(Box(root, "Display Light Pole", local + Vector3.up * 2.1f, new Vector3(0.08f, 4.2f, 0.08f), pole));
                var head = new GameObject("Display Light").transform;
                head.SetParent(root, false);
                head.localPosition = local + Vector3.up * 4.25f;
                head.rotation = Quaternion.LookRotation(DisplayPos + Vector3.up * 1.2f - head.position);
                NoCollider(Box(head, "Lamp", Vector3.zero, new Vector3(0.4f, 0.25f, 0.12f), glow));
                var spot = head.gameObject.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = new Color(1f, 0.95f, 0.86f);
                spot.intensity = 60f;
                spot.range = 16f;
                spot.spotAngle = 80f;
                spot.innerSpotAngle = 50f;
                spot.shadows = LightShadows.None;
            }

            // ---- Sign at the edge of the deck, facing the player
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Vector3 away = new Vector3(DisplayPos.x, 0f, DisplayPos.z).normalized; // from the player toward the display
            var signPos = DisplayPos - away * 5.1f;
            var post = Box(root, "Sign Post", Vector3.zero, new Vector3(0.08f, 1.0f, 0.08f), pole);
            NoCollider(post);
            post.transform.position = signPos + Vector3.up * 0.5f;

            var sign = new GameObject("Lander Sign", typeof(RectTransform));
            sign.transform.SetParent(root, false);
            sign.transform.position = signPos + Vector3.up * 1.3f;
            sign.transform.rotation = Quaternion.LookRotation(away) * Quaternion.Euler(18f, 0f, 0f);
            sign.transform.localScale = Vector3.one * 0.001f;
            sign.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)sign.transform).sizeDelta = new Vector2(1300f, 760f);

            var bg = UiChild(sign.transform, "Background", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.025f, 0.04f, 0.94f); bg.raycastTarget = false;
            var title = UiChild(sign.transform, "Title", new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.97f)).gameObject.AddComponent<Text>();
            title.font = font; title.fontSize = 110; title.fontStyle = FontStyle.Bold; title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(1f, 0.72f, 0.18f); title.text = "INSIGHT LANDER"; title.raycastTarget = false;
            var body = UiChild(sign.transform, "Body", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.72f)).gameObject.AddComponent<Text>();
            body.font = font; body.fontSize = 56; body.alignment = TextAnchor.UpperLeft; body.color = new Color(0.92f, 0.94f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap; body.verticalOverflow = VerticalWrapMode.Overflow; body.raycastTarget = false;
            body.text =
                "This is what is riding inside the rocket's nose cone, folded up for the trip.\n\n" +
                "On Mars it will listen for marsquakes and measure heat coming from deep inside the planet.";
        }
    }
}
