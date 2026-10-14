using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Target to Follow")]
    [SerializeField] private Transform target;

    [Header("Camera Positioning")]
    [Tooltip("Offset relative to the player. (0, 12, -8) gives a classic top-down isometric view.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);

    [Tooltip("Downward angle in degrees.")]
    [SerializeField] private float pitchAngle = 55f;

    [Header("Smooth Damping")]
    [Tooltip("Time taken to smoothly follow the player.")]
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 currentVelocity = Vector3.zero;

    private void Awake()
    {
        if (target == null)
        {
            TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void Start()
    {
        ApplyCameraRotation();
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
    }

    private void OnValidate()
    {
        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
