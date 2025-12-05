using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainErosion))]
public class TerrainErosionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TerrainErosion erosion = (TerrainErosion)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Erosion Controls", EditorStyles.boldLabel);

        // Underwater Erosion button
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("Apply Underwater Erosion Step", GUILayout.Height(35)))
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Play Mode Required",
                    "Underwater erosion uses runtime data in TerrainErosion.\n\nPlease enter Play Mode before applying.",
                    "OK"
                );
            }
            else
            {
                if (EditorUtility.DisplayDialog(
                    "Apply Underwater Erosion",
                    $"This will erode terrain beneath the water level, carving from the water edge inward.\n\n" +
                    $"Erosion Depth: {erosion.erosionDepth} cells\n" +
                    $"Strength: {erosion.erosionStrength}\n" +
                    $"Max Slope: {erosion.maxErodableSlope} (steeper slopes protected)\n\nContinue?",
                    "Yes", "Cancel"))
                {
                    erosion.ApplyUnderwaterErosionStep();
                    EditorUtility.SetDirty(erosion.terrain.terrainData);
                }
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();

        // Toggle ongoing auto erosion
        GUI.backgroundColor = erosion.autoErode ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 1f, 0.5f);
        string toggleLabel = erosion.autoErode ? "⏹ Stop Ongoing Erosion" : "▶ Start Ongoing Erosion";
        if (GUILayout.Button(toggleLabel, GUILayout.Height(40)))
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Play Mode Required",
                    "Ongoing erosion runs over time in Update().\n\nPlease enter Play Mode to start or stop it.",
                    "OK"
                );
            }
            else
            {
                erosion.ToggleErosion();
                EditorUtility.SetDirty(erosion.terrain.terrainData);
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();

        // Info box
        EditorGUILayout.HelpBox(
            "Underwater Erosion: Carves river channels in terrain that's below the water level.\n\n" +
            "How it works:\n" +
            "1. Detects all terrain below the water surface\n" +
            "2. Finds the water's edge (boundary between water and land)\n" +
            "3. Erodes inward from the edge, creating river channels\n\n" +
            "• 'Apply One Step' runs a single erosion pass\n" +
            "• 'Start Ongoing Erosion' continuously erodes over time\n" +
            "• Enable 'Flow Toward Deepest' to create channels flowing to lowest points\n" +
            "• Adjust 'Spread Probability' to control river width",
            MessageType.Info);

        EditorGUILayout.Space();

        // Status info if in play mode
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            if (erosion.waterLevel != null)
            {
                EditorGUILayout.LabelField($"Water Height: {erosion.waterLevel.GetWaterHeight():F2} units");
            }
            else
            {
                EditorGUILayout.LabelField("⚠️ No WaterLevel script assigned!", EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField($"Auto Erode: {(erosion.autoErode ? "Running" : "Stopped")}");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // Warning box
        EditorGUILayout.HelpBox(
            "⚠️ Erosion permanently modifies your terrain heights. Make a backup of your terrain asset before experimenting!\n\n" +
            "You can duplicate your terrain: Project panel → Right-click terrain asset → Duplicate",
            MessageType.Warning);

        EditorGUILayout.Space();

        // Quick setup tips
        if (erosion.terrain == null || erosion.waterLevel == null)
        {
            EditorGUILayout.HelpBox(
                "🔧 Quick Setup:\n" +
                "1. Assign your Terrain in the 'References' section\n" +
                "2. Assign your WaterLevel script\n" +
                "3. Enter Play Mode\n" +
                "4. Click 'Apply Underwater Erosion Step'",
                MessageType.None);
        }
    }
}