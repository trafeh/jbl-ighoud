using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Unity.Cinemachine;

public class MobileFreeLookTouchController : MonoBehaviour
{
    [Header("Target Cinemachine Orbital Follow")]
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Sensitivity Settings")]
    [Tooltip("Horizontal orbit sensitivity.")]
    [SerializeField] private float sensitivityX = 0.22f;

    [Tooltip("Vertical orbit sensitivity.")]
    [SerializeField] private float sensitivityY = 0.15f;

    [Tooltip("Look smoothing speed for fluid swipe rotation.")]
    [SerializeField] private float lookSmoothing = 28f;

    [Tooltip("Invert vertical look axis.")]
    [SerializeField] private bool invertY = false;

    [Header("Touch Screen Zone")]
    [Tooltip("Minimum normalized X screen position to allow touch look (0.5 means right half of screen strictly, leaving left half 100% free for joystick).")]
    [Range(0.3f, 0.7f)]
    [SerializeField] private float minTouchScreenX = 0.45f;

    [Header("Auto Follow Settings")]
    [Tooltip("Enable auto-follow camera to align behind player when moving without camera touch. Disabled for Free Fire style camera freedom.")]
    [SerializeField] private bool autoAlignEnabled = false;

    [Tooltip("Delay in seconds without manual camera touch before auto-align activates.")]
    [SerializeField] private float autoActivateDelay = 2.5f;

    [Tooltip("Speed in degrees per second at which the camera rotates behind the player.")]
    [SerializeField] private float alignSpeed = 45f;

    [Tooltip("Minimum angle difference between camera and player forward to trigger auto-alignment.")]
    [SerializeField] private float minTriggerAngle = 15f;

    [Tooltip("Angle threshold in degrees to consider camera aligned and deactivate auto-follow.")]
    [SerializeField] private float stopThresholdAngle = 1.5f;

    [Tooltip("Only auto-align when player is moving.")]
    [SerializeField] private bool requireMoving = true;

    private int activeFingerId = -1;
    private Vector2 lastTouchPosition;
    private bool isMouseDragging = false;
    private Vector2 lastMousePosition;

    private float timeSinceLastManualLook = 999f;
    private bool isAutoAlignActive = false;
    private TopDownPlayerController playerController;
    private Transform playerTransform;

    public bool IsAutoAlignActive => isAutoAlignActive;
    public bool IsManualLooking => (activeFingerId != -1) || isMouseDragging;
    public float TimeSinceLastManualLook => timeSinceLastManualLook;

