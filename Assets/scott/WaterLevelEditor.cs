using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WaterLevel))]
public class WaterLevelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        WaterLevel waterLevel = (WaterLevel)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Water Plane Controls", EditorStyles.boldLabel);

        // Create Water Plane button
        if (GUILayout.Button("Create Water Plane", GUILayout.Height(30)))
        {
            waterLevel.CreateWaterPlane();
        }

        // Remove Water Plane button
        if (GUILayout.Button("Remove Water Plane", GUILayout.Height(30)))
        {
            waterLevel.RemoveWaterPlane();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Water Level: " + waterLevel.waterLevel.ToString("F3") +
            "\nClick 'Create Water Plane' to generate a water plane at this height.",
            MessageType.Info);
    }
}