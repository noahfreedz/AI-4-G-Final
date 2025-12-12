using UnityEngine;

public class CloudTile : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] Vector3 drift_direction = new Vector3(1f, 0f, 0f);
    [SerializeField] float drift_speed = 2f;

    [Header("Tiling Settings")]
    [SerializeField] float tile_distance = 200f;  // Distance before cloud wraps
    [SerializeField] bool auto_calculate_tile_distance = true;

    [Header("Visual Settings")]
    [SerializeField] float opacity_variation = 0.1f;
    [SerializeField] float scale_variation = 0.2f;

    // Internal state
    private Vector3 start_position;
    private int grid_x;
    private int grid_y;
    private WaveFunctionCollapse parent_wfc;
    private Renderer cloud_renderer;
    private Color base_color = Color.white;

    // Cached material property
    private MaterialPropertyBlock prop_block;

    void Awake()
    {
        cloud_renderer = GetComponent<Renderer>();
        if (cloud_renderer == null)
        {
            cloud_renderer = GetComponentInChildren<Renderer>();
        }

        prop_block = new MaterialPropertyBlock();
    }

    void Start()
    {
        start_position = transform.position;
        parent_wfc = GetComponentInParent<WaveFunctionCollapse>();

        // Apply random variations
        ApplyVariations();

        // Calculate tile distance if needed
        if (auto_calculate_tile_distance && parent_wfc != null)
        {
            // Will be set by CloudWFC based on grid size
        }
    }

    void Update()
    {
        // Move cloud in drift direction
        transform.position += drift_direction.normalized * drift_speed * Time.deltaTime;

        // Check if cloud needs to tile/wrap
        CheckTiling();
    }

    private void CheckTiling()
    {
        Vector3 current_pos = transform.position;
        Vector3 offset = current_pos - start_position;

        // Project offset onto drift direction
        float distance_traveled = Vector3.Dot(offset, drift_direction.normalized);

        if (distance_traveled >= tile_distance)
        {
            // Wrap cloud back to start
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos - wrap_offset;
        }
        else if (distance_traveled <= -tile_distance)
        {
            // Wrap in opposite direction (in case drift direction changes)
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos + wrap_offset;
        }
    }

    private void ApplyVariations()
    {
        // Random opacity variation
        if (cloud_renderer != null)
        {
            float opacity = 1f - Random.Range(0f, opacity_variation);
            Color varied_color = base_color;
            varied_color.a = opacity;

            cloud_renderer.GetPropertyBlock(prop_block);
            prop_block.SetColor("_Color", varied_color);
            prop_block.SetColor("_BaseColor", varied_color); // For URP/HDRP
            cloud_renderer.SetPropertyBlock(prop_block);
        }

        // Random scale variation
        float scale_mult = 1f + Random.Range(-scale_variation, scale_variation);
        transform.localScale *= scale_mult;

        // Random Y rotation for variety
        transform.Rotate(0f, Random.Range(0f, 360f), 0f);
    }

    // Public setters called by CloudWFC
    public void SetCloudColor(Color color)
    {
        base_color = color;

        if (cloud_renderer != null)
        {
            cloud_renderer.GetPropertyBlock(prop_block);
            prop_block.SetColor("_Color", color);
            prop_block.SetColor("_BaseColor", color); // For URP/HDRP
            cloud_renderer.SetPropertyBlock(prop_block);
        }
    }

    public void SetGridPosition(int x, int y)
    {
        grid_x = x;
        grid_y = y;
    }

    public void SetDriftDirection(Vector3 direction)
    {
        drift_direction = direction.normalized;
    }

    public void SetDriftSpeed(float speed)
    {
        drift_speed = speed;
    }

    public void SetTileDistance(float distance)
    {
        tile_distance = distance;
        auto_calculate_tile_distance = false;
    }

    // Public getters
    public Vector3 GetDriftDirection() => drift_direction;
    public float GetDriftSpeed() => drift_speed;
    public int GetGridX() => grid_x;
    public int GetGridY() => grid_y;
}