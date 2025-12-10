using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter))]
public class TerrainCollider : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private float cellSize = 2.0f;

    [Header("Debug Visualization")]
    [SerializeField] private bool debugDrawTriangles = false;
    [SerializeField] private bool debugDrawGrid = false;
    [SerializeField] private bool debugDrawBounds = true;

    private MeshCollider meshCollider;
    private MeshFilter meshFilter;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError($"TerrainCollider on {gameObject.name}: No MeshFilter or Mesh found!");
            return;
        }

        meshCollider = new MeshCollider(meshFilter.sharedMesh, transform.lossyScale);

        meshCollider.SetOffset(float4x4.TRS(
            (float3)transform.position,
            (quaternion)transform.rotation,
            new float3(1, 1, 1)
        ));

        meshCollider.UpdateInternals();

        Debug.Log($"TerrainCollider initialized on {gameObject.name} with {meshFilter.sharedMesh.triangles.Length / 3} triangles");
    }

    void Update()
    {
        if (meshCollider != null)
        {
            meshCollider.SetOffset(float4x4.TRS(
                (float3)transform.position,
                (quaternion)transform.rotation,
                new float3(1, 1, 1)
            ));
            meshCollider.UpdateInternals();

            meshCollider.debugDrawTriangles = debugDrawTriangles;
            meshCollider.debugDrawGrid = debugDrawGrid;
            meshCollider.debugDrawBounds = debugDrawBounds;
        }
    }

    public MeshCollider GetMeshCollider()
    {
        return meshCollider;
    }

    void OnDrawGizmos()
    {
        if (meshCollider != null)
        {
            meshCollider.DrawDebugGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Gizmos.color = Color.magenta;
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

            Gizmos.DrawLine(v0, v1);
            Gizmos.DrawLine(v1, v2);
            Gizmos.DrawLine(v2, v0);
        }
    }
}