using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[SelectionBase]
public class LandscapeGrassManager : MonoBehaviour
{
    private static LandscapeGrassManager _instance;
    public static LandscapeGrassManager Instance => _instance;

    [Header("Grass Mode (Static Painted vs Dynamic Follower)")]
    [Tooltip("If true, dynamic grass patches follow the player within a circular radius. If false, static painted/baked grass is used (does not move with player).")]
    public bool useMobileDynamicGrid = false;

    [Tooltip("Strict maximum triangle ceiling for all grass rendering (never exceeds this budget).")]
    public int maxTriangleBudget = 45000;

    [Tooltip("Radius around the player to spawn dynamic grass patches in meters.")]
    [Range(15f, 60f)]
    public float dynamicRadius = 32f;

    [Tooltip("Distance between dynamic grass sample points in meters.")]
    [Range(1.6f, 4.0f)]
    public float dynamicSpacing = 2.2f;

    [Tooltip("Cast shadows from dynamic grass on mobile (recommend false for 60 FPS).")]
    public bool mobileCastShadows = false;

    [Tooltip("Receive shadows on dynamic grass.")]
    public bool mobileReceiveShadows = true;

    [Tooltip("Cave interior bounding box where grass must NEVER appear.")]
    public Bounds caveExclusionBounds = new Bounds(new Vector3(115.88f, 20.72f, -179.12f), new Vector3(50f, 25f, 75f));

    [Header("Assets")]
    [Tooltip("Baked grass instance data asset (optional / legacy).")]
    public LandscapeGrassData grassData;

    [Tooltip("Grass patch mesh.")]
    public Mesh grassMesh;

    [Tooltip("Grass material (Nyxis URP Grass).")]
    public Material grassMaterial;

    [Tooltip("Optional grass prefab for GameObject spawning mode.")]
    public GameObject grassPrefab;

    [Header("Rendering Settings (Baked Mode)")]
    [Tooltip("Maximum distance from camera to draw grass patches in baked mode.")]
    [Range(30f, 200f)]
    public float maxDrawDistance = 100f;

    [Tooltip("Cast shadows from baked grass.")]
    public bool castShadows = false;

    [Tooltip("Receive shadows on baked grass.")]
    public bool receiveShadows = true;

    [Tooltip("Render grass in Scene View while editing.")]
    public bool renderInEditMode = true;

    [Header("Player Interaction")]
    [Tooltip("Transform of the player for grass bend interaction and dynamic following.")]
    public Transform playerTransform;

    [Tooltip("Radius around the player in which grass bends away.")]
    [Range(0.5f, 5f)]
    public float playerInteractionRadius = 2.2f;

    [Tooltip("Smoothing time for player interaction position.")]
    public float playerPushSmoothTime = 0.05f;

    [Header("Bake & Environment Filters")]
    [Tooltip("Distance between grass sample points for baking.")]
    [Range(1.5f, 6f)]
    public float gridSpacing = 2.4f;

    [Tooltip("Size of each spatial chunk in meters.")]
    [Range(20f, 80f)]
    public float chunkSize = 40f;

    [Tooltip("Minimum random scale multiplier.")]
    public float minScale = 0.95f;

    [Tooltip("Maximum random scale multiplier.")]
    public float maxScale = 1.35f;

    [Tooltip("Maximum walkable slope angle for grass placement.")]
    [Range(20f, 60f)]
    public float maxSlopeAngle = 36f;

    [Tooltip("Global altitude ceiling above which terrain is considered mountain.")]
    public float maxMountainAltitude = 30f;

    [Tooltip("World Z coordinate where the southern mountain range begins.")]
    public float southMountainZThreshold = -95f;

    [Tooltip("Maximum allowed altitude in the southern mountain zone.")]
    public float southMountainMaxAltitude = 16f;

    [Tooltip("Water surface altitude. Points below this are excluded as underwater.")]
    public float waterSurfaceY = -2.25f;

    [Tooltip("Upper river maximum water level.")]
    public float upperRiverMaxY = 7.05f;

