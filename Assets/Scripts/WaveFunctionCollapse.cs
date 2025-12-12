using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class WaveFunctionCollapse : MonoBehaviour
{
    // direction enum for adjacency checks
    public enum DIRECTIONS
    {
        UP = 0,
        RIGHT,
        DOWN,
        LEFT
    }



    // tile class holds pattern data and adjacencies
    public class Tile
    {
        public int tile_index;
        public int tile_hash;
        public int frequency;
        public Color[] tile_pixels;
        public Dictionary<int, List<Tile>> adjacencies;

        public Tile(int hash, int index, Color[] pixels)
        {
            tile_hash = hash;
            tile_pixels = pixels;
            frequency = 1;
            tile_index = index;

            adjacencies = new Dictionary<int, List<Tile>>();

            for (int direction = (int)DIRECTIONS.UP; direction <= (int)DIRECTIONS.LEFT; direction++)
            {
                adjacencies[direction] = new List<Tile>();
            }
        }

        // add an adjacency in a direction if not alredy there
        public void AddAdjacency(DIRECTIONS direction, Tile new_adjacency)
        {
            bool already_contains = adjacencies[(int)direction].Contains(new_adjacency);

            if (already_contains == false)
            {
                adjacencies[(int)direction].Add(new_adjacency);
            }
        }

        // increment the frequncy counter
        public void IncrementFrequency()
        {
            frequency++;
        }
    }



    // cell class for the wfc grid
    public class Cell
    {
        public int x;
        public int y;
        public int cell_index;
        public bool collapsed;
        public Tile collapsed_tile;
        public List<Tile> collapse_options;

        private WaveFunctionCollapse wfc;

        public Cell(int _x, int _y, int index, Dictionary<int, Tile> _hash_to_tile, WaveFunctionCollapse _wfc)
        {
            x = _x;
            y = _y;
            cell_index = index;
            wfc = _wfc;

            collapsed = false;
            collapsed_tile = null;

            collapse_options = new List<Tile>();

            foreach (KeyValuePair<int, Tile> pair in _hash_to_tile)
            {
                collapse_options.Add(pair.Value);
            }
        }

        // fetch a neigbor cell in a direction
        public Cell FetchNeighbor(Dictionary<Vector2Int, Cell> position_to_cell, DIRECTIONS direction)
        {
            Vector2Int position = new Vector2Int(x, y);

            if (direction == DIRECTIONS.UP)
            {
                position.y -= 1;
            }
            else if (direction == DIRECTIONS.DOWN)
            {
                position.y += 1;
            }
            else if (direction == DIRECTIONS.LEFT)
            {
                position.x -= 1;
            }
            else if (direction == DIRECTIONS.RIGHT)
            {
                position.x += 1;
            }

            return position_to_cell[position];
        }

        // reset the cell partialy for retry
        public void PartialResetCell(Dictionary<int, Tile> _hash_to_tile)
        {
            collapsed = false;
            collapsed_tile = null;

            collapse_options = new List<Tile>();

            foreach (KeyValuePair<int, Tile> pair in _hash_to_tile)
            {
                collapse_options.Add(pair.Value);
            }
        }
    }



    // cloud wfc setings
    [Header("Cloud WFC Settings")]
    [SerializeField] Texture2D cloud_bitmap;
    [SerializeField] int pattern_size = 3;
    [SerializeField] int grid_width = 20;
    [SerializeField] int grid_height = 20;

    // cloud spawn setings
    [Header("Cloud Spawn Settings")]
    [SerializeField] GameObject cloud_prefab;
    [SerializeField] float cloud_height = 50f;
    [SerializeField] float cloud_spacing = 10f;
    [SerializeField] Vector3 spawn_origin = Vector3.zero;

    // cloud apearance settings
    [Header("Cloud Appearance")]
    [SerializeField] Color cloud_color_threshold = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField, Range(0f, 1f)] float spawn_threshold = 0.5f;



    // tile variabels
    private Dictionary<int, Tile> hash_to_tile = new Dictionary<int, Tile>();
    private Dictionary<int, Tile> index_to_tile = new Dictionary<int, Tile>();

    // cell variabels
    private List<Cell> cells = new List<Cell>();
    private Dictionary<Vector2Int, Cell> position_to_cell = new Dictionary<Vector2Int, Cell>();
    private Dictionary<int, Cell> index_to_cell = new Dictionary<int, Cell>();

    // spawned clouds list
    private List<GameObject> spawned_clouds = new List<GameObject>();

    // wfc state variabels
    private int wfc_retry_count = 0;
    private bool wfc_complete = false;

    // event for when wfc is  done
    public event Action OnWFCComplete;



    void Start()
    {
        if (cloud_bitmap != null)
        {
            StartCloudWFC();
        }
        else
        {
            Debug.LogWarning("WaveFunctionCollapse: No cloud bitmap assigned!");
        }
    }



    // start the cloud wfc process
    public void StartCloudWFC()
    {
        ClearClouds();
        ClearWFCData();
        wfc_complete = false;

        LoadWFCConditions(pattern_size, cloud_bitmap);
        StepWFC();
    }

    // start wfc with a specific bitamp
    public void StartCloudWFC(Texture2D bitmap)
    {
        cloud_bitmap = bitmap;
        StartCloudWFC();
    }



    // clear all wfc data for restart
    private void ClearWFCData()
    {
        cells.Clear();
        position_to_cell.Clear();
        index_to_cell.Clear();
        hash_to_tile.Clear();
        index_to_tile.Clear();
    }



    // clear all spawned cluods
    public void ClearClouds()
    {
        foreach (GameObject cloud in spawned_clouds)
        {
            if (cloud != null)
            {
                Destroy(cloud);
            }
        }

        spawned_clouds.Clear();
    }



    // make a texture readable at runtime if its not alredy
    private Texture2D GetReadableTexture(Texture2D source)
    {
        // try to read a pixel to check if readable
        bool is_readable = true;

        try
        {
            source.GetPixel(0, 0);
        }
        catch
        {
            is_readable = false;
        }

        if (is_readable)
        {
            return source;
        }

        // create a readable copy using render texure
        RenderTexture tmp = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );

        Graphics.Blit(source, tmp);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tmp;

        Texture2D readable = new Texture2D(source.width, source.height);
        readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);

        return readable;
    }



    // load the wfc conditions from the bitmap
    private void LoadWFCConditions(int p_size, Texture2D bitmap)
    {
        Debug.Log("Starting Cloud WFC Conditions");

        // make sure texure is readable
        bitmap = GetReadableTexture(bitmap);

        int next_tile_index = 0;

        // extract all paterns from the bitmap
        for (int x = 0; x < bitmap.width; x++)
        {
            for (int y = 0; y < bitmap.height; y++)
            {
                Color[] pattern = PullPattern(bitmap, p_size, new Vector2Int(x, y));
                int p_hash = HashPixels(pattern);

                bool hash_exists = hash_to_tile.ContainsKey(p_hash);

                if (hash_exists == false)
                {
                    Tile new_tile = new Tile(p_hash, next_tile_index, pattern);
                    index_to_tile[next_tile_index] = new_tile;
                    hash_to_tile[p_hash] = new_tile;
                    next_tile_index++;
                }
                else
                {
                    hash_to_tile[p_hash].IncrementFrequency();
                }
            }
        }

        // generate adjacencies for all tiles
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            GenerateAdjacencies(bitmap, pair.Value, pattern_size);
        }

        // create all the cells in the grid
        int cell_index = 0;

        for (int x = 0; x < grid_width; x++)
        {
            for (int y = 0; y < grid_height; y++)
            {
                Cell new_cell = new Cell(x, y, cell_index, hash_to_tile, this);
                cells.Add(new_cell);

                Vector2Int position = new Vector2Int(x, y);
                position_to_cell[position] = new_cell;
                index_to_cell[cell_index] = new_cell;

                cell_index++;
            }
        }

        // collapse a random starting cell
        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);
        int tile_index = UnityEngine.Random.Range(0, next_tile_index);

        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
    }



    // step the wfc algoritm forward one cell
    private void StepWFC()
    {
        int cell_index = -1;
        int lowest_entropy = 9999;
        int collapse_count = 0;

        // find the cell with lowest entropy
        foreach (Cell cell in cells)
        {
            if (cell.collapsed == false)
            {
                if (cell.collapse_options.Count == 0)
                {
                    continue;
                }

                if (cell.collapse_options.Count < lowest_entropy)
                {
                    lowest_entropy = cell.collapse_options.Count;
                    cell_index = cell.cell_index;
                }
            }
            else
            {
                collapse_count++;
            }
        }

        // check if we are done or need to retry
        if (cell_index == -1)
        {
            if (collapse_count >= cells.Count)
            {
                wfc_retry_count = 0;
                OnWFCFinished();
            }
            else
            {
                if (wfc_retry_count >= 3)
                {
                    wfc_retry_count = 0;
                    Debug.LogWarning("WaveFunctionCollapse: Max retries reached, spawning with partial solution");
                    OnWFCFinished();
                }
                else
                {
                    wfc_retry_count++;
                    RestartWFC();
                }
            }
            return;
        }

        Cell selected_cell = index_to_cell[cell_index];

        // check for contradicion
        if (selected_cell.collapse_options.Count == 0)
        {
            Debug.LogError("Contradiction: Cell has no valid options!");
            return;
        }

        // collapse the cell to a random valid option
        int selected_option_index = UnityEngine.Random.Range(0, selected_cell.collapse_options.Count);
        Tile tile_to_collapse_cell_to = selected_cell.collapse_options[selected_option_index];
        selected_cell.collapsed_tile = tile_to_collapse_cell_to;

        selected_cell.collapse_options.Clear();
        selected_cell.collapsed = true;

        ReduceEntropy(selected_cell);

        // continue stepping
        Invoke(nameof(StepWFC), 0.0001f);
    }



    // called when wfc is finsihed
    private void OnWFCFinished()
    {
        wfc_complete = true;
        Debug.Log("WaveFunctionCollapse: Collapse complete, spawning clouds...");
        SpawnCloudsFromResult();

        if (OnWFCComplete != null)
        {
            OnWFCComplete.Invoke();
        }
    }



    // spawn cloud prefabs based on the wfc result
    private void SpawnCloudsFromResult()
    {
        int center_index = (pattern_size * pattern_size) / 2;

        int collapsed_count = 0;
        int above_threshold_count = 0;
        float min_brightness = 1f;
        float max_brightness = 0f;

        foreach (Cell cell in cells)
        {
            if (cell.collapsed == false)
            {
                continue;
            }

            if (cell.collapsed_tile == null)
            {
                continue;
            }

            collapsed_count++;

            Color center_color = cell.collapsed_tile.tile_pixels[center_index];
            float brightness = (center_color.r + center_color.g + center_color.b) / 3f;

            if (brightness < min_brightness)
            {
                min_brightness = brightness;
            }

            if (brightness > max_brightness)
            {
                max_brightness = brightness;
            }

            // spawn cloud if pixel is bright enugh
            if (brightness >= spawn_threshold)
            {
                above_threshold_count++;

                Vector3 spawn_pos = spawn_origin + new Vector3(
                    cell.x * cloud_spacing,
                    cloud_height,
                    cell.y * cloud_spacing
                );

                GameObject cloud = Instantiate(cloud_prefab, spawn_pos, Quaternion.identity, transform);

                // set cloud color based on bitmap
                CloudTile cloud_tile = cloud.GetComponent<CloudTile>();

                if (cloud_tile != null)
                {
                    cloud_tile.SetCloudColor(center_color);
                    cloud_tile.SetGridPosition(cell.x, cell.y);
                }

                spawned_clouds.Add(cloud);
            }
        }

        Debug.Log($"WaveFunctionCollapse: Collapsed cells: {collapsed_count}, Brightness range: {min_brightness:F3} to {max_brightness:F3}, Above threshold ({spawn_threshold}): {above_threshold_count}");
        Debug.Log($"WaveFunctionCollapse: Spawned {spawned_clouds.Count} clouds");
    }



    // reduce entropy of neighboring cells using constraint propogation
    private void ReduceEntropy(Cell start_cell)
    {
        Queue<Cell> cells_to_process = new Queue<Cell>();
        HashSet<int> cells_in_queue = new HashSet<int>();

        cells_to_process.Enqueue(start_cell);
        cells_in_queue.Add(start_cell.cell_index);

        while (cells_to_process.Count > 0)
        {
            Cell current = cells_to_process.Dequeue();
            cells_in_queue.Remove(current.cell_index);

            // check all four dirrections
            foreach (DIRECTIONS direction in Enum.GetValues(typeof(DIRECTIONS)))
            {
                Vector2Int neighbor_position = new Vector2Int(current.x, current.y);

                if (direction == DIRECTIONS.UP)
                {
                    neighbor_position.y -= 1;
                }
                else if (direction == DIRECTIONS.DOWN)
                {
                    neighbor_position.y += 1;
                }
                else if (direction == DIRECTIONS.LEFT)
                {
                    neighbor_position.x -= 1;
                }
                else if (direction == DIRECTIONS.RIGHT)
                {
                    neighbor_position.x += 1;
                }

                bool has_neighbor = position_to_cell.ContainsKey(neighbor_position);

                if (has_neighbor == false)
                {
                    continue;
                }

                Cell neighbor = position_to_cell[neighbor_position];

                if (neighbor.collapsed)
                {
                    continue;
                }

                // build set of valid tiles for neighbor
                HashSet<Tile> valid_tiles = new HashSet<Tile>();

                if (current.collapsed)
                {
                    foreach (Tile adj in current.collapsed_tile.adjacencies[(int)direction])
                    {
                        valid_tiles.Add(adj);
                    }
                }
                else
                {
                    foreach (Tile option in current.collapse_options)
                    {
                        foreach (Tile adj in option.adjacencies[(int)direction])
                        {
                            valid_tiles.Add(adj);
                        }
                    }
                }

                int old_collapse_count = neighbor.collapse_options.Count;

                // remove invalid options from neigbor
                neighbor.collapse_options.RemoveAll(tile => valid_tiles.Contains(tile) == false);

                // if entropy changed add neigbor to queue
                if (neighbor.collapse_options.Count < old_collapse_count)
                {
                    bool already_in_queue = cells_in_queue.Contains(neighbor.cell_index);

                    if (already_in_queue == false)
                    {
                        cells_to_process.Enqueue(neighbor);
                        cells_in_queue.Add(neighbor.cell_index);
                    }
                }
            }
        }
    }



    // generate adjacency rules for a tile
    private void GenerateAdjacencies(Texture2D bitmap, Tile tile, int pattern_size)
    {
        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            Tile other = pair.Value;

            // check up adjacency
            bool up_valid = true;

            for (int y = 0; y < pattern_size - 1; y++)
            {
                if (up_valid == false)
                {
                    break;
                }

                for (int x = 0; x < pattern_size; x++)
                {
                    int tile_index = y * pattern_size + x;
                    int other_index = (y + 1) * pattern_size + x;

                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        up_valid = false;
                        break;
                    }
                }
            }

            if (up_valid)
            {
                tile.AddAdjacency(DIRECTIONS.UP, other);
            }

            // check down adjacency
            bool down_valid = true;

            for (int y = 0; y < pattern_size - 1; y++)
            {
                if (down_valid == false)
                {
                    break;
                }

                for (int x = 0; x < pattern_size; x++)
                {
                    int tile_index = (y + 1) * pattern_size + x;
                    int other_index = y * pattern_size + x;

                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        down_valid = false;
                        break;
                    }
                }
            }

            if (down_valid)
            {
                tile.AddAdjacency(DIRECTIONS.DOWN, other);
            }

            // check left adjacency
            bool left_valid = true;

            for (int x = 0; x < pattern_size - 1; x++)
            {
                if (left_valid == false)
                {
                    break;
                }

                for (int y = 0; y < pattern_size; y++)
                {
                    int tile_index = y * pattern_size + x;
                    int other_index = y * pattern_size + (x + 1);

                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        left_valid = false;
                        break;
                    }
                }
            }

            if (left_valid)
            {
                tile.AddAdjacency(DIRECTIONS.LEFT, other);
            }

            // check right adjacency
            bool right_valid = true;

            for (int x = 0; x < pattern_size - 1; x++)
            {
                if (right_valid == false)
                {
                    break;
                }

                for (int y = 0; y < pattern_size; y++)
                {
                    int tile_index = y * pattern_size + (x + 1);
                    int other_index = y * pattern_size + x;

                    if (tile.tile_pixels[tile_index] != other.tile_pixels[other_index])
                    {
                        right_valid = false;
                        break;
                    }
                }
            }

            if (right_valid)
            {
                tile.AddAdjacency(DIRECTIONS.RIGHT, other);
            }
        }
    }



    // restart the wfc from scratch
    private void RestartWFC()
    {
        CancelInvoke(nameof(StepWFC));

        foreach (Cell cell in cells)
        {
            cell.PartialResetCell(hash_to_tile);
        }

        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);
        int tile_index = UnityEngine.Random.Range(0, index_to_tile.Count);

        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
        StepWFC();
    }



    // pull a pattern from the texture at a corner postion
    private Color[] PullPattern(Texture2D texture, int pattern_size, Vector2Int corner)
    {
        Color[] pattern = new Color[pattern_size * pattern_size];

        int pattern_index = 0;

        for (int y = 0; y < pattern_size; y++)
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



    // hash an array of pixles into a single int
    private int HashPixels(Color[] pixels)
    {
        unchecked
        {
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



    // public acessors

    public bool GetIsComplete()
    {
        return wfc_complete;
    }

    public List<GameObject> GetSpawnedClouds()
    {
        return spawned_clouds;
    }
}