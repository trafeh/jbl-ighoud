using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Springy squash-on-press feel for HUD action buttons (Jump, etc.).</summary>
public class JbelPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressedScale = 0.86f;
    private bool held;
    private float scale = 1f, vel;

    public void OnPointerDown(PointerEventData e) { held = true; }
    public void OnPointerUp(PointerEventData e)   { held = false; vel += 5f; }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        vel += ((held ? pressedScale : 1f) - scale) * 320f * dt;
        vel *= Mathf.Exp(-20f * dt);
        scale += vel * dt;
        transform.localScale = Vector3.one * scale;
    }

    private void OnDisable() { held = false; scale = 1f; vel = 0f; transform.localScale = Vector3.one; }
}
