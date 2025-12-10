using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RawHeightmapImporter : MonoBehaviour
{
    [System.Serializable]
    public class HeightmapLayer
    {
        public DefaultAsset rawFile;
        [Range(0f, 1f)]
        public float blendAmount = 1f;
    }

    [Header("Terrain")]
    public Terrain terrain;

    [Header("Heightmap Layers")]
    public List<HeightmapLayer> heightmapLayers = new List<HeightmapLayer>();
    public int rawResolution = 512;

    [Header("Octaves Settings")]
    [Tooltip("Number of noise layers to combine (more = more detail)")]
    [Range(1, 6)]
    public int octaves = 3;

    [Tooltip("Starting frequency multiplier")]
    [Range(0.5f, 4f)]
    public float baseFrequency = 1f;

    [Tooltip("Frequency multiplier for each octave (typically 2.0)")]
    [Range(1.5f, 3f)]
    public float lacunarity = 2f;

    [Tooltip("Amplitude ratio between octaves (typically 0.5, lower = smoother)")]
    [Range(0.2f, 0.8f)]
    public float persistence = 0.5f;

    [Header("Controls")]
    [Tooltip("Overall height/intensity. 0 = completely flat")]
    [Range(0f, 2f)]
    public float heightScale = 0.5f;

    [Tooltip("Reshape elevation curve. >1 = deeper valleys, <1 = flatter peaks")]
    [Range(0.1f, 5f)]
    public float redistributionExponent = 1f;

    [Header("Post-Processing")]
    public float smoothAmount = 0f;

    [Header("Randomization")]
    [Tooltip("Strength of random variations")]
    [Range(0f, 1f)]
    public float randomStrength = 0.3f;

    public void ApplyHeightmap()
    {
#if UNITY_EDITOR
        if (terrain == null || heightmapLayers.Count == 0)
        {
            Debug.LogError("Assign a Terrain and at least one heightmap layer first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int terrainResolution = terrainData.heightmapResolution;

        float[,] combined = new float[terrainResolution, terrainResolution];
        float totalBlend = 0f;

        foreach (var layer in heightmapLayers)
        {
            if (layer.rawFile == null || layer.blendAmount <= 0f) continue;

            string path = AssetDatabase.GetAssetPath(layer.rawFile);
            byte[] data = File.ReadAllBytes(path);
            float[,] rawHeights = LoadRaw(data, rawResolution);

            float[,] layerResult = ApplyHeightmapWithControls(rawHeights, terrainResolution);

            for (int y = 0; y < terrainResolution; y++)
            {
                for (int x = 0; x < terrainResolution; x++)
                {
                    combined[y, x] += layerResult[y, x] * layer.blendAmount;
                }
            }
            totalBlend += layer.blendAmount;
        }

        if (totalBlend > 0)
        {
            for (int y = 0; y < terrainResolution; y++)
            {
                for (int x = 0; x < terrainResolution; x++)
                {
                    combined[y, x] /= totalBlend;
                }
            }
        }

        if (smoothAmount > 0)
        {
            for (int i = 0; i < Mathf.RoundToInt(smoothAmount); i++)
            {
                SmoothHeightmap(combined);
            }
        }

        terrainData.SetHeights(0, 0, combined);

        Debug.Log($"Heightmap applied - {heightmapLayers.Count} layers, Octaves: {octaves}");
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
        float[,] currentHeights = terrainData.GetHeights(0, 0, terrainResolution, terrainResolution);

        for (int y = 0; y < terrainResolution; y++)
        {
            for (int x = 0; x < terrainResolution; x++)
            {
                float randomVariation = (Random.value - 0.5f) * randomStrength * heightScale;
                currentHeights[y, x] = Mathf.Clamp01(currentHeights[y, x] + randomVariation);
            }
        }

        if (smoothAmount > 0)
        {
            for (int i = 0; i < Mathf.RoundToInt(smoothAmount); i++)
            {
                SmoothHeightmap(currentHeights);
            }
        }

        terrainData.SetHeights(0, 0, currentHeights);

        Debug.Log($"Random variations applied - Strength: {randomStrength}");
#endif
    }

    float[,] ApplyHeightmapWithControls(float[,] rawHeights, int terrainResolution)
    {
        float[,] result = new float[terrainResolution, terrainResolution];
        int rawRes = rawHeights.GetLength(0);

        if (heightScale <= 0f)
        {
            return result;
        }

        // Calculate total amplitude for normalization
        float totalAmplitude = 0f;
        float amplitude = 1f;
        for (int i = 0; i < octaves; i++)
        {
            totalAmplitude += amplitude;
            amplitude *= persistence;
        }

        for (int y = 0; y < terrainResolution; y++)
        {
            for (int x = 0; x < terrainResolution; x++)
            {
                // Sample heightmap with multiple octaves
                float combinedHeight = 0f;
                float currentAmplitude = 1f;
                float currentFrequency = baseFrequency;

                for (int octave = 0; octave < octaves; octave++)
                {
                    float normalizedX = x / (float)terrainResolution;
                    float normalizedY = y / (float)terrainResolution;

                    float sampledX = normalizedX * rawRes * currentFrequency;
                    float sampledY = normalizedY * rawRes * currentFrequency;

                    float sampledHeight = SampleBilinear(rawHeights, sampledX, sampledY);

                    combinedHeight += sampledHeight * currentAmplitude;

                    currentFrequency *= lacunarity;
                    currentAmplitude *= persistence;
                }

                // Normalize by total amplitude
                combinedHeight /= totalAmplitude;

                // Apply redistribution (power curve for flat valleys)
                if (redistributionExponent != 1f)
                {
                    combinedHeight = Mathf.Pow(combinedHeight, redistributionExponent);
                }

                // Apply height scale
                result[y, x] = combinedHeight * heightScale;
            }
        }

        return result;
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

        System.Array.Copy(temp, map, map.Length);
    }

    float SampleBilinear(float[,] map, float x, float y)
    {
        int res = map.GetLength(0);

        x = Mathf.Repeat(x, res - 0.001f);
        y = Mathf.Repeat(y, res - 0.001f);

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