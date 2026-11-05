using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Handles the progressive degradation and destruction of big rocks (such as rock_lp_x boulders).
/// Players can click/tap directly on the rock to degrade it hit-by-hit.
/// Each hit produces satisfying punch-scale recoil, stone dust puffs, flying chips, and audio feedback.
/// When degraded completely, the big rock dramatically crashes and shatters, spawning a shower
/// of collectible, physics-driven small rocks (rock_lp_x) that tumble down and can be inspected
/// in full 3D via the archaeological inspection system.
/// </summary>
[DisallowMultipleComponent]
public class DestructibleBigRock : MonoBehaviour
{
    [Header("Degradation & Hits")]
    [Tooltip("Number of clicks/hits required to completely destroy and shatter the big rock.")]
    [SerializeField] private int maxHits = 4;

    [Tooltip("Current hits remaining before crashing.")]
    [SerializeField] private int currentHits = 4;

    [Tooltip("Allow direct click / tap on the rock in game view to degrade it.")]
    [SerializeField] private bool canClickToDamage = true;

    [Tooltip("Maximum distance from camera to rock for click interaction.")]
    [SerializeField] private float maxClickDistance = 45f;

    [Tooltip("Require player character to be within interaction radius.")]
    [SerializeField] private bool requirePlayerInRange = false;

    [Tooltip("Maximum distance from player to rock if requirePlayerInRange is true.")]
    [SerializeField] private float maxPlayerDistance = 10f;

    [Header("Small Rocks Yield (Inspectable)")]
    [Tooltip("Minimum number of small rocks spawned on crash.")]
    [SerializeField] private int minSmallRocks = 5;

    [Tooltip("Maximum number of small rocks spawned on crash.")]
    [SerializeField] private int maxSmallRocks = 8;

    [Tooltip("Meshes used for the spawned small rocks. If empty, automatically populates with rock_lp_x meshes.")]
    [SerializeField] private Mesh[] smallRockMeshes;

    [Tooltip("Material used for the spawned small rocks. If null, inherits this big rock's material.")]
    [SerializeField] private Material smallRockMaterial;

    [Tooltip("Palm/collectible scale for spawned small rocks (rock_lp meshes bounds ~0.02m, so 13-17 gives ~0.25m rocks).")]
    [SerializeField] private float smallRockScale = 14f;

    [Tooltip("Outward explosive scatter velocity applied to small rocks on crash.")]
    [SerializeField] private float scatterForce = 3.6f;

    [Tooltip("Upward lift velocity applied to small rocks on crash.")]
    [SerializeField] private float upwardScatterForce = 3.2f;

    [Tooltip("Prehistoric lithic rock data applied to spawned small rocks. If empty, automatically loads Jbel Ighoud rock data.")]
    [SerializeField] private JbelIghoud.Lithic.RockData[] possibleRockData;

    [Header("Visual Feedback (Degradation)")]
    [Tooltip("Elastic squash/stretch punch magnitude on hit (0.05 to 0.15).")]
    [SerializeField] private float punchScaleAmount = 0.08f;

    [Tooltip("Duration of the hit recoil wobble animation.")]
    [SerializeField] private float punchDuration = 0.22f;

    [Tooltip("Color of dust residue and fracture particles.")]
    [SerializeField] private Color dustColor = new Color(0.72f, 0.68f, 0.60f, 0.9f);

    [Tooltip("Show 3D floating degradation health status above the rock.")]
    [SerializeField] private bool showFloatingStatus = true;

    [Header("Audio Feedback")]
    [Tooltip("Custom hit audio clip. If null, a realistic stone strike tone is procedurally synthesized.")]
    [SerializeField] private AudioClip hitAudioClip;

    [Tooltip("Custom crash audio clip. If null, a heavy rock shatter tone is procedurally synthesized.")]
    [SerializeField] private AudioClip crashAudioClip;

    // Runtime state
    private Vector3 originalLocalScale;
    private Coroutine punchCoroutine;
    private TextMesh statusTextMesh;
    private Renderer rockRenderer;
    private AudioSource audioSource;
    private bool isDestroyed = false;

