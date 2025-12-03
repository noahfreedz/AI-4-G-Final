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

        // Hydraulic Erosion button
        GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        if (GUILayout.Button("Apply Hydraulic Erosion (River Carving)", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("Apply Hydraulic Erosion",
                $"This will simulate {erosion.dropletCount} water droplets carving rivers into the terrain. This may take a moment. Continue?",
                "Yes", "Cancel"))
            {
                erosion.ApplyHydraulicErosion();
            }
        }

        // Thermal Erosion button
        GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
        if (GUILayout.Button("Apply Thermal Erosion (Slope Weathering)", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("Apply Thermal Erosion",
                $"This will simulate slope-based weathering with {erosion.thermalIterations} iterations. Continue?",
                "Yes", "Cancel"))
            {
                erosion.ApplyThermalErosion();
            }
        }

        EditorGUILayout.Space(10);

        // Combined erosion button
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("🌊 Apply Full River Erosion 🌊", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Apply Combined Erosion",
                "This will apply hydraulic erosion and thermal erosion in sequence to carve realistic rivers and valleys. This may take some time. Continue?",
                "Yes", "Cancel"))
            {
                erosion.ApplyCombinedErosion();
            }
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Hydraulic Erosion: Simulates water droplets flowing downhill, carving rivers and valleys.\n\n" +
            "Thermal Erosion: Simulates material sliding down steep slopes, creating natural riverbanks.\n\n" +
            "Use 'Full River Erosion' for realistic combined river carving!",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "⚠️ Erosion modifies your terrain permanently. Save your scene before applying!",
            MessageType.Warning);
    }
}