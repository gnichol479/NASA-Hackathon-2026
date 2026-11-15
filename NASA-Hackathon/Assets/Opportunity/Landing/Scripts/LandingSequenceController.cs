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
    XROrigin origin;
    GameObject canopy, rock, berries;
    RectTransform panel;
    TextMeshProUGUI story;
    Vector3 site;
    bool ready, discovered;
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
        rock.SetActive(true);
        if (Camera.main != null) UiFactory.FacePoint(panel, Camera.main.transform.position);
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
        panel.position = Surface(site + new Vector3(panelOffset.x, 0f, panelOffset.z))
            + Vector3.up * panelOffset.y;
        UiFactory.AddText(panel, "Title", "Opportunity: follow the water", 72f, FontStyles.Bold);
        story = UiFactory.AddText(panel, "Story", "I landed on Mars in 2004 to study rocks and soil for clues about past water.\n\nYour mission: approach the rock beside this panel. Point your controller ray at it and select to brush away the dust.\n\nDesktop simulator: approach the rock and press E.", 48f, FontStyles.Normal);
        var button = UiFactory.AddButton(panel, "Continue to Legacy", "Continue to Opportunity's legacy", out _);
        button.gameObject.SetActive(false);
        button.onClick.AddListener(() => { var flow = FindObjectOfType<LoadNextScene>(); if (flow != null) flow.Load(); });
        rock = Shape("Investigate Water Evidence Rock", PrimitiveType.Sphere, transform, Vector3.zero,
            new Vector3(1.1f, 0.65f, 0.85f), MakeMaterial(new Color(0.48f, 0.29f, 0.19f)), true);
        rock.transform.position = Surface(site + new Vector3(2.2f, 0f, -3f)) + Vector3.up * 0.25f;
        rock.AddComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Investigate());
        berries = new GameObject("Hematite Spherules (enlarged for visibility)");
        berries.transform.SetParent(rock.transform, false);
        var hematite = MakeMaterial(new Color(0.19f, 0.17f, 0.16f));
        for (int i = 0; i < 9; i++)
            Shape("Hematite " + i, PrimitiveType.Sphere, berries.transform,
                new Vector3((i % 3 - 1) * 0.15f, 0.43f, (i / 3 - 1) * 0.15f), Vector3.one * 0.075f, hematite);
        berries.SetActive(false);
        rock.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    public void Investigate()
    {
        if (!ready || discovered || Camera.main == null || Vector3.Distance(Camera.main.transform.position, rock.transform.position) > 3f) return;
        discovered = true;
        berries.SetActive(true);
        story.text = "Discovery complete! These enlarged dots represent hematite-rich 'blueberries': tiny mineral spheres. Opportunity's study of these spheres and surrounding rocks revealed evidence of ancient groundwater. This is evidence of water, not proof of life.\n\nOpportunity worked from 2004 to 2018, far beyond its planned 90-sol mission. Contact was lost during a huge dust storm; NASA ended the mission in 2019. Its discoveries still help us explore Mars.";
        panel.Find("Continue to Legacy").gameObject.SetActive(true);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame) Investigate();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E)) Investigate();
#endif
    }

    void OnDestroy()
    {
        foreach (var asset in assets) if (asset != null) Destroy(asset);
    }
}