    private void Awake()
    {
        if (orbitalFollow == null)
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow == null)
            {
                var cam = GetComponent<CinemachineCamera>();
                if (cam != null) orbitalFollow = cam.GetComponent<CinemachineOrbitalFollow>();
            }
        }

        FindPlayerReference();
    }

    private void FindPlayerReference()
    {
        if (playerTransform != null && playerController != null) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<TopDownPlayerController>();
        }
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    [Header("Control Override")]
    public bool isInputDisabled = false;

    private void Update()
    {
        if (isInputDisabled || orbitalFollow == null) return;
        if (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode) return;

        FindPlayerReference();
        HandleTouchInput();
        HandleMouseEditorFallback();
        HandleAutoAlignment();
    }

    private void HandleTouchInput()
    {
        var touches = Touch.activeTouches;

        // If actively tracking a camera drag finger on the right side
        if (activeFingerId != -1)
        {
            timeSinceLastManualLook = 0f;
            isAutoAlignActive = false;

            bool touchStillActive = false;
            foreach (var touch in touches)
            {
                if (touch.touchId == activeFingerId)
                {
                    touchStillActive = true;
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        Vector2 delta = touch.screenPosition - lastTouchPosition;
                        ApplyLookDelta(delta);
                        lastTouchPosition = touch.screenPosition;
                    }
                    else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                             touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        activeFingerId = -1;
                    }
                    break;
                }
            }

            if (!touchStillActive)
            {
                activeFingerId = -1;
            }
        }

        // Look for a new touch beginning strictly on the right half of the screen and outside UI
        if (activeFingerId == -1)
        {
            foreach (var touch in touches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    // Finger must be on right half of screen (never in joystick territory)
                    if (touch.screenPosition.x >= Screen.width * minTouchScreenX)
                    {
                        if (!IsPointerOverUI(touch.screenPosition))
                        {
                            activeFingerId = touch.touchId;
                            lastTouchPosition = touch.screenPosition;
                            timeSinceLastManualLook = 0f;
                            isAutoAlignActive = false;
                            break;
                        }
                    }
                }
            }
        }
    }

    private void HandleMouseEditorFallback()
    {
        if (Mouse.current == null) return;

        // In Editor: ONLY Right Mouse Button rotates the camera!
        // Left Mouse Button is 100% reserved for joystick, UI buttons, and rock dragging!
        bool rightPressed = Mouse.current.rightButton.isPressed;

        if (rightPressed)
        {
            timeSinceLastManualLook = 0f;
            isAutoAlignActive = false;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (!isMouseDragging)
            {
                isMouseDragging = true;
                lastMousePosition = mousePos;
            }
            else
            {
                Vector2 delta = mousePos - lastMousePosition;
                if (delta.sqrMagnitude > 0.01f)
                {
                    ApplyLookDelta(delta);
                }
                lastMousePosition = mousePos;
            }
        }
        else
        {
            isMouseDragging = false;
        }
    }

    private void HandleAutoAlignment()
    {
        if (!autoAlignEnabled || orbitalFollow == null || playerTransform == null || playerController == null)
        {
            isAutoAlignActive = false;
            return;
        }

        // If user is currently touching/dragging camera with finger or mouse:
        if (IsManualLooking)
        {
            timeSinceLastManualLook = 0f;
            isAutoAlignActive = false;
            return;
        }

        timeSinceLastManualLook += Time.deltaTime;

        // Auto-align requires player to be actively moving
        if (requireMoving && !playerController.IsMoving)
        {
            isAutoAlignActive = false;
            return;
        }

        float currentCamYaw = orbitalFollow.HorizontalAxis.Value;
        float targetPlayerYaw = playerTransform.eulerAngles.y;
        float angleDiff = Mathf.DeltaAngle(currentCamYaw, targetPlayerYaw);

        // Check if we should activate auto-follow
        if (!isAutoAlignActive)
        {
            if (timeSinceLastManualLook >= autoActivateDelay && Mathf.Abs(angleDiff) >= minTriggerAngle)
            {
                isAutoAlignActive = true;
            }
        }

        // If active, smoothly rotate camera horizontal axis towards player facing direction
        if (isAutoAlignActive)
        {
            float newCamYaw = Mathf.MoveTowardsAngle(currentCamYaw, targetPlayerYaw, alignSpeed * Time.deltaTime);

            if (newCamYaw > 180f) newCamYaw -= 360f;
            if (newCamYaw < -180f) newCamYaw += 360f;
            orbitalFollow.HorizontalAxis.Value = newCamYaw;

            // Once aligned within stopThresholdAngle, DEACTIVATE again!
            if (Mathf.Abs(Mathf.DeltaAngle(newCamYaw, targetPlayerYaw)) <= stopThresholdAngle)
            {
                orbitalFollow.HorizontalAxis.Value = targetPlayerYaw;
                isAutoAlignActive = false; // Deactivated!
            }
        }
    }

    private void ApplyLookDelta(Vector2 delta)
    {
        if (orbitalFollow == null) return;

        float deltaX = delta.x * sensitivityX;
        float deltaY = delta.y * sensitivityY * (invertY ? 1f : -1f);

        // Apply to Horizontal Axis
        float newH = orbitalFollow.HorizontalAxis.Value + deltaX;
        if (newH > 180f) newH -= 360f;
        if (newH < -180f) newH += 360f;
        orbitalFollow.HorizontalAxis.Value = newH;

        // Apply to Vertical Axis
        float newV = orbitalFollow.VerticalAxis.Value + deltaY;
        Vector2 vRange = orbitalFollow.VerticalAxis.Range;
        newV = Mathf.Clamp(newV, vRange.x, vRange.y);
        orbitalFollow.VerticalAxis.Value = newV;
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
