using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NasaHack.EditorTools
{
    // Night launch, as it really happened: Opportunity lifted off at 11:18 p.m. EDT on July 7, 2003,
    // under a first-quarter moon with the summer Milky Way overhead.
    public static partial class DeltaIIBuilder
    {
        // Set to false for the daytime version.
        public const bool NightLaunch = true;

        const string NightSkyPath = Root + "/Textures/NightSky.png";

        // World directions: player faces +Z = east, so +X = south, -X = north, -Z = west.
        static readonly Vector3 MoonDir = new Vector3(0.355f, 0.375f, -0.857f).normalized; // low in the west-southwest
        static readonly Vector3 RocketAim = new Vector3(0f, 20f, 90f);                     // middle of the rocket

        static void BuildNightAtmosphere(UnityEngine.SceneManagement.Scene scene, Mats mats)
        {
            // Moonlight: dim, blue, coming from the moon's direction.
            var moon = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (moon == null) moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.name = "Moonlight";
            moon.transform.rotation = Quaternion.LookRotation(-MoonDir);
            moon.color = new Color(0.62f, 0.72f, 1f);
            moon.intensity = 0.22f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.6f;

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
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.095f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0.06f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.08f);
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 6000f;

            BuildPostProcessing(bloomThreshold: 0.95f, bloomIntensity: 1.3f, exposure: 0.45f, saturation: 5f);
        }

        // Floodlights on the pad, red tower beacons, lit windows, a light over the viewing deck.
        static void BuildNightLights(Transform env, Mats mats)
        {
            var steel = mats.Get("Pole Gray", new Color(0.5f, 0.5f, 0.52f), 0.4f, 0.4f);
            var floodGlow = mats.Get("Floodlight Glow", Color.white, 0.5f, 0f, false, new Color(4f, 3.8f, 3.4f));
            var beacon = mats.Get("Red Beacon", new Color(0.8f, 0.05f, 0.02f), 0.5f, 0f, false, new Color(4f, 0.25f, 0.1f));
            var window = mats.Get("Lit Window", new Color(1f, 0.85f, 0.55f), 0.5f, 0f, false, new Color(1.6f, 1.2f, 0.7f));

            var lights = new GameObject("Night Lights").transform;
            lights.SetParent(env, false);

            // Three floodlight towers bathing the rocket in white light.
            foreach (var basePos in new[] { new Vector3(-30f, 0f, 55f), new Vector3(32f, 0f, 58f), new Vector3(30f, 0f, 128f) })
            {
                const float H = 24f;
                Box(lights, "Floodlight Tower", basePos + Vector3.up * H / 2f, new Vector3(0.6f, H, 0.6f), steel);
                var head = new GameObject("Floodlight").transform;
                head.SetParent(lights, false);
                head.position = basePos + Vector3.up * (H + 0.8f);
                head.rotation = Quaternion.LookRotation(RocketAim - head.position);
                NoCollider(Box(head, "Light Bank", Vector3.zero, new Vector3(3.2f, 1.6f, 0.3f), floodGlow));
                var spot = new GameObject("Spot").AddComponent<Light>();
                spot.transform.SetParent(head, false);
                spot.type = LightType.Spot;
                spot.color = new Color(1f, 0.97f, 0.9f);
                spot.intensity = 3200f;  // URP uses inverse-square falloff; ~45 m to the rocket
                spot.range = 160f;
                spot.spotAngle = 42f;
                spot.innerSpotAngle = 24f;
                spot.shadows = LightShadows.None;
            }

            // A soft light over the viewing deck so the console and railing aren't pitch black.
            var deck = new GameObject("Deck Light").AddComponent<Light>();
            deck.transform.SetParent(lights, false);
            deck.transform.position = new Vector3(0f, 3.2f, 1.2f);
            deck.type = LightType.Point;
            deck.color = new Color(1f, 0.88f, 0.7f);
            deck.intensity = 9f;
            deck.range = 10f;

            // Red aircraft-warning beacons on everything tall.
            foreach (var p in new[]
            {
                new Vector3(-24f, 56.2f, 66f), new Vector3(24f, 56.2f, 66f), new Vector3(-24f, 56.2f, 114f), new Vector3(24f, 56.2f, 114f),
                new Vector3(175f, 52.6f, 110f), new Vector3(163f, 44.6f, 110f), new Vector3(-170f, 31.5f, 160f),
                new Vector3(-10f, 45.2f, 90f),
            })
                Sphere(lights, "Red Beacon", p, 0.7f, beacon);

            // Lit windows along the hangars and the blockhouse entrance.
            foreach (var (pos, size) in new[]
            {
                (new Vector3(-270f, 0f, -60f), new Vector3(55f, 18f, 32f)),
                (new Vector3(-245f, 0f, -150f), new Vector3(40f, 13f, 26f)),
                (new Vector3(240f, 0f, -90f), new Vector3(62f, 21f, 36f)),
                (new Vector3(300f, 0f, 20f), new Vector3(30f, 11f, 22f)),
            })
            {
                // Window strip on the side facing the player (+X side for west buildings, -X for east).
                float sx = pos.x < 0f ? 1f : -1f;
                NoCollider(Box(lights, "Windows", pos + new Vector3(sx * (size.x / 2f + 0.06f), size.y * 0.7f, 0f), new Vector3(0.1f, 1.4f, size.z * 0.8f), window));
                NoCollider(Box(lights, "Windows", pos + new Vector3(0f, size.y * 0.7f, size.z / 2f + 0.06f), new Vector3(size.x * 0.8f, 1.4f, 0.1f), window));
            }
            NoCollider(Box(lights, "Blockhouse Door Light", new Vector3(-115f, 4.4f, 49.9f), new Vector3(2.5f, 0.4f, 0.2f), window));

            // The service tower at SLC-17A is lit up with work lights.
            for (float y = 6f; y < 52f; y += 8f)
                NoCollider(Box(lights, "Service Tower Lights", new Vector3(175f, y + 1f, 110f), new Vector3(13.3f, 0.35f, 13.3f), window));
        }

        static void Sphere(Transform parent, string name, Vector3 pos, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void BuildPostProcessing(float bloomThreshold, float bloomIntensity, float exposure, float saturation)
        {
            if (File.Exists(PostProfilePath)) AssetDatabase.DeleteAsset(PostProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.7f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.Override(8f);
            color.saturation.Override(saturation);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("Post Processing");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            foreach (var cam in Object.FindObjectsOfType<Camera>(true))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                cam.farClipPlane = 12000f;
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(data);
                if (PrefabUtility.IsPartOfPrefabInstance(cam))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                }
            }
        }

        // ---------------------------------------------------------------- Star field panorama

        // Equirectangular (lat/long) sky matching Unity's Skybox/Panoramic mapping:
        //   u = 0.5 - atan2(z, x) / 2π,   v = 1 - acos(y) / π
        static Texture2D NightSkyTexture()
        {
            // Regenerated on every build (a few seconds) so sky tweaks always show up.

            const int W = 4096, H = 2048;
            var px = new Color[W * H];
            var rng = new System.Random(7072003);
            float Rand() => (float)rng.NextDouble();

            var cosLon = new float[W]; var sinLon = new float[W];
            for (int x = 0; x < W; x++)
            {
                float lon = (0.5f - (x + 0.5f) / W) * 2f * Mathf.PI;
                cosLon[x] = Mathf.Cos(lon); sinLon[x] = Mathf.Sin(lon);
            }

            var zenith = new Color(0.006f, 0.009f, 0.024f);
            var horizon = new Color(0.035f, 0.045f, 0.08f);
            var orlandoGlow = new Color(0.05f, 0.03f, 0.014f);
            var westNorthWest = new Vector3(-0.38f, 0f, -0.92f).normalized;
            var mwNormal = new Vector3(0.3f, 0.12f, 0.95f).normalized;      // Milky Way runs south -> overhead -> north
            var galacticCenter = new Vector3(0.93f, 0.25f, -0.25f).normalized; // Sagittarius, low in the south

            // Background: gradient, light pollution, Milky Way.
            for (int y = 0; y < H; y++)
            {
                float lat = (1f - (y + 0.5f) / H) * Mathf.PI; // 0 at zenith
                float dy = Mathf.Cos(lat), r = Mathf.Sin(lat);
                for (int x = 0; x < W; x++)
                {
                    var d = new Vector3(r * cosLon[x], dy, r * sinLon[x]);
                    Color c;
                    if (dy < -0.02f) c = new Color(0.015f, 0.016f, 0.02f);
                    else
                    {
                        float h = Mathf.Pow(1f - Mathf.Clamp01(dy), 4f);
                        c = Color.Lerp(zenith, horizon, h);
                        var flat = new Vector3(d.x, 0f, d.z).normalized;
                        c += orlandoGlow * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(flat, westNorthWest)), 3f) * Mathf.Pow(1f - Mathf.Clamp01(dy), 8f);

                        float band = Mathf.Exp(-Mathf.Pow(Vector3.Dot(d, mwNormal) / 0.13f, 2f));
                        if (band > 0.01f)
                        {
                            float n = Mathf.PerlinNoise(x * 0.004f, y * 0.006f) * 0.6f + Mathf.PerlinNoise(x * 0.015f + 40f, y * 0.02f) * 0.4f;
                            float dust = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.01f + 9f, y * 0.012f + 3f) * 1.6f - 0.55f); // dark lanes
                            float core = 1f + 1.8f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, galacticCenter)), 6f);
                            float k = band * n * core * (1f - dust * 0.8f) * Mathf.SmoothStep(0f, 0.25f, dy);
                            c += new Color(0.05f, 0.05f, 0.06f) * k;
                        }
                    }
                    px[y * W + x] = c;
                }
            }

            // Stars.
            void Add(int x, int y, Color c)
            {
                x = (x % W + W) % W;
                if (y < 0 || y >= H) return;
                px[y * W + x] += c;
            }
            for (int i = 0; i < 26000; i++)
            {
                float zc = Rand() * 2f - 1f, phi = Rand() * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sqrt(1 - zc * zc) * Mathf.Cos(phi), zc, Mathf.Sqrt(1 - zc * zc) * Mathf.Sin(phi));
                if (d.y < 0.01f) continue;
                float u = 0.5f - Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI); u -= Mathf.Floor(u);
                float v = 1f - Mathf.Acos(d.y) / Mathf.PI;
                int sx = Mathf.FloorToInt(u * W), sy = Mathf.FloorToInt(v * H); // rows are bottom-up, like the texture
                float b = (Mathf.Pow(Rand(), 6f) * 2.2f + 0.12f) * Mathf.SmoothStep(0f, 0.2f, d.y);
                var tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.68f), Rand());
                Add(sx, sy, tint * b);
                if (b > 0.4f)
                {
                    var halo = tint * b * 0.3f;
                    Add(sx + 1, sy, halo); Add(sx - 1, sy, halo); Add(sx, sy + 1, halo); Add(sx, sy - 1, halo);
                }
            }

            // First-quarter moon (drawn larger than life so it reads in VR).
            float moonRadius = 1.3f * Mathf.Deg2Rad;
            // First quarter: sunlight hits the moon from 90° to the side (from below the western horizon),
            // so exactly half of the disc is lit.
            var sunDir = Vector3.ProjectOnPlane(new Vector3(-0.6f, -0.55f, -0.6f), MoonDir).normalized;
            float mu = 0.5f - Mathf.Atan2(MoonDir.z, MoonDir.x) / (2f * Mathf.PI); mu -= Mathf.Floor(mu);
            float mv = 1f - Mathf.Acos(MoonDir.y) / Mathf.PI;
            int mcx = Mathf.FloorToInt(mu * W), mcy = Mathf.FloorToInt(mv * H);
            int span = Mathf.CeilToInt(moonRadius / (Mathf.PI / H) * 1.6f) + 4;
            for (int y = mcy - span; y <= mcy + span; y++)
            {
                if (y < 0 || y >= H) continue;
                float lat = (1f - (y + 0.5f) / H) * Mathf.PI;
                for (int xx = mcx - span * 2; xx <= mcx + span * 2; xx++)
                {
                    int x = (xx % W + W) % W;
                    var d = new Vector3(Mathf.Sin(lat) * cosLon[x], Mathf.Cos(lat), Mathf.Sin(lat) * sinLon[x]);
                    float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, MoonDir), -1f, 1f));
                    if (ang > moonRadius * 1.6f) continue;
                    if (ang < moonRadius)
                    {
                        var o = (d - MoonDir * Vector3.Dot(d, MoonDir)) / Mathf.Sin(moonRadius);
                        float rr = Mathf.Clamp01(o.magnitude);
                        var nrm = o + (-MoonDir) * Mathf.Sqrt(1f - rr * rr);
                        float lit = Mathf.SmoothStep(-0.05f, 0.08f, Vector3.Dot(nrm, sunDir));
                        float maria = 0.8f + 0.2f * Mathf.PerlinNoise(o.x * 3f + 5f, o.y * 3f + 2f);
                        var c = Color.Lerp(new Color(0.02f, 0.025f, 0.04f), new Color(1.0f, 0.97f, 0.88f) * maria, lit);
                        float edge = Mathf.SmoothStep(moonRadius, moonRadius * 0.92f, ang);
                        px[y * W + x] = Color.Lerp(px[y * W + x], c, edge);
                    }
                    else
                    {
                        float glow = Mathf.SmoothStep(moonRadius * 1.6f, moonRadius, ang);
                        px[y * W + x] += new Color(0.06f, 0.065f, 0.08f) * glow;
                    }
                }
            }

            // Linear -> sRGB bytes.
            var bytes = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bytes[i] = new Color32(
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.r))) * 255f),
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.g))) * 255f),
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.b))) * 255f), 255);
            }
            // px row 0 is the bottom of the sky (v = 0), which is also texture row 0.
            var t = new Texture2D(W, H, TextureFormat.RGB24, false);
            t.SetPixels32(bytes);
            File.WriteAllBytes(NightSkyPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(NightSkyPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(NightSkyPath);
            imp.maxTextureSize = 4096;
            imp.mipmapEnabled = false;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NightSkyPath);
        }
    }
}
