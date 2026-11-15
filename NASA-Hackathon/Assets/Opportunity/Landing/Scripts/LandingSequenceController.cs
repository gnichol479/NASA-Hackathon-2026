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
    [Header("Stationary information panel")]
    [Tooltip("Position relative to the landing site. X = sideways, Y = height above terrain, Z = forward/back.")]
    public Vector3 panelOffset = new Vector3(1.8f, 1.45f, -3f);
    [Header("Mars landscape")]
    public bool immersiveEnvironment = true;
    [Range(0, 200)] public int sceneryRockCount = 95;
    public Material legacyRockMaterial;
    public TerrainLayer legacySandLayer;
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
    AudioSource sampleAudio;
    AudioSource narrationAudio;
    AudioClip sampleRecordedClip;
    XROrigin origin;
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
    bool ready, discovered;
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
        foreach (var terrain in Terrain.activeTerrains)
            if (terrain.GetComponent<Collider>() != null && terrain.GetComponent<TeleportationArea>() == null)
                terrain.gameObject.AddComponent<TeleportationArea>();
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.84f, 0.70f, 0.56f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0015f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.1f;
        foreach (var light in FindObjectsOfType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            light.color = new Color(1f, 0.9f, 0.78f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            break;
        }
        DynamicGI.UpdateEnvironment();
        if (legacySandLayer != null)
            foreach (var terrain in Terrain.activeTerrains)
            {
                // Keep sculpted heights and painted weights; replace texture layers on a private copy.
                var data = Instantiate(terrain.terrainData);
                assets.Add(data);
                var layers = data.terrainLayers;
                for (int i = 0; i < layers.Length; i++) layers[i] = legacySandLayer;
                if (layers.Length == 0) layers = new[] { legacySandLayer };
                data.terrainLayers = layers;
                terrain.terrainData = data;
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null) collider.terrainData = data;
            }
        site = Surface(opportunityRover.transform.position);
        if (immersiveEnvironment) BuildEnvironment();
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
            MovePlayer(Surface(site - Vector3.forward * 4.5f));
            yield return null;
            yield return fader.Fade(0f, 0.25f);
        }
        panel.gameObject.SetActive(true);
        missionRoot.SetActive(true);
        foreach (var label in rockLabels)
            if (Camera.main != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
        if (Camera.main != null) UiFactory.FacePoint(panel, Camera.main.transform.position);
        ready = true;
        PlayNarration(landingIntroductionNarration);
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

    void BuildEnvironment()
    {
        var root = new GameObject("Mars Landscape (scenery)").transform;
        root.SetParent(transform, false);
        root.position = site;
        root.rotation = Quaternion.identity;

        // Low rolling terrain surrounds the authored walking area in every direction.
        // This is an illustrative landscape, not a reconstruction of NASA elevation data.
        const int segments = 128;
        const int rings = 18;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring < rings; ring++)
        {
            float radius = Mathf.Lerp(18f, 300f, ring / (float)(rings - 1));
            for (int j = 0; j <= segments; j++)
            {
                float angle = j * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float height = -0.5f;
                var ray = new Ray(site + new Vector3(x, 1000f, z), Vector3.down);
                foreach (var terrain in Terrain.activeTerrains)
                {
                    var collider = terrain.GetComponent<TerrainCollider>();
                    if (collider != null && collider.Raycast(ray, out var hit, 2000f))
                    { height = hit.point.y - site.y - 0.15f; break; }
                }
                float distant = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(35f, 100f, radius));
                float hills = (Mathf.PerlinNoise(x * 0.014f + 12f, z * 0.014f + 31f) - 0.35f) * 14f;
                vertices.Add(new Vector3(x, height + distant * Mathf.Max(0f, hills), z));
                if (ring == rings - 1 || j == segments) continue;
                int a = ring * (segments + 1) + j;
                int b = a + segments + 1;
                triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        }
        SceneryMesh("Distant Plains and Ridges", root, vertices, triangles,
            MakeMaterial(new Color(0.61f, 0.45f, 0.32f)));

        // Deterministic, combined low-poly stones: one renderer, no physics overhead.
        vertices.Clear();
        triangles.Clear();
        var random = new System.Random(240125);
        var points = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.forward, Vector3.right, Vector3.back };
        var faces = new[] { 0,2,3, 0,3,4, 0,4,5, 0,5,2, 1,3,2, 1,4,3, 1,5,4, 1,2,5 };
        for (int i = 0; i < sceneryRockCount * 4 && vertices.Count < sceneryRockCount * 6; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Lerp(4f, 37f, Mathf.Sqrt((float)random.NextDouble()));
            var offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            // Keep the landing, approach corridor, science rock and panel area clear.
            if (Mathf.Abs(offset.x) < 2f && offset.z > -16f && offset.z < 4f) continue;
            if (Vector2.Distance(new Vector2(offset.x, offset.z), new Vector2(2.2f, -3f)) < 2.2f) continue;
            if (Vector2.Distance(new Vector2(offset.x, offset.z), new Vector2(panelOffset.x, panelOffset.z)) < 1.5f) continue;
            bool found = false;
            var position = site + offset;
            foreach (var terrain in Terrain.activeTerrains)
            {
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null && collider.Raycast(new Ray(position + Vector3.up * 1000f, Vector3.down), out var hit, 2000f))
                { position = hit.point; found = true; break; }
            }
            if (!found) continue;
            float size = Mathf.Lerp(0.09f, 0.48f, (float)random.NextDouble());
            var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            int start = vertices.Count;
            foreach (var point in points)
                vertices.Add(position - site + Vector3.up * size * 0.16f
                    + rotation * Vector3.Scale(point, new Vector3(size, size * 0.45f, size * 0.8f)));
            foreach (int index in faces) triangles.Add(start + index);
        }
        SceneryMesh("Scattered Martian Stones", root, vertices, triangles,
            legacyRockMaterial != null ? legacyRockMaterial : MakeMaterial(new Color(0.55f, 0.43f, 0.32f)));
    }

    void SceneryMesh(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        var uv = new List<Vector2>();
        foreach (var vertex in vertices) uv.Add(new Vector2(vertex.x, vertex.z) * 0.5f);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        assets.Add(mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

    void BuildMission()
    {
        panel = UiFactory.CreatePanel("Landing Science Panel", transform, 1800f);
        panel.GetComponent<Canvas>().worldCamera = Camera.main;
        panel.position = Surface(site + new Vector3(panelOffset.x, 0f, panelOffset.z)) + Vector3.up * panelOffset.y;
        UiFactory.AddText(panel, "Title", "Did Mars once have liquid water?", 68f, FontStyles.Bold);
        story = UiFactory.AddText(panel, "Story", "", 46f, FontStyles.Normal);
        var water = UiFactory.AddButton(panel, "Water Conclusion", "Evidence of ancient liquid water", out _);
        answerWater = water.gameObject;
        water.onClick.AddListener(() => SubmitConclusion(true));
        var life = UiFactory.AddButton(panel, "Life Conclusion", "Proof that living organisms were found", out _);
        answerLife = life.gameObject;
        life.onClick.AddListener(() => SubmitConclusion(false));
        answerWater.SetActive(false);
        answerLife.SetActive(false);
        var next = UiFactory.AddButton(panel, "Continue to Legacy", "Continue to Opportunity's legacy", out _);
        next.gameObject.SetActive(false);
        next.onClick.AddListener(ContinueToLegacy);
        missionRoot = new GameObject("Past Water Investigation");
        missionRoot.transform.SetParent(transform, false);
        var offsets = new[] { new Vector3(2.2f,0f,-3f), new Vector3(-2.2f,0f,-2f), new Vector3(3.5f,0f,1f) };
        var stone = legacyRockMaterial != null ? legacyRockMaterial : MakeMaterial(new Color(0.48f,0.29f,0.19f));
        var mineral = MakeMaterial(new Color(0.19f,0.17f,0.16f));
        for (int i = 0; i < studyRocks.Length; i++)
        {
            int index = i;
            var target = Shape("Water Investigation Rock " + (i+1), PrimitiveType.Sphere, missionRoot.transform,
                Vector3.zero, new Vector3(1.1f,0.65f,0.85f), stone, true);
            target.transform.position = Surface(site + offsets[i]) + Vector3.up * 0.25f;
            studyRocks[i] = target;
            target.AddComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => ScanRock(index));
            var label = new GameObject("Sample " + (i+1) + " Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(missionRoot.transform, false);
            label.transform.position = target.transform.position + Vector3.up * 0.95f;
            label.text = "SAMPLE " + (i+1) + "\nSelect to investigate";
            label.fontSize = 2.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1.6f,0.5f);
            label.color = new Color(1f,0.8f,0.4f);
            rockLabels[i] = label;
            clues[i] = new GameObject("Sample " + (i+1) + " Results");
            clues[i].transform.SetParent(target.transform, false);
            if (i == 0)
                for (int j = 0; j < 9; j++)
                    Shape("Hematite (enlarged)", PrimitiveType.Sphere, clues[i].transform,
                        new Vector3((j%3-1)*0.15f,0.43f,(j/3-1)*0.15f), Vector3.one*0.075f, mineral);
            // Mineral identity comes from the simulated instrument readout, not a visual color guess.
            clues[i].SetActive(false);
        }
        RefreshMission();
        missionRoot.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    void RefreshMission(string feedback = "")
    {
        string notes = "";
        for (int i = 0; i < examined.Length; i++)
            if (examined[i] && sampleNarrationText != null && i < sampleNarrationText.Length)
                notes += "\n\nSample " + (i+1) + ": " + sampleNarrationText[i];
        string instruction = examinedCount < 3
            ? "Approach each numbered rock and select it with the controller ray. Desktop: approach and press E.\nSimplified instrument readings inspired by Opportunity's discoveries."
            : "What do these clues support? Choose below. Desktop: 1 = ancient water; 2 = life found.";
        story.text = "Samples investigated: " + examinedCount + "/3\n" + instruction + notes + feedback;
        answerWater.SetActive(examinedCount == 3 && !discovered);
        answerLife.SetActive(examinedCount == 3 && !discovered);
    }

    // Desktop accessibility: E investigates the closest sample, within the same range as XR.
    public void Investigate()
    {
        if (!ready || scanning || discovered || Camera.main == null) return;
        int closest = -1;
        float distance = 3f;
        for (int i = 0; i < studyRocks.Length; i++)
        {
            float d = Vector3.Distance(Camera.main.transform.position, studyRocks[i].transform.position);
            if (d < distance) { closest = i; distance = d; }
        }
        if (closest >= 0) ScanRock(closest);
        else RefreshMission("\n\nMove within 3 metres of a numbered sample to investigate it.");
    }

    void ScanRock(int index)
    {
        if (!ready || scanning || discovered || Camera.main == null) return;
        if (Vector3.Distance(Camera.main.transform.position, studyRocks[index].transform.position) > 3f)
        { RefreshMission("\n\nMove closer to Sample " + (index+1) + "."); return; }
        if (examined[index]) { RefreshMission("\n\nSample already recorded. Investigate the remaining samples."); return; }
        StartCoroutine(ReadSample(index));
    }

    IEnumerator ReadSample(int index)
    {
        scanning = true;
        float duration = 1.2f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            rockLabels[index].text = "SAMPLE " + (index+1) + "\nInvestigating " + Mathf.RoundToInt(t/duration*100f) + "%";
            yield return null;
        }
        examined[index] = true;
        examinedCount++;
        PlaySampleRecorded();
        clues[index].SetActive(true);
        rockLabels[index].text = "SAMPLE " + (index+1) + "\nRecorded";
        rockLabels[index].color = new Color(0.65f,1f,0.75f);
        scanning = false;
        RefreshMission();
        if (sampleNarrationClips != null && index < sampleNarrationClips.Length)
            PlayNarration(sampleNarrationClips[index]);
    }

    public void SubmitConclusion(bool ancientWater)
    {
        if (!ready || discovered || examinedCount != 3) return;
        if (!ancientWater)
        {
            RefreshMission("\n\n" + incorrectConclusionText);
            PlayNarration(incorrectConclusionNarration);
            return;
        }
        discovered = true;
        answerWater.SetActive(false);
        answerLife.SetActive(false);
        story.text = conclusionNarrationText + "\n\nContinue to its legacy. Desktop: Enter.";
        PlayNarration(conclusionNarration);
        panel.Find("Continue to Legacy").gameObject.SetActive(true);
    }

    void ContinueToLegacy()
    {
        if (!discovered) return;
        var flow = FindObjectOfType<LoadNextScene>();
        if (flow != null) flow.Load();
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
        if (clip == null || narrationAudio == null) return;
        NarrationPlayback.Play(narrationAudio, clip);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.eKey.wasPressedThisFrame) Investigate();
        if (keyboard.digit1Key.wasPressedThisFrame) SubmitConclusion(true);
        if (keyboard.digit2Key.wasPressedThisFrame) SubmitConclusion(false);
        if (keyboard.enterKey.wasPressedThisFrame) ContinueToLegacy();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E)) Investigate();
        if (Input.GetKeyDown(KeyCode.Alpha1)) SubmitConclusion(true);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SubmitConclusion(false);
        if (Input.GetKeyDown(KeyCode.Return)) ContinueToLegacy();
#endif
    }

    void OnDestroy()
    {
        foreach (var asset in assets) if (asset != null) Destroy(asset);
    }
}

