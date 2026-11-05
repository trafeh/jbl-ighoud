using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Landscape
{
    public class TerrainTexturePainter : EditorWindow
    {
        public enum PaintLayer
        {
            Grass = 0, // Green channel
            Rock = 1,  // Blue channel
            Dirt = 2   // Red channel
        }

        public enum PaintAction
        {
            Paint = 0,
            Smooth = 1
        }

        [SerializeField] private bool isPaintingEnabled = false;
        [SerializeField] private PaintLayer activeLayer = PaintLayer.Grass;
        [SerializeField] private PaintAction activeAction = PaintAction.Paint;
        [SerializeField] private float brushRadius = 8.0f;
        [SerializeField] private float brushStrength = 0.5f;
        [SerializeField] private float brushFalloff = 0.5f;

        private Vector3 mouseHitPoint;
        private Vector3 mouseHitNormal = Vector3.up;
        private bool hasMouseHit = false;

        private List<MeshFilter> cachedTerrainTiles = new List<MeshFilter>();
        private bool isStrokeActive = false;
        private HashSet<Mesh> modifiedMeshesInStroke = new HashSet<Mesh>();

        [MenuItem("Tools/Landscape/Terrain Texture Painter", false, 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<TerrainTexturePainter>("Terrain Painter");
            window.minSize = new Vector2(320, 420);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshTerrainTiles();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (modifiedMeshesInStroke.Count > 0)
            {
                AssetDatabase.SaveAssets();
                modifiedMeshesInStroke.Clear();
            }
        }

        public void RefreshTerrainTiles()
        {
            cachedTerrainTiles.Clear();
            var env = GameObject.Find("env");
            if (env != null)
            {
                var mfs = env.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                {
                    if (mf.name.StartsWith("Terrain_Tile_") && mf.sharedMesh != null)
                    {
                        cachedTerrainTiles.Add(mf);
                    }
                }
            }

            if (cachedTerrainTiles.Count == 0)
            {
                // Fallback: search whole scene for Terrain_Tile
                var allMfs = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
                foreach (var mf in allMfs)
                {
                    if (mf.name.StartsWith("Terrain_Tile_") && mf.sharedMesh != null)
                    {
                        cachedTerrainTiles.Add(mf);
                    }
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🎨 Terrain Texture Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paint Grass, Rock, and Dirt textures directly onto the terrain mesh in Scene View.\n" +
                "Weights are stored in mesh vertex colors and blended by the URP Vertex Blend Shader.",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            // --- TOGGLE PAINTING ---
            GUI.backgroundColor = isPaintingEnabled ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.9f, 0.9f, 0.9f);
            if (GUILayout.Button(isPaintingEnabled ? "🟢 Painting ACTIVE (Click to Disable)" : "⚪ Start Painting in Scene View", GUILayout.Height(36)))
            {
                isPaintingEnabled = !isPaintingEnabled;
                if (isPaintingEnabled) RefreshTerrainTiles();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (cachedTerrainTiles.Count == 0)
            {
                EditorGUILayout.HelpBox("No Terrain_Tile_* meshes found. Click 'Refresh Tiles'.", MessageType.Warning);
                if (GUILayout.Button("🔄 Refresh Terrain Tiles")) RefreshTerrainTiles();
                return;
            }

            EditorGUILayout.Space(8);

            // --- LAYER SELECTION ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Select Paint Texture Layer", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Grass (Green)
            GUI.backgroundColor = activeLayer == PaintLayer.Grass ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("🌿 Grass\n(Herbe)", GUILayout.Height(42)))
            {
                activeLayer = PaintLayer.Grass;
                activeAction = PaintAction.Paint;
            }

            // Rock (Blue/Gray)
            GUI.backgroundColor = activeLayer == PaintLayer.Rock ? new Color(0.6f, 0.8f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("🪨 Rock\n(Rocher)", GUILayout.Height(42)))
            {
                activeLayer = PaintLayer.Rock;
                activeAction = PaintAction.Paint;
            }

            // Dirt / Road (Red/Brown)
            GUI.backgroundColor = activeLayer == PaintLayer.Dirt ? new Color(1.0f, 0.75f, 0.45f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Button("🛣️ Road / Path\n(Route / Sentier)", GUILayout.Height(42)))
            {
                activeLayer = PaintLayer.Dirt;
                activeAction = PaintAction.Paint;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Action Mode: Paint vs Smooth
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = activeAction == PaintAction.Paint ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Toggle(activeAction == PaintAction.Paint, "🖌️ Paint Mode", "Button", GUILayout.Height(24)))
            {
                activeAction = PaintAction.Paint;
            }

            GUI.backgroundColor = activeAction == PaintAction.Smooth ? new Color(0.7f, 0.9f, 1.0f) : new Color(0.85f, 0.85f, 0.85f);
            if (GUILayout.Toggle(activeAction == PaintAction.Smooth, "💧 Smooth Blend", "Button", GUILayout.Height(24)))
            {
                activeAction = PaintAction.Smooth;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // --- BRUSH SETTINGS ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Brush Settings", EditorStyles.boldLabel);

            brushRadius = EditorGUILayout.Slider("Brush Radius (m)", brushRadius, 1.0f, 40.0f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.05f, 1.0f);
            brushFalloff = EditorGUILayout.Slider("Brush Softness", brushFalloff, 0.0f, 1.0f);

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "Shortcuts:\n" +
                "• [ and ] : Decrease / Increase brush radius\n" +
                "• Shift + Drag : Smooth blend mode\n" +
                "• Ctrl + Z : Instant Undo",
                MessageType.None
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // --- TILE STATS & SAVE ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Active Terrain Tiles ({cachedTerrainTiles.Count})", EditorStyles.miniBoldLabel);
            foreach (var mf in cachedTerrainTiles)
            {
                if (mf != null && mf.sharedMesh != null)
                {
                    EditorGUILayout.LabelField($"• {mf.name}: {mf.sharedMesh.vertexCount:N0} vertices", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("💾 Save Painted Meshes to Disk", GUILayout.Height(28)))
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Saved", "Terrain mesh vertex colors saved successfully!", "OK");
            }
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPaintingEnabled) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            // Handle [ and ] radius hotkeys
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.LeftBracket)
                {
                    brushRadius = Mathf.Max(1.0f, brushRadius - 1.5f);
                    Repaint();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightBracket)
                {
                    brushRadius = Mathf.Min(40.0f, brushRadius + 1.5f);
                    Repaint();
                    e.Use();
                }
            }

            // Raycast terrain
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            hasMouseHit = false;

            if (Physics.Raycast(ray, out RaycastHit hit, 2000f))
            {
                if (hit.collider.name.StartsWith("Terrain_Tile_"))
                {
                    hasMouseHit = true;
                    mouseHitPoint = hit.point;
                    mouseHitNormal = hit.normal;
                }
            }

            // Draw Brush Disc in Scene View
            if (hasMouseHit)
            {
                Color discColor;
                if (activeAction == PaintAction.Smooth || e.shift)
                {
                    discColor = new Color(0.4f, 0.8f, 1.0f, 0.9f);
                }
                else
                {
                    switch (activeLayer)
                    {
                        case PaintLayer.Grass: discColor = new Color(0.2f, 1.0f, 0.2f, 0.9f); break;
                        case PaintLayer.Rock: discColor = new Color(0.6f, 0.7f, 1.0f, 0.9f); break;
                        default: discColor = new Color(1.0f, 0.6f, 0.2f, 0.9f); break;
                    }
                }

                Handles.color = discColor;
                Handles.DrawWireDisc(mouseHitPoint, mouseHitNormal, brushRadius);
                Handles.color = new Color(discColor.r, discColor.g, discColor.b, 0.12f);
                Handles.DrawSolidDisc(mouseHitPoint, mouseHitNormal, brushRadius);

                // Inner core radius
                Handles.color = new Color(discColor.r, discColor.g, discColor.b, 0.4f);
                Handles.DrawWireDisc(mouseHitPoint, mouseHitNormal, brushRadius * (1f - brushFalloff));

                SceneView.RepaintAll();
            }

            // Prevent object deselection during painting
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }

            // Left-Click or Drag
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && hasMouseHit && !e.alt)
            {
                bool doSmooth = (activeAction == PaintAction.Smooth) || e.shift;
                ApplyBrushStroke(mouseHitPoint, brushRadius, brushStrength * 0.2f, brushFalloff, activeLayer, doSmooth);
                isStrokeActive = true;
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0 && isStrokeActive)
            {
                isStrokeActive = false;
                AssetDatabase.SaveAssets();
            }
        }

        private void ApplyBrushStroke(Vector3 worldCenter, float radius, float strength, float falloff, PaintLayer layer, bool smooth)
        {
            if (cachedTerrainTiles.Count == 0) RefreshTerrainTiles();

            float sqrRadius = radius * radius;
            float innerRadius = radius * (1.0f - falloff);

            foreach (var mf in cachedTerrainTiles)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                var mesh = mf.sharedMesh;
                var tr = mf.transform;

                // Check bounding box intersection
                Bounds worldBounds = new Bounds(tr.TransformPoint(mesh.bounds.center), Vector3.Scale(mesh.bounds.size, tr.lossyScale));
                worldBounds.Expand(radius * 2f);
                if (!worldBounds.Contains(worldCenter)) continue;

                var vertices = mesh.vertices;
                var colors = mesh.colors;
                if (colors == null || colors.Length != vertices.Length)
                {
                    colors = new Color[vertices.Length];
                    for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 1, 0, 1);
                }

                bool meshChanged = false;

                // Register Undo on first modification
                if (!modifiedMeshesInStroke.Contains(mesh))
                {
                    Undo.RegisterCompleteObjectUndo(mesh, "Paint Terrain Texture");
                    modifiedMeshesInStroke.Add(mesh);
                }

                // Compute local center
                Vector3 localCenter = tr.InverseTransformPoint(worldCenter);
                float localRadius = radius / tr.lossyScale.x;
                float localSqrRadius = localRadius * localRadius;

                for (int i = 0; i < vertices.Length; i++)
                {
                    float d2 = (vertices[i] - localCenter).sqrMagnitude;
                    if (d2 > localSqrRadius) continue;

                    float dist = Mathf.Sqrt(d2);
                    float factor = 1.0f;
                    if (dist > innerRadius)
                    {
                        float t = (dist - innerRadius) / Mathf.Max(0.001f, radius - innerRadius);
                        factor = Mathf.Cos(t * Mathf.PI * 0.5f);
                    }

                    float applyStrength = Mathf.Clamp01(strength * factor);
                    Color c = colors[i];

                    if (smooth)
                    {
                        // Smooth towards uniform blend
                        float avg = (c.r + c.g + c.b) / 3f;
                        c.r = Mathf.Lerp(c.r, avg, applyStrength * 0.5f);
                        c.g = Mathf.Lerp(c.g, avg, applyStrength * 0.5f);
                        c.b = Mathf.Lerp(c.b, avg, applyStrength * 0.5f);
                    }
                    else
                    {
                        // Paint target layer
                        switch (layer)
                        {
                            case PaintLayer.Grass: // Green
                                c.g = Mathf.Lerp(c.g, 1.0f, applyStrength);
                                c.r = Mathf.Lerp(c.r, 0.0f, applyStrength);
                                c.b = Mathf.Lerp(c.b, 0.0f, applyStrength);
                                break;
                            case PaintLayer.Rock: // Blue
                                c.b = Mathf.Lerp(c.b, 1.0f, applyStrength);
                                c.g = Mathf.Lerp(c.g, 0.0f, applyStrength);
                                c.r = Mathf.Lerp(c.r, 0.0f, applyStrength);
                                break;
                            case PaintLayer.Dirt: // Red
                                c.r = Mathf.Lerp(c.r, 1.0f, applyStrength);
                                c.g = Mathf.Lerp(c.g, 0.0f, applyStrength);
                                c.b = Mathf.Lerp(c.b, 0.0f, applyStrength);
                                break;
                        }
                    }

                    // Normalize weights so R + G + B = 1.0
                    float total = c.r + c.g + c.b;
                    if (total > 0.001f)
                    {
                        c.r /= total;
                        c.g /= total;
                        c.b /= total;
                    }
                    else
                    {
                        c.g = 1.0f;
                    }
                    c.a = 1.0f;

                    colors[i] = c;
                    meshChanged = true;
                }

                if (meshChanged)
                {
                    mesh.colors = colors;
                    EditorUtility.SetDirty(mesh);
                }
            }
        }
    }
}
