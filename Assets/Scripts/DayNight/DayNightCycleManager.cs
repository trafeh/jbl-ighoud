using System;
using System.Collections.Generic;
using UnityEngine;

namespace JbelIghoud.DayNight
{
    [ExecuteAlways]
    public class DayNightCycleManager : MonoBehaviour
    {
        private static DayNightCycleManager _instance;
        public static DayNightCycleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<DayNightCycleManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("--- Time Settings ---")]
        [Tooltip("Current time of day in 24-hour decimal format (e.g. 7.0 = 07:00 AM). Starts at 7:00 AM.")]
        [Range(0f, 24f)]
        [SerializeField] private float currentTime = 7.0f;

        [Tooltip("Total real-time minutes for one complete 24-hour in-game day (e.g. 5 minutes = 300 seconds).")]
        [Min(0.1f)]
        public float dayDurationInMinutes = 5.0f;

        [Tooltip("Simulation speed multiplier (1x = standard, 5x = fast, etc.).")]
        [Range(0f, 100f)]
        public float timeMultiplier = 1.0f;

        [Tooltip("Pause the progression of time.")]
        public bool isPaused = false;

        [Tooltip("Day counter (starts at Day 1).")]
        public int currentDay = 1;

        [Header("--- Celestial Lighting (Sun & Moon) ---")]
        [Tooltip("Directional Light representing the Sun / Moon in the scene.")]
        public Light sunLight;

        [Tooltip("Controls directional light rotation across the 24-hour cycle.")]
        public bool controlSunRotation = true;

        [Tooltip("Controls directional light color and intensity across the 24-hour cycle.")]
        public bool controlSunLight = true;

        [Tooltip("Controls ambient environment lighting so the world never plunges into pitch blackness.")]
        public bool controlAmbient = true;

        [Tooltip("Controls atmospheric fog color.")]
        public bool controlFog = true;

        [Tooltip("Horizontal azimuth offset for the sun path in degrees.")]
        [Range(0f, 360f)]
        public float sunAzimuthOffset = 50f;

        [Header("--- Continuous Skybox Transition Settings ---")]
        [Tooltip("Continuously and gradually crossfades between the skyboxes across the entire day.")]
        public bool continuousBlending = true;

        [Tooltip("Enable periodic DynamicGI environment probe updates. Disabled by default to eliminate stutter/frame freeze.")]
        public bool updateDynamicGI = false;

        [Tooltip("Interval in real-time seconds between DynamicGI environment updates.")]
        [Range(0.5f, 10f)]
        public float giUpdateInterval = 2.0f;

        [Header("--- Optimized Skybox Stages (Lightweight Build) ---")]
        [Tooltip("Configurable stages representing the essential Skyboxes across the 24-hour day (Dawn, Day, Sunset, Night).")]
        public List<SkyboxStageData> stages = new List<SkyboxStageData>();

        // Events
        public event Action<float> OnTimeUpdated;
        public event Action<int> OnHourPassed;
        public event Action<int> OnDayPassed;
        public event Action<string> OnPhaseChanged;

        // Internal State
        private Material blendMaterial;
        private int lastHour = -1;
        private string currentPhaseName = "";
        private float lastGiUpdateTime = 0f;

