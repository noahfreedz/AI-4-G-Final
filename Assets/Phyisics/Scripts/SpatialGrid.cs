using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class SpatialGrid
{
    private Dictionary<int3, List<ParticleUpdate>> grid;
    private float cellSize;
    private float3 boundsCenter;
    private float boundsHalfWidth;

    public SpatialGrid(float cellSize, float3 boundsCenter, float boundsHalfWidth)
    {
        this.cellSize = cellSize;
        this.boundsCenter = boundsCenter;
        this.boundsHalfWidth = boundsHalfWidth;
        grid = new Dictionary<int3, List<ParticleUpdate>>();
    }

    public void Clear()
    {
        foreach (var cell in grid.Values)
        {
            cell.Clear();
        }
    }

    public void Insert(ParticleUpdate particle, Vector3 position, float radius)
    {
        int3 minCell = WorldToGrid(position - Vector3.one * radius);
        int3 maxCell = WorldToGrid(position + Vector3.one * radius);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    int3 cellKey = new int3(x, y, z);

                    if (!grid.ContainsKey(cellKey))
                    {
                        grid[cellKey] = new List<ParticleUpdate>();
                    }

                    grid[cellKey].Add(particle);
                }
            }
        }
    }

    public void GetNearbyParticles(Vector3 position, float radius, List<ParticleUpdate> results)
    {
        results.Clear();
        HashSet<ParticleUpdate> uniqueParticles = new HashSet<ParticleUpdate>();

        int3 minCell = WorldToGrid(position - Vector3.one * radius);
        int3 maxCell = WorldToGrid(position + Vector3.one * radius);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    int3 cellKey = new int3(x, y, z);

                    if (grid.ContainsKey(cellKey))
                    {
                        foreach (var particle in grid[cellKey])
                        {
                            uniqueParticles.Add(particle);
                        }
                    }
                }
            }
        }

        results.AddRange(uniqueParticles);
    }

    private int3 WorldToGrid(Vector3 worldPos)
    {
        float3 relPos = (float3)worldPos - boundsCenter;
        return new int3(
            Mathf.FloorToInt(relPos.x / cellSize),
            Mathf.FloorToInt(relPos.y / cellSize),
            Mathf.FloorToInt(relPos.z / cellSize)
        );
    }

    public void DrawDebugGizmos()
    {
        if (grid.Count == 0) return;

        foreach (var kvp in grid)
        {
            int3 cellKey = kvp.Key;
            int particleCount = kvp.Value.Count;

            float3 cellCenter = boundsCenter + new float3(
                (cellKey.x + 0.5f) * cellSize,
                (cellKey.y + 0.5f) * cellSize,
                (cellKey.z + 0.5f) * cellSize
            );

            if (particleCount > 10)
                Gizmos.color = Color.red;
            else if (particleCount > 5)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.green;

            Gizmos.DrawWireCube((Vector3)cellCenter, Vector3.one * cellSize);

            Gizmos.DrawCube((Vector3)cellCenter, Vector3.one * (cellSize * 0.1f));
        }
    }

    public int GetCellCount() { return grid.Count; }
    public int GetTotalParticlesInGrid()
    {
        int total = 0;
        foreach (var cell in grid.Values)
        {
            total += cell.Count;
        }
        return total;
    }
}