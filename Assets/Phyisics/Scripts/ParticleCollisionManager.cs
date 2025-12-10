using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class ParticleCollisionManager : MonoBehaviour
{
    [Header("Spatial Grid Settings")]
    [SerializeField] private bool useSpatialGrid = true;
    [SerializeField] private float cellSize = 5.0f;

    [Header("Auto Configure from Terrain")]
    [SerializeField] private bool autoDetectBounds = true;
    [SerializeField] private Terrain terrain;
    [SerializeField] private float verticalBoundsMargin = 50f;

    [Header("Manual Bounds")]
    [SerializeField] private Vector3 manualBoundsCenter = Vector3.zero;
    [SerializeField] private Vector3 manualBoundsSize = new Vector3(100, 100, 100);

    [Header("Grid Optimization")]
    [SerializeField] private bool autoAdjustCellSize = false;
    [SerializeField] private int targetParticlesPerCell = 5;
    [SerializeField] private float minCellSize = 1.0f;
    [SerializeField] private float maxCellSize = 20.0f;

    [Header("Debug")]
    [SerializeField] private bool drawSpatialGrid = true;
    [SerializeField] private bool drawBounds = true;
    [SerializeField] private bool drawTerrainBounds = true;

    private SpatialGrid spatialGrid;
    private List<ParticleUpdate> allParticles = new List<ParticleUpdate>();

    private Vector3 boundsCenter;
    private Vector3 boundsSize;

    private int totalChecks = 0;
    private int actualCollisions = 0;
    private int frameCount = 0;

    private static ParticleCollisionManager instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(this);
            return;
        }
        instance = this;

        CalculateBounds();
        InitializeSpatialGrid();
    }

    void Start()
    {
        if (autoDetectBounds && terrain == null)
        {
            terrain = FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                Debug.Log($"Auto-detected terrain: {terrain.name}");
                CalculateBounds();
                InitializeSpatialGrid();
            }
        }
    }

    void CalculateBounds()
    {
        if (autoDetectBounds && terrain != null)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;

            boundsCenter = terrainPos + new Vector3(
                terrainData.size.x / 2f,
                terrainData.size.y / 2f,
                terrainData.size.z / 2f
            );

            boundsSize = new Vector3(
                terrainData.size.x,
                terrainData.size.y + verticalBoundsMargin * 2f,
                terrainData.size.z
            );

            Debug.Log($"Auto-calculated bounds from terrain '{terrain.name}':");
            Debug.Log($"  Center: {boundsCenter}");
            Debug.Log($"  Size: {boundsSize}");
        }
        else
        {
            boundsCenter = manualBoundsCenter;
            boundsSize = manualBoundsSize;

            Debug.Log($"Using manual bounds:");
            Debug.Log($"  Center: {boundsCenter}");
            Debug.Log($"  Size: {boundsSize}");
        }
    }

    void InitializeSpatialGrid()
    {
        float halfWidth = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z) / 2f;
        spatialGrid = new SpatialGrid(cellSize, (float3)boundsCenter, halfWidth);
        Debug.Log($"Initialized spatial grid with cell size: {cellSize}m");
    }

    void FixedUpdate()
    {
        if (!useSpatialGrid) return;

        totalChecks = 0;
        actualCollisions = 0;
        frameCount++;

        if (autoAdjustCellSize && frameCount % 100 == 0)
        {
            AdjustCellSize();
        }

        spatialGrid.Clear();

        foreach (var particle in allParticles)
        {
            if (particle != null && particle.enabled && particle.enableSphereCollision)
            {
                spatialGrid.Insert(particle, particle.position, particle.radius);
            }
        }
    }

    void AdjustCellSize()
    {
        if (allParticles.Count == 0) return;

        int cellCount = spatialGrid.GetCellCount();
        if (cellCount == 0) return;

        float avgParticlesPerCell = (float)allParticles.Count / cellCount;

        if (avgParticlesPerCell > targetParticlesPerCell * 2)
        {
            float newCellSize = Mathf.Max(minCellSize, cellSize * 0.9f);
            if (newCellSize != cellSize)
            {
                cellSize = newCellSize;
                InitializeSpatialGrid();
                Debug.Log($"Reduced cell size to {cellSize:F2}m (avg particles/cell: {avgParticlesPerCell:F1})");
            }
        }
        else if (avgParticlesPerCell < targetParticlesPerCell * 0.5f && cellCount > 10)
        {
  
            float newCellSize = Mathf.Min(maxCellSize, cellSize * 1.1f);
            if (newCellSize != cellSize)
            {
                cellSize = newCellSize;
                InitializeSpatialGrid();
                Debug.Log($"Increased cell size to {cellSize:F2}m (avg particles/cell: {avgParticlesPerCell:F1})");
            }
        }
    }

    [ContextMenu("Recalculate Bounds")]
    public void RecalculateBounds()
    {
        CalculateBounds();
        InitializeSpatialGrid();
    }

    [ContextMenu("Reset Cell Size")]
    public void ResetCellSize()
    {
        cellSize = 5.0f;
        InitializeSpatialGrid();
    }

    public static void RegisterParticle(ParticleUpdate particle)
    {
        if (instance != null && !instance.allParticles.Contains(particle))
        {
            instance.allParticles.Add(particle);
        }
    }

    public static void UnregisterParticle(ParticleUpdate particle)
    {
        if (instance != null)
        {
            instance.allParticles.Remove(particle);
        }
    }

    public static void GetNearbyParticles(ParticleUpdate particle, Vector3 position, float radius, List<ParticleUpdate> results)
    {
        results.Clear();

        if (instance == null) return;

        if (instance.useSpatialGrid)
        {
            instance.spatialGrid.GetNearbyParticles(position, radius * 2f, results);
            results.Remove(particle);
        }
        else
        {
            foreach (var other in instance.allParticles)
            {
                if (other != particle && other != null && other.enabled)
                {
                    results.Add(other);
                }
            }
        }
    }

    public static void IncrementChecks() { if (instance != null) instance.totalChecks++; }
    public static void IncrementCollisions() { if (instance != null) instance.actualCollisions++; }

    void OnDrawGizmos()
    {
        if (drawTerrainBounds && terrain != null)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainCenter = terrainPos + terrainData.size / 2f;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(terrainCenter, terrainData.size);
        }

        if (drawBounds)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boundsCenter, boundsSize);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(boundsCenter, 2f);
        }

        if (drawSpatialGrid && spatialGrid != null && Application.isPlaying)
        {
            spatialGrid.DrawDebugGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            CalculateBounds();
        }

        Gizmos.color = new Color(0, 1, 1, 0.3f);

        int cellsX = Mathf.CeilToInt(boundsSize.x / cellSize);
        int cellsY = Mathf.CeilToInt(boundsSize.y / cellSize);
        int cellsZ = Mathf.CeilToInt(boundsSize.z / cellSize);

        Vector3 startCorner = boundsCenter - boundsSize / 2f;

        for (int x = 0; x < Mathf.Min(cellsX, 20); x++)
        {
            for (int z = 0; z < Mathf.Min(cellsZ, 20); z++)
            {
                Vector3 cellPos = startCorner + new Vector3(
                    (x + 0.5f) * cellSize,
                    boundsSize.y / 2f,
                    (z + 0.5f) * cellSize
                );
                Gizmos.DrawWireCube(cellPos, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}