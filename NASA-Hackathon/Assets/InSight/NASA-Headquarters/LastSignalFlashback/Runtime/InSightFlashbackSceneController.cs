using System;
using System.Collections;
using System.Collections.Generic;
using NasaHack;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.InSightFlashback
{
    [Serializable]
    public sealed class InSightLaunchHandoffEvent : UnityEvent { }

    /// <summary>
    /// Builds and controls the NASA/JPL-inspired flashback bay. The final rover model is optional
    /// and lives at a separate anchor; the simple scaffold and all interaction targets are temporary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InSightFlashbackSceneController : MonoBehaviour
    {
        public const string ElevenLabsBrianVoiceId = "nPczCjzI2devNBz1zQrb";

        private static readonly Color Navy = new Color(0.035f, 0.09f, 0.17f, 1f);
        private static readonly Color Blue = new Color(0.04f, 0.24f, 0.43f, 1f);
        private static readonly Color Cyan = new Color(0.12f, 0.72f, 0.84f, 1f);
        private static readonly Color Orange = new Color(0.96f, 0.38f, 0.12f, 1f);
        private static readonly Color White = new Color(0.92f, 0.95f, 0.97f, 1f);

        [Header("Shared project XR setup")]
        [SerializeField] private GameObject sharedXrRigPrefab;

        [Header("InSight lander model (field names kept from the Opportunity original)")]
        [Tooltip("Empty child anchor in this scene. Assign the imported Opportunity model prefab below.")]
        [SerializeField] private Transform opportunityModelAnchor;
        [Tooltip("Optional final model prefab. The interaction scaffold remains separate and can be replaced later.")]
        [SerializeField] private GameObject finalOpportunityModelPrefab;
        [Tooltip("Optional imported FBX pose clip. The widest-footprint endpoint is selected for deployed solar arrays.")]
        [SerializeField] private AnimationClip deployedRoverPose;
        [Tooltip("Scale for the assigned rover model. The shared detailed FBX is authored in meters and uses 1.")]
        [SerializeField] private float finalOpportunityModelScale = 1f;
        [Tooltip("Yaw correction for the assigned detailed Opportunity FBX so it faces the assembly area.")]
        [SerializeField] private Vector3 finalOpportunityModelEuler = new Vector3(0f, 180f, 0f);

        [Header("Narration clips — ElevenLabs Brian")]
        [SerializeField] private string narrationVoiceId = ElevenLabsBrianVoiceId;
        [TextArea(2, 4)] [SerializeField] private string introNarrationText =
            "InSight was built to listen to Mars, and to study its crust, mantle and core from the inside.";
        [TextArea(2, 4)] [SerializeField] private string solarPanelNarrationText =
            "Two round solar arrays unfold like paper fans. They powered InSight on the surface of Mars.";
        [TextArea(2, 4)] [SerializeField] private string cameraMastNarrationText =
            "SEIS, the seismometer, listened for marsquakes. On Mars, the robotic arm set it down on the ground.";
        [TextArea(2, 4)] [SerializeField] private string instrumentArmNarrationText =
            "The robotic arm lifted the seismometer and the heat probe off the deck and placed them on the Martian surface.";
        [TextArea(2, 4)] [SerializeField] private string systemsCheckNarrationText =
            "Power, the seismometer and the robotic arm are accounted for. InSight is ready for launch.";
        [SerializeField] private AudioClip introNarration;
        [SerializeField] private AudioClip solarPanelNarration;
        [SerializeField] private AudioClip cameraMastNarration;
        [SerializeField] private AudioClip instrumentArmNarration;
        [SerializeField] private AudioClip systemsCheckNarration;
        [SerializeField] private AudioClip snapFeedbackClip;

        [Header("Launch handoff")]
        [Tooltip("Target scene key sent to the handoff event and loaded after the fade.")]
        [SerializeField] private string launchSceneName = "InSight-Launch";
        [SerializeField] private InSightLaunchHandoffEvent onLaunchHandoffRequested =
            new InSightLaunchHandoffEvent();

        private readonly List<InSightAssemblyPart> assemblyParts = new List<InSightAssemblyPart>(3);
        private readonly List<Renderer> componentRenderers = new List<Renderer>();
        private readonly List<GameObject> hiddenModelComponents = new List<GameObject>(4);

        private GameObject rigInstance;
        private Camera playerCamera;
        private InSightFlashbackXRPlayerSetup playerSetup;
        private AudioSource narrationSource;
        private AudioSource feedbackSource;
        private AudioClip generatedSnapTone;
        private GameObject scaffoldRoot;
        private GameObject scaffoldVisualRoot;
        private GameObject finalModelInstance;
        private Transform[][] modelComponentSources;
        private Bounds[] modelComponentBounds;
        private TMP_Text statusText;
        private TMP_Text captionText;
        private InSightFacilityEnvironment facility;
        private float captionHideTime;
        private CanvasGroup captionGroup;
        private InSightFlashbackWorldButton resetButton;
        private InSightFlashbackWorldButton systemsCheckButton;
        private InSightFlashbackWorldButton readyButton;
        private bool hasBuilt;
        private bool assemblyStarted = true;
        private bool systemsCheckPassed;
        private bool launchReady;
        private bool launchTransitionStarted;

        public static event Action<string> LaunchHandoffRequested;

        public IReadOnlyList<InSightAssemblyPart> AssemblyParts => assemblyParts;
        public Transform OpportunityModelAnchor => opportunityModelAnchor;
        public Camera PlayerCamera => playerCamera;
        public int AssemblyProgress
        {
            get
            {
                int count = 0;
                foreach (InSightAssemblyPart part in assemblyParts)
                    if (part != null && part.IsSnapped)
                        count++;
                return count;
            }
        }
        public bool SystemsCheckPassed => systemsCheckPassed;
        public bool IsLaunchReady => launchReady;
        public InSightFlashbackWorldButton SystemsCheckButton => systemsCheckButton;
        public InSightFlashbackWorldButton ReadyButton => readyButton;
        public string NarrationVoiceId => narrationVoiceId;

        private void Awake()
        {
            BuildExperience(false);
        }

        private void Update()
        {
            if (captionGroup != null && Time.unscaledTime > captionHideTime)
                captionGroup.alpha = Mathf.MoveTowards(captionGroup.alpha, 0f, Time.unscaledDeltaTime * 2f);
        }

        private void Start()
        {
            if (Application.isPlaying)
                StartCoroutine(PlayFlashbackEntry());
        }

        /// <summary>Used by the Editor verification menu without starting a Unity play session.</summary>
        public void InitializeForEditorVerification(GameObject importedModelPrefab = null, AnimationClip importedModelPose = null)
        {
            if (!hasBuilt && importedModelPrefab != null)
                finalOpportunityModelPrefab = importedModelPrefab;
            if (!hasBuilt && importedModelPose != null)
                deployedRoverPose = importedModelPose;
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
            facility = GetComponentInChildren<InSightFacilityEnvironment>(true);
            if (facility == null)
                facility = InSightFacilityLayout.BuildEnvironment(transform, null);
            facility.HideAssemblyPreview();
            // Support scenes saved before the unified facility was introduced. Names containing
            // slashes cannot be looked up with Transform.Find, which interprets them as paths.
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("EDITOR PREVIEW")) child.gameObject.SetActive(false);
            }
            InSightFacilityLayout.ApplyLighting();
        }

        private void ResolveModelAnchor()
        {
            if (opportunityModelAnchor == null)
            {
                var anchorObject = new GameObject("INSIGHT MODEL ANCHOR — assign the final model prefab here");
                anchorObject.transform.SetParent(transform, false);
                anchorObject.transform.localPosition = InSightFacilityLayout.RoverOrigin;
                opportunityModelAnchor = anchorObject.transform;
            }

            if (finalOpportunityModelPrefab != null && finalModelInstance == null)
            {
                finalModelInstance = Instantiate(finalOpportunityModelPrefab, opportunityModelAnchor, false);
                finalModelInstance.name = "InSight Lander (assigned prefab)";
                InSightFacilityLayout.PrepareDetailedOpportunityModel(
                    finalModelInstance, deployedRoverPose, finalOpportunityModelEuler);
                InSightFacilityLayout.PlaceRoverOnFixture(
                    finalModelInstance.transform, opportunityModelAnchor, finalOpportunityModelScale);
            }
        }

        private void BuildAssemblyScaffold()
        {
            scaffoldRoot = InSightFacilityLayout.BuildScaffold(transform, facility.Palette, out scaffoldVisualRoot);
        }

        private void BuildAssemblyParts()
        {
            modelComponentSources = finalModelInstance != null
                ? InSightFacilityLayout.FindRoverComponentSources(finalModelInstance.transform)
                : null;
            bool useModelParts = modelComponentSources != null;
            modelComponentBounds = useModelParts ? new Bounds[3] : null;
            if (useModelParts)
                for (int i = 0; i < 3; i++)
                    useModelParts &= InSightFacilityLayout.TryGetComponentBounds(modelComponentSources[i], out modelComponentBounds[i]);
            if (!useModelParts && finalModelInstance != null)
            {
                Debug.LogWarning("[InSight Flashback] Using placeholder parts: " + (modelComponentSources == null
                    ? "the lander model has no panel_l_root / panel_r_root / SEIS / arm_01 groups."
                    : "a part group has no visible geometry.") + " Model children: " + DescribeChildren(finalModelInstance.transform, 2), this);
                finalModelInstance.SetActive(false);
            }

            Vector3[] fallbackSizes = { new Vector3(1.1f, .1f, .74f), new Vector3(.5f, .84f, .38f), new Vector3(1.1f, .38f, .3f) };
            for (int i = 0; i < 3; i++)
            {
                InSightPartKind kind = (InSightPartKind)i;
                Transform target = scaffoldRoot.transform.Find(InSightFacilityLayout.SocketNames[i]);
                Vector3 colliderSize = fallbackSizes[i];
                Vector3 spawnPosition = InSightFacilityLayout.PartSpawns[i];
                Quaternion spawnRotation = InSightFacilityLayout.PartSpawnRotation;
                if (useModelParts)
                {
                    modelComponentBounds[i].size += Vector3.one * .06f;
                    InSightFacilityLayout.PlaceSocketOnModelPart(target, target.Find("Mount guide"),
                        modelComponentBounds[i], finalModelInstance.transform.rotation);
                    colliderSize = modelComponentBounds[i].size;
                    spawnPosition = InSightFacilityLayout.GetComponentTraySpawn(i, modelComponentBounds[i]);
                    spawnRotation = finalModelInstance.transform.rotation;
                }
                InSightAssemblyPart part = CreatePart(kind + " - temporary grabbable component", kind, target,
                    spawnPosition, colliderSize,
                    kind == InSightPartKind.RoboticArm ? 1.35f : kind == InSightPartKind.Seismometer ? .8f : .7f,
                    spawnRotation);
                if (useModelParts)
                {
                    InSightFacilityLayout.BuildExtractedModelPart(finalModelInstance.transform,
                        modelComponentSources[i], modelComponentBounds[i], part.transform);
                    part.transform.localScale = Vector3.one * InSightFacilityLayout.TrayFit(modelComponentBounds[i]);
                    foreach (Transform source in modelComponentSources[i])
                    {
                        source.gameObject.SetActive(false);
                        hiddenModelComponents.Add(source.gameObject);
                    }
                }
                else InSightFacilityLayout.BuildPartVisuals(part.transform, kind, facility.Palette);
                // Collect only visible model geometry. Placeholder meshes intentionally disabled
                // on the assigned FBX must stay hidden when the tray copies are reset.
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null && renderer.enabled)
                        componentRenderers.Add(renderer);
                assemblyParts.Add(part);
            }
            if (useModelParts)
                scaffoldVisualRoot.SetActive(false);
        }

        private InSightAssemblyPart CreatePart(
            string partName,
            InSightPartKind kind,
            Transform target,
            Vector3 spawnPosition,
            Vector3 colliderSize,
            float snapRadius,
            Quaternion spawnRotation)
        {
            GameObject partObject = new GameObject(partName);
            partObject.name = partName;
            partObject.transform.SetParent(transform, true);
            partObject.transform.position = spawnPosition;
            partObject.transform.rotation = spawnRotation;
            partObject.transform.localScale = Vector3.one;
            BoxCollider box = partObject.AddComponent<BoxCollider>();
            box.size = colliderSize;
            if (kind == InSightPartKind.Seismometer) box.center = new Vector3(0f, .055f, 0f);
            if (kind == InSightPartKind.RoboticArm) box.center = new Vector3(.1f, -.05f, 0f);
            Rigidbody body = partObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            InSightAssemblyPart part = partObject.AddComponent<InSightAssemblyPart>();
            part.Configure(this, kind, target, snapRadius, spawnPosition, spawnRotation);
            return part;
        }

        private void BuildPlayerRig(bool verificationOnly)
        {
            if (!verificationOnly)
            {
                // Prefer the scene's saved instance of the shared XR rig. Keeping the origin in
                // the authored scene means scene entry always has its own tracked headset camera,
                // controller rays, direct interactors and locomotion setup.
                foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
                {
                    foreach (XROrigin candidate in sceneRoot.GetComponentsInChildren<XROrigin>(true))
                    {
                        if (candidate.Camera == null) continue;
                        playerCamera = candidate.Camera;
                        rigInstance = candidate.transform.root.gameObject;
                        break;
                    }
                    if (playerCamera != null) break;
                }
            }

            if (!verificationOnly && sharedXrRigPrefab != null)
            {
                if (rigInstance == null)
                {
                    rigInstance = Instantiate(sharedXrRigPrefab, InSightFacilityLayout.Arrival, Quaternion.identity);
                    rigInstance.name = "Shared XR Rig — tracked controllers, ray interaction and locomotion";
                    playerCamera = rigInstance.GetComponentInChildren<Camera>(true);
                }
            }

            if (playerCamera == null)
            {
                rigInstance = new GameObject("Flashback Editor Fallback Rig");
                rigInstance.transform.SetPositionAndRotation(InSightFacilityLayout.Arrival, Quaternion.identity);
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

                InSightFlashbackInputRig editorFallback = rigInstance.AddComponent<InSightFlashbackInputRig>();
                editorFallback.Configure(this, playerCamera, character);
            }

            if (verificationOnly && rigInstance != null)
                rigInstance.transform.SetParent(transform, true);

            if (playerCamera != null)
                playerCamera.tag = "MainCamera";

            // Keep this repair local to the flashback scene. The shared project prefab remains
            // untouched, while this scene gets floor-level tracking and unambiguous Quest controls.
            if (!verificationOnly && rigInstance != null && playerCamera != null)
            {
                playerSetup =
                    rigInstance.GetComponent<InSightFlashbackXRPlayerSetup>();
                if (playerSetup == null)
                    playerSetup = rigInstance.AddComponent<InSightFlashbackXRPlayerSetup>();
                playerSetup.Configure(rigInstance, playerCamera);
            }
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
            groupRect.anchorMin = new Vector2(0.15f, 0.28f);
            groupRect.anchorMax = new Vector2(0.85f, 0.45f);
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;
            Image background = groupObject.GetComponent<Image>();
            background.color = new Color(0.012f, 0.035f, 0.065f, 0.93f);
            background.raycastTarget = false;
            captionGroup = groupObject.GetComponent<CanvasGroup>();
            captionGroup.alpha = 0f;

            captionText = CreateUiText("Caption Text", groupObject.transform, new Vector2(28f, 10f),
                new Vector2(-28f, -10f), 42, TextAnchor.MiddleCenter, White, FontStyle.Normal);
            captionText.text = "";
        }

        private void BuildStationPanelsAndButtons()
        {
            statusText = InSightFacilityLayout.BuildStatusDisplay(transform);
            InSightFlashbackWorldButton[] buttons = new InSightFlashbackWorldButton[3];
            Action[] actions = { ResetAssembly, RunSystemsCheckFromButton, RequestLaunchHandoffFromButton };
            Color[] colors = { new Color(.25f,.31f,.35f), Blue, new Color(.17f,.40f,.34f) };
            for (int i = 0; i < buttons.Length; i++)
            {
                TMP_Text label;
                GameObject buttonObject = InSightFacilityLayout.BuildConsoleButton(transform, i, facility.Palette, out label);
                buttons[i] = buttonObject.AddComponent<InSightFlashbackWorldButton>();
                buttons[i].Configure(label.text, actions[i], colors[i], label);
            }
            resetButton = buttons[0];
            systemsCheckButton = buttons[1];
            readyButton = buttons[2];
        }

        private void BuildAudioSources()
        {
            EnsureAudioListener();
            narrationSource = gameObject.AddComponent<AudioSource>();
            narrationSource.playOnAwake = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.volume = 1f;
            narrationSource.ignoreListenerPause = true;
            feedbackSource = gameObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
            feedbackSource.spatialBlend = 0f;
            feedbackSource.volume = 0.55f;
            if (snapFeedbackClip == null)
                generatedSnapTone = CreateSnapTone();

            AudioClip[] narrationClips =
            {
                introNarration, solarPanelNarration, cameraMastNarration,
                instrumentArmNarration, systemsCheckNarration
            };
            foreach (AudioClip clip in narrationClips)
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
        }

        private void EnsureAudioListener()
        {
            if (playerCamera == null) return;
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                    return;
            playerCamera.gameObject.AddComponent<AudioListener>();
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
            if (fader != null) fader.SetAlpha(1f);
            float trackingWaitEnds = Time.realtimeSinceStartup + 8f;
            while (playerSetup != null && !playerSetup.TrackingReady && Time.realtimeSinceStartup < trackingWaitEnds)
                yield return null;
            if (playerSetup != null && !playerSetup.TrackingReady)
                Debug.LogWarning("[InSight Flashback] Head tracking did not become ready; starting the briefing audio anyway.", this);
            if (fader != null)
            {
                fader.SetAlpha(1f);
                yield return fader.Fade(0f, 1.2f);
            }
            PresentNarration(introNarrationText, introNarration);
        }

        public void HandlePartSnapped(InSightAssemblyPart part)
        {
            if (part == null || !assemblyStarted)
                return;

            // The tray copy is only a grab handle/display surrogate. Once it is fitted, turn the
            // corresponding original FBX group back on at its authored rover transform.
            SetOriginalModelComponentVisible(part, true);
            Transform guide = part.SnapTarget.Find("Mount guide");
            if (guide != null) guide.gameObject.SetActive(false);
            PlayFeedbackTone();
            switch (part.Kind)
            {
                case InSightPartKind.SolarArrays:
                    PresentNarration(solarPanelNarrationText, solarPanelNarration);
                    break;
                case InSightPartKind.Seismometer:
                    PresentNarration(cameraMastNarrationText, cameraMastNarration);
                    break;
                case InSightPartKind.RoboticArm:
                    PresentNarration(instrumentArmNarrationText, instrumentArmNarration);
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
            // Keep the original imported rover complete after the check. Each part was already
            // restored to its authored model transform as soon as it snapped into place.
            foreach (GameObject hiddenPart in hiddenModelComponents)
                if (hiddenPart != null) hiddenPart.SetActive(true);

            // Keep the completed educational rover visible when no imported model has been assigned.
            if (finalModelInstance != null)
            {
                if (scaffoldVisualRoot != null) scaffoldVisualRoot.SetActive(false);
                foreach (Renderer renderer in componentRenderers)
                    if (renderer != null) renderer.enabled = false;
                finalModelInstance.SetActive(true);
            }

            PlayFeedbackTone();
            PresentNarration(systemsCheckNarrationText, systemsCheckNarration);
            UpdateStatus();
            return true;
        }

        private void SetOriginalModelComponentVisible(InSightAssemblyPart part, bool visible)
        {
            if (part == null || finalModelInstance == null || modelComponentSources == null)
                return;

            int index = (int)part.Kind;
            if (index < 0 || index >= modelComponentSources.Length || modelComponentSources[index] == null)
                return;

            foreach (Transform source in modelComponentSources[index])
                if (source != null) source.gameObject.SetActive(visible);

            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) renderer.enabled = !visible;
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
                finalModelInstance.SetActive(true);

            foreach (GameObject hiddenPart in hiddenModelComponents)
                if (hiddenPart != null) hiddenPart.SetActive(false);

            foreach (Renderer renderer in componentRenderers)
                if (renderer != null)
                    renderer.enabled = true;
            foreach (InSightAssemblyPart part in assemblyParts)
                if (part != null)
                    part.SetInteractable(false);
            foreach (InSightAssemblyPart part in assemblyParts)
                if (part != null)
                {
                    part.ResetPart();
                    part.SetInteractable(true);
                    Transform guide = part.SnapTarget.Find("Mount guide");
                    if (guide != null) guide.gameObject.SetActive(true);
                }

            assemblyStarted = true;
            systemsCheckPassed = false;
            launchReady = false;
            launchTransitionStarted = false;
            if (systemsCheckButton != null)
                systemsCheckButton.SetInteractable(false);
            if (readyButton != null)
                readyButton.SetInteractable(false);
            UpdateStatus();
            PresentNarration("Assembly reset. Refit the solar arrays, seismometer, and robotic arm.", null);
        }

        /// <summary>Emits the launch handoff and transitions after all parts and systems checks pass.</summary>
        public bool RequestLaunchHandoff()
        {
            if (!launchReady || !systemsCheckPassed || !AllPartsSnapped() || launchTransitionStarted)
            {
                PresentNarration("Ready for Launch stays locked until the systems check passes.", null);
                return false;
            }

            if (Application.isPlaying && !Application.CanStreamedLevelBeLoaded(launchSceneName))
            {
                PresentNarration("The launch scene is not included in this build.", null);
                Debug.LogError("[InSight Flashback] Cannot load launch scene '" + launchSceneName + "'. Add it to Build Settings.", this);
                return false;
            }

            if (Application.isPlaying)
            {
                launchTransitionStarted = true;
                if (readyButton != null) readyButton.SetInteractable(false);
            }

            LaunchHandoffRequested?.Invoke(launchSceneName);
            onLaunchHandoffRequested?.Invoke();
            if (Application.isPlaying)
                StartCoroutine(FadeAndLoadLaunchScene());
            return true;
        }

        private IEnumerator FadeAndLoadLaunchScene()
        {
            ScreenFader fader = ScreenFader.Instance;
            if (fader != null)
                yield return fader.Fade(1f, 1.1f);
            SceneManager.LoadScene(launchSceneName, LoadSceneMode.Single);
        }

        private static string DescribeChildren(Transform t, int depth)
        {
            var sb = new System.Text.StringBuilder(t.name);
            if (depth > 0 && t.childCount > 0)
            {
                sb.Append(" [");
                for (int i = 0; i < t.childCount; i++) sb.Append(i > 0 ? ", " : "").Append(DescribeChildren(t.GetChild(i), depth - 1));
                sb.Append("]");
            }
            return sb.ToString();
        }

        private bool AllPartsSnapped()
        {
            if (assemblyParts.Count != 3)
                return false;
            foreach (InSightAssemblyPart part in assemblyParts)
                if (part == null || !part.IsSnapped)
                    return false;
            return true;
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            string instruction = systemsCheckPassed
                ? "CHECK PASSED\nReady for Launch is unlocked."
                : AssemblyProgress == 3
                    ? "ASSEMBLY COMPLETE\nRun Systems Check below."
                    : "Fit parts to matching numbered mounts.\nSystems check awaiting assembly.";
            statusText.text = "COMPONENTS   " + AssemblyProgress + " / 3\n\n" + instruction;
        }

        private void PresentNarration(string caption, AudioClip clip)
        {
            captionHideTime = Time.unscaledTime + Mathf.Max(8f, clip != null ? clip.length + .5f : 0f);
            if (captionText != null)
                captionText.text = caption;
            if (captionGroup != null)
                captionGroup.alpha = 1f;
            if (Application.isPlaying && narrationSource != null)
            {
                narrationSource.Stop();
                if (clip != null)
                {
                    if (clip.loadState == AudioDataLoadState.Unloaded)
                        clip.LoadAudioData();
                    narrationSource.enabled = true;
                    narrationSource.volume = 1f;
                    narrationSource.mute = false;
                    narrationSource.spatialBlend = 0f;
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
            AudioClip clip = AudioClip.Create("InSight Assembly Snap Chime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static TMP_Text CreateUiText(
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
            textObject.SetActive(false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            textObject.SetActive(true);
            return text;
        }
    }
}
