
using UnityEngine;
using UnityEditor;

public class EagleCraterGenerator : EditorWindow
{
    [MenuItem("NASA Hack/Generate Eagle Crater")]
    public static void GenerateCrater()
    {
        Terrain terrain = FindObjectOfType<Terrain>();

        if (terrain == null)
        {
            Debug.LogError("No Terrain found in the scene.");
            return;
        }

        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Generate Eagle Crater");

        int resolution = data.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        float terrainWidth = data.size.x;
        float terrainLength = data.size.z;
        float terrainHeight = data.size.y;

        // Approximate Eagle Crater dimensions
        float craterRadius = 11f;
        float craterDepth = 2.5f;
        float rimHeight = 0.6f;

        Vector2 craterCenter = new Vector2(
            terrainWidth / 2f,
            terrainLength / 2f
        );

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * terrainWidth;
                float worldZ = (float)y / (resolution - 1) * terrainLength;

                float distance = Vector2.Distance(
                    new Vector2(worldX, worldZ),
                    craterCenter
                );

                // Base ground elevation
                float height = 5f;

                // Smooth crater depression
                float depression = craterDepth *
                    Mathf.Exp(-Mathf.Pow(distance / 8f, 4f));

                // Raised crater rim
                float rim = rimHeight *
                    Mathf.Exp(-Mathf.Pow((distance - craterRadius) / 2f, 2f));

                // Small surface variations
                float noise = Mathf.PerlinNoise(
                    worldX * 0.08f,
                    worldZ * 0.08f
                ) * 0.25f;

                height = height - depression + rim + noise;

                heights[y, x] = Mathf.Clamp01(height / terrainHeight);
            }
        }

        data.SetHeights(0, 0, heights);

        EditorUtility.SetDirty(data);

        Debug.Log("Eagle Crater terrain generated successfully!");
    }
}
