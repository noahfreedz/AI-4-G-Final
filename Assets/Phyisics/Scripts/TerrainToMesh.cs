using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class TerrainToMesh : MonoBehaviour
{
    [Header("Terrain Settings")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private int resolution = 128;

    [Header("Generate")]
    [SerializeField] private bool generateOnStart = true;

    private MeshFilter meshFilter;

    void Start()
    {
        if (generateOnStart)
        {
            GenerateMesh();
        }
    }

    [ContextMenu("Generate Mesh from Terrain")]
    public void GenerateMesh()
    {
        if (terrain == null)
        {
            Debug.LogError("No terrain assigned!");
            return;
        }

        meshFilter = GetComponent<MeshFilter>();
        TerrainData terrainData = terrain.terrainData;
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;

        int stepX = Mathf.Max(1, width / resolution);
        int stepZ = Mathf.Max(1, height / resolution);
        int vertexCountX = width / stepX;
        int vertexCountZ = height / stepZ;

        Vector3[] vertices = new Vector3[vertexCountX * vertexCountZ];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(vertexCountX - 1) * (vertexCountZ - 1) * 6];

        for (int z = 0; z < vertexCountZ; z++)
        {
            for (int x = 0; x < vertexCountX; x++)
            {
                int index = z * vertexCountX + x;
                // to put them between 0 - 1
                float heightSampleX = (float)x / (vertexCountX - 1);
                float heightSampleZ = (float)z / (vertexCountZ - 1);
                float height1 = terrainData.GetInterpolatedHeight(heightSampleX, heightSampleZ);

                vertices[index] = new Vector3(
                    heightSampleX * terrainData.size.x,
                    height1,
                    heightSampleZ * terrainData.size.z
                );

                uvs[index] = new Vector2(heightSampleX, heightSampleZ);
            }
        }

        int triIndex = 0;
        for (int z = 0; z < vertexCountZ - 1; z++)
        {
            for (int x = 0; x < vertexCountX - 1; x++)
            {
                int vertIndex = z * vertexCountX + x;

                triangles[triIndex++] = vertIndex;
                triangles[triIndex++] = vertIndex + vertexCountX;
                triangles[triIndex++] = vertIndex + 1;

                triangles[triIndex++] = vertIndex + 1;
                triangles[triIndex++] = vertIndex + vertexCountX;
                triangles[triIndex++] = vertIndex + vertexCountX + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "TerrainCollisionMesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;

        Debug.Log($"Generated terrain mesh with {vertices.Length} vertices and {triangles.Length / 3} triangles");

        transform.position = terrain.transform.position;

        TerrainCollider terrainCollider = GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            terrainCollider = gameObject.AddComponent<TerrainCollider>();
            Debug.Log($"Added TerrainCollider component to {gameObject.name}");
        }
        else
        {
            Debug.Log($"TerrainCollider already exists on {gameObject.name}");
        }
    }
}