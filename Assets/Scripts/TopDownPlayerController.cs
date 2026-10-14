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
    [SerializeField] private float maxBorderAltitude = 16f;

    [Tooltip("Speed at which the character slides down steep mountain slopes.")]
    [SerializeField] private float slopeSlideSpeed = 14f;

    [Tooltip("Horizontal push force away from steep mountain faces.")]
    [SerializeField] private float slopeRepelForce = 6f;

    [Header("Pickup Settings")]
    [Tooltip("Distance within which the player can pick up items.")]
    [SerializeField] private float pickupRadius = 3.8f;

    [Header("References")]
    [SerializeField] private MobileJoystick joystick;
    [SerializeField] private UnityEngine.UI.Button jumpButton;
    [SerializeField] private UnityEngine.UI.Button pickButton;
    [SerializeField] private UnityEngine.UI.Button smashButton;
    [SerializeField] private Animator animator;
    [SerializeField] private MobileFreeLookTouchController touchController;

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

    public float CurrentSpeed => currentVelocity.magnitude;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.05f;
    public bool IsGrounded => characterController.isGrounded && !isOnSteepSlope;
    public bool IsOnSteepSlope => isOnSteepSlope;
    public int HeldRockCount => heldRocks.Count;
    public IReadOnlyList<RockItem> HeldRocks => heldRocks;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

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

        if (pickButton == null)
        {
            GameObject pGo = GameObject.Find("PickButton");
            if (pGo != null) pickButton = pGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (pickButton != null)
        {
            pickButton.onClick.RemoveListener(PickUpNearestRock);
            pickButton.onClick.AddListener(PickUpNearestRock);
        }

        if (smashButton == null)
        {
            GameObject sGo = GameObject.Find("SmashButton");
            if (sGo != null) smashButton = sGo.GetComponent<UnityEngine.UI.Button>();
        }
        if (smashButton != null)
        {
            smashButton.onClick.RemoveListener(SmashBothRocks);
            smashButton.onClick.AddListener(SmashBothRocks);
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        UpdatePickButtonVisuals();
        UpdateSmashButtonVisuals();
        Vector2 input = GetInputVector();
        HandleMovement(input);
        UpdateHeldRocksPosition();
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsMoving", IsMoving);
            animator.SetFloat("Speed", CurrentSpeed);

            bool isGroundedForAnim = characterController.isGrounded && (Time.time - lastJumpTime > 0.2f);
            animator.SetBool("IsGrounded", isGroundedForAnim);
        }
    }

    private void UpdatePickButtonVisuals()
    {
        if (pickButton != null)
        {
            bool canPick = CanPickUp();
            pickButton.interactable = canPick;
            var img = pickButton.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.color = canPick ? new Color(0.2f, 0.95f, 0.4f, 0.95f) : new Color(0.2f, 0.6f, 0.3f, 0.35f);
            }
        }
    }

    private void UpdateSmashButtonVisuals()
    {
        if (smashButton != null)
        {
            bool canSmash = heldRocks.Count >= 2;
            smashButton.interactable = canSmash;
            var img = smashButton.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.color = canSmash ? new Color(1f, 0.35f, 0.2f, 0.95f) : new Color(0.5f, 0.25f, 0.2f, 0.35f);
            }
        }
    }

    private Vector2 GetInputVector()
    {
        Vector2 input = Vector2.zero;

        // 1. Mobile Joystick input
        if (joystick != null && joystick.Direction.sqrMagnitude > 0.01f)
        {
            input = joystick.Direction;
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

                // E to Pick up nearest rock
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    PickUpNearestRock();
                }

                // F to smash held rocks together
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    SmashBothRocks();
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
                if (Gamepad.current.buttonWest.wasPressedThisFrame)
                {
                    PickUpNearestRock();
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

            bool isManualLooking = touchController != null && touchController.IsManualLooking;
            bool isAutoAligning = touchController != null && touchController.IsAutoAlignActive;

            if (!isInputActive)
            {
                // New movement started: anchor to current camera and stick angle
                isInputActive = true;
                initialStickAngle = currentStickAngle;
                currentWorldMoveYaw = camYaw + currentStickAngle;
            }
            else if (isManualLooking)
            {
                // Player is manually orbiting camera: re-anchor so manual camera look steers player
                initialStickAngle = currentStickAngle;
                currentWorldMoveYaw = camYaw + currentStickAngle;
            }
            else if (isAutoAligning)
            {
                // Camera is auto-aligning behind the player:
                // CRUCIAL: Keep established world movement heading so the rotating camera
                // does NOT cause an endless circular loop / death spiral!
            }
            else
            {
                // Normal movement (not auto-aligning, not manual looking):
                // If player actively steers stick by changing angle, track the steering
                float angleDelta = Mathf.DeltaAngle(initialStickAngle, currentStickAngle);
                if (Mathf.Abs(angleDelta) > 10f)
                {
                    // Re-anchor to current camera when steering deliberately
                    initialStickAngle = currentStickAngle;
                    currentWorldMoveYaw = camYaw + currentStickAngle;
                }
            }

            float stickDelta = Mathf.DeltaAngle(initialStickAngle, currentStickAngle);
            float finalWorldYaw = currentWorldMoveYaw + stickDelta;

            targetDirection = Quaternion.Euler(0f, finalWorldYaw, 0f) * Vector3.forward;

            if (targetDirection.sqrMagnitude > 1f)
            {
                targetDirection.Normalize();
            }
        }
        else
        {
            isInputActive = false;
        }

        float targetSpeed = (isSprinting ? sprintSpeed : moveSpeed) * input.magnitude;
        Vector3 targetVelocity = targetDirection * targetSpeed;

        float accelRate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accelRate * Time.deltaTime);

        // Slope & Ground Check
        CheckGroundSlope();

        bool isWalkableGrounded = characterController.isGrounded && !isOnSteepSlope;

        // Gravity & Grounding
        if (isWalkableGrounded)
        {
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
        Vector3 slideMotion = Vector3.zero;
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

        // Apply movement
        Vector3 finalMotion = currentVelocity + slideMotion;
        finalMotion.y = verticalVelocity + slideMotion.y;
        characterController.Move(finalMotion * Time.deltaTime);

        // Smooth rotation
        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void CheckGroundSlope()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        float radius = characterController != null ? characterController.radius * 0.85f : 0.4f;

        bool hitGround = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out hit,
            0.8f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (!hitGround)
        {
            hitGround = Physics.Raycast(
                transform.position + Vector3.up * 0.4f,
                Vector3.down,
                out hit,
                1.0f,
                ~0,
                QueryTriggerInteraction.Ignore
            );
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
        isOnSteepSlope = (currentSlopeAngle > maxSlopeAngle) || isOverAltitude;
        hasControllerSlopeHit = false;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Detect steep contact under feet or along lower body
        if (hit.point.y < transform.position.y + characterController.center.y + 0.3f)
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > maxSlopeAngle)
            {
                controllerSlopeNormal = hit.normal;
                hasControllerSlopeHit = true;
            }
        }
    }

    public void Jump()
    {
        // Strictly prevent jumping when on or touching steep mountain borders
        if (isOnSteepSlope) return;

        bool canJump = (characterController.isGrounded && !isOnSteepSlope) || (Time.time - lastGroundedTime < 0.2f);
        if (canJump)
        {
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
        if (heldRocks.Count >= 2) return;

        RockItem nearest = GetNearestUnheldRock();
        if (nearest != null)
        {
            heldRocks.Add(nearest);
            nearest.SetHeld(this, heldRocks.Count - 1);
        }
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
        return heldRocks.Count < 2 && GetNearestUnheldRock() != null;
    }

    private void UpdateHeldRocksPosition()
    {
        // Clean null references if any rock was destroyed
        heldRocks.RemoveAll(r => r == null);

        for (int i = 0; i < heldRocks.Count; i++)
        {
            RockItem rock = heldRocks[i];
            if (rock == null) continue;

            // Only follow slot if NOT actively being dragged by user's finger!
            if (!rock.isBeingDragged)
            {
                Vector3 slotOffset = i == 0
                    ? -transform.right * 0.9f + transform.forward * 0.7f + Vector3.up * 1.0f
                    : transform.right * 0.9f + transform.forward * 0.7f + Vector3.up * 1.0f;

                Vector3 targetSlotPos = transform.position + slotOffset;
                rock.transform.position = Vector3.Lerp(rock.transform.position, targetSlotPos, 12f * Time.deltaTime);
            }
        }
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
        if (rock == null || heldRocks.Count >= 2) return;
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
