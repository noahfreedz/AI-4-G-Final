using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerrainErosion : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;
    public WaterLevel waterLevel;

    [Header("Underwater Erosion Settings")]
    [Range(0.0001f, 0.01f)] public float erosionStrength = 0.002f;
    [Range(1, 50)] public int erosionDepth = 10;
    [Range(0.01f, 0.5f)] public float maxErodableSlope = 0.15f;
    public bool flowTowardDeepest = true;
    [Range(0, 5)] public int smoothingPasses = 1;
    [Range(1, 5)] public int erosionRadius = 2;

    [Header("River Formation")]
    [Range(0.1f, 1f)] public float spreadProbability = 0.7f;
    [Range(0f, 1f)] public float slopeBias = 0.6f;

    [Header("Sediment Transport")]
    public bool transportToCenter = false;
    [Range(0f, 1f)] public float depositionStrength = 0.5f;
    [Range(0.1f, 0.8f)] public float depositionRadius = 0.3f;

    [Header("Runtime Control")]
    public bool autoErode = false;
    [Range(0.01f, 2f)] public float stepInterval = 0.5f;
    [Range(0, 1000)] public int maxAutoSteps = 100;

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

    private List<Vector2Int> underwaterCellsList;
    private List<Vector2Int> waterEdgeCellsList;
    private HashSet<Vector2Int> underwaterCellsSet;
    
    private float[][] erosionBrushWeights;
    private float totalSedimentEroded = 0f;
    private Vector2Int terrainCenter;
    
    private float cachedWaterHeight;
    
    private List<Vector2Int> neighborBuffer = new List<Vector2Int>(8);
    private Queue<Vector2Int> erosionQueue = new Queue<Vector2Int>(1000);
    private HashSet<Vector2Int> processedSet = new HashSet<Vector2Int>();

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
        heights = terrainData.GetHeights(0, 0, resolution, resolution);
        random = new System.Random();
        terrainCenter = new Vector2Int(resolution / 2, resolution / 2);

        InitializeErosionBrush();
        RefreshTerrainData();
        
        Debug.Log($"Found {underwaterCellsList.Count} underwater cells, {waterEdgeCellsList.Count} edge cells");
    }

    public void RefreshTerrainData()
    {
        if (terrain == null) return;
        
        heights = terrainData.GetHeights(0, 0, resolution, resolution);
        cachedWaterHeight = waterLevel.GetWaterHeight();
        
        FindUnderwaterCells();
        FindWaterEdge();
        
        Debug.Log($"Terrain data refreshed. Underwater cells: {underwaterCellsList.Count}, Edge cells: {waterEdgeCellsList.Count}");
    }

    void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        if (!autoErode) return;

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
            autoErosionStepsCompleted = 0;
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

        // Cache water height once per step
        cachedWaterHeight = waterLevel.GetWaterHeight();
        
        FindUnderwaterCells();
        FindWaterEdge();

        if (waterEdgeCellsList.Count == 0)
        {
            Debug.LogWarning("No water edge found - is terrain below water level?");
            return;
        }

        totalSedimentEroded = 0f;
        ErodeFromWaterEdge();

        if (transportToCenter && totalSedimentEroded > 0)
        {
            DepositSedimentAtCenter();
        }

        if (smoothingPasses > 0)
        {
            for (int i = 0; i < smoothingPasses; i++)
            {
                SmoothUnderwater();
            }
        }

        terrainData.SetHeights(0, 0, heights);
        erosionSteps++;
        
        Debug.Log($"Erosion step {erosionSteps} complete. Eroded near {waterEdgeCellsList.Count} edge cells. " +
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
        // Reuse lists to avoid allocations
        if (underwaterCellsList == null)
            underwaterCellsList = new List<Vector2Int>(resolution * resolution / 4);
        else
            underwaterCellsList.Clear();

        float invTerrainHeight = 1f / terrainSize.y;
        float waterHeightNormalized = (cachedWaterHeight - terrainPos.y) * invTerrainHeight;

        // Direct height comparison
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (heights[y, x] < waterHeightNormalized)
                {
                    underwaterCellsList.Add(new Vector2Int(x, y));
                }
            }
        }

        // Only create HashSet when needed
        if (underwaterCellsSet == null)
            underwaterCellsSet = new HashSet<Vector2Int>(underwaterCellsList);
        else
        {
            underwaterCellsSet.Clear();
            for (int i = 0; i < underwaterCellsList.Count; i++)
                underwaterCellsSet.Add(underwaterCellsList[i]);
        }
    }

    private void FindWaterEdge()
    {
        if (waterEdgeCellsList == null)
            waterEdgeCellsList = new List<Vector2Int>(underwaterCellsList.Count / 10);
        else
            waterEdgeCellsList.Clear();

        for (int i = 0; i < underwaterCellsList.Count; i++)
        {
            Vector2Int cell = underwaterCellsList[i];
            
            bool isEdge = false;

            int x = cell.x, y = cell.y;
            
            if (x > 0 && !underwaterCellsSet.Contains(new Vector2Int(x - 1, y))) isEdge = true;
            else if (x < resolution - 1 && !underwaterCellsSet.Contains(new Vector2Int(x + 1, y))) isEdge = true;
            else if (y > 0 && !underwaterCellsSet.Contains(new Vector2Int(x, y - 1))) isEdge = true;
            else if (y < resolution - 1 && !underwaterCellsSet.Contains(new Vector2Int(x, y + 1))) isEdge = true;
            else
            {
                // Check diagonals only if cardinal directions didn't find edge
                if (x > 0 && y > 0 && !underwaterCellsSet.Contains(new Vector2Int(x - 1, y - 1))) isEdge = true;
                else if (x < resolution - 1 && y > 0 && !underwaterCellsSet.Contains(new Vector2Int(x + 1, y - 1))) isEdge = true;
                else if (x > 0 && y < resolution - 1 && !underwaterCellsSet.Contains(new Vector2Int(x - 1, y + 1))) isEdge = true;
                else if (x < resolution - 1 && y < resolution - 1 && !underwaterCellsSet.Contains(new Vector2Int(x + 1, y + 1))) isEdge = true;
            }

            if (isEdge)
                waterEdgeCellsList.Add(cell);
        }
    }

    private void ErodeFromWaterEdge()
    {
        erosionQueue.Clear();
        processedSet.Clear();

        // Add all edge cells to queue
        for (int i = 0; i < waterEdgeCellsList.Count; i++)
        {
            erosionQueue.Enqueue(waterEdgeCellsList[i]);
            processedSet.Add(waterEdgeCellsList[i]);
        }

        int currentDepth = 0;
        
        while (erosionQueue.Count > 0 && currentDepth < erosionDepth)
        {
            int currentLevelCount = erosionQueue.Count;
            
            for (int i = 0; i < currentLevelCount; i++)
            {
                Vector2Int cell = erosionQueue.Dequeue();

                float erosionAmount = erosionStrength;

                if (flowTowardDeepest)
                {
                    float depth = GetDepthBelowWaterFast(cell.x, cell.y);
                    erosionAmount *= (1f + depth * 2f);
                }

                if (transportToCenter)
                {
                    float distToCenter = GetDistanceToCenterFast(cell.x, cell.y);
                    if (distToCenter < depositionRadius)
                    {
                        erosionAmount *= Mathf.Lerp(0.3f, 1f, distToCenter / depositionRadius);
                    }
                }

                float actualEroded = ErodeCell(cell.x, cell.y, erosionAmount);
                totalSedimentEroded += actualEroded;

                GetUnderwaterNeighborsFast(cell, neighborBuffer);

                if (flowTowardDeepest && neighborBuffer.Count > 0)
                {
                    for (int j = 1; j < neighborBuffer.Count; j++)
                    {
                        Vector2Int key = neighborBuffer[j];
                        float keyDepth = heights[key.y, key.x];
                        int k = j - 1;
                        
                        while (k >= 0 && heights[neighborBuffer[k].y, neighborBuffer[k].x] > keyDepth)
                        {
                            neighborBuffer[k + 1] = neighborBuffer[k];
                            k--;
                        }
                        neighborBuffer[k + 1] = key;
                    }
                }

                float currentHeight = heights[cell.y, cell.x];
                
                for (int j = 0; j < neighborBuffer.Count; j++)
                {
                    Vector2Int neighbor = neighborBuffer[j];
                    
                    if (processedSet.Contains(neighbor)) continue;
                    if (random.NextDouble() > spreadProbability) continue;

                    if (slopeBias > 0)
                    {
                        float neighborHeight = heights[neighbor.y, neighbor.x];
                        if (neighborHeight > currentHeight)
                        {
                            if (random.NextDouble() > (1f - slopeBias)) continue;
                        }
                    }

                    erosionQueue.Enqueue(neighbor);
                    processedSet.Add(neighbor);
                }
            }
            
            currentDepth++;
        }
    }

    private void DepositSedimentAtCenter()
    {
        int depositRadius = Mathf.RoundToInt(depositionRadius * resolution);
        float sedimentPerCell = (totalSedimentEroded * depositionStrength) / (depositRadius * depositRadius * Mathf.PI);

        int depositRadiusSq = depositRadius * depositRadius;
        
        for (int y = -depositRadius; y <= depositRadius; y++)
        {
            for (int x = -depositRadius; x <= depositRadius; x++)
            {
                int distSq = x * x + y * y;
                if (distSq > depositRadiusSq) continue;

                int cellX = terrainCenter.x + x;
                int cellY = terrainCenter.y + y;
                
                if (cellX < 0 || cellX >= resolution || cellY < 0 || cellY >= resolution) continue;

                float distance = Mathf.Sqrt(distSq);
                float depositWeight = 1f - (distance / depositRadius);
                depositWeight = depositWeight * depositWeight; // Square

                heights[cellY, cellX] = Mathf.Min(1f, heights[cellY, cellX] + sedimentPerCell * depositWeight);
            }
        }
    }

    private float GetDistanceToCenterFast(int x, int y)
    {
        float dx = (x - terrainCenter.x) / (float)resolution;
        float dy = (y - terrainCenter.y) / (float)resolution;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private void GetUnderwaterNeighborsFast(Vector2Int cell, List<Vector2Int> outNeighbors)
    {
        outNeighbors.Clear();
        
        int x = cell.x, y = cell.y;
        
        // Unrolled neighbor iteration
        if (x > 0 && underwaterCellsSet.Contains(new Vector2Int(x - 1, y)))
            outNeighbors.Add(new Vector2Int(x - 1, y));
        if (x < resolution - 1 && underwaterCellsSet.Contains(new Vector2Int(x + 1, y)))
            outNeighbors.Add(new Vector2Int(x + 1, y));
        if (y > 0 && underwaterCellsSet.Contains(new Vector2Int(x, y - 1)))
            outNeighbors.Add(new Vector2Int(x, y - 1));
        if (y < resolution - 1 && underwaterCellsSet.Contains(new Vector2Int(x, y + 1)))
            outNeighbors.Add(new Vector2Int(x, y + 1));
        if (x > 0 && y > 0 && underwaterCellsSet.Contains(new Vector2Int(x - 1, y - 1)))
            outNeighbors.Add(new Vector2Int(x - 1, y - 1));
        if (x < resolution - 1 && y > 0 && underwaterCellsSet.Contains(new Vector2Int(x + 1, y - 1)))
            outNeighbors.Add(new Vector2Int(x + 1, y - 1));
        if (x > 0 && y < resolution - 1 && underwaterCellsSet.Contains(new Vector2Int(x - 1, y + 1)))
            outNeighbors.Add(new Vector2Int(x - 1, y + 1));
        if (x < resolution - 1 && y < resolution - 1 && underwaterCellsSet.Contains(new Vector2Int(x + 1, y + 1)))
            outNeighbors.Add(new Vector2Int(x + 1, y + 1));
    }

    private float GetDepthBelowWaterFast(int x, int y)
    {
        // Direct height comparison - avoid world position calculation
        float invTerrainHeight = 1f / terrainSize.y;
        float waterHeightNormalized = (cachedWaterHeight - terrainPos.y) * invTerrainHeight;
        return waterHeightNormalized - heights[y, x];
    }

    private float ErodeCell(int x, int y, float amount)
    {
        float currentHeight = heights[y, x];
        float maxNeighborHeight = currentHeight;

        // Check neighbors for slope
        int minX = Mathf.Max(0, x - 1);
        int maxX = Mathf.Min(resolution - 1, x + 1);
        int minY = Mathf.Max(0, y - 1);
        int maxY = Mathf.Min(resolution - 1, y + 1);

        for (int ny = minY; ny <= maxY; ny++)
        {
            for (int nx = minX; nx <= maxX; nx++)
            {
                if (nx == x && ny == y) continue;
                if (heights[ny, nx] > maxNeighborHeight)
                    maxNeighborHeight = heights[ny, nx];
            }
        }

        float slope = maxNeighborHeight - currentHeight;
        if (slope > maxErodableSlope) return 0f;

        float totalEroded = 0f;

        // Apply erosion brush
        for (int dy = -erosionRadius; dy <= erosionRadius; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= resolution) continue;
            
            for (int dx = -erosionRadius; dx <= erosionRadius; dx++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= resolution) continue;

                float weight = erosionBrushWeights[dy + erosionRadius][dx + erosionRadius];
                float erosionAmount = amount * weight;
                float oldHeight = heights[ny, nx];
                heights[ny, nx] = Mathf.Max(0, heights[ny, nx] - erosionAmount);
                totalEroded += oldHeight - heights[ny, nx];
            }
        }

        return totalEroded;
    }

    private void SmoothUnderwater()
    {
        float[,] temp = new float[resolution, resolution];
        System.Array.Copy(heights, temp, heights.Length);

        for (int i = 0; i < underwaterCellsList.Count; i++)
        {
            Vector2Int cell = underwaterCellsList[i];
            float sum = 0f;
            int count = 0;

            int minX = Mathf.Max(0, cell.x - 1);
            int maxX = Mathf.Min(resolution - 1, cell.x + 1);
            int minY = Mathf.Max(0, cell.y - 1);
            int maxY = Mathf.Min(resolution - 1, cell.y + 1);

            for (int ny = minY; ny <= maxY; ny++)
            {
                for (int nx = minX; nx <= maxX; nx++)
                {
                    sum += temp[ny, nx];
                    count++;
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
        if (terrain == null || underwaterCellsList == null) return;

        if (showUnderwaterCells)
        {
            Gizmos.color = new Color(0.2f, 0.5f, 0.8f, 0.3f);
            for (int i = 0; i < underwaterCellsList.Count; i++)
            {
                Vector3 pos = GetWorldPosition(underwaterCellsList[i].x, underwaterCellsList[i].y);
                Gizmos.DrawCube(pos, Vector3.one * 0.5f);
            }
        }

        if (showErosionFront)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < waterEdgeCellsList.Count; i++)
            {
                Vector3 pos = GetWorldPosition(waterEdgeCellsList[i].x, waterEdgeCellsList[i].y);
                Gizmos.DrawWireCube(pos, Vector3.one);
            }
        }

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