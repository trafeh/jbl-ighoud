using UnityEngine;

public class RockItem : MonoBehaviour
{
    [Header("Rock Attributes")]
    [Range(1f, 20f)]
    [Tooltip("Strength/Hardness value (1-5: Very Soft, 5-10: Soft/Brittle, 10-15: Hard/Abrasive, 15-20: Tool Core/Flint).")]
    public float strength = 3f;

    [Header("Inventory & Weight Settings")]
    [Tooltip("Weight in kg. If set to 0 or left unconfigured, automatically synchronizes with rockData.mass.")]
    [SerializeField] private float weight = 0f;

    public float Weight
    {
        get => GetWeight();
        set => weight = value;
    }

    [Header("Fragments (crafting material)")]
    [Tooltip("> 0: this item is a stack of small pieces of Rock Data, not a whole stone.")]
    public int fragmentCount = 0;
    public bool IsFragment => fragmentCount > 0;

    [Tooltip("The Elder's quartzite hammerstone: used to strike other stones, never breaks.")]
    public bool isHammerstone = false;
    /// <summary>Pieces a whole stone breaks into when it shatters in the player's hands.</summary>
    public const int FragmentsPerStone = 5;
    private const float FragmentMassRatio = 0.08f;

    public float GetWeight()
    {
        if (IsFragment) return Mathf.Max(0.01f, (rockData != null ? rockData.mass : 1.2f) * FragmentMassRatio * fragmentCount);
        if (weight > 0.001f) return weight;
        if (rockData != null && rockData.mass > 0.001f) return rockData.mass;
        return 1.2f;
    }

    [Header("Prehistoric Lithic Data (Jbel Ighoud)")]
    [Tooltip("Optional RockData ScriptableObject defining authentic prehistoric rock properties.")]
    public JbelIghoud.Lithic.RockData rockData;

    [Tooltip("Minimum relative collision speed to count as a hard hit.")]
    [SerializeField] private float hardHitSpeed = 2.5f;

    [Header("State")]
    public bool isHeld = false;
    public bool isBeingDragged = false;
    public int draggingPointerId = -1;

    [SerializeField] private int maxHits = 1;
    [SerializeField] private int currentHits = 1;

    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private float lastHitTimestamp = -10f;
    private TopDownPlayerController holdingPlayer;
    private int heldSlotIndex = -1;

    private TextMesh labelTextMesh;
    private Renderer rockRenderer;
    private Color originalColor;

    public int HitsRemaining => currentHits;
    public int MaxHits => maxHits;
    public Vector3 Velocity => currentVelocity;

    public void RestoreState(float restoredStrength, int restoredCurrentHits, int restoredMaxHits)
    {
        strength = restoredStrength;
        maxHits = restoredMaxHits;
        currentHits = Mathf.Clamp(restoredCurrentHits, 0, maxHits);
        UpdateLabel();
    }

    private void Awake()
    {
        ApplyRockData();
        SetupDurability();
        CacheColliders();
        RemoveLegacyLabelsAndGlow();
        lastPosition = transform.position;
    }

    private void RemoveLegacyLabelsAndGlow()
    {
        Transform label = transform.Find("RockLabel");
        if (label != null)
        {
            if (Application.isPlaying) Destroy(label.gameObject);
            else DestroyImmediate(label.gameObject);
        }

        var glow = GetComponent<DistantInteractableGlow>();
        if (glow != null)
        {
            if (Application.isPlaying) Destroy(glow);
            else DestroyImmediate(glow);
        }
    }

