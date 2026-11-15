
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
            Debug.LogError("NASA panorama not found at: " + texturePath);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("URP Unlit shader not found.");
            return;
        }

        // Create a material that displays the NASA image.
        Material material = new Material(shader);
        material.name = "NASA_Mars_Panorama";
        material.SetTexture("_BaseMap", panorama);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cull", 0); // Render both sides.

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

        // Remove previously generated panorama.
        GameObject previous = GameObject.Find("NASA_Mars_Panorama");

        if (previous != null)
        {
            Object.DestroyImmediate(previous);
        }

        // Create the panoramic environment.
        GameObject cylinder =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        cylinder.name = "NASA_Mars_Panorama";

        // Place it around the landing area.
        cylinder.transform.position = new Vector3(50f, 15f, 50f);
        cylinder.transform.localScale = new Vector3(150f, 15f, 150f);

        Renderer renderer = cylinder.GetComponent<Renderer>();
        renderer.sharedMaterial = material;

        // The panorama is only visual, not a physical obstacle.
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());

        Selection.activeGameObject = cylinder;

        Debug.Log("NASA Mars panorama created successfully!");
    }
}