    public int HitsRemaining => currentHits;
    public int MaxHits => maxHits;
    public float HealthFraction => Mathf.Clamp01((float)currentHits / Mathf.Max(1, maxHits));
    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        originalLocalScale = transform.localScale;
        currentHits = Mathf.Clamp(currentHits, 1, maxHits);

        rockRenderer = GetComponent<Renderer>();
        if (rockRenderer == null) rockRenderer = GetComponentInChildren<Renderer>();

        // Ensure audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.85f;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 30f;
        }

        // Ensure collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        InitializeMeshAndDataPool();
        CleanUpLegacyLabel();

#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    private void Reset()
    {
        InitializeMeshAndDataPool();
    }

    private void OnValidate()
    {
        if (originalLocalScale == Vector3.zero && transform.localScale != Vector3.zero)
        {
            originalLocalScale = transform.localScale;
        }
        InitializeMeshAndDataPool();
        UpdateStatusLabel();
    }

    private void Start()
    {
        UpdateStatusLabel();
    }

    private void Update()
    {
        if (isDestroyed || !canClickToDamage) return;

        // Skip input while Resident Evil 3D inspection studio is active
        if (RockInspectionManager.Instance != null && RockInspectionManager.Instance.IsInspecting)
            return;

        // Skip input while Smash drag mode is active
        if (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode)
            return;

        CheckClickInput();
    }

    private void CheckClickInput()
    {
        bool pointerDown = false;
        Vector2 pointerPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerDown = true;
            pointerPos = Mouse.current.position.ReadValue();
        }
        else if (Touch.activeTouches.Count > 0)
        {
            var touch = Touch.activeTouches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                pointerDown = true;
                pointerPos = touch.screenPosition;
            }
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (!pointerDown && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerDown = true;
            pointerPos = Mouse.current.position.ReadValue();
        }
#elif !ENABLE_INPUT_SYSTEM
        if (!pointerDown && Input.GetMouseButtonDown(0))
        {
            pointerDown = true;
            pointerPos = Input.mousePosition;
        }
#endif

        if (!pointerDown) return;

        // Ignore clicks on UI elements
        if (EventSystem.current != null)
        {
#if ENABLE_INPUT_SYSTEM
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (Touch.activeTouches.Count > 0 && EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[0].touchId)) return;
#else
            if (EventSystem.current.IsPointerOverGameObject()) return;
