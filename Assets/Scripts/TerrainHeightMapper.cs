using System.Collections.Generic;
using UnityEngine;

public class TerrainHeightMapper : MonoBehaviour
{
    [System.Serializable]
    public class HeightColorStop
    {
        public string name;
        public float height;
        public Color color;

        [Header("Physics Properties")]
        [Range(0f, 1f)]
        public float restitution = 0.5f;
        [Range(0f, 1f)]
        public float friction = 0.5f;
        [Range(0f, 1f)]
        public float drag = 0.0f;
        public bool is_water = false;
        [Range(0f, 2f)]
        public float water_density = 1.0f;
    }

    [Header("Terrain Settings")]
    [SerializeField] public Terrain target_terrain;
    [SerializeField] float cells_per_terrain_unit = 1;

    [Header("Height Color Settings")]
    [SerializeField]
    public List<HeightColorStop> height_colors = new List<HeightColorStop>()
    {
        new HeightColorStop {
            name = "Deep Water",
            height = 0.01f,
            color = new Color(0.0f, 0.2f, 0.5f),
            restitution = 0.1f,
            friction = 0.2f,
            drag = 0.8f,
            is_water = true,
            water_density = 1.0f
        },
        new HeightColorStop {
            name = "Shallow Water",
            height = 0.04f,
            color = new Color(0.0f, 0.4f, 0.8f),
            restitution = 0.2f,
            friction = 0.3f,
            drag = 0.5f,
            is_water = true,
            water_density = 0.8f
        },
        new HeightColorStop {
            name = "Sand",
            height = 0.06f,
            color = new Color(0.9f, 0.85f, 0.6f),
            restitution = 0.2f,
            friction = 0.8f,
            drag = 0.3f,
            is_water = false,
            water_density = 0f
        },
        new HeightColorStop {
            name = "Grass",
            height = 0.09f,
            color = new Color(0.2f, 0.6f, 0.2f),
            restitution = 0.4f,
            friction = 0.6f,
            drag = 0.1f,
            is_water = false,
            water_density = 0f
        },
        new HeightColorStop {
            name = "Forest",
            height = 0.14f,
            color = new Color(0.1f, 0.4f, 0.1f),
            restitution = 0.3f,
            friction = 0.7f,
            drag = 0.15f,
            is_water = false,
            water_density = 0f
        },
        new HeightColorStop {
            name = "Rock",
            height = 0.18f,
            color = new Color(0.5f, 0.4f, 0.3f),
            restitution = 0.8f,
            friction = 0.4f,
            drag = 0.0f,
            is_water = false,
            water_density = 0f
        },
        new HeightColorStop {
            name = "Snow",
            height = 0.23f,
            color = new Color(1.0f, 1.0f, 1.0f),
            restitution = 0.3f,
            friction = 0.1f,
            drag = 0.05f,
            is_water = false,
            water_density = 0f
        }
    };

    // Terrain Painting
    private Texture2D terrain_texture;
    public int texture_width { get; private set; }
    public int texture_height { get; private set; }

    void Start()
    {
        if (target_terrain != null)
        {
            InitializeTerrainTexture();
            DebugTerrainHeights();
            PaintTerrainByHeight();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RefreshTerrainColors();
        }
    }

    public void InitializeTerrainTexture()
    {
        TerrainData terrain_data = target_terrain.terrainData;

        texture_width = Mathf.RoundToInt(terrain_data.size.x * cells_per_terrain_unit);
        texture_height = Mathf.RoundToInt(terrain_data.size.z * cells_per_terrain_unit);

        terrain_texture = new Texture2D(texture_width, texture_height, TextureFormat.RGBA32, false);
        terrain_texture.filterMode = FilterMode.Point;
        terrain_texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[texture_width * texture_height];
        Color default_color = new Color(1f, 0.191f, 0.191f, 1.000f);

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = default_color;
        }
        terrain_texture.SetPixels(pixels);
        terrain_texture.Apply();

