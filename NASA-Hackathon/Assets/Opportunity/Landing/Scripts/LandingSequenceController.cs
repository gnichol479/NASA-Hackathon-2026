using System.Collections;
using System.Collections.Generic;
using NasaHack;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

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
    AudioSource sampleAudio;
    AudioClip sampleRecordedClip;
    XROrigin origin;
    GameObject canopy, missionRoot;
    readonly GameObject[] studyRocks = new GameObject[3];
    readonly GameObject[] clues = new GameObject[3];
    readonly TextMeshPro[] rockLabels = new TextMeshPro[3];
    readonly bool[] examined = new bool[3];
    readonly string[] evidence = {
        "Hematite-rich spheres: Opportunity found these mineral concretions, nicknamed blueberries. Their setting and chemistry support formation through ancient groundwater.",
        "Jarosite detected: Opportunity's mineral instrument identified this iron sulfate in rock. Jarosite forms in the presence of water, supporting a wet past.",
        "Red surface dust: color alone does not tell us whether liquid water was present. Scientists need mineral measurements and geological context."
    };
    bool scanning;
    int examinedCount;
    GameObject answerWater, answerLife;
    RectTransform panel;
    TextMeshProUGUI story;
    Vector3 site;
    bool ready, discovered, roverMode;
    [Header("Rover perspective")]
    [Min(0.5f)] public float roverEyeHeight = 1.65f;
    float roverPivotHeight;
    Quaternion roverBaseRotation;
    int activeTask;
    readonly RectTransform[] taskPanels = new RectTransform[3];
    readonly TextMeshProUGUI[] taskBodies = new TextMeshProUGUI[3];
    readonly GameObject[] taskButtons = new GameObject[3];
    readonly List<Object> assets = new List<Object>();

    IEnumerator Start()
    {
        if (lander == null || opportunityRover == null)
        {
            Debug.LogError("LandingAnimation needs Lander and Opportunity Rover references.", this);
            yield break;
        }
        origin = FindObjectOfType<XROrigin>();
        foreach (var terrain in Terrain.activeTerrains)
            if (terrain.GetComponent<Collider>() != null && terrain.GetComponent<TeleportationArea>() == null)
                terrain.gameObject.AddComponent<TeleportationArea>();
        // Landing deliberately shares Legacy's environment; do not recolor or regenerate it.
        opportunityRover.transform.position = new Vector3(0f, 0f, 3f);
        site = Surface(opportunityRover.transform.position);
        var renderers = opportunityRover.GetComponentsInChildren<Renderer>(true);
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
        roverBaseRotation = opportunityRover.transform.rotation;
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
    }

    Vector3 Surface(Vector3 point)
    {
        var ray = new Ray(point + Vector3.up * 10000f, Vector3.down);
        foreach (var terrain in Terrain.activeTerrains)
        {
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && collider.Raycast(ray, out var hit, 20000f)) return hit.point;
        }
        var ground = GameObject.Find("Ground (teleport)");
        var floor = ground != null ? ground.GetComponent<Collider>() : null;
        if (floor != null && floor.Raycast(ray, out var floorHit, 20000f)) return floorHit.point;
        Debug.LogWarning("No landing surface below " + point + "; preserving height.", this);
        return point;
    }

    void MovePlayer(Vector3 feet)
    {
        if (origin == null || origin.Camera == null) return;
        var enabledControllers = new List<CharacterController>();
        foreach (var c in origin.GetComponentsInChildren<CharacterController>())
            if (c.enabled) { enabledControllers.Add(c); c.enabled = false; }
        // Accounts for room-scale head offset without moving the tracked camera directly.
        origin.MoveCameraToWorldLocation(feet + origin.transform.up * Mathf.Max(0.1f, origin.CameraInOriginSpaceHeight));
        foreach (var c in enabledControllers) c.enabled = true;
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
            "You are Opportunity", site + new Vector3(1.8f,0f,2.5f), out story);
        story.text = "Landing complete. You now explore from Opportunity's mast-height viewpoint.\n\nUse your existing movement or teleport controls to investigate rocks. Complete three short tasks to discover whether Mars once had liquid water.\n\nSelect Begin mission. Desktop: Enter.";
        UiFactory.AddButton(panel, "Begin", "Begin mission", out _).onClick.AddListener(BeginMission);
        missionRoot = new GameObject("Opportunity Water Science Tasks");
        missionRoot.transform.SetParent(transform,false);
        var offsets = new[] { new Vector3(-2.6f,0f,4f), new Vector3(2.6f,0f,7f), new Vector3(-2f,0f,10f) };
        var titles = new[] { "Look closer at the rock", "Read the mineral evidence", "Build your conclusion" };
        var instructions = new[] {
            "TASK 1: MICROSCOPIC IMAGER\nApproach Sample 1. Select the rock to examine the small spheres embedded in it.\nDesktop: approach and press E.",
            "TASK 2: MINERAL ANALYSIS\nApproach Sample 2. Select it to run a simplified mineral reading inspired by Opportunity's spectrometer.\nDesktop: approach and press E.",
            "TASK 3: COMPARE THE EVIDENCE\nSelect Sample 3 to inspect its red dust, then decide what all three observations can tell us.\nDesktop: approach and press E."
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
            var action=UiFactory.AddButton(taskPanels[i],"Use instrument",i==0?"Focus microscopic imager":i==1?"Run mineral analysis":"Inspect surface dust",out _);
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

    void EnterRover()
    {
        if(origin==null || origin.Camera==null) return;
        foreach(var c in opportunityRover.GetComponentsInChildren<Collider>()) c.enabled=false;
        var enabledControllers = new List<CharacterController>();
        foreach(var c in origin.GetComponentsInChildren<CharacterController>())
            if(c.enabled) {enabledControllers.Add(c);c.enabled=false;}
        origin.MoveCameraToWorldLocation(site+Vector3.up*roverEyeHeight);
        // Restore locomotion colliders after the instantaneous handoff.
        foreach(var c in enabledControllers) c.enabled=true;
        roverMode=true;
    }

    void LateUpdate()
    {
        if(!roverMode || origin==null || origin.Camera==null) return;
        var camera=origin.Camera.transform;
        var position=Surface(camera.position);
        opportunityRover.transform.position=position+Vector3.up*roverPivotHeight;
        opportunityRover.transform.rotation=Quaternion.Euler(0f,origin.transform.eulerAngles.y,0f)*roverBaseRotation;
    }

    void BeginMission()
    {
        if(!ready) return;
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

    public void Investigate() { ScanRock(activeTask); }

    void ScanRock(int index)
    {
        if(!ready || scanning || discovered || panel.gameObject.activeSelf || index!=activeTask || examined[index] || Camera.main==null) return;
        if(Vector3.Distance(Camera.main.transform.position,studyRocks[index].transform.position)>3f)
        { rockLabels[index].text="SAMPLE "+(index+1)+"\nMove closer";return; }
        StartCoroutine(ReadSample(index));
    }

    IEnumerator ReadSample(int index)
    {
        scanning=true;
        for(float t=0f;t<1.5f;t+=Time.deltaTime)
        {
            rockLabels[index].text="SAMPLE "+(index+1)+"\nAnalyzing "+Mathf.RoundToInt(t/1.5f*100f)+"%";
            yield return null;
        }
        examined[index]=true;examinedCount++;
        taskPanels[index].Find("Use instrument").gameObject.SetActive(false);
        clues[index].SetActive(true);
        PlaySampleRecorded();
        rockLabels[index].text="SAMPLE "+(index+1)+"\nRecorded";
        rockLabels[index].color=new Color(0.65f,1f,0.75f);
        taskBodies[index].text=evidence[index]+(index==0?"\n\nThe spheres are enlarged for visibility. Real mineral identification required instrument measurements.":index==1?"\n\nThis is a prewritten educational reading, not a measurement of this model.":"\n\nCombine the mineral clues. What do they support? Desktop: 1 = water; 2 = life.");
        if(index<2) taskButtons[index].SetActive(true);
        else {answerWater.SetActive(true);answerLife.SetActive(true);}
        scanning=false;
    }

    public void SubmitConclusion(bool ancientWater)
    {
        if(!ready || discovered || examinedCount!=3) return;
        if(!ancientWater) {taskBodies[2].text="Try again: Opportunity found evidence of past water, not living organisms. Hematite concretions and jarosite are geological clues.\n\nChoose the conclusion supported by these measurements.";return;}
        discovered=true;answerWater.SetActive(false);answerLife.SetActive(false);
        taskBodies[2].text="MISSION COMPLETE\nMars had liquid water in its ancient past. Together, the minerals and their geological setting support this conclusion. Water alone does not prove life.\n\nOpportunity worked from 2004 to 2018, far beyond 90 sols. After losing contact during a dust storm, its mission ended in 2019. Continue to remember its legacy.";
        taskButtons[2].SetActive(true);
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

    void OnDestroy()
    {
        foreach (var asset in assets) if (asset != null) Destroy(asset);
    }
}


