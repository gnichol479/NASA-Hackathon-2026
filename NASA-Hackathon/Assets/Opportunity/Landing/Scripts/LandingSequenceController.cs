using System.Collections;
using System.Collections.Generic;
using NasaHack;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

// Demo additions are built only in Play mode; existing scene references stay intact.
public class LandingSequenceController : MonoBehaviour
{
    public GameObject lander;
    public GameObject opportunityRover;
    public float landingDuration = 4f;
    [Tooltip("Target horizontal model size in metres; matches the team's other Opportunity scenes.")]
    [Min(0.1f)] public float roverSizeMeters = 2.3f;
    [Header("Mars landscape")]


    public Material legacyRockMaterial;

    [Header("Sample feedback")]
    [Range(0f, 1f)] public float sampleSoundVolume = 0.18f;
    [Header("Narration")]
    [TextArea(3, 7)] public string landingIntroductionText = "Opportunity landed at Meridiani Planum in January 2004 to investigate geological evidence of past water. Investigate each of the three sample stations, then choose what the evidence supports. These simplified readouts are educational examples, not live measurements.";
    [TextArea(2, 5)] public string[] sampleNarrationText = {
        "Hematite-rich spheres: Opportunity found these mineral concretions, nicknamed blueberries. Their setting and chemistry support formation through ancient groundwater.",
        "Jarosite detected: Opportunity's mineral instrument identified this iron sulfate in rock. Jarosite forms in the presence of water, supporting a wet past.",
        "Red surface dust: color alone does not tell us whether liquid water was present. Scientists need mineral measurements and geological context."
    };
    [TextArea(2, 8)] public string incorrectConclusionText = "Try again: these rocks tell us about water and geology. Opportunity did not discover living organisms.";
    [TextArea(4, 12)] public string conclusionNarrationText = "Mission complete: evidence of ancient liquid water. Hematite concretions and jarosite, together with their geological setting, helped scientists reconstruct a wet past at Meridiani Planum. Red color alone would not establish this. These are simplified examples of real discoveries, not live mineral measurements. Hematite spheres are enlarged for visibility. Evidence of water is not proof of life. Opportunity operated from 2004 to 2018, far beyond its planned 90-sol mission. Contact was lost during a Mars-wide dust storm; NASA ended the mission in 2019.";
    public AudioClip landingIntroductionNarration;
    public AudioClip[] sampleNarrationClips = new AudioClip[3];
    public AudioClip incorrectConclusionNarration;
    public AudioClip conclusionNarration;
    AudioSource narrationAudio;
    AudioSource sampleAudio;
    AudioClip sampleRecordedClip;
    [Header("Detailed rover and deployment")]
    public GameObject detailedRoverPrefab;
    public AnimationClip deploymentClip;
    [Min(1f)] public float deploymentSeconds = 4f;
    [Header("Third-person rover controls")]
    [Min(1.5f)] public float thirdPersonDistance = 3.5f;
    [Min(1f)] public float thirdPersonHeight = 1.8f;
    [Min(0.1f)] public float roverDriveSpeed = 1.5f;
    [Min(5f)] public float roverSteeringDegreesPerSecond = 75f;
    [Min(0.5f)] public float sampleInteractionDistance = 2.4f;
    float deployedTime, compactTime;
    Transform armTip, toolSpinner, mast;
    Vector3 roverForwardLocal = Vector3.forward;
    readonly List<Transform> armJoints = new List<Transform>();
    readonly List<Transform> wheels = new List<Transform>();
    readonly Dictionary<Transform, Quaternion> restRotations = new Dictionary<Transform, Quaternion>();
    static readonly string[] HiddenModelParts = {
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
            "_root/body/suspension_right_middle_rocker/suspension_right_rear_rocker1/suspension_right_rear_rocker2/Cube.020/suspension_right_rear_axl/Cube",
        };
    XROrigin origin;
    DynamicMoveProvider moveProvider;
    bool moveProviderWasEnabled;
    readonly Dictionary<ActionBasedControllerManager, bool[]> controllerManagerStates = new Dictionary<ActionBasedControllerManager, bool[]>();
    readonly Dictionary<BaseTeleportationInteractable, bool> teleportableStates = new Dictionary<BaseTeleportationInteractable, bool>();
    readonly Dictionary<TeleportationProvider, bool> teleportProviderStates = new Dictionary<TeleportationProvider, bool>();
    GameObject canopy, missionRoot;
    readonly GameObject[] studyRocks = new GameObject[3];
    readonly GameObject[] clues = new GameObject[3];
    readonly TextMeshPro[] rockLabels = new TextMeshPro[3];
    readonly bool[] examined = new bool[3];
    bool scanning;
    int examinedCount;
    GameObject answerWater, answerLife;
    RectTransform panel;
    TextMeshProUGUI story;
    Vector3 site;
    bool ready, discovered, roverMode;
    Vector3 lastFollowRoverPosition;
    bool roverFollowInitialized;
    float roverPivotHeight;
    bool roverDriveEnabled;
    int activeTask;
    readonly RectTransform[] taskPanels = new RectTransform[3];
    readonly TextMeshProUGUI[] taskBodies = new TextMeshProUGUI[3];
    readonly GameObject[] taskButtons = new GameObject[3];
    readonly List<Object> assets = new List<Object>();

