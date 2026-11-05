using UnityEngine;

namespace JbelIghoud.DayNight
{
    /// <summary>
    /// Controls the dynamic atmosphere and fill lighting inside the cave,
    /// synchronized seamlessly with DayNightCycleManager and cave depth.
    /// </summary>
    [ExecuteAlways]
    public class CaveLightingController : MonoBehaviour
    {
        public static CaveLightingController Instance { get; private set; }

        [Header("--- Cave Fill Lights ---")]
        [Tooltip("Primary soft fill light illuminating the cave interior.")]
        public Light caveFillLight;

        [Tooltip("Secondary soft light for deep cavern features (forge, crystal niche).")]
        public Light caveDeepLight;

        [Header("--- Intensity Settings ---")]
        [Tooltip("Fill light intensity during daytime (06:00 - 18:30) so rocky walls remain crisp.")]
        [Range(0.1f, 2f)]
        public float dayIntensity = 0.65f;

        [Tooltip("Fill light intensity during nighttime (18:30 - 06:00) so the cave feels moody and dark.")]
        [Range(0f, 0.5f)]
        public float nightIntensity = 0.08f;

        [Tooltip("Speed of light intensity and color smoothing.")]
        [Range(0.5f, 10f)]
        public float transitionSpeed = 3.0f;

        [Header("--- Color Synchronization ---")]
        [Tooltip("Tint multiplier applied to the sky/sun color to give cave stone natural warmth.")]
        public Color caveColorMultiplier = new Color(1.1f, 0.90f, 0.85f, 1.0f);

        [Tooltip("Deep nocturnal slate/indigo tint used for night cave fill.")]
        public Color nightCaveColor = new Color(0.20f, 0.25f, 0.40f, 1.0f);

        [Header("--- Player Spatial Integration ---")]
        [Tooltip("Current normalized player depth inside the cave (0.0 = fully outside, 1.0 = fully inside).")]
        [Range(0f, 1f)]
        public float playerCaveDepth = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this && Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            UpdateLighting(true);
        }

        private void Update()
        {
            UpdateLighting(false);
        }

        public void SetPlayerCaveDepth(float depth)
        {
            playerCaveDepth = Mathf.Clamp01(depth);
        }

        public void UpdateLighting(bool immediate)
        {
            var dayNight = DayNightCycleManager.Instance;
            if (dayNight == null) return;

            float time = dayNight.CurrentTime;

            // Smooth twilight transition factor between night (0.0) and day (1.0)
            float dayFactor;
            if (time >= 7.0f && time <= 17.5f)
            {
                dayFactor = 1.0f;
            }
            else if (time >= 19.0f || time <= 5.5f)
            {
                dayFactor = 0.0f;
            }
            else if (time > 17.5f && time < 19.0f)
            {
                dayFactor = Mathf.SmoothStep(1f, 0f, (time - 17.5f) / 1.5f);
            }
            else // 5.5 to 7.0
            {
                dayFactor = Mathf.SmoothStep(0f, 1f, (time - 5.5f) / 1.5f);
            }

            // 1. Calculate Target Intensity based on time of day
            float dayProgress = Mathf.Clamp01((time - 5.5f) / 13.0f);
            float sunCurve = Mathf.Sin(dayProgress * Mathf.PI);
            float activeDayIntensity = Mathf.Lerp(dayIntensity * 0.85f, dayIntensity, sunCurve);
            float targetIntensity = Mathf.Lerp(nightIntensity, activeDayIntensity, dayFactor);

            // 2. Synchronize Color with Day/Night sky and sun
            Color sunCol = dayNight.sunLight != null ? dayNight.sunLight.color : Color.white;
            Color dayCaveCol = new Color(
                sunCol.r * caveColorMultiplier.r,
                sunCol.g * caveColorMultiplier.g,
                sunCol.b * caveColorMultiplier.b,
                1.0f
            );
            Color targetColor = Color.Lerp(nightCaveColor, dayCaveCol, dayFactor);

            // 3. Apply to Primary Cave Fill Light
            if (caveFillLight != null)
            {
                if (immediate || !Application.isPlaying)
                {
                    caveFillLight.intensity = targetIntensity;
                    caveFillLight.color = targetColor;
                }
                else
                {
                    caveFillLight.intensity = Mathf.Lerp(caveFillLight.intensity, targetIntensity, Time.deltaTime * transitionSpeed);
                    caveFillLight.color = Color.Lerp(caveFillLight.color, targetColor, Time.deltaTime * transitionSpeed);
                }
            }

            // 4. Apply to Deep Cavern Light
            if (caveDeepLight != null)
            {
                float deepTargetIntensity = targetIntensity * 0.75f;
                if (immediate || !Application.isPlaying)
                {
                    caveDeepLight.intensity = deepTargetIntensity;
                    caveDeepLight.color = targetColor;
                }
                else
                {
                    caveDeepLight.intensity = Mathf.Lerp(caveDeepLight.intensity, deepTargetIntensity, Time.deltaTime * transitionSpeed);
                    caveDeepLight.color = Color.Lerp(caveDeepLight.color, targetColor, Time.deltaTime * transitionSpeed);
                }
            }
        }
    }
}