        public float CurrentTime => currentTime;
        public int Hour => Mathf.FloorToInt(currentTime);
        public int Minute => Mathf.FloorToInt((currentTime - Mathf.Floor(currentTime)) * 60f);
        public string CurrentPhase => currentPhaseName;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this && Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            InitBlendMaterial();
            lastHour = Hour;
        }

        private void OnEnable()
        {
            InitBlendMaterial();
            if (sunLight == null)
            {
                var lights = FindObjectsByType<Light>();
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            ApplyTimeOfDay(currentTime);
        }

        private void Start()
        {
            ApplyTimeOfDay(currentTime);
        }

        private void Update()
        {
            if (Application.isPlaying && !isPaused)
            {
                // Advance time: 24 in-game hours per (dayDurationInMinutes * 60) seconds
                float hoursPerSecond = 24.0f / (dayDurationInMinutes * 60.0f);
                float deltaHours = hoursPerSecond * Time.deltaTime * timeMultiplier;
                
                SetTime(currentTime + deltaHours);
            }
            else if (!Application.isPlaying)
            {
                ApplyTimeOfDay(currentTime);
            }
        }

        private void OnValidate()
        {
            ApplyTimeOfDay(currentTime);
        }

        private void OnDestroy()
        {
            if (blendMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(blendMaterial);
                else
                    DestroyImmediate(blendMaterial);
            }
        }

        public void SetTime(float newHour)
        {
            int prevHour = Hour;
            
            currentTime = newHour;
            while (currentTime >= 24f)
            {
                currentTime -= 24f;
                currentDay++;
                OnDayPassed?.Invoke(currentDay);
            }
            while (currentTime < 0f)
            {
                currentTime += 24f;
                if (currentDay > 1) currentDay--;
            }

            int newHourInt = Hour;
            if (newHourInt != prevHour)
            {
                OnHourPassed?.Invoke(newHourInt);
            }

            ApplyTimeOfDay(currentTime);
            OnTimeUpdated?.Invoke(currentTime);
        }

        public void SetTimeMultiplier(float mult)
        {
            timeMultiplier = Mathf.Max(0f, mult);
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
        }

        public void AddMinutes(float minutes)
        {
            SetTime(currentTime + (minutes / 60.0f));
        }

        public string GetFormattedTime(bool use24Hour = false)
        {
            int h = Hour;
            int m = Minute;

            if (use24Hour)
            {
                return $"{h:D2}:{m:D2}";
            }

            string suffix = h >= 12 ? "PM" : "AM";
            int h12 = h % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12:D2}:{m:D2} {suffix}";
        }

        public string GetFormattedDay()
        {
            return $"Day {currentDay}";
        }

        private void InitBlendMaterial()
        {
            if (blendMaterial == null)
            {
                Shader blendShader = Shader.Find("Custom/SkyboxBlendedCubemap");
                if (blendShader != null)
                {
                    blendMaterial = new Material(blendShader)
                    {
                        name = "DayNight_RuntimeSkyboxBlend",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }
        }

        public void ApplyTimeOfDay(float time)
        {
            if (stages == null || stages.Count == 0) return;

            // Sort stages by start hour
            var sortedStages = new List<SkyboxStageData>(stages);
            sortedStages.Sort((a, b) => a.startHour.CompareTo(b.startHour));

            // Determine active stage and next stage
            int currentIndex = 0;
            for (int i = 0; i < sortedStages.Count; i++)
            {
                if (time >= sortedStages[i].startHour)
                {
                    currentIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (time < sortedStages[0].startHour)
            {
                currentIndex = sortedStages.Count - 1;
            }

            int nextIndex = (currentIndex + 1) % sortedStages.Count;
            SkyboxStageData stageA = sortedStages[currentIndex];
            SkyboxStageData stageB = sortedStages[nextIndex];

            // Calculate continuous progress between stage A and stage B
            float span = stageB.startHour - stageA.startHour;
            if (span <= 0f) span += 24f;

            float elapsed = time - stageA.startHour;
            if (elapsed < 0f) elapsed += 24f;

            float rawT = Mathf.Clamp01(elapsed / span);
            // S-curve smooth interpolation: gradual transition without sudden jumps
            float smoothT = continuousBlending ? Mathf.SmoothStep(0f, 1f, rawT) : (rawT > 0.5f ? 1f : 0f);

            // Update Current Phase Name
            string activePhase = rawT > 0.5f ? stageB.stageName : stageA.stageName;
            if (activePhase != currentPhaseName)
            {
                currentPhaseName = activePhase;
                OnPhaseChanged?.Invoke(currentPhaseName);
            }

            // 1. Continuous Skybox Blending
            UpdateSkybox(stageA, stageB, smoothT);

            // 2. Smooth Celestial Lighting (Sun + Moon, Never Pitch Black)
            UpdateCelestialLighting(stageA, stageB, smoothT, time);

            // 3. Smooth Ambient and Atmospheric Lighting
            UpdateEnvironmentAtmosphere(stageA, stageB, smoothT);

            // 4. Update DynamicGI occasionally (only if explicitly enabled)
            if (updateDynamicGI && Application.isPlaying && giUpdateInterval > 0f && Time.time - lastGiUpdateTime >= giUpdateInterval)
            {
                lastGiUpdateTime = Time.time;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void UpdateSkybox(SkyboxStageData a, SkyboxStageData b, float blend)
        {
            if (a.cubemapTexture != null && b.cubemapTexture != null)
            {
                if (blendMaterial == null) InitBlendMaterial();

                if (blendMaterial != null)
                {
                    blendMaterial.SetTexture("_TexA", a.cubemapTexture);
                    blendMaterial.SetTexture("_TexB", b.cubemapTexture);
                    blendMaterial.SetFloat("_Blend", blend);
                    blendMaterial.SetFloat("_Exposure", Mathf.Lerp(a.exposure, b.exposure, blend));
                    blendMaterial.SetColor("_Tint", Color.Lerp(a.tint, b.tint, blend));
                    blendMaterial.SetFloat("_Rotation", Mathf.Lerp(a.rotation, b.rotation, blend));

                    if (RenderSettings.skybox != blendMaterial)
                    {
                        RenderSettings.skybox = blendMaterial;
                    }
                    return;
                }
            }

            // Fallback
            Material targetMat = blend > 0.5f ? b.skyboxMaterial : a.skyboxMaterial;
            if (targetMat != null && RenderSettings.skybox != targetMat)
            {
                RenderSettings.skybox = targetMat;
            }
        }

        private void UpdateCelestialLighting(SkyboxStageData a, SkyboxStageData b, float blend, float time)
        {
            if (sunLight == null) return;

            if (controlSunRotation)
            {
                // Continuous 24-hour Celestial Orbit without jumps or snapping.
                // Light always aims downwards towards the ground so shadows and surfaces stay illuminated.
                // Uses seamless Quaternion.Slerp across sunrise (5.5 - 7.0) and sunset (17.5 - 19.0)
                // so sun-to-moon and moon-to-sun transitions are 100% smooth without pitch/yaw steps.
                float minPitch = 10.0f;

                // 1. Daytime Sun Trajectory (5.5 to 18.5)
                float dayNorm = Mathf.Clamp01((time - 5.5f) / 13.0f);
                float pitchSun = Mathf.Sin(dayNorm * Mathf.PI) * 65.0f + minPitch;
                float yawSun = Mathf.Lerp(sunAzimuthOffset, sunAzimuthOffset + 180.0f, dayNorm);
                Quaternion sunRot = Quaternion.Euler(pitchSun, yawSun, 0f);

                // 2. Nighttime Moon Trajectory (17.5 to 6.5, wrapped across midnight)
                float nightTime = time >= 17.5f ? (time - 17.5f) : (time + 6.5f);
                float nightNorm = Mathf.Clamp01(nightTime / 13.0f);
                float pitchMoon = Mathf.Sin(nightNorm * Mathf.PI) * 45.0f + minPitch;
                float yawMoon = Mathf.Lerp(sunAzimuthOffset + 180.0f, sunAzimuthOffset + 360.0f, nightNorm);
                Quaternion moonRot = Quaternion.Euler(pitchMoon, yawMoon, 0f);

                // 3. Smooth C1-continuous transition during twilight windows
                Quaternion finalCelestialRot;
                if (time >= 17.5f && time <= 19.0f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (time - 17.5f) / 1.5f);
                    finalCelestialRot = Quaternion.Slerp(sunRot, moonRot, t);
                }
                else if (time >= 5.5f && time <= 7.0f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (time - 5.5f) / 1.5f);
                    finalCelestialRot = Quaternion.Slerp(moonRot, sunRot, t);
                }
                else if (time > 7.0f && time < 17.5f)
                {
                    finalCelestialRot = sunRot;
                }
                else
                {
                    finalCelestialRot = moonRot;
                }

                sunLight.transform.rotation = finalCelestialRot;
            }

            if (controlSunLight)
            {
                // Smooth continuous interpolation of light color and intensity
                Color targetColor = Color.Lerp(a.sunColor, b.sunColor, blend);
                float targetIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, blend);

                sunLight.color = targetColor;
                sunLight.intensity = targetIntensity;
            }
        }

        private void UpdateEnvironmentAtmosphere(SkyboxStageData a, SkyboxStageData b, float blend)
        {
            if (controlAmbient)
            {
                // Ensure ambient light continuously transitions and maintains soft visibility
                Color targetAmbient = Color.Lerp(a.ambientColor, b.ambientColor, blend);
                RenderSettings.ambientLight = targetAmbient;
            }

            if (controlFog && RenderSettings.fog)
            {
                Color targetFog = Color.Lerp(a.fogColor, b.fogColor, blend);
                RenderSettings.fogColor = targetFog;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Populates 4 lightweight essential skyboxes (Dawn, Daytime, Sunset, Moonlit Night) saving ~144 MB of build size while retaining beautiful visuals.
        /// </summary>
        [ContextMenu("Auto Setup Optimized Skyboxes (4 Stages - Recommended)")]
        public void AutoPopulateOptimizedSkyboxes()
        {
            stages.Clear();

            // 1. Dawn / Sunrise (05:30)
            stages.Add(new SkyboxStageData(
                "Dawn", 5.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Deep Dusk/Deep Dusk Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Deep Dusk/Deep Dusk Equirect.png"),
                new Color(1.0f, 0.75f, 0.60f), 0.65f,
                new Color(0.25f, 0.22f, 0.32f), new Color(0.55f, 0.40f, 0.55f), 1.0f));

            // 2. Daytime (08:30)
            stages.Add(new SkyboxStageData(
                "Daytime", 8.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cartoon Base BlueSky/Day_BlueSky_Nothing Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Cartoon Base BlueSky/Sky_Day_BlueSky_Equirect.png"),
                new Color(1.0f, 0.98f, 0.92f), 1.35f,
                new Color(0.40f, 0.42f, 0.48f), new Color(0.72f, 0.80f, 0.90f), 1.0f));

            // 3. Sunset (17:30)
            stages.Add(new SkyboxStageData(
                "Warm Sunset", 17.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset_EquiRect_flat.png"),
                new Color(1.0f, 0.60f, 0.32f), 0.95f,
                new Color(0.38f, 0.28f, 0.30f), new Color(0.80f, 0.50f, 0.45f), 1.0f));

            // 4. Moonlit Night (21:30)
            stages.Add(new SkyboxStageData(
                "Moonlit Night", 21.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Night MoonBurst/AllSky_Night_MoonBurst Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Night MoonBurst/AllSky_Night_MoonBurst Equirect.png"),
                new Color(0.55f, 0.70f, 0.95f), 0.32f,
                new Color(0.18f, 0.22f, 0.32f), new Color(0.22f, 0.26f, 0.38f), 1.0f));

            currentTime = 7.0f;
            InitBlendMaterial();
            ApplyTimeOfDay(currentTime);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("<color=#44FF88><b>DayNightCycleManager:</b> Auto-populated 4 optimized skybox stages (Dawn, Daytime, Sunset, Night). Saved ~144 MB build size!</color>");
        }

        /// <summary>
        /// Populates all 10 AllSkyFree skyboxes automatically. Note: This will include all 10 textures (~240 MB) into the build.
        /// </summary>
        [ContextMenu("Auto Setup All 10 AllSkyFree Skyboxes (Heavy)")]
        public void AutoPopulateAllSkyFree()
        {
            stages.Clear();

            // 0. Morning Sky (Game starts here at 07:00 AM)
            stages.Add(new SkyboxStageData(
                "Morning Sky", 6.0f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cartoon Base BlueSky/Day_BlueSky_Nothing Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Cartoon Base BlueSky/Sky_Day_BlueSky_Equirect.png"),
                new Color(1.0f, 0.95f, 0.85f), 1.25f,
                new Color(0.38f, 0.42f, 0.48f), new Color(0.72f, 0.80f, 0.90f), 1.0f));

            // 1. Daytime Soft Overcast
            stages.Add(new SkyboxStageData(
                "Daytime Overcast", 9.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Overcast Low/AllSky_Overcast4_Low Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Overcast Low/AllSky_Overcast4_Low.png"),
                new Color(0.98f, 0.98f, 1.0f), 1.35f,
                new Color(0.40f, 0.43f, 0.48f), new Color(0.75f, 0.82f, 0.88f), 1.0f));

            // 2. Golden Afternoon
            stages.Add(new SkyboxStageData(
                "Golden Afternoon", 13.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_GloriousPink/Epic_GloriousPink Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Epic_GloriousPink/Epic_GloriousPink_EquiRect.png"),
                new Color(1.0f, 0.85f, 0.72f), 1.20f,
                new Color(0.42f, 0.35f, 0.38f), new Color(0.85f, 0.72f, 0.75f), 1.0f));

            // 3. Sunset
            stages.Add(new SkyboxStageData(
                "Warm Sunset", 16.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cold Sunset/Cold Sunset Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Cold Sunset/Cold Sunset Equirect.png"),
                new Color(1.0f, 0.65f, 0.38f), 0.95f,
                new Color(0.38f, 0.28f, 0.30f), new Color(0.82f, 0.55f, 0.48f), 1.0f));

            // 4. Late Sunset / Twilight
            stages.Add(new SkyboxStageData(
                "Late Sunset", 18.0f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset_EquiRect_flat.png"),
                new Color(0.95f, 0.48f, 0.32f), 0.70f,
                new Color(0.30f, 0.22f, 0.32f), new Color(0.65f, 0.40f, 0.52f), 1.0f));

            // 5. Deep Dusk
            stages.Add(new SkyboxStageData(
                "Deep Dusk", 19.25f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Deep Dusk/Deep Dusk Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Deep Dusk/Deep Dusk Equirect.png"),
                new Color(0.62f, 0.48f, 0.80f), 0.45f,
                new Color(0.24f, 0.20f, 0.32f), new Color(0.42f, 0.32f, 0.52f), 1.0f));

            // 6. Moonlit Night
            stages.Add(new SkyboxStageData(
                "Moonlit Night", 20.75f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Night MoonBurst/AllSky_Night_MoonBurst Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Night MoonBurst/AllSky_Night_MoonBurst Equirect.png"),
                new Color(0.55f, 0.70f, 0.95f), 0.32f,
                new Color(0.18f, 0.22f, 0.32f), new Color(0.25f, 0.30f, 0.42f), 1.0f));

            // 7. Cold Midnight
            stages.Add(new SkyboxStageData(
                "Deep Midnight", 23.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cold Night/Cold Night Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Cold Night/Cold Night Equirect.png"),
                new Color(0.45f, 0.58f, 0.88f), 0.26f,
                new Color(0.15f, 0.18f, 0.28f), new Color(0.20f, 0.24f, 0.36f), 1.0f));

            // 8. Late Night Nocturne
            stages.Add(new SkyboxStageData(
                "Late Night", 2.0f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Cartoon Base NightSky/Cartoon Base NightSky Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Cartoon Base NightSky/Cartoon Base NightSky Equirect.png"),
                new Color(0.38f, 0.48f, 0.78f), 0.22f,
                new Color(0.14f, 0.16f, 0.26f), new Color(0.18f, 0.22f, 0.32f), 1.0f));

            // 9. Pre-Dawn Cosmic Twilight
            stages.Add(new SkyboxStageData(
                "Pre-Dawn Cosmic", 4.5f,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Space_AnotherPlanet/AllSky_Space_AnotherPlanet Equirect.mat"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/AllSkyFree/Space_AnotherPlanet/AllSky_Space_AnotherPlanet Equirect.png"),
                new Color(0.68f, 0.52f, 0.88f), 0.42f,
                new Color(0.22f, 0.18f, 0.30f), new Color(0.35f, 0.25f, 0.45f), 1.0f));

            currentTime = 7.0f;
            InitBlendMaterial();
            ApplyTimeOfDay(currentTime);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("<color=#44FF88><b>DayNightCycleManager:</b> Auto-populated 10 AllSkyFree skyboxes with continuous smooth blending!</color>");
        }
#endif
    }
}
