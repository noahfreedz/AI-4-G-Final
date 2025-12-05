using System;

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

    public class Tile
    {
        public Tile(int hash, int index, Color[] pixels)
        {
            // Set Internals
            tile_hash = hash;
            tile_pixels = pixels;
            frequency = 1;
            tile_index = index;

            // Initalize Adjacency Dictionary
            adjacencies = new Dictionary<int, List<Tile>>();

            // Initialize Tile Lists
            for (int direction = (int)DIRECTIONS.UP; direction <= (int)DIRECTIONS.LEFT; direction++)
            {
                adjacencies[direction] = new List<Tile>();
            }
        }

        public int tile_index;
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

    public class Cell
    {
        public Cell(int _x, int _y, int index, Dictionary<int, Tile> _hash_to_tile, GameObject cell_pixel_prefab)
        {
            // Define X, Y, & Index
            x = _x;
            y = _y;
            cell_index = index;

            // Start Uncollapsed
            collapsed = false;
            collapsed_tile = null;

            // Define All Possible Options
            collapse_options = new List<Tile>();
            foreach (KeyValuePair<int, Tile> pair in _hash_to_tile)
            {
                collapse_options.Add(pair.Value);
            }

            // Create Base To Show Its A Tile
            Vector3 cell_base_position = new Vector3(_x, 0, _y);
            pixel = Instantiate(cell_pixel_prefab, cell_base_position, Quaternion.identity);
        }

        public void UpdateCell(Color cell_color)
        {
            Renderer p_render = pixel.GetComponent<Renderer>();
            p_render.material.color = cell_color;
        }

        public Cell FetchNeighbor(Dictionary<Vector2Int, Cell> position_to_cell, DIRECTIONS direction)
        {
            Vector2Int position = new Vector2Int(x, y);

            switch(direction)
            {
                case DIRECTIONS.UP:
                    position.y -= 1;
                    break;
                case DIRECTIONS.DOWN:
                    position.y += 1;
                    break;
                case DIRECTIONS.LEFT:
                    position.x -= 1;
                    break;
                case DIRECTIONS.RIGHT:
                    position.x += 1;
                    break;
            }

            return position_to_cell[position];
        }

        public void PartialResetCell(Dictionary<int, Tile> _hash_to_tile)
        {
            collapsed = false;
            collapsed_tile = null;
            UpdateCell(new Color(0.236f, 0.191f, 0.191f, 1.000f));

            // Define All Possible Options
            collapse_options = new List<Tile>();
            foreach (KeyValuePair<int, Tile> pair in _hash_to_tile)
            {
                collapse_options.Add(pair.Value);
            }

        }
        public int x, y, cell_index;
        public bool collapsed;
        public Tile? collapsed_tile;
        public List<Tile> collapse_options;
        public GameObject pixel;
    }

    Vector2Int grid_start = new Vector2Int(0, 0);
    [SerializeField] int player_radius;

    [Header("WFC Prefabs")]
    [SerializeField] GameObject[] tile_prefabs;
    [SerializeField] public GameObject tile_base_prefab;
    [SerializeField] public GameObject cell_base_prefab;
    [SerializeField] GameObject pixel_prefab;

    [Header("WFC Settings")]
    [SerializeField] int pattern_size;
    [SerializeField] int cell_grid_size;

    // Tile Variables
    private Dictionary<Vector2Int, GameObject> active_tiles = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<int, Tile> hash_to_tile = new Dictionary<int, Tile>();
    private Dictionary<int, Tile> index_to_tile = new Dictionary<int, Tile>();

    // Cell Variables
    private List<Cell> cells = new List<Cell>();
    private Dictionary<Vector2Int, Cell> position_to_cell = new Dictionary<Vector2Int, Cell>();
    private Dictionary<int, Cell> index_to_cell = new Dictionary<int, Cell>();

    private String[] wfc_files = {"3Bricks.png", "Cat.png", "Cats.png" , "Cave.png", "Chess.png", "Circle.png", "City.png",
    "ColoredCity.png", "Disk.png", "Dungeon.png", "Fabric.png", "Flowers.png", "Forest.png", "Hogs.png"};
    private int wfc_index = 0;
    private int wfc_retry_count = 0;
    // Start is called before the first frame update
    void Start()
    {
        LoadWFCConditions(pattern_size, "3Bricks.png");
    }

    // Update is called once per frame
    void Update() 
    {

    }

    private void SpawnTile(Vector2Int grid_pos)
    {
        int index = UnityEngine.Random.Range(0, tile_prefabs.Length);
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
        UnityEngine.Debug.Log("Starting WFC Contitions");

        // Load Bitmap
        Texture2D bitmap = LoadBitmap(file_name);

        int next_tile_index = 0;

        // Hashout Each Pattern
        for (int x = 0; x < bitmap.width; x++)
        {
            for (int y = 0; y < bitmap.height; y++)
            {
                Color[] pattern = PullPattern(bitmap, p_size, new Vector2Int(x, y));
                int p_hash = HashPixels(pattern);

                // Add Tile If Not Created Already
                if (!hash_to_tile.ContainsKey(p_hash))
                {
                    Tile new_tile = new Tile(p_hash, next_tile_index, pattern);
                    index_to_tile[next_tile_index] = new_tile;
                    hash_to_tile[p_hash] = new_tile;
                    next_tile_index++;
                }
                else
                {
                    hash_to_tile[p_hash].incrementFrequency();
                }
            }
        }

        // Populate Adjacencies For All Tiles
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            GenerateAdjacencies(bitmap, pair.Value, pattern_size);
        }

        // Define Index
        int cell_index = 0;
        // Create Cell Grid
        for(int x = 0; x < cell_grid_size; x++)
        {
            for (int y = 0; y < cell_grid_size; y++)
            {
                // Create Cell
                Cell new_cell = new Cell(x, y, cell_index, hash_to_tile, cell_base_prefab);
                cells.Add(new_cell);

                // Add To "Position To Cell" Dictionary
                Vector2Int position = new Vector2Int(x, y);
                position_to_cell[position] = new_cell;
                index_to_cell[cell_index] = new_cell;

                // Increment Cell Index 
                cell_index++;
            }
        }

        // Pick A Random Cell
        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);

        // Pick A Random Tile
        int tile_index = UnityEngine.Random.Range(0, next_tile_index);

        // Calculate Center Index
        int center_index = (pattern_size * pattern_size) / 2;

        // Get Tiles Center Color 
        Color tile_center_color = index_to_tile[tile_index].tile_pixels[center_index];

        // Populate Cell & Reduce Entropy
        cells[random_cell_index].UpdateCell(tile_center_color);
        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
        StepWFC();

        // Debug to Display Every Tile Patterns
        //DisplayPatterns(bitmap.width, p_size);
    }

    private void StepWFC()
    {
        int cell_index = -1;
        int lowest_entropy = 9999;
        int collapse_count = 0;
        // Sort Through To Find Lowest
        foreach(Cell cell in cells)
        {
            if (cell.collapsed == false)
            {
                // Skip cells with no options (contradictions)
                if (cell.collapse_options.Count == 0)
                {
                    continue;
                }

                if (cell.collapse_options.Count < lowest_entropy)
                {
                    // Set Lowest Entropy & Index Of Said Lowest Entropy
                    lowest_entropy = cell.collapse_options.Count;
                    cell_index = cell.cell_index;
                }
            } else
            {
                collapse_count++;
            }
        }

        if (cell_index == -1)
        {
            UnityEngine.Debug.Log("No valid cell left to collapse.");
            if(collapse_count >= cells.Count)
            {
                wfc_retry_count = 0;
                StartNextWFC();
            }
            else
            {
                if(wfc_retry_count >= 3)
                {
                    wfc_retry_count = 0;
                    StartNextWFC();
                }
                else
                {
                    wfc_retry_count++;
                    RestartWFC();
                }
            }
            return;
        }

        // Define Selected Cell & Tile Chosen
        Cell selected_cell = index_to_cell[cell_index];

        if (selected_cell.collapse_options.Count == 0)
        {
            UnityEngine.Debug.LogError("Contradiction: Cell has no valid options!");
            return;
        }

        int selected_option_index = UnityEngine.Random.Range(0, selected_cell.collapse_options.Count);
        Tile tile_to_collapse_cell_to = selected_cell.collapse_options[selected_option_index];
        selected_cell.collapsed_tile = tile_to_collapse_cell_to;

        // Update Tile Color
        int center_index = (pattern_size * pattern_size) / 2;
        Color tile_center_color = selected_cell.collapsed_tile.tile_pixels[center_index];
        selected_cell.UpdateCell(tile_center_color);

        // Wipe Options & Collapse Tile
        selected_cell.collapse_options.Clear();
        selected_cell.collapsed = true;

        ReduceEntropy(selected_cell);

        Invoke(nameof(StepWFC), 0.0001f);
    }

    private void ReduceEntropy(Cell startCell)
    {
        Queue<Cell> toProcess = new Queue<Cell>();
        HashSet<int> inQueue = new HashSet<int>();

        toProcess.Enqueue(startCell);
        inQueue.Add(startCell.cell_index);

        while (toProcess.Count > 0)
        {
            Cell current = toProcess.Dequeue();
            inQueue.Remove(current.cell_index);

            foreach (DIRECTIONS direction in Enum.GetValues(typeof(DIRECTIONS)))
            {
                // Calculate Neighbor Position
                Vector2Int neighborPos = new Vector2Int(current.x, current.y);
                switch (direction)
                {
                    case DIRECTIONS.UP: neighborPos.y -= 1; break;
                    case DIRECTIONS.DOWN: neighborPos.y += 1; break;
                    case DIRECTIONS.LEFT: neighborPos.x -= 1; break;
                    case DIRECTIONS.RIGHT: neighborPos.x += 1; break;
                }

                // Skip if neighbor is out of bounds
                if (!position_to_cell.ContainsKey(neighborPos))
                {
                    continue;
                }

                Cell neighbor = position_to_cell[neighborPos];

                // Skip if already collapsed
                if (neighbor.collapsed)
                {
                    continue;
                }

                // Build set of valid tiles for this neighbor based on current cell's possibilities
                HashSet<Tile> validTiles = new HashSet<Tile>();

                if (current.collapsed)
                {
                    // Current cell is collapsed - use its tile's adjacencies
                    foreach (Tile adj in current.collapsed_tile.adjacencies[(int)direction])
                    {
                        validTiles.Add(adj);
                    }
                }
                else
                {
                    // Current cell not collapsed - union of all possible adjacencies
                    foreach (Tile option in current.collapse_options)
                    {
                        foreach (Tile adj in option.adjacencies[(int)direction])
                        {
                            validTiles.Add(adj);
                        }
                    }
                }

                // Track count before removal
                int previousCount = neighbor.collapse_options.Count;

                // Remove invalid options
                neighbor.collapse_options.RemoveAll(tile => !validTiles.Contains(tile));

                // If options were reduced, add neighbor to queue (if not already there)
                if (neighbor.collapse_options.Count < previousCount)
                {
                    if (!inQueue.Contains(neighbor.cell_index))
                    {
                        toProcess.Enqueue(neighbor);
                        inQueue.Add(neighbor.cell_index);
                    }
                }
            }
        }
    }

    private void GenerateAdjacencies(Texture2D bitmap, Tile tile, int pattern_size)
    {
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            Tile other = pair.Value;

            // Check UP: tile's top (pattern_size-1) rows must match other's bottom (pattern_size-1) rows
            bool up_valid = true;
            for (int y = 0; y < pattern_size - 1 && up_valid; y++)
            {
                for (int x = 0; x < pattern_size; x++)
                {
                    int tile_index = y * pattern_size + x;           // rows 0, 1 of tile
                    int other_index = (y + 1) * pattern_size + x;    // rows 1, 2 of other
                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        up_valid = false;
                        break;
                    }
                }
            }
            if (up_valid) tile.addAdjacency(DIRECTIONS.UP, other);

            // Check DOWN: tile's bottom (pattern_size-1) rows must match other's top (pattern_size-1) rows
            bool down_valid = true;
            for (int y = 0; y < pattern_size - 1 && down_valid; y++)
            {
                for (int x = 0; x < pattern_size; x++)
                {
                    int tile_index = (y + 1) * pattern_size + x;     // rows 1, 2 of tile
                    int other_index = y * pattern_size + x;          // rows 0, 1 of other
                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        down_valid = false;
                        break;
                    }
                }
            }
            if (down_valid) tile.addAdjacency(DIRECTIONS.DOWN, other);

            // Check LEFT: tile's left (pattern_size-1) columns must match other's right (pattern_size-1) columns
            bool left_valid = true;
            for (int x = 0; x < pattern_size - 1 && left_valid; x++)
            {
                for (int y = 0; y < pattern_size; y++)
                {
                    int tile_index = y * pattern_size + x;           // cols 0, 1 of tile
                    int other_index = y * pattern_size + (x + 1);    // cols 1, 2 of other
                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        left_valid = false;
                        break;
                    }
                }
            }
            if (left_valid) tile.addAdjacency(DIRECTIONS.LEFT, other);

            // Check RIGHT: tile's right (pattern_size-1) columns must match other's left (pattern_size-1) columns
            bool right_valid = true;
            for (int x = 0; x < pattern_size - 1 && right_valid; x++)
            {
                for (int y = 0; y < pattern_size; y++)
                {
                    int tile_index = y * pattern_size + (x + 1);     // cols 1, 2 of tile
                    int other_index = y * pattern_size + x;          // cols 0, 1 of other
                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        right_valid = false;
                        break;
                    }
                }
            }
            if (right_valid) tile.addAdjacency(DIRECTIONS.RIGHT, other);
        }
    }
    
    private void RestartWFC()
    {
        // Cancel Invoke Incase Its Running
        CancelInvoke(nameof(StepWFC));

        foreach (Cell cell in cells)
        {
            cell.PartialResetCell(hash_to_tile);
        }

        // Pick A Random Cell
        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);

        // Pick A Random Tile
        int tile_index = UnityEngine.Random.Range(0, index_to_tile.Count);

        // Calculate Center Index
        int center_index = (pattern_size * pattern_size) / 2;

        // Get Tiles Center Color 
        Color tile_center_color = index_to_tile[tile_index].tile_pixels[center_index];

        // Populate Cell & Reduce Entropy
        cells[random_cell_index].UpdateCell(tile_center_color);
        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
        StepWFC();
    }

    private void StartNextWFC()
    {
        // Cancel Invoke If It's Trying To Run
        CancelInvoke(nameof(StepWFC));

        // Destroy all cell GameObjects first
        foreach (Cell cell in cells)
        {
            if (cell.pixel != null)
            {
                Destroy(cell.pixel);
            }
        }

        // Now clear the lists
        cells.Clear();
        position_to_cell.Clear();
        index_to_cell.Clear();

        // Reset Tile Stuffs
        active_tiles.Clear();
        hash_to_tile.Clear();
        index_to_tile.Clear();

        wfc_index++;
        if (wfc_index >= wfc_files.Length)
        {
            UnityEngine.Debug.Log("Finished all WFC files!");
            return;
        }

        LoadWFCConditions(pattern_size, wfc_files[wfc_index]);
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
        Color[] pattern = new Color[pattern_size * pattern_size];

       // Should Make The Pattern Generated Like Reading (Left->Right | Up->Down)
        int pattern_index = 0;
        for(int y = 0; y < pattern_size; y++)
        {
            for (int x = 0; x < pattern_size; x++)
            {
                int final_x = corner.x + x;
                int final_y = corner.y + y;

                pattern[pattern_index] = texture.GetPixel(final_x % texture.width, final_y % texture.height);
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

    private void DisplayPatterns(int bitmap_depth, int pattern_size)
    {
        int x = 0;
        int z = 0;

        Vector3 tile_center = new Vector3();
        tile_center.x = x;
        tile_center.z = z;

        float pixel_size = 0.33f;
        float pattern_depth = pixel_size * pattern_size;

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

                float spawn_x = (tile_center.x - pattern_depth / 2 + pixel_size/2) + (pixel_size * tile_x);
                float spawn_z = (tile_center.z - pattern_depth / 2 + pixel_size/2) + (pixel_size * tile_z);

                pixel_spawn.x = spawn_x;
                pixel_spawn.y = 0.05f;
                pixel_spawn.z = spawn_z;

                pixel.transform.position = pixel_spawn;

                Renderer p_render = pixel.GetComponent<Renderer>();
                p_render.material.color = tile.tile_pixels[i];

                tile_x++;

                if(tile_x == pattern_size)
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

            tile_center.x = x * (pattern_depth*1.1f);
            tile_center.y = 0;
            tile_center.z = z * (pattern_depth * 1.1f);
        }
    }
}
