using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NasaHack.EditorTools
{
    // Cape Canaveral surroundings for the Delta II launch: coast, beach, palms, scrub, roads,
    // viewing deck, pad structures, SLC-17A next door, sky, fog, sun and bloom.
    // Layout (player at the origin facing +Z = east, toward the Atlantic):
    //   z 0      viewing deck        z 90    SLC-17B (our pad)       z 220..275 beach      z > 250 ocean
    public static partial class DeltaIIBuilder
    {
        const string EnvRoot = "Environment";
        static string NoisePath => Root + "/Textures/Noise.png";
        static string WaterNormalPath => Root + "/Textures/WaterNormal.png";
        static string PostProfilePath => Root + "/Settings/Launch Post Processing.asset";

        const float LandEndZ = 220f, BeachEndZ = 275f;

        const float LampHeight = 6.5f;
        static readonly Vector3[] RoadLamps =
        {
            new Vector3(-5.6f, 0f, 18f), new Vector3(5.6f, 0f, 18f),
            new Vector3(-5.6f, 0f, 34f), new Vector3(5.6f, 0f, 34f),
            new Vector3(-5.6f, 0f, 52f), new Vector3(5.6f, 0f, 52f),
        };
        static readonly Vector3[] LightningMasts =
        {
            new Vector3(-26f, 0f, 64f), new Vector3(26f, 0f, 64f), new Vector3(-26f, 0f, 116f), new Vector3(26f, 0f, 116f),
        };

        static void BuildEnvironment(UnityEngine.SceneManagement.Scene scene, Mats mats, Dictionary<string, Mesh> meshes)
        {
            foreach (var name in new[] { EnvRoot, "Post Processing" })
                foreach (var g in scene.GetRootGameObjects().Where(g => g.name == name)) Object.DestroyImmediate(g);
            EnsureFolder(Root + "/Settings");

            var noise = NoiseTexture();
            var env = new GameObject(EnvRoot).transform;

            // ---------------------------------------------------------------- Ground, beach, ocean
            var groundMat = Textured(mats.Get("Cape Ground", new Color(0.47f, 0.52f, 0.34f), 0.08f), noise, Vector2.one);
            var ground = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Ground"));
            if (ground)
            {
                // Land from the far west up to the beach; still the teleport surface.
                ground.transform.position = new Vector3(0f, 0f, -170f);
                ground.transform.localScale = new Vector3(80f, 1f, 78f); // 800 m x 780 m
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                groundMat.SetTextureScale("_BaseMap", new Vector2(80f, 78f));
            }
            // Distant land under everything, so the horizon never shows an edge.
            var farLand = Textured(mats.Get("Far Land", new Color(0.42f, 0.47f, 0.31f), 0.05f), noise, new Vector2(600f, 600f));
            Quad(env, "Far Land", farLand, new Vector3(-7000, -0.6f, -9000), new Vector3(7000, -0.6f, -9000), new Vector3(-7000, -0.6f, 3000), new Vector3(7000, -0.6f, 3000), 0f);

            var sand = Textured(mats.Get("Beach Sand", new Color(0.88f, 0.82f, 0.66f), 0.15f), noise, Vector2.one);
            Quad(env, "Beach", sand,
                new Vector3(-7000, 0f, LandEndZ), new Vector3(7000, 0f, LandEndZ),
                new Vector3(-7000, -0.7f, BeachEndZ), new Vector3(7000, -0.7f, BeachEndZ), 1f / 6f);

            var water = mats.Get("Atlantic Water", new Color(0.07f, 0.27f, 0.36f), 0.93f);
            water.SetTexture("_BumpMap", WaterNormalTexture());
            water.SetFloat("_BumpScale", 0.35f);
            water.SetTextureScale("_BaseMap", new Vector2(1500f, 1000f));
            water.EnableKeyword("_NORMALMAP");
            Quad(env, "Atlantic Ocean", water,
                new Vector3(-7000, -0.35f, 250f), new Vector3(7000, -0.35f, 250f),
                new Vector3(-7000, -0.35f, 9000f), new Vector3(7000, -0.35f, 9000f), 0f);

            // ---------------------------------------------------------------- Roads, apron, deck
            var asphalt = Textured(mats.Get("Asphalt", new Color(0.17f, 0.17f, 0.18f), 0.2f), noise, Vector2.one);
            var concrete = Textured(mats.Get("Concrete Light", new Color(0.72f, 0.71f, 0.68f), 0.15f), noise, Vector2.one);
            var paint = mats.Get("Road Paint", new Color(0.95f, 0.8f, 0.2f), 0.3f);

            Flat(env, "Viewing Deck", concrete, 0f, 1.5f, 11f, 9f, 0.025f);
            Flat(env, "Access Road", asphalt, 0f, 33f, 8f, 54f, 0.03f);
            Flat(env, "Cross Road", asphalt, 0f, 42f, 800f, 8f, 0.031f);
            Flat(env, "SLC-17A Road", asphalt, 97f, 124f, 170f, 8f, 0.032f);
            Flat(env, "Pad Apron", concrete, 0f, 90f, 70f, 70f, 0.035f);
            Flat(env, "SLC-17A Apron", concrete, 175f, 110f, 50f, 50f, 0.033f);
            for (float z = 10f; z < 58f; z += 6f) Flat(env, "Center Line", paint, 0f, z, 0.15f, 3f, 0.04f);
            for (float x = -390f; x < 390f; x += 6f) Flat(env, "Center Line", paint, x, 42f, 3f, 0.15f, 0.041f);

            var railMat = mats.Get("Railing Metal", new Color(0.55f, 0.57f, 0.6f), 0.5f, 0.6f);
            for (float x = -5f; x <= 5.01f; x += 1.25f) NoCollider(Box(env, "Railing Post", new Vector3(x, 0.52f, 5.8f), new Vector3(0.05f, 1.05f, 0.05f), railMat));
            NoCollider(Box(env, "Railing Top", new Vector3(0f, 1.05f, 5.8f), new Vector3(10.1f, 0.06f, 0.06f), railMat));
            NoCollider(Box(env, "Railing Mid", new Vector3(0f, 0.55f, 5.8f), new Vector3(10.1f, 0.04f, 0.04f), railMat));

            // Light poles along the access road.
            var pole = mats.Get("Pole Gray", new Color(0.5f, 0.5f, 0.52f), 0.4f, 0.4f);
            var lamp = mats.Get("Lamp", new Color(1f, 0.95f, 0.8f), 0.6f, 0f, false,
                                NightLaunch ? new Color(3.2f, 2.7f, 1.8f) : new Color(1.2f, 1.1f, 0.85f));
            foreach (var p in RoadLamps)
            {
                float side = Mathf.Sign(p.x);
                NoCollider(Box(env, "Light Pole", new Vector3(p.x, LampHeight / 2f, p.z), new Vector3(0.14f, LampHeight, 0.14f), pole));
                NoCollider(Box(env, "Light Arm", new Vector3(p.x - side * 0.8f, LampHeight - 0.05f, p.z), new Vector3(1.7f, 0.08f, 0.08f), pole));
                NoCollider(Box(env, "Lamp Housing", new Vector3(p.x - side * 1.6f, LampHeight - 0.05f, p.z), new Vector3(0.7f, 0.12f, 0.3f), pole));
                NoCollider(Box(env, "Lamp Head", new Vector3(p.x - side * 1.6f, LampHeight - 0.13f, p.z), new Vector3(0.55f, 0.04f, 0.22f), lamp));
            }

            // ---------------------------------------------------------------- Pad structures
            var mast = mats.Get("Mast White", new Color(0.86f, 0.86f, 0.84f), 0.4f);
            var mastRed = mats.Get("Mast Red", new Color(0.75f, 0.12f, 0.08f), 0.4f);
            foreach (var p in LightningMasts)
            {
                Box(env, "Lightning Mast", new Vector3(p.x, 26f, p.z), new Vector3(0.34f, 52f, 0.34f), mast);
                for (float y = 6f; y < 52f; y += 12f)
                    NoCollider(Box(env, "Mast Band", new Vector3(p.x, y, p.z), new Vector3(0.38f, 3f, 0.38f), mastRed));
                NoCollider(Box(env, "Mast Spike", new Vector3(p.x, 54f, p.z), new Vector3(0.08f, 4f, 0.08f), pole));
            }

            // SLC-17A, the twin pad next door, with its service tower closed up.
            var padConcrete = mats.Get("Pad Concrete", new Color(0.62f, 0.61f, 0.58f), 0.15f);
            var towerWhite = mats.Get("Service Tower", new Color(0.83f, 0.84f, 0.82f), 0.35f, 0.2f);
            var towerDark = mats.Get("Tower Panel Dark", new Color(0.35f, 0.37f, 0.4f), 0.3f, 0.3f);
            var redSteel = mats.Get("Tower Steel", new Color(0.62f, 0.22f, 0.14f), 0.3f);
            Box(env, "SLC-17A Deck", new Vector3(175f, 0.4f, 110f), new Vector3(24f, 0.8f, 24f), padConcrete);
            Box(env, "SLC-17A Service Tower", new Vector3(175f, 26f, 110f), new Vector3(13f, 52f, 13f), towerWhite);
            for (float y = 6f; y < 52f; y += 8f)
                NoCollider(Box(env, "Service Tower Band", new Vector3(175f, y, 110f), new Vector3(13.2f, 1.2f, 13.2f), towerDark));
            Box(env, "SLC-17A Umbilical Tower", new Vector3(163f, 22f, 110f), new Vector3(3f, 44f, 3f), redSteel);

            // Blockhouse (the domed concrete control bunker), hangars, water tower.
            var bunker = Textured(mats.Get("Blockhouse Concrete", new Color(0.66f, 0.65f, 0.6f), 0.12f), noise, Vector2.one * 4f);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Blockhouse";
            dome.transform.SetParent(env, false);
            dome.transform.localPosition = new Vector3(-115f, 0f, 70f);
            dome.transform.localScale = new Vector3(34f, 16f, 34f);
            dome.GetComponent<Renderer>().sharedMaterial = bunker;
            Box(env, "Blockhouse Entrance", new Vector3(-115f, 2f, 53f), new Vector3(6f, 4f, 6f), bunker);

            var metal = mats.Get("Hangar Metal", new Color(0.74f, 0.76f, 0.78f), 0.45f, 0.35f);
            var roof = mats.Get("Hangar Roof", new Color(0.38f, 0.4f, 0.43f), 0.3f, 0.4f);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(-270f, 0f, -60f), new Vector3(55f, 18f, 32f)),
                (new Vector3(-245f, 0f, -150f), new Vector3(40f, 13f, 26f)),
                (new Vector3(240f, 0f, -90f), new Vector3(62f, 21f, 36f)),
                (new Vector3(300f, 0f, 20f), new Vector3(30f, 11f, 22f)),
            })
            {
                Box(env, "Hangar", pos + Vector3.up * size.y / 2f, size, metal);
                NoCollider(Box(env, "Hangar Roof", pos + Vector3.up * (size.y + 0.4f), new Vector3(size.x + 1f, 0.8f, size.z + 1f), roof));
                NoCollider(Box(env, "Hangar Door", pos + new Vector3(0f, size.y * 0.35f, -size.z / 2f - 0.05f), new Vector3(size.x * 0.6f, size.y * 0.7f, 0.1f), towerDark));
            }

            var tank = mats.Get("Water Tower", new Color(0.9f, 0.9f, 0.88f), 0.5f, 0.2f);
            var wt = new Vector3(-170f, 0f, 160f);
            foreach (var (x, z) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
                Box(env, "Water Tower Leg", wt + new Vector3(x, 12f, z), new Vector3(0.5f, 24f, 0.5f), pole);
            var tankGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tankGo.name = "Water Tower Tank";
            tankGo.transform.SetParent(env, false);
            tankGo.transform.localPosition = wt + Vector3.up * 27f;
            tankGo.transform.localScale = new Vector3(11f, 8f, 11f);
            tankGo.GetComponent<Renderer>().sharedMaterial = tank;

            // ---------------------------------------------------------------- Vegetation
            var palmMesh = PalmMesh(meshes);
            var trunk = mats.Get("Palm Trunk", new Color(0.47f, 0.39f, 0.29f), 0.15f);
            var frond = mats.Get("Palm Frond", new Color(0.24f, 0.4f, 0.15f), 0.25f, 0f, true);
            var bushA = mats.Get("Scrub Dark", new Color(0.2f, 0.32f, 0.14f), 0.1f);
            var bushB = mats.Get("Scrub Dry", new Color(0.4f, 0.42f, 0.24f), 0.1f);
            var dune = mats.Get("Dune Sand", new Color(0.84f, 0.78f, 0.6f), 0.1f);
            foreach (var m in new[] { trunk, frond, bushA, bushB, dune }) m.enableInstancing = true;
            var tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphereMesh = tmpSphere.GetComponent<MeshFilter>().sharedMesh; // built-in asset, safe to keep
            Object.DestroyImmediate(tmpSphere);

            var rng = new System.Random(2003);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var veg = new GameObject("Vegetation").transform;
            veg.SetParent(env, false);

            // Palms: clusters beside the viewing area, near the blockhouse, and along the dunes.
            var palmSpots = new List<Vector3>();
            for (int i = 0; i < 14; i++) palmSpots.Add(new Vector3(R(-45f, -9f), 0f, R(-35f, 25f)));
            for (int i = 0; i < 12; i++) palmSpots.Add(new Vector3(R(10f, 50f), 0f, R(-35f, 25f)));
            for (int i = 0; i < 8; i++) palmSpots.Add(new Vector3(R(-150f, -80f), 0f, R(20f, 120f)));
            // Flanking the access road, where the road lamps pick them out against the night sky.
            for (int i = 0; i < 9; i++) palmSpots.Add(new Vector3(R(-24f, -8f), 0f, R(22f, 36f)));
            for (int i = 0; i < 12; i++) palmSpots.Add(new Vector3(R(8f, 26f), 0f, R(9f, 36f)));
            for (int i = 0; i < 10; i++) palmSpots.Add(new Vector3((i % 2 == 0 ? -1f : 1f) * R(9f, 30f), 0f, R(48f, 58f)));
            for (int i = 0; i < 26; i++) palmSpots.Add(new Vector3(R(-380f, 380f), 0f, R(200f, 216f)));
            foreach (var p in palmSpots)
            {
                if (OnRoadOrPad(p, 3f)) continue;
                var palm = new GameObject("Palm Tree");
                palm.transform.SetParent(veg, false);
                palm.transform.localPosition = p;
                palm.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                palm.transform.localScale = Vector3.one * R(0.8f, 1.25f);
                palm.AddComponent<MeshFilter>().sharedMesh = palmMesh;
                palm.AddComponent<MeshRenderer>().sharedMaterials = new[] { trunk, frond };
            }

            // Dunes along the back of the beach.
            for (float x = -400f; x < 400f; x += R(14f, 26f))
                Blob(veg, "Dune", sphereMesh, dune, new Vector3(x, -0.6f, R(212f, 222f)), new Vector3(R(18f, 30f), R(2.5f, 4.5f), R(8f, 12f)), R(0f, 30f));

            // Scrub brush scattered over the land, kept off roads, pads and the viewing area.
            int placed = 0;
            for (int i = 0; i < 2000 && placed < 420; i++)
            {
                var p = new Vector3(R(-390f, 390f), 0f, R(-540f, 214f));
                if (OnRoadOrPad(p, 2f)) continue;
                if (Mathf.Abs(p.x) < 14f && p.z > -12f && p.z < 12f) continue; // viewing area
                float s = R(1.2f, 3.4f);
                Blob(veg, "Scrub", sphereMesh, rng.NextDouble() < 0.6 ? bushA : bushB,
                     p + Vector3.up * s * 0.15f, new Vector3(s * R(1f, 1.6f), s * R(0.5f, 0.8f), s * R(1f, 1.6f)), R(0f, 360f));
                placed++;
            }

            // Tree lines at the edges of the land, hiding where the walkable ground ends.
            for (float x = -400f; x <= 400f; x += R(5f, 9f))
                Blob(veg, "Tree Line", sphereMesh, bushA, new Vector3(x, 2f, -555f + R(-4f, 4f)), new Vector3(R(8f, 14f), R(5f, 8f), R(8f, 14f)), R(0f, 360f));
            for (float z = -550f; z <= 200f; z += R(5f, 9f))
                foreach (float x in new[] { -400f, 400f })
                    Blob(veg, "Tree Line", sphereMesh, bushA, new Vector3(x + R(-4f, 4f), 2f, z), new Vector3(R(8f, 14f), R(5f, 8f), R(8f, 14f)), R(0f, 360f));

            foreach (var t in env.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            if (NightLaunch)
            {
                BuildNightLights(env, mats);
                BuildNightAtmosphere(scene, mats);
            }
            else BuildAtmosphere(scene, mats);
        }

        // ---------------------------------------------------------------- Sky, sun, fog, post

        static void BuildAtmosphere(UnityEngine.SceneManagement.Scene scene, Mats mats)
        {
            // Sun behind the viewer's left shoulder, lighting the rocket's near side.
            var sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.Euler(38f, 25f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;

            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Florida Sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, $"{MatDir}/Florida Sky.mat");
            }
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.42f));
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.57f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.73f, 0.8f, 0.87f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 7000f;

            // Post-processing: bloom makes the engine flames glow; ACES gives filmic contrast.
            BuildPostProcessing(bloomThreshold: 1.0f, bloomIntensity: 0.9f, exposure: 0.35f, saturation: 12f);
        }

        // ---------------------------------------------------------------- Helpers

        static bool OnRoadOrPad(Vector3 p, float margin)
        {
            bool In(float cx, float cz, float w, float d) =>
                Mathf.Abs(p.x - cx) < w / 2f + margin && Mathf.Abs(p.z - cz) < d / 2f + margin;
            return In(0f, 33f, 8f, 54f) || In(0f, 42f, 800f, 8f) || In(97f, 124f, 170f, 8f) ||
                   In(0f, 90f, 70f, 70f) || In(175f, 110f, 50f, 50f) || In(0f, 1.5f, 11f, 9f) ||
                   In(-115f, 70f, 40f, 44f) || In(-170f, 160f, 14f, 14f) ||
                   In(-270f, -60f, 60f, 37f) || In(-245f, -150f, 45f, 31f) || In(240f, -90f, 67f, 41f) || In(300f, 20f, 35f, 27f) ||
                   In(-26f, 64f, 3f, 3f) || In(26f, 64f, 3f, 3f) || In(-26f, 116f, 3f, 3f) || In(26f, 116f, 3f, 3f) ||
                   In(-8.5f, 15f, 9f, 9f); // countdown sign
        }

        static void Blob(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        static void NoCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
        }

        // Thin, collider-free rectangle lying on the ground (so teleporting still lands on the ground below).
        static void Flat(Transform parent, string name, Material mat, float cx, float cz, float w, float d, float y)
        {
            Quad(parent, name, mat,
                new Vector3(cx - w / 2f, y, cz - d / 2f), new Vector3(cx + w / 2f, y, cz - d / 2f),
                new Vector3(cx - w / 2f, y, cz + d / 2f), new Vector3(cx + w / 2f, y, cz + d / 2f), 1f / 6f);
        }

        // Quad from four corners (near-left, near-right, far-left, far-right). UVs in world metres * uvScale (0 = 0..1).
        static void Quad(Transform parent, string name, Material mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { a, b, c, d };
            Vector2 Uv(Vector3 v, Vector2 fallback) => uvScale > 0f ? new Vector2(v.x, v.z) * uvScale : fallback;
            mesh.uv = new[] { Uv(a, new Vector2(0, 0)), Uv(b, new Vector2(1, 0)), Uv(c, new Vector2(0, 1)), Uv(d, new Vector2(1, 1)) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, MeshPath);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Textured(Material m, Texture2D tex, Vector2 tiling)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Palm: slightly curved tapering trunk + drooping fronds, one mesh with two submeshes.
        static Mesh PalmMesh(Dictionary<string, Mesh> meshes)
        {
            const float H = 9f;
            float Bend(float y) => 0.05f * y * y / H;

            var trunkProfile = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float y = H * i / 12f; trunkProfile.Add(new Vector2(Mathf.Lerp(0.3f, 0.17f, i / 12f), y)); }
            var trunk = Lathe("Palm Trunk Part", trunkProfile.ToArray(), false, true, 12);
            var tv = trunk.vertices;
            for (int i = 0; i < tv.Length; i++) tv[i].x += Bend(tv[i].y);
            trunk.vertices = tv;
            trunk.RecalculateBounds();

            var top = new Vector3(Bend(H), H, 0f);
            var fv = new List<Vector3>(); var ft = new List<int>(); var fu = new List<Vector2>();
            for (int f = 0; f < 9; f++)
            {
                float ang = f * 40f + (f % 2) * 12f;
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                float L = 4.2f + (f % 3) * 0.4f;
                int start = fv.Count;
                const int N = 10;
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N;
                    var center = top + dir * (L * u) + Vector3.up * (1.1f * u - 2.6f * u * u);
                    float w = 0.95f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(u * 1.15f, 1f))), 0.7f) + 0.02f;
                    fv.Add(center - side * w / 2f); fv.Add(center + Vector3.up * 0.12f * (1f - u)); fv.Add(center + side * w / 2f);
                    fu.Add(new Vector2(0f, u)); fu.Add(new Vector2(0.5f, u)); fu.Add(new Vector2(1f, u));
                }
                for (int i = 0; i < N; i++)
                {
                    int a = start + i * 3, b = a + 3;
                    ft.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, a + 1, b + 1, a + 2, a + 2, b + 1, b + 2 });
                }
            }
            var fronds = new Mesh { name = "Palm Fronds Part" };
            fronds.SetVertices(fv); fronds.SetUVs(0, fu); fronds.SetTriangles(ft, 0);
            fronds.RecalculateNormals();

            var palm = new Mesh { name = "Palm Tree" };
            palm.CombineMeshes(new[]
            {
                new CombineInstance { mesh = trunk, transform = Matrix4x4.identity },
                new CombineInstance { mesh = fronds, transform = Matrix4x4.identity },
            }, false, true);
            palm.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(palm, MeshPath);
            meshes["Palm Tree"] = palm;
            return palm;
        }

        // Tileable fractal noise, used (tinted) for ground, sand, asphalt and concrete.
        static float[,] TileableNoise(int n, float freq, int octaves, float seed)
        {
            var h = new float[n, n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0f, amp = 1f, f = freq, total = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        float P(float px, float py) => Mathf.PerlinNoise(seed + px * f / n, seed * 1.7f + py * f / n);
                        float fx = x / (float)n, fy = y / (float)n;
                        float s = P(x, y) * (1 - fx) * (1 - fy) + P(x - n, y) * fx * (1 - fy) + P(x, y - n) * (1 - fx) * fy + P(x - n, y - n) * fx * fy;
                        v += s * amp; total += amp; amp *= 0.5f; f *= 2f;
                    }
                    h[x, y] = v / total;
                }
            return h;
        }

        static Texture2D NoiseTexture()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            if (tex) return tex;
            const int N = 256;
            var h = TileableNoise(N, 6f, 5, 13.37f);
            var t = new Texture2D(N, N, TextureFormat.RGB24, true);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = Mathf.Lerp(0.72f, 1.0f, Mathf.SmoothStep(0.2f, 0.8f, h[x, y]));
                    t.SetPixel(x, y, new Color(v, v, v));
                }
            File.WriteAllBytes(NoisePath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(NoisePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        }

        static Texture2D WaterNormalTexture()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath);
            if (tex) return tex;
            const int N = 256;
            var h = TileableNoise(N, 10f, 3, 4.2f);
            var t = new Texture2D(N, N, TextureFormat.RGB24, true);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (h[(x + 1) % N, y] - h[(x - 1 + N) % N, y]) * 6f;
                    float dy = (h[x, (y + 1) % N] - h[x, (y - 1 + N) % N]) * 6f;
                    var nrm = new Vector3(-dx, -dy, 1f).normalized;
                    t.SetPixel(x, y, new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f));
                }
            File.WriteAllBytes(WaterNormalPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(WaterNormalPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(WaterNormalPath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath);
        }
    }
}