#endif
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(pointerPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            // Verify if this rock or any of its child colliders was clicked
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
            {
                // Range check to player if required
                if (requirePlayerInRange)
                {
                    var player = FindAnyObjectByType<TopDownPlayerController>();
                    if (player != null && Vector3.Distance(player.transform.position, transform.position) > maxPlayerDistance)
                    {
                        return;
                    }
                }

                DamageRock(1, hit.point, hit.normal);
            }
        }
    }

    /// <summary>
    /// Delivers damage to the rock, triggering progressive degradation and physical feedback.
    /// </summary>
    public void DamageRock(int damage = 1, Vector3 hitPoint = default, Vector3 hitNormal = default)
    {
        if (isDestroyed) return;

        if (originalLocalScale == Vector3.zero) originalLocalScale = transform.localScale;
        if (hitPoint == default) hitPoint = transform.position + Vector3.up * 0.5f;
        if (hitNormal == default) hitNormal = Vector3.up;

        currentHits = Mathf.Max(0, currentHits - damage);

        // Visual and audio degradation feedback
        if (Application.isPlaying)
        {
            TriggerHitPunch(hitNormal);
            SpawnHitParticles(hitPoint, hitNormal);
            PlayHitAudio();
        }
        UpdateStatusLabel();

        // Spawn a micro-flake chip during degradation if nearing fracture
        if (currentHits == 1 && maxHits >= 3 && Application.isPlaying)
        {
            SpawnSingleFragment(hitPoint + hitNormal * 0.2f, false);
        }

        if (currentHits <= 0)
        {
            CrashAndShatter(hitPoint);
        }
    }

    private void TriggerHitPunch(Vector3 hitNormal)
    {
        if (!Application.isPlaying) return;
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(PunchScaleRoutine(hitNormal));
    }

    private IEnumerator PunchScaleRoutine(Vector3 hitNormal)
    {
        float elapsed = 0f;
        Vector3 targetSquash = originalLocalScale * (1f - punchScaleAmount);
        Vector3 targetStretch = originalLocalScale * (1f + punchScaleAmount * 0.5f);

        // Quick punch inward
        float halfTime = punchDuration * 0.4f;
        while (elapsed < halfTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfTime;
            transform.localScale = Vector3.Lerp(originalLocalScale, targetSquash, t);
            yield return null;
        }

        // Rebound overshoot
        elapsed = 0f;
        float reboundTime = punchDuration * 0.3f;
        while (elapsed < reboundTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / reboundTime;
            transform.localScale = Vector3.Lerp(targetSquash, targetStretch, t);
            yield return null;
        }

        // Settle back to original
        elapsed = 0f;
        float settleTime = punchDuration * 0.3f;
        while (elapsed < settleTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleTime;
            transform.localScale = Vector3.Lerp(targetStretch, originalLocalScale, t);
            yield return null;
        }

        transform.localScale = originalLocalScale;
        punchCoroutine = null;
    }

    private void SpawnHitParticles(Vector3 pos, Vector3 normal)
    {
        if (!Application.isPlaying) return;

        // 1. Expanding stone dust puff
        GameObject dustPuff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dustPuff.name = "RockDustPuff";
        dustPuff.transform.position = pos + normal * 0.15f;
        dustPuff.transform.localScale = Vector3.one * 0.45f;
        Collider puffCol = dustPuff.GetComponent<Collider>();
        if (puffCol != null) Destroy(puffCol);

        Renderer puffRend = dustPuff.GetComponent<Renderer>();
        if (puffRend != null)
        {
            puffRend.material.color = dustColor;
        }
        StartCoroutine(FadeAndExpandDust(dustPuff, 0.4f, 1.4f));

        // 2. Chipping debris flakes
        int chipCount = Random.Range(3, 6);
        for (int i = 0; i < chipCount; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "StoneChip";
            chip.transform.position = pos + Random.insideUnitSphere * 0.2f;
            chip.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

            Collider col = chip.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer cr = chip.GetComponent<Renderer>();
            if (cr != null) cr.material.color = Color.Lerp(dustColor, Color.gray, Random.value);

            Rigidbody rb = chip.AddComponent<Rigidbody>();
            Vector3 scatterDir = (normal + Random.insideUnitSphere * 0.7f).normalized;
            rb.linearVelocity = scatterDir * Random.Range(2.5f, 5.5f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            Destroy(chip, Random.Range(0.8f, 1.4f));
        }
    }

    private IEnumerator FadeAndExpandDust(GameObject dustObj, float duration, float scaleTarget)
    {
        if (dustObj == null) yield break;
        float elapsed = 0f;
        Vector3 initialScale = dustObj.transform.localScale;
        Vector3 endScale = initialScale * scaleTarget;
        Renderer rend = dustObj.GetComponent<Renderer>();
        Color startCol = rend != null ? rend.material.color : dustColor;

        while (elapsed < duration)
        {
            if (dustObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            dustObj.transform.localScale = Vector3.Lerp(initialScale, endScale, t);
            if (rend != null)
            {
                Color c = startCol;
                c.a = Mathf.Lerp(startCol.a, 0f, t);
                rend.material.color = c;
            }
            yield return null;
        }

        if (dustObj != null) Destroy(dustObj);
    }

    /// <summary>
    /// Executes the final crash: spawns grand explosion dust, flying debris, and yields
    /// multiple physics-driven small rocks (rock_lp_x) that tumble down and can be inspected.
    /// </summary>
    public void CrashAndShatter(Vector3 impactPoint = default)
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (impactPoint == default) impactPoint = transform.position;

        if (Application.isPlaying)
        {
            PlayCrashAudio();
            SpawnCrashExplosion(impactPoint);
        }

        // Spawn the inspectable small rocks down onto the ground!
        SpawnSmallRocksYield();

        // Notify save system if active
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.NotifyRockDestroyed(gameObject.name);
        }

        // Clean up status label
        if (statusTextMesh != null)
        {
            if (Application.isPlaying) Destroy(statusTextMesh.gameObject);
            else DestroyImmediate(statusTextMesh.gameObject);
        }

        // Destroy big rock
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void SpawnCrashExplosion(Vector3 center)
    {
        if (!Application.isPlaying) return;

        // Billowing dust cloud spheres
        int dustSpheres = 8;
        for (int i = 0; i < dustSpheres; i++)
        {
            GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dust.name = "GrandDustCloud";
            Vector3 offset = Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f;
            dust.transform.position = center + offset;
            dust.transform.localScale = Vector3.one * Random.Range(0.6f, 1.2f);

            Collider col = dust.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = dust.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = Color.Lerp(dustColor, new Color(0.85f, 0.82f, 0.74f, 0.8f), Random.value);
            }

            StartCoroutine(FadeAndExpandDust(dust, Random.Range(0.8f, 1.5f), Random.Range(2.2f, 3.5f)));
        }

        // Large structural fracture chunks
        int chunkCount = 10;
        for (int i = 0; i < chunkCount; i++)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = "FractureChunk";
            chunk.transform.position = center + Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f;
            chunk.transform.localScale = Vector3.one * Random.Range(0.18f, 0.38f);

            Collider col = chunk.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer cr = chunk.GetComponent<Renderer>();
            if (cr != null) cr.material.color = Color.Lerp(dustColor, Color.gray, Random.value);

            Rigidbody rb = chunk.AddComponent<Rigidbody>();
            Vector3 burstDir = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
            rb.linearVelocity = burstDir * Random.Range(3.5f, 7.5f);
            rb.angularVelocity = Random.insideUnitSphere * 15f;

            Destroy(chunk, Random.Range(1.2f, 2.0f));
        }
    }

    private void SpawnSmallRocksYield()
    {
        int count = Random.Range(minSmallRocks, maxSmallRocks + 1);
        Vector3 basePosition = transform.position + Vector3.up * 0.6f;

        // Container to keep hierarchy clean
        GameObject yieldContainer = GameObject.Find("Spawned_Prehistoric_Rocks");
        if (yieldContainer == null)
        {
            yieldContainer = new GameObject("Spawned_Prehistoric_Rocks");
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = basePosition + (Random.insideUnitSphere * 0.65f) + (Vector3.up * Random.Range(0.2f, 0.7f));
            SpawnSingleFragment(spawnPos, true, yieldContainer.transform);
        }
    }

    private GameObject SpawnSingleFragment(Vector3 position, bool giveExplosiveScatter = true, Transform parent = null)
    {
        // Select mesh from pool (rock_lp_x)
        Mesh chosenMesh = GetRandomSmallRockMesh();
        string meshName = chosenMesh != null ? chosenMesh.name : "rock_lp";

        GameObject smallRock = new GameObject($"Rock_{meshName}_{Random.Range(100, 999)}");
        smallRock.transform.position = position;
        smallRock.transform.rotation = Random.rotation;
        smallRock.transform.localScale = Vector3.one * (smallRockScale * Random.Range(0.85f, 1.25f));

        if (parent != null) smallRock.transform.SetParent(parent, true);

        // MeshFilter & MeshRenderer
        MeshFilter mf = smallRock.AddComponent<MeshFilter>();
        if (chosenMesh != null) mf.sharedMesh = chosenMesh;

        MeshRenderer mr = smallRock.AddComponent<MeshRenderer>();
        if (smallRockMaterial != null)
        {
            mr.sharedMaterial = smallRockMaterial;
        }
        else if (rockRenderer != null && rockRenderer.sharedMaterial != null)
        {
            mr.sharedMaterial = rockRenderer.sharedMaterial;
        }

        // Solid physical collider
        MeshCollider mc = smallRock.AddComponent<MeshCollider>();
        if (chosenMesh != null) mc.sharedMesh = chosenMesh;
        mc.convex = true;

        // Rigidbody physics
        Rigidbody rb = smallRock.AddComponent<Rigidbody>();
        rb.mass = Random.Range(1.2f, 2.8f);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (giveExplosiveScatter && Application.isPlaying)
        {
            Vector3 scatterDir = (Random.insideUnitSphere.normalized + Vector3.up * 0.85f).normalized;
            rb.linearVelocity = (scatterDir * Random.Range(scatterForce * 0.7f, scatterForce * 1.3f)) + (Vector3.up * upwardScatterForce);
            rb.angularVelocity = Random.insideUnitSphere * Random.Range(4f, 12f);
        }

        // RockItem archaeological setup
        RockItem rockItem = smallRock.AddComponent<RockItem>();
        rockItem.strength = Random.Range(6.0f, 18.0f);

        // Assign authentic prehistoric Lithic RockData (cloned so the mesh remains our rock_lp_x!)
        JbelIghoud.Lithic.RockData baseData = GetRandomRockData();
        if (baseData != null)
        {
            var clonedData = ScriptableObject.Instantiate(baseData);
            clonedData.rockMesh = chosenMesh;
            clonedData.rockMaterial = mr.sharedMaterial;
            clonedData.rockName = $"{clonedData.rockName} (Fragment)";
            rockItem.rockData = clonedData;
        }

        rockItem.ApplyRockData();
        rockItem.SetupDurability();
        rockItem.CacheColliders();

        return smallRock;
    }

    private Mesh GetRandomSmallRockMesh()
    {
        if (smallRockMeshes == null || smallRockMeshes.Length == 0)
        {
            InitializeMeshAndDataPool();
        }

        if (smallRockMeshes != null && smallRockMeshes.Length > 0)
        {
            return smallRockMeshes[Random.Range(0, smallRockMeshes.Length)];
        }

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;

        return null;
    }

    private JbelIghoud.Lithic.RockData GetRandomRockData()
    {
        if (possibleRockData == null || possibleRockData.Length == 0)
        {
            InitializeMeshAndDataPool();
        }

        if (possibleRockData != null && possibleRockData.Length > 0)
        {
            return possibleRockData[Random.Range(0, possibleRockData.Length)];
        }
        return null;
    }

    public void InitializeMeshAndDataPool()
    {
        // Auto-discover rock_lp meshes if not assigned
        if (smallRockMeshes == null || smallRockMeshes.Length == 0)
        {
            smallRockMeshes = LoadAllRockLpMeshes();
        }

        // Auto-discover rock material if not assigned
        if (smallRockMaterial == null)
        {
            if (rockRenderer != null && rockRenderer.sharedMaterial != null)
            {
                smallRockMaterial = rockRenderer.sharedMaterial;
            }
#if UNITY_EDITOR
            else
            {
                smallRockMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Envirement/landscape/RockSet2.mat");
            }
#endif
        }

        // Auto-discover prehistoric Lithic RockData assets
        if (possibleRockData == null || possibleRockData.Length == 0)
        {
            var loadedList = new List<JbelIghoud.Lithic.RockData>();
            var s = Resources.Load<JbelIghoud.Lithic.RockData>("Rocks/Silex_Flint");
            if (s != null) loadedList.Add(s);
            var g = Resources.Load<JbelIghoud.Lithic.RockData>("Rocks/Gres_Sandstone");
            if (g != null) loadedList.Add(g);
            var c = Resources.Load<JbelIghoud.Lithic.RockData>("Rocks/Calcaire_Limestone");
            if (c != null) loadedList.Add(c);

#if UNITY_EDITOR
            if (loadedList.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RockData");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var rd = UnityEditor.AssetDatabase.LoadAssetAtPath<JbelIghoud.Lithic.RockData>(path);
                    if (rd != null) loadedList.Add(rd);
                }
            }
#endif
            possibleRockData = loadedList.ToArray();
        }
    }

    public static Mesh[] LoadAllRockLpMeshes()
    {
        List<Mesh> meshes = new List<Mesh>();
#if UNITY_EDITOR
        string path = "Assets/Envirement/700m_Mountain_River_Lake_Cave_Mobile.fbx";
        Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object a in allAssets)
        {
            if (a is Mesh m)
            {
                string ln = m.name.ToLower();
                if (ln.Contains("rock") || ln.Contains("fragment") || ln.Contains("pebble"))
                {
                    meshes.Add(m);
                }
            }
        }
