using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master Controller for the authentic Jbel Ighoud Live Map System:
    /// - Live dedicated top-down Camera follows the player from high altitude (orthographic).
    /// - Renders the real 3D world (terrain, mountains, lake, cave, vegetation) directly into a high-res RenderTexture.
    /// - Displayed in the left rounded panel with a dynamic Player Pin tracking player heading.
    /// - Right side: Objectives Panel Background (empty container ready for the objectives system).
    /// - Camera is active ONLY while the map is open for 0 performance impact during gameplay.
    /// </summary>
    public class JbelMapMenuController : MonoBehaviour
    {
        private static JbelMapMenuController _instance;
        public static JbelMapMenuController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<JbelMapMenuController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        public enum MapCameraMode
        {
            FollowPlayer,        // High-detail camera centered over player; real world moves underneath; pin centered
            WholeWorldOverview   // Camera framed over the whole 700m terrain; pin travels across the map
        }

        public enum MapRotationMode
        {
            RotateWithCamera,   // Map rotates with Main Camera view (camera forward is UP on map)
            RotateWithPlayer,   // Map rotates with Player heading (player pin always points UP)
            FixedNorth          // Classic static North-up map
        }

        [Header("========================================")]
        [Header("--- LIVE TOP-DOWN CAMERA & RENDERTEXTURE ---")]
        [Header("========================================")]
        [SerializeField] private Camera mapTopDownCamera;
        [SerializeField] private RenderTexture mapRenderTexture;
        [SerializeField] private RawImage mapRawImage;
        [SerializeField] private RectTransform playerPinRT;
        [SerializeField] private Transform playerTransform;

        [Header("========================================")]
        [Header("--- CAMERA POSITION & ZOOM SETTINGS ---")]
        [Header("========================================")]
        [SerializeField] private MapCameraMode cameraMode = MapCameraMode.FollowPlayer;

        [Tooltip("How the map rotates when following player. RotateWithCamera keeps the view camera's forward UP.")]
        [SerializeField] private MapRotationMode rotationMode = MapRotationMode.RotateWithCamera;
        public MapRotationMode RotationMode
        {
            get => rotationMode;
            set => rotationMode = value;
        }

        [Tooltip("Altitude in meters above ground for the top-down camera.")]
        [SerializeField] private float cameraAltitude = 260f;

        [Tooltip("Zoom / Orthographic size when following player (smaller = more zoomed in).")]
        [Range(40f, 350f)]
        [SerializeField] private float followZoomSize = 110f;

        [Tooltip("Zoom / Orthographic size for whole 700m terrain overview.")]
        [SerializeField] private float overviewZoomSize = 350f;

        [Tooltip("World center of the 700m terrain (X, Z).")]
        [SerializeField] private Vector2 worldCenter = new Vector2(6.88f, 5.66f);

        [Tooltip("Total terrain dimension in meters (700m x 700m).")]
        [SerializeField] private Vector2 worldSize = new Vector2(700f, 700f);

        [Tooltip("Arrow pin rotation offset in degrees.")]
        [SerializeField] private float pinRotationOffset = 0f;

        [Header("========================================")]
        [Header("--- MODAL PANELS & CANVAS GROUPS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup mapModalCG;
        [SerializeField] private Image dimBackdropImage;
        [SerializeField] private RectTransform contentContainerRT;
        [SerializeField] private Button closeButton;

        [Header("========================================")]
        [Header("--- TIMING & FADE ---")]
        [Header("========================================")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float dimBackdropAlpha = 0.72f;
        [SerializeField] private float modalAnimationDuration = 0.22f;

        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private bool wasOpenedFromPause = false;
        private Coroutine modalAnimCoroutine;

        private void Awake()
        {
            _instance = this;

            AutoWireReferences();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseMap);
                closeButton.onClick.AddListener(CloseMap);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

private float nextMapRenderTime;

        // The map camera is only ever rendered manually (throttled) while the map is open.
        // Leaving it enabled made it draw the whole world every frame during gameplay.
        private void LateUpdate()
        {
            if (mapTopDownCamera != null && mapTopDownCamera.enabled) mapTopDownCamera.enabled = false;
        }


        private void Start()
        {
            FindPlayerReference();
            AutoWireReferences();

            // Only disable camera if minimap is NOT using LiveCamera
            bool minimapUsesLiveCam = (JbelMinimapController.Instance != null &&
                                       JbelMinimapController.Instance.CurrentDisplayMode == JbelMinimapController.DisplayMode.LiveCamera);
            if (mapTopDownCamera != null)
            {
                mapTopDownCamera.gameObject.SetActive(true);
                mapTopDownCamera.enabled = minimapUsesLiveCam;
                if (minimapUsesLiveCam && JbelMinimapController.Instance != null)
                {
                    mapTopDownCamera.orthographicSize = JbelMinimapController.Instance.CameraOrthoSize;
                }
            }

            // Initialize hidden
            if (mapModalCG != null)
            {
                mapModalCG.alpha = 0f;
                mapModalCG.blocksRaycasts = false;
                mapModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = false;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one * 0.95f;
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // Update live camera & pin every frame in unscaled time
            UpdateLiveCameraAndPin();

            // Keyboard shortcut to close map (Escape, P, or M)
            bool closeRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                closeRequested = Keyboard.current.escapeKey.wasPressedThisFrame ||
                                 Keyboard.current.pKey.wasPressedThisFrame ||
                                 Keyboard.current.mKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!closeRequested)
            {
                try
                {
                    closeRequested = Input.GetKeyDown(KeyCode.Escape) ||
                                     Input.GetKeyDown(KeyCode.P) ||
                                     Input.GetKeyDown(KeyCode.M);
                }
                catch { }
            }
#endif

            if (closeRequested)
            {
                CloseMap();
            }
        }

        private void FindPlayerReference()
        {
            if (playerTransform != null) return;

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj == null) playerObj = GameObject.Find("Y Bot");

            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        public void OpenMap(bool fromPause = false)
        {
            if (isOpen) return;
            isOpen = true;
            wasOpenedFromPause = fromPause;

            Time.timeScale = 0f;

            // Hide gameplay HUD & joystick during Map view for clean full-screen map
            SetHUDVisible(false);

            // Lock camera & player input
            var touchCtrl = Object.FindAnyObjectByType<MobileFreeLookTouchController>();
            if (touchCtrl != null) touchCtrl.isInputDisabled = true;

            var player = Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null) player.SetControlsLocked(true);

            // If opened from pause, hide pause menu modal
            if (fromPause && JbelPauseMenuController.Instance != null)
            {
                var pauseCG = JbelPauseMenuController.Instance.GetComponent<CanvasGroup>();
                if (pauseCG != null)
                {
                    pauseCG.alpha = 0f;
                    pauseCG.blocksRaycasts = false;
                    pauseCG.interactable = false;
                }
            }

            FindPlayerReference();

            // Enable Live Top-Down Camera
            if (mapTopDownCamera != null)
            {
                mapTopDownCamera.enabled = true;
            }

            nextMapRenderTime = 0f;
            UpdateLiveCameraAndPin();

            // Animate open
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(true));

            Debug.Log("<color=cyan>[JbelMap] Live Camera Map Opened.</color>");
        }

        public void CloseMap()
        {
            if (!isOpen) return;
            isOpen = false;

            // If Minimap uses LiveCamera, keep top-down camera enabled and restore its minimap ortho size
            bool minimapUsesLiveCam = (JbelMinimapController.Instance != null &&
                                       JbelMinimapController.Instance.CurrentDisplayMode == JbelMinimapController.DisplayMode.LiveCamera);
            if (mapTopDownCamera != null)
            {
                if (minimapUsesLiveCam)
                {
                    mapTopDownCamera.gameObject.SetActive(true);
                    mapTopDownCamera.enabled = true;
                    if (JbelMinimapController.Instance != null)
                    {
                        mapTopDownCamera.orthographicSize = JbelMinimapController.Instance.CameraOrthoSize;
                    }
                }
                else
                {
                    mapTopDownCamera.enabled = false;
                }
            }

            // Animate close
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(false));

            if (wasOpenedFromPause && JbelPauseMenuController.Instance != null)
            {
                // Return to Pause Menu
                var pauseCG = JbelPauseMenuController.Instance.GetComponent<CanvasGroup>();
                if (pauseCG != null)
                {
                    pauseCG.alpha = 1f;
                    pauseCG.blocksRaycasts = true;
                    pauseCG.interactable = true;
                }
            }
            else
            {
                // Restore gameplay HUD & joystick
                SetHUDVisible(true);

                // Unfreeze and resume gameplay
                Time.timeScale = 1f;

                var touchCtrl = Object.FindAnyObjectByType<MobileFreeLookTouchController>();
                if (touchCtrl != null) touchCtrl.isInputDisabled = false;

                var player = Object.FindAnyObjectByType<TopDownPlayerController>();
                if (player != null) player.SetControlsLocked(false);
            }

            Debug.Log("<color=green>[JbelMap] Live Camera Map Closed.</color>");
        }

        private void SetHUDVisible(bool visible)
        {
            if (transform.parent == null) return;

            var hudPanel = transform.parent.Find("JbelHUD_Panel");
            if (hudPanel != null)
            {
                var cg = hudPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = hudPanel.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = visible ? 1f : 0f;
                cg.blocksRaycasts = visible;
                cg.interactable = visible;
            }

            var joystick = transform.parent.Find("JoystickZone");
            if (joystick != null)
            {
                joystick.gameObject.SetActive(visible);
            }
        }

        public void ToggleMap()
        {
            if (isOpen) CloseMap();
            else OpenMap(fromPause: false);
        }

        public void UpdateLiveCameraAndPin()
        {
            FindPlayerReference();

            if (mapTopDownCamera != null)
            {
                if (cameraMode == MapCameraMode.FollowPlayer && playerTransform != null)
                {
                    // Camera positioned directly above the player
                    Vector3 camTarget = new Vector3(playerTransform.position.x, cameraAltitude, playerTransform.position.z);
                    mapTopDownCamera.transform.position = camTarget;

                    float playerYaw = playerTransform.eulerAngles.y;
                    Camera mainCam = Camera.main;
                    float cameraYaw = (mainCam != null) ? mainCam.transform.eulerAngles.y : playerYaw;

                    float mapYaw = 0f;
                    float pinRot = 0f;

                    switch (rotationMode)
                    {
                        case MapRotationMode.RotateWithCamera:
                            mapYaw = cameraYaw;
                            pinRot = -(playerYaw - mapYaw) + pinRotationOffset;
                            break;
                        case MapRotationMode.RotateWithPlayer:
                            mapYaw = playerYaw;
                            pinRot = pinRotationOffset;
                            break;
                        case MapRotationMode.FixedNorth:
                        default:
                            mapYaw = 0f;
                            pinRot = -playerYaw + pinRotationOffset;
                            break;
                    }

                    mapTopDownCamera.transform.rotation = Quaternion.Euler(90f, mapYaw, 0f);
                    mapTopDownCamera.orthographic = true;
                    mapTopDownCamera.orthographicSize = followZoomSize;

                    // When following, player is centered at (0, 0)
                    if (playerPinRT != null)
                    {
                        playerPinRT.anchoredPosition = Vector2.zero;
                        playerPinRT.localRotation = Quaternion.Euler(0f, 0f, pinRot);
                    }
                }
                else
                {
                    // Whole terrain overview mode
                    Vector3 camTarget = new Vector3(worldCenter.x, cameraAltitude, worldCenter.y);
                    mapTopDownCamera.transform.position = camTarget;
                    mapTopDownCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    mapTopDownCamera.orthographic = true;
                    mapTopDownCamera.orthographicSize = overviewZoomSize;

                    // Map pin travels across the map rect
                    if (playerPinRT != null && mapRawImage != null && playerTransform != null)
                    {
                        Vector3 worldPos = playerTransform.position;
                        float normX = (worldPos.x - (worldCenter.x - worldSize.x * 0.5f)) / worldSize.x;
                        float normZ = (worldPos.z - (worldCenter.y - worldSize.y * 0.5f)) / worldSize.y;
                        normX = Mathf.Clamp01(normX);
                        normZ = Mathf.Clamp01(normZ);

                        Vector2 mapSize = mapRawImage.rectTransform.rect.size;
                        float localX = (normX - 0.5f) * mapSize.x;
                        float localY = (normZ - 0.5f) * mapSize.y;
                        playerPinRT.anchoredPosition = new Vector2(localX, localY);

                        float playerYaw = playerTransform.eulerAngles.y;
                        playerPinRT.localRotation = Quaternion.Euler(0f, 0f, -playerYaw + pinRotationOffset);
                    }
                }

                // Force camera render so RenderTexture updates instantly in unscaled time
                if (mapTopDownCamera.targetTexture == null && mapRenderTexture != null) mapTopDownCamera.targetTexture = mapRenderTexture;
                if (mapRawImage != null && mapRawImage.texture == null) mapRawImage.texture = mapTopDownCamera.targetTexture;
                if (mapTopDownCamera.targetTexture != null && Time.unscaledTime >= nextMapRenderTime)
                {
                    nextMapRenderTime = Time.unscaledTime + 0.1f; // 10 Hz is plenty for a paused map
                    mapTopDownCamera.enabled = false;
                    mapTopDownCamera.Render();
                }
            }
        }

        private IEnumerator AnimateModalRoutine(bool opening)
        {
            if (mapModalCG == null) yield break;

            if (opening)
            {
                mapModalCG.blocksRaycasts = true;
                mapModalCG.interactable = true;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = true;
            }

            float t = 0f;
            float startAlpha = mapModalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = (contentContainerRT != null) ? contentContainerRT.localScale : Vector3.one;
            Vector3 targetScale = opening ? Vector3.one : Vector3.one * 0.95f;

            while (t < modalAnimationDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / modalAnimationDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                mapModalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);

                if (dimBackdropImage != null)
                {
                    Color c = dimBackdropImage.color;
                    c.a = Mathf.Lerp(0f, dimBackdropAlpha, opening ? smoothP : (1f - smoothP));
                    dimBackdropImage.color = c;
                }

                if (contentContainerRT != null)
                {
                    contentContainerRT.localScale = Vector3.Lerp(startScale, targetScale, smoothP);
                }

                yield return null;
            }

            mapModalCG.alpha = targetAlpha;

            if (!opening)
            {
                mapModalCG.blocksRaycasts = false;
                mapModalCG.interactable = false;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = false;
            }
        }

        public void AutoWireReferences()
        {
            if (mapModalCG == null) mapModalCG = GetComponent<CanvasGroup>();
            if (dimBackdropImage == null)
            {
                var d = transform.Find("DimBackdrop");
                if (d != null) dimBackdropImage = d.GetComponent<Image>();
            }
            if (contentContainerRT == null)
            {
                var c = transform.Find("MapScreenContainer");
                if (c != null) contentContainerRT = c.GetComponent<RectTransform>();
            }
            if (mapRawImage == null && contentContainerRT != null)
            {
                var m = contentContainerRT.Find("MapCard/MapCardMask/MapLiveRawImage");
                if (m != null) mapRawImage = m.GetComponent<RawImage>();
            }
            if (playerPinRT == null && contentContainerRT != null)
            {
                var p = contentContainerRT.Find("MapCard/MapCardMask/PlayerPin");
                if (p != null) playerPinRT = p.GetComponent<RectTransform>();
            }
            if (closeButton == null && contentContainerRT != null)
            {
                var b = contentContainerRT.Find("ObjectivesCard/CloseButton");
                if (b != null) closeButton = b.GetComponent<Button>();
            }
            if (mapTopDownCamera == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (var c in cams)
                {
                    if (c.name == "Map_TopDownCamera")
                    {
                        mapTopDownCamera = c;
                        break;
                    }
                }
            }
            if (mapTopDownCamera != null && !mapTopDownCamera.gameObject.activeSelf)
            {
                mapTopDownCamera.gameObject.SetActive(true);
            }
            if (mapRenderTexture == null)
            {
#if UNITY_EDITOR
                mapRenderTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/UI/MAP/LiveMap_RenderTexture.renderTexture");
#endif
            }
        }

        // --- Editor Preview Helpers ---
        public void ApplyPreviewOpenState()
        {
            AutoWireReferences();
            if (mapModalCG != null)
            {
                mapModalCG.alpha = 1f;
                mapModalCG.blocksRaycasts = true;
                mapModalCG.interactable = true;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = dimBackdropAlpha;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = true;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one;
            }
            if (mapTopDownCamera != null)
            {
                mapTopDownCamera.enabled = true;
            }
            UpdateLiveCameraAndPin();
        }

        public void ApplyPreviewClosedState()
        {
            AutoWireReferences();
            if (mapModalCG != null)
            {
                mapModalCG.alpha = 0f;
                mapModalCG.blocksRaycasts = false;
                mapModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = false;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one * 0.95f;
            }
            if (mapTopDownCamera != null)
            {
                mapTopDownCamera.enabled = false;
            }
        }
    }
}
