using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RawHeightmapImporter))]
public class RawHeightmapImporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RawHeightmapImporter importer = (RawHeightmapImporter)target;

        if (GUILayout.Button("Randomize & Apply"))
        {
            importer.ApplyRandomized();
        }
    }
}
