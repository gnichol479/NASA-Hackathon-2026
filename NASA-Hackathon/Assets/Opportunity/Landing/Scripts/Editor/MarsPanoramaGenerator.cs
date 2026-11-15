
using UnityEngine;
using UnityEditor;

public static class MarsPanoramaGenerator
{
    [MenuItem("NASA Hack/Create Mars Panorama")]
    public static void CreatePanorama()
    {
        string texturePath =
            "Assets/Opportunity/Landing/Textures/PIA05755.jpg";

        Texture2D panorama =
            AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        if (panorama == null)
        {
            Debug.LogError("NASA panorama not found.");
            return;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        Material material = new Material(shader);
        material.SetTexture("_BaseMap", panorama);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cull", 0);

        string materialPath =
            "Assets/Opportunity/Landing/Materials/NASA_Mars_Panorama.mat";

        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            EditorUtility.CopySerialized(material, existing);
            Object.DestroyImmediate(material);
            material = existing;
        }

        GameObject previous =
            GameObject.Find("NASA_Mars_Panorama");

        if (previous != null)
            Object.DestroyImmediate(previous);

        GameObject cylinder = new GameObject("NASA_Mars_Panorama");

        // Center the panorama near the landing area.
        cylinder.transform.position = new Vector3(3f, 0f, 3f);

        int segments = 128;
        float radius = 80f;
        float height = 35f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            int index = i * 2;

            vertices[index] = new Vector3(x, -5f, z);
            vertices[index + 1] = new Vector3(x, height, z);

            float u = (float)i / segments;

            uv[index] = new Vector2(u, 0f);
            uv[index + 1] = new Vector2(u, 1f);

            if (i < segments)
            {
                int t = i * 6;

                triangles[t] = index;
                triangles[t + 1] = index + 1;
                triangles[t + 2] = index + 2;

                triangles[t + 3] = index + 1;
                triangles[t + 4] = index + 3;
                triangles[t + 5] = index + 2;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Mars Panorama Mesh";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter filter = cylinder.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = cylinder.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        Selection.activeGameObject = cylinder;

        Debug.Log("NASA Mars panorama generated!");
    }
}