#endif
        return meshes.ToArray();
    }

    private void CleanUpLegacyLabel()
    {
        Transform existing = transform.Find("BigRockStatusLabel");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    private void CreateFloatingStatusLabel()
    {
        CleanUpLegacyLabel();
    }

    public void UpdateStatusLabel()
    {
        CleanUpLegacyLabel();
    }

    private void LateUpdate()
    {
    }

    // Support physical smash collision if struck by another held rock
    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        RockItem otherRock = collision.collider.GetComponentInParent<RockItem>();
        if (otherRock != null && collision.relativeVelocity.magnitude > 2.0f)
        {
            ContactPoint contact = collision.contacts[0];
            DamageRock(1, contact.point, contact.normal);
        }
    }

    private void PlayHitAudio()
    {
        if (audioSource == null) return;

        if (hitAudioClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.15f);
            audioSource.PlayOneShot(hitAudioClip, 0.85f);
        }
        else
        {
            AudioClip procClip = CreateProceduralStoneHitClip();
            audioSource.pitch = Random.Range(0.92f, 1.12f);
            audioSource.PlayOneShot(procClip, 0.9f);
        }
    }

    private void PlayCrashAudio()
    {
        if (crashAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(crashAudioClip, transform.position, 1.0f);
        }
        else
        {
            AudioClip procCrash = CreateProceduralCrashClip();
            AudioSource.PlayClipAtPoint(procCrash, transform.position, 1.0f);
        }
    }

    private static AudioClip _cachedHitClip;
    private static AudioClip _cachedCrashClip;

    private static AudioClip CreateProceduralStoneHitClip()
    {
        if (_cachedHitClip != null) return _cachedHitClip;

        int sampleRate = 22050;
        int length = (int)(sampleRate * 0.18f); // 180ms
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 32f);

            // High impact transient + resonant rock body frequency (~950Hz and ~1400Hz)
            float freq1 = Mathf.Sin(2f * Mathf.PI * 950f * t);
            float freq2 = Mathf.Sin(2f * Mathf.PI * 1420f * t);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 70f);

            samples[i] = (freq1 * 0.4f + freq2 * 0.3f + noise * 0.3f) * envelope;
        }

        _cachedHitClip = AudioClip.Create("ProceduralStoneHit", length, 1, sampleRate, false);
        _cachedHitClip.SetData(samples, 0);
        return _cachedHitClip;
    }

    private static AudioClip CreateProceduralCrashClip()
    {
        if (_cachedCrashClip != null) return _cachedCrashClip;

        int sampleRate = 22050;
        int length = (int)(sampleRate * 0.65f); // 650ms
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 6.5f);

            // Heavy low-frequency rumble (80-140Hz) + shattering snaps
            float sub = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.45f;
            float crunch = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f;
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 12f) * 0.3f;

            samples[i] = (sub + crunch + noise) * envelope;
        }

        _cachedCrashClip = AudioClip.Create("ProceduralRockCrash", length, 1, sampleRate, false);
        _cachedCrashClip.SetData(samples, 0);
        return _cachedCrashClip;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Damage (1 Hit)")]
    private void ContextMenuDamage()
    {
        DamageRock(1);
    }

    [ContextMenu("Test Crash Now")]
    private void ContextMenuCrash()
    {
        CrashAndShatter();
    }
#endif
}
