using System;
using System.Collections;
using System.Collections.Generic;
using NasaHack;
using LastSignal.InSightFlashback;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LastSignal.InSightMission
{
    public enum InSightChapter
    {
        Landing,
        Science,
        Legacy
    }

    [Serializable]
    public sealed class InSightVoiceCue
    {
        public string id;
        public string heading;
        [TextArea(2, 4)] public string caption;
        public AudioClip audioClip;
    }

    /// <summary>One scene controller for InSight's landing, science tour, and legacy chapters.</summary>
    [DisallowMultipleComponent]
    public sealed class InSightMissionSceneController : MonoBehaviour
    {
        // Keep this byte-for-byte aligned with the requested ElevenLabs Brian voice ID.
        // The previous casing typo made every generated clip request return voice_not_found.
        public const string BrianVoiceId = "nPczCjzI2devNBz1zQrb";

        [SerializeField] InSightChapter chapter;
        [SerializeField] string nextSceneName;
        [SerializeField] InSightVoiceCue[] voiceCues = Array.Empty<InSightVoiceCue>();
        [SerializeField] GameObject playerRig;
        [SerializeField] Camera playerCamera;
        [SerializeField] Transform landerInstance;
        [SerializeField] TMP_Text panelHeading;
        [SerializeField] TMP_Text panelBody;
        [SerializeField] Button[] actionButtons = new Button[4];
        [SerializeField] TMP_Text[] actionLabels = new TMP_Text[4];
        [SerializeField] TMP_Text controlHint;

        readonly HashSet<int> exploredInstrumentCues = new HashSet<int>();
        AudioSource narrationSource;
        LoadNextScene sceneFlow;
        Terrain opportunityTerrain;
        int landingBeat;
        bool legacyComplete;
        bool sceneBuilt;

        public InSightChapter Chapter => chapter;
        public IReadOnlyList<InSightVoiceCue> VoiceCues => voiceCues;

        /// <summary>Called by the InSight-only Editor builder to save a complete, visible scene.</summary>
        public void BuildSceneInEditor(InSightChapter sceneChapter, string nextScene,
            GameObject rigInstance, GameObject modelInstance, Material marsGround, Material marsSky,
            Material missionBlue, Material safetyOrange, TerrainData opportunityTerrainData,
            TerrainData opportunityFarTerrainData, InSightVoiceCue[] cues)
        {
            chapter = sceneChapter;
            nextSceneName = nextScene;
            voiceCues = cues ?? Array.Empty<InSightVoiceCue>();
            playerRig = rigInstance;
            sceneBuilt = true;

            if (playerRig != null)
            {
                playerRig.transform.SetParent(transform, false);
                playerRig.transform.localPosition = new Vector3(0f, 0f, -5.4f);
                playerRig.transform.localRotation = Quaternion.identity;
                XROrigin origin = playerRig.GetComponentInChildren<XROrigin>(true);
                if (origin != null)
                {
                    origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                    origin.CameraYOffset = 1.62f;
                }
            }

            BuildMarsEnvironment(marsGround, marsSky, missionBlue, safetyOrange,
                opportunityTerrainData, opportunityFarTerrainData);
            BuildLander(modelInstance);
            BuildMissionConsole();
            sceneFlow = GetComponent<LoadNextScene>();
            if (sceneFlow == null) sceneFlow = gameObject.AddComponent<LoadNextScene>();
            sceneFlow.nextScene = nextSceneName;
            sceneFlow.fadeSeconds = 0.65f;
        }

        void BuildMarsEnvironment(Material marsGround, Material marsSky, Material missionBlue,
            Material safetyOrange, TerrainData opportunityTerrainData, TerrainData opportunityFarTerrainData)
        {
            RenderSettings.skybox = marsSky;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.68f, 0.47f, 0.37f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0017f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.57f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.39f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.22f, 0.18f);
            RenderSettings.ambientIntensity = 1f;

            opportunityTerrain = EnsureOpportunityTerrain("Mars terrain — shared Opportunity landscape",
                opportunityTerrainData, new Vector3(-1618.9576f, -232.85645f, -1614.3766f), 12f, true);
            EnsureOpportunityTerrain("Mars far terrain — shared Opportunity horizon", opportunityFarTerrainData,
                new Vector3(-44570.848f, -965.2638f, -44875.08f), 25f, false);

            if (playerRig != null)
            {
                Vector3 rigPosition = playerRig.transform.position;
                rigPosition.y = SurfaceHeightAt(rigPosition.x, rigPosition.z);
                playerRig.transform.position = rigPosition;
            }

            GameObject lightObject = new GameObject("Elysium daylight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.83f, 0.70f);
            sun.shadows = LightShadows.Soft;

            // A few small stones frame the landing area; the site stays wide and uncluttered.
            Vector3[] stonePositions =
            {
                new Vector3(-7.8f, 0.12f, 1.2f), new Vector3(7.4f, 0.10f, 3.1f),
                new Vector3(-6.4f, 0.09f, 9.2f), new Vector3(6.9f, 0.13f, 10.8f)
            };
            Vector3[] stoneScales =
            {
                new Vector3(.48f,.22f,.34f), new Vector3(.35f,.19f,.31f),
                new Vector3(.31f,.18f,.27f), new Vector3(.52f,.24f,.38f)
            };
            for (int i = 0; i < stonePositions.Length; i++)
            {
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = "Small Elysium surface stone " + (i + 1);
                stone.transform.SetParent(transform, false);
                stone.transform.localPosition = stonePositions[i];
                stone.transform.localScale = stoneScales[i];
                stone.transform.localRotation = Quaternion.Euler(0f, i * 37f, i * 11f);
                Vector3 stonePosition = stone.transform.position;
                stonePosition.y = SurfaceHeightAt(stonePosition.x, stonePosition.z) + stoneScales[i].y * 0.5f;
                stone.transform.position = stonePosition;
                if (marsGround != null) stone.GetComponent<Renderer>().sharedMaterial = marsGround;
            }

            BuildSurveyMarkers(missionBlue, safetyOrange);
        }

        Terrain EnsureOpportunityTerrain(string objectName, TerrainData terrainData,
            Vector3 worldPosition, float heightmapPixelError, bool addCollider)
        {
            if (terrainData == null)
            {
                Debug.LogError("InSight mission scene is missing the shared Opportunity Mars TerrainData: " + objectName, this);
                return null;
            }

            Transform existing = transform.Find(objectName);
            Terrain terrain = existing != null ? existing.GetComponent<Terrain>() : null;
            if (terrain == null)
            {
                GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                terrainObject.name = objectName;
                terrainObject.transform.SetParent(transform, false);
                terrain = terrainObject.GetComponent<Terrain>();
            }

            terrain.name = objectName;
            terrain.terrainData = terrainData;
            terrain.transform.SetParent(transform, false);
            terrain.transform.localPosition = worldPosition;
            terrain.transform.localRotation = Quaternion.identity;
            terrain.transform.localScale = Vector3.one;
            terrain.heightmapPixelError = heightmapPixelError;
            terrain.drawInstanced = true;
            terrain.drawHeightmap = true;
            terrain.drawTreesAndFoliage = true;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (addCollider)
            {
                if (terrainCollider == null) terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
                terrainCollider.terrainData = terrainData;
                terrainCollider.enabled = true;
            }
            else if (terrainCollider != null)
            {
                DestroyImmediate(terrainCollider);
            }

            return terrain;
        }

        float SurfaceHeightAt(float x, float z)
        {
            if (opportunityTerrain == null || opportunityTerrain.terrainData == null) return 0f;
            Vector3 samplePoint = new Vector3(x, opportunityTerrain.transform.position.y, z);
            return opportunityTerrain.SampleHeight(samplePoint) + opportunityTerrain.transform.position.y;
        }

        /// <summary>Updates saved InSight mission scenes to use Opportunity's shared Mars terrain assets.</summary>
        public void ApplyOpportunityTerrainInEditor(TerrainData terrainData, TerrainData farTerrainData)
        {
            opportunityTerrain = EnsureOpportunityTerrain("Mars terrain — shared Opportunity landscape",
                terrainData, new Vector3(-1618.9576f, -232.85645f, -1614.3766f), 12f, true);
            EnsureOpportunityTerrain("Mars far terrain — shared Opportunity horizon", farTerrainData,
                new Vector3(-44570.848f, -965.2638f, -44875.08f), 25f, false);

            Transform oldPlane = transform.Find("Elysium Planitia — smooth illustrative surface");
            if (oldPlane != null) DestroyImmediate(oldPlane.gameObject);

            foreach (Transform child in transform)
            {
                Vector3 position = child.position;
                if (child.name.StartsWith("Small Elysium surface stone", StringComparison.Ordinal))
                {
                    position.y = SurfaceHeightAt(position.x, position.z) + child.lossyScale.y * 0.5f;
                    child.position = position;
                }
                else if (child.name.StartsWith("Instrument work-area marker", StringComparison.Ordinal))
                {
                    position.y = SurfaceHeightAt(position.x, position.z) + 0.012f;
                    child.position = position;
                }
            }

            if (playerRig != null)
            {
                Vector3 rigPosition = playerRig.transform.position;
                rigPosition.y = SurfaceHeightAt(rigPosition.x, rigPosition.z);
                playerRig.transform.position = rigPosition;
            }

            if (landerInstance != null)
            {
                Bounds bounds = InSightFacilityLayout.GetBounds(landerInstance.GetComponentsInChildren<Renderer>(true));
                if (bounds.size != Vector3.zero)
                {
                    Vector3 landerPosition = landerInstance.position;
                    landerPosition.y += SurfaceHeightAt(landerPosition.x, landerPosition.z) - bounds.min.y;
                    landerInstance.position = landerPosition;
                }
            }

            Transform console = transform.Find("InSight mission console");
            if (console != null)
            {
                Vector3 consolePosition = console.position;
                float x = playerRig != null ? playerRig.transform.position.x : 0f;
                float z = playerRig != null ? playerRig.transform.position.z : -5.4f;
                consolePosition.y = SurfaceHeightAt(x, z) + 1.64f;
                console.position = consolePosition;
            }
        }

        void BuildSurveyMarkers(Material missionBlue, Material safetyOrange)
        {
            Vector3[] positions =
            {
                new Vector3(-1.5f, 0.018f, 5.5f), new Vector3(1.35f, 0.018f, 6.2f),
                new Vector3(0.1f, 0.018f, 8.2f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Instrument work-area marker " + (i + 1);
                marker.transform.SetParent(transform, false);
                Vector3 markerPosition = positions[i];
                markerPosition.y = SurfaceHeightAt(markerPosition.x, markerPosition.z) + .012f;
                marker.transform.localPosition = markerPosition;
                marker.transform.localScale = new Vector3(.36f, .012f, .36f);
                Collider collider = marker.GetComponent<Collider>();
                if (collider != null) DestroyImmediate(collider);
                Material material = i == 1 ? safetyOrange : missionBlue;
                if (material != null) marker.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        void BuildLander(GameObject modelInstance)
        {
            if (modelInstance == null)
            {
                Debug.LogError("InSight scene builder could not create the lander model instance.", this);
                return;
            }

            landerInstance = modelInstance.transform;
            landerInstance.name = "InSight Lander — source model instance (unchanged)";
            landerInstance.SetParent(transform, false);
            landerInstance.localPosition = chapter == InSightChapter.Landing
                ? new Vector3(0f, 0f, 7.5f)
                : new Vector3(0f, 0f, 6.2f);
            InSightFacilityLayout.PrepareDetailedOpportunityModel(modelInstance, null, new Vector3(0f, 180f, 0f));
            landerInstance.localScale = Vector3.one;

            Bounds bounds = InSightFacilityLayout.GetBounds(modelInstance.GetComponentsInChildren<Renderer>(true));
            if (bounds.size != Vector3.zero)
            {
                Vector3 correction = new Vector3(-bounds.center.x, -bounds.min.y, 0f);
                landerInstance.position += correction;
                Vector3 landerPosition = landerInstance.position;
                landerPosition.y = SurfaceHeightAt(landerPosition.x, landerPosition.z);
                landerInstance.position = landerPosition;
            }
        }

        void BuildMissionConsole()
        {
            playerCamera = playerRig != null ? playerRig.GetComponentInChildren<Camera>(true) : null;
            if (playerCamera != null)
            {
                playerCamera.tag = "MainCamera";
                playerCamera.nearClipPlane = 0.02f;
                playerCamera.farClipPlane = 300f;
                panelBody = null;
                RectTransform panel = UiFactory.CreatePanel("InSight mission console", transform, 1500f);
                panel.position = new Vector3(0f, SurfaceHeightAt(0f, -5.4f) + 1.64f, -3.55f);
                UiFactory.FacePoint(panel, playerCamera.transform.position);
                Canvas mainCanvas = panel.GetComponent<Canvas>();
                mainCanvas.worldCamera = playerCamera;

                TMP_Text heading = UiFactory.AddText(panel, "Heading", "INSIGHT / MARS", 58f, FontStyles.Bold);
                heading.color = UiFactory.Accent;
                panelHeading = heading;
                panelBody = UiFactory.AddText(panel, "Caption", "", 46f, FontStyles.Normal);
                panelBody.color = UiFactory.TextColor;

                actionButtons = new Button[4];
                actionLabels = new TMP_Text[4];
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    actionButtons[i] = UiFactory.AddButton(panel, "Action " + (i + 1), "Continue", out TextMeshProUGUI label);
                    label.fontSize = 40f;
                    actionLabels[i] = label;
                    actionButtons[i].gameObject.SetActive(false);
                }

                TMP_Text accessibility = UiFactory.AddText(panel, "Controls", "Left stick: move   •   Right stick: snap-turn   •   Trigger: select\nDesktop: WASD / arrows, 1–4, Enter", 26f, FontStyles.Normal);
                accessibility.alignment = TextAlignmentOptions.Center;
                accessibility.color = new Color(.78f, .84f, .87f, 1f);
                controlHint = accessibility;
            }
            else
            {
                Debug.LogError("InSight mission scene is missing its shared XR Rig camera.", this);
            }
        }

        void Awake()
        {
            narrationSource = NarrationPlayback.CreateSource(this);
            narrationSource.enabled = true;
            narrationSource.volume = 1f;
            narrationSource.mute = false;
            narrationSource.spatialBlend = 0f;
            narrationSource.ignoreListenerPause = true;
            EnsureAudioListener();
            if (voiceCues != null)
                foreach (InSightVoiceCue cue in voiceCues)
                    if (cue != null && cue.audioClip != null && cue.audioClip.loadState == AudioDataLoadState.Unloaded)
                        cue.audioClip.LoadAudioData();
            ConfigurePlayerRig();
            InSightVREffectCleanup.ApplyToScene(gameObject.scene);
            BindButtons();
            if (!sceneBuilt)
                Debug.LogError("This InSight scene was not generated with Last Signal > InSight > Build Mission Flow.", this);
        }

        IEnumerator Start()
        {
            if (voiceCues == null || voiceCues.Length == 0) yield break;
            // Let the XR camera/listener and Quest audio device finish their first frame before
            // starting the opening narration; this avoids losing the clip during scene handoff.
            yield return null;
            switch (chapter)
            {
                case InSightChapter.Landing:
                    landingBeat = 0;
                    SetLandingBeat();
                    break;
                case InSightChapter.Science:
                    PlayCue(0);
                    SetButton(0, "SEIS — listen for marsquakes", true);
                    SetButton(1, "HP³ — measure heat flow", true);
                    SetButton(2, "RISE — track Mars's wobble", true);
                    SetButton(3, "Continue to the legacy", false);
                    break;
                case InSightChapter.Legacy:
                    PlayCue(0);
                    SetButton(0, "Dust reduced solar power", true);
                    SetButton(1, "A seismometer stopped working", true);
                    SetButton(2, "The mission ended on its planned first day", true);
                    SetButton(3, "Return to mission select", false);
                    break;
            }
        }

        void EnsureAudioListener()
        {
            if (playerCamera == null) return;
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                    return;
            playerCamera.gameObject.AddComponent<AudioListener>();
        }

        void ConfigurePlayerRig()
        {
            if (playerRig == null) return;
            XROrigin origin = playerRig.GetComponentInChildren<XROrigin>(true);
            if (origin != null)
            {
                if (playerCamera != null) origin.Camera = playerCamera;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                origin.CameraYOffset = 1.62f;
                CharacterControllerDriver driver = playerRig.GetComponentInChildren<CharacterControllerDriver>(true);
                if (driver != null) driver.minHeight = Mathf.Max(driver.minHeight, 0.5f);
            }

            // Remove the Starter Assets black tunnel mask. It is unnecessary in this
            // stationary instrument tour and obscures the ray-selected controls.
            foreach (Transform child in playerRig.GetComponentsInChildren<Transform>(true))
                if (child != playerRig.transform && child.name.IndexOf("TunnelingVignette", StringComparison.OrdinalIgnoreCase) >= 0)
                    child.gameObject.SetActive(false);
        }

        void BindButtons()
        {
            if (actionButtons == null) return;
            for (int i = 0; i < actionButtons.Length; i++)
            {
                int index = i;
                if (actionButtons[i] != null)
                    actionButtons[i].onClick.AddListener(() => OnAction(index));
            }
        }

        void OnAction(int index)
        {
            switch (chapter)
            {
                case InSightChapter.Landing:
                    if (landingBeat < 4)
                    {
                        landingBeat++;
                        SetLandingBeat();
                    }
                    else LoadNextChapter();
                    break;
                case InSightChapter.Science:
                    if (index < 3)
                    {
                        int cueIndex = index + 1;
                        exploredInstrumentCues.Add(cueIndex);
                        PlayCue(cueIndex);
                        SetButton(index, "✓ " + (index == 0 ? "SEIS reviewed" : index == 1 ? "HP³ reviewed" : "RISE reviewed"), true);
                        if (exploredInstrumentCues.Count == 3)
                        {
                            PlayCue(4);
                            SetButton(3, "Continue to the legacy", true);
                        }
                    }
                    else if (exploredInstrumentCues.Count == 3) LoadNextChapter();
                    break;
                case InSightChapter.Legacy:
                    if (index == 0)
                    {
                        legacyComplete = true;
                        PlayCue(2);
                        SetButton(0, "✓ Dust reduced solar power", true);
                        SetButton(1, "A seismometer stopped working", false);
                        SetButton(2, "The mission ended on its planned first day", false);
                        SetButton(3, "Return to mission select", true);
                    }
                    else if (index == 3 && legacyComplete)
                    {
                        LoadNextChapter();
                    }
                    else
                    {
                        PlayCue(1);
                    }
                    break;
            }
        }

        void SetLandingBeat()
        {
            PlayCue(landingBeat);
            string[] labels = { "Begin InSight's descent", "Continue: parachute", "Continue: powered descent", "Touchdown", "Explore the instruments" };
            SetButton(0, labels[Mathf.Clamp(landingBeat, 0, labels.Length - 1)], true);
            for (int i = 1; i < actionButtons.Length; i++) SetButton(i, string.Empty, false);
        }

        void SetButton(int index, string label, bool visible)
        {
            if (actionButtons == null || index < 0 || index >= actionButtons.Length || actionButtons[index] == null) return;
            actionButtons[index].gameObject.SetActive(visible);
            actionButtons[index].interactable = visible;
            if (actionLabels != null && index < actionLabels.Length && actionLabels[index] != null)
                actionLabels[index].text = label;
        }

        void PlayCue(int index)
        {
            if (voiceCues == null || index < 0 || index >= voiceCues.Length) return;
            InSightVoiceCue cue = voiceCues[index];
            if (panelHeading != null && !string.IsNullOrWhiteSpace(cue.heading)) panelHeading.text = cue.heading;
            if (panelBody != null) panelBody.text = cue.caption ?? string.Empty;
            if (narrationSource != null)
            {
                narrationSource.Stop();
                if (cue.audioClip != null)
                {
                    if (cue.audioClip.loadState == AudioDataLoadState.Unloaded)
                        cue.audioClip.LoadAudioData();
                    NarrationPlayback.Play(narrationSource, cue.audioClip);
                }
            }
        }

        void LoadNextChapter()
        {
            if (sceneFlow == null) sceneFlow = GetComponent<LoadNextScene>();
            if (sceneFlow != null)
            {
                sceneFlow.nextScene = nextSceneName;
                sceneFlow.Load();
                return;
            }

            if (!string.IsNullOrWhiteSpace(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
            else
                Debug.LogError("InSight next scene is not included in Build Settings: " + nextSceneName, this);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 move = Vector2.zero;
            move.y += keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            move.y -= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
            move.x += keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            move.x -= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            if (move.sqrMagnitude > 0.01f && playerRig != null)
            {
                Transform basis = playerCamera != null ? playerCamera.transform : playerRig.transform;
                Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
                playerRig.transform.position += (right * move.x + forward * move.y).normalized * (2.0f * Time.unscaledDeltaTime);
            }

            if (keyboard.enterKey.wasPressedThisFrame) OnAction(chapter == InSightChapter.Landing ? 0 : chapter == InSightChapter.Legacy && legacyComplete ? 3 : 0);
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) OnAction(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) OnAction(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) OnAction(2);
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) OnAction(3);
#endif
        }
    }
}
