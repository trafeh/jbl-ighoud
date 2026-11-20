using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Free Fire style Mobile Joystick:
/// 1. Walk vs Sprint sensitivity (light tilt = walk, full push = sprint).
/// 2. Run Lock: the RUN icon is hidden. Swipe the stick up and it slides in above the stick;
///    keep sliding until your thumb REACHES the icon and sprint locks instantly (turns gold).
///    Let go and you keep sprinting forward hands-free. Touch the stick again to cancel.
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
    [SerializeField] private bool enableSprintLock = true;

    [Tooltip("Tilt magnitude threshold to switch from walking to sprinting (0.65 = 65% tilt).")]
    [Range(0.4f, 0.9f)]
    [SerializeField] private float sprintThreshold = 0.65f;

    [Tooltip("How far up (x handleRange) the thumb must drag before the RUN icon appears.")]
    [Range(0.3f, 1.5f)]
    [SerializeField] private float revealDragRatio = 0.75f;

    [Tooltip("Hit radius multiplier on the RUN icon. >1 = easier to reach with a thumb.")]
    [Range(0.8f, 2f)]
    [SerializeField] private float lockHitScale = 1.15f;

    [Header("Sprint Lock UI")]
    [SerializeField] private RectTransform sprintLockContainer;
    [SerializeField] private Image sprintLockIcon;
    [SerializeField] private Image sprintLockRing;   // optional, Filled/Radial360 - fills as the thumb approaches
    [SerializeField] private Sprite runIdleSprite;
    [SerializeField] private Sprite runLockedSprite;
    [SerializeField] private Text sprintLockText;    // legacy, hidden

    [Header("Sprint Lock Animation")]
    [SerializeField] private float slideFromY = -45f;
    [SerializeField] private float animSpeed = 9f;

    private Canvas canvas;
    private Camera canvasCamera;
    private Vector2 inputVector = Vector2.zero;
    private Vector2 defaultBackgroundPosition;
    private bool isPointerDown = false;
    private bool isSprintLocked = false;

    // lock widget state
    private CanvasGroup lockGroup;
    private Vector2 lockRestPos;
    private bool lockRevealed;
    private float lockVisibleT;
    private float lockApproach;
    private float lockPunch;

    public Vector2 Direction => inputVector;
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    public bool IsActive => isSprintLocked || (isPointerDown && inputVector.sqrMagnitude > (deadZone * deadZone));
    public bool IsSprintLocked => isSprintLocked;
    public bool IsSprinting => isSprintLocked || (isPointerDown && inputVector.magnitude >= sprintThreshold);
    public float SprintThreshold => sprintThreshold;

    private void Awake()
    {
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null && transform.childCount > 0) handle = transform.GetChild(0) as RectTransform;

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCamera = canvas.worldCamera;

        if (background != null) defaultBackgroundPosition = background.anchoredPosition;

        SetupSprintLockUI();
    }

    private void SetupSprintLockUI()
    {
        if (sprintLockContainer == null && background != null)
            sprintLockContainer = background.Find("SprintLockTarget") as RectTransform;
        if (sprintLockContainer == null) return;

        if (sprintLockIcon == null) sprintLockIcon = sprintLockContainer.GetComponent<Image>();
        if (sprintLockText != null) sprintLockText.gameObject.SetActive(false);

        lockGroup = sprintLockContainer.GetComponent<CanvasGroup>();
        if (lockGroup == null) lockGroup = sprintLockContainer.gameObject.AddComponent<CanvasGroup>();
        lockGroup.blocksRaycasts = false;
        lockGroup.interactable = false;
        lockGroup.alpha = 0f;

        lockRestPos = sprintLockContainer.anchoredPosition;
        if (sprintLockIcon != null && runIdleSprite != null) sprintLockIcon.sprite = runIdleSprite;
        if (sprintLockRing != null) sprintLockRing.fillAmount = 0f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Time.timeScale <= 0f) return;

        // Touching the stick again cancels the run lock (Free Fire behaviour)
        if (isSprintLocked) UnlockSprint();

        isPointerDown = true;
        if (dynamicPosition && background != null && canvas != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, canvasCamera, out Vector2 localPoint);
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

        if (isSprintLocked)
        {
            // Already locked on this swipe: keep the knob pinned up, ignore the finger until release.
            inputVector = Vector2.up;
            handle.anchoredPosition = new Vector2(0f, handleRange);
            return;
        }

        Vector2 clampedDelta = Vector2.ClampMagnitude(currentDelta, handleRange);
        handle.anchoredPosition = clampedDelta;

        Vector2 normalized = clampedDelta / handleRange;
        inputVector = normalized.magnitude < deadZone ? Vector2.zero : normalized;

        if (!enableSprintLock || sprintLockContainer == null) return;

        // Reveal when swiping up inside a ~70 degree cone
        bool inCone = currentDelta.y > 0f && Mathf.Abs(currentDelta.x) < currentDelta.y * 0.75f;
        float revealY = handleRange * revealDragRatio;
        lockRevealed = inCone && currentDelta.y > revealY;

        // Lock the moment the thumb reaches the icon
        float hitR = sprintLockContainer.rect.width * 0.5f * lockHitScale;
        float dist = Vector2.Distance(currentDelta, lockRestPos);
        float startDist = Mathf.Max(hitR + 1f, lockRestPos.y - revealY);
        lockApproach = lockRevealed ? Mathf.Clamp01(1f - (dist - hitR) / (startDist - hitR)) : 0f;

        if (lockRevealed && dist <= hitR) LockSprint();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        lockRevealed = false;
        lockApproach = 0f;

        if (isSprintLocked) return; // stays sprinting hands-free

        inputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (dynamicPosition && background != null) background.anchoredPosition = defaultBackgroundPosition;
    }

    public void LockSprint()
    {
        if (isSprintLocked) return;
        isSprintLocked = true;
        inputVector = Vector2.up;
        if (handle != null) handle.anchoredPosition = new Vector2(0f, handleRange);
        if (sprintLockIcon != null && runLockedSprite != null) sprintLockIcon.sprite = runLockedSprite;
        lockPunch = 1f;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    public void UnlockSprint()
    {
        if (!isSprintLocked) return;
        isSprintLocked = false;
        if (sprintLockIcon != null && runIdleSprite != null) sprintLockIcon.sprite = runIdleSprite;
        if (!isPointerDown)
        {
            inputVector = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }

    private void Update()
    {
        if (sprintLockContainer == null || lockGroup == null) return;
        float dt = Time.unscaledDeltaTime;

        bool visible = isSprintLocked || (isPointerDown && lockRevealed);
        lockVisibleT = Mathf.MoveTowards(lockVisibleT, visible ? 1f : 0f, dt * animSpeed);
        float e = 1f - Mathf.Pow(1f - lockVisibleT, 3f); // ease-out

        lockPunch = Mathf.MoveTowards(lockPunch, 0f, dt * 3.5f);
        float bob = (!isSprintLocked && visible) ? Mathf.Sin(Time.unscaledTime * 9f) * 3f : 0f;
        float pulse = isSprintLocked ? 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.035f : 1f;
        float approachGrow = isSprintLocked ? 0f : lockApproach * 0.12f;

        lockGroup.alpha = e;
        sprintLockContainer.anchoredPosition = lockRestPos + new Vector2(0f, Mathf.Lerp(slideFromY, 0f, e) + bob);
        sprintLockContainer.localScale = Vector3.one * (Mathf.Lerp(0.55f, 1f, e) * pulse + approachGrow + lockPunch * 0.22f);

        if (sprintLockRing != null)
        {
            float target = isSprintLocked ? 1f : lockApproach;
            sprintLockRing.fillAmount = Mathf.Lerp(sprintLockRing.fillAmount, target, dt * 18f);
        }
    }

    private void OnDisable()
    {
        isPointerDown = false;
        lockRevealed = false;
    }
}
