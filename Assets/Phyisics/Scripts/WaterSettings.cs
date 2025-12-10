using UnityEngine;

public class WaterSettings : MonoBehaviour
{
    [Header("Water Properties")]
    [Tooltip("Liquid density in kg/m³ (water = 1000, lower values = less buoyant)")]
    [SerializeField] private float waterDensity = 500f; 

    [Tooltip("Linear drag coefficient (higher = more resistance)")]
    [SerializeField] private float waterDrag = 1.0f;

    [Tooltip("Angular drag coefficient for rotation")]
    [SerializeField] private float waterAngularDrag = 0.5f;

    [Header("Water Surface")]
    [Tooltip("The transform that represents the water surface (typically a plane)")]
    [SerializeField] private Transform waterSurface;

    private float waterHeight;

    void Awake()
    {
        waterSurface = this.transform;
    }

    public float GetWaterHeight()
    {
        return waterHeight;
    }

    public float GetWaterDensity()
    {
        return waterDensity;
    }

    public float GetWaterDrag()
    {
        return waterDrag;
    }

    public float GetWaterAngularDrag()
    {
        return waterAngularDrag;
    }

    public bool HasWaterSurface()
    {
        return waterSurface != null;
    }

    void LateUpdate()
    {
        if (waterSurface != null)
        {
            waterHeight = waterSurface.position.y;
        }
    }

    void OnDrawGizmos()
    {
        if (waterSurface != null)
        {
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
            Gizmos.DrawCube(waterSurface.position, new Vector3(20, 0.1f, 20));

            Gizmos.color = new Color(0, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(waterSurface.position, new Vector3(20, 0.05f, 20));
        }
    }
}