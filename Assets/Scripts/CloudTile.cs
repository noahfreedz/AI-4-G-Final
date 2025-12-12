using UnityEngine;

public class CloudTile : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] Vector3 drift_direction = new Vector3(1f, 0f, 0f);
    [SerializeField] float drift_speed = 2f;

    [Header("Tiling Settings")]
    [SerializeField] float tile_distance = 200f;
    [SerializeField] bool auto_calculate_tile_distance = true;

    [Header("Visual Settings")]
    [SerializeField] float opacity_variation = 0.1f;
    [SerializeField] float scale_variation = 0.2f;

    [Header("Spawning Settings")]
    [SerializeField] GameObject spawn_prefab;
    [SerializeField] float spawn_radius = 5f;
    float spawn_cooldown = 20f;
    [SerializeField] float spawn_height_offset = 0f;
    [SerializeField] bool spawn_at_ground = true;
    [SerializeField] LayerMask ground_layer = ~0;
    [SerializeField] float ground_raycast_distance = 500f;

    // Internal state
    private Vector3 start_position;
    private int grid_x;
    private int grid_y;
    private WaveFunctionCollapse parent_wfc;
    private Renderer cloud_renderer;
    private Color base_color = Color.white;

    // Spawning state
    private float spawn_timer;

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

        ApplyVariations();

        // Randomize initial spawn timer so clouds don't all spawn at once
        spawn_timer = Random.Range(0f, spawn_cooldown);
    }

    void Update()
    {
        transform.position += drift_direction.normalized * drift_speed * Time.deltaTime;

        CheckTiling();
        HandleSpawning();
    }

    private void HandleSpawning()
    {
        if (spawn_prefab == null) return;

        spawn_timer -= Time.deltaTime;

        if (spawn_timer <= 0f)
        {
            SpawnPrefab();
            spawn_timer = spawn_cooldown;
        }
    }

    private void SpawnPrefab()
    {
        // Get random position within radius
        Vector2 random_circle = Random.insideUnitCircle * spawn_radius;
        Vector3 spawn_pos = transform.position + new Vector3(random_circle.x, 0f, random_circle.y);

        if (spawn_at_ground)
        {
            // Raycast down to find ground
            RaycastHit hit;
            Vector3 raycast_origin = spawn_pos;
            raycast_origin.y = transform.position.y + 100f;

            if (Physics.Raycast(raycast_origin, Vector3.down, out hit, ground_raycast_distance, ground_layer))
            {
                spawn_pos = hit.point + Vector3.up * spawn_height_offset;
            }
            else
            {
                spawn_pos.y = transform.position.y + spawn_height_offset;
            }
        }
        else
        {
            spawn_pos.y = transform.position.y + spawn_height_offset;
        }

        Instantiate(spawn_prefab, spawn_pos, Quaternion.identity);
    }

    private void CheckTiling()
    {
        Vector3 current_pos = transform.position;
        Vector3 offset = current_pos - start_position;

        float distance_traveled = Vector3.Dot(offset, drift_direction.normalized);

        if (distance_traveled >= tile_distance)
        {
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos - wrap_offset;
        }
        else if (distance_traveled <= -tile_distance)
        {
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos + wrap_offset;
        }
    }

    private void ApplyVariations()
    {
        if (cloud_renderer != null)
        {
            float opacity = 1f - Random.Range(0f, opacity_variation);
            Color varied_color = base_color;
            varied_color.a = opacity;

            cloud_renderer.GetPropertyBlock(prop_block);
            prop_block.SetColor("_Color", varied_color);
            prop_block.SetColor("_BaseColor", varied_color);
            cloud_renderer.SetPropertyBlock(prop_block);
        }

        float scale_mult = 1f + Random.Range(-scale_variation, scale_variation);
        transform.localScale *= scale_mult;

        transform.Rotate(0f, Random.Range(0f, 360f), 0f);
    }

    // Public setters
    public void SetCloudColor(Color color)
    {
        base_color = color;

        if (cloud_renderer != null)
        {
            cloud_renderer.GetPropertyBlock(prop_block);
            prop_block.SetColor("_Color", color);
            prop_block.SetColor("_BaseColor", color);
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

    public void SetSpawnPrefab(GameObject prefab)
    {
        spawn_prefab = prefab;
    }

    public void SetSpawnCooldown(float cooldown)
    {
        spawn_cooldown = cooldown;
    }

    public void SetSpawnRadius(float radius)
    {
        spawn_radius = radius;
    }

    // Public getters
    public Vector3 GetDriftDirection() => drift_direction;
    public float GetDriftSpeed() => drift_speed;
    public int GetGridX() => grid_x;
    public int GetGridY() => grid_y;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawn_radius);
    }
}