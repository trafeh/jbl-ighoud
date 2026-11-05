using UnityEngine;

public class SuimonoWaterVFX : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private TopDownPlayerController playerController;

    [Header("Suimono Swimming VFX (Trailing Behind Swimmer)")]
    [Tooltip("32-frame animated fluid splash flipbook from Suimono Water System 2.")]
    [SerializeField] private ParticleSystem swimSplash;

    [Tooltip("8-frame animated fluid wake flipbook from Suimono Water System 2.")]
    [SerializeField] private ParticleSystem swimWake;

    [Tooltip("High-velocity water drops kicked backwards by swimming strokes.")]
    [SerializeField] private ParticleSystem swimDrops;

    [Tooltip("Subsurface water bubbles churning behind the swimmer.")]
    [SerializeField] private ParticleSystem swimBubbles;

    [Header("Suimono Wading VFX (Walking in Shallow Sea / Water)")]
    [Tooltip("Small 32-frame fluid splash flipbook at the feet while walking in water.")]
    [SerializeField] private ParticleSystem wadingSplash;

    [Tooltip("Foaming water ripples expanding around footsteps.")]
    [SerializeField] private ParticleSystem wadingRipples;

    [Tooltip("Water droplets spraying up from walking through the sea.")]
    [SerializeField] private ParticleSystem wadingDrops;

    [Header("Placement Settings")]
    [Tooltip("Distance behind the swimmer (in meters) where the wake and kicks emit.")]
    [SerializeField] private float swimTrailDistance = 0.90f;

    [Tooltip("Vertical offset above water surface to avoid z-fighting with water mesh.")]
    [SerializeField] private float surfaceOffset = 0.02f;

    [Header("Audio (Suimono Sound Effects)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] splashClips;
    [SerializeField] private AudioClip bigSplashClip;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.45f;

    private bool wasInWaterLastFrame = false;
    private bool wasSwimmingLastFrame = false;
    private float soundStepTimer = 0f;

    public ParticleSystem SwimSplash => swimSplash;
    public ParticleSystem SwimWake => swimWake;
    public ParticleSystem SwimDrops => swimDrops;
    public ParticleSystem SwimBubbles => swimBubbles;
    public ParticleSystem WadingSplash => wadingSplash;
    public ParticleSystem WadingRipples => wadingRipples;
    public ParticleSystem WadingDrops => wadingDrops;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<TopDownPlayerController>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 25f;
            }
        }

        StopAllEmissions();
    }

    public void SetReferences(
        TopDownPlayerController controller,
        ParticleSystem sSplash,
        ParticleSystem sWake,
        ParticleSystem sDrops,
        ParticleSystem sBubbles,
        ParticleSystem wSplash,
        ParticleSystem wRipples,
        ParticleSystem wDrops,
        AudioClip[] splashes,
        AudioClip bigSplash)
    {
        playerController = controller;
        swimSplash = sSplash;
        swimWake = sWake;
        swimDrops = sDrops;
        swimBubbles = sBubbles;
        wadingSplash = wSplash;
        wadingRipples = wRipples;
        wadingDrops = wDrops;
        splashClips = splashes;
        bigSplashClip = bigSplash;

        StopAllEmissions();
    }

    private void StopAllEmissions()
    {
        DisableEmission(swimSplash);
        DisableEmission(swimWake);
        DisableEmission(swimDrops);
        DisableEmission(swimBubbles);
        DisableEmission(wadingSplash);
        DisableEmission(wadingRipples);
        DisableEmission(wadingDrops);
    }

    private void LateUpdate()
    {
        if (playerController == null) return;

        bool isInWater = playerController.IsInWater;
        bool isSwimming = playerController.IsSwimming;
        bool isMoving = playerController.IsMoving;
        float surfaceY = playerController.WaterSurfaceY + surfaceOffset;

        // Plunge splash check (falling/jumping into water)
        if (isInWater && !wasInWaterLastFrame)
        {
            TriggerPlungeEffect();
        }

        wasInWaterLastFrame = isInWater;
        wasSwimmingLastFrame = isSwimming;

        if (isSwimming)
        {
            bool isDiving = playerController.IsDiving;

            // SWIMMING / DIVING: Emitter trails behind the swimmer at surface or underwater
            Vector3 backDir = -playerController.transform.forward;
            Vector3 targetPos = playerController.transform.position + backDir * swimTrailDistance;
            targetPos.y = isDiving ? (playerController.transform.position.y + 0.8f) : surfaceY;

            transform.position = targetPos;
            transform.rotation = playerController.transform.rotation;

            // Turn off wading emitters
            DisableEmission(wadingSplash);
            DisableEmission(wadingRipples);
            DisableEmission(wadingDrops);

            if (isDiving)
            {
                // Underwater diving: suppress surface wake, emit bubbling subsurface stream
                DisableEmission(swimSplash);
                DisableEmission(swimWake);
                DisableEmission(swimDrops);
                SetEmission(swimBubbles, true, isMoving ? 18f : 8f);
            }
            else if (isMoving)
            {
                // Active swimming: 32-frame fluid splash, 8-frame wake, kicking droplets, bubbles
                SetEmission(swimSplash, true, 7f);
                SetEmission(swimWake, true, 5f);
                SetEmission(swimDrops, true, 16f);
                SetEmission(swimBubbles, true, 12f);

                // Periodic swimming stroke audio
                soundStepTimer += Time.deltaTime;
                if (soundStepTimer >= 0.75f)
                {
                    soundStepTimer = 0f;
                    PlaySplashSound(0.35f);
                }
            }
            else
            {
                // Treading water: only gentle surface ripples
                soundStepTimer = 0f;
                DisableEmission(swimSplash);
                DisableEmission(swimDrops);
                DisableEmission(swimBubbles);
                SetEmission(swimWake, true, 1.8f);
            }
        }
        else if (isInWater)
        {
            // WADING (Walking in water / sea):
            // Emitter placed at the feet at water surface level
            Vector3 footPos = playerController.transform.position + playerController.transform.forward * 0.1f;
            footPos.y = surfaceY;

            transform.position = footPos;
            transform.rotation = playerController.transform.rotation;

            // Turn off swimming emitters
            DisableEmission(swimSplash);
            DisableEmission(swimWake);
            DisableEmission(swimDrops);
            DisableEmission(swimBubbles);

            if (isMoving)
            {
                // Walking through sea: upward 32-frame footstep splashes, foaming ripples, droplets
                SetEmission(wadingSplash, true, 5f);
                SetEmission(wadingRipples, true, 4f);
                SetEmission(wadingDrops, true, 14f);

                // Footstep wading splash sound
                soundStepTimer += Time.deltaTime;
                if (soundStepTimer >= 0.55f)
                {
                    soundStepTimer = 0f;
                    PlaySplashSound(0.28f);
                }
            }
            else
            {
                soundStepTimer = 0f;
                DisableEmission(wadingSplash);
                DisableEmission(wadingDrops);
                SetEmission(wadingRipples, true, 1.2f);
            }
        }
        else
        {
            // DRY LAND: all water emissions disabled
            soundStepTimer = 0f;
            DisableEmission(swimSplash);
            DisableEmission(swimWake);
            DisableEmission(swimDrops);
            DisableEmission(swimBubbles);
            DisableEmission(wadingSplash);
            DisableEmission(wadingRipples);
            DisableEmission(wadingDrops);
        }
    }

    private void TriggerPlungeEffect()
    {
        if (swimSplash != null) swimSplash.Emit(2);
        if (swimDrops != null) swimDrops.Emit(12);
        if (swimWake != null) swimWake.Emit(1);

        if (bigSplashClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(bigSplashClip, soundVolume * 0.9f);
        }
        else
        {
            PlaySplashSound(0.5f);
        }
    }

    private void PlaySplashSound(float volumeScale)
    {
        if (splashClips != null && splashClips.Length > 0 && audioSource != null)
        {
            var clip = splashClips[Random.Range(0, splashClips.Length)];
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.15f);
                audioSource.PlayOneShot(clip, soundVolume * volumeScale);
            }
        }
    }

    private void SetEmission(ParticleSystem ps, bool enabled, float rate)
    {
        if (ps == null) return;
        if (!ps.isPlaying && enabled) ps.Play();
        var em = ps.emission;
        em.enabled = enabled;
        em.rateOverTime = rate;
    }

    private void DisableEmission(ParticleSystem ps)
    {
        if (ps == null) return;
        var em = ps.emission;
        if (em.enabled)
        {
            em.enabled = false;
        }
    }
}
