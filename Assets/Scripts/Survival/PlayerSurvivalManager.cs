using System;
using System.Collections;
using UnityEngine;
using JbelIghoud.DayNight;

namespace JbelIghoud.Survival
{
    public enum DamageCause
    {
        Generic,
        Drowning,
        Starvation,
        Exhaustion,
        Fall
    }

    /// <summary>
    /// Master survival system for the player managing Health, Respiration (Oxygen),
    /// Hunger, and Sleep (Fatigue) systems in a unified, modular architecture.
    /// </summary>
    public class PlayerSurvivalManager : MonoBehaviour
    {
        private static PlayerSurvivalManager _instance;
        public static PlayerSurvivalManager Instance => _instance;

        [Header("--- Survival Stats ---")]
        [Tooltip("Player Health: Depleted by drowning, starvation, exhaustion, or hazards.")]
        [SerializeField] private SurvivalStat health = new SurvivalStat(StatType.Health, 100f, 0f, 2.5f, 25f, 10f);

        [Tooltip("Respiration / Oxygen: Depleted when diving or submerged underwater; refilled when surfacing and taking a breath.")]
        [SerializeField] private SurvivalStat respiration = new SurvivalStat(StatType.Respiration, 100f, 4.5f, 32f, 25f, 10f);

        [Tooltip("Hunger: Gradually consumed over time; accelerated by sprinting and swimming. Restored by eating.")]
        [SerializeField] private SurvivalStat hunger = new SurvivalStat(StatType.Hunger, 100f, 0.45f, 0f, 20f, 8f);

        [Tooltip("Sleep / Energy: Drained throughout the day and accelerates at night. Restored by sleeping until morning.")]
        [SerializeField] private SurvivalStat sleep = new SurvivalStat(StatType.Sleep, 100f, 0.35f, 0f, 20f, 8f);

        [Header("--- Damage & Penalties Settings ---")]
        [Tooltip("Health damage per second when out of oxygen (drowning).")]
        [SerializeField] private float drowningDamageRate = 12.0f;

        [Tooltip("Health damage per second when hunger is empty (starvation).")]
        [SerializeField] private float starvationDamageRate = 4.0f;

        [Tooltip("Health damage per second when sleep is empty (extreme exhaustion).")]
        [SerializeField] private float exhaustionDamageRate = 2.5f;

        [Tooltip("Delay in seconds after taking damage before health natural regeneration can resume.")]
        [SerializeField] private float damageRegenDelay = 4.0f;

        [Header("--- Sleep / Rest Settings ---")]
        [Tooltip("Target morning hour when waking up from sleep (e.g. 7.0 = 07:00 AM).")]
        [SerializeField] private float wakeUpHour = 7.0f;

        [Tooltip("Hunger consumed during overnight sleep.")]
        [SerializeField] private float sleepHungerCost = 15.0f;

        [Header("--- Audio References ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip inhaleBreathClip;
        [SerializeField] private AudioClip drownGaspClip;
        [SerializeField] private AudioClip eatSoundClip;
        [SerializeField] private AudioClip hurtSoundClip;
        [SerializeField] private AudioClip wakeUpChimeClip;

        // References
        private TopDownPlayerController playerController;
        private DayNightCycleManager dayNightManager;

        // Runtime Tracking
        private float lastDamageTime = -100f;
        private bool wasUnderwaterLastFrame = false;
        private bool isDead = false;
        private bool isSleeping = false;
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;

        // Events
        public event Action<float, DamageCause> OnDamageTaken;
        public event Action<float> OnHealed;
        public event Action OnTakeBreath;
        public event Action<float> OnAteFood;
        public event Action OnPlayerDied;
        public event Action OnPlayerRespawned;
        public event Action<bool> OnSleepStateChanged;

        // Public Accessors
        public SurvivalStat Health => health;
        public SurvivalStat Respiration => respiration;
        public SurvivalStat Hunger => hunger;
        public SurvivalStat Sleep => sleep;

        public bool IsDead => isDead;
        public bool IsSleeping => isSleeping;
        public bool IsDrowning => respiration.IsDepleted && playerController != null && playerController.IsUnderwater;
        public bool IsStarving => hunger.IsDepleted;
        public bool IsExhausted => sleep.IsLow;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            health.Initialize();
            respiration.Initialize();
            hunger.Initialize();
            sleep.Initialize();

            playerController = GetComponent<TopDownPlayerController>();
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<TopDownPlayerController>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.0f; // 2D clean HUD audio
                }
            }

