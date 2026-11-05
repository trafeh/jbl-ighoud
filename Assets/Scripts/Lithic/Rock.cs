using System;
using UnityEngine;

namespace JbelIghoud.Lithic
{
    [RequireComponent(typeof(Collider))]
    public class Rock : MonoBehaviour
    {
        [Header("Rock Configuration (ScriptableObject)")]
        [SerializeField] private RockData rockData;

        [Header("Runtime State")]
        [SerializeField] private int currentDurability = 3;
        [SerializeField] private bool isHeld = false;

        [Header("Interaction System")]
        [Tooltip("Shared interaction matrix. If left empty, will attempt to find one in Resources or scene.")]
        [SerializeField] private RockInteractionMatrix interactionMatrix;

        [Tooltip("Minimum impact velocity to trigger a strike collision.")]
        [SerializeField] private float minImpactVelocity = 2.0f;

        [Tooltip("Cooldown between registered impacts to prevent physics stutter.")]
        [SerializeField] private float hitCooldown = 0.35f;

        // Internal tracking
        private float lastHitTimestamp = -10f;
        private Vector3 lastPosition;
        private Vector3 currentVelocity;
        private Rigidbody rb;
        private Renderer rockRenderer;
        private Color originalColor;
        private TextMesh floatingLabel;

        // Events
        public event Action<Rock, InteractionResult> OnInteractionResolved;
        public event Action<Rock> OnRockDestroyed;

        public RockData Data => rockData;
        public int CurrentDurability => currentDurability;
        public int MaxDurability => rockData != null ? rockData.baseDurability : 3;
        public float Hardness => rockData != null ? rockData.hardness : 5f;
        public Vector3 Velocity
        {
            get
            {
                if (rb != null && !rb.isKinematic)
                {
#if UNITY_6000_0_OR_NEWER
                    return rb.linearVelocity;
#else
                    return rb.velocity;
#endif
                }
                return currentVelocity;
            }
        }
        public bool IsHeld => isHeld;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rockRenderer = GetComponent<Renderer>();

            InitializeFromData();
            // CreateOrUpdateDebugLabel();
            CacheColliders();
            lastPosition = transform.position;
        }

        private void Start()
        {
            if (interactionMatrix == null)
            {
                // Look for an InteractionMatrix asset in Resources or Scene
                interactionMatrix = Resources.Load<RockInteractionMatrix>("DefaultRockInteractionMatrix");
            }

            InitializeFromData();
            // UpdateDebugLabel();
        }

        private void OnValidate()
        {
            if (rockData != null)
            {
                currentDurability = Mathf.Clamp(currentDurability, 0, rockData.baseDurability);
                ApplyVisualAttributes();
                // UpdateDebugLabel();
            }
        }

        private void FixedUpdate()
        {
            if (Time.fixedDeltaTime > 0f)
            {
                currentVelocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
            }
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (floatingLabel != null && Camera.main != null)
            {
                floatingLabel.transform.rotation = Camera.main.transform.rotation;
            }
        }

        /// <summary>
        /// Sets new RockData at runtime (e.g. dynamic spawning or morphing).
        /// </summary>
        public void SetRockData(RockData newData, bool resetDurability = true)
        {
            rockData = newData;
            if (resetDurability && rockData != null)
            {
                currentDurability = rockData.baseDurability;
            }
            InitializeFromData();
            // UpdateDebugLabel();
        }

        private void InitializeFromData()
        {
            if (rockData == null) return;

            if (currentDurability <= 0)
            {
                currentDurability = rockData.baseDurability;
            }

            if (rb != null)
            {
                rb.mass = Mathf.Max(0.1f, rockData.mass);
            }

            ApplyVisualAttributes();
        }