    [Tooltip("Layers to consider as solid obstacles blocking grass (e.g. big rocks/props).")]
    public LayerMask obstacleMask = 0;

    [Header("Performance Stats (Mobile Safe: <50k Tris)")]
    [SerializeField] private int activeInstancesRendered;
    [SerializeField] private int activeTrianglesRendered;
    [SerializeField] private int instancedDrawCalls;

    // Runtime Dynamic Follower State
    private Matrix4x4[] dynamicMatrices = new Matrix4x4[1023];
    private int dynamicMatrixCount = 0;
    private Vector3 lastGridCenter = new Vector3(-9999f, -9999f, -9999f);

    // Player Interaction State
    private Vector3 smoothedPlayerPos;
    private Vector3 playerVelocity;
    private static readonly int GlobalPlayerPosID = Shader.PropertyToID("_GlobalPlayerPos");
    private static readonly int GlobalPlayerRadiusID = Shader.PropertyToID("_GlobalPlayerRadius");

    private void Awake()
    {
        _instance = this;
        InitializeReferences();
    }

    private void OnEnable()
    {
        _instance = this;
        InitializeReferences();
        lastGridCenter = new Vector3(-9999f, -9999f, -9999f);
    }

    private void Start()
    {
        InitializeReferences();
        if (playerTransform != null)
        {
            smoothedPlayerPos = playerTransform.position;
        }
        lastGridCenter = new Vector3(-9999f, -9999f, -9999f);
    }

    public void InitializeReferences()
    {
        if (playerTransform == null)
        {
            var pGo = GameObject.FindWithTag("Player");
            if (pGo != null) playerTransform = pGo.transform;
            else
            {
                var ybot = GameObject.Find("Y Bot");
                if (ybot != null) playerTransform = ybot.transform;
            }
        }

#if UNITY_EDITOR
        if (grassMesh == null)
        {
            grassMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Nyxis_GrassInteractionSystem/Mesh/Grass_Patch.fbx");
            if (grassMesh == null)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nyxis_GrassInteractionSystem/Mesh/Grass_Patch.fbx");
                if (fbx != null) grassMesh = fbx.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            }
        }

        if (grassMaterial == null)
        {
            grassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Nyxis_GrassInteractionSystem/Mat/Custom_URP_Genshin_Grass_Unity6.mat");
        }

        if (grassPrefab == null)
        {
            grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nyxis_GrassInteractionSystem/Prefab/Grass_Patch.prefab");
        }
#endif
    }

    private void Update()
    {
        UpdatePlayerInteraction();

        if (!Application.isPlaying && !renderInEditMode) return;
        if (grassMesh == null || grassMaterial == null) return;

        Camera cam = GetRenderingCamera();
        if (cam == null) return;

        if (useMobileDynamicGrid)
        {
            RenderDynamicFollowerGrass(cam);
        }
        else
        {
            RenderBakedGrassChunks(cam);
        }
    }

    private Camera GetRenderingCamera()
    {
        Camera cam = null;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                cam = SceneView.lastActiveSceneView.camera;
            }
        }
