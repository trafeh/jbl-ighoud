using UnityEngine;
using Unity.Cinemachine;

namespace JbelIghoud.CameraControl
{
#pragma warning disable CS0618
    /// <summary>
    /// Smoothly transitions the Cinemachine FreeLook Camera between outdoor wide landscape
    /// and indoor cave framing to eliminate ceiling/wall collisions, prevent clipping,
    /// and deliver a buttery-smooth third-person interior experience.
    /// </summary>
    [ExecuteAlways]
    public class CaveCameraSmoothController : MonoBehaviour
    {
#pragma warning disable CS0618
        [Header("Cinemachine Target")]
        [Tooltip("Target CinemachineCamera with OrbitalFollow.")]
        public CinemachineCamera virtualCamera;
        public CinemachineOrbitalFollow orbitalFollow;
        public CinemachineCollider cinemachineCollider;
#pragma warning restore CS0618

        [Header("Player Reference")]
        public Transform playerTransform;

        [Header("Wall Collision & Anti-Clipping (Editable in Unity)")]
        [Tooltip("Automatically optimize CinemachineCollider to prevent wall clipping and allow camera to go inside character.")]
        public bool autoOptimizeCollider = true;

        [Tooltip("Minimum distance from target. Set to 0 to allow camera inside character rather than popping behind walls outside the world.")]
        [Range(0f, 1f)]
        public float minDistanceFromTarget = 0.0f;

        [Tooltip("Collision sphere radius. Narrower radius fits against walls and corners smoothly.")]
        [Range(0.01f, 0.4f)]
        public float cameraCollisionRadius = 0.08f;

        [Tooltip("Damping when moving away from obstacles.")]
        [Range(0f, 0.5f)]
        public float collisionDamping = 0.02f;

        [Tooltip("Damping when hitting an obstacle. 0 for instantaneous reaction to prevent wall penetration.")]
        public float dampingWhenOccluded = 0.0f;

        [Tooltip("Smoothing time for collision resolution.")]
        public float collisionSmoothingTime = 0.01f;

        [Tooltip("Collision layers for walls/environment.")]
        public LayerMask wallCollisionLayers = ~((1 << 2) | (1 << 3));

        [Header("Character Interior Optimization")]
        [Tooltip("When camera pushes inside character due to wall collision, smoothly hide the character mesh (keeps shadow) for an unobstructed view.")]
        public bool hideMeshWhenInsideCharacter = true;

        [Tooltip("Distance threshold between camera and target to hide character mesh.")]
        [Range(0.1f, 1.2f)]
        public float characterHideDistance = 0.45f;

        private Renderer[] cachedPlayerRenderers;
        private bool isMeshHidden = false;

        [Header("Cave Region Definition")]
        [Tooltip("Bounding box of the cave interior.")]
        public Bounds caveInteriorBounds = new Bounds(new Vector3(115.88f, 19.32f, -181.37f), new Vector3(48f, 12f, 74f));

        [Tooltip("Outside entrance reference point.")]
        public Vector3 entrancePoint = new Vector3(126.88f, 17.5f, -142.0f);

        [Tooltip("Inside entrance reference point.")]
        public Vector3 tunnelPoint = new Vector3(126.88f, 17.5f, -162.0f);

        [Header("Transition Settings")]
        [Tooltip("Speed of camera framing transition in seconds.")]
        [Range(1f, 8f)]
        public float transitionSpeed = 3.5f;

        [Header("Outdoor Settings")]
        public float outdoorRadius = 4.8f;
        public float outdoorTopHeight = 4.5f;
        public float outdoorTopRadius = 3.2f;
        public float outdoorCenterHeight = 2.2f;
        public float outdoorCenterRadius = 4.8f;
        public float outdoorBottomHeight = 1.0f;
        public float outdoorBottomRadius = 3.2f;
        public Vector2 outdoorVerticalRange = new Vector2(4f, 52f);