        private void ApplyVisualAttributes()
        {
            if (rockRenderer == null) rockRenderer = GetComponent<Renderer>();
            if (rockData != null)
            {
                // Apply 3D Mesh if specified
                if (rockData.rockMesh != null)
                {
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                    if (mf.sharedMesh != rockData.rockMesh)
                    {
                        mf.sharedMesh = rockData.rockMesh;
                    }

                    // Update collider to convex mesh collider
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

                if (rockRenderer != null)
                {
                    if (rockData.rockMaterial != null)
                    {
                        rockRenderer.sharedMaterial = rockData.rockMaterial;
                    }
                    else
                    {
                        rockRenderer.material.color = rockData.rockColor;
                    }
                    originalColor = rockRenderer.material.color;
                }
            }
        }

        #region Collision & Interaction Resolution

        private void OnCollisionEnter(Collision collision)
        {
            Rock otherRock = collision.gameObject.GetComponent<Rock>();
            if (otherRock == null) return;

            // Calculate impact velocity
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minImpactVelocity)
            {
                // Fallback to velocity tracking if kinematic or dragged
                impactSpeed = Mathf.Max(currentVelocity.magnitude, otherRock.Velocity.magnitude);
            }

            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            HandleRockImpact(otherRock, impactSpeed, contactPoint);
        }

        /// <summary>
        /// Evaluates impact against another rock using the interaction matrix.
        /// </summary>
        public void HandleRockImpact(Rock otherRock, float impactSpeed, Vector3 contactPoint)
        {
            if (otherRock == null || otherRock == this) return;
            if (interactionMatrix == null) return;

            // Debounce strikes
            if (Time.time - lastHitTimestamp < hitCooldown || Time.time - otherRock.lastHitTimestamp < hitCooldown)
            {
                return;
            }

            if (impactSpeed < minImpactVelocity) return;

            lastHitTimestamp = Time.time;
            otherRock.lastHitTimestamp = Time.time;

            // Determine Striker (rock moving faster) vs Receiver (rock moving slower / stationary)
            Rock striker = this.Velocity.sqrMagnitude >= otherRock.Velocity.sqrMagnitude ? this : otherRock;
            Rock receiver = striker == this ? otherRock : this;

            // Resolve outcome via Matrix
            InteractionResult result = interactionMatrix.ResolveCollision(striker, receiver, impactSpeed);

            if (result.outcomeType == InteractionOutcomeType.NoEffect)
            {
                PlayStrikeAudio(striker);
                return;
            }

            // Visual hit feedback
            SpawnHitSparks(contactPoint, result);
            PlayStrikeAudio(striker, result.sfxClip);

            Debug.Log($"<color=#F4D03F><b>[Lithic Matrix]</b></color> {striker.Data?.rockName} struck {receiver.Data?.rockName} (Impact: {impactSpeed:F1}). Outcome: <b>{result.outcomeType}</b> | {result.feedbackMessage}");

            // Spawn Yield item (flake, powder, fossil, paste)
            if (result.spawnedYieldPrefab != null)
            {
                SpawnYieldItem(result.spawnedYieldPrefab, contactPoint, result.outcomeType);
            }
            else
            {
                SpawnProceduralGreyboxYield(result.outcomeType, receiver.Data, contactPoint);
            }

            // Apply Durability Damage
            if (result.damageToStriker > 0)
            {
                striker.ApplyDurabilityDamage(result.damageToStriker);
            }

            if (result.damageToReceiver > 0)
            {
                receiver.ApplyDurabilityDamage(result.damageToReceiver);
            }

            // Notify listeners
            striker.OnInteractionResolved?.Invoke(striker, result);
            receiver.OnInteractionResolved?.Invoke(receiver, result);
        }

        #endregion

        #region Damage, Shatter, and Spawning

        public void ApplyDurabilityDamage(int damage)
        {
            currentDurability = Mathf.Max(0, currentDurability - damage);
            // UpdateDebugLabel();

            // Flash red feedback
            if (rockRenderer != null)
            {
                rockRenderer.material.color = Color.red;
                Invoke(nameof(ResetVisualColor), 0.12f);
            }

            if (currentDurability <= 0)
            {
                ShatterRock();
            }
        }

        private void ResetVisualColor()
        {
            if (rockRenderer != null)
            {
                rockRenderer.material.color = originalColor;
            }
        }

