using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Free Fire style Mobile Joystick with:
/// 1. Walk vs Sprint sensitivity (light tilt = walk, full push = sprint).
/// 2. Drag-to-top Run Lock (Auto-Sprint): dragging the stick top outside the bounds locks
///    continuous forward sprint hands-free until the stick is touched again.
/// 3. Visual Sprint Lock UI above the stick.
/// </summary>
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Elements")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Settings")]
    [SerializeField] private float handleRange = 75f;
    [SerializeField] private float deadZone = 0.1f;
    [SerializeField] private bool dynamicPosition = false;

    [Header("Free Fire Sprint & Run Lock")]
    [Tooltip("Enable dragging up outside joystick bounds to engage locked auto-sprint.")]
    [SerializeField] private bool enableSprintLock = true;

    [Tooltip("Tilt magnitude threshold to switch from walking to sprinting (0.65 = 65% tilt).")]
    [Range(0.4f, 0.9f)]
    [SerializeField] private float sprintThreshold = 0.65f;

    [Tooltip("Distance multiplier beyond handleRange in the top direction required to trigger sprint lock.")]
    [Range(1.1f, 2.0f)]
    [SerializeField] private float sprintLockDragRatio = 1.30f;

    [Header("Sprint Lock UI")]
    [Tooltip("Optional custom UI container for the sprint lock icon placed above joystick.")]
    [SerializeField] private RectTransform sprintLockContainer;
    [SerializeField] private Image sprintLockIcon;
    [SerializeField] private Text sprintLockText;

    private Canvas canvas;
    private Camera canvasCamera;
    private Vector2 inputVector = Vector2.zero;
    private Vector2 defaultBackgroundPosition;
    private bool isPointerDown = false;
    private bool isSprintLocked = false;
    private bool isHoveringLockTarget = false;

    public Vector2 Direction => inputVector;
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    public bool IsActive => isSprintLocked || (isPointerDown && inputVector.sqrMagnitude > (deadZone * deadZone));
    public bool IsSprintLocked => isSprintLocked;
    public bool IsSprinting => isSprintLocked || (isPointerDown && inputVector.magnitude >= sprintThreshold);
    public float SprintThreshold => sprintThreshold;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<RectTransform>();

        if (handle == null && transform.childCount > 0)
            handle = transform.GetChild(0) as RectTransform;

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = canvas.worldCamera;
        }

        if (background != null)
        {
            defaultBackgroundPosition = background.anchoredPosition;
        }

        EnsureSprintLockUI();
        UpdateLockVisuals(0f);
    }

    /// <summary>
    /// Auto-constructs a Free Fire style Sprint Lock widget above the joystick if not assigned.
    /// </summary>
    private void EnsureSprintLockUI()
    {
        if (sprintLockContainer != null || background == null) return;

        // Check if already created as a child of background
        Transform existing = background.Find("SprintLockTarget");
        if (existing != null)
        {
            sprintLockContainer = existing as RectTransform;
            sprintLockIcon = sprintLockContainer.GetComponent<Image>();
            sprintLockText = sprintLockContainer.GetComponentInChildren<Text>();
            return;
        }

        // Create sleek Free Fire style sprint lock indicator above the joystick
        GameObject lockGO = new GameObject("SprintLockTarget");
        lockGO.transform.SetParent(background, false);

        sprintLockContainer = lockGO.AddComponent<RectTransform>();
        sprintLockContainer.anchorMin = new Vector2(0.5f, 0.5f);
        sprintLockContainer.anchorMax = new Vector2(0.5f, 0.5f);
        sprintLockContainer.pivot = new Vector2(0.5f, 0.5f);
        sprintLockContainer.anchoredPosition = new Vector2(0f, handleRange * 1.62f);
        sprintLockContainer.sizeDelta = new Vector2(76f, 32f);

        Image lockBg = lockGO.AddComponent<Image>();
        lockBg.color = new Color(0.08f, 0.12f, 0.18f, 0.65f);
        sprintLockIcon = lockBg;

        Outline outline = lockGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.3f, 0.4f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Lock label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(lockGO.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;

        Text txt = labelGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 12;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = "RUN 🔒";
        txt.color = new Color(1f, 1f, 1f, 0.6f);
        sprintLockText = txt;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Time.timeScale <= 0f) return;

        // Touching the stick again unlocks sprint immediately!
        if (isSprintLocked)
        {
            UnlockSprint();
        }

        isPointerDown = true;
        if (dynamicPosition && background != null && canvas != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvasCamera,
                out Vector2 localPoint);
            background.anchoredPosition = localPoint;
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Time.timeScale <= 0f) return;
        if (background == null || handle == null) return;

        Vector2 position = RectTransformUtility.WorldToScreenPoint(canvasCamera, background.position);
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        if (scale <= 0.001f) scale = 1f;

        Vector2 currentDelta = (eventData.position - position) / scale;
        Vector2 clampedDelta = Vector2.ClampMagnitude(currentDelta, handleRange);
        handle.anchoredPosition = clampedDelta;

        Vector2 normalized = clampedDelta / handleRange;
        if (normalized.magnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }
        else
        {
            inputVector = normalized;
        }

        // Free Fire Drag-to-Top Run Lock Detection:
        if (enableSprintLock)
        {
            float lockTriggerDist = handleRange * sprintLockDragRatio;
            // Upward drag beyond stick border within a ~65-degree cone towards top
            bool isDraggingTowardsLock = currentDelta.y > handleRange * 0.8f && Mathf.Abs(currentDelta.x) < currentDelta.y * 0.75f;
            float proximity = Mathf.Clamp01((currentDelta.y - handleRange * 0.7f) / (lockTriggerDist - handleRange * 0.7f));

            isHoveringLockTarget = (currentDelta.y >= lockTriggerDist && Mathf.Abs(currentDelta.x) < currentDelta.y * 0.75f);

            // While actively touching/dragging, stick remains fully dynamic and never freezes/fixes hands-free
            UpdateLockVisuals(isHoveringLockTarget ? 1f : (isDraggingTowardsLock ? proximity : 0f));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;

        // The stick ONLY fixes/locks forward if the player intentionally releases while keeping it inside the lock target!
        if (enableSprintLock && isHoveringLockTarget)
        {
            isHoveringLockTarget = false;
            LockSprint();
            return;
        }

        // Standard release: NEVER stays fixed/stuck. Instantly resets to center and stops player.
        isHoveringLockTarget = false;
        UnlockSprint();

        inputVector = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        if (dynamicPosition && background != null)
        {
            background.anchoredPosition = defaultBackgroundPosition;
        }

        UpdateLockVisuals(0f);
    }

    /// <summary>
    /// Locks sprint movement so the player keeps sprinting hands-free.
    /// </summary>
    public void LockSprint()
    {
        isSprintLocked = true;
        inputVector = new Vector2(0f, 1f);
        if (handle != null)
        {
            handle.anchoredPosition = new Vector2(0f, handleRange);
        }
        UpdateLockVisuals(1f);
    }

    /// <summary>
    /// Disengages sprint lock, returning movement control to normal touch.
    /// </summary>
    public void UnlockSprint()
    {
        isSprintLocked = false;
        UpdateLockVisuals(0f);
    }

    private void UpdateLockVisuals(float proximity)
    {
        if (sprintLockContainer == null) return;

        if (isSprintLocked)
        {
            // Vibrant locked state
            if (sprintLockIcon != null)
            {
                sprintLockIcon.color = new Color(1.0f, 0.65f, 0.0f, 0.95f); // Glowing Gold/Amber
            }
            if (sprintLockText != null)
            {
                sprintLockText.text = "LOCKED 🔒";
                sprintLockText.color = Color.white;
            }
            sprintLockContainer.localScale = Vector3.one * 1.12f;
        }
        else if (proximity > 0.05f)
        {
            // Approaching lock zone
            Color highlightCol = Color.Lerp(
                new Color(0.12f, 0.16f, 0.22f, 0.6f),
                new Color(1.0f, 0.85f, 0.25f, 0.85f),
                proximity
            );
            if (sprintLockIcon != null) sprintLockIcon.color = highlightCol;
            if (sprintLockText != null)
            {
                sprintLockText.text = proximity > 0.8f ? "RELEASE TO LOCK" : "RUN 🔒";
                sprintLockText.color = Color.Lerp(new Color(1f, 1f, 1f, 0.6f), Color.white, proximity);
            }
            sprintLockContainer.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.08f, proximity);
        }
        else
        {
            // Normal subtle idle state
            if (sprintLockIcon != null)
            {
                sprintLockIcon.color = new Color(0.08f, 0.12f, 0.18f, 0.50f);
            }
            if (sprintLockText != null)
            {
                sprintLockText.text = "RUN 🔒";
                sprintLockText.color = new Color(1f, 1f, 1f, 0.50f);
            }
            sprintLockContainer.localScale = Vector3.one;
        }
    }
}
