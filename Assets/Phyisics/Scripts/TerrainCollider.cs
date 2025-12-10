using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter))]
public class TerrainCollider : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private float cellSize = 2.0f;
    [SerializeField] private bool visualizeGrid = false;

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
        }
    }

    public MeshCollider GetMeshCollider()
    {
        return meshCollider;
    }
}