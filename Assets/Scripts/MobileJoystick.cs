using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Elements")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Settings")]
    [SerializeField] private float handleRange = 75f;
    [SerializeField] private float deadZone = 0.1f;
    [SerializeField] private bool dynamicPosition = false;

    private Canvas canvas;
    private Camera canvasCamera;
    private Vector2 inputVector = Vector2.zero;
    private Vector2 defaultBackgroundPosition;
    private bool isPointerDown = false;

    public Vector2 Direction => inputVector;
    public float Horizontal => inputVector.x;
    public float Vertical => inputVector.y;
    public bool IsActive => isPointerDown && inputVector.sqrMagnitude > (deadZone * deadZone);

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
    }

    public void OnPointerDown(PointerEventData eventData)
    {
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
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        inputVector = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        if (dynamicPosition && background != null)
        {
            background.anchoredPosition = defaultBackgroundPosition;
        }
    }
}