#endif
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = Camera.current;
        return cam;
    }

    private void UpdatePlayerInteraction()
    {
        if (playerTransform == null)
        {
            var pGo = GameObject.FindWithTag("Player");
            if (pGo != null) playerTransform = pGo.transform;
            else
            {
                var ybot = GameObject.Find("Y Bot");
                if (ybot != null) playerTransform = ybot.transform;
            }
        }

        if (playerTransform != null)
        {
            Vector3 targetPos = playerTransform.position;
            if (Application.isPlaying)
            {
                smoothedPlayerPos = Vector3.SmoothDamp(smoothedPlayerPos, targetPos, ref playerVelocity, playerPushSmoothTime);
            }
            else
            {
                smoothedPlayerPos = targetPos;
            }

            Shader.SetGlobalVector(GlobalPlayerPosID, smoothedPlayerPos);
            Shader.SetGlobalFloat(GlobalPlayerRadiusID, playerInteractionRadius);
        }
    }

    /// <summary>
    /// Renders dynamic follower grass around the player strictly under the 50k triangle ceiling.
    /// Uses 1 single GPU instanced draw call for optimal mobile performance (60 FPS).
    /// </summary>
    private void RenderDynamicFollowerGrass(Camera cam)
    {
        Vector3 targetCenter = playerTransform != null ? playerTransform.position : cam.transform.position;
        float spacing = Mathf.Max(1.6f, dynamicSpacing);

        // Snap to fixed grid intervals so grass patches do not shift or slide while player walks
        float snappedX = Mathf.Round(targetCenter.x / spacing) * spacing;
        float snappedZ = Mathf.Round(targetCenter.z / spacing) * spacing;
        Vector3 currentSnappedCenter = new Vector3(snappedX, targetCenter.y, snappedZ);

        // Rebuild matrix grid only when center has shifted by at least half spacing
        if (Vector3.Distance(new Vector3(snappedX, 0f, snappedZ), new Vector3(lastGridCenter.x, 0f, lastGridCenter.z)) >= spacing * 0.5f || dynamicMatrixCount == 0)
        {
            RebuildDynamicGrid(currentSnappedCenter, spacing);
            lastGridCenter = currentSnappedCenter;
        }

        if (dynamicMatrixCount == 0)
        {
            activeInstancesRendered = 0;
            activeTrianglesRendered = 0;
            instancedDrawCalls = 0;
            return;
        }

        // Draw GPU instanced batch in 1 draw call
        var shadowMode = mobileCastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        int layer = gameObject.layer;

        Graphics.DrawMeshInstanced(
            grassMesh,
            0,
            grassMaterial,
            dynamicMatrices,
            dynamicMatrixCount,
            null,
            shadowMode,
            mobileReceiveShadows,
            layer
        );

        int meshTris = grassMesh != null ? (grassMesh.triangles.Length / 3) : 36;
        activeInstancesRendered = dynamicMatrixCount;
        activeTrianglesRendered = dynamicMatrixCount * meshTris;
        instancedDrawCalls = 1;
    }

    private void RebuildDynamicGrid(Vector3 center, float spacing)
    {
        int meshTris = grassMesh != null ? (grassMesh.triangles.Length / 3) : 36;
        int maxAllowedInstances = Mathf.Min(1023, maxTriangleBudget / Mathf.Max(1, meshTris));

        float radius = dynamicRadius;
        float sqrRadius = radius * radius;
        float minSlopeCos = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
        int count = 0;

        for (float x = -radius; x <= radius; x += spacing)
        {
            for (float z = -radius; z <= radius; z += spacing)
            {
                if (x * x + z * z > sqrRadius) continue;
                if (count >= maxAllowedInstances) break;

                float worldX = center.x + x;
                float worldZ = center.z + z;

                // 1. Cave interior exclusion: never spawn grass inside the cave
                if (caveExclusionBounds.Contains(new Vector3(worldX, 20f, worldZ))) continue;

                // Deterministic seed based on integer grid cell to guarantee motionless jitter
                int ix = Mathf.FloorToInt(worldX * 10f);
                int iz = Mathf.FloorToInt(worldZ * 10f);
                int seed = (ix * 73856093) ^ (iz * 19349663);

                float jx = ((seed & 0xFF) / 255f - 0.5f) * spacing * 0.42f;
                float jz = (((seed >> 8) & 0xFF) / 255f - 0.5f) * spacing * 0.42f;
                float sampleX = worldX + jx;
                float sampleZ = worldZ + jz;

                // Secondary cave check on jittered point
                if (caveExclusionBounds.Contains(new Vector3(sampleX, 20f, sampleZ))) continue;

                // 2. Terrain Raycast
                Vector3 rayOrigin = new Vector3(sampleX, center.y + 25f, sampleZ);
                var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 60f);
                RaycastHit hit = default(RaycastHit);
                bool hitTerrain = false;

                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider != null && hits[h].collider.name.StartsWith("Terrain_Tile"))
                    {
                        hit = hits[h];
                        hitTerrain = true;
                        break;
                    }
                }
                if (!hitTerrain) continue;

                // 3. Slope check
                if (hit.normal.y < minSlopeCos) continue;

                // 4. Water altitude check
                if (hit.point.y <= waterSurfaceY) continue;

                // 5. Mountain altitude checks
                if (hit.point.y > maxMountainAltitude) continue;
                if (hit.point.z < southMountainZThreshold && hit.point.y > southMountainMaxAltitude) continue;

                // 6. Solid obstacle check (boulders / structures)
                if (obstacleMask != 0)
                {
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.25f, 0.4f, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                // Deterministic yaw and scale from seed
                float yaw = ((seed >> 4) & 0x1FF) * (360f / 512f);
                Quaternion normalRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                Quaternion randomYaw = Quaternion.Euler(0f, yaw, 0f);
                Quaternion rot = normalRot * randomYaw;

                float scaleNorm = ((seed >> 12) & 0x7F) / 127f;
                float s = Mathf.Lerp(minScale, maxScale, scaleNorm);
                Vector3 scale = new Vector3(s, s * 1.05f, s);

                dynamicMatrices[count] = Matrix4x4.TRS(hit.point, rot, scale);
                count++;
            }
            if (count >= maxAllowedInstances) break;
        }

        dynamicMatrixCount = count;
    }

    /// <summary>
    /// Renders pre-baked grass chunks with strict triangle cap enforcement for mobile safety.
    /// </summary>
    private void RenderBakedGrassChunks(Camera cam)
    {
        if (grassData == null || grassData.chunks == null || grassData.chunks.Count == 0)
        {
            activeInstancesRendered = 0;
            activeTrianglesRendered = 0;
            instancedDrawCalls = 0;
            return;
        }

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        Vector3 camPos = cam.transform.position;
        float sqrMaxDist = maxDrawDistance * maxDrawDistance;
        int layer = gameObject.layer;
        var shadowMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        int meshTris = grassMesh != null ? (grassMesh.triangles.Length / 3) : 36;
        int maxAllowedInstances = maxTriangleBudget / Mathf.Max(1, meshTris);

        int totalInstancesDrawn = 0;
        int totalDrawCalls = 0;

        for (int i = 0; i < grassData.chunks.Count; i++)
        {
            var chunk = grassData.chunks[i];
            if (chunk == null) continue;

            // Distance culling
            float sqrDist = chunk.bounds.SqrDistance(camPos);
            if (sqrDist > sqrMaxDist) continue;

            // Frustum culling
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds)) continue;

            // Draw GPU instanced batches
            var batches = chunk.MatrixBatches;
            if (batches == null) continue;

            for (int b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                if (batch == null || batch.Length == 0) continue;

                int batchCount = batch.Length;
                if (totalInstancesDrawn + batchCount > maxAllowedInstances)
                {
                    batchCount = maxAllowedInstances - totalInstancesDrawn;
                }
                if (batchCount <= 0) break;

                Graphics.DrawMeshInstanced(
                    grassMesh,
                    0,
                    grassMaterial,
                    batch,
                    batchCount,
                    null,
                    shadowMode,
                    receiveShadows,
                    layer
                );

                totalInstancesDrawn += batchCount;
                totalDrawCalls++;

                if (totalInstancesDrawn >= maxAllowedInstances) break;
            }

            if (totalInstancesDrawn >= maxAllowedInstances) break;
        }

        activeInstancesRendered = totalInstancesDrawn;
        activeTrianglesRendered = totalInstancesDrawn * meshTris;
        instancedDrawCalls = totalDrawCalls;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Paints grass instances inside a circular brush area onto terrain.
    /// <summary>
    /// Paints grass instances inside a circular brush area onto terrain.
    /// </summary>
    public int PaintGrassAt(Vector3 center, float radius, float spacing, float minS, float maxS, bool alignNormal)
    {
        if (grassData == null) return 0;

        float step = Mathf.Max(0.8f, spacing);
        float sqrRadius = radius * radius;
        var bucketMap = new Dictionary<LandscapeGrassData.GrassChunk, List<LandscapeGrassData.GrassInstance>>();
        int addedCount = 0;

        for (float x = center.x - radius; x <= center.x + radius; x += step)
        {
            for (float z = center.z - radius; z <= center.z + radius; z += step)
            {
                float jx = x + UnityEngine.Random.Range(-step * 0.35f, step * 0.35f);
                float jz = z + UnityEngine.Random.Range(-step * 0.35f, step * 0.35f);

                float dx = jx - center.x;
                float dz = jz - center.z;
                if (dx * dx + dz * dz > sqrRadius) continue;

                Vector3 rayOrigin = new Vector3(jx, 250f, jz);
                var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 300f);
                RaycastHit hit = default(RaycastHit);
                bool found = false;
                float closestDist = float.MaxValue;
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider != null && hits[h].collider.name.StartsWith("Terrain_Tile"))
                    {
                        if (hits[h].distance < closestDist)
                        {
                            closestDist = hits[h].distance;
                            hit = hits[h];
                            found = true;
                        }
                    }
                }
                if (!found) continue;

                // Cave exclusion
                if (caveExclusionBounds.Contains(hit.point)) continue;

                // Water altitude check
                if (hit.point.y <= waterSurfaceY) continue;

                if (grassData.HasInstanceNear(hit.point, step * 0.75f)) continue;

                Quaternion normalRot = alignNormal ? Quaternion.FromToRotation(Vector3.up, hit.normal) : Quaternion.identity;
                Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                Quaternion rot = normalRot * randomYaw;

                float s = UnityEngine.Random.Range(minS, maxS);
                Vector3 scale = new Vector3(s, s * UnityEngine.Random.Range(0.92f, 1.15f), s);

                var inst = new LandscapeGrassData.GrassInstance
                {
                    position = hit.point,
                    rotation = rot,
                    scale = scale
                };

                var chunk = grassData.FindOrCreateChunk(hit.point, chunkSize);
                if (!bucketMap.TryGetValue(chunk, out var list))
                {
                    list = new List<LandscapeGrassData.GrassInstance>();
                    bucketMap[chunk] = list;
                }
                list.Add(inst);
                addedCount++;
            }
        }

        foreach (var kvp in bucketMap)
        {
            kvp.Key.AddInstances(kvp.Value);
        }

        if (addedCount > 0)
        {
            grassData.RecalculateTotalInstances();
            EditorUtility.SetDirty(grassData);
        }

        return addedCount;
    }

    /// <summary>
    /// Erases grass instances inside a circular brush area.
    /// </summary>
    public int EraseGrassAt(Vector3 center, float radius)
    {
        if (grassData == null) return 0;
        int removed = grassData.EraseInstancesInRadius(center, radius);
        if (removed > 0)
        {
            EditorUtility.SetDirty(grassData);
        }
        return removed;
    }

    /// <summary>
    /// Completely clears all grass data, giving the user a 100% clean canvas to paint from scratch.
    /// </summary>
    public void ClearGrassData()
    {
        if (grassData != null)
        {
            Undo.RecordObject(grassData, "Clear Grass Data");
            if (grassData.chunks != null)
            {
                grassData.chunks.Clear();
            }
            grassData.totalInstances = 0;
            grassData.InvalidateAllMatrices();
            EditorUtility.SetDirty(grassData);
            AssetDatabase.SaveAssets();
        }
        activeInstancesRendered = 0;
        activeTrianglesRendered = 0;
        instancedDrawCalls = 0;
    }

    /// <summary>
    /// Places small, sparse aesthetic clusters of grass in selective spots ('here and there')
    /// across the landscape (e.g. near the river, lake, meadow trail). Low triangle count (~12k-15k tris).
    /// </summary>
    public void GenerateSparseGrassClusters()
    {
        ClearGrassData();

        Vector2[] clusterCenters = new Vector2[]
        {
            new Vector2(122f, -125f), // Cluster 1: Outside Cave approach meadow
            new Vector2(95f, -85f),   // Cluster 2: Foothills trail bend
            new Vector2(40f, -40f),   // Cluster 3: Upper river meadow
            new Vector2(15f, -10f),   // Cluster 4: Mid-river grassy bank
            new Vector2(-35f, 50f),   // Cluster 5: Lake eastern shore oasis
            new Vector2(-60f, 75f),   // Cluster 6: Lake northern shore
            new Vector2(45f, 45f),    // Cluster 7: Central plateau clearing
        };

        float clusterRadius = 10f;
        float spacing = 2.4f;

        for (int i = 0; i < clusterCenters.Length; i++)
        {
            Vector3 center = new Vector3(clusterCenters[i].x, 20f, clusterCenters[i].y);
            PaintGrassAt(center, clusterRadius, spacing, 0.9f, 1.3f, true);
        }

        Debug.Log($"[LandscapeGrassManager] Placed sparse aesthetic clusters: {grassData.totalInstances} total patches.");
    }

    /// <summary>
    /// Scans the landscape terrain and bakes grass instances strictly under budget.
    /// </summary>
    public void BakeGrass()
    {
        InitializeReferences();

        if (grassData == null)
        {
            string assetPath = "Assets/Settings/LandscapeGrassData.asset";
            grassData = AssetDatabase.LoadAssetAtPath<LandscapeGrassData>(assetPath);
            if (grassData == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }
                grassData = ScriptableObject.CreateInstance<LandscapeGrassData>();
                AssetDatabase.CreateAsset(grassData, assetPath);
                AssetDatabase.SaveAssets();
            }
        }

        Undo.RecordObject(grassData, "Bake Landscape Grass");

        float minX = -335f;
        float maxX = 345f;
        float minZ = -335f;
        float maxZ = 345f;

        float spacing = Mathf.Max(2.0f, gridSpacing);
        float minSlopeCos = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        int chunkCols = Mathf.CeilToInt((maxX - minX) / chunkSize);
        int chunkRows = Mathf.CeilToInt((maxZ - minZ) / chunkSize);
        var chunkBuckets = new List<LandscapeGrassData.GrassInstance>[chunkCols, chunkRows];

        for (int c = 0; c < chunkCols; c++)
        {
            for (int r = 0; r < chunkRows; r++)
            {
                chunkBuckets[c, r] = new List<LandscapeGrassData.GrassInstance>();
            }
        }

        int placedCount = 0;
        Bounds caveBounds = caveExclusionBounds;

        for (float x = minX; x <= maxX; x += spacing)
        {
            for (float z = minZ; z <= maxZ; z += spacing)
            {
                float jx = x + UnityEngine.Random.Range(-spacing * 0.42f, spacing * 0.42f);
                float jz = z + UnityEngine.Random.Range(-spacing * 0.42f, spacing * 0.42f);

                if (caveBounds.Contains(new Vector3(jx, 20f, jz))) continue;

                Vector3 rayOrigin = new Vector3(jx, 250f, jz);
                var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 300f);
                RaycastHit hit = default(RaycastHit);
                bool hitTerrain = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider != null && hits[h].collider.name.StartsWith("Terrain_Tile"))
                    {
                        hit = hits[h];
                        hitTerrain = true;
                        break;
                    }
                }
                if (!hitTerrain) continue;

                if (hit.normal.y < minSlopeCos) continue;

                RaycastHit waterHit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out waterHit, 300f, 1 << 4))
                {
                    if (waterHit.point.y >= hit.point.y - 0.25f) continue;
                }
                if (hit.point.y <= waterSurfaceY) continue;

                if (hit.point.y > maxMountainAltitude) continue;
                if (hit.point.z < southMountainZThreshold && hit.point.y > southMountainMaxAltitude) continue;

                if (obstacleMask != 0)
                {
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.25f, 0.45f, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                Quaternion normalRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                Quaternion randomYaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                Quaternion finalRot = normalRot * randomYaw;

                float uniformScale = UnityEngine.Random.Range(minScale, maxScale);
                float heightScale = uniformScale * UnityEngine.Random.Range(0.92f, 1.15f);
                Vector3 finalScale = new Vector3(uniformScale, heightScale, uniformScale);

                var instance = new LandscapeGrassData.GrassInstance
                {
                    position = hit.point,
                    rotation = finalRot,
                    scale = finalScale
                };

                int col = Mathf.Clamp(Mathf.FloorToInt((jx - minX) / chunkSize), 0, chunkCols - 1);
                int row = Mathf.Clamp(Mathf.FloorToInt((jz - minZ) / chunkSize), 0, chunkRows - 1);
                chunkBuckets[col, row].Add(instance);
                placedCount++;
            }
        }

        grassData.chunks = new List<LandscapeGrassData.GrassChunk>();
        for (int c = 0; c < chunkCols; c++)
        {
            for (int r = 0; r < chunkRows; r++)
            {
                var list = chunkBuckets[c, r];
                if (list.Count == 0) continue;

                var chunk = new LandscapeGrassData.GrassChunk();
                chunk.instances = list.ToArray();

                Vector3 bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < list.Count; i++)
                {
                    Vector3 p = list[i].position;
                    bMin = Vector3.Min(bMin, p - Vector3.one * 0.5f);
                    bMax = Vector3.Max(bMax, p + Vector3.one * 1.5f);
                }
                chunk.bounds = new Bounds((bMin + bMax) * 0.5f, bMax - bMin);
                chunk.PrepareMatrices();
                grassData.chunks.Add(chunk);
            }
        }

        grassData.totalInstances = placedCount;
        grassData.bakedSpacing = spacing;
        grassData.bakedTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        EditorUtility.SetDirty(grassData);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LandscapeGrassManager] Successfully baked {placedCount:N0} grass patches into {grassData.chunks.Count} chunks.");
    }

    public void SpawnGameObjectsFromData()
    {
        ClearSpawnedGameObjects();

        if (grassData == null || grassData.chunks == null || grassData.chunks.Count == 0)
        {
            Debug.LogWarning("[LandscapeGrassManager] No grass data to spawn GameObjects from! Bake first.");
            return;
        }

        var root = new GameObject("Grass_Chunks_Hierarchy");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        int chunkIndex = 0;
        foreach (var chunk in grassData.chunks)
        {
            if (chunk == null || chunk.instances == null || chunk.instances.Length == 0) continue;

            var chunkGo = new GameObject($"GrassChunk_{chunkIndex:D3}");
            chunkGo.transform.SetParent(root.transform);
            chunkGo.transform.localPosition = Vector3.zero;

            for (int i = 0; i < chunk.instances.Length; i++)
            {
                var inst = chunk.instances[i];
                GameObject patchGo;
                if (grassPrefab != null)
                {
                    patchGo = (GameObject)PrefabUtility.InstantiatePrefab(grassPrefab, chunkGo.transform);
                }
                else
                {
                    patchGo = new GameObject("Grass_Patch");
                    patchGo.transform.SetParent(chunkGo.transform);
                    var mf = patchGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = grassMesh;
                    var mr = patchGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = grassMaterial;
                }

                patchGo.transform.position = inst.position;
                patchGo.transform.rotation = inst.rotation;
                patchGo.transform.localScale = inst.scale;
            }
            chunkIndex++;
        }

        Undo.RegisterCreatedObjectUndo(root, "Spawn Grass GameObjects");
        Debug.Log($"[LandscapeGrassManager] Spawned {grassData.totalInstances:N0} GameObjects across {chunkIndex} chunks under Grass_Chunks_Hierarchy.");
    }

    public void ClearSpawnedGameObjects()
    {
        var existing = transform.Find("Grass_Chunks_Hierarchy");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }
#endif
}
