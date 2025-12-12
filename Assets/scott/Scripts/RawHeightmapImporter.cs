using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

public class RawHeightmapImporter : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Generation Mode")]
    [Tooltip("Use Perlin noise for completely random terrain, or sample from heightmap files")]
    public bool useRandomGeneration = true;

    [Header("Island Shaping")]
    [Tooltip("Enable island mode - pushes terrain down at edges using distance from center")]
    public bool useIslandMode = false;

    [Header("Distance Function")]
    [Tooltip("How much to blend between original noise (0) and island shape (1)")]
    [Range(0f, 1f)]
    public float islandMix = 0.5f;

    [Tooltip("Distance function for island shaping")]
    public enum DistanceFunction
    {
        Euclidean,        // Circular island
        EuclideanSquared, // Softer circular falloff
        Manhattan,        // Diamond-shaped island
        Diagonal,         // Square-ish with diagonal emphasis
        Blob              // Organic blob shape
    }
    public DistanceFunction distanceFunction = DistanceFunction.Euclidean;

    [Tooltip("Adjust island size - higher = smaller island")]
    [Range(0.5f, 3f)]
    public float islandScale = 1f;

    [Tooltip("Reshape the distance curve (>1 = sharper edges, <1 = softer edges)")]
    [Range(0.5f, 4f)]
    public float distanceExponent = 2f;

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

    float[,] GenerateRandomTerrain(int terrainResolution)
    {
        float[,] result = new float[terrainResolution, terrainResolution];

        if (heightScale <= 0f)
        {
            return result;
        }

        float offsetX = Random.Range(0f, 10000f);
        float offsetY = Random.Range(0f, 10000f);

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
                float combinedHeight = 0f;
                float currentAmplitude = 1f;
                float currentFrequency = baseFrequency;

                for (int octave = 0; octave < octaves; octave++)
                {
                    // Normalize to 0-1 range
                    float normalizedX = x / (float)(terrainResolution - 1);
                    float normalizedY = y / (float)(terrainResolution - 1);

                    // Generate Perlin noise at different frequencies
                    float sampleX = (normalizedX * currentFrequency * 10f) + offsetX;
                    float sampleY = (normalizedY * currentFrequency * 10f) + offsetY;

                    // Use Unity's Perlin noise for completely random generation
                    float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);

                    combinedHeight += noiseValue * currentAmplitude;

                    currentFrequency *= lacunarity;
                    currentAmplitude *= persistence;
                }

                combinedHeight /= totalAmplitude;

                // Apply island shaping if enabled
                if (useIslandMode)
                {
                    // Normalize to -0.5 to 0.5 range (centered)
                    float nx = (x / (float)(terrainResolution - 1)) - 0.5f;
                    float ny = (y / (float)(terrainResolution - 1)) - 0.5f;

                    // Calculate distance from center
                    float distance = CalculateDistance(nx, ny) * islandScale;

                    // Apply exponent to reshape the falloff curve
                    distance = Mathf.Pow(distance, distanceExponent);

                    // Create island shape (1 at center, 0 at edges)
                    float islandShape = Mathf.Max(0, 1f - distance);

                    // Mix between original noise and island shape
                    combinedHeight = Mathf.Lerp(combinedHeight, combinedHeight * islandShape, islandMix);
                }

                if (redistributionExponent != 1f)
                {
                    combinedHeight = Mathf.Pow(combinedHeight, redistributionExponent);
                }

                result[y, x] = combinedHeight * heightScale;
            }
        }

        return result;
    }

    float CalculateDistance(float nx, float ny)
    {
        switch (distanceFunction)
        {
            case DistanceFunction.Euclidean:
                return Mathf.Sqrt(nx * nx + ny * ny) * 2f;

            case DistanceFunction.EuclideanSquared:
                return (nx * nx + ny * ny) * 2f;

            case DistanceFunction.Manhattan:
                return (Mathf.Abs(nx) + Mathf.Abs(ny)) * 2f;

            case DistanceFunction.Diagonal:
                return Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) * 2f;

            case DistanceFunction.Blob:
                // Organic blob using noise
                float blobNoise = Mathf.PerlinNoise(nx * 3f + 100f, ny * 3f + 100f) * 0.3f;
                return (Mathf.Sqrt(nx * nx + ny * ny) + blobNoise) * 2f;

            default:
                return Mathf.Sqrt(nx * nx + ny * ny) * 2f;
        }
    }

    float[,] ApplyHeightmapWithControls(float[,] rawHeights, int terrainResolution)
    {
        float[,] result = new float[terrainResolution, terrainResolution];

        if (heightScale <= 0f)
        {
            return result;
        }

        // Generate random offset for this terrain generation
        float offsetX = Random.Range(0f, 10000f);
        float offsetY = Random.Range(0f, 10000f);

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
                float combinedHeight = 0f;
                float currentAmplitude = 1f;
                float currentFrequency = baseFrequency;

                for (int octave = 0; octave < octaves; octave++)
                {
                    // Normalize to 0-1 range
                    float normalizedX = x / (float)(terrainResolution - 1);
                    float normalizedY = y / (float)(terrainResolution - 1);

                    // Generate Perlin noise at different frequencies
                    float sampleX = (normalizedX * currentFrequency * 10f) + offsetX;
                    float sampleY = (normalizedY * currentFrequency * 10f) + offsetY;

                    // Use Unity's Perlin noise for completely random generation
                    float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);

                    combinedHeight += noiseValue * currentAmplitude;

                    currentFrequency *= lacunarity;
                    currentAmplitude *= persistence;
                }

                combinedHeight /= totalAmplitude;

                if (redistributionExponent != 1f)
                {
                    combinedHeight = Mathf.Pow(combinedHeight, redistributionExponent);
                }

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

}