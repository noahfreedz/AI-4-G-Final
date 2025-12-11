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

        public void UpdateCell(Color cell_color)
        {
            if (wfc.target_terrain != null)
            {
                wfc.PaintTerrainAtCell(x, y, cell_color);
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
            UpdateCell(new Color(0.236f, 0.191f, 0.191f, 1.000f));

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
    }

    [System.Serializable]
    public class HeightColorStop
    {
        public float height;
        public Color color;
    }

    Vector2Int grid_start = new Vector2Int(0, 0);
    [SerializeField] int player_radius;

    [Header("Terrain Settings")]
    [SerializeField] public Terrain target_terrain;
    [SerializeField] float cells_per_terrain_unit = 1;

    [Header("Height Color Settings")]
    [SerializeField]
    List<HeightColorStop> height_colors = new List<HeightColorStop>()
    {
        new HeightColorStop { height = 0.01f, color = new Color(0.0f, 0.2f, 0.5f) },   // Deep Water
        new HeightColorStop { height = 0.04f, color = new Color(0.0f, 0.4f, 0.8f) },   // Shallow Water
        new HeightColorStop { height = 0.06f, color = new Color(0.9f, 0.85f, 0.6f) },  // Sand
        new HeightColorStop { height = 0.09f, color = new Color(0.2f, 0.6f, 0.2f) },   // Grass
        new HeightColorStop { height = 0.14f, color = new Color(0.1f, 0.4f, 0.1f) },   // Forest
        new HeightColorStop { height = 0.18f, color = new Color(0.5f, 0.4f, 0.3f) },   // Rock
        new HeightColorStop { height = 0.23f, color = new Color(1.0f, 1.0f, 1.0f) }    // Snow
    };

    [Header("WFC Prefabs")]
    [SerializeField] GameObject[] tile_prefabs;
    [SerializeField] public GameObject tile_base_prefab;
    [SerializeField] public GameObject cell_base_prefab;
    [SerializeField] GameObject pixel_prefab;

    [Header("WFC Settings")]
    [SerializeField] int pattern_size;
    [SerializeField] int cell_grid_size;

    // Terrain Painting
    private Texture2D terrain_texture;
    private int texture_width;
    private int texture_height;

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

    void Start()
    {
        if (target_terrain != null)
        {
            InitializeTerrainTexture();
            CalculateGridSizeFromTerrain();
            PaintTerrainByHeight();
        }

        //LoadWFCConditions(pattern_size, "3Bricks.png");
    }

    public void RefreshTerrainColors()
    {
        if (target_terrain == null) return;

        InitializeTerrainTexture();
        PaintTerrainByHeight();
    }

    void Update()
    {

    }

    private void InitializeTerrainTexture()
    {
        TerrainData terrain_data = target_terrain.terrainData;

        // Calculate Texture Size Based On Terrain Size And Cells Per Unit
        texture_width = Mathf.RoundToInt(terrain_data.size.x * cells_per_terrain_unit);
        texture_height = Mathf.RoundToInt(terrain_data.size.z * cells_per_terrain_unit);

        // Create The Texture
        terrain_texture = new Texture2D(texture_width, texture_height, TextureFormat.RGBA32, false);
        terrain_texture.filterMode = FilterMode.Point;
        terrain_texture.wrapMode = TextureWrapMode.Clamp;

        // Fill With Default Color
        Color[] pixels = new Color[texture_width * texture_height];
        Color default_color = new Color(1f, 0.191f, 0.191f, 1.000f);

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = default_color;
        }
        terrain_texture.SetPixels(pixels);
        terrain_texture.Apply();

        // Apply Texture To Terrain
        ApplyTextureToTerrain();
    }

    private void ApplyTextureToTerrain()
    {
        TerrainData terrain_data = target_terrain.terrainData;

        // Create A New Terrain Layer If Needed
        TerrainLayer[] existing_layers = terrain_data.terrainLayers;
        TerrainLayer wfc_layer = null;

        // Create New Terrain Layer
        wfc_layer = new TerrainLayer();
        wfc_layer.name = "WFC_Layer";
        wfc_layer.tileSize = new Vector2(terrain_data.size.x, terrain_data.size.z);
        wfc_layer.tileOffset = Vector2.zero;

        // Add To Terrain Layers
        TerrainLayer[] new_layers = new TerrainLayer[existing_layers.Length + 1];
        existing_layers.CopyTo(new_layers, 0);
        new_layers[existing_layers.Length] = wfc_layer;
        terrain_data.terrainLayers = new_layers;

        wfc_layer.diffuseTexture = terrain_texture;

        // Set The Alphamap To Use Only Our Layer
        int alphamap_res = terrain_data.alphamapResolution;
        float[,,] alphamaps = new float[alphamap_res, alphamap_res, terrain_data.terrainLayers.Length];

        int wfc_layer_index = System.Array.IndexOf(terrain_data.terrainLayers, wfc_layer);

        for (int y = 0; y < alphamap_res; y++)
        {
            for (int x = 0; x < alphamap_res; x++)
            {
                alphamaps[y, x, wfc_layer_index] = 1f;
            }
        }

        terrain_data.SetAlphamaps(0, 0, alphamaps);
    }

    private void CalculateGridSizeFromTerrain()
    {
        if (target_terrain == null) return;

        TerrainData terrain_data = target_terrain.terrainData;

        // Grid Size Matches Texture Size
        cell_grid_size = Mathf.Max(texture_width, texture_height);

        UnityEngine.Debug.Log($"Terrain size: {terrain_data.size.x}x{terrain_data.size.z}, Grid size: {cell_grid_size}x{cell_grid_size}");
    }

    public float GetTerrainHeightAtCell(int cell_x, int cell_y)
    {
        if (target_terrain == null) return 0f;

        TerrainData terrain_data = target_terrain.terrainData;

        // Convert Cell Position To Normalized Terrain Coordinates (0-1)
        float norm_x = (float)cell_x / texture_width;
        float norm_y = (float)cell_y / texture_height;

        // Sample Height (Returns 0-1)
        float height = terrain_data.GetInterpolatedHeight(norm_x, norm_y) / terrain_data.size.y;

        return height;
    }

    public float GetAverageTerrainHeightAtCell(int cell_x, int cell_y, int samples_per_axis = 3)
    {
        if (target_terrain == null) return 0f;

        TerrainData terrain_data = target_terrain.terrainData;

        // Calculate Cell Size In Normalized Coordinates
        float cell_width = 1f / texture_width;
        float cell_height = 1f / texture_height;

        // Starting Corner Of The Cell In Normalized Coords
        float start_x = (float)cell_x / texture_width;
        float start_y = (float)cell_y / texture_height;

        float total_height = 0f;
        int sample_count = 0;

        // Sample A Grid Of Points Within The Cell
        for (int sx = 0; sx < samples_per_axis; sx++)
        {
            for (int sy = 0; sy < samples_per_axis; sy++)
            {
                // Calculate Sample Position Within Cell
                float sample_x = start_x + (cell_width * sx / (samples_per_axis - 1));
                float sample_y = start_y + (cell_height * sy / (samples_per_axis - 1));

                // Clamp To Valid Range
                sample_x = Mathf.Clamp01(sample_x);
                sample_y = Mathf.Clamp01(sample_y);

                total_height += terrain_data.GetInterpolatedHeight(sample_x, sample_y);
                sample_count++;
            }
        }

        // Return Normalized Height (0-1)
        return (total_height / sample_count) / terrain_data.size.y;
    }

    public Color GetColorForHeight(float normalized_height)
    {
        // Handle Edge Cases
        if (height_colors == null || height_colors.Count == 0)
        {
            return Color.magenta;
        }

        if (height_colors.Count == 1)
        {
            return height_colors[0].color;
        }

        // Find The Two Color Stops To Interpolate Between
        for (int i = 0; i < height_colors.Count - 1; i++)
        {
            HeightColorStop current = height_colors[i];
            HeightColorStop next = height_colors[i + 1];

            if (normalized_height >= current.height && normalized_height <= next.height)
            {
                // Calculate Interpolation Factor
                float range = next.height - current.height;
                float t = (normalized_height - current.height) / range;

                // Lerp Between Colors
                return Color.Lerp(current.color, next.color, t);
            }
        }

        // If Below First Stop, Return First Color
        if (normalized_height < height_colors[0].height)
        {
            return height_colors[0].color;
        }

        // If Above Last Stop, Return Last Color
        return height_colors[height_colors.Count - 1].color;
    }

    public void PaintTerrainByHeight()
    {
        if (terrain_texture == null || target_terrain == null) return;

        Color[] pixels = new Color[texture_width * texture_height];

        for (int y = 0; y < texture_height; y++)
        {
            for (int x = 0; x < texture_width; x++)
            {
                float height = GetTerrainHeightAtCell(x, y);
                Color color = GetColorForHeight(height);
                pixels[y * texture_width + x] = color;
            }
        }

        terrain_texture.SetPixels(pixels);
        terrain_texture.Apply();

        UnityEngine.Debug.Log("Painted terrain by height");
    }

    public void PaintTerrainAtCell(int cell_x, int cell_y, Color color)
    {
        if (terrain_texture == null) return;

        // Clamp To Texture Bounds
        int tex_x = Mathf.Clamp(cell_x, 0, texture_width - 1);
        int tex_y = Mathf.Clamp(cell_y, 0, texture_height - 1);

        // Set The Pixel
        terrain_texture.SetPixel(tex_x, tex_y, color);
        terrain_texture.Apply();
    }

    public void PaintTerrainAtCellByHeight(int cell_x, int cell_y)
    {
        if (terrain_texture == null) return;

        float height = GetTerrainHeightAtCell(cell_x, cell_y);
        Color color = GetColorForHeight(height);
        PaintTerrainAtCell(cell_x, cell_y, color);
    }

    public void PaintTerrainAtCells(List<Vector2Int> cell_positions, List<Color> colors)
    {
        if (terrain_texture == null || cell_positions.Count != colors.Count) return;

        for (int i = 0; i < cell_positions.Count; i++)
        {
            int tex_x = Mathf.Clamp(cell_positions[i].x, 0, texture_width - 1);
            int tex_y = Mathf.Clamp(cell_positions[i].y, 0, texture_height - 1);
            terrain_texture.SetPixel(tex_x, tex_y, colors[i]);
        }

        terrain_texture.Apply();
    }

    public void SetTargetTerrain(Terrain terrain)
    {
        target_terrain = terrain;
        InitializeTerrainTexture();
        CalculateGridSizeFromTerrain();
    }

    private void SpawnTile(Vector2Int grid_pos)
    {
        int index = UnityEngine.Random.Range(0, tile_prefabs.Length);
        GameObject tile_prefab = tile_prefabs[index];
        Vector3 worldPos = new Vector3(grid_pos.x, 0, grid_pos.y);
        GameObject tile = Instantiate(tile_prefab, worldPos, Quaternion.identity);
        active_tiles.Add(grid_pos, tile);
    }

    public void UpdateWFC(Vector3 player_position)
    {
        Vector2Int playerGridPos = new Vector2Int(
            Mathf.FloorToInt(player_position.x),
            Mathf.FloorToInt(player_position.z)
        );

        HashSet<Vector2Int> tilesToKeep = new HashSet<Vector2Int>();

        for (int x = -player_radius; x <= player_radius; x++)
        {
            for (int y = -player_radius; y <= player_radius; y++)
            {
                Vector2Int grid_pos = new Vector2Int(
                    playerGridPos.x + x,
                    playerGridPos.y + y
                );

                grid_pos -= grid_start;
                tilesToKeep.Add(grid_pos);

                if (!active_tiles.ContainsKey(grid_pos))
                {
                    SpawnTile(grid_pos);
                }
            }
        }

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
        UnityEngine.Debug.Log("Starting WFC Conditions");

        Texture2D bitmap = LoadBitmap(file_name);

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

        // Use Calculated Grid Size For Terrain, Or Fall Back To Manual Setting
        int grid_width = texture_width > 0 ? texture_width : cell_grid_size;
        int grid_height = texture_height > 0 ? texture_height : cell_grid_size;

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

        int random_cell_index = UnityEngine.Random.Range(0, cells.Count);
        int tile_index = UnityEngine.Random.Range(0, next_tile_index);
        int center_index = (pattern_size * pattern_size) / 2;

        Color tile_center_color = index_to_tile[tile_index].tile_pixels[center_index];

        cells[random_cell_index].UpdateCell(tile_center_color);
        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
        StepWFC();
    }

    private void StepWFC()
    {
        UnityEngine.Debug.Log("Stepped WFC");
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
            UnityEngine.Debug.Log("No valid cell left to collapse.");
            if (collapse_count >= cells.Count)
            {
                wfc_retry_count = 0;
                StartNextWFC();
            }
            else
            {
                if (wfc_retry_count >= 3)
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

        Cell selected_cell = index_to_cell[cell_index];

        if (selected_cell.collapse_options.Count == 0)
        {
            UnityEngine.Debug.LogError("Contradiction: Cell has no valid options!");
            return;
        }

        int selected_option_index = UnityEngine.Random.Range(0, selected_cell.collapse_options.Count);
        Tile tile_to_collapse_cell_to = selected_cell.collapse_options[selected_option_index];
        selected_cell.collapsed_tile = tile_to_collapse_cell_to;

        int center_index = (pattern_size * pattern_size) / 2;
        Color tile_center_color = selected_cell.collapsed_tile.tile_pixels[center_index];
        selected_cell.UpdateCell(tile_center_color);

        selected_cell.collapse_options.Clear();
        selected_cell.collapsed = true;

        ReduceEntropy(selected_cell);

        Invoke(nameof(StepWFC), 0.05f);
    }

    private void ReduceEntropy(Cell start_cell)
    {
        Queue<Cell> cells_to_proccess = new Queue<Cell>();
        HashSet<int> cells_in_queue = new HashSet<int>();

        cells_to_proccess.Enqueue(start_cell);
        cells_in_queue.Add(start_cell.cell_index);

        while (cells_to_proccess.Count > 0)
        {
            Cell current = cells_to_proccess.Dequeue();
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
                        cells_to_proccess.Enqueue(neighbor);
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
        int center_index = (pattern_size * pattern_size) / 2;

        Color tile_center_color = index_to_tile[tile_index].tile_pixels[center_index];

        cells[random_cell_index].UpdateCell(tile_center_color);
        cells[random_cell_index].collapsed = true;
        cells[random_cell_index].collapsed_tile = index_to_tile[tile_index];
        ReduceEntropy(cells[random_cell_index]);
        StepWFC();
    }

    private void StartNextWFC()
    {
        CancelInvoke(nameof(StepWFC));

        cells.Clear();
        position_to_cell.Clear();
        index_to_cell.Clear();

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
        for (int i = 0; i < tile_a.tile_pixels.Length; i++)
        {
            if (tile_a.tile_pixels[i] == tile_b.tile_pixels[i])
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

        return texture;
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

    private void DisplayPatterns(int bitmap_depth, int pattern_size)
    {
        int x = 0;
        int z = 0;

        Vector3 tile_center = new Vector3();
        tile_center.x = x;
        tile_center.z = z;

        float pixel_size = 0.33f;
        float pattern_depth = pixel_size * pattern_size;

        foreach (KeyValuePair<int, Tile> pair in hash_to_tile)
        {
            Tile tile = pair.Value;
            int tile_x = 0;
            int tile_z = 0;

            GameObject tile_base = Instantiate(tile_base_prefab);
            tile_base.transform.position = tile_center;

            for (int i = 0; i < tile.tile_pixels.Length; i++)
            {
                GameObject pixel = Instantiate(pixel_prefab);
                Vector3 pixel_spawn = new Vector3();

                float spawn_x = (tile_center.x - pattern_depth / 2 + pixel_size / 2) + (pixel_size * tile_x);
                float spawn_z = (tile_center.z - pattern_depth / 2 + pixel_size / 2) + (pixel_size * tile_z);

                pixel_spawn.x = spawn_x;
                pixel_spawn.y = 0.05f;
                pixel_spawn.z = spawn_z;

                pixel.transform.position = pixel_spawn;

                Renderer p_render = pixel.GetComponent<Renderer>();
                p_render.material.color = tile.tile_pixels[i];

                tile_x++;

                if (tile_x == pattern_size)
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

            tile_center.x = x * (pattern_depth * 1.1f);
            tile_center.y = 0;
            tile_center.z = z * (pattern_depth * 1.1f);
        }
    }
}