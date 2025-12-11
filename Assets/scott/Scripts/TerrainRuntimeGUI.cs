using UnityEngine;

public class TerrainRuntimeGUI : MonoBehaviour
{
    [Header("References")]
    public RawHeightmapImporter heightmapImporter;
    public TerrainErosion terrainErosion;
    public WaterLevel waterLevel;

    [Header("GUI Settings")]
    public KeyCode toggleKey = KeyCode.Tab;
    public bool showGUI = true;

    private Rect windowRect = new Rect(20, 20, 400, 600);
    private Vector2 scrollPosition;
    private bool isDragging = false;

    // GUI state
    private enum GUITab { Heightmap, Erosion, Water, All }
    private GUITab currentTab = GUITab.All;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showGUI = !showGUI;

            // Unlock cursor when showing GUI
            if (showGUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void OnGUI()
    {
        if (!showGUI) return;

        // Make window draggable
        windowRect = GUI.Window(0, windowRect, DrawWindow, "Terrain Runtime Controls");
    }

    void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();

        // Tab selection
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heightmap", currentTab == GUITab.Heightmap ? GUI.skin.box : GUI.skin.button))
            currentTab = GUITab.Heightmap;
        if (GUILayout.Button("Erosion", currentTab == GUITab.Erosion ? GUI.skin.box : GUI.skin.button))
            currentTab = GUITab.Erosion;
        if (GUILayout.Button("Water", currentTab == GUITab.Water ? GUI.skin.box : GUI.skin.button))
            currentTab = GUITab.Water;
        if (GUILayout.Button("All", currentTab == GUITab.All ? GUI.skin.box : GUI.skin.button))
            currentTab = GUITab.All;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Scrollable content
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(500));

        if (currentTab == GUITab.Heightmap || currentTab == GUITab.All)
        {
            DrawHeightmapControls();
        }

        if (currentTab == GUITab.Erosion || currentTab == GUITab.All)
        {
            DrawErosionControls();
        }

        if (currentTab == GUITab.Water || currentTab == GUITab.All)
        {
            DrawWaterControls();
        }

        GUILayout.EndScrollView();

        // Instructions
        GUILayout.Space(10);
        GUILayout.Label($"Press {toggleKey} to toggle this window", EditorStyles.miniLabel);

        GUILayout.EndVertical();

