using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Unity.Cinemachine;

public class RockDragManager : MonoBehaviour
{
    public static RockDragManager Instance { get; private set; }

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera fpCamera;
    [SerializeField] private Camera mainCamera;

    [Header("First Person View Constraints (1D X-Axis)")]
    [Tooltip("Distance in front of camera lens.")]
    [SerializeField] private float viewDepth = 0.85f;

    [Tooltip("Height relative to camera center.")]
    [SerializeField] private float viewHeight = -0.22f;

    [Tooltip("Left rock resting X coordinate relative to camera.")]
    [SerializeField] private float leftRestX = -0.42f;

    [Tooltip("Right rock resting X coordinate relative to camera.")]
    [SerializeField] private float rightRestX = +0.42f;

    [Tooltip("Minimum X limit on left side.")]
    [SerializeField] private float minX = -0.68f;

    [Tooltip("Maximum X limit on right side.")]
    [SerializeField] private float maxX = +0.68f;

    [Tooltip("Distance at which the two rocks make physical contact along X.")]
    [SerializeField] private float minCollisionDistance = 0.28f;

    [Tooltip("Scale of rocks when held in hands in first-person view.")]
    [SerializeField] private float handheldRockScale = 0.35f;

    [Tooltip("Target on-screen size of a held rock in metres (rocks are normalised by their mesh size). 0 = use Handheld Rock Scale as-is.")]
    [SerializeField] private float handheldRockSize = 0.24f;

    [Header("Timing Strike (tap when the needle is in the green)")]
    [SerializeField] private Sprite barFillSprite;
    [SerializeField] private Sprite barGreenSprite;
    [SerializeField] private Sprite barNeedleSprite;
    [Tooltip("On: tap-timing bar. Off: the old drag-the-rocks-together gesture.")]
    [SerializeField] private bool timingStrike = true;
    [SerializeField] private float strongDamage = 2f;
    [SerializeField] private float weakDamage = 0.4f;
    [Tooltip("Needle sweeps per second at the first hit.")]
    [SerializeField] private float startSpeed = 0.85f;
    [SerializeField] private float speedGainPerHit = 1.16f;
    [SerializeField] private float maxSpeed = 2.3f;
    [Tooltip("Green zone width as a fraction of the bar at the first hit.")]
    [SerializeField] private float startGreenWidth = 0.27f;
    [SerializeField] private float greenShrinkPerHit = 0.86f;
    [SerializeField] private float minGreenWidth = 0.09f;
    private float needleT, needleDir = 1f, needleSpeed, greenCenter = 0.5f, greenWidth;
    private bool hitAnimating;
    private RectTransform barRT, greenRT, needleRT;
    private TMPro.TextMeshProUGUI hitPopup;
    private float popupTime;

    [Header("Smash Dynamics")]
    [Tooltip("Minimum impact speed to trigger a smash.")]
    [SerializeField] private float minSmashSpeed = 1.25f;

    [Tooltip("Speed at which dragged rock follows touch.")]
    [SerializeField] private float dragFollowSpeed = 35f;

    [Tooltip("How fast rocks spring back to resting positions when released.")]
    [SerializeField] private float springReturnTime = 0.12f;

    [Header("Dependencies")]
    [SerializeField] private TopDownPlayerController player;
    [SerializeField] private MobileFreeLookTouchController touchLook;

    // State
    private bool isInSmashMode = false;
    private bool isResolvingSmash = false;
    private RockItem rockLeft = null;
    private RockItem rockRight = null;
    private Vector3 rockLeftOrigScale = Vector3.one;
    private Vector3 rockRightOrigScale = Vector3.one;

    // 1D X axis tracking
    private float currentLeftX;
    private float currentRightX;
    private float prevLeftX;
    private float prevRightX;
    private float leftVelocity = 0f;
    private float rightVelocity = 0f;
    private float leftReturnVel = 0f;
    private float rightReturnVel = 0f;

    // Drag status
    private bool isDraggingLeft = false;
    private bool isDraggingRight = false;
    private int leftFingerId = -1;
    private int rightFingerId = -1;

    // Screen shake & FX
    private Vector3 shakeOffset = Vector3.zero;
    private float shakeIntensity = 0f;
    private float lastSmashTimestamp = -10f;
    private string statusMessage = "";
    private float statusTimer = 0f;

    // Cached HUD canvas references to toggle
    private GameObject mobileHudCanvas;

    /// <summary>Raised on every strike: (striker, receiver, feedback text or null).</summary>
    public event System.Action<RockItem, RockItem, string> OnStrike;
    /// <summary>Raised right after first-person strike mode closes (put away, stone broken...).</summary>
    public event System.Action OnSmashModeExited;
    /// <summary>Hide the IMGUI title/instruction banner (tutorials show their own objective instead).</summary>
    [HideInInspector] public bool showInstructionBanner = true;