    void Awake()
    {
        narrationAudio = NarrationPlayback.CreateSource(this);
    }

    IEnumerator Start()
    {
        if (lander == null || opportunityRover == null)
        {
            Debug.LogError("LandingAnimation needs Lander and Opportunity Rover references.", this);
            yield break;
        }
        origin = FindObjectOfType<XROrigin>();
        ConfigureLandingCharacterController();
        PrepareDetailedRover();
        // Landing deliberately shares Legacy's environment; do not recolor or regenerate it.
        opportunityRover.transform.position = new Vector3(0f, 0f, 3f);
        site = Surface(opportunityRover.transform.position);
        var visibleRenderers = new List<Renderer>();
        foreach(var renderer in opportunityRover.GetComponentsInChildren<Renderer>(true))
            if(renderer.enabled && renderer.gameObject.activeInHierarchy) visibleRenderers.Add(renderer);
        var renderers = visibleRenderers.ToArray();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
            if (horizontalSize > 0.001f)
            {
                opportunityRover.transform.localScale *= Mathf.Max(0.1f, roverSizeMeters) / horizontalSize;
                bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            }
            opportunityRover.transform.position += Vector3.up * (site.y - bounds.min.y);
        }
        roverPivotHeight = opportunityRover.transform.position.y - site.y;
        opportunityRover.SetActive(false);
        var oldPanel = GameObject.Find("Oppertunity_InfoCanvas") ?? GameObject.Find("Opportunity_InfoCanvas");
        if (oldPanel != null) oldPanel.SetActive(false);
        BuildLander();
        BuildMission();
        if (origin != null)
        {
            origin.transform.root.localScale = Vector3.one;
            origin.transform.localScale = Vector3.one;
            MovePlayer(Surface(site - Vector3.forward * 12f));
        }
        float elapsed = 0f;
        while (elapsed < landingDuration)
        {
            canopy.SetActive(elapsed < landingDuration * 0.45f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        lander.SetActive(false);
        opportunityRover.SetActive(true);
        yield return DeployRover();
        var fader = ScreenFader.Instance;
        if (origin != null && fader != null)
        {
            yield return fader.Fade(1f, 0.25f);
            EnterRover();
            yield return null;
            yield return fader.Fade(0f, 0.25f);
        }
        if (!roverMode) EnterRover();
        panel.gameObject.SetActive(true);
        missionRoot.SetActive(true);
        foreach (var label in rockLabels)
            if (Camera.main != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
        if (Camera.main != null) UiFactory.FacePoint(panel, Camera.main.transform.position);
        foreach (var taskPanel in taskPanels)
            if (Camera.main != null) UiFactory.FacePoint(taskPanel, Camera.main.transform.position);
        ready = true;
        story.text = landingIntroductionText;
        PlayNarration(landingIntroductionNarration);
    }

    Vector3 Surface(Vector3 point)
    {
        var ray = new Ray(point + Vector3.up * 10000f, Vector3.down);
        bool found = false;
        Vector3 surface = point;
        foreach (var terrain in Terrain.activeTerrains)
        {
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && collider.Raycast(ray, out var hit, 20000f)
                && (!found || hit.point.y > surface.y))
            { surface = hit.point; found = true; }
        }
        if (found) return surface;
        var ground = GameObject.Find("Ground (teleport)");
        var floor = ground != null ? ground.GetComponent<Collider>() : null;
        if (floor != null && floor.Raycast(ray, out var floorHit, 20000f)) return floorHit.point;
        Debug.LogWarning("No landing surface below " + point + "; preserving height.", this);
        return point;
    }

    void MovePlayer(Vector3 feet)
    {
        if (origin == null || origin.Camera == null) return;
        MoveCameraSafely(feet + origin.transform.up * Mathf.Max(0.1f, origin.CameraInOriginSpaceHeight));
    }

    void MoveCameraSafely(Vector3 cameraPosition)
    {
        if(origin==null || origin.Camera==null)return;
        var providers=new List<LocomotionProvider>();
        foreach(var provider in origin.transform.root.GetComponentsInChildren<LocomotionProvider>(true))
            if(provider.enabled){providers.Add(provider);provider.enabled=false;}
        var enabledControllers = new List<CharacterController>();
        foreach (var c in origin.transform.root.GetComponentsInChildren<CharacterController>(true))
            if (c.enabled) { enabledControllers.Add(c); c.enabled = false; }
        try
        {
            // Moving the rig while its providers are active can make XRI issue a
            // CharacterController.Move against the temporarily disabled capsule.
            origin.MoveCameraToWorldLocation(cameraPosition);
        }
        finally
        {
            foreach (var c in enabledControllers) if(c!=null)c.enabled = true;
            foreach (var provider in providers) if(provider!=null)provider.enabled = true;
        }
    }

    void ConfigureLandingCharacterController()
    {
        if(origin==null)return;
        var driver=origin.transform.root.GetComponentInChildren<CharacterControllerDriver>(true);
        if(driver==null)return;
        // A zero tracked-head height can shrink the capsule to zero on the first
        // locomotion event; the Starter Assets rig's 0.5 m step offset then becomes
        // invalid. Keep a small, valid floor-tracking capsule during scene handoff.
        driver.minHeight=Mathf.Max(driver.minHeight,0.5f);
    }

    Material MakeMaterial(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        assets.Add(material);
        return material;
    }

    GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    void BuildLander()
    {
        // Clip coordinates are (0,Y,4), relative to an anchor on the real surface.
        var anchor = new GameObject("Landing Surface Anchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.position = site - Vector3.forward * 4f;
        anchor.transform.rotation = Quaternion.identity;
        lander.transform.SetParent(anchor.transform, false);
        lander.transform.localScale = Vector3.one;
        lander.transform.localPosition = new Vector3(0f, 25f, 4f);
        foreach (Transform child in lander.transform) child.gameObject.SetActive(false);
        var renderer = lander.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        var collider = lander.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        var white = MakeMaterial(new Color(0.9f, 0.87f, 0.78f));
        var orange = MakeMaterial(new Color(0.85f, 0.22f, 0.06f));
        var dark = MakeMaterial(new Color(0.23f, 0.2f, 0.17f));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f;
            Shape("Airbag " + i, PrimitiveType.Sphere, lander.transform,
                new Vector3(Mathf.Cos(a) * 0.55f, -0.2f, Mathf.Sin(a) * 0.55f), Vector3.one * 1.6f, white);
        }
        Shape("Landing Payload", PrimitiveType.Cube, lander.transform, Vector3.up * 0.6f, new Vector3(0.9f, 0.5f, 0.9f), orange);
        canopy = new GameObject("Descent Parachute and Lines");
        canopy.transform.SetParent(lander.transform, false);
        // Open dome, alternating colored gores, visible from below and above.
        for (int gore = 0; gore < 16; gore++)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring <= 8; ring++)
            {
                float latitude = ring * Mathf.PI / 16f;
                for (int edge = 0; edge < 2; edge++)
                {
                    float longitude = (gore + edge) * Mathf.PI / 8f;
                    vertices.Add(new Vector3(2.6f * Mathf.Sin(latitude) * Mathf.Cos(longitude),
                        4f + 1.3f * Mathf.Cos(latitude), 2.6f * Mathf.Sin(latitude) * Mathf.Sin(longitude)));
                }
                if (ring == 8) continue;
                int a = ring * 2;
                triangles.AddRange(new[] { a, a+2, a+1, a+1, a+2, a+3, a+1, a+2, a, a+3, a+2, a+1 });
            }
            var mesh = new Mesh { name = "Parachute Gore" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            assets.Add(mesh);
            var go = new GameObject("Canopy Gore " + gore, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(canopy.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = gore % 2 == 0 ? white : orange;
            if (gore % 2 != 0) continue;
            var line = new GameObject("Suspension Line " + gore).AddComponent<LineRenderer>();
            line.transform.SetParent(canopy.transform, false);
            line.useWorldSpace = false;
            line.sharedMaterial = dark;
            line.startWidth = line.endWidth = 0.025f;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.up * 0.8f);
            float angle = gore * Mathf.PI / 8f;
            line.SetPosition(1, new Vector3(2.6f * Mathf.Cos(angle), 4f, 2.6f * Mathf.Sin(angle)));
        }
    }

    RectTransform MissionPanel(string name, string step, string title, Vector3 position, out TextMeshProUGUI body)
    {
        var canvas = UiFactory.CreatePanel(name, transform, 1400f);
        canvas.GetComponent<Canvas>().worldCamera = Camera.main;
        canvas.position = Surface(position) + Vector3.up * 1.35f;
        var layout = canvas.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(55,55,40,40);
        layout.spacing = 24f;
        var caption = UiFactory.AddText(canvas, "Mission", step, 32f, FontStyles.Bold);
        caption.color = UiFactory.Accent;
        UiFactory.AddText(canvas, "Title", title, 64f, FontStyles.Bold);
        body = UiFactory.AddText(canvas, "Instructions", "", 43f, FontStyles.Normal);
        return canvas;
    }

    void BuildMission()
    {
        panel = MissionPanel("Rover Briefing Canvas", "OPPORTUNITY / SURFACE OPERATIONS",
            "Opportunity: surface control", site + new Vector3(1.8f,0f,2.5f), out story);
        story.text = landingIntroductionText;
        UiFactory.AddButton(panel, "Begin", "Begin mission", out _).onClick.AddListener(BeginMission);
        missionRoot = new GameObject("Opportunity Water Science Tasks");
        missionRoot.transform.SetParent(transform,false);
        var offsets = new[] { new Vector3(-2.6f,0f,4f), new Vector3(2.6f,0f,7f), new Vector3(-2f,0f,10f) };
        var titles = new[] { "Look closer at the rock", "Read the mineral evidence", "Build your conclusion" };
        var instructions = new[] {
            "TASK 1: MICROSCOPIC IMAGER\nLeft stick drives; right stick steers. Move within 2.4 m of Sample 1, then press Scan Sample with the trigger or select the rock with grip.\nDesktop: WASD / arrows drive; E scans.",
            "TASK 2: MINERAL ANALYSIS\nDrive to Sample 2 and press Scan Sample with the trigger or select the rock with grip. This simplified reading is inspired by Opportunity's spectrometer.\nDesktop: WASD / arrows drive; E scans.",
            "TASK 3: COMPARE THE EVIDENCE\nDrive to Sample 3 and scan its red dust. Press Scan Sample with the trigger or select the rock with grip, then choose the conclusion.\nDesktop: WASD / arrows drive; E scans."
        };
        var stone = legacyRockMaterial != null ? legacyRockMaterial : MakeMaterial(new Color(0.48f,0.29f,0.19f));
        var mineral = MakeMaterial(new Color(0.19f,0.17f,0.16f));
        for (int i=0;i<3;i++)
        {
            int index=i;
            var position=Surface(site+offsets[i]);
            var target=Shape("Science Sample "+(i+1),PrimitiveType.Sphere,missionRoot.transform,Vector3.zero,new Vector3(1.1f,0.65f,0.85f),stone,true);
            target.transform.position=position+Vector3.up*0.25f;
            studyRocks[i]=target;
            target.AddComponent<XRSimpleInteractable>().selectEntered.AddListener(_=>ScanRock(index));
            var label=new GameObject("Sample "+(i+1)+" Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(missionRoot.transform,false);
            label.transform.position=position+Vector3.up*0.95f;
            label.text="SAMPLE "+(i+1);
            label.fontSize=2.5f;
            label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(1.5f,0.4f);
            label.color=UiFactory.Accent;
            rockLabels[i]=label;
            clues[i]=new GameObject("Sample "+(i+1)+" Discovery");
            clues[i].transform.SetParent(target.transform,false);
            if(i==0) for(int j=0;j<9;j++) Shape("Enlarged hematite sphere",PrimitiveType.Sphere,clues[i].transform,
                new Vector3((j%3-1)*0.15f,0.43f,(j/3-1)*0.15f),Vector3.one*0.075f,mineral);
            clues[i].SetActive(false);
            taskPanels[i]=MissionPanel("Task "+(i+1)+" Canvas", "WATER INVESTIGATION / "+(i+1)+" OF 3",titles[i],position+new Vector3(1.4f,0f,0.4f),out taskBodies[i]);
            taskBodies[i].text=instructions[i];
            var action=UiFactory.AddButton(taskPanels[i],"Use instrument","Scan Sample",out _);
            action.onClick.AddListener(()=>ScanRock(index));
            var next=UiFactory.AddButton(taskPanels[i],"Next task",i==0?"Record spheres / next task":i==1?"Record mineral / next task":"Continue to legacy",out _);
            taskButtons[i]=next.gameObject;
            next.onClick.AddListener(()=>AdvanceTask(index));
            next.gameObject.SetActive(false);
            taskPanels[i].gameObject.SetActive(false);
        }
        answerWater=UiFactory.AddButton(taskPanels[2],"Water conclusion","Evidence of ancient liquid water",out _).gameObject;
        answerWater.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>SubmitConclusion(true));
        answerLife=UiFactory.AddButton(taskPanels[2],"Life conclusion","Proof that living organisms were found",out _).gameObject;
        answerLife.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>SubmitConclusion(false));
        answerWater.SetActive(false);answerLife.SetActive(false);
        missionRoot.SetActive(false);panel.gameObject.SetActive(false);
    }