        [Header("Indoor Cave Settings (< Ceiling Height)")]
        public float indoorRadius = 3.0f;
        public float indoorTopHeight = 1.9f;
        public float indoorTopRadius = 2.4f;
        public float indoorCenterHeight = 1.35f;
        public float indoorCenterRadius = 3.0f;
        public float indoorBottomHeight = 0.8f;
        public float indoorBottomRadius = 2.2f;
        public Vector2 indoorVerticalRange = new Vector2(2f, 32f);

        [Header("Current Runtime State")]
        [Range(0f, 1f)]
        public float currentCaveFactor = 0f;
        private float targetCaveFactor = 0f;

        private void Awake()
        {
            InitializeReferences();
        }

        private void OnEnable()
        {
            InitializeReferences();
        }

        private void Start()
        {
            InitializeReferences();
        }

        private void OnValidate()
        {
            ApplyColliderOptimization();
        }

        public void InitializeReferences()
        {
            if (virtualCamera == null)
            {
                var flGo = GameObject.Find("FreeLook Camera");
                if (flGo != null) virtualCamera = flGo.GetComponent<CinemachineCamera>();
            }

            if (orbitalFollow == null && virtualCamera != null)
            {
                orbitalFollow = virtualCamera.GetComponent<CinemachineOrbitalFollow>();
            }

            if (cinemachineCollider == null)
            {
                if (virtualCamera != null) cinemachineCollider = virtualCamera.GetComponent<CinemachineCollider>();
                if (cinemachineCollider == null) cinemachineCollider = GetComponent<CinemachineCollider>();
            }

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

            if (playerTransform != null && (cachedPlayerRenderers == null || cachedPlayerRenderers.Length == 0))
            {
                cachedPlayerRenderers = playerTransform.GetComponentsInChildren<Renderer>();
            }

            ApplyColliderOptimization();
        }

        public void ApplyColliderOptimization()
        {
            if (!autoOptimizeCollider) return;

            if (cinemachineCollider == null)
            {
                if (virtualCamera != null) cinemachineCollider = virtualCamera.GetComponent<CinemachineCollider>();
                if (cinemachineCollider == null) cinemachineCollider = GetComponent<CinemachineCollider>();
            }

            if (cinemachineCollider != null)
            {
                cinemachineCollider.m_AvoidObstacles = true;
                cinemachineCollider.m_MinimumDistanceFromTarget = minDistanceFromTarget;
                cinemachineCollider.m_CameraRadius = cameraCollisionRadius;
                cinemachineCollider.m_Damping = collisionDamping;
                cinemachineCollider.m_DampingWhenOccluded = dampingWhenOccluded;
                cinemachineCollider.m_SmoothingTime = collisionSmoothingTime;
                cinemachineCollider.m_IgnoreTag = "Player";
                if (wallCollisionLayers.value != 0)
                {
                    cinemachineCollider.m_CollideAgainst = wallCollisionLayers;
                }
            }
        }

        private void Update()
        {
            if (orbitalFollow == null || playerTransform == null)
            {
                InitializeReferences();
                if (orbitalFollow == null || playerTransform == null) return;
            }

            ApplyColliderOptimization();
            CalculateTargetCaveFactor();

            // Smooth interpolation
            float dt = Application.isPlaying ? Time.deltaTime : 0.05f;
            currentCaveFactor = Mathf.MoveTowards(currentCaveFactor, targetCaveFactor, dt * transitionSpeed);

            ApplyCameraFraming(currentCaveFactor);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;

            UpdateCharacterVisibility();
            EnforceWallSafety();
        }

