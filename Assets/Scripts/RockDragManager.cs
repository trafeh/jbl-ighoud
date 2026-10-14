using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;

public class RockDragManager : MonoBehaviour
{
    [Header("Drag Settings")]
    [SerializeField] private float dragHeight = 1.0f;
    [SerializeField] private float dragFollowSpeed = 25f;
    [SerializeField] private float hardSmashSpeed = 2.5f;

    private Camera mainCamera;
    private Dictionary<int, RockItem> touchToRock = new Dictionary<int, RockItem>();
    private RockItem mouseLeftRock = null;
    private RockItem mouseRightRock = null;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        HandleTouchDragging();
        HandleMouseDraggingEditor();
        CheckProximitySmashes();

        // Keyboard cheat for editor: press F to smash held rocks
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            SmashHeldRocks();
        }
    }

    private void HandleTouchDragging()
    {
        var touches = Touch.activeTouches;
        foreach (var touch in touches)
        {
            int touchId = touch.touchId;

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                RockItem targetRock = FindRockAtScreenPoint(touch.screenPosition);
                if (targetRock != null && !touchToRock.ContainsValue(targetRock))
                {
                    targetRock.isBeingDragged = true;
                    targetRock.draggingPointerId = touchId;
                    touchToRock[touchId] = targetRock;
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                if (touchToRock.TryGetValue(touchId, out RockItem rock) && rock != null)
                {
                    Vector3 targetWorldPos = ScreenToGroundPoint(touch.screenPosition, dragHeight);
                    rock.transform.position = Vector3.MoveTowards(rock.transform.position, targetWorldPos, dragFollowSpeed * Time.deltaTime);
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                if (touchToRock.TryGetValue(touchId, out RockItem rock) && rock != null)
                {
                    rock.isBeingDragged = false;
                    rock.draggingPointerId = -1;
                }
                touchToRock.Remove(touchId);
            }
        }
    }

    private void HandleMouseDraggingEditor()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Left Click -> Drag Rock 1
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            RockItem rock = FindRockAtScreenPoint(mousePos);
            if (rock != null && rock != mouseRightRock)
            {
                mouseLeftRock = rock;
                mouseLeftRock.isBeingDragged = true;
            }
        }
        else if (Mouse.current.leftButton.isPressed && mouseLeftRock != null)
        {
            Vector3 targetPos = ScreenToGroundPoint(mousePos, dragHeight);
            mouseLeftRock.transform.position = Vector3.MoveTowards(mouseLeftRock.transform.position, targetPos, dragFollowSpeed * Time.deltaTime);
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame && mouseLeftRock != null)
        {
            mouseLeftRock.isBeingDragged = false;
            mouseLeftRock = null;
        }

        // Right Click -> Drag Rock 2
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            RockItem rock = FindRockAtScreenPoint(mousePos);
            if (rock != null && rock != mouseLeftRock)
            {
                mouseRightRock = rock;
                mouseRightRock.isBeingDragged = true;
            }
        }
        else if (Mouse.current.rightButton.isPressed && mouseRightRock != null)
        {
            Vector3 targetPos = ScreenToGroundPoint(mousePos, dragHeight);
            mouseRightRock.transform.position = Vector3.MoveTowards(mouseRightRock.transform.position, targetPos, dragFollowSpeed * Time.deltaTime);
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame && mouseRightRock != null)
        {
            mouseRightRock.isBeingDragged = false;
            mouseRightRock = null;
        }
    }

    private RockItem FindRockAtScreenPoint(Vector2 screenPos)
    {
        if (mainCamera == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            RockItem rock = hit.collider.GetComponentInParent<RockItem>();
            if (rock != null) return rock;
        }

        // Also check distance to all active rocks on ground
        RockItem[] allRocks = Object.FindObjectsByType<RockItem>();
        RockItem closestRock = null;
        float closestDist = float.MaxValue;

        Vector3 groundPoint = ScreenToGroundPoint(screenPos, dragHeight);
        foreach (var r in allRocks)
        {
            if (r == null) continue;
            float dist = Vector3.Distance(groundPoint, r.transform.position);
            if (dist < 1.8f && dist < closestDist)
            {
                closestDist = dist;
                closestRock = r;
            }
        }

        return closestRock;
    }

    private Vector3 ScreenToGroundPoint(Vector2 screenPos, float yPlane)
    {
        if (mainCamera == null) return Vector3.zero;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, yPlane, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return Vector3.zero;
    }

    private void CheckProximitySmashes()
    {
        // Check collisions between any currently dragged rocks
        RockItem[] rocks = Object.FindObjectsByType<RockItem>();
        for (int i = 0; i < rocks.Length; i++)
        {
            if (rocks[i] == null) continue;
            for (int j = i + 1; j < rocks.Length; j++)
            {
                if (rocks[j] == null) continue;

                float dist = Vector3.Distance(rocks[i].transform.position, rocks[j].transform.position);
                if (dist < 1.1f)
                {
                    float relSpeed = (rocks[i].Velocity - rocks[j].Velocity).magnitude;
                    if (rocks[i].isBeingDragged || rocks[j].isBeingDragged)
                    {
                        rocks[i].CheckCollisionWithOtherRock(rocks[j].gameObject, Mathf.Max(relSpeed, hardSmashSpeed));
                    }
                }
            }
        }
    }

    public void SmashHeldRocks()
    {
        TopDownPlayerController player = Object.FindAnyObjectByType<TopDownPlayerController>();
        if (player != null)
        {
            player.SmashBothRocks();
        }
    }
}
