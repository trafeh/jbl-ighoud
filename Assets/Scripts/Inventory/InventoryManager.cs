using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core weight-based inventory system for Jbel Ighoud expedition.
/// Manages specimen collection, encumbrance limit (default 200 kg),
/// dropping items back to the world, and UI synchronization.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;
    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InventoryManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InventoryManager");
                    _instance = go.AddComponent<InventoryManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Capacity & Encumbrance")]
    [Tooltip("Maximum carrying capacity in kilograms (default: 200.0 kg).")]
    [SerializeField] private float maxWeight = 200.0f;

    [Header("Inventory Storage")]
    [SerializeField] private List<RockItem> items = new List<RockItem>();

    // Transform where stored items are hidden while in inventory
    private Transform storageContainer;

    // Events
    public event Action OnInventoryChanged;
    public event Action<string, Color> OnInventoryNotification;

    public float MaxWeight
    {
        get => maxWeight;
        set => maxWeight = Mathf.Max(1f, value);
    }

    public float CurrentWeight
    {
        get
        {
            float total = 0f;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                {
                    items.RemoveAt(i);
                    continue;
                }
                total += items[i].GetWeight();
            }
            return total;
        }
    }

    public float WeightRatio => Mathf.Clamp01(CurrentWeight / Mathf.Max(0.001f, maxWeight));
    public int ItemCount => items.Count;
    public IReadOnlyList<RockItem> Items => items;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        EnsureStorageContainer();
    }

    private void EnsureStorageContainer()
    {
        if (storageContainer == null)
        {
            GameObject container = new GameObject("StoredInventoryItems");
            container.transform.SetParent(transform, false);
            container.transform.position = new Vector3(0f, -2000f, 0f); // Hidden deep in void
            container.SetActive(false);
            storageContainer = container.transform;
        }
    }

    /// <summary>
    /// Checks if adding a given weight would exceed the 200kg maximum limit.
    /// </summary>
    public bool CanAddItem(float itemWeight)
    {
        return (CurrentWeight + itemWeight) <= (maxWeight + 0.001f);
    }

    /// <summary>
    /// Checks if a specific rock item can be carried.
    /// </summary>
    public bool CanAddItem(RockItem rock)
    {
        if (rock == null) return false;
        return CanAddItem(rock.GetWeight());
    }

    /// <summary>
    /// Adds a rock specimen into the expedition satchel, putting it in inventory (NOT in hand).
    /// </summary>
    public bool AddItem(RockItem rock)
    {
        if (rock == null) return false;

        float rockWeight = rock.GetWeight();
        if (!CanAddItem(rockWeight))
        {
            string warn = $"Cannot carry! Adding {rockWeight:F1} kg exceeds 200 kg limit ({CurrentWeight:F1}/{maxWeight:F1} kg).";
            Debug.LogWarning($"<color=#FF5555><b>[Inventory]</b></color> {warn}");
            OnInventoryNotification?.Invoke(warn, new Color(1f, 0.4f, 0.4f));
            return false;
        }

        if (items.Contains(rock))
        {
            return false;
        }

        EnsureStorageContainer();

        // Remove from player hands if it happened to be held
        if (rock.isHeld)
        {
            TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
            if (player != null)
            {
                player.ReleaseHeldRock(rock);
            }
        }

        // Parent to storage container and disable world presence
        rock.transform.SetParent(storageContainer, false);
        rock.gameObject.SetActive(false);

        items.Add(rock);

        string rockName = rock.rockData != null ? rock.rockData.rockName : rock.name;
        string notif = $"[+] {rockName} ({rockWeight:F1} kg) stored in Satchel.";
        Debug.Log($"<color=#89D99D><b>[Inventory]</b></color> {notif} Total: {CurrentWeight:F1}/{maxWeight:F1} kg");
        OnInventoryNotification?.Invoke(notif, new Color(0.55f, 0.95f, 0.65f));

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Drops an item from inventory back onto the ground in front of the player with physics.
    /// </summary>
    public bool DropItem(RockItem rock)
    {
        if (rock == null || !items.Contains(rock)) return false;

        items.Remove(rock);

        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
        Vector3 dropPos = Vector3.up * 2f;
        Vector3 forwardDir = Vector3.forward;

        if (player != null)
        {
            dropPos = player.transform.position + player.transform.forward * 1.4f + Vector3.up * 0.7f;
            forwardDir = player.transform.forward;
        }
        else if (Camera.main != null)
        {
            dropPos = Camera.main.transform.position + Camera.main.transform.forward * 2.5f;
            forwardDir = Camera.main.transform.forward;
        }

        // Restore to scene
        rock.transform.SetParent(null, true);
        rock.transform.position = dropPos;
        rock.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), 0);
        rock.gameObject.SetActive(true);

        // Restore colliders
        rock.CacheColliders();
        Collider[] colliders = rock.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = true;
        }

        // Apply physical settling impulse
        Rigidbody rb = rock.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = forwardDir * 1.5f + Vector3.up * 0.8f;
#else
            rb.velocity = forwardDir * 1.5f + Vector3.up * 0.8f;
#endif
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2f;
        }

        string rockName = rock.rockData != null ? rock.rockData.rockName : rock.name;
        string notif = $"[-] Dropped {rockName} ({rock.GetWeight():F1} kg) on ground.";
        Debug.Log($"<color=#E0B050><b>[Inventory]</b></color> {notif} Total: {CurrentWeight:F1}/{maxWeight:F1} kg");
        OnInventoryNotification?.Invoke(notif, new Color(0.95f, 0.78f, 0.45f));

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Moves a rock from the satchel into the player's physical hands (e.g. for knapping or smashing).
    /// </summary>
    public bool EquipToHand(RockItem rock)
    {
        if (rock == null || !items.Contains(rock)) return false;

        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
        if (player == null) return false;

        if (player.HeldRockCount >= player.MaxHeldRocks)
        {
            string warn = $"Hands are full ({player.HeldRockCount}/{player.MaxHeldRocks})! Put down currently held rock first.";
            OnInventoryNotification?.Invoke(warn, new Color(1f, 0.4f, 0.4f));
            return false;
        }

        items.Remove(rock);

        rock.transform.SetParent(null, true);
        rock.gameObject.SetActive(true);

        bool pickedUp = player.PickUpSpecificRock(rock);
        if (!pickedUp)
        {
            // If failed to pick up, restore to inventory
            items.Add(rock);
            rock.transform.SetParent(storageContainer, false);
            rock.gameObject.SetActive(false);
            return false;
        }

        string rockName = rock.rockData != null ? rock.rockData.rockName : rock.name;
        string notif = $"Equipped {rockName} to hand.";
        OnInventoryNotification?.Invoke(notif, new Color(0.55f, 0.95f, 0.65f));

        OnInventoryChanged?.Invoke();
        return true;
    }
}