        public void ShatterRock()
        {
            if (rockData != null && rockData.shatterClip != null)
            {
                AudioSource.PlayClipAtPoint(rockData.shatterClip, transform.position, 1f);
            }

            // Spawn debris prefab or procedural debris
            if (rockData != null && rockData.debrisPrefab != null)
            {
                Instantiate(rockData.debrisPrefab, transform.position, transform.rotation);
            }
            else
            {
                SpawnProceduralDebris();
            }

            OnRockDestroyed?.Invoke(this);
            Destroy(gameObject);
        }

        private void SpawnYieldItem(GameObject yieldPrefab, Vector3 spawnPos, InteractionOutcomeType outcome)
        {
            GameObject spawned = Instantiate(yieldPrefab, spawnPos + Vector3.up * 0.15f, Quaternion.identity);
            Rigidbody spawnRb = spawned.GetComponent<Rigidbody>();
            if (spawnRb != null)
            {
                spawnRb.AddForce(Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 1.5f, ForceMode.Impulse);
            }
        }

        private void SpawnProceduralGreyboxYield(InteractionOutcomeType outcome, RockData targetData, Vector3 spawnPos)
        {
            if (outcome == InteractionOutcomeType.NoEffect) return;

            GameObject yieldObj = null;
            string yieldName = "YieldItem";
            Color yieldCol = targetData != null ? targetData.residueColor : Color.white;

            switch (outcome)
            {
                case InteractionOutcomeType.ProduceFlake:
                    yieldObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    yieldObj.transform.localScale = new Vector3(0.35f, 0.03f, 0.2f); // Thin sharp flake/blade
                    yieldName = $"{targetData?.rockName ?? "Silex"} Flake";
                    break;

                case InteractionOutcomeType.ProducePowder:
                    yieldObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    yieldObj.transform.localScale = new Vector3(0.45f, 0.015f, 0.45f); // Powder pile
                    yieldName = $"{targetData?.rockName ?? "Calcaire"} Powder";
                    break;

                case InteractionOutcomeType.ProduceClayPaste:
                    yieldObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    yieldObj.transform.localScale = new Vector3(0.28f, 0.2f, 0.28f); // Clay lump
                    yieldName = "Malleable Clay Clump";
                    break;

                case InteractionOutcomeType.ExtractFossil:
                    yieldObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    yieldObj.transform.localScale = new Vector3(0.15f, 0.28f, 0.1f); // Tooth shape
                    yieldName = "Fossil Shark Tooth";
                    yieldCol = new Color(0.95f, 0.9f, 0.75f);
                    break;

                case InteractionOutcomeType.StrikeSparks:
                    SpawnHitSparks(spawnPos, new InteractionResult());
                    return;
            }

            if (yieldObj != null)
            {
                yieldObj.name = yieldName;
                yieldObj.transform.position = spawnPos + Vector3.up * 0.25f;

                Renderer rend = yieldObj.GetComponent<Renderer>();
                if (rend != null) rend.material.color = yieldCol;

                Rigidbody yRb = yieldObj.AddComponent<Rigidbody>();
                yRb.mass = 0.3f;
                yRb.AddForce(Vector3.up * 2.2f + UnityEngine.Random.insideUnitSphere * 1.2f, ForceMode.Impulse);

                RockResidue residue = yieldObj.AddComponent<RockResidue>();
                residue.itemName = yieldName;
                residue.yieldType = outcome;
                residue.itemColor = yieldCol;
            }
        }

