using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RawHeightmapImporter : MonoBehaviour
{
    public Terrain terrain;
    public DefaultAsset rawFile;   // <-- drag your .raw file here
    public int resolution = 512;
    public float maxHeight = 0.2f;

    public void ApplyRandomized()
    {
#if UNITY_EDITOR
        if (terrain == null || rawFile == null)
        {
            Debug.LogError("Assign a Terrain and a RAW file first.");
            return;
        }

        // Get actual file path
        string path = AssetDatabase.GetAssetPath(rawFile);

        // Read raw bytes from the file
        byte[] data = File.ReadAllBytes(path);

        // Convert raw bytes → height array
        float[,] heights = LoadRaw(data, resolution);

        // Randomize height values
        Randomize(heights);

        // Apply to terrain
        ApplyToTerrain(terrain, heights);

        Debug.Log("Randomized heightmap applied.");
#endif
    }

    float[,] LoadRaw(byte[] data, int res)
    {
        float[,] heights = new float[res, res];
        int idx = 0;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // Read 16-bit little-endian value
                ushort h = (ushort)(data[idx] | (data[idx + 1] << 8));
                idx += 2;

                heights[y, x] = h / 65535f;
            }
        }

        return heights;
    }

    void Randomize(float[,] map)
    {
        int res = map.GetLength(0);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                map[y, x] = Random.value * maxHeight;
            }
        }
    }

    void ApplyToTerrain(Terrain t, float[,] heights)
    {
        TerrainData td = t.terrainData;

        td.heightmapResolution = heights.GetLength(0) + 1;
        td.SetHeights(0, 0, heights);
    }
}
