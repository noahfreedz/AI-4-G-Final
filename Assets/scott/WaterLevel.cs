using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaterLevel : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Water Settings")]
    [Tooltip("Water level threshold (0-1). Elevation below this = underwater")]
    [Range(0f, 1f)]
    public float waterLevel = 0.15f;

    [Header("Water Plane")]
    [Tooltip("Material for the water plane (assign a blue material)")]
    public Material waterMaterial;

    [Tooltip("Offset water plane slightly above calculated height to prevent z-fighting")]
    [Range(0f, 5f)]
    public float waterOffset = 0.5f;

    private GameObject waterPlane;

    void Start()
    {
        CreateWaterPlane();
    }

    public void CreateWaterPlane()
    {
        if (terrain == null)
        {
            //Debug.LogError("Assign a Terrain first.");
            return;
        }

        // Remove existing water plane if it exists
        if (waterPlane != null)
        {
            Destroy(waterPlane);
        }

        // Create the water plane
        waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterPlane.name = "Water Plane";
        waterPlane.transform.SetParent(transform);

        // Calculate water height in world units
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        float waterHeight = terrainPos.y + (waterLevel * terrainData.size.y) + waterOffset;

        // Position and scale the plane
        Vector3 terrainSize = terrainData.size;
        waterPlane.transform.position = new Vector3(
            terrainPos.x + terrainSize.x / 2f,
            waterHeight,
            terrainPos.z + terrainSize.z / 2f
        );

        // Scale plane to match terrain (Unity plane is 10x10 by default)
        waterPlane.transform.localScale = new Vector3(
            terrainSize.x / 10f,
            1f,
            terrainSize.z / 10f
        );

        // Apply material
        if (waterMaterial != null)
        {
            waterPlane.GetComponent<Renderer>().material = waterMaterial;
        }
        else
        {
            // Create simple blue material if none assigned
            Material simpleMaterial = new Material(Shader.Find("Standard"));
            simpleMaterial.color = new Color(0.2f, 0.5f, 0.8f, 0.7f);
            simpleMaterial.SetFloat("_Mode", 3); // Transparent mode
            simpleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            simpleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            simpleMaterial.SetInt("_ZWrite", 0);
            simpleMaterial.DisableKeyword("_ALPHATEST_ON");
            simpleMaterial.EnableKeyword("_ALPHABLEND_ON");
            simpleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            simpleMaterial.renderQueue = 3000;

            waterPlane.GetComponent<Renderer>().material = simpleMaterial;
        }

        Debug.Log($"Water plane created at height: {waterHeight} (water level: {waterLevel})");
    }

    public void RemoveWaterPlane()
    {
        if (waterPlane != null)
        {
            Destroy(waterPlane);
            Debug.Log("Water plane removed.");
        }
    }

    public bool IsUnderwater(Vector3 worldPosition)
    {
        if (terrain == null) return false;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Get terrain height at this world position
        Vector3 localPos = worldPosition - terrainPos;
        float normalizedX = localPos.x / terrainData.size.x;
        float normalizedZ = localPos.z / terrainData.size.z;

        if (normalizedX < 0 || normalizedX > 1 || normalizedZ < 0 || normalizedZ > 1)
            return false;

        float terrainHeight = terrain.SampleHeight(worldPosition);
        float elevation = terrainHeight / terrainData.size.y;

        return elevation < waterLevel;
    }

    public float GetWaterHeight()
    {
        if (terrain == null) return 0f;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        return terrainPos.y + (waterLevel * terrainData.size.y);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (terrain == null) return;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        // Draw water level as a transparent blue box
        float waterHeight = terrainPos.y + (waterLevel * terrainSize.y);

        Gizmos.color = new Color(0.2f, 0.5f, 0.8f, 0.3f);
        Vector3 center = new Vector3(
            terrainPos.x + terrainSize.x / 2f,
            waterHeight,
            terrainPos.z + terrainSize.z / 2f
        );
        Vector3 size = new Vector3(terrainSize.x, 0.1f, terrainSize.z);
        Gizmos.DrawCube(center, size);

        // Draw outline
        Gizmos.color = new Color(0.2f, 0.5f, 0.8f, 1f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}