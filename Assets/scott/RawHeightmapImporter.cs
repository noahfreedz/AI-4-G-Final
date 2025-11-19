using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RawHeightmapImporter : MonoBehaviour
{
    [System.Serializable]
    public class NoiseLayer
    {
        public DefaultAsset rawFile;
    }

    public Terrain terrain;
    public DefaultAsset rawFile;
    public List<NoiseLayer> noiseLayers = new List<NoiseLayer>();
    public int rawResolution = 512;
    public float blendStrength = 0.3f;
    public float smoothAmount = 0.8f;

    public void ApplySubtleVariations()
    {
#if UNITY_EDITOR
        if (terrain == null || rawFile == null)
        {
            Debug.LogError("Assign a Terrain and a RAW file first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int terrainResolution = terrainData.heightmapResolution;

        // Get actual file path
        string path = AssetDatabase.GetAssetPath(rawFile);

        // Read raw bytes from the file
        byte[] data = File.ReadAllBytes(path);

        // Convert raw bytes → height array
        float[,] rawHeights = LoadRaw(data, rawResolution);

        // Get current terrain heights
        float[,] currentHeights = terrainData.GetHeights(0, 0, terrainResolution, terrainResolution);

        // Blend RAW data into terrain (creates subtle variations)
        float[,] blendedHeights = BlendHeightmaps(currentHeights, rawHeights, terrainResolution, rawResolution);

        // Apply to terrain
        terrainData.SetHeights(0, 0, blendedHeights);

        Debug.Log("Subtle heightmap variations applied to full terrain.");
#endif
    }

    public void BlendPerlinNoiseLayers()
    {
#if UNITY_EDITOR
        if (terrain == null || noiseLayers.Count == 0)
        {
            Debug.LogError("Assign a Terrain and at least one RAW noise file first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int terrainResolution = terrainData.heightmapResolution;

        // Initialize blended heightmap
        float[,] blendedHeights = new float[terrainResolution, terrainResolution];

        // Load and blend all noise layers with equal weight
        int validLayers = 0;
        foreach (var layer in noiseLayers)
        {
            if (layer.rawFile == null) continue;

            string path = AssetDatabase.GetAssetPath(layer.rawFile);
            byte[] data = File.ReadAllBytes(path);
            float[,] rawHeights = LoadRaw(data, rawResolution);

            // Blend this layer into the result with equal weight
            BlendLayer(blendedHeights, rawHeights, terrainResolution, 1f);
            validLayers++;
        }

        // Normalize by number of layers (equal blending)
        if (validLayers > 0)
            NormalizeHeightmapByCount(blendedHeights, validLayers);

        // Apply smoothing for natural mountains
        for (int i = 0; i < Mathf.RoundToInt(smoothAmount * 5f); i++)
        {
            SmoothHeightmap(blendedHeights);
        }

        // Apply to terrain
        terrainData.SetHeights(0, 0, blendedHeights);

        Debug.Log("Perlin noise layers blended smoothly!");
#endif
    }

    public void ApplyRandomized()
    {
#if UNITY_EDITOR
        if (terrain == null)
        {
            Debug.LogError("Assign a Terrain first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int terrainResolution = terrainData.heightmapResolution;

        // Get current terrain heights
        float[,] currentHeights = terrainData.GetHeights(0, 0, terrainResolution, terrainResolution);

        // Apply random variations to entire terrain
        Randomize(currentHeights, blendStrength);

        // Apply to terrain
        terrainData.SetHeights(0, 0, currentHeights);

        Debug.Log("Randomized heightmap variations applied to full terrain.");
#endif
    }

    void BlendLayer(float[,] result, float[,] layerHeights, int terrainRes, float weight)
    {
        for (int y = 0; y < terrainRes; y++)
        {
            for (int x = 0; x < terrainRes; x++)
            {
                // Map terrain coordinates to noise coordinates
                float noiseX = (x / (float)terrainRes) * layerHeights.GetLength(1);
                float noiseY = (y / (float)terrainRes) * layerHeights.GetLength(0);

                float sampledNoise = SampleBilinear(layerHeights, noiseX, noiseY);
                result[y, x] += sampledNoise * weight;
            }
        }
    }

    void NormalizeHeightmapByCount(float[,] map, int count)
    {
        int res = map.GetLength(0);
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                map[y, x] = map[y, x] / count;
            }
        }
    }

    void SmoothHeightmap(float[,] map)
    {
        int res = map.GetLength(0);
        float[,] temp = new float[res, res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float sum = 0f;
                int count = 0;

                // Sample neighboring cells (3x3 kernel)
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < res && ny >= 0 && ny < res)
                        {
                            sum += map[ny, nx];
                            count++;
                        }
                    }
                }

                temp[y, x] = sum / count;
            }
        }

        // Copy smoothed values back
        System.Array.Copy(temp, map, map.Length);
    }

    void Randomize(float[,] map, float strength)
    {
        int res = map.GetLength(0);
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // Create subtle random variation (dips and rises)
                float randomVariation = (Random.value - 0.5f) * strength;
                map[y, x] = Mathf.Clamp01(map[y, x] + randomVariation);
            }
        }
    }

    float[,] LoadRaw(byte[] data, int res)
    {
        float[,] heights = new float[res, res];
        int idx = 0;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                if (idx + 1 >= data.Length) break;
                ushort h = (ushort)(data[idx] | (data[idx + 1] << 8));
                idx += 2;
                heights[y, x] = h / 65535f;
            }
        }
        return heights;
    }

    float[,] BlendHeightmaps(float[,] terrainHeights, float[,] rawHeights, int terrainRes, int rawRes)
    {
        float[,] result = new float[terrainRes, terrainRes];

        for (int y = 0; y < terrainRes; y++)
        {
            for (int x = 0; x < terrainRes; x++)
            {
                // Map terrain coordinates to RAW coordinates
                float rawX = (x / (float)terrainRes) * rawRes;
                float rawY = (y / (float)terrainRes) * rawRes;

                // Bilinear interpolation for smooth sampling
                float sampledRaw = SampleBilinear(rawHeights, rawX, rawY);

                // Blend: keep most of terrain, add subtle variation from RAW
                // Convert RAW sample to a variation (-0.5 to 0.5 range for dips/rises)
                float rawVariation = (sampledRaw - 0.5f) * blendStrength;

                result[y, x] = Mathf.Clamp01(terrainHeights[y, x] + rawVariation);
            }
        }

        return result;
    }

    float SampleBilinear(float[,] map, float x, float y)
    {
        int res = map.GetLength(0);

        // Clamp coordinates
        x = Mathf.Clamp(x, 0, res - 1.001f);
        y = Mathf.Clamp(y, 0, res - 1.001f);

        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Mathf.Min(x0 + 1, res - 1);
        int y1 = Mathf.Min(y0 + 1, res - 1);

        float fx = x - x0;
        float fy = y - y0;

        float h00 = map[y0, x0];
        float h10 = map[y0, x1];
        float h01 = map[y1, x0];
        float h11 = map[y1, x1];

        float h0 = Mathf.Lerp(h00, h10, fx);
        float h1 = Mathf.Lerp(h01, h11, fx);

        return Mathf.Lerp(h0, h1, fy);
    }
}