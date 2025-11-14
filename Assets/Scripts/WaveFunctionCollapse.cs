using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveFunctionCollapse : MonoBehaviour
{
    Vector2Int grid_start = new Vector2Int(0, 0);
    [SerializeField] GameObject[] tile_prefabs;
    [SerializeField] int player_radius;

    private Dictionary<Vector2Int, GameObject> active_tiles = new Dictionary<Vector2Int, GameObject>();

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void SpawnTile(Vector2Int grid_pos)
    {
        int index = Random.Range(0, tile_prefabs.Length);
        GameObject tile_prefab = tile_prefabs[index];
        Vector3 worldPos = new Vector3(grid_pos.x, 0, grid_pos.y);
        GameObject tile = Instantiate(tile_prefab, worldPos, Quaternion.identity);
        active_tiles.Add(grid_pos, tile);
    }

    public void UpdateWFC(Vector3 player_position) {
        Vector2Int playerGridPos = new Vector2Int(
            Mathf.FloorToInt(player_position.x),
            Mathf.FloorToInt(player_position.z)
        );

        HashSet<Vector2Int> tilesToKeep = new HashSet<Vector2Int>();

        // Loop over a square around the player
        for (int x = -player_radius; x <= player_radius; x++)
        {
            for (int y = -player_radius; y <= player_radius; y++)
            {
                Vector2Int grid_pos = new Vector2Int(
                    playerGridPos.x + x,
                    playerGridPos.y + y
                );

                // Enforce grid starting at (0,0)
                grid_pos -= grid_start;

                // Keep track of what SHOULD exist
                tilesToKeep.Add(grid_pos);

                if (!active_tiles.ContainsKey(grid_pos))
                {
                    SpawnTile(grid_pos);
                }
            }
        }

        // Remove tiles that are outside the radius
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();

        foreach (var pair in active_tiles)
        {
            if (!tilesToKeep.Contains(pair.Key))
                tilesToRemove.Add(pair.Key);
        }

        foreach (var tile in tilesToRemove)
        {
            Destroy(active_tiles[tile]);
            active_tiles.Remove(tile);
        }
    }
}
