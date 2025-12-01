using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class WaveFunctionCollapse : MonoBehaviour
{
    public enum DIRECTIONS
    {
        UP = 0,
        RIGHT,
        DOWN,
        LEFT
    }

    public struct Tile
    {
        public Tile(int hash, Color[] pixels)
        {
            // Set Internals
            tile_hash = hash;
            tile_pixels = pixels;
            frequency = 1;

            // Initalize Adjacency Dictionary
            adjacencies = new Dictionary<int, List<Tile>>();

            // Initialize Tile Lists
            for (int direction = (int)DIRECTIONS.UP; direction <= (int)DIRECTIONS.LEFT; direction++)
            {
                adjacencies[direction] = new List<Tile>();
            }
        }

        int tile_hash;
        int frequency;
        public Color[] tile_pixels;
        public Dictionary<int, List<Tile>> adjacencies;

        public void addAdjacency(DIRECTIONS direction, Tile new_adjacency)
        {
            if (!adjacencies[(int)direction].Contains(new_adjacency))
            {
                adjacencies[(int)direction].Add(new_adjacency);
            }
        }

        public void incrementFrequency()
        {
            frequency++;
        }
    }

    Vector2Int grid_start = new Vector2Int(0, 0);
    [SerializeField] int player_radius;

    [Header("WFC Prefabs")]
    [SerializeField] GameObject[] tile_prefabs;
    [SerializeField] GameObject tile_base_prefab;
    [SerializeField] GameObject pixel_prefab;

    [Header("WFC Settings")]
    [SerializeField] int pattern_size;

    private Dictionary<Vector2Int, GameObject> active_tiles = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<int, Tile> hash_to_tile = new Dictionary<int, Tile>();

    //private Dictionary<Vector2Int, Tile> active_tiles;

    // Start is called before the first frame update
    void Start()
    {
        LoadWFCConditions(pattern_size, "3bricks.png");
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

    private void LoadWFCConditions(int p_size, string file_name)
    {

        Debug.Log("Starting WFC Contitions");
        // Load Bitmap
        Texture2D bitmap = LoadBitmap(file_name);

        Debug.Log("BM Width: " + bitmap.width);
        Debug.Log("BM HeIght: " + bitmap.width);

        // Hashout Each Pattern
        for (int x = 0; x < bitmap.width - p_size; x++)
        {
            for (int y = 0; y < bitmap.height - p_size; y++)
            {
                Color[] pattern = PullPattern(bitmap, p_size, new Vector2Int(x, y));
                int p_hash = HashPixels(pattern);

                // Add Tile If Not Created Already
                if (!hash_to_tile.ContainsKey(p_hash))
                {
                    Tile new_tile = new Tile(p_hash, pattern);
                    hash_to_tile[p_hash] = new_tile;
                   // Debug.Log("New Got Hash: " + p_hash);

                }
                else
                {
                    hash_to_tile[p_hash].incrementFrequency();
                   // Debug.Log("Repeat Got Hash: " + p_hash);
                }
            }
            
        }
        Debug.Log("Pattern Count: " + hash_to_tile.Count);

        // Populate Adjacencies For All Tiles
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            GenerateAdjacencies(bitmap, pair.Value);
            int adjacencies = 0;
            adjacencies += pair.Value.adjacencies[(int)DIRECTIONS.UP].Count;
            adjacencies += pair.Value.adjacencies[(int)DIRECTIONS.DOWN].Count;
            adjacencies += pair.Value.adjacencies[(int)DIRECTIONS.LEFT].Count;
            adjacencies += pair.Value.adjacencies[(int)DIRECTIONS.RIGHT].Count;
            //Debug.Log("ADJACENCIES: " + adjacencies);
        }

        Debug.Log("Finished Generating Adjancencies");


        // Debug to Display Every Tile Patterns
        DisplayPatterns(bitmap.width);
    }

    private void GenerateAdjacencies(Texture2D bitmap, Tile tile)
    {
        // Loop Through All Other Tiles & Compare Them
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            // Define Requirements For Tiles To Be Overlapping
            int[] up_requirements = { 0, 1, 2, 3, 4, 5 };
            int[] right_requirements = { 1, 2, 4, 5, 7, 8 };
            int[] down_requirements = { 3, 4, 5, 6, 7, 8 };
            int[] left_requirements = { 0, 1, 2, 3, 4, 5 };

            // Get Similarities Between Tiles
            List<int> tile_similarities = CompareTiles(tile, pair.Value);

            // Check Up Requirements, Add If Satisfied
            bool up_requirements_met = true;
            foreach(int requirement in up_requirements)
            { 
                if(!tile_similarities.Contains(requirement))
                {
                    up_requirements_met = false;
                    break;
                }
            }
            if(up_requirements_met)
            {
                tile.addAdjacency(DIRECTIONS.UP, pair.Value);
            }

            // Check Right Requirements, Add If Satisfied
            bool right_requirements_met = true;
            foreach (int requirement in right_requirements)
            {
                if (!tile_similarities.Contains(requirement))
                {
                    right_requirements_met = false;
                    break;
                }
            }
            if (right_requirements_met)
            {
                tile.addAdjacency(DIRECTIONS.RIGHT, pair.Value);
            }

            // Check Down Requirements, Add If Satisfied
            bool down_requirements_met = true;
            foreach (int requirement in down_requirements)
            {
                if (!tile_similarities.Contains(requirement))
                {
                    down_requirements_met = false;
                    break;
                }
            }
            if (down_requirements_met)
            {
                tile.addAdjacency(DIRECTIONS.DOWN, pair.Value);
            }

            // Check Left Requirements, Add If Satisfied
            bool left_requirements_met = true;
            foreach (int requirement in left_requirements)
            {
                if (!tile_similarities.Contains(requirement))
                {
                    left_requirements_met = false;
                    break;
                }
            }
            if (left_requirements_met)
            {
                tile.addAdjacency(DIRECTIONS.LEFT, pair.Value);
            }
        }
    }

    private List<int> CompareTiles(Tile tile_a, Tile tile_b)
    {
        List<int> similarities = new List<int>();
        for(int i = 0; i < tile_a.tile_pixels.Length; i++)
        {
            if(tile_a.tile_pixels[i] == tile_b.tile_pixels[i])
            {
                similarities.Add(i);
            }
        }
        return similarities;
    }


    private Texture2D LoadBitmap(string file_name)
    {
        string path = "Assets/WFC_Bitmaps/" + file_name;
        byte[] bitmap_data = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bitmap_data);

        Color pixel = texture.GetPixel(0, 0);
        return texture;
    }
    private Color[] PullPattern(Texture2D texture, int pattern_size, Vector2Int corner)
    {
        Color[] pattern = new Color[9];

        // Should Make The Pattern Generated Like Reading (Left->Right | Up->Down)
        int pattern_index = 0;
        for(int y = 0; y < pattern_size; y++)
        {
            for (int x = 0; x < pattern_size; x++)
            {
                pattern[pattern_index] = texture.GetPixel(corner.x + x, corner.y + y);
                pattern_index++;
            }
        }


        return pattern;
    }
    private int HashPixels(Color[] pixels)
    {
        string pixel_string = "";
        foreach (Color pixel in pixels)
        {
            pixel_string += "R" + pixel.r + "G" + pixel.g + "B" + pixel.b + "A" + pixel.a;
        }
        //Debug.Log("PIXEL VALUE : " + pixel_string);

        unchecked
        {
            // Start With Seed
            int hash = 0x165667B1;

            foreach (var p in pixels)
            {
                hash += (byte)(p.r * 255);
                hash ^= hash << 13;
                hash += (byte)(p.g * 255);
                hash ^= hash >> 7;
                hash += (byte)(p.b * 255);
                hash ^= hash << 3;
                hash += (byte)(p.a * 255);
                hash ^= hash >> 17;
            }

            return hash;
        }
    }

    private void DisplayPatterns(int bitmap_depth)
    {
        int x = 0;
        int z = 0;

        Vector3 tile_center = new Vector3();
        tile_center.x = x;
        tile_center.z = z;

        // Loop Through All Tiles & Display Pixels Properly
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            Tile tile = pair.Value;
            int tile_x = 0;
            int tile_z = 0;

            GameObject tile_base = Instantiate(tile_base_prefab);
            tile_base.transform.position = tile_center;

            for(int i = 0; i < tile.tile_pixels.Length; i++)
            {
                GameObject pixel = Instantiate(pixel_prefab);
                Vector3 pixel_spawn = new Vector3();

                float spawn_x = (tile_center.x - 0.33f) + (0.33f*tile_x);
                float spawn_z = (tile_center.z - 0.33f) + (0.33f*tile_z);

                pixel_spawn.x = spawn_x;
                pixel_spawn.y = 0.05f;
                pixel_spawn.z = spawn_z;

                pixel.transform.position = pixel_spawn;

                Renderer p_render = pixel.GetComponent<Renderer>();
                p_render.material.color = tile.tile_pixels[i];

                tile_x++;

                if(tile_x == 3)
                {
                    tile_x = 0;
                    tile_z++;
                }
            }
            if (x > bitmap_depth)
            {
                x = 0;
                z++;
            }

            x++;

            tile_center.x = x * 1.1f;
            tile_center.y = 0;
            tile_center.z = z * 1.1f;
        }
    }
}
