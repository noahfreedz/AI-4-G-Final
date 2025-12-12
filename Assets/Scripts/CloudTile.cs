using UnityEngine;

public class CloudTile : MonoBehaviour
{
    // movment settings for the cloud drift
    [Header("Movement Settings")]
    [SerializeField] Vector3 drift_direction = new Vector3(1f, 0f, 0f);
    [SerializeField] float drift_speed = 2f;

    // tiling settings for wraping around
    [Header("Tiling Settings")]
    [SerializeField] float tile_distance = 200f;
    [SerializeField] bool auto_calculate_tile_distance = true;

    // visual setings  for opacity and scale
    [Header("Visual Settings")]
    [SerializeField] float opacity_variation = 0.1f;
    [SerializeField] float scale_variation = 0.2f;

    // spawning settings for prefab drops
    [Header("Spawning Settings")]
    [SerializeField] GameObject spawn_prefab;
    [SerializeField] float spawn_radius = 5f;
    [SerializeField] float spawn_cooldown = 20f;
    [SerializeField] float spawn_height_offset = 0f;
    [SerializeField] bool spawn_at_ground = true;
    [SerializeField] LayerMask ground_layer = ~0;
    [SerializeField] float ground_raycast_distance = 500f;

    // internal  state variables
    private Vector3 start_position;
    private int grid_x;
    private int grid_y;
    private WaveFunctionCollapse parent_wfc;
    private Renderer cloud_renderer;
    private Color base_color = Color.white;

    // spawning state for timer
    private float spawn_timer;

    // cached material propety block
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

        // randomize initial spawn timer so clouds dont all spawn at once
        spawn_timer = Random.Range(0f, spawn_cooldown);
    }



    void Update()
    {
        // move cloud in drift  direction
        transform.position += drift_direction.normalized * drift_speed * Time.deltaTime;

        CheckTiling();
        HandleSpawning();
    }



    private void HandleSpawning()
    {
        if (spawn_prefab == null)
        {
            return;
        }

        spawn_timer -= Time.deltaTime;

        if (spawn_timer <= 0f)
        {
            SpawnPrefab();
            spawn_timer = spawn_cooldown;
        }
    }



    private void SpawnPrefab()
    {
        // get random positon within radius
        Vector2 random_circle = Random.insideUnitCircle * spawn_radius;
        Vector3 spawn_pos = transform.position + new Vector3(random_circle.x, 0f, random_circle.y);

        if (spawn_at_ground)
        {
            // raycast down to find the ground
            RaycastHit hit;
            Vector3 raycast_origin = spawn_pos;
            raycast_origin.y = transform.position.y + 100f;

            bool did_hit = Physics.Raycast(raycast_origin, Vector3.down, out hit, ground_raycast_distance, ground_layer);

            if (did_hit)
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

        // project offset onto drift direction
        float distance_traveled = Vector3.Dot(offset, drift_direction.normalized);

        if (distance_traveled >= tile_distance)
        {
            // wrap cloud back to  start
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos - wrap_offset;
        }
        else if (distance_traveled <= -tile_distance)
        {
            // wrap in oposite direction in case drift direction changes
            Vector3 wrap_offset = drift_direction.normalized * tile_distance;
            transform.position = current_pos + wrap_offset;
        }
    }



    private void ApplyVariations()
    {
        // apply random opacity variaton
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

        // apply random scale  variation
        float scale_mult = 1f + Random.Range(-scale_variation, scale_variation);
        transform.localScale *= scale_mult;

        // random y rotation for varety
        transform.Rotate(0f, Random.Range(0f, 360f), 0f);
    }



    // public setters for external acess

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



    // public getters for external  acess

    public Vector3 GetDriftDirection()
    {
        return drift_direction;
    }

    public float GetDriftSpeed()
    {
        return drift_speed;
    }

    public int GetGridX()
    {
        return grid_x;
    }

    public int GetGridY()
    {
        return grid_y;
    }



    // gizmo drawing for debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawn_radius);
    }
}