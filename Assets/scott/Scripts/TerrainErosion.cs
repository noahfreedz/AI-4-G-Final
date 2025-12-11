using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerrainErosion : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;
    [Tooltip("WaterLevel script that defines the water height")]
    public WaterLevel waterLevel;

    [Header("Underwater Erosion Settings")]
    [Tooltip("How much to erode per step (depth in normalized units)")]
    [Range(0.0001f, 0.01f)]
    public float erosionStrength = 0.002f;

    [Tooltip("Maximum distance from water edge to erode (in terrain cells)")]
    [Range(1, 50)]
    public int erosionDepth = 10;

    [Tooltip("Maximum slope that can be eroded (prevents cutting down mountains)")]
    [Range(0.01f, 0.5f)]
    public float maxErodableSlope = 0.15f;

    [Tooltip("Prefer eroding deeper areas (creates channels toward lowest points)")]
    public bool flowTowardDeepest = true;

    [Tooltip("Smoothing iterations after erosion")]
    [Range(0, 5)]
    public int smoothingPasses = 1;

    [Tooltip("Erosion brush radius (creates smoother channels)")]
    [Range(1, 5)]
    public int erosionRadius = 2;

    [Header("River Formation")]
    [Tooltip("Probability of erosion spreading to neighbors (lower = narrower rivers)")]
    [Range(0.1f, 1f)]
    public float spreadProbability = 0.7f;

    [Tooltip("Favor downward slopes when eroding")]
    [Range(0f, 1f)]
    public float slopeBias = 0.6f;

    [Header("Sediment Transport")]
    [Tooltip("Push eroded sediment toward the center (for island building)")]
    public bool transportToCenter = false;

    [Tooltip("Strength of sediment deposition (higher = more buildup at center)")]
    [Range(0f, 1f)]
    public float depositionStrength = 0.5f;

    [Tooltip("Radius around center where sediment accumulates (in normalized units)")]
    [Range(0.1f, 0.8f)]
    public float depositionRadius = 0.3f;

    [Header("Runtime Control")]
    [Tooltip("If true, erosion will run continuously in Play Mode")]
    public bool autoErode = false;

    [Tooltip("Seconds between erosion steps")]
    [Range(0.01f, 2f)]
    public float stepInterval = 0.5f;

    [Tooltip("Maximum number of steps for auto erosion (0 = unlimited, prevents Unity freezing)")]
    [Range(0, 1000)]
    public int maxAutoSteps = 100;

    [Header("Debug Visualization")]
    public bool showUnderwaterCells = false;
    public bool showErosionFront = false;

    private TerrainData terrainData;
    private float[,] heights;
    private int resolution;
    private Vector3 terrainPos;
    private Vector3 terrainSize;
    private System.Random random;

    private float nextStepTime = 0f;
    private int erosionSteps = 0;
    private int autoErosionStepsCompleted = 0;

    private HashSet<Vector2Int> underwaterCells;
    private HashSet<Vector2Int> waterEdgeCells;
    private float[][] erosionBrushWeights;

    // For sediment transport
    private float totalSedimentEroded = 0f;
    private Vector2Int terrainCenter;

    void Start()
    {
        if (terrain == null)
        {
            Debug.LogError("TerrainErosion: Assign a Terrain first.");
            enabled = false;
            return;
        }

        if (waterLevel == null)
        {
            Debug.LogError("TerrainErosion: Assign a WaterLevel script.");
            enabled = false;
            return;
        }

        terrainData = terrain.terrainData;
        resolution = terrainData.heightmapResolution;
        terrainPos = terrain.transform.position;
        terrainSize = terrainData.size;

        // IMPORTANT: Get the CURRENT terrain heights (not cached from before Play mode)
        // This ensures we work with whatever terrain was generated at runtime
        heights = terrainData.GetHeights(0, 0, resolution, resolution);
        random = new System.Random();

        terrainCenter = new Vector2Int(resolution / 2, resolution / 2);

        InitializeErosionBrush();

        // Refresh underwater detection based on current terrain
        RefreshTerrainData();

        Debug.Log($"Found {underwaterCells.Count} underwater cells, {waterEdgeCells.Count} edge cells");
    }

    /// <summary>
    /// Call this after generating new terrain to update erosion system
    /// </summary>
    public void RefreshTerrainData()
    {
        if (terrain == null) return;

        // Reload current heights from terrain
        heights = terrainData.GetHeights(0, 0, resolution, resolution);

        // Recalculate underwater areas
        FindUnderwaterCells();
        FindWaterEdge();

        Debug.Log($"Terrain data refreshed. Underwater cells: {underwaterCells.Count}, Edge cells: {waterEdgeCells.Count}");
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        if (!autoErode) return;

        // Check if we've hit the max steps limit
        if (maxAutoSteps > 0 && autoErosionStepsCompleted >= maxAutoSteps)
        {
            autoErode = false;
            Debug.Log($"Auto erosion stopped: reached max steps limit ({maxAutoSteps})");
            return;
        }

        if (Time.time >= nextStepTime)
        {
            nextStepTime = Time.time + stepInterval;
            ApplyUnderwaterErosionStep();
            autoErosionStepsCompleted++;
        }
    }

    public void ToggleErosion()
    {
        autoErode = !autoErode;
        if (autoErode)
        {
            nextStepTime = Time.time;
            autoErosionStepsCompleted = 0; // Reset counter when starting
            Debug.Log($"TerrainErosion: Auto erosion started. Will run for {(maxAutoSteps > 0 ? maxAutoSteps.ToString() : "unlimited")} steps.");
        }
        else
        {
            Debug.Log($"TerrainErosion: Auto erosion stopped. Completed {autoErosionStepsCompleted} steps.");
        }
    }

    public void ApplyUnderwaterErosionStep()
    {
        if (heights == null || terrainData == null)
        {
            Debug.LogError("Terrain data not initialized");
            return;
        }

        // Refresh underwater detection
        FindUnderwaterCells();
        FindWaterEdge();

        if (waterEdgeCells.Count == 0)
        {
            Debug.LogWarning("No water edge found - is terrain below water level?");
            return;
        }

        // Reset sediment counter
        totalSedimentEroded = 0f;

        // Erode from the water edge inward
        ErodeFromWaterEdge();

        // Deposit sediment toward center if enabled
        if (transportToCenter && totalSedimentEroded > 0)
        {
            DepositSedimentAtCenter();
        }

        // Optional smoothing
        if (smoothingPasses > 0)
        {
            for (int i = 0; i < smoothingPasses; i++)
            {
                SmoothUnderwater();
            }
        }

        // Apply changes
        terrainData.SetHeights(0, 0, heights);
        erosionSteps++;

        Debug.Log($"Erosion step {erosionSteps} complete. Eroded near {waterEdgeCells.Count} edge cells. " +
                  $"Sediment: {totalSedimentEroded:F4}. Auto steps: {autoErosionStepsCompleted}/{(maxAutoSteps > 0 ? maxAutoSteps.ToString() : "?")}");
    }

    private void InitializeErosionBrush()
    {
        erosionBrushWeights = new float[erosionRadius * 2 + 1][];
        for (int i = 0; i < erosionBrushWeights.Length; i++)
        {
            erosionBrushWeights[i] = new float[erosionRadius * 2 + 1];
        }

        float weightSum = 0;
        for (int dy = -erosionRadius; dy <= erosionRadius; dy++)
        {
            for (int dx = -erosionRadius; dx <= erosionRadius; dx++)
            {
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float weight = Mathf.Max(0, erosionRadius - distance);
                erosionBrushWeights[dy + erosionRadius][dx + erosionRadius] = weight;
                weightSum += weight;
            }
        }

        // Normalize
        for (int dy = 0; dy < erosionBrushWeights.Length; dy++)
        {
            for (int dx = 0; dx < erosionBrushWeights[dy].Length; dx++)
            {
                erosionBrushWeights[dy][dx] /= weightSum;
            }
        }
    }

    private void FindUnderwaterCells()
    {
        underwaterCells = new HashSet<Vector2Int>();
        float waterHeight = waterLevel.GetWaterHeight();

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector3 worldPos = GetWorldPosition(x, y);
                if (worldPos.y < waterHeight)
                {
                    underwaterCells.Add(new Vector2Int(x, y));
                }
            }
        }
    }


    // finds Shore by looking at nabors 
    private void FindWaterEdge()
    {
        waterEdgeCells = new HashSet<Vector2Int>();

        foreach (var cell in underwaterCells)
        {
            // Check if any neighbor is above water
            bool isEdge = false;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = cell.x + dx;
                    int ny = cell.y + dy;

                    if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        continue;

                    if (!underwaterCells.Contains(new Vector2Int(nx, ny)))
                    {
                        isEdge = true;
                        break;
                    }
                }
                if (isEdge) break;
            }

            if (isEdge)
            {
                waterEdgeCells.Add(cell);
            }
        }
    }

    private void ErodeFromWaterEdge()
    {
        Queue<Vector2Int> erosionQueue = new Queue<Vector2Int>();
        HashSet<Vector2Int> processed = new HashSet<Vector2Int>();

        foreach (var edge in waterEdgeCells)
        {
            erosionQueue.Enqueue(edge);
            processed.Add(edge);
        }

        int currentDepth = 0;

        while (erosionQueue.Count > 0 && currentDepth < erosionDepth)
        {
            int currentLevelCount = erosionQueue.Count;

            for (int i = 0; i < currentLevelCount; i++)
            {
                Vector2Int cell = erosionQueue.Dequeue();

                // Erode this cell
                float erosionAmount = erosionStrength;

                // Increase erosion for deeper areas if flowing toward deepest
                if (flowTowardDeepest)
                {
                    float depth = GetDepthBelowWater(cell.x, cell.y);
                    erosionAmount *= (1f + depth * 2f);
                }

                // Reduce erosion near center if transporting sediment there
                if (transportToCenter)
                {
                    float distToCenter = GetDistanceToCenter(cell.x, cell.y);
                    if (distToCenter < depositionRadius)
                    {
                        // Reduce erosion near center
                        erosionAmount *= Mathf.Lerp(0.3f, 1f, distToCenter / depositionRadius);
                    }
                }

                float actualEroded = ErodeCell(cell.x, cell.y, erosionAmount);
                totalSedimentEroded += actualEroded;

                // Spread to neighbors
                List<Vector2Int> neighbors = GetUnderwaterNeighbors(cell);

                if (flowTowardDeepest)
                {
                    neighbors.Sort((a, b) =>
                        GetDepthBelowWater(b.x, b.y).CompareTo(GetDepthBelowWater(a.x, a.y))
                    );
                }

                foreach (var neighbor in neighbors)
                {
                    if (processed.Contains(neighbor))
                        continue;

                    if (random.NextDouble() > spreadProbability)
                        continue;

                    if (slopeBias > 0)
                    {
                        float currentHeight = heights[cell.y, cell.x];
                        float neighborHeight = heights[neighbor.y, neighbor.x];

                        if (neighborHeight > currentHeight)
                        {
                            if (random.NextDouble() > (1f - slopeBias))
                                continue;
                        }
                    }

                    erosionQueue.Enqueue(neighbor);
                    processed.Add(neighbor);
                }
            }

            currentDepth++;
        }
    }

    private void DepositSedimentAtCenter()
    {
        // Deposit sediment in a radius around the center
        int depositRadius = Mathf.RoundToInt(depositionRadius * resolution);
        float sedimentPerCell = (totalSedimentEroded * depositionStrength) / (depositRadius * depositRadius * Mathf.PI);

        for (int y = -depositRadius; y <= depositRadius; y++)
        {
            for (int x = -depositRadius; x <= depositRadius; x++)
            {
                int cellX = terrainCenter.x + x;
                int cellY = terrainCenter.y + y;

                if (cellX < 0 || cellX >= resolution || cellY < 0 || cellY >= resolution)
                    continue;

                float distance = Mathf.Sqrt(x * x + y * y);
                if (distance > depositRadius)
                    continue;


                float depositWeight = 1f - (distance / depositRadius);
                depositWeight = Mathf.Pow(depositWeight, 2f); // Square for more concentration

                heights[cellY, cellX] = Mathf.Min(1f, heights[cellY, cellX] + sedimentPerCell * depositWeight);
            }
        }
    }

    private float GetDistanceToCenter(int x, int y)
    {
        float dx = (x - terrainCenter.x) / (float)resolution;
        float dy = (y - terrainCenter.y) / (float)resolution;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private List<Vector2Int> GetUnderwaterNeighbors(Vector2Int cell)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = cell.x + dx;
                int ny = cell.y + dy;

                if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                    continue;

                Vector2Int neighbor = new Vector2Int(nx, ny);
                if (underwaterCells.Contains(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }
        }

        return neighbors;
    }

    private float GetDepthBelowWater(int x, int y)
    {
        Vector3 worldPos = GetWorldPosition(x, y);
        float waterHeight = waterLevel.GetWaterHeight();
        return (waterHeight - worldPos.y) / terrainSize.y;
    }

    private float ErodeCell(int x, int y, float amount)
    {
        // Check if slope is too steep to erode (prevents cutting down mountains)
        float currentHeight = heights[y, x];
        float maxNeighborHeight = currentHeight;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < resolution && ny >= 0 && ny < resolution)
                {
                    maxNeighborHeight = Mathf.Max(maxNeighborHeight, heights[ny, nx]);
                }
            }
        }

        float slope = maxNeighborHeight - currentHeight;

        // Don't erode if slope is too steep
        if (slope > maxErodableSlope)
        {
            return 0f;
        }

        float totalEroded = 0f;

        // Erode with brush
        for (int dy = -erosionRadius; dy <= erosionRadius; dy++)
        {
            for (int dx = -erosionRadius; dx <= erosionRadius; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < resolution && ny >= 0 && ny < resolution)
                {
                    float weight = erosionBrushWeights[dy + erosionRadius][dx + erosionRadius];
                    float erosionAmount = amount * weight;
                    float oldHeight = heights[ny, nx];
                    heights[ny, nx] = Mathf.Max(0, heights[ny, nx] - erosionAmount);
                    totalEroded += oldHeight - heights[ny, nx];
                }
            }
        }

        return totalEroded;
    }

    private void SmoothUnderwater()
    {
        float[,] temp = new float[resolution, resolution];
        System.Array.Copy(heights, temp, heights.Length);

        foreach (var cell in underwaterCells)
        {
            float sum = 0f;
            int count = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cell.x + dx;
                    int ny = cell.y + dy;

                    if (nx >= 0 && nx < resolution && ny >= 0 && ny < resolution)
                    {
                        sum += temp[ny, nx];
                        count++;
                    }
                }
            }

            heights[cell.y, cell.x] = sum / count;
        }
    }

    private Vector3 GetWorldPosition(int x, int y)
    {
        float height01 = heights[y, x];
        return new Vector3(
            terrainPos.x + (x / (float)(resolution - 1)) * terrainSize.x,
            terrainPos.y + height01 * terrainSize.y,
            terrainPos.z + (y / (float)(resolution - 1)) * terrainSize.z
        );
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showUnderwaterCells && !showErosionFront) return;
        if (terrain == null || underwaterCells == null) return;

        // Draw underwater cells
        if (showUnderwaterCells && underwaterCells != null)
        {
            Gizmos.color = new Color(0.2f, 0.5f, 0.8f, 0.3f);
            foreach (var cell in underwaterCells)
            {
                Vector3 pos = GetWorldPosition(cell.x, cell.y);
                Gizmos.DrawCube(pos, Vector3.one * 0.5f);
            }
        }

        // Draw water edge
        if (showErosionFront && waterEdgeCells != null)
        {
            Gizmos.color = Color.red;
            foreach (var cell in waterEdgeCells)
            {
                Vector3 pos = GetWorldPosition(cell.x, cell.y);
                Gizmos.DrawWireCube(pos, Vector3.one);
            }
        }

        // Draw deposition zone
        if (transportToCenter)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Vector3 centerPos = GetWorldPosition(terrainCenter.x, terrainCenter.y);
            float radius = depositionRadius * terrainSize.x;
            Gizmos.DrawWireSphere(centerPos, radius);
        }
    }
#endif
}