    void PrepareDetailedRover()
    {
        if(detailedRoverPrefab==null) {Debug.LogWarning("Detailed rover prefab is not assigned; keeping the current rover.",this);return;}
        opportunityRover.SetActive(false);
        var model=Instantiate(detailedRoverPrefab,transform);
        model.name="Opportunity Detailed Rover";
        model.transform.rotation=Quaternion.Euler(0f,180f,0f);
        opportunityRover=model;
        foreach(var animator in model.GetComponentsInChildren<Animator>()) animator.enabled=false;
        foreach(var animation in model.GetComponentsInChildren<Animation>()) animation.enabled=false;
        foreach(var path in HiddenModelParts)
        {
            var part=model.transform.Find(path);
            if(part!=null && part.TryGetComponent<Renderer>(out var renderer)) renderer.enabled=false;
        }
        if(deploymentClip!=null)
        {
            SampleDeployment(0f);float start=Footprint();
            SampleDeployment(deploymentClip.length);float end=Footprint();
            deployedTime=end>=start?deploymentClip.length:0f;
            compactTime=end>=start?0f:deploymentClip.length;
            SampleDeployment(deployedTime);
        }
        foreach(var part in model.GetComponentsInChildren<Transform>())
        {
            string name=part.name.ToLowerInvariant();
            if(name.Contains("wheel") && part.GetComponent<Renderer>()!=null) wheels.Add(part);
            if(name=="mast") mast=part;
            if(name=="arm_base" || name=="arm_joint1" || name=="arm_joint2" || name=="arm_wrist") armJoints.Add(part);
            if(name=="arm_turret") armTip=part;
        }
        // Order joints along the hierarchy so CCD can solve from wrist back toward the base.
        armJoints.Sort((a,b)=>Depth(a).CompareTo(Depth(b)));
        if(armTip==null && armJoints.Count>0) armTip=armJoints[armJoints.Count-1];
        if(armJoints.Count>0)
        {
            // The imported model root is rotated for its scene placement. Derive chassis
            // forward from its front-mounted arm base so steering and scans use the model's
            // actual orientation instead of blindly composing root quaternions.
            var mountDirection=model.transform.InverseTransformPoint(armJoints[0].position);
            mountDirection=Vector3.ProjectOnPlane(mountDirection,Vector3.up);
            if(mountDirection.sqrMagnitude>0.0001f) roverForwardLocal=mountDirection.normalized;
        }
        if(armTip==null || armJoints.Count==0)
            Debug.LogWarning("Detailed rover arm joints were not found; check the imported model hierarchy.",this);
    }

