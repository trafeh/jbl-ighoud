using UnityEngine;

public class RockItem : MonoBehaviour
{
    [Header("Rock Attributes")]
    [Range(1f, 10f)]
    [Tooltip("Strength/Force of the rock. Under 5: destroys in 1 hit. 5-10: needs multiple hits.")]
    public float strength = 3f;

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
        rockRenderer = GetComponent<Renderer>();
        if (rockRenderer != null)
        {
            originalColor = rockRenderer.material.color;
        }

        SetupDurability();
        CreateFloatingLabel();
        lastPosition = transform.position;
    }

    private void Start()
    {
        SetupDurability();
        UpdateLabel();
    }

    private void OnValidate()
    {
        SetupDurability();
        UpdateLabel();
    }

    public void SetupDurability()
    {
        // Under 5: destroys in 1 hit. 5-10: needs 2 hits to destroy.
        maxHits = strength < 5f ? 1 : 2;
        currentHits = maxHits;
    }

    public void CreateFloatingLabel()
    {
        Transform existing = transform.Find("RockLabel");
        if (existing != null)
        {
            labelTextMesh = existing.GetComponent<TextMesh>();
        }
        else
        {
            GameObject textObj = new GameObject("RockLabel");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            labelTextMesh = textObj.AddComponent<TextMesh>();
            labelTextMesh.alignment = TextAlignment.Center;
            labelTextMesh.anchor = TextAnchor.MiddleCenter;
            labelTextMesh.characterSize = 0.12f;
            labelTextMesh.fontSize = 36;
            labelTextMesh.fontStyle = FontStyle.Bold;
        }

        UpdateLabel();
    }

    public void UpdateLabel()
    {
        if (labelTextMesh == null)
        {
            Transform existing = transform.Find("RockLabel");
            if (existing != null) labelTextMesh = existing.GetComponent<TextMesh>();
        }
        if (labelTextMesh == null) return;

        string strengthCategory = strength < 5f ? "< 5 (Weak)" : "5-10 (Strong)";
        labelTextMesh.text = $"STR: {strength:F1} | {strengthCategory}\nHP: {currentHits}/{maxHits}";
        labelTextMesh.color = strength < 5f ? new Color(1f, 0.45f, 0.35f) : new Color(0.35f, 0.95f, 0.55f);
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

    public void SetHeld(TopDownPlayerController player, int slotIndex)
    {
        isHeld = true;
        holdingPlayer = player;
        heldSlotIndex = slotIndex;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void ReleaseHeld()
    {
        isHeld = false;
        holdingPlayer = null;
        heldSlotIndex = -1;
        isBeingDragged = false;
        draggingPointerId = -1;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
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

        SpawnHitEffect((transform.position + otherRock.transform.position) * 0.5f);

        // Apply hit to both rocks according to strength rules:
        // - Under 5: destroyed in 1 hit
        // - 5-10: needs more than 1 hit to destroy
        this.TakeHit();
        otherRock.TakeHit();
    }

    public void TakeHit()
    {
        currentHits--;
        UpdateLabel();

        // Flash red
        if (rockRenderer != null)
        {
            rockRenderer.material.color = Color.red;
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

    public void DestroyRock()
    {
        SpawnDestructionEffect();

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

    private void SpawnHitEffect(Vector3 pos)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "SmashEffect";
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * 0.5f;
        Collider col = spark.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = spark.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.yellow;
        }
        Destroy(spark, 0.25f);
    }

    private void SpawnDestructionEffect()
    {
        for (int i = 0; i < 6; i++)
        {
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "RockDebris";
            debris.transform.position = transform.position + Random.insideUnitSphere * 0.3f;
            debris.transform.localScale = Vector3.one * Random.Range(0.18f, 0.32f);
            
            Collider col = debris.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = debris.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.35f, 0.35f, 0.35f);
            }

            Rigidbody rb = debris.AddComponent<Rigidbody>();
            rb.AddExplosionForce(280f, transform.position, 2f);

            Destroy(debris, 1.2f);
        }
    }
}