        // Make window draggable
        GUI.DragWindow();
    }

    void DrawHeightmapControls()
    {
        if (heightmapImporter == null)
        {
            GUILayout.Label("? RawHeightmapImporter not assigned!", EditorStyles.boldLabel);
            return;
        }

        GUILayout.Label("??? HEIGHTMAP GENERATION ???", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Generation mode toggle
        heightmapImporter.useRandomGeneration = GUILayout.Toggle(heightmapImporter.useRandomGeneration, "Use Random Generation (Perlin Noise)");

        // Island mode toggle
        heightmapImporter.useIslandMode = GUILayout.Toggle(heightmapImporter.useIslandMode, "Island Mode (Euclidean Shaping)");

        // Island settings (only show if island mode is on)
        if (heightmapImporter.useIslandMode)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Island Settings:", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Island Mix: {heightmapImporter.islandMix:F2}", GUILayout.Width(150));
            heightmapImporter.islandMix = GUILayout.HorizontalSlider(heightmapImporter.islandMix, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Island Scale: {heightmapImporter.islandScale:F2}", GUILayout.Width(150));
            heightmapImporter.islandScale = GUILayout.HorizontalSlider(heightmapImporter.islandScale, 0.5f, 3f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Edge Sharpness: {heightmapImporter.distanceExponent:F2}", GUILayout.Width(150));
            heightmapImporter.distanceExponent = GUILayout.HorizontalSlider(heightmapImporter.distanceExponent, 0.5f, 4f);
            GUILayout.EndHorizontal();

            // Distance function selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance Function:", GUILayout.Width(150));
            if (GUILayout.Button(heightmapImporter.distanceFunction.ToString()))
            {
                int currentIndex = (int)heightmapImporter.distanceFunction;
                int maxIndex = System.Enum.GetValues(typeof(RawHeightmapImporter.DistanceFunction)).Length;
                heightmapImporter.distanceFunction = (RawHeightmapImporter.DistanceFunction)((currentIndex + 1) % maxIndex);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.Space(10);

        // Octaves
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Octaves: {heightmapImporter.octaves}", GUILayout.Width(150));
        heightmapImporter.octaves = (int)GUILayout.HorizontalSlider(heightmapImporter.octaves, 1, 6);
        GUILayout.EndHorizontal();

        // Base Frequency
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Base Frequency: {heightmapImporter.baseFrequency:F2}", GUILayout.Width(150));
        heightmapImporter.baseFrequency = GUILayout.HorizontalSlider(heightmapImporter.baseFrequency, 0.5f, 4f);
        GUILayout.EndHorizontal();

        // Lacunarity
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Lacunarity: {heightmapImporter.lacunarity:F2}", GUILayout.Width(150));
        heightmapImporter.lacunarity = GUILayout.HorizontalSlider(heightmapImporter.lacunarity, 1.5f, 3f);
        GUILayout.EndHorizontal();

        // Persistence
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Persistence: {heightmapImporter.persistence:F2}", GUILayout.Width(150));
        heightmapImporter.persistence = GUILayout.HorizontalSlider(heightmapImporter.persistence, 0.2f, 0.8f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Height Scale
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Height Scale: {heightmapImporter.heightScale:F2}", GUILayout.Width(150));
        heightmapImporter.heightScale = GUILayout.HorizontalSlider(heightmapImporter.heightScale, 0f, 2f);
        GUILayout.EndHorizontal();

        // Redistribution Exponent
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Redistribution: {heightmapImporter.redistributionExponent:F2}", GUILayout.Width(150));
        heightmapImporter.redistributionExponent = GUILayout.HorizontalSlider(heightmapImporter.redistributionExponent, 0.1f, 5f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Smooth Amount
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Smooth Passes: {heightmapImporter.smoothAmount:F1}", GUILayout.Width(150));
        heightmapImporter.smoothAmount = GUILayout.HorizontalSlider(heightmapImporter.smoothAmount, 0f, 10f);
        GUILayout.EndHorizontal();

        // Random Strength
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Random Strength: {heightmapImporter.randomStrength:F2}", GUILayout.Width(150));
        heightmapImporter.randomStrength = GUILayout.HorizontalSlider(heightmapImporter.randomStrength, 0f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Info about the apply process
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 9;
        infoStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
        infoStyle.wordWrap = true;
        GUILayout.Label("Apply order: Base heightmap ? Random variation ? Smoothing", infoStyle);

        GUILayout.Space(5);

        // Action button - applies heightmap, then adds random variation, then smooths
        if (GUILayout.Button("Apply Heightmap (with Random + Smoothing)", GUILayout.Height(35)))
        {
            ApplyCompleteHeightmap();
        }

        GUILayout.Space(20);
    }

    void ApplyCompleteHeightmap()
    {
        if (heightmapImporter == null) return;

        // Step 1: Apply base heightmap
        heightmapImporter.ApplyHeightmap();

        // Step 2: Add random variation if strength > 0
        if (heightmapImporter.randomStrength > 0)
        {
            heightmapImporter.ApplyRandomized();
        }

        // Step 3: Refresh erosion system with new terrain data
        if (terrainErosion != null)
        {
            terrainErosion.RefreshTerrainData();
        }

        Debug.Log("Complete heightmap applied (base + random + smoothing)");
    }

    void DrawErosionControls()
    {
        if (terrainErosion == null)
        {
            GUILayout.Label("? TerrainErosion not assigned!", EditorStyles.boldLabel);
            return;
        }

        GUILayout.Label("??? UNDERWATER EROSION ???", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Erosion Strength
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Erosion Strength: {terrainErosion.erosionStrength:F4}", GUILayout.Width(150));
        terrainErosion.erosionStrength = GUILayout.HorizontalSlider(terrainErosion.erosionStrength, 0.0001f, 0.01f);
        GUILayout.EndHorizontal();

        // Erosion Depth
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Erosion Depth: {terrainErosion.erosionDepth}", GUILayout.Width(150));
        terrainErosion.erosionDepth = (int)GUILayout.HorizontalSlider(terrainErosion.erosionDepth, 1, 50);
        GUILayout.EndHorizontal();

        // Max Erodable Slope
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Max Slope: {terrainErosion.maxErodableSlope:F2}", GUILayout.Width(150));
        terrainErosion.maxErodableSlope = GUILayout.HorizontalSlider(terrainErosion.maxErodableSlope, 0.01f, 0.5f);
        GUILayout.EndHorizontal();

        // Erosion Radius
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Erosion Radius: {terrainErosion.erosionRadius}", GUILayout.Width(150));
        terrainErosion.erosionRadius = (int)GUILayout.HorizontalSlider(terrainErosion.erosionRadius, 1, 5);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Smoothing Passes
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Smoothing Passes: {terrainErosion.smoothingPasses}", GUILayout.Width(150));
        terrainErosion.smoothingPasses = (int)GUILayout.HorizontalSlider(terrainErosion.smoothingPasses, 0, 5);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // River Formation
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Spread Probability: {terrainErosion.spreadProbability:F2}", GUILayout.Width(150));
        terrainErosion.spreadProbability = GUILayout.HorizontalSlider(terrainErosion.spreadProbability, 0.1f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Slope Bias: {terrainErosion.slopeBias:F2}", GUILayout.Width(150));
        terrainErosion.slopeBias = GUILayout.HorizontalSlider(terrainErosion.slopeBias, 0f, 1f);
        GUILayout.EndHorizontal();

        // Flow to Deepest
        terrainErosion.flowTowardDeepest = GUILayout.Toggle(terrainErosion.flowTowardDeepest, "Flow Toward Deepest");

        GUILayout.Space(10);

        // Sediment Transport
        GUILayout.Label("Sediment Transport", EditorStyles.boldLabel);
        terrainErosion.transportToCenter = GUILayout.Toggle(terrainErosion.transportToCenter, "Transport Sediment to Center");

        if (terrainErosion.transportToCenter)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Deposition Strength: {terrainErosion.depositionStrength:F2}", GUILayout.Width(150));
            terrainErosion.depositionStrength = GUILayout.HorizontalSlider(terrainErosion.depositionStrength, 0f, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Deposition Radius: {terrainErosion.depositionRadius:F2}", GUILayout.Width(150));
            terrainErosion.depositionRadius = GUILayout.HorizontalSlider(terrainErosion.depositionRadius, 0.1f, 0.8f);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.Space(10);

        // Auto Erosion Settings
        GUILayout.Label("Auto Erosion", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Step Interval: {terrainErosion.stepInterval:F2}s", GUILayout.Width(150));
        terrainErosion.stepInterval = GUILayout.HorizontalSlider(terrainErosion.stepInterval, 0.01f, 2f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Max Auto Steps: {terrainErosion.maxAutoSteps}", GUILayout.Width(150));
        terrainErosion.maxAutoSteps = (int)GUILayout.HorizontalSlider(terrainErosion.maxAutoSteps, 0, 1000);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Action buttons
        if (GUILayout.Button("Single Erosion Step", GUILayout.Height(30)))
        {
            terrainErosion.ApplyUnderwaterErosionStep();
        }

        string autoButtonText = terrainErosion.autoErode ? "Stop Auto Erosion" : "Start Auto Erosion";
        if (GUILayout.Button(autoButtonText, GUILayout.Height(30)))
        {
            terrainErosion.ToggleErosion();
        }

        GUILayout.Space(10);

        // Debug visualization
        GUILayout.Label("Debug Visualization", EditorStyles.boldLabel);
        terrainErosion.showUnderwaterCells = GUILayout.Toggle(terrainErosion.showUnderwaterCells, "Show Underwater Cells");
        terrainErosion.showErosionFront = GUILayout.Toggle(terrainErosion.showErosionFront, "Show Erosion Front");

        GUILayout.Space(20);
    }

    void DrawWaterControls()
    {
        if (waterLevel == null)
        {
            GUILayout.Label("? WaterLevel not assigned!", EditorStyles.boldLabel);
            return;
        }

        GUILayout.Label("??? WATER SETTINGS ???", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Water Height
        float currentWaterHeight = waterLevel.GetWaterHeight();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Water Height: {currentWaterHeight:F2}", GUILayout.Width(150));
        float newWaterHeight = GUILayout.HorizontalSlider(currentWaterHeight, 0f, 100f);
        GUILayout.EndHorizontal();

        if (newWaterHeight != currentWaterHeight)
        {
            waterLevel.SetWaterHeight(newWaterHeight);
        }

        GUILayout.Space(10);

        // Regenerate All button
        if (GUILayout.Button("Regenerate All (Terrain + Water)", GUILayout.Height(35)))
        {
            RegenerateAll();
        }

        GUILayout.Space(10);

        // Info text
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 9;
        infoStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
        infoStyle.wordWrap = true;
        GUILayout.Label("Regenerate creates new terrain and randomizes water height", infoStyle);

        GUILayout.Space(20);
    }

    void RegenerateAll()
    {
        if (heightmapImporter == null || waterLevel == null)
        {
            Debug.LogError("Cannot regenerate: missing references");
            return;
        }

        // Step 1: Apply heightmap with new random seed
        ApplyCompleteHeightmap();

        // Step 2: Randomize water level
        float minWater = heightmapImporter.terrain.terrainData.size.y * 0.2f;
        float maxWater = heightmapImporter.terrain.terrainData.size.y * 0.6f;
        float newWaterHeight = Random.Range(minWater, maxWater);
        waterLevel.SetWaterHeight(newWaterHeight);

        Debug.Log($"Regenerated terrain and water level (new height: {newWaterHeight:F2})");
    }
}

// Custom EditorStyles that work in runtime
static class EditorStyles
{
    private static GUIStyle _boldLabel;
    public static GUIStyle boldLabel
    {
        get
        {
            if (_boldLabel == null)
            {
                _boldLabel = new GUIStyle(GUI.skin.label);
                _boldLabel.fontStyle = FontStyle.Bold;
            }
            return _boldLabel;
        }
    }

    private static GUIStyle _miniLabel;
    public static GUIStyle miniLabel
    {
        get
        {
            if (_miniLabel == null)
            {
                _miniLabel = new GUIStyle(GUI.skin.label);
                _miniLabel.fontSize = 10;
            }
            return _miniLabel;
        }
    }
}