            respawnPosition = transform.position;
            respawnRotation = transform.rotation;
        }

        private void Start()
        {
            dayNightManager = DayNightCycleManager.Instance;
            if (dayNightManager == null)
            {
                dayNightManager = FindAnyObjectByType<DayNightCycleManager>();
            }

            respawnPosition = transform.position;
            respawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (isDead || isSleeping) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            UpdateRespiration(dt);
            UpdateHunger(dt);
            UpdateSleep(dt);
            UpdateHealthRegeneration(dt);

            // Desktop Testing Hotkeys
            HandleTestingHotkeys();
        }

        private void UpdateRespiration(float dt)
        {
            bool isUnderwater = playerController != null && playerController.IsUnderwater;

            if (isUnderwater)
            {
                // Underwater / Diving: Holding breath, consume respiration
                respiration.Deplete(respiration.DepletionRate * dt);

                if (respiration.IsDepleted)
                {
                    // Drowning damage
                    TakeDamage(drowningDamageRate * dt, DamageCause.Drowning);
                    PlaySound(drownGaspClip ?? ProceduralSurvivalAudio.GetDrownGaspClip(), 0.35f, false);
                }
            }
            else
            {
                // Surfaced or on land: Breathe in!
                if (wasUnderwaterLastFrame && respiration.CurrentValue < respiration.MaxValue)
                {
                    // Immediate breath intake on surfacing
                    PlaySound(inhaleBreathClip ?? ProceduralSurvivalAudio.GetInhaleBreathClip(), 0.7f, true);
                    OnTakeBreath?.Invoke();
                }

                // Recover breath rapidly towards 100%
                if (respiration.CurrentValue < respiration.MaxValue)
                {
                    respiration.Recover(respiration.RecoveryRate * dt);
                }
            }

            wasUnderwaterLastFrame = isUnderwater;
        }

        private void UpdateHunger(float dt)
        {
            float rate = hunger.DepletionRate;

            if (playerController != null)
            {
                if (playerController.IsSwimming) rate *= 1.8f;
                else if (playerController.IsMoving) rate *= 1.35f;
            }

            hunger.Deplete(rate * dt);

            if (hunger.IsDepleted)
            {
                TakeDamage(starvationDamageRate * dt, DamageCause.Starvation);
            }
        }

        private void UpdateSleep(float dt)
        {
            float rate = sleep.DepletionRate;

            if (dayNightManager != null)
            {
                float hour = dayNightManager.CurrentTime;
                bool isNight = hour >= 19.5f || hour <= 5.5f;
                if (isNight)
                {
                    rate *= 1.6f; // Faster energy consumption at night
                }
            }

            sleep.Deplete(rate * dt);

            if (sleep.IsDepleted)
            {
                TakeDamage(exhaustionDamageRate * dt, DamageCause.Exhaustion);
            }
        }

        private void UpdateHealthRegeneration(float dt)
        {
            // Health naturally heals when well-fed and well-rested
            bool isWellFed = hunger.CurrentValue >= 70f;
            bool isRested = sleep.CurrentValue >= 35f;
            bool notRecentDamage = (Time.time - lastDamageTime) >= damageRegenDelay;
            bool notUnderwater = playerController == null || !playerController.IsUnderwater;

            if (isWellFed && isRested && notRecentDamage && notUnderwater && health.CurrentValue < health.MaxValue)
            {
                health.Recover(health.RecoveryRate * dt);
            }
        }

