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

        ShowStatus("💥 Drag rocks horizontally to smash them together!", 3.5f);
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

        ShowStatus("Returned to exploration.", 2.0f);
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

        // Handle inputs
        HandleTouchInput();
        HandleMouseAndKeyboardInput();

        // Check 1D collision along X axis
        Check1DCollision();

        // Update screen shake
        UpdateScreenShake();

        // Update status timer
        if (statusTimer > 0f) statusTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
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

        // Visual spark effect
        SpawnSmashVFX(contactMid);

        // Screen shake
        shakeIntensity = 0.045f;

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

        Vector3 targetScale = Vector3.one * handheldRockScale;

        if (rockLeft != null)
        {
            rockLeft.transform.position = mainCamera.transform.TransformPoint(leftLocalPos);
            // Slight tilt inwards towards each other
            rockLeft.transform.rotation = mainCamera.transform.rotation * Quaternion.Euler(10f, 25f, -15f);
            rockLeft.transform.localScale = Vector3.Lerp(rockLeft.transform.localScale, targetScale, 15f * Time.deltaTime);
        }

        if (rockRight != null)
        {
            rockRight.transform.position = mainCamera.transform.TransformPoint(rightLocalPos);
            // Slight tilt inwards towards each other
            rockRight.transform.rotation = mainCamera.transform.rotation * Quaternion.Euler(10f, -25f, 15f);
            rockRight.transform.localScale = Vector3.Lerp(rockRight.transform.localScale, targetScale, 15f * Time.deltaTime);
        }
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
            shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0f, 0.25f * Time.deltaTime);
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
    }

    private void SpawnSmashVFX(Vector3 pos)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "SmashFlashVFX";
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * 0.45f;

        Collider col = spark.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = spark.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(1f, 0.85f, 0.2f);
        }

        Destroy(spark, 0.2f);
    }

    private void ShowStatus(string msg, float duration)
    {
        statusMessage = msg;
        statusTimer = duration;
    }

    private void OnGUI()
    {
        if (!isInSmashMode) return;

        // 1. Top Instruction Banner
        float bannerW = Mathf.Min(480f, Screen.width * 0.85f);
        float bannerH = 68f;
        float bannerX = (Screen.width - bannerW) * 0.5f;
        float bannerY = 20f;

        Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);
        GUI.color = new Color(0.08f, 0.12f, 0.18f, 0.88f);
        GUI.Box(bannerRect, GUIContent.none);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
        GUI.Label(new Rect(bannerX, bannerY + 6, bannerW, 24), " FIRST-PERSON ROCK SMASH", titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            fontSize = 13,
            fontStyle = FontStyle.Normal
        };
        subStyle.normal.textColor = Color.white;
        string subText = string.IsNullOrEmpty(statusMessage) || statusTimer <= 0f
            ? "Swipe rocks horizontally with your fingers to strike them!"
            : statusMessage;
        GUI.Label(new Rect(bannerX, bannerY + 30, bannerW, 30), subText, subStyle);

        // 2. Drag Guidance Arrows
        GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        arrowStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);

        float arrowY = Screen.height * 0.55f;
        GUI.Label(new Rect(Screen.width * 0.18f, arrowY, 140, 40), "▶ ▶ DRAG", arrowStyle);
        GUI.Label(new Rect(Screen.width * 0.82f - 140, arrowY, 140, 40), "DRAG ◀ ◀", arrowStyle);

        // 3. Put Away / Exit Button in Top-Right Corner
        float exitW = 120f;
        float exitH = 40f;
        float exitX = Screen.width - exitW - 20f;
        float exitY = 20f;

        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 0.9f);
        if (GUI.Button(new Rect(exitX, exitY, exitW, exitH), "✕ Put Away"))
        {
            ExitFirstPersonSmashMode();
        }
        GUI.backgroundColor = Color.white;
    }
}