        private void UpdateCharacterVisibility()
        {
            if (!hideMeshWhenInsideCharacter || playerTransform == null) return;

            if (cachedPlayerRenderers == null || cachedPlayerRenderers.Length == 0)
            {
                cachedPlayerRenderers = playerTransform.GetComponentsInChildren<Renderer>();
                if (cachedPlayerRenderers == null || cachedPlayerRenderers.Length == 0) return;
            }

            Transform camTr = Camera.main != null ? Camera.main.transform : (virtualCamera != null ? virtualCamera.transform : null);
            if (camTr == null) return;

            Vector3 targetPoint = playerTransform.position + Vector3.up * 1.35f;
            float dist = Vector3.Distance(camTr.position, targetPoint);
            bool shouldHideMesh = dist < characterHideDistance;

            if (isMeshHidden != shouldHideMesh)
            {
                isMeshHidden = shouldHideMesh;
                for (int i = 0; i < cachedPlayerRenderers.Length; i++)
                {
                    var r = cachedPlayerRenderers[i];
                    if (r == null) continue;
                    // Do not hide held stones/residues or particle renderers
                    if (r.GetComponent<RockItem>() != null || r.GetComponent<JbelIghoud.Lithic.Rock>() != null) continue;
                    r.shadowCastingMode = isMeshHidden ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        private void EnforceWallSafety()
        {
            if (playerTransform == null) return;

            Transform camTr = Camera.main != null ? Camera.main.transform : (virtualCamera != null ? virtualCamera.transform : null);
            if (camTr == null) return;

            Vector3 targetPoint = playerTransform.position + Vector3.up * 1.35f;
            Vector3 toCam = camTr.position - targetPoint;
            float dist = toCam.magnitude;
            if (dist <= 0.05f) return;

            Vector3 dir = toCam / dist;

            // SphereCast from player target out towards camera to guarantee camera never punches behind walls
            if (Physics.SphereCast(targetPoint, cameraCollisionRadius, dir, out RaycastHit hit, dist, wallCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.CompareTag("Player") && !hit.collider.isTrigger)
                {
                    float safeDist = Mathf.Max(0.01f, hit.distance - 0.02f);
                    camTr.position = targetPoint + dir * safeDist;
                }
            }
        }

        private void CalculateTargetCaveFactor()
        {
            Vector3 p = playerTransform.position;

            // 1. Deep inside cave bounds -> 100% cave framing
            if (caveInteriorBounds.Contains(p))
            {
                targetCaveFactor = 1.0f;
                return;
            }

            // 2. Entrance transition zone (between outside entrance and inside tunnel)
            Vector3 line = tunnelPoint - entrancePoint;
            float lineLen = line.magnitude;
            if (lineLen > 0.01f)
            {
                Vector3 toPlayer = p - entrancePoint;
                float proj = Vector3.Dot(toPlayer, line.normalized);
                float factor = Mathf.Clamp01(proj / lineLen);

                // Check lateral distance to line
                Vector3 closestPoint = entrancePoint + line.normalized * Mathf.Clamp(proj, 0f, lineLen);
                float distToLine = Vector3.Distance(p, closestPoint);

                if (distToLine < 14f)
                {
                    targetCaveFactor = factor;
                    return;
                }
            }

            targetCaveFactor = 0.0f;
        }

        private void ApplyCameraFraming(float factor)
        {
            float radius = Mathf.Lerp(outdoorRadius, indoorRadius, factor);
            orbitalFollow.Radius = radius;

            var orbits = orbitalFollow.Orbits;

            orbits.Top.Height = Mathf.Lerp(outdoorTopHeight, indoorTopHeight, factor);
            orbits.Top.Radius = Mathf.Lerp(outdoorTopRadius, indoorTopRadius, factor);

            orbits.Center.Height = Mathf.Lerp(outdoorCenterHeight, indoorCenterHeight, factor);
            orbits.Center.Radius = Mathf.Lerp(outdoorCenterRadius, indoorCenterRadius, factor);

            orbits.Bottom.Height = Mathf.Lerp(outdoorBottomHeight, indoorBottomHeight, factor);
            orbits.Bottom.Radius = Mathf.Lerp(outdoorBottomRadius, indoorBottomRadius, factor);

            orbitalFollow.Orbits = orbits;

            var vertAxis = orbitalFollow.VerticalAxis;
            vertAxis.Range = Vector2.Lerp(outdoorVerticalRange, indoorVerticalRange, factor);
            orbitalFollow.VerticalAxis = vertAxis;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireCube(caveInteriorBounds.center, caveInteriorBounds.size);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(entrancePoint, 0.6f);
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(tunnelPoint, 0.6f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(entrancePoint, tunnelPoint);
        }
    }
#pragma warning restore CS0618
}
