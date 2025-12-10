using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class MeshCollider : Collider
{
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector3 scale;

    private Dictionary<int2, List<int>> spatialGrid;
    private float cellSize = 2.0f;
    private float3 gridMin;
    private float3 gridMax;

    public bool debugDrawTriangles = false;
    public bool debugDrawGrid = false;
    public bool debugDrawBounds = false;

    public MeshCollider(Mesh m, Vector3 meshScale) : base(ColliderType.Plane)
    {
        mesh = m;
        scale = meshScale;
        vertices = mesh.vertices;
        triangles = mesh.triangles;

        BuildSpatialGrid();
    }

    private void BuildSpatialGrid()
    {
        spatialGrid = new Dictionary<int2, List<int>>();

        gridMin = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        gridMax = new float3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var v in vertices)
        {
            float3 worldV = TransformVertex(v);
            gridMin = math.min(gridMin, worldV);
            gridMax = math.max(gridMax, worldV);
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int triIndex = i / 3;

            float3 v0 = TransformVertex(vertices[triangles[i]]);
            float3 v1 = TransformVertex(vertices[triangles[i + 1]]);
            float3 v2 = TransformVertex(vertices[triangles[i + 2]]);

            float minX = math.min(math.min(v0.x, v1.x), v2.x);
            float maxX = math.max(math.max(v0.x, v1.x), v2.x);
            float minZ = math.min(math.min(v0.z, v1.z), v2.z);
            float maxZ = math.max(math.max(v0.z, v1.z), v2.z);

            int minCellX = Mathf.FloorToInt(minX / cellSize);
            int maxCellX = Mathf.FloorToInt(maxX / cellSize);
            int minCellZ = Mathf.FloorToInt(minZ / cellSize);
            int maxCellZ = Mathf.FloorToInt(maxZ / cellSize);

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cz = minCellZ; cz <= maxCellZ; cz++)
                {
                    int2 cell = new int2(cx, cz);
                    if (!spatialGrid.ContainsKey(cell))
                    {
                        spatialGrid[cell] = new List<int>();
                    }
                    spatialGrid[cell].Add(triIndex);
                }
            }
        }

        Debug.Log($"MeshCollider: Built spatial grid with {spatialGrid.Count} cells for {triangles.Length / 3} triangles");
    }

    private float3 TransformVertex(Vector3 localVertex)
    {
        float3 scaled = new float3(localVertex.x * scale.x, localVertex.y * scale.y, localVertex.z * scale.z);
        float3 transformed = math.mul(offset, new float4(scaled, 1)).xyz;
        return transformed;
    }

    public void GetPotentialCollisionTriangles(float3 position, float radius, List<int> outTriangles)
    {
        outTriangles.Clear();
        int minCellX = Mathf.FloorToInt((position.x - radius) / cellSize);
        int maxCellX = Mathf.FloorToInt((position.x + radius) / cellSize);
        int minCellZ = Mathf.FloorToInt((position.z - radius) / cellSize);
        int maxCellZ = Mathf.FloorToInt((position.z + radius) / cellSize);

        HashSet<int> uniqueTriangles = new HashSet<int>();

        for (int cx = minCellX; cx <= maxCellX; cx++)
        {
            for (int cz = minCellZ; cz <= maxCellZ; cz++)
            {
                int2 cell = new int2(cx, cz);
                if (spatialGrid.ContainsKey(cell))
                {
                    foreach (int triIndex in spatialGrid[cell])
                    {
                        uniqueTriangles.Add(triIndex);
                    }
                }
            }
        }

        outTriangles.AddRange(uniqueTriangles);
    }

    public void GetTriangle(int triangleIndex, out float3 v0, out float3 v1, out float3 v2)
    {
        int baseIndex = triangleIndex * 3;
        v0 = TransformVertex(vertices[triangles[baseIndex]]);
        v1 = TransformVertex(vertices[triangles[baseIndex + 1]]);
        v2 = TransformVertex(vertices[triangles[baseIndex + 2]]);
    }

    public void DrawDebugGizmos()
    {
        if (debugDrawBounds)
        {
            Gizmos.color = Color.cyan;
            float3 center = (gridMin + gridMax) / 2f;
            float3 size = gridMax - gridMin;
            Gizmos.DrawWireCube((Vector3)center, (Vector3)size);
        }

        if (debugDrawGrid)
        {
            Gizmos.color = Color.yellow;
            foreach (var cell in spatialGrid.Keys)
            {
                float3 cellMin = new float3(cell.x * cellSize, gridMin.y, cell.y * cellSize);
                float3 cellMax = new float3((cell.x + 1) * cellSize, gridMax.y, (cell.y + 1) * cellSize);
                float3 cellCenter = (cellMin + cellMax) / 2f;
                float3 cellSize3D = new float3(cellSize, gridMax.y - gridMin.y, cellSize);
                Gizmos.DrawWireCube((Vector3)cellCenter, (Vector3)cellSize3D);
            }
        }

        if (debugDrawTriangles)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float3 v0 = TransformVertex(vertices[triangles[i]]);
                float3 v1 = TransformVertex(vertices[triangles[i + 1]]);
                float3 v2 = TransformVertex(vertices[triangles[i + 2]]);

                Gizmos.DrawLine((Vector3)v0, (Vector3)v1);
                Gizmos.DrawLine((Vector3)v1, (Vector3)v2);
                Gizmos.DrawLine((Vector3)v2, (Vector3)v0);
            }
        }
    }

    public float3 GetScale() { return (float3)scale; }
    public Mesh GetMesh() { return mesh; }
}