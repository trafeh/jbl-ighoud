using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Normal walk speed in units per second.")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("Sprint/Boost speed in units per second.")]
    [SerializeField] private float sprintSpeed = 9f;

    [Tooltip("How quickly the character reaches full speed.")]
    [SerializeField] private float acceleration = 15f;

    [Tooltip("How quickly the character stops when input is released.")]
    [SerializeField] private float deceleration = 20f;

    [Tooltip("Speed of turning towards movement direction in degrees per second.")]
    [SerializeField] private float rotationSpeed = 720f;

    [Tooltip("Smoothing rate for Free Fire character turning interpolation.")]
    [SerializeField] private float turnSmoothing = 16f;

    [Header("Jump & Gravity")]
    [Tooltip("Jump apex height in units.")]
    [SerializeField] private float jumpHeight = 1.8f;

    [Tooltip("Downward gravitational acceleration.")]
    [SerializeField] private float gravity = 25f;

    [Tooltip("Downward grounding force when standing on terrain.")]
    [SerializeField] private float groundedStickForce = 3f;

    [Header("Slope & Mountain Border Protection")]
    [Tooltip("Maximum walkable slope angle in degrees. Slopes steeper than this prevent jumping and force sliding down.")]
    [SerializeField] private float maxSlopeAngle = 45f;

    [Tooltip("Maximum allowed player altitude in units before mountain borders force sliding back down.")]
    [SerializeField] private float maxBorderAltitude = 65f;

    [Tooltip("Speed at which the character slides down steep mountain slopes.")]
    [SerializeField] private float slopeSlideSpeed = 14f;

    [Tooltip("Horizontal push force away from steep mountain faces.")]
    [SerializeField] private float slopeRepelForce = 6f;

    [Header("Pickup Settings")]
    [Tooltip("Distance within which the player can pick up items.")]
    [SerializeField] private float pickupRadius = 3.8f;

    [Tooltip("Maximum number of rocks the player can hold at once.")]
    [SerializeField] private int maxHeldRocks = 1;

    [Header("Water & Swimming Settings")]
    [Tooltip("Layer mask used for detecting water bodies.")]
    [SerializeField] private LayerMask waterLayerMask = 1 << 4;

    [Tooltip("Swimming movement speed in units per second.")]
    [SerializeField] private float swimSpeed = 3.8f;

    [Tooltip("Swimming sprint/boost speed in units per second.")]
    [SerializeField] private float swimSprintSpeed = 5.6f;

    [Tooltip("Acceleration while swimming in water.")]
    [SerializeField] private float swimAcceleration = 8f;

    [Tooltip("Deceleration / fluid drag glide when swimming input is released.")]
    [SerializeField] private float swimDeceleration = 5f;

    [Tooltip("Speed of turning towards movement direction while swimming.")]
    [SerializeField] private float swimRotationSpeed = 540f;

    [Tooltip("Upward buoyant force/speed lifting player towards water surface.")]
    [SerializeField] private float buoyancyStrength = 7f;

    [Tooltip("Vertical drag applied when falling/plunging into water.")]
    [SerializeField] private float waterVerticalDrag = 12f;

    [Tooltip("Height of neck above character base (feet). When water level reaches this height, swimming activates.")]
    [SerializeField] private float neckHeight = 1.48f;

    [Tooltip("Depth below water surface at which character floats while swimming (keeps head/neck above water).")]
    [SerializeField] private float swimFloatDepth = 1.35f;

    [Tooltip("Upward breach jump impulse when pressing jump while swimming.")]
    [SerializeField] private float swimJumpForce = 5.0f;

    [Tooltip("Allow a single breach jump from water surface to climb onto banks. If false, jumping while swimming is disabled.")]
    [SerializeField] private bool allowSwimBreachJump = true;

    [Tooltip("Minimum cooldown in seconds between water breach jumps.")]
    [SerializeField] private float swimJumpCooldown = 1.2f;

    private bool hasSwimJumped = false;

    [Header("Wading & Sea Drag Settings")]
    [Tooltip("Speed multiplier applied when stepping into shallow water (ankles/shins).")]
    [SerializeField] private float wadingMaxSpeedMultiplier = 0.50f;

    [Tooltip("Speed multiplier applied when wading deeper up to the neck.")]
    [SerializeField] private float wadingMinSpeedMultiplier = 0.30f;

    [Tooltip("Acceleration rate while wading through water.")]
    [SerializeField] private float wadingAcceleration = 7f;

    [Tooltip("Deceleration rate while wading through water.")]
    [SerializeField] private float wadingDeceleration = 12f;

    [Tooltip("Optional Suimono water splash and wake VFX attached to the player.")]
    [SerializeField] private SuimonoWaterVFX suimonoWaterVFX;

    [Header("Diving & Underwater Settings")]
    [Tooltip("Target depth below water surface while diving underwater.")]
    [SerializeField] private float diveFloatDepth = 2.85f;

    [Tooltip("Vertical speed applied when diving down or surfacing.")]
    [SerializeField] private float diveVerticalSpeed = 4.2f;

    [Tooltip("Dive toggle button on mobile HUD.")]
    [SerializeField] private UnityEngine.UI.Button diveButton;

    [Header("References")]
    [SerializeField] private MobileJoystick joystick;
    [SerializeField] private UnityEngine.UI.Button jumpButton;
    [SerializeField] private UnityEngine.UI.Button pickButton;
    [Tooltip("HUD button to put down a held rock.")]
    [SerializeField] private UnityEngine.UI.Button putDownButton;
    [SerializeField] private UnityEngine.UI.Button smashButton;
    [SerializeField] private Animator animator;
    [SerializeField] private MobileFreeLookTouchController touchController;

    private static TopDownPlayerController _instance;
    public static TopDownPlayerController Instance => _instance;

    private float lastGroundedTime = -10f;
    private float lastJumpTime = -10f;

    private CharacterController characterController;
    private Vector3 currentVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private bool isSprinting = false;

    // Stable heading tracking during camera auto-alignment & continuous input
    private bool isInputActive = false;
    private float currentWorldMoveYaw = 0f;
    private float initialStickAngle = 0f;

    // Slope & mountain border tracking
    private Vector3 groundHitNormal = Vector3.up;
    private bool isOnSteepSlope = false;
    private float currentSlopeAngle = 0f;
    private Vector3 controllerSlopeNormal = Vector3.up;
    private bool hasControllerSlopeHit = false;

    // Held rocks collection (maximum 2)
    private List<RockItem> heldRocks = new List<RockItem>();

    // Water & Swimming state
    private bool isInWater = false;
    private bool isSwimming = false;
    private bool isDiving = false;
    private float currentWaterSurfaceY = 0f;
    private float currentSubmersionDepth = 0f;
    private WaterBody currentWaterBody = null;

    public event System.Action<bool> OnDiveStateChanged;

    public float CurrentSpeed => currentVelocity.magnitude;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.05f;
    public bool IsGrounded => (characterController != null ? characterController.isGrounded : false) && !isOnSteepSlope && !isSwimming;
    public bool IsOnSteepSlope => isOnSteepSlope && !isSwimming;
    public bool IsInWater => isInWater;
    public bool IsSwimming => isSwimming;
    public bool IsDiving => isDiving;
    public bool IsUnderwater => isInWater && (isDiving || (currentSubmersionDepth >= (neckHeight + 0.35f)));
    public float WaterSubmersionDepth => currentSubmersionDepth;
    public float WaterSurfaceY => currentWaterSurfaceY;
    public int MaxHeldRocks => maxHeldRocks;
    public int HeldRockCount => heldRocks.Count;
    public IReadOnlyList<RockItem> HeldRocks => heldRocks;
    public bool IsHandsFull => heldRocks.Count >= maxHeldRocks;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (maxHeldRocks <= 0) maxHeldRocks = 1;

        if (joystick == null)
        {
            joystick = FindAnyObjectByType<MobileJoystick>();
        }

        if (touchController == null)
        {
            touchController = FindAnyObjectByType<MobileFreeLookTouchController>();
        }

        if (jumpButton == null)
        {
            GameObject jGo = GameObject.Find("JumpButton");
            if (jGo != null) jumpButton = jGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (jumpButton != null)
        {
            jumpButton.onClick.RemoveListener(Jump);
            jumpButton.onClick.AddListener(Jump);
        }

        _instance = this;

        if (pickButton == null)
        {
            GameObject pGo = GameObject.Find("PickButton");
            if (pGo != null) pickButton = pGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (pickButton != null)
        {
            // Direct in-world pick button disabled per user request (rocks only picked up via inspection)
            pickButton.gameObject.SetActive(false);
        }

        if (putDownButton == null)
        {
            GameObject pdGo = GameObject.Find("PutDownButton");
            if (pdGo != null) putDownButton = pdGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (putDownButton != null)
        {
            putDownButton.onClick.RemoveListener(DropLastHeldRock);
            putDownButton.onClick.AddListener(DropLastHeldRock);
            putDownButton.gameObject.SetActive(heldRocks.Count > 0);
        }

        if (smashButton == null)
        {
            GameObject sGo = GameObject.Find("SmashButton");
            if (sGo != null) smashButton = sGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (smashButton != null)
        {
            smashButton.gameObject.SetActive(false);
        }

        if (diveButton == null)
        {
            GameObject dGo = GameObject.Find("DiveButton");
            if (dGo != null) diveButton = dGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (diveButton != null)
        {
            diveButton.onClick.RemoveListener(ToggleDive);
            diveButton.onClick.AddListener(ToggleDive);
            diveButton.gameObject.SetActive(false);
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.isHuman)
        {
            Transform neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (neckBone != null)
            {
                float calculatedNeckHeight = neckBone.position.y - transform.position.y;
                if (calculatedNeckHeight > 0.5f && calculatedNeckHeight < 2.5f)
                {
                    neckHeight = calculatedNeckHeight;
                }
            }
        }
        if (swimFloatDepth <= 0.1f)
        {
            swimFloatDepth = neckHeight * 0.90f;
        }

        if (suimonoWaterVFX == null)
        {
            suimonoWaterVFX = GetComponentInChildren<SuimonoWaterVFX>();
        }

        IgnoreWaterCollisions();
    }

    private void Start()
    {
        IgnoreWaterCollisions();
    }

    private bool isControlsLocked = false;
    public bool IsControlsLocked => isControlsLocked;

    public void SetControlsLocked(bool locked)
    {
        isControlsLocked = locked;
        if (locked)
        {
            if (joystick != null) joystick.UnlockSprint();
            currentVelocity = Vector3.zero;
            verticalVelocity = 0f;
            isInputActive = false;
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsSwimming", false);
            }
        }
        UpdatePutDownButtonVisuals();
    }

    private void Update()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();

        UpdatePutDownButtonVisuals();
        UpdateSmashButtonVisuals();
        UpdateDiveButtonVisuals();

        if (isControlsLocked || Time.timeScale <= 0f) return;

        UpdateWaterState();

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                ToggleDive();
            }
        }
#endif

        Vector2 input = GetInputVector();
        HandleMovement(input);
        UpdateAnimator();

        Shader.SetGlobalVector("_GlobalPlayerPos", transform.position);
        Shader.SetGlobalFloat("_GlobalPlayerRadius", 2.2f);
    }

    private void LateUpdate()
    {
        UpdateHeldRocksPosition();
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsSwimming", isSwimming);
            animator.SetBool("IsMoving", IsMoving);
            animator.SetFloat("Speed", CurrentSpeed);

            bool isGroundedForAnim = !isSwimming && characterController.isGrounded && (Time.time - lastJumpTime > 0.2f);
            animator.SetBool("IsGrounded", isGroundedForAnim);
        }
    }

    public void IgnoreWaterCollisions()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController == null) return;

        int waterLayer = LayerMask.NameToLayer("Water");
        var colliders = Object.FindObjectsByType<Collider>();
        foreach (var col in colliders)
        {
            if (col == null || col == characterController) continue;
            if ((waterLayer != -1 && col.gameObject.layer == waterLayer) || col.GetComponent<WaterBody>() != null)
            {
                Physics.IgnoreCollision(characterController, col, true);
            }
        }
    }

    private void UpdateWaterState()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();

        Vector3 pos = transform.position;
        bool foundWater = false;
        float bestSurfaceY = float.MinValue;
        WaterBody bestBody = null;

        // 1. Query registered WaterBody components first
        if (WaterBody.All.Count == 0)
        {
            var foundBodies = Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Exclude);
            for (int b = 0; b < foundBodies.Length; b++)
            {
                if (!WaterBody.All.Contains(foundBodies[b]))
                {
                    WaterBody.All.Add(foundBodies[b]);
                    foundBodies[b].UpdateBounds();
                }
            }
        }

        for (int i = 0; i < WaterBody.All.Count; i++)
        {
            var wb = WaterBody.All[i];
            if (wb != null && wb.TryGetWaterSurfaceY(pos, out float sY))
            {
                if (sY > bestSurfaceY)
                {
                    bestSurfaceY = sY;
                    bestBody = wb;
                    foundWater = true;
                }
            }
        }

        // 2. Fallback: Raycast down against water layer
        if (!foundWater)
        {
            int waterLayer = LayerMask.NameToLayer("Water");
            int mask = waterLayer != -1 ? (1 << waterLayer) : waterLayerMask.value;
            Vector3 rayStart = new Vector3(pos.x, pos.y + 6f, pos.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, mask, QueryTriggerInteraction.Collide))
            {
                bestSurfaceY = hit.point.y;
                foundWater = true;
            }
        }

        if (foundWater)
        {
            // Valid water immersion: character feet must be strictly at or below water surface
            // (pos is transform.position which corresponds to the soles of the character's feet)
            if (pos.y <= bestSurfaceY && pos.y >= bestSurfaceY - 15f)
            {
                isInWater = true;
                currentWaterSurfaceY = bestSurfaceY;
                currentSubmersionDepth = bestSurfaceY - pos.y;
                currentWaterBody = bestBody;
            }
            else
            {
                isInWater = false;
                currentSubmersionDepth = 0f;
                currentWaterBody = null;
            }
        }
        else
        {
            isInWater = false;
            currentSubmersionDepth = 0f;
            currentWaterBody = null;
        }

        // Evaluate Swimming transition:
        // Swimming starts when water reaches character's neck!
        if (isInWater)
        {
            if (!isSwimming && currentSubmersionDepth >= neckHeight)
            {
                isSwimming = true;
                verticalVelocity = Mathf.Clamp(verticalVelocity, -3f, 2f);
            }
            else if (isSwimming)
            {
                // Exit swimming when character's feet touch ground in shallow water (below neck)
                bool groundedOnTerrain = characterController != null && characterController.isGrounded;
                bool shallowEnough = currentSubmersionDepth < (neckHeight - 0.25f);
                if (groundedOnTerrain && shallowEnough)
                {
                    isSwimming = false;
                    if (isDiving) SetDiving(false);
                }
            }
        }
        else
        {
            isSwimming = false;
            if (isDiving) SetDiving(false);
        }
    }

    private void UpdatePutDownButtonVisuals()
    {
        if (pickButton != null && pickButton.gameObject.activeSelf)
        {
            pickButton.gameObject.SetActive(false);
        }

        if (putDownButton != null)
        {
            bool shouldShow = !isControlsLocked && heldRocks.Count > 0;
            if (putDownButton.gameObject.activeSelf != shouldShow)
            {
                putDownButton.gameObject.SetActive(shouldShow);
            }
        }
    }

    private void UpdateSmashButtonVisuals()
    {
        if (smashButton != null && smashButton.gameObject.activeSelf)
        {
            smashButton.gameObject.SetActive(false);
        }
    }

    public void ToggleDive()
    {
        if (!isInWater) return;
        SetDiving(!isDiving);
    }

    public void SetDiving(bool diving)
    {
        if (!isInWater && diving) return;
        if (isDiving == diving) return;

        isDiving = diving;

        if (isDiving)
        {
            isSwimming = true;
            verticalVelocity = -diveVerticalSpeed;
            if (suimonoWaterVFX != null && suimonoWaterVFX.SwimSplash != null)
            {
                suimonoWaterVFX.SwimSplash.Emit(14);
            }
        }
        else
        {
            verticalVelocity = 3.8f;
            if (suimonoWaterVFX != null && suimonoWaterVFX.SwimSplash != null)
            {
                suimonoWaterVFX.SwimSplash.Emit(18);
            }
        }

        UpdateDiveButtonVisuals();
        OnDiveStateChanged?.Invoke(isDiving);
    }

    public void SetDiveButton(UnityEngine.UI.Button button)
    {
        diveButton = button;
        if (diveButton != null)
        {
            diveButton.onClick.RemoveListener(ToggleDive);
            diveButton.onClick.AddListener(ToggleDive);
            UpdateDiveButtonVisuals();
        }
    }

    private void UpdateDiveButtonVisuals()
    {
        if (diveButton != null)
        {
            bool shouldShow = (isInWater || isSwimming) && !isControlsLocked;
            if (diveButton.gameObject.activeSelf != shouldShow)
            {
                diveButton.gameObject.SetActive(shouldShow);
            }

            var text = diveButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = isDiving ? "SURFACE" : "DIVE";
            }
            var tmp = diveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = isDiving ? "SURFACE" : "DIVE";
            }
        }
    }

    private Vector2 GetInputVector()
    {
        Vector2 input = Vector2.zero;

        // 1. Mobile Joystick input (Free Fire controls with auto-sprint lock)
        if (joystick != null && joystick.IsActive)
        {
            if (joystick.IsSprintLocked)
            {
                input = new Vector2(0f, 1f);
                isSprinting = true;
            }
            else
            {
                input = joystick.Direction;
                isSprinting = joystick.IsSprinting;
            }
        }
        else
        {
            // 2. Keyboard Fallback (Unity Editor)
            if (Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                // Support both QWERTY (WASD) and AZERTY (ZQSD) + Arrow Keys
                if (Keyboard.current.wKey.isPressed || Keyboard.current.zKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                input = new Vector2(x, y);

                isSprinting = Keyboard.current.leftShiftKey.isPressed;

                // Space to Jump
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    Jump();
                }

                // F to smash held rocks together
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    SmashBothRocks();
                }

                // G to drop / put down held rock
                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    DropLastHeldRock();
                }
            }

            // 3. Gamepad Fallback
            if (Gamepad.current != null && input.sqrMagnitude < 0.01f)
            {
                input = Gamepad.current.leftStick.ReadValue();
                isSprinting = Gamepad.current.rightTrigger.isPressed;

                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    Jump();
                }
                if (Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    DropLastHeldRock();
                }
            }
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        return input;
    }

    private void HandleMovement(Vector2 input)
    {
        Vector3 targetDirection = Vector3.zero;

        if (input.sqrMagnitude > 0.001f)
        {
            Camera cam = Camera.main;
            float camYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            float currentStickAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;

            bool isAutoAligning = touchController != null && touchController.IsAutoAlignActive;

            if (isAutoAligning)
            {
                // During subtle camera auto-alignment behind player, hold established world heading
                // so the camera automatically swinging doesn't spin the character in circles
                float stickDelta = Mathf.DeltaAngle(initialStickAngle, currentStickAngle);
                float finalWorldYaw = currentWorldMoveYaw + stickDelta;
                targetDirection = Quaternion.Euler(0f, finalWorldYaw, 0f) * Vector3.forward;
            }
            else
            {
                // Active steering & manual camera rotation:
                // Move directly relative to camera horizontal view smoothly without deadzone snaps
                currentWorldMoveYaw = camYaw + currentStickAngle;
                initialStickAngle = currentStickAngle;
                targetDirection = Quaternion.Euler(0f, currentWorldMoveYaw, 0f) * Vector3.forward;
            }

            if (targetDirection.sqrMagnitude > 1f)
            {
                targetDirection.Normalize();
            }
        }
        else
        {
            isInputActive = false;
        }

        float targetSpeed;
        float accelRate;

        if (isSwimming)
        {
            float diveFactor = isDiving ? 0.88f : 1.0f;
            targetSpeed = (isSprinting ? swimSprintSpeed : swimSpeed) * input.magnitude * diveFactor;
            accelRate = input.sqrMagnitude > 0.01f ? swimAcceleration : swimDeceleration;
        }
        else if (isInWater && currentSubmersionDepth > 0.05f)
        {
            // Wading in shallow water / sea: heavy water drag significantly slows movement
            float depthRatio = Mathf.Clamp01(currentSubmersionDepth / neckHeight);
            float wadingFactor = Mathf.Lerp(wadingMaxSpeedMultiplier, wadingMinSpeedMultiplier, depthRatio);
            targetSpeed = (isSprinting ? sprintSpeed : moveSpeed) * input.magnitude * wadingFactor;
            accelRate = input.sqrMagnitude > 0.01f ? wadingAcceleration : wadingDeceleration;
        }
        else
        {
            // Free Fire style: walk on light stick push, sprint on outer stick push or locked sprint
            float speedScale = isSprinting ? sprintSpeed : Mathf.Lerp(moveSpeed * 0.45f, moveSpeed, Mathf.Clamp01((input.magnitude - 0.1f) / 0.55f));
            targetSpeed = speedScale * input.magnitude;
            accelRate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        }

        float rotSpeed = isSwimming ? swimRotationSpeed : rotationSpeed;

        if (input.sqrMagnitude > 0.001f)
        {
            float currentSpeed = currentVelocity.magnitude;
            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            if (currentSpeed > 0.1f && targetDirection.sqrMagnitude > 0.001f)
            {
                // Rotate velocity vector towards targetDirection so momentum stays locked to character facing direction
                // Prevents Cartesian linear cutting that causes sideways sliding / ice-skating when turning
                Vector3 currentMoveDir = currentVelocity / currentSpeed;
                float turnRateRad = Mathf.Max(rotSpeed * Mathf.Deg2Rad, 20f);
                Vector3 newMoveDir = Vector3.RotateTowards(currentMoveDir, targetDirection, turnRateRad * Time.deltaTime, 0f);
                currentVelocity = newMoveDir * newSpeed;
            }
            else
            {
                currentVelocity = targetDirection * newSpeed;
            }
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        Vector3 slideMotion = Vector3.zero;

        if (isSwimming)
        {
            // Swimming Physics: Buoyancy & Surface / Dive Float
            isOnSteepSlope = false;
            float targetDepth = isDiving ? diveFloatDepth : swimFloatDepth;
            float targetFloatY = currentWaterSurfaceY - targetDepth;
            float yDiff = targetFloatY - transform.position.y;

            if (verticalVelocity < 0f && !isDiving)
            {
                // Fluid drag rapidly dampens downward plunge into water
                verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, waterVerticalDrag * Time.deltaTime);
            }

            // Smooth buoyancy spring towards target float level
            float springStrength = isDiving ? 5.5f : buoyancyStrength;
            verticalVelocity = Mathf.Lerp(verticalVelocity, yDiff * springStrength, 8f * Time.deltaTime);

            // Reset swim jump once settled and floating steadily at surface
            if (hasSwimJumped && Time.time - lastJumpTime >= swimJumpCooldown && Mathf.Abs(verticalVelocity) < 0.5f)
            {
                hasSwimJumped = false;
            }

            // Gentle river current push, if present
            if (currentWaterBody != null && currentWaterBody.currentFlow.sqrMagnitude > 0.01f)
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, currentVelocity + currentWaterBody.currentFlow, 5f * Time.deltaTime);
            }
        }
        else
        {
            // Slope & Ground Check (excludes Water layer so it senses terrain beneath)
            CheckGroundSlope();

            bool isWalkableGrounded = characterController.isGrounded && !isOnSteepSlope;

            // Gravity & Grounding
            if (isWalkableGrounded)
            {
                hasSwimJumped = false;
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -groundedStickForce;
                }
            }
            else
            {
                // In air or on steep slope: apply gravity downwards
                verticalVelocity -= gravity * Time.deltaTime;
            }

            // Calculate slope sliding motion (slides down mountains / prevents climbing walls)
            if (isOnSteepSlope)
            {
                Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundHitNormal).normalized;
                Vector3 outward = new Vector3(groundHitNormal.x, 0f, groundHitNormal.z).normalized;

                slideMotion = (slopeDown * slopeSlideSpeed) + (outward * slopeRepelForce);

                // Cancel player velocity pushing forward into the steep slope
                float dot = Vector3.Dot(currentVelocity, -groundHitNormal);
                if (dot > 0f)
                {
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, groundHitNormal);
                }
            }
        }

        // Apply movement
        Vector3 finalMotion = currentVelocity + slideMotion;
        finalMotion.y = verticalVelocity + slideMotion.y;
        characterController.Move(finalMotion * Time.deltaTime);

        // Free Fire style natural smooth rotation (eliminates sudden twitches and snapping)
        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection, Vector3.up);
            if (isDiving)
            {
                targetRot *= Quaternion.Euler(22f, 0f, 0f); // Pitch down smoothly while diving underwater
            }
            float slerpRate = isSwimming ? (swimRotationSpeed * 0.03f) : turnSmoothing;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, slerpRate * Time.deltaTime);
        }
        else if (isDiving)
        {
            Quaternion diveTreadRot = Quaternion.Euler(14f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, diveTreadRot, 6f * Time.deltaTime);
        }
    }

    private void CheckGroundSlope()
    {
        // Ground mask ignores Water layer so raycast detects the terrain bed beneath
        int waterLayer = LayerMask.NameToLayer("Water");
        int groundMask = waterLayer != -1 ? ~(1 << waterLayer) : ~0;

        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        float radius = characterController != null ? characterController.radius * 0.85f : 0.4f;

        bool hitGround = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out hit,
            0.8f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (!hitGround)
        {
            hitGround = Physics.Raycast(
                transform.position + Vector3.up * 0.4f,
                Vector3.down,
                out hit,
                1.0f,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }

        if (hitGround)
        {
            // Do not treat held rocks as terrain
            if (hit.collider != null)
            {
                RockItem rHit = hit.collider.GetComponentInParent<RockItem>();
                if (rHit != null && rHit.isHeld)
                {
                    hitGround = false;
                }
            }
        }

        if (hitGround)
        {
            groundHitNormal = hit.normal;
        }
        else if (hasControllerSlopeHit)
        {
            groundHitNormal = controllerSlopeNormal;
        }
        else
        {
            groundHitNormal = Vector3.up;
        }

        currentSlopeAngle = Vector3.Angle(groundHitNormal, Vector3.up);

        if (hasControllerSlopeHit)
        {
            float hitAngle = Vector3.Angle(controllerSlopeNormal, Vector3.up);
            if (hitAngle > currentSlopeAngle)
            {
                currentSlopeAngle = hitAngle;
                groundHitNormal = controllerSlopeNormal;
            }
        }

        bool isOverAltitude = transform.position.y > maxBorderAltitude;
        if (IsInCaveArea())
        {
            isOnSteepSlope = false;
            hasControllerSlopeHit = false;
            return;
        }

        isOnSteepSlope = (currentSlopeAngle > maxSlopeAngle) || isOverAltitude;
        hasControllerSlopeHit = false;
    }

    public bool IsInCaveArea()
    {
        Vector3 pos = transform.position;
        return pos.z <= -139f && pos.z >= -225f && pos.x >= 90f && pos.x <= 142f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;

        // Ignore water colliders
        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer != -1 && hit.collider.gameObject.layer == waterLayer) return;
        if (hit.collider.GetComponent<WaterBody>() != null) return;
        if (isSwimming) return;

        // In cave or cave entrance, disable artificial mountain slope sliding so player can walk freely near walls and archway
        if (IsInCaveArea()) return;
        string colName = hit.collider.gameObject.name;
        if (colName.Contains("Cave") || colName.Contains("Portal") || colName.Contains("Pillar") || colName.Contains("Lintel") || colName.Contains("Jamb"))
        {
            return;
        }

        // CRITICAL: Ignore any held rocks or rock items being carried!
        // Never treat carried rocks as steep terrain obstacles or slope barriers
        RockItem rockHit = hit.collider.GetComponentInParent<RockItem>();
        if (rockHit != null && rockHit.isHeld) return;

        for (int i = 0; i < heldRocks.Count; i++)
        {
            if (heldRocks[i] != null && hit.collider.transform.IsChildOf(heldRocks[i].transform))
            {
                return;
            }
        }

        // Detect steep ground contact under feet that exceeds walkable slope angle
        // Must be contact near foot/step level, and must have an upward-facing normal (ground incline, NOT vertical wall/ceiling)
        if (characterController == null) characterController = GetComponent<CharacterController>();
        float stepOffset = characterController != null ? characterController.stepOffset : 0.3f;
        float maxStepContactY = transform.position.y + stepOffset + 0.15f;
        if (hit.point.y < maxStepContactY && hit.normal.y > 0.15f)
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > maxSlopeAngle && angle < 85f)
            {
                controllerSlopeNormal = hit.normal;
                hasControllerSlopeHit = true;
            }
        }
    }

    public void Jump()
    {
        if (isControlsLocked || (!isSwimming && isOnSteepSlope)) return;
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController == null) return;

        if (isSwimming)
        {
            if (isDiving)
            {
                // When diving underwater, pressing jump surfaces the player back to the water top to take a breath!
                SetDiving(false);
                return;
            }

            if (!allowSwimBreachJump) return;

            // Strict water jump validation:
            // 1. Cannot jump if already jumped (prevents infinite / repeated water jumps)
            // 2. Cooldown must have elapsed
            // 3. Must be resting at/near water surface (cannot jump mid-air or while falling)
            // 4. Cannot be already traveling upwards
            float targetFloatY = currentWaterSurfaceY - swimFloatDepth;
            bool isAtSurface = Mathf.Abs(transform.position.y - targetFloatY) <= 0.45f;
            bool canBreachJump = !hasSwimJumped &&
                                 (Time.time - lastJumpTime >= swimJumpCooldown) &&
                                 (verticalVelocity >= -2.0f && verticalVelocity <= 0.8f) &&
                                 isAtSurface;

            if (canBreachJump)
            {
                hasSwimJumped = true;
                verticalVelocity = swimJumpForce;
                lastJumpTime = Time.time;

                if (animator != null)
                {
                    animator.SetBool("IsGrounded", false);
                    animator.SetTrigger("JumpForward");
                }

                if (suimonoWaterVFX != null && suimonoWaterVFX.SwimSplash != null)
                {
                    suimonoWaterVFX.SwimSplash.Emit(15);
                }
            }
            return;
        }

        bool canJump = (characterController.isGrounded && !isOnSteepSlope) || (Time.time - lastGroundedTime < 0.2f);
        if (canJump)
        {
            hasSwimJumped = false;
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
            lastGroundedTime = -10f;
            lastJumpTime = Time.time;

            if (animator != null)
            {
                animator.SetBool("IsGrounded", false);
                animator.SetTrigger("JumpForward");
            }
        }
    }

    public void PickUpNearestRock()
    {
        if (isControlsLocked || heldRocks.Count >= maxHeldRocks) return;

        RockItem nearest = GetNearestUnheldRock();
        if (nearest != null)
        {
            heldRocks.Add(nearest);
            int newIdx = heldRocks.Count - 1;
            nearest.SetHeld(this, newIdx);

            // Immediately ignore CharacterController collision
            if (characterController != null)
            {
                Collider[] cols = nearest.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null)
                    {
                        Physics.IgnoreCollision(characterController, cols[i], true);
                    }
                }
            }

            // Immediately place into hand slot to prevent sweeping through player body
            Vector3 targetSlot = transform.position + GetHeldSlotOffset(newIdx, heldRocks.Count);
            nearest.transform.position = targetSlot;
            nearest.transform.rotation = transform.rotation;

            // Re-offset first rock if picking up second rock so both fan out nicely
            if (heldRocks.Count == 2 && heldRocks[0] != null)
            {
                Vector3 slot0 = transform.position + GetHeldSlotOffset(0, 2);
                heldRocks[0].transform.position = slot0;
            }

            if (heldRocks.Count == 1)
            {
                Debug.Log($"<color=#64D2FF><b>[Player]</b></color> Picked up rock '{nearest.name}'! Held rocks: {heldRocks.Count}/{maxHeldRocks}");
            }
            else if (heldRocks.Count == 2)
            {
                Debug.Log("<color=#4CD964><b>[Player]</b></color> Picked up both rocks! Transitioning to First-Person Smash Mode...");
                if (RockDragManager.Instance != null)
                {
                    RockDragManager.Instance.StartFirstPersonSmash(heldRocks[0], heldRocks[1]);
                }
            }
        }
    }

    public bool PickUpSpecificRock(RockItem rock)
    {
        if (rock == null || heldRocks.Count >= maxHeldRocks || heldRocks.Contains(rock)) return false;

        heldRocks.Add(rock);
        int newIdx = heldRocks.Count - 1;
        rock.SetHeld(this, newIdx);

        if (characterController != null)
        {
            Collider[] cols = rock.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    Physics.IgnoreCollision(characterController, cols[i], true);
                }
            }
        }

        Vector3 targetSlot = transform.position + GetHeldSlotOffset(newIdx, heldRocks.Count);
        rock.transform.position = targetSlot;
        rock.transform.rotation = transform.rotation;

        if (heldRocks.Count == 2 && heldRocks[0] != null)
        {
            Vector3 slot0 = transform.position + GetHeldSlotOffset(0, 2);
            heldRocks[0].transform.position = slot0;
        }

        if (heldRocks.Count == 1)
        {
            Debug.Log($"<color=#64D2FF><b>[Player]</b></color> Picked up rock '{rock.name}' via inspection! Held rocks: {heldRocks.Count}/{maxHeldRocks}");
        }
        else if (heldRocks.Count == 2)
        {
            Debug.Log($"<color=#4CD964><b>[Player]</b></color> Picked up rock '{rock.name}' via inspection! Held rocks: {heldRocks.Count}/{maxHeldRocks}");
        }

        UpdatePutDownButtonVisuals();
        return true;
    }

    public RockItem GetNearestUnheldRock()
    {
        RockItem[] allRocks = Object.FindObjectsByType<RockItem>();
        RockItem best = null;
        float bestDist = pickupRadius;

        foreach (var rock in allRocks)
        {
            if (rock == null || rock.isHeld) continue;

            float dist = Vector3.Distance(transform.position, rock.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = rock;
            }
        }

        return best;
    }

    public bool CanPickUp()
    {
        return !isControlsLocked && heldRocks.Count < maxHeldRocks && GetNearestUnheldRock() != null;
    }

    public Vector3 GetHeldSlotOffset(int slotIndex, int totalCount)
    {
        if (totalCount <= 1)
        {
            // Single rock held naturally in front, slightly to the right side
            return transform.forward * 0.65f + transform.right * 0.22f + Vector3.up * 0.95f;
        }
        else
        {
            // Two rocks: left hand and right hand slots
            return slotIndex == 0
                ? -transform.right * 0.38f + transform.forward * 0.62f + Vector3.up * 0.95f
                : transform.right * 0.38f + transform.forward * 0.62f + Vector3.up * 0.95f;
        }
    }

    private void UpdateHeldRocksPosition()
    {
        if (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode)
        {
            return;
        }
        // Clean null references if any rock was destroyed
        heldRocks.RemoveAll(r => r == null);

        int count = heldRocks.Count;
        for (int i = 0; i < count; i++)
        {
            RockItem rock = heldRocks[i];
            if (rock == null) continue;

            // Only follow slot if NOT actively being dragged by user's finger!
            if (!rock.isBeingDragged)
            {
                Vector3 targetSlotPos = transform.position + GetHeldSlotOffset(i, count);
                // Crisp follow speed (28f * dt) ensures the rock never lags into the player's capsule
                rock.transform.position = Vector3.Lerp(rock.transform.position, targetSlotPos, 28f * Time.deltaTime);
                // Align rotation with player orientation
                rock.transform.rotation = Quaternion.Slerp(rock.transform.rotation, transform.rotation, 25f * Time.deltaTime);
            }
        }
    }

    public void DropLastHeldRock()
    {
        if (isControlsLocked || heldRocks.Count == 0) return;
        heldRocks.RemoveAll(r => r == null);
        if (heldRocks.Count == 0)
        {
            UpdatePutDownButtonVisuals();
            return;
        }

        int lastIdx = heldRocks.Count - 1;
        RockItem rock = heldRocks[lastIdx];
        heldRocks.RemoveAt(lastIdx);

        if (rock != null)
        {
            rock.ReleaseHeld();
            // Drop gently onto the ground in front of the player
            Vector3 dropPos = transform.position + transform.forward * 1.0f + Vector3.up * 0.35f;
            rock.transform.position = dropPos;
            rock.transform.rotation = transform.rotation;

            Rigidbody rb = rock.GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(transform.forward * 1.2f + Vector3.up * 0.8f, ForceMode.Impulse);
            }
            Debug.Log($"<color=#FFCC00><b>[Player]</b></color> Put down {rock.name}. Remaining held: {heldRocks.Count}");
        }

        UpdatePutDownButtonVisuals();
    }

    public bool ReleaseHeldRock(RockItem rock)
    {
        if (rock == null || !heldRocks.Contains(rock)) return false;
        heldRocks.Remove(rock);
        rock.ReleaseHeld();
        UpdatePutDownButtonVisuals();
        return true;
    }

    public void SmashBothRocks()
    {
        heldRocks.RemoveAll(r => r == null);
        if (heldRocks.Count < 2) return;

        RockItem r1 = heldRocks[0];
        RockItem r2 = heldRocks[1];

        // Move them rapidly towards their midpoint to collide hard
        Vector3 mid = (r1.transform.position + r2.transform.position) * 0.5f;
        r1.transform.position = mid - (r2.transform.position - r1.transform.position).normalized * 0.2f;
        r2.transform.position = mid + (r2.transform.position - r1.transform.position).normalized * 0.2f;

        // Trigger the smash check with high speed (6.0 m/s)
        r1.CheckCollisionWithOtherRock(r2.gameObject, 6.0f);
    }

    public void OnRockDestroyed(RockItem rock)
    {
        heldRocks.Remove(rock);
    }

    public void SetJoystick(MobileJoystick newJoystick)
    {
        joystick = newJoystick;
    }

    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
    }

    public void ClearHeldRocks()
    {
        for (int i = 0; i < heldRocks.Count; i++)
        {
            if (heldRocks[i] != null)
            {
                heldRocks[i].ReleaseHeld();
            }
        }
        heldRocks.Clear();
    }

    public void RestoreHeldRock(RockItem rock)
    {
        if (rock == null || heldRocks.Count >= maxHeldRocks) return;
        if (!heldRocks.Contains(rock))
        {
            heldRocks.Add(rock);
            rock.SetHeld(this, heldRocks.Count - 1);
        }
    }

    public void TeleportTo(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            characterController.enabled = true;
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