        public void TakeDamage(float amount, DamageCause cause = DamageCause.Generic)
        {
            if (isDead || amount <= 0f) return;

            lastDamageTime = Time.time;
            health.Deplete(amount);
            OnDamageTaken?.Invoke(amount, cause);

            if (cause != DamageCause.Drowning)
            {
                PlaySound(hurtSoundClip ?? ProceduralSurvivalAudio.GetHurtClip(), 0.45f, false);
            }

            if (health.IsDepleted && !isDead)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;
            health.Recover(amount);
            OnHealed?.Invoke(amount);
        }

        public void Eat(float foodValue = 25f)
        {
            if (isDead) return;

            hunger.Recover(foodValue);
            health.Recover(foodValue * 0.2f);
            PlaySound(eatSoundClip ?? ProceduralSurvivalAudio.GetEatClip(), 0.65f, true);
            OnAteFood?.Invoke(foodValue);
        }

        public void SleepUntilMorning(float targetMorningHour = 7.0f)
        {
            if (isDead || isSleeping) return;

            StartCoroutine(SleepRoutine(targetMorningHour));
        }

        private IEnumerator SleepRoutine(float targetMorningHour)
        {
            isSleeping = true;
            OnSleepStateChanged?.Invoke(true);

            if (playerController != null)
            {
                playerController.SetControlsLocked(true);
            }

            yield return new WaitForSeconds(1.2f);

            // Advance Day/Night Cycle to Morning
            if (dayNightManager == null) dayNightManager = DayNightCycleManager.Instance;
            if (dayNightManager != null)
            {
                dayNightManager.currentDay++;
                dayNightManager.SetTime(targetMorningHour);
            }

            // Restore Stats
            sleep.RestoreFull();
            health.RestoreFull();
            hunger.Deplete(sleepHungerCost);

            PlaySound(wakeUpChimeClip ?? ProceduralSurvivalAudio.GetWakeChimeClip(), 0.75f, true);

            yield return new WaitForSeconds(1.5f);

            isSleeping = false;
            OnSleepStateChanged?.Invoke(false);

            if (playerController != null)
            {
                playerController.SetControlsLocked(false);
            }
        }

        private void Die()
        {
            isDead = true;
            OnPlayerDied?.Invoke();

            if (playerController != null)
            {
                playerController.SetControlsLocked(true);
                if (playerController.IsSwimming)
                {
                    playerController.SetDiving(false);
                }
            }

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(2.0f);

            // Teleport to safe location
            if (playerController != null)
            {
                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = respawnPosition;
                playerController.transform.rotation = respawnRotation;
                if (cc != null) cc.enabled = true;
            }

            // Reset survival vitals to safe baseline
            health.RestoreFull();
            respiration.RestoreFull();
            hunger.Set(60f);
            sleep.Set(60f);

            isDead = false;
            OnPlayerRespawned?.Invoke();

            if (playerController != null)
            {
                playerController.SetControlsLocked(false);
            }
        }

        public void SetSafeRespawnPoint(Vector3 pos, Quaternion rot)
        {
            respawnPosition = pos;
            respawnRotation = rot;
        }

        private void PlaySound(AudioClip clip, float volume, bool forceRestart)
        {
            if (audioSource == null || clip == null) return;
            if (!forceRestart && audioSource.isPlaying) return;

            audioSource.PlayOneShot(clip, volume);
        }

        private void HandleTestingHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame)
            {
                Eat(25f);
            }
            if (kb.kKey.wasPressedThisFrame)
            {
                SleepUntilMorning(wakeUpHour);
            }
            if (kb.jKey.wasPressedThisFrame)
            {
                TakeDamage(20f, DamageCause.Generic);
            }
#endif
        }
    }
}
