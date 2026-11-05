using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LandscapeGrassManager))]
public class LandscapeGrassManagerEditor : Editor
{
    public enum BrushMode
    {
        Off = 0,
        Paint = 1,
        Erase = 2
    }

    private static BrushMode currentBrushMode = BrushMode.Off;
    private static float brushRadius = 6.0f;
    private static float brushSpacing = 1.8f;
    private static float brushMinScale = 0.95f;
    private static float brushMaxScale = 1.35f;
    private static bool brushAlignNormal = true;

    private Vector3 lastMouseWorldPos = Vector3.zero;
    private Vector3 lastMouseNormal = Vector3.up;
    private bool hasValidTerrainHit = false;

    public override void OnInspectorGUI()
    {
        var manager = (LandscapeGrassManager)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Landscape Grass System", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Interactive Anime Grass System. You can paint/erase grass manually in the Scene View with the brush, or bake grass across the entire landscape area automatically.",
            MessageType.Info
        );

        EditorGUILayout.Space(4);

        // Stats Box
        if (manager.grassData != null && manager.grassData.chunks != null && manager.grassData.chunks.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("--- Grass Statistics ---", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Total Grass Patches: {manager.grassData.totalInstances:N0}");
            EditorGUILayout.LabelField($"Spatial Chunks: {manager.grassData.chunks.Count}");
            EditorGUILayout.LabelField($"Baked Spacing: {manager.grassData.bakedSpacing:F2}m");
            EditorGUILayout.LabelField($"Last Baked: {manager.grassData.bakedTimestamp}");
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Grass has not been baked yet. Click 'Bake Grass Across Landscape' or use the Brush below.", MessageType.Warning);
        }

        EditorGUILayout.Space(8);

        // --- GROUND TEXTURE PAINTER BUTTON ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🎨 Ground Texture Painting", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Paint Grass, Rock, and Dirt textures directly onto the ground surface with the Scene View brush.", MessageType.Info);
        GUI.backgroundColor = new Color(0.45f, 0.85f, 1.0f);
        if (GUILayout.Button("🎨 Open Terrain Texture Painter Tool", GUILayout.Height(32)))
        {
            Landscape.TerrainTexturePainter.OpenWindow();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // --- INTERACTIVE BRUSH TOOLBAR ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🖌️ Grass Brush Tool (Scene View)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = currentBrushMode == BrushMode.Off ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        if (GUILayout.Toggle(currentBrushMode == BrushMode.Off, "View (Off)", "Button", GUILayout.Height(30)))
        {
            currentBrushMode = BrushMode.Off;
        }

        GUI.backgroundColor = currentBrushMode == BrushMode.Paint ? new Color(0.35f, 0.9f, 0.35f) : Color.white;
        if (GUILayout.Toggle(currentBrushMode == BrushMode.Paint, "🌿 Paint Grass", "Button", GUILayout.Height(30)))
        {
            currentBrushMode = BrushMode.Paint;
        }

        GUI.backgroundColor = currentBrushMode == BrushMode.Erase ? new Color(0.95f, 0.35f, 0.35f) : Color.white;
        if (GUILayout.Toggle(currentBrushMode == BrushMode.Erase, "🗑️ Erase Grass", "Button", GUILayout.Height(30)))
        {
            currentBrushMode = BrushMode.Erase;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (currentBrushMode != BrushMode.Off)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                currentBrushMode == BrushMode.Paint
                    ? "🌿 Paint Mode:\n• Left-Click & Drag in Scene View to paint grass.\n• Hold CTRL while dragging to quickly Erase!\n• Use [ and ] to resize brush."
                    : "🗑️ Erase Mode:\n• Left-Click & Drag in Scene View to remove grass.\n• Use [ and ] to resize brush.",
                MessageType.None
            );

            brushRadius = EditorGUILayout.Slider("Brush Radius (m)", brushRadius, 1f, 30f);
            if (currentBrushMode == BrushMode.Paint)
            {
                brushSpacing = EditorGUILayout.Slider("Grass Spacing (m)", brushSpacing, 0.9f, 3.5f);
                EditorGUILayout.MinMaxSlider("Scale Range", ref brushMinScale, ref brushMaxScale, 0.5f, 2.5f);
                EditorGUILayout.LabelField($"Min Scale: {brushMinScale:F2}x  |  Max Scale: {brushMaxScale:F2}x");
                brushAlignNormal = EditorGUILayout.Toggle("Align to Normal", brushAlignNormal);
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // --- CLEAR & SPARSE PRESETS ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚡ Quick Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.5f);
        if (GUILayout.Button("🌱 Sparse Clusters ('Here & There')", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Place Sparse Clusters",
                "This will place a few aesthetic, natural grass clusters in selective spots (meadows, lake/river banks) with very low numbers (~350 patches, < 15k tris). Proceed?",
                "Yes, Place Clusters",
                "Cancel"))
            {
                manager.GenerateSparseGrassClusters();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }
        }

        GUI.backgroundColor = new Color(1.0f, 0.35f, 0.35f);
        if (GUILayout.Button("🗑️ Erase / Clear All Grass", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Clear All Grass Data",
                "Are you sure you want to completely erase all grass? This will give you a clean slate to paint grass wherever you like.",
                "Yes, Erase All Grass",
                "Cancel"))
            {
                manager.ClearGrassData();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Bake Button
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("🌿 Bake Grass Across Landscape (Exclude Mountain)", GUILayout.Height(34)))
        {
            if (EditorUtility.DisplayDialog(
                "Bake Landscape Grass",
                "This will scan the landscape and bake grass instances across the entire area (excluding mountain). Proceed?",
                "Yes, Bake Grass",
                "Cancel"))
            {
                manager.BakeGrass();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        // Hierarchy GameObjects Controls
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📦 Spawn as GameObjects", GUILayout.Height(26)))
        {
            manager.SpawnGameObjectsFromData();
        }
        if (GUILayout.Button("🗑 Clear Spawned GameObjects", GUILayout.Height(26)))
        {
            manager.ClearSpawnedGameObjects();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        DrawDefaultInspector();

        EditorGUILayout.Space(6);
        if (GUI.changed)
        {
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        var manager = (LandscapeGrassManager)target;
        if (manager == null || manager.grassData == null) return;
        if (currentBrushMode == BrushMode.Off) return;

        Event e = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        // Shortcut to resize brush: [ and ]
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftBracket)
            {
                brushRadius = Mathf.Max(1f, brushRadius - 1f);
                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.RightBracket)
            {
                brushRadius = Mathf.Min(30f, brushRadius + 1f);
                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
        }

        // Raycast from mouse onto terrain
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var hits = Physics.RaycastAll(ray, 500f);
        hasValidTerrainHit = false;
        RaycastHit bestHit = default(RaycastHit);
        float closestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null && hits[i].collider.name.StartsWith("Terrain_Tile"))
            {
                if (hits[i].distance < closestDist)
                {
                    closestDist = hits[i].distance;
                    bestHit = hits[i];
                    hasValidTerrainHit = true;
                }
            }
        }

        if (hasValidTerrainHit)
        {
            lastMouseWorldPos = bestHit.point;
            lastMouseNormal = bestHit.normal;

            bool isCtrlHeld = e.control || e.command;
            bool isErasing = (currentBrushMode == BrushMode.Erase) || (currentBrushMode == BrushMode.Paint && isCtrlHeld);

            // Draw Brush in Scene View
            Color discColor = isErasing ? new Color(1f, 0.2f, 0.2f, 0.25f) : new Color(0.2f, 1f, 0.2f, 0.25f);
            Color wireColor = isErasing ? Color.red : Color.green;

            Handles.color = discColor;
            Handles.DrawSolidDisc(lastMouseWorldPos, lastMouseNormal, brushRadius);
            Handles.color = wireColor;
            Handles.DrawWireDisc(lastMouseWorldPos, lastMouseNormal, brushRadius);

            // Handle painting on click or drag
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                GUIUtility.hotControl = controlID;
                Undo.RecordObject(manager.grassData, isErasing ? "Erase Grass" : "Paint Grass");

                if (isErasing)
                {
                    manager.EraseGrassAt(lastMouseWorldPos, brushRadius);
                }
                else
                {
                    manager.PaintGrassAt(lastMouseWorldPos, brushRadius, brushSpacing, brushMinScale, brushMaxScale, brushAlignNormal);
                }

                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    EditorUtility.SetDirty(manager.grassData);
                    AssetDatabase.SaveAssets();
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseMove)
            {
                SceneView.RepaintAll();
            }

            // In-Scene GUI Label
            Handles.BeginGUI();
            string modeText = isErasing ? "🗑️ ERASE GRASS (Hold Left-Click)" : "🌿 PAINT GRASS (Left-Click to Add | Hold Ctrl to Erase)";
            var rect = new Rect(10, 10, 360, 48);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(16, 14, 348, 40));
            GUILayout.Label(modeText, EditorStyles.boldLabel);
            GUILayout.Label($"Radius: {brushRadius:F1}m ([ and ] to resize) | Spacing: {brushSpacing:F1}m", EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        if (currentBrushMode != BrushMode.Off)
        {
            HandleUtility.AddDefaultControl(controlID);
        }
    }
}
