using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class WaterBody : MonoBehaviour
{
    public static readonly List<WaterBody> All = new List<WaterBody>();

    [Header("Water Body Configuration")]
    [Tooltip("Set to true for flat lakes/ponds where water surface Y is constant.")]
    public bool isFlatSurface = true;

    [Tooltip("Constant surface Y in world coordinates for flat water bodies.")]
    public float flatSurfaceY = -2.38f;

    [Tooltip("Downward current or flow direction and speed (units/sec), if any.")]
    public Vector3 currentFlow = Vector3.zero;

    private Collider waterCollider;
    private MeshFilter meshFilter;
    private Bounds worldBounds;

    public Collider WaterCollider => waterCollider;
    public Bounds WorldBounds => worldBounds;

    private void Awake()
    {
        waterCollider = GetComponent<Collider>();
        meshFilter = GetComponent<MeshFilter>();
        EnsureWaterLayer();
        UpdateBounds();
        IgnoreCollisionWithActivePlayers();
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
        UpdateBounds();
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void EnsureWaterLayer()
    {
        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer != -1 && gameObject.layer != waterLayer)
        {
            gameObject.layer = waterLayer;
        }
    }

    public void UpdateBounds()
    {
        if (waterCollider != null && waterCollider.bounds.size.sqrMagnitude > 0.01f)
        {
            worldBounds = waterCollider.bounds;
        }
        else if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds b = meshFilter.sharedMesh.bounds;
            Vector3 worldCenter = transform.TransformPoint(b.center);
            Vector3 worldSize = Vector3.Scale(b.size, transform.lossyScale);
            worldBounds = new Bounds(worldCenter, worldSize);
        }
    }

    public void IgnoreCollisionWithActivePlayers()
    {
        var controllers = Object.FindObjectsByType<CharacterController>();
        foreach (var cc in controllers)
        {
            if (cc != null && waterCollider != null)
            {
                Physics.IgnoreCollision(cc, waterCollider, true);
            }
        }
    }

    public bool IsPositionInHorizontalBounds(Vector3 pos, float margin = 0f)
    {
        if (waterCollider == null) waterCollider = GetComponent<Collider>();
        Bounds b = waterCollider != null ? waterCollider.bounds : worldBounds;
        return pos.x >= b.min.x - margin && pos.x <= b.max.x + margin &&
               pos.z >= b.min.z - margin && pos.z <= b.max.z + margin;
    }

    public bool TryGetWaterSurfaceY(Vector3 worldPos, out float surfaceY)
    {
        surfaceY = 0f;

        if (waterCollider == null)
        {
            waterCollider = GetComponent<Collider>();
        }

        if (waterCollider == null)
        {
            return false;
        }

        // 1. Broadphase check: AABB with 0 margin
        Bounds bounds = waterCollider.bounds;
        if (worldPos.x < bounds.min.x || worldPos.x > bounds.max.x ||
            worldPos.z < bounds.min.z || worldPos.z > bounds.max.z)
        {
            return false;
        }

        // 2. Exact narrowphase check:
        // Raycast straight down onto the water mesh collider.
        // This ensures the player is strictly inside the exact polygon outline of the water
        // and eliminates any false slow zones or margins on the dry shoreline.
        float rayStartY = Mathf.Max(bounds.max.y + 6f, worldPos.y + 6f);
        Ray ray = new Ray(new Vector3(worldPos.x, rayStartY, worldPos.z), Vector3.down);
        float maxDist = Mathf.Max(bounds.size.y + 15f, 40f);

        if (waterCollider.Raycast(ray, out RaycastHit hit, maxDist))
        {
            surfaceY = isFlatSurface ? flatSurfaceY : hit.point.y;
            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
        if (waterCollider != null)
        {
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
        }
    }
}