        private void SpawnProceduralDebris()
        {
            Color debrisColor = rockData != null ? rockData.rockColor : Color.gray;
            for (int i = 0; i < 5; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "RockFragment";
                chunk.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * 0.2f;
                chunk.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.12f, 0.25f);

                Renderer r = chunk.GetComponent<Renderer>();
                if (r != null) r.material.color = debrisColor;

                Rigidbody cRb = chunk.AddComponent<Rigidbody>();
                cRb.mass = 0.2f;
                cRb.AddExplosionForce(220f, transform.position, 1.8f);

                Destroy(chunk, 2.5f);
            }
        }

        private void SpawnHitSparks(Vector3 pos, InteractionResult result)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ImpactFlash";
            spark.transform.position = pos;
            spark.transform.localScale = Vector3.one * 0.35f;

            Collider c = spark.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer rend = spark.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = result.outcomeType == InteractionOutcomeType.StrikeSparks ? new Color(1f, 0.8f, 0.1f) : Color.white;
            }
            Destroy(spark, 0.2f);
        }

        private void PlayStrikeAudio(Rock sourceRock, AudioClip overrideClip = null)
        {
            AudioClip clip = overrideClip;
            if (clip == null && sourceRock != null && sourceRock.Data != null && sourceRock.Data.strikeClips != null && sourceRock.Data.strikeClips.Length > 0)
            {
                clip = sourceRock.Data.strikeClips[UnityEngine.Random.Range(0, sourceRock.Data.strikeClips.Length)];
            }

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, 0.9f);
            }
        }

        #endregion

        #region Debug & Greybox UI

        // private void CreateOrUpdateDebugLabel()
        // {
        //     Transform existing = transform.Find("RockDebugLabel");
        //     if (existing != null)
        //     {
        //         floatingLabel = existing.GetComponent<TextMesh>();
        //     }
        //     else
        //     {
        //         GameObject labelObj = new GameObject("RockDebugLabel");
        //         labelObj.transform.SetParent(transform);
        //         labelObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);

        //         floatingLabel = labelObj.AddComponent<TextMesh>();
        //         floatingLabel.alignment = TextAlignment.Center;
        //         floatingLabel.anchor = TextAnchor.MiddleCenter;
        //         floatingLabel.characterSize = 0.11f;
        //         floatingLabel.fontSize = 32;
        //         floatingLabel.fontStyle = FontStyle.Bold;
        //     }

        //     UpdateDebugLabel();
        // }

        // public void UpdateDebugLabel()
        // {
        //     if (floatingLabel == null) return;

        //     string nameStr = rockData != null ? rockData.rockName : gameObject.name;
        //     float hardnessVal = rockData != null ? rockData.hardness : 5f;
        //     int maxHp = MaxDurability;

        //     string tierName;
        //     Color tierCol;

        //     if (hardnessVal <= 5f)
        //     {
        //         tierName = "1-5 (Very Soft)";
        //         tierCol = new Color(1f, 0.55f, 0.35f);
        //     }
        //     else if (hardnessVal <= 10f)
        //     {
        //         tierName = "5-10 (Soft/Brittle)";
        //         tierCol = new Color(1f, 0.85f, 0.35f);
        //     }
        //     else if (hardnessVal <= 15f)
        //     {
        //         tierName = "10-15 (Hard/Abrasive)";
        //         tierCol = new Color(0.4f, 0.85f, 1f);
        //     }
        //     else
        //     {
        //         tierName = "15-20 (Tool Core)";
        //         tierCol = new Color(0.4f, 1f, 0.5f);
        //     }

        //     floatingLabel.text = $"[{nameStr}]\nVal: {hardnessVal:F1} | {tierName}\nHP: {currentDurability}/{maxHp}";
        //     floatingLabel.color = tierCol;
        // }

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

        public void SetHeld(bool held, GameObject playerObj = null)
        {
            isHeld = held;
            CacheColliders();

            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                if (held)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
#else
                if (held)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
#endif
                rb.isKinematic = held;
                rb.detectCollisions = !held;
            }

            if (held)
            {
                Collider[] playerCols = playerObj != null ? playerObj.GetComponentsInChildren<Collider>() : null;

                for (int i = 0; i < rockColliders.Length; i++)
                {
                    if (rockColliders[i] == null) continue;
                    rockColliders[i].isTrigger = true; // Prevents blocking CharacterController!

                    if (playerCols != null)
                    {
                        for (int j = 0; j < playerCols.Length; j++)
                        {
                            if (playerCols[j] != null)
                            {
                                Physics.IgnoreCollision(rockColliders[i], playerCols[j], true);
                            }
                        }
                    }
                }
            }
            else
            {
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
            }
        }

        #endregion
    }
}