    int Depth(Transform part) {int depth=0;while(part.parent!=null){depth++;part=part.parent;}return depth;}
    float Footprint()
    {
        var rs=opportunityRover.GetComponentsInChildren<Renderer>();
        var b=new Bounds(opportunityRover.transform.position,Vector3.zero);bool first=true;
        foreach(var r in rs) if(r.enabled){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
        return b.size.x*b.size.z;
    }
    void SampleDeployment(float time)
    {
        if(deploymentClip==null)return;
        var t=opportunityRover.transform;var p=t.position;var r=t.rotation;var scale=t.localScale;
        deploymentClip.SampleAnimation(opportunityRover,time);
        t.SetPositionAndRotation(p,r);t.localScale=scale;
    }
    IEnumerator DeployRover()
    {
        // The original imported unfold clip controls the actual solar-panel joints.
        if(deploymentClip!=null)
        {
            for(float t=0f;t<deploymentSeconds;t+=Time.deltaTime)
            {
                SampleDeployment(Mathf.Lerp(compactTime,deployedTime,Mathf.SmoothStep(0f,1f,t/deploymentSeconds)));
                yield return null;
            }
            SampleDeployment(deployedTime);
        }
        foreach(var part in armJoints)restRotations[part]=part.localRotation;
        if(mast!=null)restRotations[mast]=mast.localRotation;
        if(armTip!=null)
        {
            var tool=Shape("Rock Abrasion Tool (illustrative)",PrimitiveType.Cylinder,armTip,Vector3.zero,
                Vector3.one,MakeMaterial(new Color(0.25f,0.27f,0.29f)));
            // Set world-sized tool geometry despite the imported model's unit scale.
            tool.transform.localScale=new Vector3(0.12f,0.035f,0.12f)/Mathf.Max(0.001f,armTip.lossyScale.x);
            toolSpinner=tool.transform;
        }
    }

    void EnterRover()
    {
        if(roverMode)return;
        if(origin!=null && origin.Camera!=null)
        {
            foreach(var c in opportunityRover.GetComponentsInChildren<Collider>())c.enabled=false;
            var viewForward=Vector3.ProjectOnPlane(origin.Camera.transform.forward,Vector3.up).normalized;
            if(viewForward.sqrMagnitude<0.001f) viewForward=GetRoverForward();
            if(viewForward.sqrMagnitude<0.001f) viewForward=Vector3.forward;
            MoveCameraSafely(opportunityRover.transform.position-viewForward*thirdPersonDistance+Vector3.up*thirdPersonHeight);
        }
        ConfigureRoverControls();
        // Keep the player's XR rig at a stable offset as the rover drives. Translate
        // the origin only; never rotate it, so headset tracking remains world-stable.
        lastFollowRoverPosition = opportunityRover.transform.position;
        roverFollowInitialized = origin != null;
        roverMode=true;
    }

    void ConfigureRoverControls()
    {
        // Landing is a rover-driving activity. Remove teleport targets and teleport
        // actions here so a UI trigger press can never become a ground teleport.
        foreach(var teleportable in FindObjectsOfType<BaseTeleportationInteractable>(true))
        {
            if(teleportable.gameObject.scene!=gameObject.scene) continue;
            teleportableStates[teleportable]=teleportable.enabled;
            teleportable.enabled=false;
        }
        if(origin==null)return;

        var rig=origin.transform.root;
        moveProvider=rig.GetComponentInChildren<DynamicMoveProvider>(true);
        if(moveProvider!=null)
        {
            moveProviderWasEnabled=moveProvider.enabled;
            moveProvider.enabled=false;
        }
        foreach(var manager in rig.GetComponentsInChildren<ActionBasedControllerManager>(true))
        {
            controllerManagerStates[manager]=new[]{manager.smoothMotionEnabled,manager.smoothTurnEnabled};
            // Keeps the normal interaction ray active and exposes thumbsticks for driving.
            manager.smoothMotionEnabled=true;
            manager.smoothTurnEnabled=false;
        }
        foreach(var provider in rig.GetComponentsInChildren<TeleportationProvider>(true))
        {
            teleportProviderStates[provider]=provider.enabled;
            provider.enabled=false;
        }
    }

    void LateUpdate()
    {
        if (roverMode && origin != null && opportunityRover != null)
        {
            var roverPosition = opportunityRover.transform.position;
            if (roverFollowInitialized)
            {
                var roverDelta = roverPosition - lastFollowRoverPosition;
                // Ignore tiny animation/physics noise, but follow all real rover travel.
                if (roverDelta.sqrMagnitude > 0.000001f)
                    origin.transform.position += roverDelta;
            }
            lastFollowRoverPosition = roverPosition;
        }

        if(roverMode && !scanning && mast!=null && restRotations.ContainsKey(mast))
            mast.localRotation=restRotations[mast]*Quaternion.Euler(0f,Mathf.Sin(Time.time*0.35f)*8f,0f);
    }

    Vector3 GetRoverForward()
    {
        if(opportunityRover==null)return Vector3.forward;
        return Vector3.ProjectOnPlane(opportunityRover.transform.TransformDirection(roverForwardLocal),Vector3.up).normalized;
    }

    Quaternion RotationFacing(Vector3 heading)
    {
        var desired=Vector3.ProjectOnPlane(heading,Vector3.up).normalized;
        var current=GetRoverForward();
        if(desired.sqrMagnitude<0.001f || current.sqrMagnitude<0.001f) return opportunityRover.transform.rotation;
        return Quaternion.AngleAxis(Vector3.SignedAngle(current,desired,Vector3.up),Vector3.up)*opportunityRover.transform.rotation;
    }

    void UpdateRoverDrive()
    {
        if(!roverDriveEnabled || !roverMode || scanning || opportunityRover==null)return;
        Vector2 throttleInput=moveProvider!=null && moveProvider.leftHandMoveAction.action!=null
            ? moveProvider.leftHandMoveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector2 steeringInput=moveProvider!=null && moveProvider.rightHandMoveAction.action!=null
            ? moveProvider.rightHandMoveAction.action.ReadValue<Vector2>() : Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard!=null)
        {
            throttleInput.y+=(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1f:0f)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1f:0f);
            steeringInput.x+=(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1f:0f)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1f:0f);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        throttleInput.y+=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1f:0f)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1f:0f);
        steeringInput.x+=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1f:0f)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1f:0f);
