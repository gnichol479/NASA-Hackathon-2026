using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack.EditorTools
{
    // Replaces the flat orange plane in Title, Arrival and Legacy with:
    //   - real terrain: HiRISE DTM DTEEC_018701_1775_018846_1775 (NASA/JPL/UArizona/USGS), 1 m posts,
    //     cropped 3.2 km around Opportunity's final position (2.336 S, 354.651 E, Perseverance Valley)
    //   - real sky: Opportunity's last 360 panorama, PIA22909 (NASA/JPL-Caltech/Cornell/ASU)
    //   - scattered rocks, Mars haze and sun
    // Data files live in Assets/Shared/Mars/. Re-running replaces what it built before.
    public static class MarsEnvironmentBuilder
    {
        const string MarsDir = "Assets/Shared/Mars";
        const string HeightRaw = MarsDir + "/PerseveranceValley_2049.raw";
        const string HeightMeta = MarsDir + "/PerseveranceValley.json";
        const string SkyTex = MarsDir + "/MarsSky_PIA22909.jpg";
        const string SandAlbedo = MarsDir + "/Sand_Albedo.png";
        const string SandNormal = MarsDir + "/Sand_Normal.png";
        const string RockAlbedo = MarsDir + "/Rock_Albedo.png";
        const string TerrainDataPath = MarsDir + "/PerseveranceValley.asset";
        const string RockMeshPath = MarsDir + "/Rocks.asset";

        static readonly string[] Scenes =
        {
            "Assets/Shared/Scenes/TitleScreen.unity",
            "Assets/Opportunity/Arrival/Scenes/Opportunity-Arrival.unity",
            "Assets/Opportunity/Legacy/Scenes/Opportunity-Legacy.unity",
        };

        // Must match OpportunitySceneBuilder.RoverPosition: the rover pixel of the DTM lands here.
        static readonly Vector3 RoverPosition = new Vector3(0f, 0f, 3f);
        const int Resolution = 2049;

        [System.Serializable]
#pragma warning disable 649 // fields are filled by JsonUtility
        class Meta
        {
            public float metersPerPixel;
            public float minElevation;
            public float maxElevation;
            public float roverElevation;
            public int roverPixelX;   // column, west -> east
            public int roverPixelZ;   // row, south -> north (already flipped for Unity)
            public float skyRotation; // degrees, turns the panorama so its view down the valley faces east
        }

        [MenuItem("NasaHack/Build Mars Environment (Title, Arrival, Legacy)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NasaHack] Stop Play mode first.");
                return;
            }
            foreach (var f in new[] { HeightRaw, HeightMeta, SkyTex, SandAlbedo, SandNormal, RockAlbedo })
            {
                if (!File.Exists(f))
                {
                    Debug.LogError("[NasaHack] Missing " + f);
                    return;
                }
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureTextures();
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(HeightMeta));
            var terrainData = BuildTerrainData(meta);
            var sky = BuildSkyMaterial(meta);
            var rockMeshes = BuildRockMeshes();
            var rockMat = BuildRockMaterial();

            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                BuildScene(meta, terrainData, sky, rockMeshes, rockMat, path.Contains("TitleScreen"));
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[NasaHack] Mars environment built in " + path);
            }
            EditorSceneManager.OpenScene(Scenes[1]);
            Debug.Log("[NasaHack] Mars environment done. Credits: terrain NASA/JPL/University of Arizona/USGS (HiRISE DTM); sky NASA/JPL-Caltech/Cornell/ASU (PIA22909).");
        }

        // ---------- assets ----------

        static void ConfigureTextures()
        {
            SetImporter(SkyTex, t =>
            {
                t.maxTextureSize = 8192;
                t.wrapModeU = TextureWrapMode.Repeat;
                t.wrapModeV = TextureWrapMode.Clamp;
                t.mipmapEnabled = true;
                t.sRGBTexture = true;
            });
            SetImporter(SandAlbedo, t => { t.maxTextureSize = 1024; t.wrapMode = TextureWrapMode.Repeat; });
            SetImporter(RockAlbedo, t => { t.maxTextureSize = 1024; t.wrapMode = TextureWrapMode.Repeat; });
            SetImporter(SandNormal, t =>
            {
                t.maxTextureSize = 1024;
                t.wrapMode = TextureWrapMode.Repeat;
                t.textureType = TextureImporterType.NormalMap;
            });
        }

        static void SetImporter(string path, System.Action<TextureImporter> configure)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            configure(importer);
            importer.SaveAndReimport();
        }

        static TerrainData BuildTerrainData(Meta meta)
        {
            var bytes = File.ReadAllBytes(HeightRaw);
            if (bytes.Length != Resolution * Resolution * 2)
                throw new System.Exception($"[NasaHack] {HeightRaw} should be {Resolution}x{Resolution} 16-bit, got {bytes.Length} bytes.");

            var heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                {
                    int i = (z * Resolution + x) * 2;
                    heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                }

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }
            float sizeM = (Resolution - 1) * meta.metersPerPixel;
            data.heightmapResolution = Resolution;
            data.size = new Vector3(sizeM, meta.maxElevation - meta.minElevation, sizeM);
            data.SetHeights(0, 0, heights);

            var sand = LoadOrCreateLayer(MarsDir + "/Sand.terrainlayer", SandAlbedo, SandNormal, 3f);
            var rock = LoadOrCreateLayer(MarsDir + "/Rock.terrainlayer", RockAlbedo, null, 2f);
            data.terrainLayers = new[] { sand, rock };

            // Rock shows through on slopes and in patches; sand everywhere else.
            const int alphaRes = 1024;
            data.alphamapResolution = alphaRes;
            var alpha = new float[alphaRes, alphaRes, 2];
            for (int z = 0; z < alphaRes; z++)
                for (int x = 0; x < alphaRes; x++)
                {
                    float nx = x / (float)(alphaRes - 1), nz = z / (float)(alphaRes - 1);
                    float steep = data.GetSteepness(nx, nz);
                    float patch = Mathf.PerlinNoise(nx * 40f + 3.1f, nz * 40f + 7.7f);
                    float rockW = Mathf.Clamp01((steep - 8f) / 14f) + Mathf.Clamp01((patch - 0.62f) * 4f) * 0.6f;
                    rockW = Mathf.Clamp01(rockW);
                    alpha[z, x, 0] = 1f - rockW;
                    alpha[z, x, 1] = rockW;
                }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        static TerrainLayer LoadOrCreateLayer(string path, string albedo, string normal, float tile)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
            layer.normalMapTexture = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
            layer.normalScale = 0.8f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.05f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static Material BuildSkyMaterial(Meta meta)
        {
            var path = MarsDir + "/Mars Sky (PIA22909).mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTex));
            mat.SetFloat("_Mapping", 1f);    // latitude-longitude layout
            mat.SetFloat("_ImageType", 0f);  // 360 degrees
            mat.SetFloat("_Rotation", meta.skyRotation);
            mat.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material BuildRockMaterial()
        {
            var path = MarsDir + "/Rock.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(GraphicsSettings.currentRenderPipeline.defaultShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RockAlbedo));
            mat.SetColor("_BaseColor", new Color(0.9f, 0.85f, 0.8f));
            mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Three lumpy rocks made from a displaced sphere. Saved once, instanced many times.
        static Mesh[] BuildRockMeshes()
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(RockMeshPath).OfType<Mesh>().ToArray();
            if (existing.Length == 3) return existing;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var source = sphere.GetComponent<MeshFilter>().sharedMesh;
            var meshes = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                var m = Object.Instantiate(source);
                m.name = "Rock " + i;
                var verts = m.vertices;
                float seed = i * 17.3f;
                for (int v = 0; v < verts.Length; v++)
                {
                    var p = verts[v];
                    float n = Mathf.PerlinNoise(p.x * 2.5f + seed, p.y * 2.5f + p.z * 1.7f + seed) - 0.5f;
                    float n2 = Mathf.PerlinNoise(p.z * 6f + seed, p.x * 6f - p.y * 4f) - 0.5f;
                    float r = 1f + 0.45f * n + 0.15f * n2;
                    p *= r;
                    p.y *= 0.65f; // flatter, sits on the ground
                    verts[v] = p;
                }
                m.vertices = verts;
                m.RecalculateNormals();
                m.RecalculateBounds();
                meshes[i] = m;
            }
            Object.DestroyImmediate(sphere);

            AssetDatabase.CreateAsset(meshes[0], RockMeshPath);
            AssetDatabase.AddObjectToAsset(meshes[1], RockMeshPath);
            AssetDatabase.AddObjectToAsset(meshes[2], RockMeshPath);
            AssetDatabase.SaveAssets();
            return meshes;
        }

        // ---------- per scene ----------

        static void BuildScene(Meta meta, TerrainData data, Material sky, Mesh[] rockMeshes, Material rockMat, bool isTitle)
        {
            foreach (var name in new[] { "Ground (teleport)", "Mars Terrain", "Mars Rocks" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            // Terrain, positioned so the rover pixel sits at RoverPosition, at height 0.
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Mars Terrain";
            terrainGo.isStatic = true;
            var terrain = terrainGo.GetComponent<Terrain>();
            terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 400f;
            terrain.drawInstanced = true;
            float roverX = meta.roverPixelX * meta.metersPerPixel;
            float roverZ = meta.roverPixelZ * meta.metersPerPixel;
            float roverY = meta.roverElevation - meta.minElevation;
            terrainGo.transform.position = new Vector3(RoverPosition.x - roverX, -roverY, RoverPosition.z - roverZ);
            terrainGo.AddComponent<TeleportationArea>();

            ScatterRocks(terrain, rockMeshes, rockMat, isTitle ? 11 : 23);

            // Sky, haze and sun
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0015f;
            RenderSettings.fogColor = new Color(0.84f, 0.70f, 0.56f);

            var sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
                sun.color = new Color(1f, 0.92f, 0.82f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.8f;
            }
            DynamicGI.UpdateEnvironment();
        }

        static void ScatterRocks(Terrain terrain, Mesh[] meshes, Material mat, int seed)
        {
            var parent = new GameObject("Mars Rocks").transform;
            var rng = new System.Random(seed);
            var data = terrain.terrainData;
            var origin = terrain.transform.position;

            // Keep the pad around the rover and player clear.
            var keepClear = new[] { RoverPosition, Vector3.zero, new Vector3(2.2f, 0f, 5f) };

            int placed = 0, tries = 0;
            while (placed < 260 && tries < 4000)
            {
                tries++;
                // more rocks near the player, thinning out with distance
                float dist = 6f + 150f * Mathf.Pow((float)rng.NextDouble(), 1.6f);
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                if (keepClear.Any(k => Vector3.Distance(new Vector3(k.x, 0f, k.z), pos) < 5f)) continue;

                float nx = (pos.x - origin.x) / data.size.x, nz = (pos.z - origin.z) / data.size.z;
                if (nx <= 0f || nx >= 1f || nz <= 0f || nz >= 1f) continue;
                pos.y = terrain.SampleHeight(pos) + origin.y;
                var normal = data.GetInterpolatedNormal(nx, nz);

                float size = 0.12f + 1.1f * Mathf.Pow((float)rng.NextDouble(), 3f);
                if (dist > 80f) size *= 2.5f; // distant boulders read from afar
                var rock = new GameObject("Rock");
                rock.transform.SetParent(parent, false);
                rock.transform.position = pos - normal * size * 0.15f;
                rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                rock.transform.localScale = new Vector3(size * (0.8f + 0.5f * (float)rng.NextDouble()), size, size * (0.8f + 0.5f * (float)rng.NextDouble()));
                rock.isStatic = true;
                rock.AddComponent<MeshFilter>().sharedMesh = meshes[rng.Next(meshes.Length)];
                var mr = rock.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = size > 0.3f ? ShadowCastingMode.On : ShadowCastingMode.Off;
                placed++;
            }
        }
    }
}
