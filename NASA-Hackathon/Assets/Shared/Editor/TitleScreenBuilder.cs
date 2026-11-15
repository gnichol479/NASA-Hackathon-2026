using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack.EditorTools
{
    // The game's title screen: "Abandoned but not Forgotten", with one button per mission.
    //
    //   NasaHack > Build Night Title Screen   ->  Assets/Shared/Scenes/TitleScreen-Night.unity
    //
    // It is a separate scene from the earlier TitleScreen.unity, which this never touches.
    // Building it also makes it the first scene in Build Settings and adds the scenes its buttons load.
    // A mission with no scene yet shows "Coming soon"; rebuild once a scene named <Mission>-... exists.
    //
    // The setting: dusk on Mars. Sunsets on Mars are blue around the sun, and Earth shows as a bright
    // "evening star". Opportunity stands in silhouette against the glow. The player faces +Z.
    public static class TitleScreenBuilder
    {
        const string ScenePath = "Assets/Shared/Scenes/TitleScreen-Night.unity";
        const string AssetDir = "Assets/Shared/Title";
        const string SkyTexPath = AssetDir + "/MarsDuskSky.png";
        const string RigPath = "Assets/Shared/Prefabs/XR Rig.prefab";
        const string RoverPath = "Assets/Opportunity/Models/Mars Exploration Rover - Spirit and Opportunity.fbx";

        // One card per mission, left to right. Each button starts that mission's first scene.
        static readonly (string name, string kind, string blurb)[] Missions =
        {
            ("Opportunity", "Mars rover", "Built for 90 sols.\nDrove for almost 15 years."),
            ("InSight", "Mars lander", "Listened for marsquakes to study\nthe inside of Mars."),
            ("Ingenuity", "Mars helicopter", "First powered, controlled flight\non another planet."),
        };

        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);
        static readonly Vector3 SunDir = new Vector3(0.24f, -0.05f, 0.97f).normalized;    // just below the horizon, ahead
        static readonly Vector3 EarthDir = new Vector3(-0.58f, 0.4f, 0.71f).normalized;   // the evening star
        static readonly Color HorizonDust = new Color(0.15f, 0.105f, 0.08f);

        [MenuItem("NasaHack/Build Night Title Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Title] Stop Play mode first."); return; }
            if (EditorSceneManager.GetActiveScene().path != ScenePath && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Shared", "Title");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------------- Rig, ground, flow
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rig == null) { Debug.LogError("[Title] Missing " + RigPath); return; }
            PrefabUtility.InstantiatePrefab(rig, scene);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground (teleport)";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            // Its own soil material: the team's shared Mars Ground is made for daylight.
            var groundMat = Asset<Material>("Mars Dusk Ground.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            groundMat.SetColor("_BaseColor", new Color(0.36f, 0.215f, 0.15f));
            groundMat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(groundMat);
            floor.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            floor.AddComponent<TeleportationArea>();


            // ---------------------------------------------------------------- Sky, light, fog
            var sky = Asset<Material>("Mars Dusk Sky.mat", () => new Material(Shader.Find("Skybox/Panoramic")));
            sky.SetTexture("_MainTex", DuskSkyTexture());
            sky.SetFloat("_Mapping", 1f);
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(sky);

            // The last light of the day, low and blue, coming from the sunset: long shadows toward the player.
            var dusk = new GameObject("Dusk Light").AddComponent<Light>();
            dusk.type = LightType.Directional;
            dusk.transform.rotation = Quaternion.LookRotation(-new Vector3(SunDir.x, 0.13f, SunDir.z).normalized);
            dusk.color = new Color(0.55f, 0.7f, 1f);
            dusk.intensity = 0.75f;
            dusk.shadows = LightShadows.Soft;
            dusk.shadowStrength = 0.85f;

            RenderSettings.skybox = sky;
            RenderSettings.sun = dusk;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.085f, 0.085f, 0.12f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.09f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.025f, 0.02f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.007f;
            RenderSettings.fogColor = HorizonDust.gamma;

            // ---------------------------------------------------------------- Landscape
            var land = new GameObject("Landscape").transform;
            var rockMat = Asset<Material>("Mars Rock.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            rockMat.SetColor("_BaseColor", new Color(0.26f, 0.16f, 0.12f));
            rockMat.SetFloat("_Smoothness", 0.08f);
            rockMat.enableInstancing = true;
            var ridgeMat = Asset<Material>("Mars Ridge.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            ridgeMat.SetColor("_BaseColor", new Color(0.2f, 0.125f, 0.095f));
            ridgeMat.SetFloat("_Smoothness", 0.03f);
            EditorUtility.SetDirty(rockMat); EditorUtility.SetDirty(ridgeMat);

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);

            // Crater rim and mesas on the horizon. (x, z, width, peak height, depth)
            foreach (var h in new[]
            {
                new[] { -140f, 210f, 260f, 26f, 90f }, new[] { 90f, 250f, 320f, 34f, 110f }, new[] { 260f, 150f, 200f, 22f, 150f },
                new[] { -260f, 90f, 180f, 30f, 220f }, new[] { -200f, -160f, 300f, 28f, 160f }, new[] { 180f, -190f, 280f, 24f, 150f },
            })
                Blob(land, "Ridge", sphere, ridgeMat, new Vector3(h[0], -h[3] * 0.25f, h[1]), new Vector3(h[2], h[3] * 2.5f, h[4]), 0f);

            var rng = new System.Random(2004);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 320; i++)
            {
                var p = new Vector3(R(-45f, 45f), 0f, R(-25f, 70f));
                if (p.magnitude < 3f) continue;                                    // keep the player's feet clear
                if (Mathf.Abs(p.x) < 2.4f && p.z > 1f && p.z < 3.4f) continue; // and the Begin panel
                if (Vector3.Distance(p, new Vector3(-2.4f, 0f, 8f)) < 2.2f) continue; // and the rover
                float s = Mathf.Pow(R(0f, 1f), 3f) * (p.magnitude < 7f ? 0.2f : 0.75f) + 0.07f; // only pebbles near the player
                Blob(land, "Rock", sphere, rockMat, p + Vector3.up * s * 0.2f,
                     new Vector3(s * R(0.85f, 1.3f), s * R(0.55f, 0.9f), s * R(0.85f, 1.3f)), R(0f, 360f)).transform.Rotate(R(-10f, 10f), 0f, R(-10f, 10f));
            }
            foreach (var t in land.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            // ---------------------------------------------------------------- Opportunity, in silhouette
            var roverPos = new Vector3(-2.4f, 0f, 8f); // seen through the gap between the first two cards
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RoverPath);
            if (model == null) Debug.LogWarning("[Title] Rover model not found at " + RoverPath);
            else
            {
                var root = new GameObject("Opportunity").transform;
                root.position = roverPos;
                var rover = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                rover.name = "Model";
                foreach (var r in rover.GetComponentsInChildren<Renderer>(true))       // leftover Blender parts in the FBX
                    if (r.name == "_root" || r.name.StartsWith("Cube")) r.enabled = false;

                // Pose with the solar panels open: whichever end of the built-in clip has the bigger footprint.
                var clip = AssetDatabase.LoadAllAssetsAtPath(RoverPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null)
                {
                    clip.SampleAnimation(rover, 0f);
                    var a = WorldBounds(rover).size;
                    clip.SampleAnimation(rover, clip.length);
                    var b = WorldBounds(rover).size;
                    clip.SampleAnimation(rover, b.x * b.z >= a.x * a.z ? clip.length : 0f);
                }
                rover.transform.localRotation = Quaternion.Euler(0f, 215f, 0f);
                var bounds = WorldBounds(rover);
                rover.transform.position += new Vector3(roverPos.x - bounds.center.x, -bounds.min.y, roverPos.z - bounds.center.z);
                rover.AddComponent<DustCover>().amount = 0.6f;
            }

            // A warm light from the side picks the rover out of the dark, against the cold sky.
            var key = new GameObject("Rover Light").AddComponent<Light>();
            key.transform.position = new Vector3(1.5f, 4.5f, 4.2f);
            key.transform.rotation = Quaternion.LookRotation(roverPos + Vector3.up * 0.9f - key.transform.position);
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.72f, 0.46f);
            key.intensity = 34f;
            key.range = 22f;
            key.spotAngle = 50f;
            key.innerSpotAngle = 28f;
            key.shadows = LightShadows.None;

            // ---------------------------------------------------------------- Title lettering in the sky
            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.position = new Vector3(-0.6f, 5.4f, 15f);
            title.transform.rotation = Quaternion.LookRotation(title.transform.position - new Vector3(0f, 5.4f, 0f)) * Quaternion.Euler(-12f, 0f, 0f);
            title.transform.localScale = Vector3.one / UiFactory.PixelsPerMeter;
            title.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)title.transform).sizeDelta = new Vector2(13000f, 4200f);

            Line(title.transform, "Abandoned", "ABANDONED", 1700f, new Color(1f, 0.97f, 0.92f), 0.52f, 1f, 18f, FontStyles.Bold);
            Line(title.transform, "But Not Forgotten", "BUT NOT FORGOTTEN", 700f, UiFactory.Accent, 0.26f, 0.52f, 34f, FontStyles.Bold);
            var tagline = Line(title.transform, "Tagline", "A VR field trip to the machines NASA left behind", 300f, new Color(0.85f, 0.87f, 0.95f), 0.06f, 0.24f, 6f, FontStyles.Normal);
            var titleNarration = tagline.gameObject.AddComponent<NasaHack.SceneIntroNarrator>();
            titleNarration.narrationText = "Abandoned, but not forgotten. A VR field trip to the machines NASA left behind. Choose a mission to begin.";

            // Earth, far from the rover it sent.
            var earth = new GameObject("Earth Label", typeof(RectTransform));
            earth.transform.position = Head + EarthDir * 60f + Vector3.Cross(Vector3.up, EarthDir).normalized * 4.4f;
            earth.transform.rotation = Quaternion.LookRotation(earth.transform.position - Head);
            earth.transform.localScale = Vector3.one / UiFactory.PixelsPerMeter;
            earth.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)earth.transform).sizeDelta = new Vector2(6000f, 1800f);
            var earthText = Line(earth.transform, "Text", "Earth", 1300f, new Color(0.7f, 0.82f, 1f, 0.85f), 0f, 1f, 4f, FontStyles.Italic);
            earthText.alignment = TextAlignmentOptions.Left;

            // ---------------------------------------------------------------- Mission cards, within reach
            var flows = new GameObject("Scene Flows").transform;
            var targets = new System.Collections.Generic.List<string>();
            Label("Choose Heading", new Vector3(0f, 1.66f, 2.32f), "Choose a mission", 44f, new Color(0.92f, 0.94f, 1f), 1600f);
            for (int i = 0; i < Missions.Length; i++)
            {
                var (name, kind, blurb) = Missions[i];
                float angle = (i - (Missions.Length - 1) / 2f) * 33f * Mathf.Deg2Rad;
                var card = UiFactory.CreatePanel(name + " Card", null, 600f);
                card.position = new Vector3(Mathf.Sin(angle) * 2.3f, 1.32f, Mathf.Cos(angle) * 2.3f);
                UiFactory.FacePoint(card, Head);

                var nameText = UiFactory.AddText(card, "Name", name, 58f, FontStyles.Bold);
                nameText.color = UiFactory.Accent; nameText.alignment = TextAlignmentOptions.Center;
                var kindText = UiFactory.AddText(card, "Kind", kind.ToUpper(), 26f, FontStyles.Bold);
                kindText.alignment = TextAlignmentOptions.Center; kindText.characterSpacing = 12f;
                kindText.color = new Color(0.7f, 0.76f, 0.9f);
                var blurbText = UiFactory.AddText(card, "Blurb", blurb, 30f, FontStyles.Normal);
                blurbText.alignment = TextAlignmentOptions.Center;

                string target = FirstScene(name);
                var button = UiFactory.AddButton(card, name + " Button", target != null ? "Start" : "Coming soon", out var label);
                var cardNarrator = button.gameObject.AddComponent<NasaHack.TitleCardNarrator>();
                cardNarrator.narrationText = name + ". " + kind + ". " + blurb.Replace('\n', ' ') + ". " + label.text + ".";
                if (target != null)
                {
                    var flow = new GameObject("Scene Flow (" + name + ")").AddComponent<LoadNextScene>();
                    flow.transform.SetParent(flows, false);
                    flow.nextScene = target;
                    flow.debugKey = KeyCode.Alpha1 + i;   // desktop testing: 1, 2, 3
                    UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(flow.Load));
                    targets.Add(target);
                }
                else
                {
                    button.interactable = false;
                    button.targetGraphic.color = new Color(0.3f, 0.31f, 0.34f);
                    label.color = new Color(0.72f, 0.73f, 0.76f);
                }
                Debug.Log("[Title] " + name + " -> " + (target ?? "no scene yet (Coming soon)"));
            }
            Label("Credits", new Vector3(0f, 0.74f, 2.2f), "NASA Space Apps 2026  •  Yayo, Gibson, Dominic, Susan", 30f, new Color(0.7f, 0.7f, 0.74f), 1600f);

            var glow = new GameObject("Panel Glow").AddComponent<Light>();
            glow.transform.position = new Vector3(0f, 1.0f, 1.5f);
            glow.type = LightType.Point;
            glow.color = UiFactory.Accent;
            glow.intensity = 2.2f;
            glow.range = 7f;

            // ---------------------------------------------------------------- Bloom, so the lettering and Earth glow
            var profilePath = AssetDir + "/Title Post Processing.asset";
            if (File.Exists(profilePath)) AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f); bloom.intensity.Override(1.1f); bloom.scatter.Override(0.72f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.35f); grade.contrast.Override(10f); grade.saturation.Override(8f);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            Canvas.ForceUpdateCanvases();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            WireIntoGame(targets);
            Debug.Log("[Title] Built " + ScenePath + ".");
        }

        // Stills of the title screen, written next to the launch previews.
        internal static string Capture()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath) return "SKIPPED: TitleScreen-Night is not the open scene";
            Directory.CreateDirectory(DeltaIIBuilder.PreviewDir);
            foreach (var f in Directory.GetFiles(DeltaIIBuilder.PreviewDir, "*.png")) File.Delete(f);
            Canvas.ForceUpdateCanvases();

            var camGo = new GameObject("Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 5000f; cam.allowHDR = true;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                DeltaIIBuilder.Shot(cam, "title-1-view", Head, new Vector3(0.6f, 3.6f, 14f), 78f);
                DeltaIIBuilder.Shot(cam, "title-2-wide", Head + new Vector3(0f, 0f, -1.5f), new Vector3(0.4f, 3f, 14f), 100f);
                DeltaIIBuilder.Shot(cam, "title-3-cards", Head, new Vector3(0f, 1.35f, 2.3f), 82f);
                DeltaIIBuilder.Shot(cam, "title-4-rover", Head, new Vector3(-2.4f, 0.8f, 8f), 45f);
            }
            finally { Object.DestroyImmediate(camGo); }
            return "OK " + string.Join(", ", Directory.GetFiles(DeltaIIBuilder.PreviewDir, "*.png").Select(Path.GetFileName));
        }

        // The first scene of a mission: its Arrival if there is one, else its Launch, else whatever exists.
        static string FirstScene(string mission)
        {
            var names = AssetDatabase.FindAssets("t:Scene " + mission)
                .Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)))
                .Where(n => n.StartsWith(mission + "-")).OrderBy(n => n).ToList();
            foreach (var part in new[] { "Arrival", "Launch" })
                if (names.Contains(mission + "-" + part)) return mission + "-" + part;
            return names.FirstOrDefault();
        }

        // Scenes load by name, so they must be in Build Settings. This scene goes first: a build starts there.
        static void WireIntoGame(System.Collections.Generic.IEnumerable<string> sceneNames)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(sc => sc.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (var name in sceneNames)
            {
                var path = AssetDatabase.FindAssets("t:Scene " + name).Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(pth => Path.GetFileNameWithoutExtension(pth) == name);
                if (path == null || scenes.Any(sc => sc.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
                Debug.Log("[Title] Added to Build Settings: " + path);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // A line of text floating in the world, with no panel behind it.
        static TextMeshProUGUI Label(string name, Vector3 position, string text, float size, Color color, float widthPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.position = position;
            UiFactory.FacePoint(go.transform, Head);
            go.transform.localScale = Vector3.one / UiFactory.PixelsPerMeter;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(widthPx, size * 1.6f);
            return Line(go.transform, "Text", text, size, color, 0f, 1f, 2f, FontStyles.Normal);
        }

        // ---------------------------------------------------------------- helpers

        static TextMeshProUGUI Line(Transform parent, string name, string text, float size, Color color, float yMin, float yMax, float spacing, FontStyles style)
        {
            var t = UiFactory.AddText(parent, name, text, size, style);
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = spacing;
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0f, yMin); r.anchorMax = new Vector2(1f, yMax);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return t;
        }

        static GameObject Blob(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static T Asset<T>(string file, System.Func<T> create) where T : Object
        {
            var path = AssetDir + "/" + file;
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static float Step(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // Mars at dusk, as an equirectangular panorama (same mapping as Unity's Skybox/Panoramic):
        // dusty dark sky, a blue glow where the sun went down, stars, and Earth with its moon.
        static Texture2D DuskSkyTexture()
        {
            const int W = 4096, H = 2048;
            var px = new Color[W * H];
            var rng = new System.Random(1252004);
            float Rand() => (float)rng.NextDouble();

            var zenith = new Color(0.010f, 0.009f, 0.014f);
            var glowWide = new Color(0.10f, 0.19f, 0.36f);
            var glowCore = new Color(0.42f, 0.6f, 0.95f);

            Vector3 Dir(int x, int y)
            {
                float lon = (0.5f - (x + 0.5f) / W) * 2f * Mathf.PI;
                float lat = (1f - (y + 0.5f) / H) * Mathf.PI;
                return new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
            }
            (int, int) Pixel(Vector3 d)
            {
                float u = 0.5f - Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI); u -= Mathf.Floor(u);
                float v = 1f - Mathf.Acos(d.y) / Mathf.PI;
                return (Mathf.FloorToInt(u * W), Mathf.Clamp(Mathf.FloorToInt(v * H), 0, H - 1));
            }
            void Add(int x, int y, Color c) { x = (x % W + W) % W; if (y >= 0 && y < H) px[y * W + x] += c; }

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var d = Dir(x, y);
                    if (d.y < -0.01f) { px[y * W + x] = HorizonDust * 0.5f; continue; }
                    float up = Mathf.Clamp01(d.y);
                    var c = Color.Lerp(zenith, HorizonDust, Mathf.Pow(1f - up, 3.5f));
                    float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, SunDir), -1f, 1f));
                    float low = Mathf.Pow(1f - up, 2f);                            // the glow hugs the horizon
                    c += glowWide * Mathf.Exp(-Mathf.Pow(ang / 0.5f, 2f)) * (0.35f + 0.65f * low);
                    c += glowCore * Mathf.Exp(-Mathf.Pow(ang / 0.16f, 2f));
                    px[y * W + x] = c;
                }

            for (int i = 0; i < 16000; i++)
            {
                float zc = Rand(), phi = Rand() * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sqrt(1 - zc * zc) * Mathf.Cos(phi), zc, Mathf.Sqrt(1 - zc * zc) * Mathf.Sin(phi));
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, SunDir), -1f, 1f));
                float b = (Mathf.Pow(Rand(), 10f) * 1.6f + 0.035f + 0.06f * Rand()) * Step(0.04f, 0.35f, d.y) * (1f - Mathf.Exp(-Mathf.Pow(ang / 0.55f, 2f)));
                var (sx, sy) = Pixel(d);
                Add(sx, sy, Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.86f, 0.7f), Rand()) * b);
            }

            // Earth: the brightest "star", with the Moon as a faint dot beside it.
            var (ex, ey) = Pixel(EarthDir);
            var earth = new Color(0.75f, 0.88f, 1f);
            for (int dy = -5; dy <= 5; dy++)
                for (int dx = -5; dx <= 5; dx++)
                {
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Add(ex + dx, ey + dy, earth * (r < 1.6f ? 3f : 1.2f * Mathf.Exp(-r * r / 5f)));
                }
            Add(ex + 9, ey + 2, new Color(0.8f, 0.8f, 0.78f) * 0.6f); Add(ex + 10, ey + 2, new Color(0.8f, 0.8f, 0.78f) * 0.4f);

            var bytes = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bytes[i] = new Color32(
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.r))) * 255f),
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.g))) * 255f),
                    (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, c.b))) * 255f), 255);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.SetPixels32(bytes);
            File.WriteAllBytes(SkyTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SkyTexPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(SkyTexPath);
            imp.maxTextureSize = 4096;
            imp.mipmapEnabled = false;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexPath);
        }
    }
}
