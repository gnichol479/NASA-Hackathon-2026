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
        // Far backdrop: MOLA MEGDR 128 px/deg (NASA/GSFC), ~90 km around the rover, under the HiRISE tile.
        const string FarRaw = MarsDir + "/EndeavourFar_513.raw";
        const string FarMeta = MarsDir + "/EndeavourFar.json";
        const string FarTerrainDataPath = MarsDir + "/EndeavourFar.asset";
        const int FarResolution = 513;

        // Free Asset Store pack "Mars Landscape 3D" (Evgenii Nikolskii). Used when present, otherwise the generated textures.
        const string PackDir = "Assets/Mars Landscape 3D";
        const string PackTex = PackDir + "/Resources/Textures/";
        static bool HasPack => AssetDatabase.IsValidFolder(PackDir);

        static readonly string[] Scenes =
        {
            "Assets/Shared/Scenes/TitleScreen-Night.unity",
            "Assets/Shared/Scenes/TitleScreen.unity",
            "Assets/Opportunity/Arrival/Scenes/Opportunity-Arrival.unity",
            "Assets/Opportunity/Legacy/Scenes/Opportunity-Legacy.unity",
            "Assets/Opportunity/Landing/Scenes/Opportunity-Landing.unity",
            "Assets/Ingenuity/Arrival/Scenes/Ingenuity-Arrival.unity",
            "Assets/Ingenuity/Legacy/Scenes/Ingenuity-Legacy.unity",
            "Assets/Ingenuity/Landing/Scenes/Ingenuity-Landing.unity",
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

        [System.Serializable]
        class FarMetaData
        {
            public float metersPerPixel;
            public float minElevation;
            public float maxElevation;
            public float roverElevation;
            public float roverPixelX;
            public float roverPixelZ;
        }

        [MenuItem("NasaHack/Mars Environment/NASA terrain + panorama sky + rocks")]
        public static void BuildFull() => Build(usePanorama: true, scatterRocks: true);

        // Only the elevation data (with sand/rock shading): procedural sky, no scattered rocks.
        [MenuItem("NasaHack/Mars Environment/NASA terrain only")]
        public static void BuildTerrainOnly() => Build(usePanorama: false, scatterRocks: false);

        // NASA terrain, dusk sky, textures and rocks from the free "Mars Landscape 3D" pack (if imported).
        [MenuItem("NasaHack/Mars Environment/NASA terrain + Mars Landscape 3D textures and rocks")]
        public static void BuildTextured() => Build(usePanorama: false, scatterRocks: true);

        public static void Build(bool usePanorama, bool scatterRocks)
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
            ExtendCameraRange();
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(HeightMeta));
            var terrainData = BuildTerrainData(meta);
            var farData = File.Exists(FarRaw) && File.Exists(FarMeta) ? BuildFarTerrainData() : null;
            // Terrain-only mode: Dominic's dusk sky (stars, blue sunset glow, Earth) if it exists, same look as the title.
            var dusk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shared/Title/Mars Dusk Sky.mat");
            var sky = usePanorama
                ? BuildSkyMaterial(meta)
                : (dusk != null ? dusk : AssetDatabase.LoadAssetAtPath<Material>("Assets/Shared/Settings/Mars Sky.mat"));
            bool duskLighting = !usePanorama && dusk != null;
            var rockMeshes = BuildRockMeshes();
            var rockMat = BuildRockMaterial();

            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                BuildScene(meta, terrainData, sky, rockMeshes, rockMat, path.Contains("TitleScreen"), scatterRocks, duskLighting);
                if (farData != null) BuildFarTerrain(farData);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[NasaHack] Mars environment built in " + path);
            }
            EditorSceneManager.OpenScene(Scenes[1]);
            Debug.Log("[NasaHack] Mars environment done. Credits: terrain NASA/JPL/University of Arizona/USGS (HiRISE DTM); sky NASA/JPL-Caltech/Cornell/ASU (PIA22909).");
        }

        // ---------- assets ----------

        // Unity cameras stop drawing at 1 km by default; the terrain reaches much further.
        static void ExtendCameraRange()
        {
            const string rigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
            var rig = PrefabUtility.LoadPrefabContents(rigPath);
            try
            {
                bool changed = false;
                foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.farClipPlane >= 60000f) continue;
                    cam.farClipPlane = 60000f;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
                    Debug.Log("[NasaHack] XR Rig camera far clip set to 60 km.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }

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

            AssignLayers(data, far: false);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        static TerrainData BuildFarTerrainData()
        {
            var meta = JsonUtility.FromJson<FarMetaData>(File.ReadAllText(FarMeta));
            var bytes = File.ReadAllBytes(FarRaw);
            if (bytes.Length != FarResolution * FarResolution * 2)
                throw new System.Exception($"[NasaHack] {FarRaw} should be {FarResolution}x{FarResolution} 16-bit.");
            var heights = new float[FarResolution, FarResolution];
            for (int z = 0; z < FarResolution; z++)
                for (int x = 0; x < FarResolution; x++)
                {
                    int i = (z * FarResolution + x) * 2;
                    heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                }
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(FarTerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, FarTerrainDataPath);
            }
            float sizeM = (FarResolution - 1) * meta.metersPerPixel;
            data.heightmapResolution = FarResolution;
            data.size = new Vector3(sizeM, meta.maxElevation - meta.minElevation, sizeM);
            data.SetHeights(0, 0, heights);
            AssignLayers(data, far: true);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        static void BuildFarTerrain(TerrainData data)
        {
            var meta = JsonUtility.FromJson<FarMetaData>(File.ReadAllText(FarMeta));
            var old = GameObject.Find("Mars Far Terrain");
            if (old != null) Object.DestroyImmediate(old);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Mars Far Terrain";
            go.isStatic = true;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            terrain.heightmapPixelError = 25f;
            terrain.basemapDistance = 3000f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            float roverX = meta.roverPixelX * meta.metersPerPixel;
            float roverZ = meta.roverPixelZ * meta.metersPerPixel;
            float roverY = meta.roverElevation - meta.minElevation;
            go.transform.position = new Vector3(RoverPosition.x - roverX, -roverY, RoverPosition.z - roverZ);
        }

        // Paints the terrain. With the pack: rippled sand on flats, pebble soil in patches, gravel on
        // moderate slopes, cracked bedrock on the steep valley walls. Without it: generated sand + rock.
        static void AssignLayers(TerrainData data, bool far)
        {
            string suffix = far ? " Far" : "";
            float k = far ? 18f : 1f; // bigger tiles on the far terrain
            TerrainLayer[] layers;
            if (HasPack)
            {
                layers = new[]
                {
                    LoadOrCreateLayer(MarsDir + "/ML Sand" + suffix + ".terrainlayer", PackTex + "Ground_01_D.png", PackTex + "Ground_01_N.png", 16f * k),
                    LoadOrCreateLayer(MarsDir + "/ML Pebbles" + suffix + ".terrainlayer", PackTex + "Ground_00_D.png", PackTex + "Ground_00_N.png", 6f * k),
                    LoadOrCreateLayer(MarsDir + "/ML Gravel" + suffix + ".terrainlayer", PackTex + "Ground_03_D.png", PackTex + "Ground_03_N.png", 10f * k),
                    LoadOrCreateLayer(MarsDir + "/ML Bedrock" + suffix + ".terrainlayer", PackTex + "Ground_02_D.png", PackTex + "Ground_02_N.png", 12f * k),
                };
            }
            else
            {
                layers = new[]
                {
                    LoadOrCreateLayer(MarsDir + "/Sand" + suffix + ".terrainlayer", SandAlbedo, SandNormal, 3f * k),
                    LoadOrCreateLayer(MarsDir + "/Rock" + suffix + ".terrainlayer", RockAlbedo, null, 2f * k),
                };
            }
            data.terrainLayers = layers;

            int res = far ? 256 : 1024;
            data.alphamapResolution = res;
            var alpha = new float[res, res, layers.Length];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)(res - 1), nz = z / (float)(res - 1);
                    float steep = data.GetSteepness(nx, nz);
                    float n1 = Mathf.PerlinNoise(nx * 25f + 3.1f, nz * 25f + 7.7f);
                    float n2 = Mathf.PerlinNoise(nx * 90f + 5.3f, nz * 90f + 9.1f);
                    if (layers.Length == 4)
                    {
                        float bedrock = Mathf.Clamp01((steep - 13f) / 8f);
                        float gravel = Mathf.Clamp01((steep - 5f + (n2 - 0.5f) * 4f) / 8f) * (1f - bedrock);
                        float flat = 1f - Mathf.Clamp01(steep / 6f);
                        float pebbles = flat * Mathf.Clamp01((n1 - 0.45f) * 3f) * (1f - gravel - bedrock);
                        float sand = Mathf.Max(0f, 1f - bedrock - gravel - pebbles);
                        float sum = sand + pebbles + gravel + bedrock;
                        alpha[z, x, 0] = sand / sum; alpha[z, x, 1] = pebbles / sum; alpha[z, x, 2] = gravel / sum; alpha[z, x, 3] = bedrock / sum;
                    }
                    else
                    {
                        float rockW = Mathf.Clamp01(Mathf.Clamp01((steep - 8f) / 14f) + Mathf.Clamp01((n1 - 0.62f) * 4f) * 0.6f);
                        alpha[z, x, 0] = 1f - rockW; alpha[z, x, 1] = rockW;
                    }
                }
            data.SetAlphamaps(0, 0, alpha);
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

        static void BuildScene(Meta meta, TerrainData data, Material sky, Mesh[] rockMeshes, Material rockMat, bool isTitle, bool scatterRocks, bool dusk)
        {
            foreach (var name in new[] { "Ground (teleport)", "Mars Terrain", "Mars Rocks" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            // Other terrains in the scene (e.g. a placeholder) are switched off, not deleted.
            foreach (var t in Object.FindObjectsOfType<Terrain>())
                if (t.name != "Mars Terrain" && t.name != "Mars Far Terrain") t.gameObject.SetActive(false);

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

            if (scatterRocks) ScatterRocks(terrain, rockMeshes, rockMat, isTitle ? 11 : 23);

            // Sky, haze and sun
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.00015f;   // thin Mars air: the far rim 20 km out stays faintly visible
            RenderSettings.fogColor = dusk ? new Color(0.42f, 0.33f, 0.28f) : new Color(0.84f, 0.70f, 0.56f);
            RenderSettings.ambientIntensity = dusk ? 0.75f : 1.1f;

            var sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                if (dusk)
                {
                    // Sun sits in the glow of the dusk skybox: just above the horizon, about 11 degrees east of +Z.
                    sun.transform.rotation = Quaternion.Euler(5f, 191f, 0f);
                    sun.color = new Color(1f, 0.78f, 0.62f);
                    sun.intensity = 0.9f;
                }
                else
                {
                    sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
                    sun.color = new Color(1f, 0.92f, 0.82f);
                    sun.intensity = 1.25f;
                }
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
            }
            DynamicGI.UpdateEnvironment();
        }

        struct RockSource
        {
            public Mesh mesh;
            public Material material;
            public Bounds bounds;
        }

        // Rock meshes from the pack with URP copies of their materials; falls back to the generated blobs.
        static System.Collections.Generic.List<RockSource> RockSources(Mesh[] fallbackMeshes, Material fallbackMat)
        {
            var list = new System.Collections.Generic.List<RockSource>();
            if (HasPack)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackDir + "/Prefabs" }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
                    var mr = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
                    if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;
                    list.Add(new RockSource { mesh = mf.sharedMesh, material = UrpCopy(mr.sharedMaterial), bounds = mf.sharedMesh.bounds });
                }
            }
            if (list.Count == 0)
                foreach (var m in fallbackMeshes) list.Add(new RockSource { mesh = m, material = fallbackMat, bounds = m.bounds });
            return list;
        }

        // Built-in Standard -> URP Lit, keeping albedo, normal and metallic/smoothness maps.
        static Material UrpCopy(Material source)
        {
            var path = MarsDir + "/" + source.name + " (URP).mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name + " (URP)" };
            if (source.HasProperty("_MainTex")) mat.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
            {
                mat.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (source.HasProperty("_MetallicGlossMap") && source.GetTexture("_MetallicGlossMap") != null)
            {
                mat.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void ScatterRocks(Terrain terrain, Mesh[] meshes, Material mat, int seed)
        {
            var sources = RockSources(meshes, mat);
            var parent = new GameObject("Mars Rocks").transform;
            var rng = new System.Random(seed);
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var keepClear = new[] { RoverPosition, Vector3.zero, new Vector3(2.2f, 0f, 5f) };

            int placed = 0, tries = 0;
            while (placed < 320 && tries < 5000)
            {
                tries++;
                float dist = 5f + 160f * Mathf.Pow((float)rng.NextDouble(), 1.6f);
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                if (keepClear.Any(k => Vector3.Distance(new Vector3(k.x, 0f, k.z), pos) < 4.5f)) continue;

                float nx = (pos.x - origin.x) / data.size.x, nz = (pos.z - origin.z) / data.size.z;
                if (nx <= 0f || nx >= 1f || nz <= 0f || nz >= 1f) continue;
                pos.y = terrain.SampleHeight(pos) + origin.y;
                var normal = data.GetInterpolatedNormal(nx, nz);

                var src = sources[rng.Next(sources.Count)];
                float size = 0.15f + 1.3f * Mathf.Pow((float)rng.NextDouble(), 3f);
                if (dist > 90f) size *= 2.5f;
                float scale = size / Mathf.Max(src.bounds.size.x, Mathf.Max(src.bounds.size.y, src.bounds.size.z));

                var rock = new GameObject("Rock");
                rock.transform.SetParent(parent, false);
                rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                rock.transform.localScale = Vector3.one * scale;
                // sit on the ground, a little buried
                float bottom = src.bounds.min.y * scale;
                rock.transform.position = pos - normal * (bottom + size * 0.12f);
                rock.isStatic = true;
                rock.AddComponent<MeshFilter>().sharedMesh = src.mesh;
                var mr = rock.AddComponent<MeshRenderer>();
                mr.sharedMaterial = src.material;
                mr.shadowCastingMode = size > 0.3f ? ShadowCastingMode.On : ShadowCastingMode.Off;
                placed++;
            }
        }
    }
}