#endif
        float throttle=Mathf.Abs(throttleInput.y)>0.18f?Mathf.Clamp(throttleInput.y,-1f,1f):0f;
        float steering=Mathf.Abs(steeringInput.x)>0.18f?Mathf.Clamp(steeringInput.x,-1f,1f):0f;
        if(Mathf.Abs(steering)>0f)
        {
            float reverseSign=throttle<-.1f?-1f:1f;
            opportunityRover.transform.rotation=Quaternion.AngleAxis(steering*reverseSign*roverSteeringDegreesPerSecond*Time.deltaTime,Vector3.up)*opportunityRover.transform.rotation;
        }
        if(Mathf.Abs(throttle)<0.001f)return;
        var before=opportunityRover.transform.position;
        var target=before+GetRoverForward()*(throttle*roverDriveSpeed*Time.deltaTime);
        target.y=Surface(target).y+roverPivotHeight;
        opportunityRover.transform.position=target;
        float travel=Vector3.ProjectOnPlane(target-before,Vector3.up).magnitude;
        foreach(var wheel in wheels)wheel.Rotate(Vector3.right,travel/0.13f*Mathf.Rad2Deg,Space.Self);
    }

    void SolveArm(Vector3 target)
    {
        if(armTip==null)return;
        for(int iteration=0;iteration<5;iteration++)
            for(int j=armJoints.Count-1;j>=0;j--)
            {
                var joint=armJoints[j];var toTip=armTip.position-joint.position;var toTarget=target-joint.position;
                if(toTip.sqrMagnitude<0.00001f || toTarget.sqrMagnitude<0.00001f)continue;
                joint.rotation=Quaternion.FromToRotation(toTip,toTarget)*joint.rotation;
            }
    }

    IEnumerator AnimateScience(int index)
    {
        var target=studyRocks[index];
        var initialRotation=opportunityRover.transform.rotation;
        var heading=target.transform.position-opportunityRover.transform.position;heading.y=0f;
        var facing=heading.sqrMagnitude>0.001f?RotationFacing(heading):initialRotation;
        const float alignSeconds=0.28f;
        for(float t=0f;t<alignSeconds;t+=Time.deltaTime)
        {
            opportunityRover.transform.rotation=Quaternion.Slerp(initialRotation,facing,Mathf.SmoothStep(0f,1f,t/alignSeconds));
            yield return null;
        }
        opportunityRover.transform.rotation=facing;
        var contact=target.GetComponent<Collider>().ClosestPoint(armTip!=null?armTip.position:opportunityRover.transform.position);
        var home=armTip!=null?armTip.position:opportunityRover.transform.position;
        var rotations=new Dictionary<Transform,Quaternion>();foreach(var j in armJoints)rotations[j]=j.localRotation;
        if(armTip!=null)
        {
            const float reachSeconds=0.62f;
            for(float t=0f;t<reachSeconds;t+=Time.deltaTime)
            {
                SolveArm(Vector3.Lerp(home,contact,Mathf.SmoothStep(0f,1f,t/reachSeconds)));
                rockLabels[index].text="SAMPLE "+(index+1)+"\nPositioning instrument";
                yield return null;
            }
            // Controlled contact rather than plunging the whole arm through the rock.
            const float readingSeconds=0.55f;
            for(float t=0f;t<readingSeconds;t+=Time.deltaTime)
            {
                SolveArm(contact);
                if(index==0 && toolSpinner!=null)toolSpinner.Rotate(Vector3.up,360f*Time.deltaTime,Space.Self);
                rockLabels[index].text="SAMPLE "+(index+1)+"\nReading "+Mathf.RoundToInt(t/readingSeconds*100f)+"%";
                yield return null;
            }
            var extended=new Dictionary<Transform,Quaternion>();foreach(var j in armJoints)extended[j]=j.localRotation;
            const float retractSeconds=0.42f;
            for(float t=0f;t<retractSeconds;t+=Time.deltaTime)
            {
                foreach(var j in armJoints)j.localRotation=Quaternion.Slerp(extended[j],rotations[j],Mathf.SmoothStep(0f,1f,t/retractSeconds));
                yield return null;
            }
            foreach(var j in armJoints)j.localRotation=rotations[j];
        }
    }

    void BeginMission()
    {
        if(!ready) return;
        roverDriveEnabled=true;
        panel.gameObject.SetActive(false);
        taskPanels[activeTask].gameObject.SetActive(true);
    }

    void AdvanceTask(int index)
    {
        if(!examined[index] || index!=activeTask || scanning) return;
        if(index==2) { ContinueToLegacy();return; }
        activeTask++;
        taskPanels[activeTask].gameObject.SetActive(true);
        taskButtons[index].SetActive(false);
        taskBodies[index].text += "\n\nRecorded. Continue to Sample "+(activeTask+1)+".";
    }

    void PositionConclusionPanelNearCamera()
    {
        var view = origin != null ? origin.Camera : Camera.main;
        if (view == null || taskPanels[2] == null) return;

        // The final answer panel is dense and interactive, so bring it into the
        // player's comfortable reading range when it first appears.
        const float readingDistance = 1.8f;
        taskPanels[2].position = view.transform.position + view.transform.forward * readingDistance;
        UiFactory.FacePoint(taskPanels[2], view.transform.position);
    }

    public void Investigate() { ScanRock(activeTask); }

    void ScanRock(int index)
    {
        if(!ready || scanning || discovered || panel.gameObject.activeSelf || index!=activeTask || examined[index]) return;
        if(Vector3.Distance(opportunityRover.transform.position,studyRocks[index].transform.position)>sampleInteractionDistance)
        { rockLabels[index].text="SAMPLE "+(index+1)+"\nDrive to marked station";return; }
        StartCoroutine(ReadSample(index));
    }

    IEnumerator ReadSample(int index)
    {
        scanning=true;
        yield return AnimateScience(index);
        const float resultSeconds=0.22f;
        for(float t=0f;t<resultSeconds;t+=Time.deltaTime)
        {
            rockLabels[index].text="SAMPLE "+(index+1)+"\nRecording "+Mathf.RoundToInt(t/resultSeconds*100f)+"%";
            yield return null;
        }
        examined[index]=true;examinedCount++;
        taskPanels[index].Find("Use instrument").gameObject.SetActive(false);
        clues[index].SetActive(true);
        PlaySampleRecorded();
        rockLabels[index].text="SAMPLE "+(index+1)+"\nRecorded";
        rockLabels[index].color=new Color(0.65f,1f,0.75f);
        taskBodies[index].text = sampleNarrationText[index];
        if(index<2) taskButtons[index].SetActive(true);
        else
        {
            answerWater.SetActive(true);
            answerLife.SetActive(true);
            PositionConclusionPanelNearCamera();
        }
        scanning=false;
        if (sampleNarrationClips != null && index < sampleNarrationClips.Length)
            PlayNarration(sampleNarrationClips[index]);
    }

    public void SubmitConclusion(bool ancientWater)
    {
        if(!ready || discovered || examinedCount!=3) return;
        if(!ancientWater)
        {
            taskBodies[2].text = incorrectConclusionText;
            PlayNarration(incorrectConclusionNarration);
            return;
        }
        discovered=true;answerWater.SetActive(false);answerLife.SetActive(false);
        taskBodies[2].text = conclusionNarrationText;
        taskButtons[2].SetActive(true);
        PlayNarration(conclusionNarration);
    }

    void ContinueToLegacy()
    {
        if(!discovered) return;
        var flow=FindObjectOfType<LoadNextScene>();if(flow!=null)flow.Load();
    }

    void HandleEnter()
    {
        if(!ready) return;
        if(panel.gameObject.activeSelf) BeginMission();
        else if(discovered) ContinueToLegacy();
        else if(examined[activeTask]) AdvanceTask(activeTask);
    }

    void Update()
    {
        UpdateRoverDrive();
#if ENABLE_INPUT_SYSTEM
        var keyboard=UnityEngine.InputSystem.Keyboard.current;if(keyboard==null)return;
        if(keyboard.eKey.wasPressedThisFrame)Investigate();
        if(keyboard.digit1Key.wasPressedThisFrame)SubmitConclusion(true);
        if(keyboard.digit2Key.wasPressedThisFrame)SubmitConclusion(false);
        if(keyboard.enterKey.wasPressedThisFrame)HandleEnter();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if(Input.GetKeyDown(KeyCode.E))Investigate();
        if(Input.GetKeyDown(KeyCode.Alpha1))SubmitConclusion(true);
        if(Input.GetKeyDown(KeyCode.Alpha2))SubmitConclusion(false);
        if(Input.GetKeyDown(KeyCode.Return))HandleEnter();
#endif
    }

    void PlaySampleRecorded()
    {
        if (sampleSoundVolume <= 0f) return;
        if (sampleAudio == null)
        {
            // Interface feedback, not a simulation of sound travelling through Mars' atmosphere.
            var audioObject = new GameObject("Sample Recorded Audio");
            audioObject.transform.SetParent(transform, false);
            sampleAudio = audioObject.AddComponent<AudioSource>();
            sampleAudio.playOnAwake = false;
            sampleAudio.spatialBlend = 0f;
            const int sampleRate = 44100;
            var samples = new float[(int)(sampleRate * 0.24f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                bool secondNote = time >= 0.12f;
                float localTime = secondNote ? time - 0.12f : time;
                float frequency = secondNote ? 880f : 660f;
                float envelope = Mathf.Clamp01(localTime / 0.008f)
                    * Mathf.Clamp01((0.12f - localTime) / 0.045f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * localTime) * envelope * 0.5f;
            }
            sampleRecordedClip = AudioClip.Create("Sample Recorded Chime", samples.Length, 1, sampleRate, false);
            sampleRecordedClip.SetData(samples, 0);
            assets.Add(sampleRecordedClip);
        }
        sampleAudio.PlayOneShot(sampleRecordedClip, sampleSoundVolume);
    }

    void PlayNarration(AudioClip clip)
    {
        NarrationPlayback.Play(narrationAudio, clip);
    }

    void OnDestroy()
    {
        foreach(var pair in teleportableStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        foreach(var pair in teleportProviderStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        foreach(var pair in controllerManagerStates)
            if(pair.Key!=null){pair.Key.smoothMotionEnabled=pair.Value[0];pair.Key.smoothTurnEnabled=pair.Value[1];}
        if(moveProvider!=null)moveProvider.enabled=moveProviderWasEnabled;
        foreach (var asset in assets) if (asset != null) Destroy(asset);
    }
}