        ApplyTextureToTerrain();
    }

    private void ApplyTextureToTerrain()
    {
        TerrainData terrain_data = target_terrain.terrainData;

        TerrainLayer[] existing_layers = terrain_data.terrainLayers;
        TerrainLayer wfc_layer = new TerrainLayer();
        wfc_layer.name = "WFC_Layer";
        wfc_layer.tileSize = new Vector2(terrain_data.size.x, terrain_data.size.z);
        wfc_layer.tileOffset = Vector2.zero;

        TerrainLayer[] new_layers = new TerrainLayer[existing_layers.Length + 1];
        existing_layers.CopyTo(new_layers, 0);
        new_layers[existing_layers.Length] = wfc_layer;
        terrain_data.terrainLayers = new_layers;

        wfc_layer.diffuseTexture = terrain_texture;

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

    public void DebugTerrainHeights()
    {
        if (target_terrain == null) return;

        float min_height = 1f;
        float max_height = 0f;

        for (int y = 0; y < texture_height; y++)
        {
            for (int x = 0; x < texture_width; x++)
            {
                float height = GetTerrainHeightAtCell(x, y);
                if (height < min_height) min_height = height;
                if (height > max_height) max_height = height;
            }
        }

        Debug.Log($"Terrain height range: {min_height} to {max_height}");
    }

    public float GetTerrainHeightAtCell(int cell_x, int cell_y)
    {
        if (target_terrain == null) return 0f;

        TerrainData terrain_data = target_terrain.terrainData;

        float norm_x = (float)cell_x / texture_width;
        float norm_y = (float)cell_y / texture_height;

        float height = terrain_data.GetInterpolatedHeight(norm_x, norm_y) / terrain_data.size.y;

        return height;
    }

    public float GetTerrainHeightAtPosition(Vector3 world_position)
    {
        if (target_terrain == null) return 0f;

        TerrainData terrain_data = target_terrain.terrainData;
        Vector3 terrain_position = target_terrain.transform.position;

        float norm_x = (world_position.x - terrain_position.x) / terrain_data.size.x;
        float norm_z = (world_position.z - terrain_position.z) / terrain_data.size.z;

        norm_x = Mathf.Clamp01(norm_x);
        norm_z = Mathf.Clamp01(norm_z);

        float height = terrain_data.GetInterpolatedHeight(norm_x, norm_z) / terrain_data.size.y;

        return height;
    }

    public float GetAverageTerrainHeightAtCell(int cell_x, int cell_y, int samples_per_axis = 3)
    {
        if (target_terrain == null) return 0f;

        TerrainData terrain_data = target_terrain.terrainData;

        float cell_width = 1f / texture_width;
        float cell_height = 1f / texture_height;

        float start_x = (float)cell_x / texture_width;
        float start_y = (float)cell_y / texture_height;

        float total_height = 0f;
        int sample_count = 0;

        for (int sx = 0; sx < samples_per_axis; sx++)
        {
            for (int sy = 0; sy < samples_per_axis; sy++)
            {
                float sample_x = start_x + (cell_width * sx / (samples_per_axis - 1));
                float sample_y = start_y + (cell_height * sy / (samples_per_axis - 1));

                sample_x = Mathf.Clamp01(sample_x);
                sample_y = Mathf.Clamp01(sample_y);

                total_height += terrain_data.GetInterpolatedHeight(sample_x, sample_y);
                sample_count++;
            }
        }

        return (total_height / sample_count) / terrain_data.size.y;
    }

    public Color GetColorForHeight(float normalized_height)
    {
        if (height_colors == null || height_colors.Count == 0)
        {
            return Color.magenta;
        }

        if (height_colors.Count == 1)
        {
            return height_colors[0].color;
        }

        for (int i = 0; i < height_colors.Count - 1; i++)
        {
            HeightColorStop current = height_colors[i];
            HeightColorStop next = height_colors[i + 1];

            if (normalized_height >= current.height && normalized_height <= next.height)
            {
                float range = next.height - current.height;
                float t = (normalized_height - current.height) / range;

                return Color.Lerp(current.color, next.color, t);
            }
        }

        if (normalized_height < height_colors[0].height)
        {
            return height_colors[0].color;
        }

        return height_colors[height_colors.Count - 1].color;
    }

    public HeightColorStop GetPropertiesForHeight(float normalized_height)
    {
        if (height_colors == null || height_colors.Count == 0)
        {
            return new HeightColorStop();
        }

        if (height_colors.Count == 1)
        {
            return height_colors[0];
        }

        for (int i = 0; i < height_colors.Count - 1; i++)
        {
            HeightColorStop current = height_colors[i];
            HeightColorStop next = height_colors[i + 1];

            if (normalized_height >= current.height && normalized_height < next.height)
            {
                return current;
            }
        }

        if (normalized_height < height_colors[0].height)
        {
            return height_colors[0];
        }

        return height_colors[height_colors.Count - 1];
    }

    public HeightColorStop GetTerrainPropertiesAtPosition(Vector3 world_position)
    {
        float height = GetTerrainHeightAtPosition(world_position);
        return GetPropertiesForHeight(height);
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

        Debug.Log("Painted terrain by height");
    }

    public void RefreshTerrainColors()
    {
        if (target_terrain == null) return;

        InitializeTerrainTexture();
        PaintTerrainByHeight();
    }

    public void PaintTerrainAtCell(int cell_x, int cell_y, Color color)
    {
        if (terrain_texture == null) return;

        int tex_x = Mathf.Clamp(cell_x, 0, texture_width - 1);
        int tex_y = Mathf.Clamp(cell_y, 0, texture_height - 1);

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
    }
}