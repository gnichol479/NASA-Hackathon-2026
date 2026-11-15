using LastSignal.OpportunityFlashback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LastSignal.OpportunityFlashback.Editor
{
    /// <summary>Creates a saved, non-interactive Edit Mode view of the runtime-built facility.</summary>
    public static class OpportunityFacilityPreviewBuilder
    {
        private const string PreviewName = "EDITOR PREVIEW — NASA/JPL Opportunity Bay";
        private const string MaterialsFolder = "Assets/Opportunity/NASA-Headquarters/LastSignalFlashback/Materials/FacilityPreview";

        private static readonly Color Navy = new Color(0.035f, 0.09f, 0.17f);
        private static readonly Color Blue = new Color(0.07f, 0.18f, 0.29f);
        private static readonly Color Cyan = new Color(0.12f, 0.72f, 0.84f);
        private static readonly Color Orange = new Color(0.96f, 0.38f, 0.12f);
        private static readonly Color White = new Color(0.92f, 0.95f, 0.97f);

        [InitializeOnLoadMethod]
        private static void RegisterAutoPreview()
        {
            EditorApplication.delayCall += EnsurePreviewForOpenFlashbackScene;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current)
        {
            EditorApplication.delayCall += EnsurePreviewForOpenFlashbackScene;
        }

        private static void EnsurePreviewForOpenFlashbackScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path) ||
                !scene.path.Replace('\\', '/').EndsWith("NASA-Headquarters/LastSignalFlashback/Scenes/Opportunity-Flashback.unity"))
                return;

            foreach (OpportunityFlashbackSceneController candidate in Resources.FindObjectsOfTypeAll<OpportunityFlashbackSceneController>())
            {
                if (candidate == null || candidate.gameObject.scene != scene)
                    continue;

                int previewCount = 0;
                bool hasCompletePreview = false;
                for (int i = 0; i < candidate.transform.childCount; i++)
                {
                    Transform child = candidate.transform.GetChild(i);
                    if (child.name != PreviewName)
                        continue;
                    previewCount++;
                    hasCompletePreview |= child.Find("TEMPORARY ASSEMBLY SCAFFOLD — educational simplification") != null;
                }

                if (previewCount != 1 || !hasCompletePreview)
                {
                    BuildPreview();
                    return;
                }
            }
        }

        [MenuItem("Last Signal/Opportunity Flashback/Build Visible Facility Preview")]
        public static void BuildPreview()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path) ||
                !scene.path.Replace('\\', '/').EndsWith("NASA-Headquarters/LastSignalFlashback/Scenes/Opportunity-Flashback.unity"))
            {
                Debug.LogError("Open NASA-Headquarters/LastSignalFlashback/Scenes/Opportunity-Flashback.unity first.");
                return;
            }

            OpportunityFlashbackSceneController controller = null;
            foreach (OpportunityFlashbackSceneController candidate in Resources.FindObjectsOfTypeAll<OpportunityFlashbackSceneController>())
            {
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    controller = candidate;
                    break;
                }
            }
            if (controller == null)
            {
                Debug.LogError("The active flashback scene has no Opportunity Flashback Scene Controller.");
                return;
            }

            EnsureMaterialFolders();
            for (int i = controller.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = controller.transform.GetChild(i);
                if (child.name == PreviewName)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }

            var rootObject = new GameObject(PreviewName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Build Opportunity facility preview");
            rootObject.transform.SetParent(controller.transform, false);

            BuildFacility(rootObject.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = controller.gameObject;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.pivot = new Vector3(0f, 1.9f, 1.45f);
                sceneView.rotation = Quaternion.Euler(12f, 0f, 0f);
                sceneView.size = 6.8f;
                sceneView.Repaint();
            }
            Debug.Log("Visible NASA/JPL-inspired facility preview built and saved in the Opportunity flashback scene.");
        }

        private static void BuildFacility(Transform root)
        {
            Material floor = Mat("Floor", new Color(0.20f, 0.25f, 0.30f), 0.12f);
            Material wall = Mat("CleanRoomWall", new Color(0.76f, 0.80f, 0.82f), 0.05f);
            Material blue = Mat("MissionBlue", Blue, 0.18f);
            Material orange = Mat("SafetyOrange", Orange, 0.12f);
            Material dark = Mat("Graphite", new Color(0.045f, 0.065f, 0.085f), 0.35f);
            Material metal = Mat("BrushedMetal", new Color(0.56f, 0.62f, 0.64f), 0.58f);
            Material chassis = Mat("RoverChassis", new Color(0.84f, 0.78f, 0.61f), 0.32f);
            Material tire = Mat("RoverTire", new Color(0.16f, 0.19f, 0.21f), 0.2f);
            Material cyan = Mat("SocketCyan", Cyan, 0.18f);
            Material solar = Mat("SolarCells", new Color(0.035f, 0.15f, 0.30f), 0.48f);
            Material glass = Mat("CameraGlass", new Color(0.06f, 0.37f, 0.61f), 0.6f);
            Material armGold = Mat("InstrumentGold", new Color(0.82f, 0.68f, 0.43f), 0.4f);

            // Open-front clean room with a work bay, service panels, lighting and safety lanes.
            Cube(root, "Integration Bay Floor", Vector3.zero, new Vector3(9f, 0.18f, 9f), floor);
            Cube(root, "Rear Clean-Room Wall", new Vector3(0f, 2.2f, 4.55f), new Vector3(9f, 4.4f, 0.18f), wall);
            Cube(root, "Left Clean-Room Wall", new Vector3(-4.55f, 2.2f, 0f), new Vector3(0.18f, 4.4f, 9f), wall);
            Cube(root, "Right Clean-Room Wall", new Vector3(4.55f, 2.2f, 0f), new Vector3(0.18f, 4.4f, 9f), wall);
            Cube(root, "Mission Blue Wall Band", new Vector3(0f, 3.72f, 4.42f), new Vector3(8.7f, 0.9f, 0.12f), blue);
            Cube(root, "Open Bay Header", new Vector3(0f, 4.25f, 0.15f), new Vector3(9f, 0.16f, 0.24f), dark);
            Cube(root, "Assembly Workbench Top", new Vector3(0f, 0.91f, 1.85f), new Vector3(4.1f, 0.2f, 1.65f), dark);
            Cube(root, "Workbench Lower Shelf", new Vector3(0f, 0.42f, 1.85f), new Vector3(3.7f, 0.1f, 1.3f), metal);
            for (int side = -1; side <= 1; side += 2)
                for (int front = -1; front <= 1; front += 2)
                    Cube(root, "Workbench Leg", new Vector3(side * 1.72f, 0.66f, 1.22f + (front + 1) * 0.63f),
                        new Vector3(0.1f, 0.48f, 0.1f), metal);

            for (int i = 0; i < 8; i++)
                Cube(root, "Workbench Hazard Marking " + (i + 1), new Vector3(-2f + i * 0.57f, 1.02f, 1.0f),
                    new Vector3(0.28f, 0.018f, 0.045f), orange);
            Cube(root, "Safety Lane Left", new Vector3(-2.42f, 0.013f, 1f), new Vector3(0.035f, 0.012f, 4.6f), orange);
            Cube(root, "Safety Lane Right", new Vector3(2.42f, 0.013f, 1f), new Vector3(0.035f, 0.012f, 4.6f), orange);

            for (int i = 0; i < 5; i++)
                Cube(root, "Rear Instrument Panel " + (i + 1), new Vector3(-3.5f + i * 1.75f, 2.25f, 4.42f),
                    new Vector3(1.55f, 1.55f, 0.08f), i % 2 == 0 ? wall : metal);
            for (int i = 0; i < 3; i++)
            {
                float x = -2.4f + i * 2.4f;
                Cube(root, "Overhead Light Housing " + (i + 1), new Vector3(x, 4.05f, 1.3f),
                    new Vector3(1.4f, 0.08f, 0.44f), Mat("Light", new Color(0.72f, 0.86f, 0.96f), 0f, true));
                var lightObject = new GameObject("Bay Light " + (i + 1));
                lightObject.transform.SetParent(root, false);
                lightObject.transform.localPosition = new Vector3(x, 3.92f, 1.3f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.75f, 0.88f, 1f);
                light.range = 7f;
                light.intensity = 2.4f;
            }

            Text(root, "Facility Header", new Vector3(0f, 3.71f, 4.34f), 180f,
                "LAST SIGNAL  /  OPPORTUNITY DEVELOPMENT BAY", 0.075f, White);
            Text(root, "Assembly Instructions", new Vector3(0f, 2.55f, 4.34f), 180f,
                "GRAB  •  ALIGN WITH THE MARKER  •  RELEASE TO SNAP\nSecure all three simplified parts, then run the systems check.", 0.045f, White);
            Cube(root, "Mission Fact Panel", new Vector3(-4.4f, 2.2f, 1.4f), new Vector3(0.08f, 1.65f, 2.35f), blue);
            Text(root, "Mission Fact Text", new Vector3(-4.34f, 2.2f, 1.4f), 90f,
                "OPPORTUNITY\nMars Exploration Rover\n\nInvestigate rocks and soils\nfor evidence of past water", 0.034f, White);
            Cube(root, "Checklist Display Stand", new Vector3(2.95f, 0.62f, 1.85f), new Vector3(0.18f, 1.15f, 0.18f), dark);
            Cube(root, "Checklist Display", new Vector3(2.95f, 1.46f, 1.85f), new Vector3(0.95f, 0.72f, 0.1f), blue);
            Cube(root, "Facility Status Beacon", new Vector3(2.96f, 2.04f, 1.78f), new Vector3(0.13f, 0.13f, 0.13f), cyan);
            Text(root, "Checklist Display Text", new Vector3(2.895f, 1.47f, 1.79f), 180f,
                "OPPORTUNITY\nASSEMBLY  0 / 3\nSYSTEMS CHECK  LOCKED\nREADY FOR LAUNCH  LOCKED", 0.018f, White);

            BuildScaffold(root, chassis, tire, metal, cyan, solar);
            BuildSolarPanel(root, solar, orange);
            BuildCameraMast(root, metal, glass, dark);
            BuildInstrumentArm(root, armGold, dark);
            Text(root, "Temporary Scaffold Notice", new Vector3(0f, 1.56f, 2.76f), 180f,
                "TEMPORARY ASSEMBLY SCAFFOLD  •  EDUCATIONAL SIMPLIFICATION", 0.032f, Navy);
            Text(root, "Solar Part Label", new Vector3(-1.2f, 1.61f, 0.82f), 180f,
                "SOLAR PANEL\nSunlight → electrical power", 0.031f, White);
            Text(root, "Camera Part Label", new Vector3(0f, 1.61f, 0.78f), 180f,
                "CAMERA MAST\nStereo navigation & panoramas", 0.029f, White);
            Text(root, "Arm Part Label", new Vector3(1.2f, 1.61f, 0.82f), 180f,
                "INSTRUMENT ARM\nTools meet rock surfaces", 0.03f, White);
        }

        private static void BuildScaffold(Transform root, Material chassis, Material tire, Material metal, Material cyan, Material solar)
        {
            var rover = new GameObject("TEMPORARY ASSEMBLY SCAFFOLD — educational simplification");
            rover.transform.SetParent(root, false);
            rover.transform.localPosition = new Vector3(0f, 1.08f, 1.85f);
            Transform visuals = Child(rover.transform, "Primitive rover chassis and wheels");
            Cube(visuals, "Simplified Rover Chassis", new Vector3(0f, 0.12f, 0f), new Vector3(1.05f, 0.26f, 0.82f), chassis);
            Cube(visuals, "Front Equipment Deck", new Vector3(0f, 0.03f, -0.4f), new Vector3(0.73f, 0.13f, 0.22f), metal);
            Cube(visuals, "Rear Equipment Deck", new Vector3(0f, 0.03f, 0.4f), new Vector3(0.73f, 0.13f, 0.22f), metal);
            for (int side = -1; side <= 1; side += 2)
                for (int axle = -1; axle <= 1; axle++)
                    Cylinder(visuals, side < 0 ? "Left Rover Wheel" : "Right Rover Wheel",
                        new Vector3(side * 0.63f, -0.02f, axle * 0.3f), new Vector3(0.34f, 0.13f, 0.34f), tire);
            Cube(visuals, "Deck Sensor Block", new Vector3(0f, 0.31f, 0.13f), new Vector3(0.32f, 0.14f, 0.28f), chassis);
            Socket(rover.transform, "Solar Panel Socket", new Vector3(-0.49f, 0.34f, 0.04f), new Vector3(1.11f, 0.035f, 0.75f), cyan);
            Socket(rover.transform, "Camera Mast Socket", new Vector3(0f, 0.32f, 0.06f), new Vector3(0.39f, 0.06f, 0.36f), cyan);
            Socket(rover.transform, "Instrument Arm Socket", new Vector3(0.48f, 0.17f, -0.34f), new Vector3(0.86f, 0.05f, 0.25f), cyan);
        }

        private static void BuildSolarPanel(Transform root, Material cells, Material frame)
        {
            Transform part = PositionedChild(root, "Temporary Solar Panel — preview only", new Vector3(-1.18f, 1.15f, 0.94f));
            Cube(part, "Amber Frame", Vector3.zero, new Vector3(1.08f, 0.055f, 0.72f), frame);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                    Cube(part, "Solar Cell", new Vector3(-0.39f + col * 0.26f, 0.034f, -0.25f + row * 0.25f),
                        new Vector3(0.235f, 0.025f, 0.215f), cells);
        }

        private static void BuildCameraMast(Transform root, Material metal, Material glass, Material dark)
        {
            Transform part = PositionedChild(root, "Temporary Camera Mast — preview only", new Vector3(0f, 1.19f, 0.8f));
            Cube(part, "Mast Foot", new Vector3(0f, -0.29f, 0f), new Vector3(0.36f, 0.12f, 0.33f), dark);
            Cube(part, "Raised Mast", new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.65f, 0.12f), metal);
            Cube(part, "Stereo Camera Bar", new Vector3(0f, 0.38f, 0.025f), new Vector3(0.48f, 0.16f, 0.18f), metal);
            Cube(part, "Left Camera Lens", new Vector3(-0.16f, 0.38f, -0.075f), new Vector3(0.11f, 0.11f, 0.035f), glass);
            Cube(part, "Right Camera Lens", new Vector3(0.16f, 0.38f, -0.075f), new Vector3(0.11f, 0.11f, 0.035f), glass);
        }

        private static void BuildInstrumentArm(Transform root, Material gold, Material dark)
        {
            Transform part = PositionedChild(root, "Temporary Instrument Arm — preview only", new Vector3(1.18f, 1.15f, 0.91f));
            Cube(part, "Shoulder", new Vector3(-0.23f, 0f, 0f), new Vector3(0.19f, 0.23f, 0.21f), dark);
            Cube(part, "Arm Segment", new Vector3(0.03f, 0.02f, 0f), new Vector3(0.54f, 0.11f, 0.12f), gold);
            Cube(part, "Instrument Turret", new Vector3(0.35f, -0.015f, 0f), new Vector3(0.20f, 0.20f, 0.20f), dark);
            Cube(part, "Instrument Contact Plate", new Vector3(0.45f, -0.14f, 0f), new Vector3(0.26f, 0.08f, 0.25f), gold);
        }

        private static void Socket(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            Transform socket = PositionedChild(parent, name, position);
            Cube(socket, "Alignment Marker", Vector3.zero, size, material);
            Cube(socket, "Forward Alignment Bar", new Vector3(0f, size.y * 0.9f, -size.z * 0.43f),
                new Vector3(size.x * 0.55f, size.y * 0.55f, 0.035f), Mat("SafetyOrange", Orange, 0.12f));
        }

        private static void EnsureMaterialFolders()
        {
            const string opportunity = "Assets/Opportunity";
            const string headquarters = opportunity + "/NASA-Headquarters";
            const string flashback = headquarters + "/LastSignalFlashback";
            const string materials = flashback + "/Materials";
            if (!AssetDatabase.IsValidFolder(materials)) AssetDatabase.CreateFolder(flashback, "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder(materials, "FacilityPreview");
        }

        private static Material Mat(string name, Color color, float metallic = 0f, bool emissive = false)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = value.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return value;
        }

        private static void Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = position;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = scale;
            wheel.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
        }

        private static void Text(Transform parent, string name, Vector3 position, float yaw, string content, float characterSize, Color color)
        {
            string[] lines = content.Split('\n');
            int longestLine = 0;
            foreach (string line in lines)
                longestLine = Mathf.Max(longestLine, line.Length);
            float width = Mathf.Max(1.1f, longestLine * characterSize * 0.58f);
            float height = Mathf.Max(0.18f, lines.Length * characterSize * 1.7f);

            var canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = position;
            canvasObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.001f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width * 1000f, height * 1000f);

            var textObject = new GameObject(name + " Text", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            textObject.SetActive(false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 8f);
            textRect.offsetMax = new Vector2(-20f, -8f);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = 72;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            textObject.SetActive(true);
        }

        private static Transform PositionedChild(Transform parent, string name, Vector3 position)
        {
            Transform child = Child(parent, name);
            child.localPosition = position;
            return child;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }
    }
}
