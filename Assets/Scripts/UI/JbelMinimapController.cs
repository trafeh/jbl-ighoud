using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Minimap Controller for Jbel Ighoud HUD.
    /// Positioned top-right under the ResourceBar (collected items).
    /// Features:
    /// - Circular aesthetic matching the reference art with golden rim border (#FFCF5F).
    /// - Stylized cartographic map overview showing terrain, river, lake, trail, and markers.
    /// - Dynamic player navigation arrow matching reference cream triangle (#F3EFE0)
    ///   with real-time heading rotation and world-coordinate tracking.
    /// - Optional LiveCamera mode rendering real-time 3D top-down camera view.
    /// - Interactive tap-to-expand button opening the full JbelMapMenuController modal.
    /// </summary>
    public class JbelMinimapController : MonoBehaviour
    {
        private static JbelMinimapController _instance;
        public static JbelMinimapController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<JbelMinimapController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        public enum DisplayMode
        {
            StylizedMap,
            LiveCamera
        }

        public enum MinimapRotationMode
        {
            RotateWithCamera,   // Map rotates with 3D Main Camera view (forward view is always UP on map)
            RotateWithPlayer,   // Map rotates with Player character heading (player arrow always points UP)
            FixedNorth          // Classic map: North is always UP, arrow rotates freely
        }

        [Header("--- Display & Rotation Mode ---")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.LiveCamera;
        public DisplayMode CurrentDisplayMode
        {
            get => displayMode;
            set
            {
                displayMode = value;
                ApplyDisplayMode();
            }
        }

        [Tooltip("How the live minimap rotates. RotateWithCamera makes the map turn with your 3D view camera so forward in the world is always UP on the minimap.")]
        [SerializeField] private MinimapRotationMode rotationMode = MinimapRotationMode.RotateWithCamera;
        public MinimapRotationMode RotationMode
        {
            get => rotationMode;
            set => rotationMode = value;
        }

        [Header("--- UI References ---")]
        [SerializeField] private RectTransform minimapRoot;
        [SerializeField] private Image mapBackgroundImage;
        [SerializeField] private RawImage mapCameraRawImage;
        [SerializeField] private Image borderFrameImage;
        [SerializeField] private RectTransform playerArrowRT;
        [SerializeField] private Button minimapButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("--- Target References ---")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera mainGameplayCamera;
        [SerializeField] private Camera topDownCamera;

        [Header("--- Live Camera Settings ---")]
        [Tooltip("Altitude in meters above ground for the top-down minimap camera.")]
        [SerializeField] private float cameraAltitude = 260f;

        [Tooltip("Zoom / Orthographic size for the live minimap (smaller = more zoomed in).")]
        [Range(20f, 150f)]
        [SerializeField] private float cameraOrthoSize = 55f;
        public float CameraOrthoSize => cameraOrthoSize;

        [Header("--- Coordinate Calibration ---")]
        [Tooltip("World coordinate center corresponding to the map center (X, Z).")]
        [SerializeField] private Vector2 worldCenter = new Vector2(6.88f, 4.13f);

        [Tooltip("Total terrain dimension in meters (700m x 700m).")]
        [SerializeField] private Vector2 worldSize = new Vector2(700f, 700f);

        [Tooltip("Invert horizontal mapping if needed.")]
        [SerializeField] private bool invertX = false;

        [Tooltip("Invert vertical mapping if needed.")]
        [SerializeField] private bool invertZ = false;

        [Tooltip("Rotation offset in degrees for the player arrow.")]
        [SerializeField] private float arrowRotationOffset = 0f;

        [Tooltip("Effective inner radius in UI pixels within which the arrow can travel (used only in StylizedMap mode).")]
        [SerializeField] private float maxMarkerRadius = 86f;

        [Tooltip("Smooth interpolation speed for player arrow position & rotation.")]
        [SerializeField] private float smoothSpeed = 16f;

        private Vector2 targetArrowPos;
        private float targetArrowRot;
        private Vector2 currentArrowPos;
        private float currentArrowRot;

        private float targetMapYaw = 0f;
        private float currentMapYaw = 0f;

        private void Awake()
        {
            _instance = this;

            if (minimapRoot == null) minimapRoot = GetComponent<RectTransform>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (minimapButton == null) minimapButton = GetComponent<Button>();

            if (minimapButton != null)
            {
                minimapButton.onClick.RemoveListener(OnMinimapClicked);
                minimapButton.onClick.AddListener(OnMinimapClicked);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            FindPlayerReference();
            FindMainCamera();
            FindCameraReference();
            ApplyDisplayMode();

            // Initial instant snap
            UpdateArrowAndCameraTargets();
            currentArrowPos = targetArrowPos;
            currentArrowRot = targetArrowRot;
            currentMapYaw = targetMapYaw;

            if (playerArrowRT != null)
            {
                playerArrowRT.anchoredPosition = currentArrowPos;
                playerArrowRT.localEulerAngles = new Vector3(0f, 0f, currentArrowRot);
            }
            if (topDownCamera != null && displayMode == DisplayMode.LiveCamera)
            {
                topDownCamera.transform.rotation = Quaternion.Euler(90f, currentMapYaw, 0f);
            }
        }

        private void OnEnable()
        {
            FindMainCamera();
            ApplyDisplayMode();
            UpdateArrowAndCameraTargets();
            currentMapYaw = targetMapYaw;
            currentArrowRot = targetArrowRot;
        }

        private void OnDisable()
        {
            if (topDownCamera != null && displayMode == DisplayMode.LiveCamera)
            {
                topDownCamera.enabled = false;
            }
        }

        [Tooltip("Seconds between minimap camera renders (performance). 0.066 = 15 fps.")]
        public float minimapRenderInterval = 0.066f;
        private float nextMinimapRenderTime;
        private bool hasRendered;
        private Vector3 lastRenderPos;

        private void LateUpdate()
        {
            if (playerTransform == null)
            {
                FindPlayerReference();
                if (playerTransform == null) return;
            }

            if (mainGameplayCamera == null)
            {
                FindMainCamera();
            }

            if (topDownCamera == null)
            {
                FindCameraReference();
            }

            UpdateArrowAndCameraTargets();

            // Smooth interpolation
            float dt = Time.unscaledDeltaTime;
            currentMapYaw = Mathf.LerpAngle(currentMapYaw, targetMapYaw, dt * smoothSpeed);
            currentArrowPos = Vector2.Lerp(currentArrowPos, targetArrowPos, dt * smoothSpeed);
            currentArrowRot = Mathf.LerpAngle(currentArrowRot, targetArrowRot, dt * smoothSpeed);

            if (playerArrowRT != null)
            {
                playerArrowRT.anchoredPosition = currentArrowPos;
                playerArrowRT.localEulerAngles = new Vector3(0f, 0f, currentArrowRot);
            }

            // Update live camera if in LiveCamera mode
            // If full map modal is open, let JbelMapMenuController manage the camera
            if (JbelMapMenuController.Instance != null && JbelMapMenuController.Instance.IsOpen)
            {
                hasRendered = false;
                if (mapCameraRawImage != null) { mapCameraRawImage.rectTransform.localEulerAngles = Vector3.zero; mapCameraRawImage.uvRect = new Rect(0f, 0f, 1f, 1f); }
            }
            bool fullMapOpen = (JbelMapMenuController.Instance != null && JbelMapMenuController.Instance.IsOpen);
            if (displayMode == DisplayMode.LiveCamera && topDownCamera != null && !fullMapOpen)
            {
                if (topDownCamera.enabled) topDownCamera.enabled = false; // rendered manually below (throttled)

                // Render the map NORTH-UP at a throttled rate, then rotate/offset the RawImage every frame
                // so rotation and movement stay perfectly smooth regardless of the render interval.
                if (Time.unscaledTime >= nextMinimapRenderTime || !hasRendered)
                {
                    nextMinimapRenderTime = Time.unscaledTime + minimapRenderInterval;
                    lastRenderPos = playerTransform.position;
                    hasRendered = true;
                    topDownCamera.transform.position = new Vector3(lastRenderPos.x, cameraAltitude, lastRenderPos.z);
                    topDownCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    topDownCamera.orthographic = true;
                    topDownCamera.orthographicSize = cameraOrthoSize;
                    topDownCamera.Render();
                }

                if (mapCameraRawImage != null)
                {
                    mapCameraRawImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, currentMapYaw);
                    Vector3 d = playerTransform.position - lastRenderPos;
                    float inv = 1f / (2f * cameraOrthoSize);
                    mapCameraRawImage.uvRect = new Rect(d.x * inv, d.z * inv, 1f, 1f);
                }
            }
        }

        private void UpdateArrowAndCameraTargets()
        {
            if (playerTransform == null) return;

            Vector3 worldPos = playerTransform.position;
            float playerYaw = playerTransform.eulerAngles.y;
            float cameraYaw = (mainGameplayCamera != null) ? mainGameplayCamera.transform.eulerAngles.y : playerYaw;

            if (displayMode == DisplayMode.LiveCamera)
            {
                // In LiveCamera mode, player arrow stays anchored in the center of the circular viewport
                targetArrowPos = Vector2.zero;

                switch (rotationMode)
                {
                    case MinimapRotationMode.RotateWithCamera:
                        // Map rotates so the Main Camera's horizontal forward direction is ALWAYS UP on the minimap
                        targetMapYaw = cameraYaw;
                        // Player arrow points in character's facing direction relative to the camera view
                        targetArrowRot = -(playerYaw - targetMapYaw) + arrowRotationOffset;
                        break;

                    case MinimapRotationMode.RotateWithPlayer:
                        // Map rotates with player body heading; player arrow is locked pointing straight forward (UP)
                        targetMapYaw = playerYaw;
                        targetArrowRot = arrowRotationOffset;
                        break;

                    case MinimapRotationMode.FixedNorth:
                    default:
                        // Classic static North-up map
                        targetMapYaw = 0f;
                        targetArrowRot = -playerYaw + arrowRotationOffset;
                        break;
                }
            }
            else
            {
                // In StylizedMap overview mode, calculate normalized position [-0.5, +0.5]
                targetMapYaw = 0f;
                float normX = (worldPos.x - worldCenter.x) / worldSize.x;
                float normZ = (worldPos.z - worldCenter.y) / worldSize.y;

                if (invertX) normX = -normX;
                if (invertZ) normZ = -normZ;

                // Scale to UI pixel radius
                float px = normX * (maxMarkerRadius * 2f);
                float py = normZ * (maxMarkerRadius * 2f);

                // Clamp within circular radius
                Vector2 pos = new Vector2(px, py);
                if (pos.magnitude > maxMarkerRadius)
                {
                    pos = pos.normalized * maxMarkerRadius;
                }

                targetArrowPos = pos;
                targetArrowRot = -playerYaw + arrowRotationOffset;
            }
        }

        private void ApplyDisplayMode()
        {
            bool isLive = (displayMode == DisplayMode.LiveCamera);

            if (mapBackgroundImage != null) mapBackgroundImage.gameObject.SetActive(!isLive);
            if (mapCameraRawImage != null)
            {
                mapCameraRawImage.gameObject.SetActive(isLive);
                if (isLive && mapCameraRawImage.texture == null)
                {
                    var rt = Resources.Load<RenderTexture>("LiveMap_RenderTexture");
                    if (rt == null)
                    {
#if UNITY_EDITOR
                        rt = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/UI/MAP/LiveMap_RenderTexture.renderTexture");
#endif
                    }
                    if (rt != null) mapCameraRawImage.texture = rt;
                }
            }

            if (topDownCamera == null)
            {
                FindCameraReference();
            }

            if (topDownCamera != null)
            {
                topDownCamera.gameObject.SetActive(true);
                topDownCamera.enabled = isLive;
            }
        }

        private void FindPlayerReference()
        {
            if (playerTransform != null) return;

            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p == null) p = GameObject.Find("Y Bot");
            if (p != null) playerTransform = p.transform;
        }

        private void FindMainCamera()
        {
            if (mainGameplayCamera != null) return;

            mainGameplayCamera = Camera.main;
            if (mainGameplayCamera == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) mainGameplayCamera = camGo.GetComponent<Camera>();
            }
            if (mainGameplayCamera == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (var c in cams)
                {
                    if (c.name.Contains("Main Camera", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mainGameplayCamera = c;
                        break;
                    }
                }
            }
        }

        private void FindCameraReference()
        {
            if (topDownCamera != null) return;

            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (var c in cams)
            {
                if (c.name == "Map_TopDownCamera")
                {
                    topDownCamera = c;
                    break;
                }
            }

            if (topDownCamera != null && !topDownCamera.gameObject.activeSelf)
            {
                topDownCamera.gameObject.SetActive(true);
            }
        }

        public void OnMinimapClicked()
        {
            Debug.Log("[JbelMinimap] Minimap tapped -> opening full map modal.");
            if (JbelMapMenuController.Instance != null)
            {
                JbelMapMenuController.Instance.OpenMap(fromPause: false);
            }
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        public void SetDisplayMode(DisplayMode mode)
        {
            displayMode = mode;
            ApplyDisplayMode();
        }

#if UNITY_EDITOR
        [ContextMenu("Snap to Current Player Position")]
        public void SnapToPlayerNow()
        {
            FindPlayerReference();
            FindMainCamera();
            UpdateArrowAndCameraTargets();
            currentArrowPos = targetArrowPos;
            currentArrowRot = targetArrowRot;
            currentMapYaw = targetMapYaw;
            if (playerArrowRT != null)
            {
                playerArrowRT.anchoredPosition = currentArrowPos;
                playerArrowRT.localEulerAngles = new Vector3(0f, 0f, currentArrowRot);
            }
            if (topDownCamera != null)
            {
                topDownCamera.transform.rotation = Quaternion.Euler(90f, currentMapYaw, 0f);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