    public void ApplyRockData()
    {
        if (rockData != null)
        {
            // fragment piles use their own low-poly shard mesh (Resources/Fragments/FragmentPile_<data>)
            if (IsFragment)
            {
                var pile = Resources.Load<Mesh>("Fragments/FragmentPile_" + rockData.name);
                if (pile != null)
                {
                    MeshFilter fmf = GetComponent<MeshFilter>();
                    if (fmf == null) fmf = gameObject.AddComponent<MeshFilter>();
                    fmf.sharedMesh = pile;
                    MeshCollider fmc = GetComponent<MeshCollider>();
                    if (fmc == null) fmc = gameObject.AddComponent<MeshCollider>();
                    fmc.sharedMesh = pile;
                    fmc.convex = true;
                }
            }
            else if (rockData.rockMesh != null)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                if (mf.sharedMesh != rockData.rockMesh)
                {
                    mf.sharedMesh = rockData.rockMesh;
                }

                SphereCollider sc = GetComponent<SphereCollider>();
                if (sc != null)
                {
                    DestroyImmediate(sc);
                }
                MeshCollider mc = GetComponent<MeshCollider>();
                if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = rockData.rockMesh;
                mc.convex = true;
            }

            rockRenderer = GetComponent<Renderer>();
            if (rockRenderer != null)
            {
                if (rockData.rockMaterial != null) rockRenderer.sharedMaterial = rockData.rockMaterial;
                else if (Application.isPlaying)
                {
                    rockRenderer.material.color = rockData.rockColor;
                    originalColor = rockRenderer.material.color;
                }
            }

            if (weight <= 0.001f && rockData.mass > 0f)
            {
                weight = rockData.mass;
            }
        }
    }

    private void Start()
    {
        ApplyRockData();
        SetupDurability();
        UpdateLabel();
    }

    private void OnValidate()
    {
        ApplyRockData();
        SetupDurability();
        UpdateLabel();
    }

    public void SetupDurability()
    {
        if (rockData != null)
        {
            strength = rockData.hardness;
            maxHits = rockData.baseDurability;
            currentHits = maxHits;
            return;
        }

        // Under 5: destroys in 1 hit. 5-10: needs 2 hits to destroy.
        maxHits = strength < 5f ? 1 : 2;
        currentHits = maxHits;
    }

    public void CreateFloatingLabel()
    {
        Transform existing = transform.Find("RockLabel");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    public void UpdateLabel()
    {
        Transform existing = transform.Find("RockLabel");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (labelTextMesh != null && Camera.main != null)
        {
            labelTextMesh.transform.rotation = Camera.main.transform.rotation;
        }

        if (Time.deltaTime > 0f)
        {
            currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        }
        lastPosition = transform.position;
    }

    private Collider[] rockColliders;
    private bool[] originalTriggerStates;

    public void CacheColliders()
    {
        if (rockColliders == null || rockColliders.Length == 0)
        {
            rockColliders = GetComponentsInChildren<Collider>();
            originalTriggerStates = new bool[rockColliders.Length];
            for (int i = 0; i < rockColliders.Length; i++)
            {
                if (rockColliders[i] != null)
                {
                    originalTriggerStates[i] = rockColliders[i].isTrigger;
                }
            }
        }
    }

    public void SetHeld(TopDownPlayerController player, int slotIndex)
    {
        isHeld = true;
        holdingPlayer = player;
        heldSlotIndex = slotIndex;

        CacheColliders();

        // Ensure Rigidbody exists so this object is NEVER treated as a moving static collider!
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#endif
        rb.isKinematic = true;
        rb.detectCollisions = false;

        // Convert rock colliders to triggers and ignore all collisions with player colliders
        Collider[] playerColliders = player != null ? player.GetComponentsInChildren<Collider>() : null;

        for (int i = 0; i < rockColliders.Length; i++)
        {
            if (rockColliders[i] == null) continue;
            rockColliders[i].isTrigger = true;

            if (playerColliders != null)
            {
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    if (playerColliders[j] != null)
                    {
                        Physics.IgnoreCollision(rockColliders[i], playerColliders[j], true);
                    }
                }
            }
        }

        // Sync with Lithic Rock component if attached
        JbelIghoud.Lithic.Rock lithicRock = GetComponent<JbelIghoud.Lithic.Rock>();
        if (lithicRock != null)
        {
            lithicRock.SetHeld(true, player != null ? player.gameObject : null);
        }
    }

    public void ReleaseHeld()
    {
        CacheColliders();

        // Restore collision ignore with player
        if (holdingPlayer != null && rockColliders != null)
        {
            Collider[] playerColliders = holdingPlayer.GetComponentsInChildren<Collider>();
            for (int i = 0; i < rockColliders.Length; i++)
            {
                if (rockColliders[i] != null && playerColliders != null)
                {
                    for (int j = 0; j < playerColliders.Length; j++)
                    {
                        if (playerColliders[j] != null)
                        {
                            Physics.IgnoreCollision(rockColliders[i], playerColliders[j], false);
                        }
                    }
                }
            }
        }

        // Unconditionally restore solid collision on release
        if (rockColliders != null)
        {
            for (int i = 0; i < rockColliders.Length; i++)
            {
                if (rockColliders[i] != null)
                {
                    rockColliders[i].isTrigger = false;
                }
            }
        }

        isHeld = false;
        holdingPlayer = null;
        heldSlotIndex = -1;
        isBeingDragged = false;
        draggingPointerId = -1;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        JbelIghoud.Lithic.Rock lithicRock = GetComponent<JbelIghoud.Lithic.Rock>();
        if (lithicRock != null)
        {
            lithicRock.SetHeld(false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckCollisionWithOtherRock(collision.gameObject, collision.relativeVelocity.magnitude);
    }

    private void OnTriggerEnter(Collider other)
    {
        RockItem otherRock = other.GetComponent<RockItem>();
        if (otherRock != null)
        {
            float relSpeed = (currentVelocity - otherRock.Velocity).magnitude;
            CheckCollisionWithOtherRock(other.gameObject, relSpeed);
        }
    }

    public void CheckCollisionWithOtherRock(GameObject otherGo, float impactSpeed)
    {
        RockItem otherRock = otherGo.GetComponent<RockItem>();
        if (otherRock == null || otherRock == this) return;

        // Debounce hits
        if (Time.time - lastHitTimestamp < 0.35f || Time.time - otherRock.lastHitTimestamp < 0.35f) return;

        // Check if collision is hard enough
        if (impactSpeed < hardHitSpeed && currentVelocity.magnitude < hardHitSpeed && otherRock.Velocity.magnitude < hardHitSpeed)
        {
            return;
        }

        lastHitTimestamp = Time.time;
        otherRock.lastHitTimestamp = Time.time;

        Vector3 contactMid = (transform.position + otherRock.transform.position) * 0.5f;
        SpawnHitEffect(contactMid);

        // Prehistoric Lithic Matrix Evaluation if RockData is attached
        var matrix = Resources.Load<JbelIghoud.Lithic.RockInteractionMatrix>("DefaultRockInteractionMatrix");
        if (matrix != null && (rockData != null || otherRock.rockData != null))
        {
            RockItem striker = currentVelocity.sqrMagnitude >= otherRock.Velocity.sqrMagnitude ? this : otherRock;
            RockItem receiver = striker == this ? otherRock : this;

            var result = matrix.ResolveCollision(striker.rockData, receiver.rockData, impactSpeed);
            if (result.outcomeType != JbelIghoud.Lithic.InteractionOutcomeType.NoEffect)
            {
                if (result.spawnedYieldPrefab != null)
                {
                    Instantiate(result.spawnedYieldPrefab, contactMid + Vector3.up * 0.15f, Quaternion.identity);
                }

                if (result.damageToStriker > 0) striker.TakeHit(result.damageToStriker);
                if (result.damageToReceiver > 0) receiver.TakeHit(result.damageToReceiver);
                return;
            }
        }

        // Fallback default hit
        this.TakeHit();
        otherRock.TakeHit();
    }

    private float partialDamage;
    /// <summary>Health left including fractions (timing-bar hits deal 2 or 0.4).</summary>
    public float HealthLeft => Mathf.Max(0f, currentHits - partialDamage);

    /// <summary>Fractional damage: weak hits (0.4) add up until they remove a whole hit point.</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (isHammerstone) { TakeHit(1); return; }
        partialDamage += amount;
        int whole = Mathf.FloorToInt(partialDamage + 0.0001f);
        partialDamage -= whole;
        if (whole > 0) TakeHit(whole);
        else if (rockRenderer != null) { rockRenderer.material.color = Color.Lerp(originalColor, Color.white, 0.3f); Invoke(nameof(ResetColor), 0.1f); }
    }

    public void TakeHit(int damage = 1)
    {
        if (isHammerstone)
        {
            // the hammerstone never wears out: just a quick flash
            if (rockRenderer != null) { rockRenderer.material.color = Color.Lerp(originalColor, Color.white, 0.4f); Invoke(nameof(ResetColor), 0.12f); }
            return;
        }
        currentHits = Mathf.Max(0, currentHits - damage);
        UpdateLabel();

        // Flash red
        if (rockRenderer != null)
        {
            rockRenderer.material.color = Color.Lerp(originalColor, Color.white, 0.55f); // quick bright flash
            Invoke(nameof(ResetColor), 0.15f);
        }

        if (currentHits <= 0)
        {
            DestroyRock();
        }
    }

    private void ResetColor()
    {
        if (rockRenderer != null)
        {
            rockRenderer.material.color = originalColor;
        }
    }

    public System.Action<RockItem> OnRockDestroyedEvent;

    public void DestroyRock()
    {
        SpawnDestructionEffect();

        // broken by the player (in hand / striking): the pieces go to the bag as crafting material
        if (!IsFragment && (isHeld || (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode)))
            GrantFragments(FragmentsPerStone);

        OnRockDestroyedEvent?.Invoke(this);

        if (holdingPlayer != null)
        {
            holdingPlayer.OnRockDestroyed(this);
        }

        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.NotifyRockDestroyed(gameObject.name);
        }

        Destroy(gameObject);
    }

    /// <summary>Adds the pieces of this stone to the bag, stacking onto an existing pile of the same stone.</summary>
    private void GrantFragments(int count)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || rockData == null || count <= 0) return;
        string label = (rockData.rockName ?? rockData.name);

        var items = inv.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.IsFragment && it.rockData == rockData)
            {
                it.fragmentCount += count;
                inv.Notify($"[+] {count} fragments  ({label})", new Color(0.95f, 0.82f, 0.5f));
                inv.NotifyChanged();
                return;
            }
        }

        // new pile: a small, inactive copy of this stone that lives in the bag
        var go = Instantiate(gameObject);
        go.name = rockData.name + "_Fragments";
        var frag = go.GetComponent<RockItem>();
        frag.isHeld = false;
        frag.isBeingDragged = false;
        frag.fragmentCount = count;
        var lithic = go.GetComponent<JbelIghoud.Lithic.Rock>();
        if (lithic != null) Destroy(lithic); // it would re-apply the whole-stone mesh
        frag.ApplyRockData();
        var shine = go.GetComponent("JbelStoneShine");
        if (shine != null) Destroy(shine);
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            go.transform.localScale = Vector3.one * (0.35f / Mathf.Max(0.0001f, mf.sharedMesh.bounds.size.magnitude)); // ~35 cm pile
        if (!inv.AddItem(frag)) { Destroy(go); return; }
        inv.Notify($"[+] {count} fragments  ({label})", new Color(0.95f, 0.82f, 0.5f));
    }

    private void SpawnHitEffect(Vector3 pos)
    {
        // the first-person strike already plays the full impact feedback
        if (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode) return;
        JbelImpactFX.Hit(pos, rockData != null ? rockData.hardness : strength, rockData != null ? rockData.residueColor : Color.gray);
    }

    private void SpawnDestructionEffect()
    {
        Color c = rockData != null ? rockData.rockColor : new Color(0.35f, 0.35f, 0.35f);
        JbelImpactFX.Shatter(transform.position, c, 0.8f);
    }
}
