using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LastSignal.OpportunityFlashback
{
    public enum FacilitySurface
    {
        Floor, Wall, Trim, Blue, Dark, Metal, Light, Glass,
        RoverGold, Tire, Solar, Guide, Amber, Screen, Paper
    }

    /// <summary>Meter-scale layout and shared geometry for both the saved scene and runtime interactions.</summary>
    public static class OpportunityFacilityLayout
    {
        public const int Version = 17;
        public const string EnvironmentName = "FACILITY - Opportunity Integration Bay";
        public static readonly Vector3 Arrival = new Vector3(0f, 0f, -6.1f);
        public static readonly Vector3 RoverOrigin = new Vector3(0f, 0.96f, 1.5f);
        public static readonly Vector3 ConsoleCenter = new Vector3(4.4f, 1.60f, 0.45f);
        public static readonly Quaternion PartSpawnRotation = Quaternion.Euler(0f, -90f, 0f);
        public static readonly Vector3[] PartSpawns =
        {
            new Vector3(-4.65f, 1.005f, -0.3f),
            new Vector3(-4.65f, 1.33f, 1.25f),
            new Vector3(-4.65f, 1.16f, 2.8f)
        };
        // World-space Z centers for the three trays on the component bench.
        private static readonly float[] ComponentTrayDepths = { -.3f, 1.25f, 2.8f };
        public static readonly Vector3[] SocketPositions =
        {
            new Vector3(-0.58f, 0.30f, 0.04f),
            new Vector3(0.12f, 0.60f, -0.30f),
            new Vector3(0.48f, 0.10f, -0.44f)
        };
        public static readonly string[] SocketNames = { "Solar Panel Socket", "Camera Mast Socket", "Instrument Arm Socket" };
        public static readonly string[] MaterialNames =
        {
            "Floor", "CleanRoomWall", "Trim", "MissionBlue", "Graphite", "BrushedMetal", "Light",
            "ObservationGlass", "RoverChassis", "RoverTire", "SolarCells", "SocketCyan", "SafetyOrange", "Display", "Paper"
        };
        // The working Arrival/Legacy scenes hide these Blender proxy meshes from the imported FBX.
        // Keep the same paths here so the flashback never displays the large _root proxy as a cube.
        private static readonly string[] LegacyPlaceholderPaths =
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
            "_root/body/suspension_right_middle_rocker/suspension_right_rear_rocker1/suspension_right_rear_rocker2/Cube.020/suspension_right_rear_axl/Cube"
        };
        private static readonly Color[] Colors =
        {
            new Color(.60f,.65f,.67f), new Color(.87f,.89f,.88f), new Color(.68f,.73f,.75f),
            new Color(.13f,.29f,.43f), new Color(.11f,.15f,.18f), new Color(.45f,.51f,.53f),
            new Color(.93f,.97f,1f), new Color(.66f,.82f,.89f,.12f), new Color(.72f,.65f,.47f),
            new Color(.13f,.15f,.16f), new Color(.045f,.12f,.23f), new Color(.19f,.52f,.59f),
            new Color(.82f,.61f,.22f), new Color(.055f,.12f,.16f), new Color(.93f,.91f,.84f)
        };

        public static Material MakeMaterial(int index)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ConfigureMaterial(material, index);
            return material;
        }

        public static void ConfigureMaterial(Material material, int index)
        {
            bool unlit = index == (int)FacilitySurface.Light || index == (int)FacilitySurface.Screen || index == (int)FacilitySurface.Glass;
            material.shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            material.name = MaterialNames[index];
            material.SetColor("_BaseColor", Colors[index]);
            if (!unlit)
            {
                material.SetFloat("_Smoothness", index == (int)FacilitySurface.Metal ? .35f : .18f);
                material.SetFloat("_Metallic", index == (int)FacilitySurface.Metal ? .3f : 0f);
            }
            material.enableInstancing = true;
            if (index == (int)FacilitySurface.Glass)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        public static void ApplyLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f,.76f,.79f);
            RenderSettings.ambientEquatorColor = new Color(.54f,.59f,.63f);
            RenderSettings.ambientGroundColor = new Color(.37f,.40f,.43f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = .25f;
        }

        public static void PlaceRoverOnFixture(Transform model, Transform fixtureAnchor, float scale = 1f)
        {
            if (model == null || fixtureAnchor == null) return;
            model.localScale = Vector3.one * Mathf.Max(.01f, scale);
            Bounds bounds = GetBounds(model.GetComponentsInChildren<Renderer>(true));
            if (bounds.size == Vector3.zero) return;
            Vector3 destination = fixtureAnchor.position;
            Vector3 correction = new Vector3(destination.x - bounds.center.x, .80f - bounds.min.y, destination.z - bounds.center.z);
            model.position += correction;
        }

        public static void PrepareDetailedOpportunityModel(GameObject model, AnimationClip deployedPose, Vector3 euler)
        {
            if (model == null) return;

            Quaternion facing = Quaternion.Euler(euler);
            model.transform.localRotation = facing;
            HideImportedPlaceholderGeometry(model);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            if (deployedPose != null)
            {
                deployedPose.SampleAnimation(model, 0f);
                Bounds startBounds = GetBounds(model.GetComponentsInChildren<Renderer>(true));
                deployedPose.SampleAnimation(model, deployedPose.length);
                Bounds endBounds = GetBounds(model.GetComponentsInChildren<Renderer>(true));
                float startFootprint = startBounds.size.x * startBounds.size.z;
                float endFootprint = endBounds.size.x * endBounds.size.z;
                deployedPose.SampleAnimation(model, endFootprint >= startFootprint ? deployedPose.length : 0f);
                model.transform.localRotation = facing;
            }

            HideImportedPlaceholderGeometry(model);
        }

        public static void HideImportedPlaceholderGeometry(GameObject model)
        {
            if (model == null) return;
            foreach (string path in LegacyPlaceholderPaths)
            {
                Transform placeholder = model.transform.Find(path);
                Renderer placeholderRenderer = placeholder != null ? placeholder.GetComponent<Renderer>() : null;
                if (placeholderRenderer != null) placeholderRenderer.enabled = false;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                string objectName = renderer.name;
                if (objectName.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase) ||
                    objectName.StartsWith("_Hemi", System.StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
            }
        }

        public static Transform[][] FindRoverComponentSources(Transform modelRoot)
        {
            if (modelRoot == null) return null;
            Transform leftPanel = FindNamedTransform(modelRoot, "panels_left");
            Transform rightPanel = FindNamedTransform(modelRoot, "panels_right");
            Transform mast = FindNamedTransform(modelRoot, "mast");
            Transform arm = FindNamedTransform(modelRoot, "arm_mount");
            if (leftPanel == null || rightPanel == null || mast == null || arm == null) return null;
            return new[] { new[] { leftPanel, rightPanel }, new[] { mast }, new[] { arm } };
        }

        public static Bounds GetBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) return new Bounds(Vector3.zero, Vector3.zero);
            return bounds;
        }

        public static bool TryGetComponentBounds(Transform[] sources, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasRenderer = false;
            if (sources == null) return false;
            foreach (Transform source in sources)
            {
                if (source == null) continue;
                foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!hasRenderer) { bounds = renderer.bounds; hasRenderer = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return hasRenderer;
        }

        public static Vector3 GetComponentTraySpawn(int index, Bounds bounds)
        {
            float y = .95f + Mathf.Max(.08f, bounds.size.y * .5f) + .035f;
            return new Vector3(-4.65f, y, ComponentTrayDepths[index]);
        }

        public static void PlaceSocketOnModelPart(Transform socket, Transform guide, Bounds bounds, Quaternion modelRotation)
        {
            socket.position = bounds.center;
            socket.rotation = modelRotation;
            socket.localScale = Vector3.one;
            guide.localPosition = Vector3.down * Mathf.Max(.04f, bounds.extents.y + .025f);
            Transform[] rails = new Transform[4];
            int railCount = 0;
            for (int i = 0; i < guide.childCount; i++)
                if (guide.GetChild(i).name == "Alignment edge" && railCount < rails.Length)
                    rails[railCount++] = guide.GetChild(i);
            float width = Mathf.Clamp(bounds.size.x + .08f, .16f, 1.5f);
            float depth = Mathf.Clamp(bounds.size.z + .08f, .16f, 1.5f);
            Vector3[] positions = { new Vector3(-width*.5f,0f,0f), new Vector3(0f,0f,-depth*.5f),
                new Vector3(width*.5f,0f,0f), new Vector3(0f,0f,depth*.5f) };
            Vector3[] sizes = { new Vector3(.018f,.012f,depth), new Vector3(width,.012f,.018f),
                new Vector3(.018f,.012f,depth), new Vector3(width,.012f,.018f) };
            for (int i = 0; i < railCount; i++)
            {
                rails[i].localPosition = positions[i];
                rails[i].localScale = sizes[i];
            }
        }

        public static void BuildExtractedModelPart(Transform modelRoot, Transform[] sources, Bounds bounds, Transform partRoot)
        {
            Quaternion inverseModelRotation = Quaternion.Inverse(modelRoot.rotation);
            foreach (Transform source in sources)
            {
                if (source == null) continue;
                GameObject clone = Object.Instantiate(source.gameObject, partRoot, false);
                clone.name = source.name + " - extracted rover component";
                clone.transform.localPosition = inverseModelRotation * (source.position - bounds.center);
                clone.transform.localRotation = inverseModelRotation * source.rotation;
                clone.transform.localScale = source.lossyScale;
                HideImportedPlaceholderGeometry(clone);
                foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
        }

        private static Transform FindNamedTransform(Transform root, string exactName)
        {
            if (root.name == exactName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamedTransform(root.GetChild(i), exactName);
                if (found != null) return found;
            }
            return null;
        }

        public static OpportunityFacilityEnvironment BuildEnvironment(Transform parent, Material[] palette)
        {
            if (palette == null || palette.Length != MaterialNames.Length)
            {
                palette = new Material[MaterialNames.Length];
                for (int i = 0; i < palette.Length; i++) palette[i] = MakeMaterial(i);
            }
            var root = Group(EnvironmentName, parent);
            var facility = root.gameObject.AddComponent<OpportunityFacilityEnvironment>();
            facility.Initialize(palette);
            BuildShell(root, palette);
            BuildStructureAndLighting(root, palette);
            BuildObservationGallery(root, palette);
            BuildWorkFloor(root, palette);
            BuildServiceWall(root, palette);
            BuildBriefing(root, palette);
            ApplyLighting();
            return facility;
        }

        private static void BuildShell(Transform parent, Material[] p)
        {
            Transform root = Group("01 - Architecture | 16 x 18 m, 7 m clear height", parent);
            Cube(root, "Continuous epoxy floor", new Vector3(1.1f,-.12f,1f), new Vector3(18.2f,.24f,18f), p[0], true);
            Cube(root, "West insulated wall", new Vector3(-8.1f,3.55f,1f), new Vector3(.2f,7.1f,18f), p[1], true);
            Cube(root, "North insulated wall", new Vector3(1.1f,3.55f,10.1f), new Vector3(18.4f,7.1f,.2f), p[1], true);
            Cube(root, "Arrival wall", new Vector3(0f,3.55f,-8.1f), new Vector3(16.2f,7.1f,.2f), p[1], true);
            GameObject ceiling = Cube(root, "Acoustic ceiling", new Vector3(0f,7.15f,1f), new Vector3(16.2f,.2f,18.2f), p[1]);
            // The economical directional key represents the interior luminaires, not exterior sunlight.
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(root, "East wall below observation windows", new Vector3(8f,.55f,3f), new Vector3(.18f,1.1f,14f), p[1], true);
            Cube(root, "East wall above observation windows", new Vector3(8f,5.35f,3f), new Vector3(.18f,3.6f,14f), p[1], true);
            Cube(root, "East arrival wall", new Vector3(8f,3.55f,-6f), new Vector3(.18f,7.1f,4f), p[1], true);
            // Architectural seams establish human scale without expensive texture sets.
            for (int z = -7; z <= 9; z += 2)
            {
                Cube(root, "Wall panel reveal", new Vector3(-7.985f,3.4f,z), new Vector3(.018f,6.6f,.018f), p[2]);
                Cube(root, "Floor expansion joint", new Vector3(0f,.002f,z), new Vector3(15.9f,.003f,.012f), p[2]);
            }
            for (int x = -6; x <= 6; x += 4)
                Cube(root, "Floor expansion joint", new Vector3(x,.002f,1f), new Vector3(.012f,.003f,18f), p[2]);
            Cube(root, "West protective wall rail", new Vector3(-7.94f,.85f,1f), new Vector3(.09f,.14f,18f), p[3]);
            Cube(root, "West skirting", new Vector3(-7.94f,.12f,1f), new Vector3(.1f,.24f,18f), p[2]);
            Cube(root, "East skirting", new Vector3(7.88f,.12f,1f), new Vector3(.1f,.24f,18f), p[2]);
            // A real loading door gives the room a reason to be this large.
            Cube(root, "Loading door recess", new Vector3(0f,2.45f,9.93f), new Vector3(5.4f,4.9f,.16f), p[4]);
            Cube(root, "Sectional loading door", new Vector3(0f,2.42f,9.82f), new Vector3(5.05f,4.8f,.08f), p[2], true);
            for (int i = 1; i < 9; i++)
                Cube(root, "Door section joint", new Vector3(0f,i*.53f,9.77f), new Vector3(5.02f,.018f,.012f), p[5]);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(root, "Loading door vision panel", new Vector3(side*1.25f,2.8f,9.72f), new Vector3(1.5f,.48f,.07f), p[13]);
                Cube(root, "Door bollard", new Vector3(side*2.95f,.5f,9.1f), new Vector3(.16f,1f,.16f), p[12], true);
            }
            Label(root, "Bay identification", new Vector3(0f,5.7f,9.82f), Quaternion.identity,
                new Vector2(7f,.58f), "SPACECRAFT ASSEMBLY", .34f, p[3].color, TextAlignmentOptions.Center);
            Label(root, "Mission identification", new Vector3(0f,5.12f,9.81f), Quaternion.identity,
                new Vector2(5f,.26f), "OPPORTUNITY  /  MARS EXPLORATION ROVER", .115f, p[4].color, TextAlignmentOptions.Center);
            // Arrival airlock: closed scenery, with the entry position safely in front of it.
            Cube(root, "Airlock surround", new Vector3(0f,1.4f,-7.94f), new Vector3(2.5f,2.8f,.15f), p[2]);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(root, "Airlock leaf", new Vector3(side*.56f,1.3f,-7.81f), new Vector3(1.07f,2.6f,.12f), p[1]);
                Cube(root, "Airlock glass", new Vector3(side*.56f,1.65f,-7.73f), new Vector3(.62f,.8f,.04f), p[13]);
            }
        }

        private static void BuildStructureAndLighting(Transform parent, Material[] p)
        {
            Transform root = Group("02 - Steel structure and overhead luminaires", parent);
            for (int z = -6; z <= 9; z += 5)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Cube(root, "Column", new Vector3(side*7.65f,3.25f,z), new Vector3(.25f,6.5f,.3f), p[2]);
                    Cube(root, "Column foot", new Vector3(side*7.65f,.15f,z), new Vector3(.46f,.3f,.48f), p[3], true);
                }
                Cube(root, "Primary beam web", new Vector3(0f,6.48f,z), new Vector3(15.55f,.42f,.12f), p[2]);
                Cube(root, "Beam lower flange", new Vector3(0f,6.23f,z), new Vector3(15.65f,.08f,.42f), p[2]);
                Cube(root, "Beam upper flange", new Vector3(0f,6.73f,z), new Vector3(15.65f,.08f,.42f), p[2]);
            }
            foreach (float x in new[] {-4.2f,4.2f})
            {
                Cube(root, "Longitudinal service rail", new Vector3(x,6.90f,1f), new Vector3(.18f,.18f,18f), p[2]);
                for (int z = -5; z <= 7; z += 4)
                {
                    Cube(root, "Fluorescent housing", new Vector3(x,6.08f,z), new Vector3(.72f,.14f,2.65f), p[2]);
                    Cube(root, "Diffused overhead panel", new Vector3(x,5.998f,z), new Vector3(.62f,.025f,2.53f), p[6]);
                    for (int end = -1; end <= 1; end += 2)
                        Cube(root, "Luminaire suspension", new Vector3(x,6.5f,z+end), new Vector3(.025f,.7f,.025f), p[5]);
                }
            }
            Cube(root, "Assembly task light frame", new Vector3(0f,4.75f,1.5f), new Vector3(3.8f,.12f,2.5f), p[2]);
            Cube(root, "Assembly task light diffuser", new Vector3(0f,4.675f,1.5f), new Vector3(3.62f,.025f,2.32f), p[6]);
            foreach (float x in new[] {-1.65f,1.65f})
                foreach (float z in new[] {.5f,2.5f})
                    Cube(root, "Task light hanger", new Vector3(x,5.8f,z), new Vector3(.035f,2f,.035f), p[5]);
            var lightRoot = Group("Facility illumination - one shadow caster", root);
            Light key = lightRoot.gameObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f,.96f,.90f);
            key.intensity = 1.1f;
            key.shadows = LightShadows.Hard;
            key.shadowStrength = .55f;
            lightRoot.rotation = Quaternion.Euler(74f,-28f,0f);
            Light task = Group("Assembly fill - no shadows", root).gameObject.AddComponent<Light>();
            task.transform.localPosition = new Vector3(0f,4.3f,1.5f);
            task.type = LightType.Point;
            task.range = 8f;
            task.intensity = .65f;
            task.color = new Color(.9f,.95f,1f);
            task.shadows = LightShadows.None;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name.Contains("light") || renderer.name.Contains("Luminaire") || renderer.name.Contains("luminaire") ||
                    renderer.name.Contains("Fluorescent") || renderer.name.Contains("overhead panel"))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BuildObservationGallery(Transform parent, Material[] p)
        {
            Transform root = Group("03 - Glazed observation gallery", parent);
            Cube(root, "Gallery backing", new Vector3(10.1f,2.2f,3f), new Vector3(.15f,4.4f,14f), p[1]);
            Cube(root, "Gallery ceiling", new Vector3(9f,3.8f,3f), new Vector3(2.2f,.12f,14f), p[1]).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(root, "Gallery south return", new Vector3(9f,2.2f,-4.05f), new Vector3(2.2f,4.4f,.1f), p[1]);
            Cube(root, "Window sill", new Vector3(8f,1.12f,3f), new Vector3(.34f,.08f,14f), p[5]);
            Cube(root, "Window head", new Vector3(8f,3.55f,3f), new Vector3(.24f,.10f,14f), p[5]);
            for (int i = 0; i < 6; i++)
            {
                float z = -4f + i*2.8f;
                Cube(root, "Window mullion", new Vector3(8f,2.34f,z), new Vector3(.22f,2.45f,.07f), p[5]);
                if (i == 5) continue;
                GameObject glass = Cube(root, "Observation pane", new Vector3(8.01f,2.34f,z+1.4f), new Vector3(.012f,2.34f,2.73f), p[7], true);
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                glass.GetComponent<Renderer>().receiveShadows = false;
            }
            foreach (float z in new[] {-1.8f,2.8f,7.4f})
            {
                Transform station = Group("Gallery engineering desk", root);
                station.localPosition = new Vector3(9.15f,0f,z);
                station.localRotation = Quaternion.Euler(0f,90f,0f);
                Bench(station, Vector3.zero, new Vector2(1.8f,.65f), p);
                Monitor(station, new Vector3(0f,1.17f,.14f), p);
                Cube(station, "Keyboard", new Vector3(0f,.965f,-.13f), new Vector3(.46f,.035f,.15f), p[2]);
                Cube(station, "Chair seat", new Vector3(0f,.48f,-.68f), new Vector3(.46f,.12f,.44f), p[3]);
                Cube(station, "Chair back", new Vector3(0f,.83f,-.87f), new Vector3(.46f,.58f,.08f), p[3]);
                Cube(station, "Chair pedestal", new Vector3(0f,.23f,-.68f), new Vector3(.08f,.46f,.08f), p[5]);
            }
        }

        private static void BuildWorkFloor(Transform parent, Material[] p)
        {
            Transform root = Group("04 - Central integration floor and component benches", parent);
            // Flush floor markings leave the entire aisle navigable; no raised decorative kerbs.
            Outline(root, "Integration zone", new Vector3(0f,.009f,1.5f), new Vector2(5.5f,5f), .045f, p[3]);
            foreach (float x in new[] {-2.75f,2.75f})
                foreach (float z in new[] {-1f,4f})
                    Cube(root, "Safety corner", new Vector3(x,.013f,z), new Vector3(.35f,.008f,.18f), p[12]);
            Cube(root, "Arrival aisle stripe left", new Vector3(-1.6f,.008f,-4.1f), new Vector3(.035f,.008f,4.2f), p[1]);
            Cube(root, "Arrival aisle stripe right", new Vector3(1.6f,.008f,-4.1f), new Vector3(.035f,.008f,4.2f), p[1]);

            Transform platform = Group("Opportunity central assembly fixture", root);
            platform.localPosition = new Vector3(0f,0f,1.5f);
            Cube(platform, "Recessed fixture plinth", new Vector3(0f,.20f,0f), new Vector3(2.2f,.4f,2.0f), p[2], true);
            Cube(platform, "Fixture cabinet", new Vector3(0f,.52f,0f), new Vector3(2.55f,.5f,2.18f), p[1], true);
            Cube(platform, "Machined assembly surface", new Vector3(0f,.735f,0f), new Vector3(2.8f,.09f,2.4f), p[5], true);
            Cube(platform, "Clean protective mat", new Vector3(0f,.786f,0f), new Vector3(2.62f,.012f,2.22f), p[4]);
            Cube(platform, "Fixture blue fascia", new Vector3(0f,.53f,-.765f), new Vector3(2.5f,.21f,.025f), p[3]);
            Label(platform, "Fixture identification plate", new Vector3(0f,.535f,-.785f), Quaternion.identity,
                new Vector2(2.2f,.16f), "OPPORTUNITY   /   EDUCATIONAL ASSEMBLY", .063f, Color.white, TextAlignmentOptions.Center);

            // Three separate trays on one continuous bench, all approached from the clear central aisle.
            Transform benches = Group("Component preparation bench", root);
            benches.localPosition = new Vector3(-4.65f,0f,1.25f);
            // The full solar array spans more than two meters. A wide preparation table keeps it
            // supported while leaving a clear aisle between the bench and central fixture.
            Bench(benches, Vector3.zero, new Vector2(2.8f,4.75f), p);
            string[] names = { "01   SOLAR PANEL", "02   CAMERA MAST", "03   INSTRUMENT ARM" };
            for (int i = 0; i < 3; i++)
            {
                float z = -1.55f+i*1.55f;
                Cube(benches, "Anti-static component tray " + (i+1), new Vector3(0f,.966f,z), new Vector3(2.55f,.025f,1.35f), p[3]);
                Placard(benches, names[i], new Vector3(.70f,1.06f,z), Quaternion.Euler(0f,-90f,0f), new Vector2(1.1f,.19f), .062f, p);
            }
            // The systems console is off the assembly platform, leaving the rover unobstructed.
            Transform console = Group("Systems-check console furniture", root);
            console.localPosition = new Vector3(4.4f,0f,.6f);
            Cube(console, "Console plinth", new Vector3(0f,.10f,0f), new Vector3(2.35f,.2f,.77f), p[4], true);
            Cube(console, "Console cabinet", new Vector3(0f,.60f,0f), new Vector3(2.6f,1f,.85f), p[1], true);
            Cube(console, "Control deck", new Vector3(0f,1.11f,-.12f), new Vector3(2.7f,.1f,1f), p[2], true);
            Cube(console, "Display pedestal", new Vector3(0f,1.34f,.1f), new Vector3(.35f,.4f,.16f), p[5]);
            DisplayHousing(root, "Systems monitor", ConsoleCenter, Quaternion.identity, new Vector2(2.08f,.94f), p);
            Placard(console, "SYSTEMS VERIFICATION", new Vector3(0f,.75f,-.44f), Quaternion.identity, new Vector2(1.9f,.2f), .075f, p);
            // Fixed cable paths follow the perimeter and never cross the pedestrian approach.
            Cube(root, "Console cable trench", new Vector3(6.15f,.008f,1.1f), new Vector3(3.5f,.008f,.09f), p[5]);
            Cube(root, "West service cable route", new Vector3(-6.1f,.008f,3.7f), new Vector3(.09f,.008f,9.4f), p[5]);
        }

        private static void BuildServiceWall(Transform parent, Material[] p)
        {
            Transform root = Group("05 - Engineering services and storage", parent);
            foreach (float x in new[] {-6.6f,-5.45f})
            {
                Cube(root, "Instrument rack enclosure", new Vector3(x,1.12f,8.92f), new Vector3(.92f,2.24f,.8f), p[2], true);
                Cube(root, "Rack recessed face", new Vector3(x,1.15f,8.505f), new Vector3(.79f,1.98f,.04f), p[4]);
                for (int i = 0; i < 6; i++)
                {
                    Cube(root, "Rack instrument module", new Vector3(x,.42f+i*.28f,8.465f), new Vector3(.72f,.23f,.04f), p[5]);
                    Cube(root, "Module readout", new Vector3(x-.15f,.42f+i*.28f,8.435f), new Vector3(.28f,.075f,.02f), p[13]);
                    Cube(root, "Module switch", new Vector3(x+.22f,.42f+i*.28f,8.435f), new Vector3(.035f,.035f,.02f), p[11]);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float x = 4.3f+i*1.1f;
                Cube(root, "Storage cabinet", new Vector3(x,1.05f,9.18f), new Vector3(1.02f,2.1f,.7f), p[2], true);
                for (int side = -1; side <= 1; side += 2)
                {
                    Cube(root, "Cabinet door", new Vector3(x+side*.25f,1.1f,8.81f), new Vector3(.48f,1.9f,.04f), p[1]);
                    Cube(root, "Cabinet handle", new Vector3(x+side*.06f,1.1f,8.77f), new Vector3(.025f,.18f,.035f), p[5]);
                }
            }
            Transform station = Group("Engineering paperwork station", root);
            station.localPosition = new Vector3(-6.65f,0f,-3.3f);
            station.localRotation = Quaternion.Euler(0f,-90f,0f);
            Bench(station, Vector3.zero, new Vector2(2.1f,.85f), p);
            Monitor(station, new Vector3(-.45f,1.2f,.2f), p);
            Cube(station, "Keyboard", new Vector3(-.45f,.96f,-.15f), new Vector3(.5f,.035f,.18f), p[2]);
            Cube(station, "Inspection log binder", new Vector3(.5f,.97f,0f), new Vector3(.38f,.06f,.3f), p[3]);
            Cube(station, "Inspection sheet", new Vector3(.5f,1.005f,-.01f), new Vector3(.33f,.006f,.26f), p[14]);
            Cube(root, "Perimeter overhead cable tray", new Vector3(-7.65f,4.6f,2f), new Vector3(.4f,.12f,14f), p[5]);
            for (int i = 0; i < 3; i++)
                Cube(root, "Contained cable bundle", new Vector3(-7.77f+i*.11f,4.68f,2f), new Vector3(.055f,.055f,14f), p[4]);
            Cube(root, "Rack service drop", new Vector3(-7.72f,2.6f,8.8f), new Vector3(.13f,4.1f,.13f), p[5]);
        }

        private static void BuildBriefing(Transform parent, Material[] p)
        {
            Transform root = Group("06 - Arrival and mission briefing", parent);
            root.localPosition = new Vector3(-3.4f,0f,-5f);
            root.localRotation = Quaternion.Euler(0f,20f,0f);
            Cube(root, "Briefing stand foot", new Vector3(0f,.07f,0f), new Vector3(1.2f,.14f,.75f), p[2], true);
            Cube(root, "Briefing stand", new Vector3(0f,.7f,.08f), new Vector3(.15f,1.35f,.15f), p[5]);
            DisplayHousing(root, "Mission briefing display", new Vector3(0f,1.55f,0f), Quaternion.identity, new Vector2(2.05f,1.24f), p);
            Label(root, "Briefing eyebrow", new Vector3(0f,2.01f,-.1f), Quaternion.identity, new Vector2(1.82f,.16f),
                "MISSION BRIEF   /   MER-B", .065f, new Color(.53f,.73f,.82f), TextAlignmentOptions.Left);
            Label(root, "Opportunity title", new Vector3(0f,1.80f,-.1f), Quaternion.identity, new Vector2(1.82f,.28f),
                "OPPORTUNITY", .16f, Color.white, TextAlignmentOptions.Left);
            Label(root, "Mission purpose", new Vector3(0f,1.48f,-.1f), Quaternion.identity, new Vector2(1.82f,.40f),
                "Read the rocks.\nFind evidence of past water on Mars.", .073f, Color.white, TextAlignmentOptions.Left);
            Label(root, "Assembly instruction", new Vector3(0f,1.16f,-.1f), Quaternion.identity, new Vector2(1.82f,.23f),
                "Fit three components. Then run the systems check.", .061f, new Color(.72f,.82f,.87f), TextAlignmentOptions.Left);
            Placard(root, "NASA / JPL INSPIRED   -   EDUCATIONAL SIMPLIFICATION", new Vector3(0f,.92f,-.08f),
                Quaternion.identity, new Vector2(2.1f,.14f), .043f, p);
        }

        public static GameObject BuildScaffold(Transform parent, Material[] p, out GameObject visualRoot)
        {
            Transform root = Group("TEMPORARY ASSEMBLY SCAFFOLD - educational simplification", parent);
            root.localPosition = RoverOrigin;
            visualRoot = Group("Temporary primitive rover chassis and wheels", root).gameObject;
            Transform v = visualRoot.transform;
            Cube(v, "Simplified rover chassis", new Vector3(0f,.12f,0f), new Vector3(1.05f,.26f,.82f), p[8]);
            Cube(v, "Front equipment deck", new Vector3(0f,.03f,-.4f), new Vector3(.73f,.13f,.22f), p[4]);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(v, "Suspension rail", new Vector3(side*.53f,-.04f,0f), new Vector3(.08f,.09f,.84f), p[5]);
                for (int axle = -1; axle <= 1; axle++)
                {
                    var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = "Temporary rover wheel";
                    wheel.transform.SetParent(v,false);
                    wheel.transform.localPosition = new Vector3(side*.63f,-.02f,axle*.30f);
                    wheel.transform.localRotation = Quaternion.Euler(0f,0f,90f);
                    wheel.transform.localScale = new Vector3(.34f,.10f,.34f);
                    wheel.GetComponent<Renderer>().sharedMaterial = p[9];
                    wheel.GetComponent<Collider>().enabled = false;
                }
            }
            Vector2[] sizes = { new Vector2(1.12f,.76f), new Vector2(.39f,.36f), new Vector2(.87f,.27f) };
            for (int i = 0; i < 3; i++)
            {
                Transform socket = Group(SocketNames[i],root);
                socket.localPosition = SocketPositions[i];
                Transform guide = Group("Mount guide",socket);
                guide.localPosition = Vector3.up * (i == 1 ? -.35f : -.035f);
                Outline(guide, "Alignment edge", Vector3.zero, sizes[i], .018f, p[11]);
                Cube(guide, "Number plate", new Vector3(-sizes[i].x*.5f,0f,-sizes[i].y*.5f), new Vector3(.14f,.018f,.14f), p[3]);
                Label(guide, "Mount number", new Vector3(-sizes[i].x*.5f,.013f,-sizes[i].y*.5f), Quaternion.Euler(90f,0f,0f),
                    new Vector2(.13f,.13f), "0"+(i+1), .065f, Color.white, TextAlignmentOptions.Center);
            }
            return root.gameObject;
        }

        public static void BuildPartVisuals(Transform parent, OpportunityPartKind kind, Material[] p)
        {
            if (kind == OpportunityPartKind.SolarPanel)
            {
                Cube(parent,"Panel frame",Vector3.zero,new Vector3(1.08f,.055f,.72f),p[8]);
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 4; col++)
                        Cube(parent,"Photovoltaic cell",new Vector3(-.39f+col*.26f,.034f,-.25f+row*.25f),new Vector3(.235f,.025f,.215f),p[10]);
            }
            else if (kind == OpportunityPartKind.CameraMast)
            {
                Cube(parent,"Mast foot",new Vector3(0f,-.29f,0f),new Vector3(.36f,.12f,.33f),p[4]);
                Cube(parent,"Camera mast",new Vector3(0f,.05f,0f),new Vector3(.12f,.65f,.12f),p[2]);
                Cube(parent,"Stereo camera bar",new Vector3(0f,.38f,.025f),new Vector3(.48f,.16f,.18f),p[1]);
                for (int side = -1; side <= 1; side += 2)
                    Cube(parent,"Camera lens",new Vector3(side*.16f,.38f,-.075f),new Vector3(.11f,.11f,.035f),p[10]);
            }
            else
            {
                Cube(parent,"Arm shoulder",new Vector3(-.23f,0f,0f),new Vector3(.19f,.23f,.21f),p[4]);
                Cube(parent,"Arm segment",new Vector3(.03f,.02f,0f),new Vector3(.54f,.11f,.12f),p[8]);
                Cube(parent,"Instrument turret",new Vector3(.35f,-.015f,0f),new Vector3(.20f,.20f,.20f),p[4]);
                Cube(parent,"Contact plate",new Vector3(.45f,-.14f,0f),new Vector3(.26f,.08f,.25f),p[8]);
            }
        }

        public static TMP_Text BuildStatusDisplay(Transform parent)
        {
            return Label(parent,"Systems checklist on physical monitor",ConsoleCenter + new Vector3(0f,0f,-.1f),
                Quaternion.identity,new Vector2(1.88f,.78f),"COMPONENTS   0 / 3\n\nFit parts to matching numbered mounts.\nSystems check awaiting assembly.",
                .065f,new Color(.88f,.95f,.96f),TextAlignmentOptions.Left);
        }

        public static GameObject BuildConsoleButton(Transform parent, int index, Material[] p, out TMP_Text text)
        {
            string[] labels = { "RESET", "SYSTEMS CHECK", "READY FOR LAUNCH" };
            float[] offsets = { -.91f, -.15f, .76f };
            float[] widths = { .54f, .80f, .90f };
            Transform root = Group(labels[index]+" button",parent);
            root.localPosition = new Vector3(4.4f+offsets[index],1.28f,.03f);
            root.localRotation = Quaternion.Euler(26f,0f,0f);
            Vector3 size = new Vector3(widths[index],.22f,.06f);
            Cube(root,"Button face",Vector3.zero,size,p[index == 0 ? 5 : 3]);
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            text = Label(root,"Button legend",new Vector3(0f,0f,-.034f),Quaternion.identity,
                new Vector2(size.x-.035f,.19f),labels[index],.05f,Color.white,TextAlignmentOptions.Center);
            return root.gameObject;
        }

        public static TMP_Text Label(Transform parent, string name, Vector3 position, Quaternion rotation,
            Vector2 size, string content, float fontMeters, Color color, TextAlignmentOptions alignment)
        {
            var canvasObject = new GameObject(name,typeof(RectTransform),typeof(Canvas));
            canvasObject.transform.SetParent(parent,false);
            canvasObject.transform.localPosition = position;
            canvasObject.transform.localRotation = rotation;
            canvasObject.transform.localScale = Vector3.one*.001f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = size*1000f;
            var textObject = new GameObject("Display text",typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform,false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = fontMeters*1000f;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void Placard(Transform parent, string text, Vector3 position, Quaternion rotation, Vector2 size, float fontMeters, Material[] p)
        {
            Transform root = Group("Fixed identification plate",parent);
            root.localPosition = position;
            root.localRotation = rotation;
            Cube(root,"Plate",Vector3.zero,new Vector3(size.x,size.y,.025f),p[3]);
            Label(root,"Plate lettering",new Vector3(0f,0f,-.016f),Quaternion.identity,size*.94f,text,fontMeters,Color.white,TextAlignmentOptions.Center);
        }

        private static void DisplayHousing(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Material[] p)
        {
            Transform root = Group(name,parent);
            root.localPosition = position;
            root.localRotation = rotation;
            Cube(root,"Monitor bezel",Vector3.zero,new Vector3(size.x+.12f,size.y+.12f,.16f),p[4]);
            Cube(root,"Monitor screen",new Vector3(0f,0f,-.085f),new Vector3(size.x,size.y,.008f),p[13]);
        }

        private static void Monitor(Transform parent, Vector3 position, Material[] p)
        {
            DisplayHousing(parent,"Engineering workstation monitor",position,Quaternion.identity,new Vector2(.55f,.38f),p);
            Cube(parent,"Monitor stand",position+new Vector3(0f,-.20f,.03f),new Vector3(.12f,.18f,.12f),p[2]);
            // Quiet telemetry graphics, without extra blocks of unreadable placeholder prose.
            for (int i = 0; i < 4; i++)
                Cube(parent,"Telemetry trace",position+new Vector3(-.04f,-.10f+i*.065f,-.094f),new Vector3(.32f-i*.045f,.012f,.006f),p[11]);
        }

        private static void Bench(Transform parent, Vector3 position, Vector2 size, Material[] p)
        {
            Cube(parent,"Laminate bench surface",position+Vector3.up*.91f,new Vector3(size.x,.08f,size.y),p[1],true);
            Cube(parent,"Bench lower shelf",position+Vector3.up*.26f,new Vector3(size.x-.16f,.06f,size.y-.16f),p[2]);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Cube(parent,"Bench leg",position+new Vector3(x*(size.x*.5f-.08f),.44f,z*(size.y*.5f-.08f)),new Vector3(.07f,.88f,.07f),p[5],true);
        }

        private static void Outline(Transform parent,string name,Vector3 center,Vector2 size,float width,Material material)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(parent,name,center+new Vector3(side*size.x*.5f,0f,0f),new Vector3(width,.008f,size.y),material);
                Cube(parent,name,center+new Vector3(0f,0f,side*size.y*.5f),new Vector3(size.x,.008f,width),material);
            }
        }

        public static Transform Group(string name,Transform parent)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent,false);
            return result;
        }

        public static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool solid = false)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent,false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.GetComponent<Collider>().enabled = solid;
            return cube;
        }
    }
}