    public bool IsInSmashMode => isInSmashMode;
    public RockItem LeftRock => rockLeft;
    public RockItem RightRock => rockRight;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainCamera == null) mainCamera = Camera.main;
        FindReferences();
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        FindReferences();
        EnsureFirstPersonCameraExists();
    }

    private void FindReferences()
    {
        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (touchLook == null) touchLook = FindAnyObjectByType<MobileFreeLookTouchController>();
        if (mainCamera == null) mainCamera = Camera.main;

        if (mobileHudCanvas == null)
        {
            mobileHudCanvas = GameObject.Find("MobileHUD_Canvas");
        }

        if (fpCamera == null)
        {
            GameObject fpGo = GameObject.Find("FirstPersonRockCamera");
            if (fpGo != null) fpCamera = fpGo.GetComponent<CinemachineCamera>();
        }
    }

    private void EnsureFirstPersonCameraExists()
    {
        if (fpCamera == null)
        {
            GameObject existing = GameObject.Find("FirstPersonRockCamera");
            if (existing != null)
            {
                fpCamera = existing.GetComponent<CinemachineCamera>();
            }
            else if (player != null)
            {
                GameObject fpGo = new GameObject("FirstPersonRockCamera");
                fpGo.transform.SetParent(player.transform, false);
                fpGo.transform.localPosition = new Vector3(0f, 1.55f, 0.22f);
                fpGo.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

                fpCamera = fpGo.AddComponent<CinemachineCamera>();
                fpCamera.Priority.Value = 0;
                fpCamera.enabled = false;
            }
        }
    }

    public void StartFirstPersonSmash(RockItem r1, RockItem r2)
    {
        if (isInSmashMode || r1 == null || r2 == null) return;

        FindReferences();
        EnsureFirstPersonCameraExists();

        isInSmashMode = true;
        isResolvingSmash = false;
        rockLeft = r1;
        rockRight = r2;

        // Save original scales
        rockLeftOrigScale = r1.transform.localScale;
        rockRightOrigScale = r2.transform.localScale;

        // Ensure rigidbodies are kinematic
        Rigidbody rb1 = r1.GetComponent<Rigidbody>();
        if (rb1 != null) rb1.isKinematic = true;
        Rigidbody rb2 = r2.GetComponent<Rigidbody>();
        if (rb2 != null) rb2.isKinematic = true;

        // Initialize 1D positions
        currentLeftX = leftRestX;
        currentRightX = rightRestX;
        prevLeftX = leftRestX;
        prevRightX = rightRestX;
        leftVelocity = 0f;
        rightVelocity = 0f;

        isDraggingLeft = false;
        isDraggingRight = false;
        leftFingerId = -1;
        rightFingerId = -1;

        // Lock player walking and camera orbit
        if (player != null) player.SetControlsLocked(true);
        if (touchLook != null) touchLook.isInputDisabled = true;

        // Activate Cinemachine First Person camera with smooth blend
        if (fpCamera != null)
        {
            fpCamera.enabled = true;
            fpCamera.Priority.Value = 25;
        }

        // Hide 3rd person mobile controls while in smash minigame
        ToggleThirdPersonHUD(false);

        ShowSmashUI(true);
        HideHudButtons(true);
        HideNearbyActors(true);
        ResetTimingBar();
        JbelImpactFX.EnsureListener(fpCamera);
        Debug.Log("<color=#4CD964><b>[RockDragManager]</b></color> Entered First-Person Rock Smashing Mode.");
    }

    public void ExitFirstPersonSmashMode()
    {
        if (!isInSmashMode) return;

        isInSmashMode = false;
        isResolvingSmash = false;

        // Restore scale on remaining rocks
        if (rockLeft != null) rockLeft.transform.localScale = rockLeftOrigScale;
        if (rockRight != null) rockRight.transform.localScale = rockRightOrigScale;

        rockLeft = null;
        rockRight = null;

        // Deactivate first-person camera (smooth blend back to 3rd person)
        if (fpCamera != null)
        {
            fpCamera.Priority.Value = 0;
            fpCamera.enabled = false;
        }

        // Unlock player walking and camera orbit
        if (player != null) player.SetControlsLocked(false);
        if (touchLook != null) touchLook.isInputDisabled = false;

        // Restore 3rd person mobile controls
        ToggleThirdPersonHUD(true);

        ShowSmashUI(false);
        HideHudButtons(false);
        HideNearbyActors(false);
        OnSmashModeExited?.Invoke();
        Debug.Log("<color=#64D2FF><b>[RockDragManager]</b></color> Exited First-Person Rock Smashing Mode.");
    }

    private void ToggleThirdPersonHUD(bool active)
    {
        if (mobileHudCanvas == null) mobileHudCanvas = GameObject.Find("MobileHUD_Canvas");
        if (mobileHudCanvas != null)
        {
            Transform joystick = mobileHudCanvas.transform.Find("JoystickZone");
            Transform jump = mobileHudCanvas.transform.Find("JumpButton");
            Transform pick = mobileHudCanvas.transform.Find("PickButton");
            Transform putDown = mobileHudCanvas.transform.Find("PutDownButton");
            Transform smash = mobileHudCanvas.transform.Find("SmashButton");

            if (joystick != null) joystick.gameObject.SetActive(active);
            if (jump != null) jump.gameObject.SetActive(active);
            if (pick != null) pick.gameObject.SetActive(false); // Pick button removed
            if (putDown != null)
            {
                bool hasRocks = TopDownPlayerController.Instance != null && TopDownPlayerController.Instance.HeldRockCount > 0;
                putDown.gameObject.SetActive(active && hasRocks);
            }
            if (smash != null) smash.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isInSmashMode) return;

        if (mainCamera == null) mainCamera = Camera.main;

        // Update velocity from previous frame
        if (Time.deltaTime > 0f)
        {
            leftVelocity = (currentLeftX - prevLeftX) / Time.deltaTime;
            rightVelocity = (currentRightX - prevRightX) / Time.deltaTime;
        }
        prevLeftX = currentLeftX;
        prevRightX = currentRightX;

        if (timingStrike) UpdateTimingStrike();
        else
        {
            // Handle inputs
            HandleTouchInput();
            HandleMouseAndKeyboardInput();

            // Check 1D collision along X axis
            Check1DCollision();
        }

        // Update screen shake
        UpdateScreenShake();

        // Update status timer
        if (statusTimer > 0f) statusTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        Update_SmashUI();
        if (!isInSmashMode) return;
        if (mainCamera == null) mainCamera = Camera.main;

        UpdateRockTransforms();
    }

    private void HandleTouchInput()
    {
        if (isResolvingSmash) return;

        var touches = Touch.activeTouches;

        bool foundLeft = false;
        bool foundRight = false;

        foreach (var touch in touches)
        {
            int touchId = touch.touchId;
            Vector2 screenPos = touch.screenPosition;

            // Check if touch is on left half or right half
            bool isScreenLeft = screenPos.x < Screen.width * 0.5f;

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                if (isScreenLeft && leftFingerId == -1)
                {
                    leftFingerId = touchId;
                    isDraggingLeft = true;
                }
                else if (!isScreenLeft && rightFingerId == -1)
                {
                    rightFingerId = touchId;
                    isDraggingRight = true;
                }
            }

            if (touchId == leftFingerId)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    foundLeft = true;
                    float targetX = ScreenToLocalX(screenPos);
                    targetX = Mathf.Clamp(targetX, minX, currentRightX - minCollisionDistance);
                    currentLeftX = Mathf.MoveTowards(currentLeftX, targetX, dragFollowSpeed * Time.deltaTime);
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    leftFingerId = -1;
                    isDraggingLeft = false;
                }
            }
            else if (touchId == rightFingerId)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    foundRight = true;
                    float targetX = ScreenToLocalX(screenPos);
                    targetX = Mathf.Clamp(targetX, currentLeftX + minCollisionDistance, maxX);
                    currentRightX = Mathf.MoveTowards(currentRightX, targetX, dragFollowSpeed * Time.deltaTime);
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    rightFingerId = -1;
                    isDraggingRight = false;
                }
            }
        }

        if (!foundLeft && leftFingerId != -1)
        {
            leftFingerId = -1;
            isDraggingLeft = false;
        }
        if (!foundRight && rightFingerId != -1)
        {
            rightFingerId = -1;
            isDraggingRight = false;
        }

        // Return to rest position if not dragging
        if (!isDraggingLeft)
        {
            currentLeftX = Mathf.SmoothDamp(currentLeftX, leftRestX, ref leftReturnVel, springReturnTime);
        }
        if (!isDraggingRight)
        {
            currentRightX = Mathf.SmoothDamp(currentRightX, rightRestX, ref rightReturnVel, springReturnTime);
        }
    }

    private void HandleMouseAndKeyboardInput()
    {
        if (isResolvingSmash) return;

        // Mouse Dragging Fallback (Editor / PC)
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            bool isScreenLeft = mousePos.x < Screen.width * 0.5f;

            // Left Mouse Button
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (isScreenLeft) isDraggingLeft = true;
                else isDraggingRight = true;
            }

            if (Mouse.current.leftButton.isPressed)
            {
                float targetX = ScreenToLocalX(mousePos);
                if (isDraggingLeft)
                {
                    targetX = Mathf.Clamp(targetX, minX, currentRightX - minCollisionDistance);
                    currentLeftX = Mathf.MoveTowards(currentLeftX, targetX, dragFollowSpeed * Time.deltaTime);
                }
                else if (isDraggingRight)
                {
                    targetX = Mathf.Clamp(targetX, currentLeftX + minCollisionDistance, maxX);
                    currentRightX = Mathf.MoveTowards(currentRightX, targetX, dragFollowSpeed * Time.deltaTime);
                }
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDraggingLeft = false;
                isDraggingRight = false;
            }

            // Right Mouse Button allows dragging the other rock simultaneously
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (isScreenLeft) isDraggingLeft = true;
                else isDraggingRight = true;
            }
            if (Mouse.current.rightButton.isPressed)
            {
                float targetX = ScreenToLocalX(mousePos);
                if (isScreenLeft)
                {
                    targetX = Mathf.Clamp(targetX, minX, currentRightX - minCollisionDistance);
                    currentLeftX = Mathf.MoveTowards(currentLeftX, targetX, dragFollowSpeed * Time.deltaTime);
                }
                else
                {
                    targetX = Mathf.Clamp(targetX, currentLeftX + minCollisionDistance, maxX);
                    currentRightX = Mathf.MoveTowards(currentRightX, targetX, dragFollowSpeed * Time.deltaTime);
                }
            }
            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                if (isScreenLeft) isDraggingLeft = false;
                else isDraggingRight = false;
            }
        }

        // Keyboard Controls Fallback (A/D for left rock, Left/Right for right rock)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
            {
                currentLeftX = Mathf.MoveTowards(currentLeftX, minX, 3f * Time.deltaTime);
            }
            if (Keyboard.current.dKey.isPressed)
            {
                float targetX = currentRightX - minCollisionDistance;
                currentLeftX = Mathf.MoveTowards(currentLeftX, targetX, 5f * Time.deltaTime);
            }

            if (Keyboard.current.leftArrowKey.isPressed)
            {
                float targetX = currentLeftX + minCollisionDistance;
                currentRightX = Mathf.MoveTowards(currentRightX, targetX, 5f * Time.deltaTime);
            }
            if (Keyboard.current.rightArrowKey.isPressed)
            {
                currentRightX = Mathf.MoveTowards(currentRightX, maxX, 3f * Time.deltaTime);
            }

            // Quick smash cheat: Space or F
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
            {
                ExecuteDirectSmashCheat();
            }

            // Escape key exits first person
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitFirstPersonSmashMode();
            }
        }
    }

    private float ScreenToLocalX(Vector2 screenPos)
    {
        if (mainCamera == null) return 0f;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(mainCamera.transform.forward, mainCamera.transform.position + mainCamera.transform.forward * viewDepth);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldHit = ray.GetPoint(enter);
            Vector3 localPt = mainCamera.transform.InverseTransformPoint(worldHit);
            return localPt.x;
        }

        return 0f;
    }

    private void Check1DCollision()
    {
        if (isResolvingSmash || rockLeft == null || rockRight == null) return;

        float distance = currentRightX - currentLeftX;

        // Check if rocks made physical contact along X
        if (distance <= minCollisionDistance + 0.025f)
        {
            float relSpeed = Mathf.Abs(leftVelocity - rightVelocity);
            float maxSingleSpeed = Mathf.Max(Mathf.Abs(leftVelocity), Mathf.Abs(rightVelocity));
            float impactSpeed = Mathf.Max(relSpeed, maxSingleSpeed);

            if (impactSpeed >= minSmashSpeed && (Time.time - lastSmashTimestamp > 0.4f))
            {
                TriggerSmashImpact(impactSpeed);
            }
        }
    }

    private void TriggerSmashImpact(float impactSpeed)
    {
        if (isResolvingSmash || rockLeft == null || rockRight == null) return;

        lastSmashTimestamp = Time.time;
        isDraggingLeft = false;
        isDraggingRight = false;

        // Visual contact midpoint in world
        Vector3 contactMid = (rockLeft.transform.position + rockRight.transform.position) * 0.5f;

        // Impact feedback scaled by the stones' hardness: particles, sound, haptics, camera shake, hit-stop
        {
            float hA = rockLeft.rockData != null ? rockLeft.rockData.hardness : rockLeft.strength;
            float hB = rockRight.rockData != null ? rockRight.rockData.hardness : rockRight.strength;
            RockItem softer = hA <= hB ? rockLeft : rockRight;
            Color dust = softer.rockData != null ? softer.rockData.residueColor : Color.gray;
            JbelImpactFX.Strike(contactMid, hA, hB, dust, Mathf.Clamp(impactSpeed / 4f, 0.5f, 1.3f));
        }

        // Hands jolt (the world shake comes from the camera impulse)
        shakeIntensity = 0.02f;
        if (smashHint != null) smashHintSeen = true;

        // Evaluate impact using Prehistoric Lithic Matrix if RockData is available
        var matrix = Resources.Load<JbelIghoud.Lithic.RockInteractionMatrix>("DefaultRockInteractionMatrix");
        string outcomeMessage = null;

        if (matrix != null && (rockLeft.rockData != null || rockRight.rockData != null))
        {
            bool leftFaster = Mathf.Abs(leftVelocity) >= Mathf.Abs(rightVelocity);
            RockItem striker = leftFaster ? rockLeft : rockRight;
            RockItem receiver = leftFaster ? rockRight : rockLeft;

            var result = matrix.ResolveCollision(striker.rockData, receiver.rockData, impactSpeed);
            if (result.outcomeType != JbelIghoud.Lithic.InteractionOutcomeType.NoEffect)
            {
                outcomeMessage = result.feedbackMessage;

                if (result.spawnedYieldPrefab != null)
                {
                    Instantiate(result.spawnedYieldPrefab, contactMid + Vector3.up * 0.15f, Quaternion.identity);
                }

                if (result.damageToStriker > 0) striker.TakeHit(result.damageToStriker);
                if (result.damageToReceiver > 0) receiver.TakeHit(result.damageToReceiver);
            }
            else
            {
                rockLeft.TakeHit();
                rockRight.TakeHit();
            }
        }
        else
        {
            // Apply hits to both rocks according to durability rules
            rockLeft.TakeHit();
            rockRight.TakeHit();
        }

        {
            bool lf = Mathf.Abs(leftVelocity) >= Mathf.Abs(rightVelocity);
            OnStrike?.Invoke(lf ? rockLeft : rockRight, lf ? rockRight : rockLeft, outcomeMessage);
        }

        // Check if one or both rocks were shattered
        bool leftDestroyed = (rockLeft == null) || (rockLeft.HitsRemaining <= 0);
        bool rightDestroyed = (rockRight == null) || (rockRight.HitsRemaining <= 0);

        if (leftDestroyed || rightDestroyed)
        {
            isResolvingSmash = true;
            string msg = !string.IsNullOrEmpty(outcomeMessage) ? $" {outcomeMessage}" : " ROCK SHATTERED! Great strike!";
            ShowStatus(msg, 3.0f);
            StartCoroutine(PostSmashCompletionRoutine());
        }
        else
        {
            // Recoil bounce back
            currentLeftX = leftRestX - 0.08f;
            currentRightX = rightRestX + 0.08f;
            string msg = !string.IsNullOrEmpty(outcomeMessage) ? $" {outcomeMessage}" : $" TOUGH ROCK! Hit again! (STR: {rockRight.strength:F1})";
            ShowStatus(msg, 2.5f);
        }
    }

    private IEnumerator PostSmashCompletionRoutine()
    {
        // Wait for debris particles and satisfaction of destruction
        yield return new WaitForSeconds(0.85f);

        ExitFirstPersonSmashMode();
    }

    private void ExecuteDirectSmashCheat()
    {
        if (!isInSmashMode || rockLeft == null || rockRight == null) return;

        currentLeftX = -minCollisionDistance * 0.5f;
        currentRightX = +minCollisionDistance * 0.5f;
        leftVelocity = 6f;
        rightVelocity = -6f;
        TriggerSmashImpact(6.5f);
    }

    private void UpdateRockTransforms()
    {
        // Smoothly position rocks in front of camera strictly along 1D X-axis
        Vector3 leftLocalPos = new Vector3(currentLeftX, viewHeight, viewDepth) + shakeOffset;
        Vector3 rightLocalPos = new Vector3(currentRightX, viewHeight, viewDepth) + shakeOffset;


        if (rockLeft != null)
        {
            rockLeft.transform.position = mainCamera.transform.TransformPoint(leftLocalPos);
            // Slight tilt inwards towards each other
            rockLeft.transform.rotation = mainCamera.transform.rotation * Quaternion.Euler(10f, 25f, -15f);
            rockLeft.transform.localScale = Vector3.Lerp(rockLeft.transform.localScale, HandheldScale(rockLeft), 15f * Time.deltaTime);
        }

        if (rockRight != null)
        {
            rockRight.transform.position = mainCamera.transform.TransformPoint(rightLocalPos);
            // Slight tilt inwards towards each other
            rockRight.transform.rotation = mainCamera.transform.rotation * Quaternion.Euler(10f, -25f, 15f);
            rockRight.transform.localScale = Vector3.Lerp(rockRight.transform.localScale, HandheldScale(rockRight), 15f * Time.deltaTime);
        }
    }

    // ================================================================== TIMING STRIKE

    private readonly List<Renderer> hiddenActors = new List<Renderer>();

    /// <summary>Hide characters standing right in front of the first-person view (e.g. the Elder).</summary>
    private void HideNearbyActors(bool hide)
    {
        if (!hide)
        {
            foreach (var r in hiddenActors) if (r != null) r.enabled = true;
            hiddenActors.Clear();
            return;
        }
        var cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return;
        foreach (var a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (player != null && a.transform.IsChildOf(player.transform)) continue;
            Vector3 to = a.transform.position - cam.transform.position;
            if (to.magnitude > 8f || Vector3.Dot(to.normalized, cam.transform.forward) < 0.3f) continue; // only people in front of the view
            foreach (var r in a.transform.root.GetComponentsInChildren<Renderer>())
                if (r.enabled && !(r is ParticleSystemRenderer)) { r.enabled = false; hiddenActors.Add(r); }
        }
    }

    private void ResetTimingBar()
    {
        needleT = 0f; needleDir = 1f;
        needleSpeed = startSpeed;
        greenWidth = startGreenWidth;
        greenCenter = Random.Range(0.3f, 0.7f);
        hitAnimating = false;
        currentLeftX = leftRestX; currentRightX = rightRestX;
        LayoutBar();
    }

    private void BuildTimingBar(TMPro.TMP_FontAsset font)
    {
        Sprite fill = null, green = null, needle = null;
#if UNITY_EDITOR
        fill = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/breaking the rocks/fill.png");
        green = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/breaking the rocks/green zone.png");
        needle = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/breaking the rocks/moving vertex.png");
#endif
        if (barFillSprite != null) fill = barFillSprite;
        if (barGreenSprite != null) green = barGreenSprite;
        if (barNeedleSprite != null) needle = barNeedleSprite;

        var bar = new GameObject("TimingBar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bar.transform.SetParent(smashUI.transform, false);
        barRT = (RectTransform)bar.transform;
        barRT.anchorMin = barRT.anchorMax = barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 150f);
        barRT.sizeDelta = new Vector2(880f, 55f);
        var bi = bar.GetComponent<UnityEngine.UI.Image>(); bi.sprite = fill; bi.raycastTarget = false;
        if (fill == null) bi.color = new Color(0.72f, 0.45f, 0.21f);

        var g = new GameObject("GreenZone", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        g.transform.SetParent(bar.transform, false);
        greenRT = (RectTransform)g.transform;
        greenRT.anchorMin = new Vector2(0f, 0f); greenRT.anchorMax = new Vector2(0f, 1f); greenRT.pivot = new Vector2(0.5f, 0.5f);
        var gi = g.GetComponent<UnityEngine.UI.Image>(); gi.sprite = green; gi.raycastTarget = false;
        if (green == null) gi.color = new Color(0.4f, 0.75f, 0.33f);

        var nd = new GameObject("Needle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        nd.transform.SetParent(bar.transform, false);
        needleRT = (RectTransform)nd.transform;
        needleRT.anchorMin = needleRT.anchorMax = new Vector2(0f, 0.5f); needleRT.pivot = new Vector2(0.5f, 0.5f);
        needleRT.sizeDelta = new Vector2(26f, 104f);
        var ni = nd.GetComponent<UnityEngine.UI.Image>(); ni.sprite = needle; ni.raycastTarget = false;
        if (needle == null) ni.color = new Color(0.97f, 0.94f, 0.86f);

        var pop = new GameObject("HitPopup", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        pop.transform.SetParent(bar.transform, false);
        var prt = (RectTransform)pop.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 40f); prt.sizeDelta = new Vector2(600f, 70f);
        hitPopup = pop.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) hitPopup.font = font;
        hitPopup.fontSize = 46f; hitPopup.alignment = TMPro.TextAlignmentOptions.Center; hitPopup.raycastTarget = false;
        hitPopup.color = new Color(1f, 1f, 1f, 0f);
        if (smashHint != null) ((RectTransform)smashHint.transform).anchoredPosition = new Vector2(0f, 225f);
        LayoutBar();
    }

    private void LayoutBar()
    {
        if (barRT == null) return;
        float w = barRT.rect.width > 1f ? barRT.rect.width : barRT.sizeDelta.x;
        greenRT.sizeDelta = new Vector2(w * greenWidth, 0f);
        greenRT.anchoredPosition = new Vector2(w * greenCenter, 0f);
        needleRT.anchoredPosition = new Vector2(w * needleT, 0f);
    }

    private void UpdateTimingStrike()
    {
        if (barRT != null) barRT.gameObject.SetActive(isInSmashMode);
        if (!isResolvingSmash && !hitAnimating)
        {
            needleT += needleDir * needleSpeed * Time.unscaledDeltaTime;
            if (needleT >= 1f) { needleT = 1f; needleDir = -1f; }
            else if (needleT <= 0f) { needleT = 0f; needleDir = 1f; }
            if (TapThisFrame()) TimingHit();
        }
        LayoutBar();
        if (hitPopup != null && popupTime > 0f)
        {
            popupTime -= Time.unscaledDeltaTime;
            var c = hitPopup.color; c.a = Mathf.Clamp01(popupTime / 0.35f); hitPopup.color = c;
            ((RectTransform)hitPopup.transform).anchoredPosition = new Vector2(0f, 40f + (0.8f - popupTime) * 50f);
        }
    }

    /// <summary>A tap anywhere that isn't on a button (e.g. PUT AWAY). Space works in the editor.</summary>
    private bool TapThisFrame()
    {
        Vector2 pos = default; bool tapped = false;
        foreach (var t in Touch.activeTouches)
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) { pos = t.screenPosition; tapped = true; break; }
        if (!tapped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { pos = Mouse.current.position.ReadValue(); tapped = true; }
        if (!tapped && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
        if (!tapped) return false;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null)
        {
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = pos };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null) return false;
        }
        return true;
    }

    private void TimingHit()
    {
        if (rockLeft == null || rockRight == null) return;
        bool strong = Mathf.Abs(needleT - greenCenter) <= greenWidth * 0.5f;
        StartCoroutine(TimedHitRoutine(strong));
    }

    private IEnumerator TimedHitRoutine(bool strong)
    {
        hitAnimating = true;
        // the hammer (right) swings into the stone (left)
        float contactX = currentLeftX + minCollisionDistance;
        float wind = rightRestX + 0.12f;
        for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime) { currentRightX = Mathf.Lerp(rightRestX, wind, t / 0.08f); yield return null; }
        for (float t = 0f; t < 0.07f; t += Time.unscaledDeltaTime) { float k = t / 0.07f; currentRightX = Mathf.Lerp(wind, contactX, k * k); yield return null; }
        currentRightX = contactX;
        ResolveTimedImpact(strong);
        // bounce back
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime) { currentRightX = Mathf.Lerp(contactX, rightRestX, Mathf.SmoothStep(0f, 1f, t / 0.18f)); yield return null; }
        currentRightX = rightRestX;
        currentLeftX = leftRestX;
        // every hit: faster needle, smaller green zone
        needleSpeed = Mathf.Min(maxSpeed, needleSpeed * speedGainPerHit);
        greenWidth = Mathf.Max(minGreenWidth, greenWidth * greenShrinkPerHit);
        greenCenter = Random.Range(0.15f + greenWidth * 0.5f, 0.85f - greenWidth * 0.5f);
        hitAnimating = false;
    }

    private void ResolveTimedImpact(bool strong)
    {
        if (rockLeft == null || rockRight == null) return;
        lastSmashTimestamp = Time.time;
        Vector3 contactMid = (rockLeft.transform.position + rockRight.transform.position) * 0.5f;
        float hA = rockLeft.rockData != null ? rockLeft.rockData.hardness : rockLeft.strength;
        float hB = rockRight.rockData != null ? rockRight.rockData.hardness : rockRight.strength;
        RockItem softer = hA <= hB ? rockLeft : rockRight;
        Color dust = softer.rockData != null ? softer.rockData.residueColor : Color.gray;
        JbelImpactFX.Strike(contactMid, hA, hB, dust, strong ? 1.3f : 0.55f);
        shakeIntensity = strong ? 0.03f : 0.012f;

        if (hitPopup != null)
        {
            hitPopup.text = strong ? "STRONG HIT  +2" : "WEAK  +0.4";
            hitPopup.color = strong ? new Color(0.55f, 0.95f, 0.45f, 1f) : new Color(0.96f, 0.86f, 0.6f, 1f);
            popupTime = 0.8f;
        }
        OnStrike?.Invoke(rockRight, rockLeft, null);

        float dmg = strong ? strongDamage : weakDamage;
        // the stone being broken is the one on the left; a non-hammer right stone wears too
        rockLeft.TakeDamage(dmg);
        if (rockRight != null && !rockRight.isHammerstone) rockRight.TakeDamage(dmg * 0.5f);

        bool leftDestroyed = rockLeft == null || rockLeft.HitsRemaining <= 0;
        bool rightDestroyed = rockRight == null || rockRight.HitsRemaining <= 0;
        if (leftDestroyed || rightDestroyed)
        {
            isResolvingSmash = true;
            StartCoroutine(PostSmashCompletionRoutine());
        }
    }

    /// <summary>Same visual size in hand whatever the rock mesh's raw scale is.</summary>
    private Vector3 HandheldScale(RockItem rock)
    {
        if (handheldRockSize <= 0f) return Vector3.one * handheldRockScale;
        var mf = rock.GetComponent<MeshFilter>();
        float meshSize = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds.size.magnitude : 0f;
        if (meshSize < 0.0001f) return Vector3.one * handheldRockScale;
        return Vector3.one * (handheldRockSize / meshSize);
    }

    private void UpdateScreenShake()
    {
        if (shakeIntensity > 0.001f)
        {
            shakeOffset = new Vector3(
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity * 0.5f, 0f)
            );
            shakeIntensity *= Mathf.Exp(-18f * Time.unscaledDeltaTime);
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
    }

    private void SpawnSmashVFX(Vector3 pos) { } // replaced by JbelImpactFX

    private void ShowStatus(string msg, float duration)
    {
        statusMessage = msg;
        statusTimer = duration;
    }

    // ================================================================== SMASH UI (uGUI, game style)
    private GameObject smashUI;
    private TMPro.TextMeshProUGUI smashHint;
    private bool smashHintSeen;
    private readonly System.Collections.Generic.List<GameObject> hiddenHud = new System.Collections.Generic.List<GameObject>();

    private void ShowSmashUI(bool show)
    {
        if (show && smashUI == null) BuildSmashUI();
        if (smashUI != null) smashUI.SetActive(show);
        if (show) smashHintSeen = false;
    }

    private void BuildSmashUI()
    {
        smashUI = new GameObject("SmashUI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        DontDestroyOnLoad(smashUI);
        var canvas = smashUI.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = smashUI.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // PUT AWAY: a copy of the HUD's BAG button so it matches the game's style
        var hud = FindAnyObjectByType<JbelIghoud.UI.JbelHUDController>();
        TMPro.TMP_FontAsset font = null;
        if (hud != null && hud.BagButton != null)
        {
            var b = JbelIghoud.UI.JbelHudButtonFactory.Create(hud.BagButton, smashUI.transform, "PutAwayButton", "PUT AWAY");
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-70f, 60f);
            b.onClick.AddListener(ExitFirstPersonSmashMode);
        }
        font = JbelIghoud.UI.JbelHudButtonFactory.HudFont();

        // gesture hint (hidden during tutorials: the objective banner says what to do)
        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        hintGo.transform.SetParent(smashUI.transform, false);
        var hrt = (RectTransform)hintGo.transform;
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 70f);
        hrt.sizeDelta = new Vector2(900f, 60f);
        smashHint = hintGo.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) smashHint.font = font;
        smashHint.text = timingStrike ? "TAP WHEN THE MARK IS IN THE GREEN" : "DRAG THE STONES INTO EACH OTHER";
        BuildTimingBar(font);
        smashHint.fontSize = 34f;
        smashHint.alignment = TMPro.TextAlignmentOptions.Center;
        smashHint.color = new Color(0.96f, 0.86f, 0.6f, 0.9f);
        smashHint.raycastTarget = false;
    }

    private void Update_SmashUI()
    {
        if (smashHint == null) return;
        float target = (isInSmashMode && showInstructionBanner && !smashHintSeen) ? 0.9f : 0f;
        var c = smashHint.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 3f);
        smashHint.color = c;
    }

    /// <summary>Hide every HUD action button (JUMP / MAP / COLLECT / BAG / TALK / STORE) while striking.</summary>
    private void HideHudButtons(bool hide)
    {
        if (!hide)
        {
            foreach (var g in hiddenHud) if (g != null) g.SetActive(true);
            hiddenHud.Clear();
            return;
        }
        if (mobileHudCanvas == null) mobileHudCanvas = GameObject.Find("MobileHUD_Canvas");
        if (mobileHudCanvas == null) return;
        string[] keys = { "Jump", "Map", "Collect", "Bag", "Talk", "ReturnToInventory", "Dive", "PutDown" };
        foreach (var b in mobileHudCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(false))
        {
            string n = b.gameObject.name;
            if (n.Contains("Modal") || n.Contains("Close")) continue;
            foreach (var k in keys)
                if (n.Contains(k)) { b.gameObject.SetActive(false); hiddenHud.Add(b.gameObject); break; }
        }
    }
}
