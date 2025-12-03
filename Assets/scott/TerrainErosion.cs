using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerrainErosion : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Hydraulic Erosion (Water Flow)")]
    [Tooltip("Number of water droplets to simulate")]
    [Range(1000, 100000)]
    public int dropletCount = 50000;

    [Tooltip("How many steps each droplet takes")]
    [Range(4, 64)]
    public int maxDropletLifetime = 30;

    [Tooltip("How much sediment a droplet can carry")]
    [Range(0.01f, 1f)]
    public float sedimentCapacity = 0.3f;

    [Tooltip("How fast droplets pick up sediment")]
    [Range(0.1f, 1f)]
    public float erosionRate = 0.5f;

    [Tooltip("How fast droplets drop sediment")]
    [Range(0.1f, 1f)]
    public float depositionRate = 0.3f;

    [Tooltip("How fast water evaporates")]
    [Range(0.01f, 0.5f)]
    public float evaporationRate = 0.02f;

    [Tooltip("Minimum slope required for erosion")]
    [Range(0.01f, 0.2f)]
    public float minSlope = 0.01f;

    [Tooltip("Initial water amount per droplet")]
    [Range(0.5f, 2f)]
    public float waterAmount = 1f;

    [Header("Thermal Erosion (Slope Weathering)")]
    [Tooltip("Number of thermal erosion iterations")]
    [Range(0, 50)]
    public int thermalIterations = 10;

    [Tooltip("Maximum stable slope angle (degrees)")]
    [Range(30f, 80f)]
    public float talusAngle = 45f;

    [Tooltip("How much material moves down slopes")]
    [Range(0.1f, 1f)]
    public float thermalErosionRate = 0.5f;

    [Header("Rain Settings")]
    [Tooltip("Simulates rainfall across terrain")]
    public bool simulateRainfall = true;

    [Tooltip("Amount of rain per iteration")]
    [Range(0f, 0.1f)]
    public float rainfallAmount = 0.01f;

    public void ApplyHydraulicErosion()
    {
#if UNITY_EDITOR
        if (terrain == null)
        {
            Debug.LogError("Assign a Terrain first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        System.Random random = new System.Random();

        // Simulate droplets
        for (int i = 0; i < dropletCount; i++)
        {
            // Random starting position
            float x = (float)random.NextDouble() * (resolution - 1);
            float y = (float)random.NextDouble() * (resolution - 1);

            float sediment = 0f;
            float water = waterAmount;
            float velocity = 1f;

            for (int step = 0; step < maxDropletLifetime; step++)
            {
                int xi = (int)x;
                int yi = (int)y;

                // Get current height
                float currentHeight = heights[yi, xi];

                // Find steepest descent direction
                float steepestSlope = 0f;
                int bestX = xi;
                int bestY = yi;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = xi + dx;
                        int ny = yi + dy;

                        if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution) continue;

                        float neighborHeight = heights[ny, nx];
                        float slope = currentHeight - neighborHeight;

                        if (slope > steepestSlope)
                        {
                            steepestSlope = slope;
                            bestX = nx;
                            bestY = ny;
                        }
                    }
                }

                // If no downward slope, deposit and stop
                if (steepestSlope < minSlope)
                {
                    heights[yi, xi] += sediment;
                    break;
                }

                // Calculate sediment capacity based on slope and velocity
                float capacity = Mathf.Max(steepestSlope, minSlope) * velocity * water * sedimentCapacity;

                // Erosion or deposition
                if (sediment > capacity)
                {
                    // Deposit excess sediment
                    float deposit = (sediment - capacity) * depositionRate;
                    heights[yi, xi] += deposit;
                    sediment -= deposit;
                }
                else
                {
                    // Erode terrain
                    float erode = Mathf.Min((capacity - sediment) * erosionRate, steepestSlope);
                    heights[yi, xi] -= erode;
                    sediment += erode;
                }

                // Move to next position
                x = bestX;
                y = bestY;

                // Update velocity and evaporate water
                velocity = Mathf.Sqrt(velocity * velocity + steepestSlope);
                water *= (1f - evaporationRate);

                // Stop if out of water
                if (water < 0.01f) break;
            }
        }

        terrainData.SetHeights(0, 0, heights);
        Debug.Log($"Hydraulic erosion applied - {dropletCount} droplets simulated");
#endif
    }

    public void ApplyThermalErosion()
    {
#if UNITY_EDITOR
        if (terrain == null)
        {
            Debug.LogError("Assign a Terrain first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        float talusThreshold = Mathf.Tan(talusAngle * Mathf.Deg2Rad) * (1f / resolution);

        for (int iteration = 0; iteration < thermalIterations; iteration++)
        {
            float[,] newHeights = (float[,])heights.Clone();

            for (int y = 1; y < resolution - 1; y++)
            {
                for (int x = 1; x < resolution - 1; x++)
                {
                    float currentHeight = heights[y, x];
                    float totalDiff = 0f;
                    int neighbors = 0;

                    // Check all 8 neighbors
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            float neighborHeight = heights[ny, nx];
                            float heightDiff = currentHeight - neighborHeight;

                            // If slope is too steep, material slides down
                            if (heightDiff > talusThreshold)
                            {
                                totalDiff += heightDiff - talusThreshold;
                                neighbors++;
                            }
                        }
                    }

                    // Distribute material to lower neighbors
                    if (neighbors > 0)
                    {
                        float transfer = (totalDiff / neighbors) * thermalErosionRate;
                        newHeights[y, x] -= transfer * neighbors;

                        // Add to neighbors
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                int nx = x + dx;
                                int ny = y + dy;

                                float neighborHeight = heights[ny, nx];
                                float heightDiff = currentHeight - neighborHeight;

                                if (heightDiff > talusThreshold)
                                {
                                    newHeights[ny, nx] += transfer;
                                }
                            }
                        }
                    }
                }
            }

            heights = newHeights;
        }

        terrainData.SetHeights(0, 0, heights);
        Debug.Log($"Thermal erosion applied - {thermalIterations} iterations");
#endif
    }

    public void ApplyRainfall()
    {
#if UNITY_EDITOR
        if (terrain == null)
        {
            Debug.LogError("Assign a Terrain first.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        // Simple rainfall simulation - just adds water/erosion to all points
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Simulate small amount of erosion from rain impact
                float erosion = rainfallAmount * 0.1f;
                heights[y, x] -= erosion;
            }
        }

        terrainData.SetHeights(0, 0, heights);
        Debug.Log("Rainfall applied");
#endif
    }

    public void ApplyCombinedErosion()
    {
#if UNITY_EDITOR
        // Apply in realistic order
        if (simulateRainfall)
        {
            ApplyRainfall();
        }

        ApplyHydraulicErosion();
        ApplyThermalErosion();

        Debug.Log("Combined erosion complete!");
#endif
    }
}