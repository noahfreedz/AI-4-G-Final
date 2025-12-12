using System;
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

    public class Tile
    {
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

        public Cell FetchNeighbor(Dictionary<Vector2Int, Cell> position_to_cell, DIRECTIONS direction)
        {
            Vector2Int position = new Vector2Int(x, y);

            switch (direction)
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

            collapse_options = new List<Tile>();
            foreach (KeyValuePair<int, Tile> pair in _hash_to_tile)
            {
                collapse_options.Add(pair.Value);
            }
        }

        public int x, y, cell_index;
        public bool collapsed;
        public Tile collapsed_tile;
        public List<Tile> collapse_options;
    }

    [Header("Cloud WFC Settings")]
    [SerializeField] Texture2D cloud_bitmap;
    [SerializeField] int pattern_size = 3;
    [SerializeField] int grid_width = 20;
    [SerializeField] int grid_height = 20;

    [Header("Cloud Spawn Settings")]
    [SerializeField] GameObject cloud_prefab;
    [SerializeField] float cloud_height = 50f;
    [SerializeField] float cloud_spacing = 10f;
    [SerializeField] Vector3 spawn_origin = Vector3.zero;

    [Header("Cloud Appearance")]
    [SerializeField] Color cloud_color_threshold = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField, Range(0f, 1f)] float spawn_threshold = 0.5f;

    // Tile Variables
    private Dictionary<int, Tile> hash_to_tile = new Dictionary<int, Tile>();
    private Dictionary<int, Tile> index_to_tile = new Dictionary<int, Tile>();

    // Cell Variables
    private List<Cell> cells = new List<Cell>();
    private Dictionary<Vector2Int, Cell> position_to_cell = new Dictionary<Vector2Int, Cell>();
    private Dictionary<int, Cell> index_to_cell = new Dictionary<int, Cell>();

    // Spawned clouds
    private List<GameObject> spawned_clouds = new List<GameObject>();

    private int wfc_retry_count = 0;
    private bool wfc_complete = false;

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

    public void StartCloudWFC()
    {
        ClearClouds();
        ClearWFCData();
        wfc_complete = false;

        LoadWFCConditions(pattern_size, cloud_bitmap);
        StepWFC();
    }

    public void StartCloudWFC(Texture2D bitmap)
    {
        cloud_bitmap = bitmap;
        StartCloudWFC();
    }

    private void ClearWFCData()
    {
        cells.Clear();
        position_to_cell.Clear();
        index_to_cell.Clear();
        hash_to_tile.Clear();
        index_to_tile.Clear();
    }

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

    private void LoadWFCConditions(int p_size, Texture2D bitmap)
    {
        Debug.Log("Starting Cloud WFC Conditions");

        int next_tile_index = 0;

        for (int x = 0; x < bitmap.width; x++)
        {
            for (int y = 0; y < bitmap.height; y++)
            {
                Color[] pattern = PullPattern(bitmap, p_size, new Vector2Int(x, y));
                int p_hash = HashPixels(pattern);

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

        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            GenerateAdjacencies(bitmap, pair.Value, pattern_size);
        }

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

        // Collapse a random starting cell
        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);
        int tile_index = UnityEngine.Random.Range(0, next_tile_index);

        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
    }

    private void StepWFC()
    {
        int cell_index = -1;
        int lowest_entropy = 9999;
        int collapse_count = 0;

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

        if (selected_cell.collapse_options.Count == 0)
        {
            Debug.LogError("Contradiction: Cell has no valid options!");
            return;
        }

        int selected_option_index = UnityEngine.Random.Range(0, selected_cell.collapse_options.Count);
        Tile tile_to_collapse_cell_to = selected_cell.collapse_options[selected_option_index];
        selected_cell.collapsed_tile = tile_to_collapse_cell_to;

        selected_cell.collapse_options.Clear();
        selected_cell.collapsed = true;

        ReduceEntropy(selected_cell);

        Invoke(nameof(StepWFC), 0.0001f);
    }

    private void OnWFCFinished()
    {
        wfc_complete = true;
        Debug.Log("WaveFunctionCollapse: Collapse complete, spawning clouds...");
        SpawnCloudsFromResult();
        OnWFCComplete?.Invoke();
    }

    private void SpawnCloudsFromResult()
    {
        int center_index = (pattern_size * pattern_size) / 2;

        foreach (Cell cell in cells)
        {
            if (!cell.collapsed || cell.collapsed_tile == null) continue;

            Color center_color = cell.collapsed_tile.tile_pixels[center_index];
            float brightness = (center_color.r + center_color.g + center_color.b) / 3f;

            // Spawn cloud if pixel is bright enough (cloud-like)
            if (brightness >= spawn_threshold)
            {
                Vector3 spawn_pos = spawn_origin + new Vector3(
                    cell.x * cloud_spacing,
                    cloud_height,
                    cell.y * cloud_spacing
                );

                GameObject cloud = Instantiate(cloud_prefab, spawn_pos, Quaternion.identity, transform);

                // Set cloud color based on bitmap
                CloudTile cloud_tile = cloud.GetComponent<CloudTile>();
                if (cloud_tile != null)
                {
                    cloud_tile.SetCloudColor(center_color);
                    cloud_tile.SetGridPosition(cell.x, cell.y);
                }

                spawned_clouds.Add(cloud);
            }
        }

        Debug.Log($"WaveFunctionCollapse: Spawned {spawned_clouds.Count} clouds");
    }

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

            foreach (DIRECTIONS direction in Enum.GetValues(typeof(DIRECTIONS)))
            {
                Vector2Int neighbor_position = new Vector2Int(current.x, current.y);
                switch (direction)
                {
                    case DIRECTIONS.UP: neighbor_position.y -= 1; break;
                    case DIRECTIONS.DOWN: neighbor_position.y += 1; break;
                    case DIRECTIONS.LEFT: neighbor_position.x -= 1; break;
                    case DIRECTIONS.RIGHT: neighbor_position.x += 1; break;
                }

                if (!position_to_cell.ContainsKey(neighbor_position))
                {
                    continue;
                }

                Cell neighbor = position_to_cell[neighbor_position];

                if (neighbor.collapsed)
                {
                    continue;
                }

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

                neighbor.collapse_options.RemoveAll(tile => !valid_tiles.Contains(tile));

                if (neighbor.collapse_options.Count < old_collapse_count)
                {
                    if (!cells_in_queue.Contains(neighbor.cell_index))
                    {
                        cells_to_process.Enqueue(neighbor);
                        cells_in_queue.Add(neighbor.cell_index);
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

            bool up_valid = true;
            for (int y = 0; y < pattern_size - 1 && up_valid; y++)
            {
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
            if (up_valid) tile.addAdjacency(DIRECTIONS.UP, other);

            bool down_valid = true;
            for (int y = 0; y < pattern_size - 1 && down_valid; y++)
            {
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
            if (down_valid) tile.addAdjacency(DIRECTIONS.DOWN, other);

            bool left_valid = true;
            for (int x = 0; x < pattern_size - 1 && left_valid; x++)
            {
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
            if (left_valid) tile.addAdjacency(DIRECTIONS.LEFT, other);

            bool right_valid = true;
            for (int x = 0; x < pattern_size - 1 && right_valid; x++)
            {
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
            if (right_valid) tile.addAdjacency(DIRECTIONS.RIGHT, other);
        }
    }

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

    // Public accessors
    public bool IsComplete => wfc_complete;
    public List<GameObject> GetSpawnedClouds() => spawned_clouds;
}