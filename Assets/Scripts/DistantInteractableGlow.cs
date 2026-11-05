using UnityEngine;

/// <summary>
/// DistantInteractableGlow - Glow and shine effects removed per user request.
/// Safely cleans up any legacy beacon flares and disables itself.
/// </summary>
[DisallowMultipleComponent]
public class DistantInteractableGlow : MonoBehaviour
{
    private void Awake()
    {
        CleanUpFlare();
        enabled = false;
    }

    private void OnEnable()
    {
        CleanUpFlare();
        enabled = false;
    }

    private void Start()
    {
        CleanUpFlare();
        enabled = false;
    }

    private void CleanUpFlare()
    {
        Transform existing = transform.Find("DistantBeaconFlare");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }
}
