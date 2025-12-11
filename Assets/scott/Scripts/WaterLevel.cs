using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaterLevel : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Water Settings")]
    [Tooltip("0 = lowest point on terrain, 1 = highest point actually used on terrain")]
    [Range(0f, 1f)]
    public float waterLevel = 0.15f;

    [Header("Water Plane")]
    [Tooltip("Material for the water plane (assign a blue material)")]
    public Material waterMaterial;

    [Tooltip("If true, water plane height will update when you change waterLevel in the inspector")]
    public bool dynamicHeight = true;

    private GameObject waterPlane;

    // Cached water height so we don't recompute all the time
    private float cachedWaterHeight = float.NaN;
    private float minTerrainHeight = 0f;
    private float maxTerrainHeight = 100f;

    void Start()
    {
        RecalculateWaterHeight();
        CreateWaterPlane();
    }

    void OnValidate()
    {
        // Whenever you tweak values in the inspector, invalidate cache
        cachedWaterHeight = float.NaN;
    }

    /// <summary>
    /// Creates or recreates the water plane at the correct height.
    /// </summary>
    public void CreateWaterPlane()
    {
        if (terrain == null)
        {
            Debug.LogError("Assign a Terrain first.");
            return;
        }

        // Remove existing water plane if it exists
        if (waterPlane != null)
        {
            DestroyImmediate(waterPlane);
        }

        // Make sure we have a correct water height
        RecalculateWaterHeight();

        // Create the water plane
        waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterPlane.name = "Water Plane";

        waterPlane.transform.SetParent(transform, true);

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        float waterHeight = GetWaterHeight();

        // Position plane centered over the terrain
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
            waterPlane.AddComponent<WaterSettings>();
            waterPlane.GetComponent<Renderer>().material = simpleMaterial;
        }

        Debug.Log($"Water plane created at world height: {waterHeight} (normalized waterLevel: {waterLevel})");
    }

    public void RemoveWaterPlane()
    {
        if (waterPlane != null)
        {
            DestroyImmediate(waterPlane);
            waterPlane = null;
            Debug.Log("Water plane removed.");
        }
    }

    /// <summary>
    /// Returns true if the given world position is below the water surface.
    /// </summary>
    public bool IsUnderwater(Vector3 worldPosition)
    {
        if (terrain == null) return false;

        float waterHeight = GetWaterHeight();
        return worldPosition.y < waterHeight;
    }

    /// <summary>
    /// Get the world-space Y height of the water surface.
    /// </summary>
    public float GetWaterHeight()
    {
        if (float.IsNaN(cachedWaterHeight))
        {
            RecalculateWaterHeight();
        }
        return cachedWaterHeight;
    }

    /// <summary>
    /// Set the water height directly in world space units.
    /// This will update the normalized waterLevel value accordingly.
    /// </summary>
    public void SetWaterHeight(float worldHeight)
    {
        if (terrain == null)
        {
            Debug.LogWarning("Cannot set water height: terrain not assigned");
            return;
        }

        // Make sure we have current min/max values
        RecalculateTerrainHeightRange();

        // Convert world height to normalized 0-1 range
        if (maxTerrainHeight > minTerrainHeight)
        {
            waterLevel = Mathf.InverseLerp(minTerrainHeight, maxTerrainHeight, worldHeight);
            waterLevel = Mathf.Clamp01(waterLevel);
        }
        else
        {
            waterLevel = 0.5f;
        }

        // Update cached height
        cachedWaterHeight = worldHeight;

        // Update water plane position if it exists
        UpdateWaterPlanePosition();
    }

    /// <summary>
    /// Update the water plane's Y position to match current water height
    /// </summary>
    private void UpdateWaterPlanePosition()
    {
        if (waterPlane != null)
        {
            Vector3 pos = waterPlane.transform.position;
            pos.y = GetWaterHeight();
            waterPlane.transform.position = pos;
        }
    }

    /// <summary>
    /// Recalculate water height based on actual sampled terrain heights.
    /// This accounts for however you've sculpted the terrain, not just size.y.
    /// </summary>
    private void RecalculateWaterHeight()
    {
        if (terrain == null)
        {
            cachedWaterHeight = 0f;
            return;
        }

        RecalculateTerrainHeightRange();

        if (!float.IsFinite(minTerrainHeight) || !float.IsFinite(maxTerrainHeight))
        {
            cachedWaterHeight = terrain.transform.position.y;
            return;
        }

        // Now waterLevel = 0 means minHeight, 1 means maxHeight
        cachedWaterHeight = Mathf.Lerp(minTerrainHeight, maxTerrainHeight, waterLevel);
    }

    /// <summary>
    /// Sample the terrain to find min and max heights
    /// </summary>
    private void RecalculateTerrainHeightRange()
    {
        if (terrain == null) return;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Sample across the terrain to find the min and max *actual* heights
        const int samplesPerAxis = 64; // can tweak if you want more/less precision
        minTerrainHeight = float.PositiveInfinity;
        maxTerrainHeight = float.NegativeInfinity;

        for (int z = 0; z < samplesPerAxis; z++)
        {
            float tz = (float)z / (samplesPerAxis - 1);
            for (int x = 0; x < samplesPerAxis; x++)
            {
                float tx = (float)x / (samplesPerAxis - 1);

                // World-space sample position on the terrain
                float worldX = terrainPos.x + tx * terrainData.size.x;
                float worldZ = terrainPos.z + tz * terrainData.size.z;

                float h = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
                if (h < minTerrainHeight) minTerrainHeight = h;
                if (h > maxTerrainHeight) maxTerrainHeight = h;
            }
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        // In editor, if you slide waterLevel and want the plane to move live
        if (dynamicHeight && !Application.isPlaying)
        {
            RecalculateWaterHeight();
            UpdateWaterPlanePosition();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (terrain == null) return;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        float waterHeight = GetWaterHeight();

        // Draw water level as a transparent blue box
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