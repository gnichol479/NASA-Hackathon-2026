using System;
using System.Collections;
using System.Collections.Generic;
using NasaHack;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.OpportunityFlashback
{
    [Serializable]
    public sealed class OpportunityLaunchHandoffEvent : UnityEvent { }

    /// <summary>
    /// Builds and controls the NASA/JPL-inspired flashback bay. The final rover model is optional
    /// and lives at a separate anchor; the simple scaffold and all interaction targets are temporary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpportunityFlashbackSceneController : MonoBehaviour
    {
        public const string ElevenLabsBrianVoiceId = "nPczcJzI2devNBz1zQrb";

        private const string IntroCaption =
            "Opportunity was built to study Martian rocks and soils for clues that water once flowed there.";
        private const string SolarCaption =
            "The solar arrays collected sunlight and converted it into electricity for the rover's batteries.";
        private const string MastCaption =
            "The camera mast raised stereo cameras for navigation and panoramic views of the terrain.";
        private const string ArmCaption =
            "The instrument arm placed science tools against rocks so Opportunity could examine their texture and chemistry.";
        private const string SystemsCaption =
            "Power, vision, and science tools are accounted for. Opportunity is ready for launch.";

        private static readonly Color Navy = new Color(0.035f, 0.09f, 0.17f, 1f);
        private static readonly Color Blue = new Color(0.04f, 0.24f, 0.43f, 1f);
        private static readonly Color Cyan = new Color(0.12f, 0.72f, 0.84f, 1f);
        private static readonly Color Orange = new Color(0.96f, 0.38f, 0.12f, 1f);
        private static readonly Color White = new Color(0.92f, 0.95f, 0.97f, 1f);

        [Header("Shared project XR setup")]
        [SerializeField] private GameObject sharedXrRigPrefab;

        [Header("Opportunity model")]
        [Tooltip("Empty child anchor in this scene. Assign the imported Opportunity model prefab below.")]
        [SerializeField] private Transform opportunityModelAnchor;
        [Tooltip("Optional final model prefab. The interaction scaffold remains separate and can be replaced later.")]
        [SerializeField] private GameObject finalOpportunityModelPrefab;

        [Header("Narration clips — ElevenLabs Brian")]
        [SerializeField] private string narrationVoiceId = ElevenLabsBrianVoiceId;
        [SerializeField] private AudioClip introNarration;
        [SerializeField] private AudioClip solarPanelNarration;
        [SerializeField] private AudioClip cameraMastNarration;
        [SerializeField] private AudioClip instrumentArmNarration;
        [SerializeField] private AudioClip systemsCheckNarration;
        [SerializeField] private AudioClip snapFeedbackClip;

        [Header("Launch handoff")]
        [Tooltip("Target scene key sent to the handoff event. This controller does not load that scene.")]
        [SerializeField] private string launchSceneName = "Opportunity-Launch";
        [SerializeField] private OpportunityLaunchHandoffEvent onLaunchHandoffRequested =
            new OpportunityLaunchHandoffEvent();

        private readonly List<OpportunityAssemblyPart> assemblyParts = new List<OpportunityAssemblyPart>(3);
        private readonly List<Renderer> componentRenderers = new List<Renderer>();

        private GameObject rigInstance;
        private Camera playerCamera;
        private AudioSource narrationSource;
        private AudioSource feedbackSource;
        private AudioClip generatedSnapTone;
        private GameObject scaffoldRoot;
        private GameObject scaffoldVisualRoot;
        private GameObject finalModelInstance;
        private Text statusText;
        private Text captionText;
        private CanvasGroup captionGroup;
        private OpportunityFlashbackWorldButton resetButton;
        private OpportunityFlashbackWorldButton systemsCheckButton;
        private OpportunityFlashbackWorldButton readyButton;
        private bool hasBuilt;
        private bool assemblyStarted = true;
        private bool systemsCheckPassed;
        private bool launchReady;

        public static event Action<string> LaunchHandoffRequested;

        public IReadOnlyList<OpportunityAssemblyPart> AssemblyParts => assemblyParts;
        public Transform OpportunityModelAnchor => opportunityModelAnchor;
        public int AssemblyProgress
        {
            get
            {
                int count = 0;
                foreach (OpportunityAssemblyPart part in assemblyParts)
                    if (part != null && part.IsSnapped)
                        count++;
                return count;
            }
        }
        public bool SystemsCheckPassed => systemsCheckPassed;
        public bool IsLaunchReady => launchReady;
        public OpportunityFlashbackWorldButton SystemsCheckButton => systemsCheckButton;
        public OpportunityFlashbackWorldButton ReadyButton => readyButton;
        public string NarrationVoiceId => narrationVoiceId;

        private void Awake()
        {
            // The serialized Edit Mode preview makes the scene easy to inspect. Runtime geometry
            // is built from the same controller, so hide the preview before creating that copy.
            Transform editorPreview = transform.Find("EDITOR PREVIEW — NASA/JPL Opportunity Bay");
            if (editorPreview != null)
                editorPreview.gameObject.SetActive(false);
            BuildExperience(false);
        }

        private void Start()
        {
            if (Application.isPlaying)
                StartCoroutine(PlayFlashbackEntry());
        }

        /// <summary>Used by the Editor verification menu without starting a Unity play session.</summary>
        public void InitializeForEditorVerification()
        {
            BuildExperience(true);
        }

        private void BuildExperience(bool verificationOnly)
        {
            if (hasBuilt)
                return;
            hasBuilt = true;

            BuildRoom();
            ResolveModelAnchor();
            BuildAssemblyScaffold();
            BuildPlayerRig(verificationOnly);
            BuildAssemblyParts();
            BuildNarrationCanvas();
            BuildStationPanelsAndButtons();
            BuildAudioSources();
            SetInitialState();
        }

        private void BuildRoom()
        {
            Material floor = MakeMaterial(new Color(0.20f, 0.25f, 0.30f), 0.12f);
            Material wall = MakeMaterial(new Color(0.76f, 0.80f, 0.82f), 0.05f);
            Material wallBlue = MakeMaterial(new Color(0.07f, 0.18f, 0.29f), 0.18f);
            Material white = MakeMaterial(White, 0.08f);
            Material orange = MakeMaterial(Orange, 0.1f);
            Material dark = MakeMaterial(new Color(0.045f, 0.065f, 0.085f), 0.35f);
            Material cyan = MakeMaterial(Cyan, 0.15f);

            CreateCube("Integration Bay Floor", transform, Vector3.zero, new Vector3(9f, 0.18f, 9f), floor, true);
            CreateCube("Rear Clean-Room Wall", transform, new Vector3(0f, 2.2f, 4.55f), new Vector3(9f, 4.4f, 0.18f), wall, true);
            CreateCube("Left Clean-Room Wall", transform, new Vector3(-4.55f, 2.2f, 0f), new Vector3(0.18f, 4.4f, 9f), wall, true);
            CreateCube("Right Clean-Room Wall", transform, new Vector3(4.55f, 2.2f, 0f), new Vector3(0.18f, 4.4f, 9f), wall, true);
            CreateCube("Rear Mission Blue Band", transform, new Vector3(0f, 3.65f, 4.42f), new Vector3(8.7f, 1.15f, 0.12f), wallBlue, false);
            CreateCube("Assembly Workbench", transform, new Vector3(0f, 0.91f, 1.85f), new Vector3(4.1f, 0.20f, 1.65f), dark, true);

            // Clean-room floor lanes and hazard markings make the assembly zone easy to read.
            CreateCube("Safety Lane Left", transform, new Vector3(-2.42f, 0.013f, 1.0f), new Vector3(0.035f, 0.012f, 4.6f), orange, false);
            CreateCube("Safety Lane Right", transform, new Vector3(2.42f, 0.013f, 1.0f), new Vector3(0.035f, 0.012f, 4.6f), orange, false);
            for (int i = 0; i < 8; i++)
            {
                float x = -2.0f + i * 0.57f;
                CreateCube("Workbench Hazard Stripe " + (i + 1), transform,
                    new Vector3(x, 1.019f, 1.0f), new Vector3(0.28f, 0.018f, 0.045f), orange, false);
            }

            // Modular equipment panels, overhead light bars, and a task sign establish a test bay.
            for (int i = 0; i < 5; i++)
            {
                float x = -3.5f + i * 1.75f;
                CreateCube("Rear Wall Equipment Panel " + (i + 1), transform,
                    new Vector3(x, 2.25f, 4.42f), new Vector3(1.55f, 1.55f, 0.08f), i % 2 == 0 ? wall : white, false);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = -2.4f + i * 2.4f;
                CreateCube("Ceiling Light Housing " + (i + 1), transform,
                    new Vector3(x, 4.05f, 1.3f), new Vector3(1.4f, 0.08f, 0.44f), white, false);
                AddPointLight("Ceiling Bay Light " + (i + 1), new Vector3(x, 3.92f, 1.3f),
                    new Color(0.75f, 0.88f, 1f), 7f, 2.4f);
            }

            AddTextPanel("Facility Header", transform, new Vector3(0f, 3.62f, 4.31f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(2100f, 260f), 0.0015f,
                "LAST SIGNAL  /  OPPORTUNITY DEVELOPMENT BAY", 104, TextAnchor.MiddleCenter, White, Color.clear);
            AddTextPanel("Clean Room Wayfinding", transform, new Vector3(-4.42f, 2.25f, 1.2f),
                Quaternion.Euler(0f, 90f, 0f), new Vector2(1000f, 210f), 0.0011f,
                "ASSEMBLY  •  INTEGRATION  •  TEST", 72, TextAnchor.MiddleCenter, new Color(0.14f, 0.31f, 0.44f), Color.clear);
            AddTextPanel("Assembly Instructions", transform, new Vector3(0f, 2.54f, 4.31f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(1650f, 420f), 0.00125f,
                "GRAB  •  ALIGN WITH THE MARKER  •  RELEASE TO SNAP\nUse a direct reach or controller ray. Secure all three simplified parts, then run the systems check.",
                62, TextAnchor.MiddleCenter, White, Color.clear);

            CreateCube("Status Console Stand", transform, new Vector3(2.95f, 0.62f, 1.85f), new Vector3(0.18f, 1.15f, 0.18f), dark, true);
            CreateCube("Status Console Display", transform, new Vector3(2.95f, 1.46f, 1.85f), new Vector3(0.95f, 0.72f, 0.10f), wallBlue, false);

            Light key = new GameObject("Clean Room Key Light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.78f, 0.86f, 1f);
            key.intensity = 1.15f;
            key.transform.SetParent(transform, false);
            key.transform.rotation = Quaternion.Euler(47f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.4f, 0.46f, 0.52f, 1f);
        }

        private void ResolveModelAnchor()
        {
            if (opportunityModelAnchor == null)
            {
                var anchorObject = new GameObject("OPPORTUNITY MODEL ANCHOR — assign the final model prefab here");
                anchorObject.transform.SetParent(transform, false);
                anchorObject.transform.localPosition = new Vector3(0f, 1.08f, 1.85f);
                opportunityModelAnchor = anchorObject.transform;
            }

            if (finalOpportunityModelPrefab != null && finalModelInstance == null)
            {
                finalModelInstance = Instantiate(finalOpportunityModelPrefab, opportunityModelAnchor, false);
                finalModelInstance.name = "Opportunity Final Model (assigned prefab)";
                finalModelInstance.SetActive(false);
            }
        }

        private void BuildAssemblyScaffold()
        {
            Material chassis = MakeMaterial(new Color(0.84f, 0.78f, 0.61f), 0.32f);
            Material chassisDark = MakeMaterial(new Color(0.25f, 0.29f, 0.31f), 0.62f);
            Material solarCell = MakeMaterial(new Color(0.035f, 0.15f, 0.30f), 0.5f);
            Material socket = MakeMaterial(new Color(0.12f, 0.45f, 0.54f), 0.24f);

            scaffoldRoot = new GameObject("TEMPORARY ASSEMBLY SCAFFOLD — educational simplification");
            scaffoldRoot.transform.SetParent(transform, false);
            scaffoldRoot.transform.localPosition = new Vector3(0f, 1.08f, 1.85f);
            scaffoldVisualRoot = new GameObject("Temporary primitive rover chassis and wheels");
            scaffoldVisualRoot.transform.SetParent(scaffoldRoot.transform, false);

            CreateCube("Simplified Rover Chassis", scaffoldVisualRoot.transform, new Vector3(0f, 0.12f, 0f),
                new Vector3(1.05f, 0.26f, 0.82f), chassis, false);
            CreateCube("Front Equipment Deck", scaffoldVisualRoot.transform, new Vector3(0f, 0.03f, -0.40f),
                new Vector3(0.73f, 0.13f, 0.22f), chassisDark, false);
            CreateCube("Rear Equipment Deck", scaffoldVisualRoot.transform, new Vector3(0f, 0.03f, 0.40f),
                new Vector3(0.73f, 0.13f, 0.22f), chassisDark, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = -1; axle <= 1; axle++)
                {
                    var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = side < 0 ? "Temporary Left Wheel" : "Temporary Right Wheel";
                    wheel.transform.SetParent(scaffoldVisualRoot.transform, false);
                    wheel.transform.localPosition = new Vector3(side * 0.63f, -0.02f, axle * 0.30f);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheel.transform.localScale = new Vector3(0.34f, 0.13f, 0.34f);
                    wheel.GetComponent<Renderer>().material = chassisDark;
                    Collider wheelCollider = wheel.GetComponent<Collider>();
                    if (wheelCollider != null)
                        wheelCollider.enabled = false;
                }
            }
            CreateCube("Rover Deck Sensor Block", scaffoldVisualRoot.transform, new Vector3(0f, 0.31f, 0.13f),
                new Vector3(0.32f, 0.14f, 0.28f), chassis, false);

            CreateSocket("Solar Panel Socket", new Vector3(-0.49f, 0.34f, 0.04f), new Vector3(1.11f, 0.035f, 0.75f), socket);
            CreateSocket("Camera Mast Socket", new Vector3(0f, 0.32f, 0.06f), new Vector3(0.39f, 0.06f, 0.36f), socket);
            CreateSocket("Instrument Arm Socket", new Vector3(0.48f, 0.17f, -0.34f), new Vector3(0.86f, 0.05f, 0.25f), socket);

            AddTextPanel("Scaffold Educational Notice", transform, new Vector3(0f, 1.54f, 2.77f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(1200f, 170f), 0.001f,
                "TEMPORARY ASSEMBLY SCAFFOLD  •  EDUCATIONAL SIMPLIFICATION", 54,
                TextAnchor.MiddleCenter, new Color(0.06f, 0.18f, 0.27f), Color.clear);
            AddTextPanel("Solar Tray Label", transform, new Vector3(-1.22f, 1.6f, 0.92f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(560f, 180f), 0.001f,
                "SOLAR PANEL\nSunlight → electrical power", 48, TextAnchor.MiddleCenter, White, Navy);
            AddTextPanel("Camera Tray Label", transform, new Vector3(0f, 1.6f, 0.86f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(600f, 180f), 0.001f,
                "CAMERA MAST\nStereo navigation & panoramas", 46, TextAnchor.MiddleCenter, White, Navy);
            AddTextPanel("Arm Tray Label", transform, new Vector3(1.22f, 1.6f, 0.92f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(560f, 180f), 0.001f,
                "INSTRUMENT ARM\nTools meet the rock surface", 47, TextAnchor.MiddleCenter, White, Navy);
        }

        private Transform CreateSocket(string socketName, Vector3 localPosition, Vector3 markerSize, Material markerMaterial)
        {
            var socketObject = new GameObject(socketName);
            socketObject.transform.SetParent(scaffoldRoot.transform, false);
            socketObject.transform.localPosition = localPosition;
            socketObject.transform.localRotation = Quaternion.identity;

            CreateCube(socketName + " alignment marker", socketObject.transform, Vector3.zero, markerSize, markerMaterial, false);
            var arrow = CreateCube(socketName + " forward alignment bar", socketObject.transform,
                new Vector3(0f, markerSize.y * 0.9f, -markerSize.z * 0.43f),
                new Vector3(markerSize.x * 0.55f, markerSize.y * 0.55f, 0.035f), MakeMaterial(Orange), false);
            arrow.transform.localRotation = Quaternion.identity;
            return socketObject.transform;
        }

        private void BuildAssemblyParts()
        {
            Material solarFrame = MakeMaterial(new Color(0.77f, 0.48f, 0.16f), 0.35f);
            Material solarCell = MakeMaterial(new Color(0.035f, 0.16f, 0.34f), 0.45f);
            Material mastMetal = MakeMaterial(new Color(0.75f, 0.80f, 0.81f), 0.48f);
            Material cameraGlass = MakeMaterial(new Color(0.06f, 0.37f, 0.61f), 0.55f);
            Material armMetal = MakeMaterial(new Color(0.82f, 0.68f, 0.43f), 0.4f);
            Material jointDark = MakeMaterial(new Color(0.21f, 0.26f, 0.30f), 0.62f);

            Transform solarTarget = scaffoldRoot.transform.Find("Solar Panel Socket");
            Transform mastTarget = scaffoldRoot.transform.Find("Camera Mast Socket");
            Transform armTarget = scaffoldRoot.transform.Find("Instrument Arm Socket");

            OpportunityAssemblyPart solar = CreatePart("Solar Panel — temporary grabbable part", OpportunityPartKind.SolarPanel,
                solarTarget, new Vector3(-1.18f, 1.13f, 0.88f), new Vector3(1.04f, 0.10f, 0.70f), 0.5f);
            CreateCube("Solar Panel Amber Frame", solar.transform, Vector3.zero,
                new Vector3(1.08f, 0.055f, 0.72f), solarFrame, false);
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float x = -0.39f + col * 0.26f;
                    float z = -0.25f + row * 0.25f;
                    CreateCube("Solar Cell " + (row * 4 + col + 1), solar.transform,
                        new Vector3(x, 0.034f, z), new Vector3(0.235f, 0.025f, 0.215f), solarCell, false);
                }
            }

            OpportunityAssemblyPart mast = CreatePart("Camera Mast — temporary grabbable part", OpportunityPartKind.CameraMast,
                mastTarget, new Vector3(0f, 1.19f, 0.80f), new Vector3(0.38f, 0.78f, 0.35f), 0.42f);
            CreateCube("Mast Foot", mast.transform, new Vector3(0f, -0.29f, 0f),
                new Vector3(0.36f, 0.12f, 0.33f), jointDark, false);
            CreateCube("Raised Camera Mast", mast.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(0.12f, 0.65f, 0.12f), mastMetal, false);
            CreateCube("Stereo Camera Bar", mast.transform, new Vector3(0f, 0.38f, 0.025f),
                new Vector3(0.48f, 0.16f, 0.18f), mastMetal, false);
            CreateCube("Left Camera Lens", mast.transform, new Vector3(-0.16f, 0.38f, -0.075f),
                new Vector3(0.11f, 0.11f, 0.035f), cameraGlass, false);
            CreateCube("Right Camera Lens", mast.transform, new Vector3(0.16f, 0.38f, -0.075f),
                new Vector3(0.11f, 0.11f, 0.035f), cameraGlass, false);

            OpportunityAssemblyPart arm = CreatePart("Instrument Arm — temporary grabbable part", OpportunityPartKind.InstrumentArm,
                armTarget, new Vector3(1.18f, 1.15f, 0.91f), new Vector3(0.88f, 0.23f, 0.24f), 0.42f);
            CreateCube("Instrument Arm Shoulder", arm.transform, new Vector3(-0.23f, 0f, 0f),
                new Vector3(0.19f, 0.23f, 0.21f), jointDark, false);
            CreateCube("Instrument Arm Segment", arm.transform, new Vector3(0.03f, 0.02f, 0f),
                new Vector3(0.54f, 0.11f, 0.12f), armMetal, false);
            CreateCube("Instrument Turret", arm.transform, new Vector3(0.35f, -0.015f, 0f),
                new Vector3(0.20f, 0.20f, 0.20f), jointDark, false);
            CreateCube("Instrument Contact Plate", arm.transform, new Vector3(0.45f, -0.14f, 0f),
                new Vector3(0.26f, 0.08f, 0.25f), armMetal, false);

            assemblyParts.Add(solar);
            assemblyParts.Add(mast);
            assemblyParts.Add(arm);
        }

        private OpportunityAssemblyPart CreatePart(
            string partName,
            OpportunityPartKind kind,
            Transform target,
            Vector3 spawnPosition,
            Vector3 colliderSize,
            float snapRadius)
        {
            GameObject partObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            partObject.name = partName;
            partObject.transform.SetParent(transform, true);
            partObject.transform.position = spawnPosition;
            partObject.transform.rotation = Quaternion.identity;
            partObject.transform.localScale = Vector3.one;
            Renderer placeholder = partObject.GetComponent<Renderer>();
            if (placeholder != null)
                placeholder.enabled = false;
            BoxCollider box = partObject.GetComponent<BoxCollider>();
            box.size = colliderSize;
            Rigidbody body = partObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            OpportunityAssemblyPart part = partObject.AddComponent<OpportunityAssemblyPart>();
            part.Configure(this, kind, target, snapRadius, spawnPosition, Quaternion.identity);
            foreach (Renderer childRenderer in partObject.GetComponentsInChildren<Renderer>(true))
                if (childRenderer.enabled)
                    componentRenderers.Add(childRenderer);
            return part;
        }

        private void BuildPlayerRig(bool verificationOnly)
        {
            if (!verificationOnly && sharedXrRigPrefab != null)
            {
                rigInstance = Instantiate(sharedXrRigPrefab, new Vector3(0f, 0f, -3.2f), Quaternion.identity);
                rigInstance.name = "Shared XR Rig — tracked controllers, ray interaction and locomotion";
                playerCamera = rigInstance.GetComponentInChildren<Camera>(true);
            }

            if (playerCamera == null)
            {
                rigInstance = new GameObject("Flashback Editor Fallback Rig");
                rigInstance.transform.SetPositionAndRotation(new Vector3(0f, 0f, -3.2f), Quaternion.identity);
                CharacterController character = rigInstance.AddComponent<CharacterController>();
                character.height = 1.8f;
                character.radius = 0.27f;
                character.center = new Vector3(0f, 0.9f, 0f);
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(rigInstance.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                playerCamera = cameraObject.AddComponent<Camera>();
                playerCamera.clearFlags = CameraClearFlags.SolidColor;
                playerCamera.backgroundColor = new Color(0.025f, 0.045f, 0.075f, 1f);
                playerCamera.nearClipPlane = 0.05f;
                playerCamera.farClipPlane = 70f;
                playerCamera.stereoTargetEye = StereoTargetEyeMask.Both;

                OpportunityFlashbackInputRig editorFallback = rigInstance.AddComponent<OpportunityFlashbackInputRig>();
                editorFallback.Configure(this, playerCamera, character);
            }

            if (verificationOnly && rigInstance != null)
                rigInstance.transform.SetParent(transform, true);

            if (playerCamera != null)
                playerCamera.tag = "MainCamera";
        }

        private void BuildNarrationCanvas()
        {
            if (playerCamera == null)
                return;

            var canvasObject = new GameObject("Captions — readable headset lower third", typeof(RectTransform));
            canvasObject.transform.SetParent(playerCamera.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = playerCamera;
            canvas.planeDistance = 1.25f;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject groupObject = new GameObject("Caption Card", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            groupObject.transform.SetParent(canvasObject.transform, false);
            RectTransform groupRect = groupObject.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.15f, 0.06f);
            groupRect.anchorMax = new Vector2(0.85f, 0.23f);
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;
            Image background = groupObject.GetComponent<Image>();
            background.color = new Color(0.012f, 0.035f, 0.065f, 0.93f);
            background.raycastTarget = false;
            captionGroup = groupObject.GetComponent<CanvasGroup>();
            captionGroup.alpha = 1f;

            captionText = CreateUiText("Caption Text", groupObject.transform, new Vector2(28f, 10f),
                new Vector2(-28f, -10f), 42, TextAnchor.MiddleCenter, White, FontStyle.Normal);
            captionText.text = "";
        }

        private void BuildStationPanelsAndButtons()
        {
            var statusCanvas = CreateWorldCanvas("Assembly Checklist Display", transform,
                new Vector3(2.95f, 1.47f, 1.78f), Quaternion.Euler(0f, 180f, 0f),
                new Vector2(900f, 560f), 0.0008f);
            statusText = CreateUiText("Assembly Checklist Text", statusCanvas.transform, Vector2.zero,
                Vector2.zero, 54, TextAnchor.MiddleLeft, White, FontStyle.Bold);
            statusText.rectTransform.anchorMin = new Vector2(0.06f, 0.05f);
            statusText.rectTransform.anchorMax = new Vector2(0.94f, 0.95f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;
            statusText.text = "OPPORTUNITY  /  ASSEMBLY\n\n0 of 3 components secured";

            CreateWorldButton("Reset Assembly Button", "RESET\nASSEMBLY", new Vector3(-1.38f, 1.25f, 0.48f),
                new Vector3(1.05f, 0.32f, 0.10f), new Color(0.22f, 0.30f, 0.38f), ResetAssembly, out resetButton);
            CreateWorldButton("Systems Check Button", "RUN\nSYSTEMS CHECK", new Vector3(0f, 1.25f, 0.48f),
                new Vector3(1.16f, 0.32f, 0.10f), Blue, RunSystemsCheckFromButton, out systemsCheckButton);
            CreateWorldButton("Ready for Launch Button", "READY FOR\nLAUNCH", new Vector3(1.40f, 1.25f, 0.48f),
                new Vector3(1.26f, 0.32f, 0.10f), Orange, RequestLaunchHandoffFromButton, out readyButton);

            CreateCube("Button Console Rail", transform, new Vector3(0f, 1.03f, 0.48f),
                new Vector3(3.15f, 0.08f, 0.20f), MakeMaterial(new Color(0.12f, 0.17f, 0.21f), 0.42f), false);
            CreateCube("Console Status Beacon", transform, new Vector3(2.96f, 2.04f, 1.78f),
                new Vector3(0.13f, 0.13f, 0.13f), MakeMaterial(Cyan), false);
        }

        private void CreateWorldButton(
            string objectName,
            string label,
            Vector3 position,
            Vector3 size,
            Color buttonColor,
            Action callback,
            out OpportunityFlashbackWorldButton button)
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = objectName;
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = position;
            buttonObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            buttonObject.transform.localScale = size;
            Renderer buttonRenderer = buttonObject.GetComponent<Renderer>();
            buttonRenderer.material = MakeMaterial(buttonColor, 0.22f);

            Canvas labelCanvas = CreateWorldCanvas(label + " Label", buttonObject.transform,
                new Vector3(0f, 0f, 0.54f), Quaternion.identity,
                new Vector2(size.x * 1000f, size.y * 1000f), 0.001f);
            Text labelText = CreateUiText(label + " Text", labelCanvas.transform, Vector2.zero,
                Vector2.zero, 72, TextAnchor.MiddleCenter, White, FontStyle.Bold);
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = new Vector2(10f, 4f);
            labelText.rectTransform.offsetMax = new Vector2(-10f, -4f);

            button = buttonObject.AddComponent<OpportunityFlashbackWorldButton>();
            button.Configure(label, callback, buttonColor, labelText);
        }

        private Canvas CreateWorldCanvas(
            string canvasName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector2 size,
            float scale)
        {
            var canvasObject = new GameObject(canvasName, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = localRotation;
            canvasObject.transform.localScale = Vector3.one * scale;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return canvas;
        }

        private Text AddTextPanel(
            string panelName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector2 size,
            float scale,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color textColor,
            Color backingColor)
        {
            Canvas canvas = CreateWorldCanvas(panelName, parent, localPosition, localRotation, size, scale);
            if (backingColor.a > 0f)
            {
                var backing = new GameObject(panelName + " Backing", typeof(RectTransform), typeof(Image));
                backing.transform.SetParent(canvas.transform, false);
                RectTransform backingRect = backing.GetComponent<RectTransform>();
                backingRect.anchorMin = Vector2.zero;
                backingRect.anchorMax = Vector2.one;
                backingRect.offsetMin = Vector2.zero;
                backingRect.offsetMax = Vector2.zero;
                backing.GetComponent<Image>().color = backingColor;
            }
            Text uiText = CreateUiText(panelName + " Text", canvas.transform, new Vector2(20f, 12f),
                new Vector2(-20f, -12f), fontSize, alignment, textColor, FontStyle.Bold);
            uiText.text = text;
            return uiText;
        }

        private void BuildAudioSources()
        {
            narrationSource = gameObject.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.volume = 1f;
            feedbackSource = gameObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
            feedbackSource.spatialBlend = 0f;
            feedbackSource.volume = 0.55f;
            if (snapFeedbackClip == null)
                generatedSnapTone = CreateSnapTone();
        }

        private void SetInitialState()
        {
            assemblyStarted = true;
            systemsCheckPassed = false;
            launchReady = false;
            if (resetButton != null)
                resetButton.SetInteractable(true);
            if (systemsCheckButton != null)
                systemsCheckButton.SetInteractable(false);
            if (readyButton != null)
                readyButton.SetInteractable(false);
            UpdateStatus();
        }

        private IEnumerator PlayFlashbackEntry()
        {
            ScreenFader fader = ScreenFader.Instance;
            if (fader != null)
            {
                fader.SetAlpha(1f);
                yield return fader.Fade(0f, 1.2f);
            }
            PresentNarration(IntroCaption, introNarration);
        }

        public void HandlePartSnapped(OpportunityAssemblyPart part)
        {
            if (part == null || !assemblyStarted)
                return;

            PlayFeedbackTone();
            switch (part.Kind)
            {
                case OpportunityPartKind.SolarPanel:
                    PresentNarration(SolarCaption, solarPanelNarration);
                    break;
                case OpportunityPartKind.CameraMast:
                    PresentNarration(MastCaption, cameraMastNarration);
                    break;
                case OpportunityPartKind.InstrumentArm:
                    PresentNarration(ArmCaption, instrumentArmNarration);
                    break;
            }

            if (systemsCheckButton != null && AssemblyProgress == assemblyParts.Count)
                systemsCheckButton.SetInteractable(true);
            UpdateStatus();
        }

        public bool RunSystemsCheck()
        {
            if (!AllPartsSnapped())
            {
                PresentNarration("Systems check is locked. Secure all three components first.", null);
                return false;
            }

            systemsCheckPassed = true;
            launchReady = true;
            if (readyButton != null)
                readyButton.SetInteractable(true);
            if (scaffoldVisualRoot != null)
                scaffoldVisualRoot.SetActive(false);
            foreach (Renderer renderer in componentRenderers)
                if (renderer != null)
                    renderer.enabled = false;
            if (finalModelInstance != null)
                finalModelInstance.SetActive(true);

            PlayFeedbackTone();
            PresentNarration(SystemsCaption, systemsCheckNarration);
            UpdateStatus();
            return true;
        }

        private void RunSystemsCheckFromButton()
        {
            RunSystemsCheck();
        }

        private void RequestLaunchHandoffFromButton()
        {
            RequestLaunchHandoff();
        }

        public void ResetAssembly()
        {
            if (scaffoldRoot != null)
                scaffoldRoot.SetActive(true);
            if (scaffoldVisualRoot != null)
                scaffoldVisualRoot.SetActive(true);
            if (finalModelInstance != null)
                finalModelInstance.SetActive(false);

            foreach (Renderer renderer in componentRenderers)
                if (renderer != null)
                    renderer.enabled = true;
            foreach (OpportunityAssemblyPart part in assemblyParts)
                if (part != null)
                    part.SetInteractable(false);
            foreach (OpportunityAssemblyPart part in assemblyParts)
                if (part != null)
                {
                    part.ResetPart();
                    part.SetInteractable(true);
                }

            assemblyStarted = true;
            systemsCheckPassed = false;
            launchReady = false;
            if (systemsCheckButton != null)
                systemsCheckButton.SetInteractable(false);
            if (readyButton != null)
                readyButton.SetInteractable(false);
            UpdateStatus();
            PresentNarration("Assembly reset. Refit the solar panel, camera mast, and instrument arm.", null);
        }

        /// <summary>Emits the launch handoff only after all parts and the final systems check pass.</summary>
        public bool RequestLaunchHandoff()
        {
            if (!launchReady || !systemsCheckPassed || !AllPartsSnapped())
            {
                PresentNarration("Ready for Launch stays locked until the systems check passes.", null);
                return false;
            }

            LaunchHandoffRequested?.Invoke(launchSceneName);
            onLaunchHandoffRequested?.Invoke();
            return true;
        }

        private bool AllPartsSnapped()
        {
            if (assemblyParts.Count != 3)
                return false;
            foreach (OpportunityAssemblyPart part in assemblyParts)
                if (part == null || !part.IsSnapped)
                    return false;
            return true;
        }

        private void UpdateStatus()
        {
            if (statusText == null)
                return;

            string check = systemsCheckPassed ? "PASS  ✓" : AssemblyProgress == 3 ? "READY TO RUN" : "LOCKED";
            string launch = launchReady ? "UNLOCKED" : "LOCKED";
            statusText.text = "OPPORTUNITY  /  ASSEMBLY\n\n"
                + "SECURED  " + AssemblyProgress + " / 3\n"
                + "SYSTEMS CHECK  " + check + "\n"
                + "READY FOR LAUNCH  " + launch;
        }

        private void PresentNarration(string caption, AudioClip clip)
        {
            if (captionText != null)
                captionText.text = caption;
            if (captionGroup != null)
                captionGroup.alpha = 1f;
            if (Application.isPlaying && narrationSource != null)
            {
                narrationSource.Stop();
                if (clip != null)
                {
                    narrationSource.clip = clip;
                    narrationSource.Play();
                }
            }
        }

        private void PlayFeedbackTone()
        {
            if (!Application.isPlaying || feedbackSource == null)
                return;
            AudioClip clip = snapFeedbackClip != null ? snapFeedbackClip : generatedSnapTone;
            if (clip != null)
                feedbackSource.PlayOneShot(clip);
        }

        private AudioClip CreateSnapTone()
        {
            const int sampleRate = 22050;
            const float duration = 0.20f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * (i / (float)sampleCount));
                float frequency = Mathf.Lerp(640f, 1120f, t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.18f;
            }
            AudioClip clip = AudioClip.Create("Opportunity Assembly Snap Chime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void AddPointLight(string lightName, Vector3 position, Color color, float range, float intensity)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private static Material MakeMaterial(Color color, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Diffuse");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = material;
            if (!keepCollider)
            {
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = false;
            }
            return cube;
        }

        private static Text CreateUiText(
            string name,
            Transform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            // Assign the project's supported built-in font before enabling Text. On this Unity
            // version an enabled, unconfigured UI Text briefly requests the removed Arial.ttf.
            textObject.SetActive(false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            textObject.SetActive(true);
            return text;
        }
    }
}
