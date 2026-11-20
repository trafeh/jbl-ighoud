using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Central graphics/performance presets. Applied automatically at game start
    /// (before the Settings screen is ever opened) and from the Graphics settings modal.
    /// Low-end phones (e.g. Galaxy A03) get the Low preset + 30 FPS by default.
    /// </summary>
    public static class JbelGraphicsPresets
    {
        public const string PREF_QUALITY = "JBEL_GRAPHICS_QUALITY";
        public const string PREF_FPS = "JBEL_GRAPHICS_FPS";
        public const string PREF_BATTERY = "Jbel_BatterySaver";

        // ---------------------------------------------------------------- detection
        public static int DetectDefaultQuality()
        {
            if (!Application.isMobilePlatform) return 2; // editor / desktop
            int ram = SystemInfo.systemMemorySize;       // MB
            string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            bool weakGpu = gpu.Contains("mp1") || gpu.Contains("mali-g52") || gpu.Contains("mali-g51") ||
                           gpu.Contains("mali-t") || gpu.Contains("powervr") || gpu.Contains("adreno (tm) 5") ||
                           gpu.Contains("adreno (tm) 4") || gpu.Contains("adreno (tm) 610") || gpu.Contains("ge8320");
            if (ram <= 4300 || weakGpu) return 0;   // Low
            if (ram <= 6500) return 1;               // Medium
            return 2;                                // High
        }

        public static int DetectDefaultFps(int quality) => quality <= 1 ? 30 : 60;

        public static int SavedQuality => PlayerPrefs.GetInt(PREF_QUALITY, DetectDefaultQuality());
        public static int SavedFps => PlayerPrefs.GetInt(PREF_FPS, DetectDefaultFps(SavedQuality));
        public static bool SavedBatterySaver => PlayerPrefs.GetInt(PREF_BATTERY, 0) == 1;

        // ---------------------------------------------------------------- boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnBoot()
        {
            Apply(SavedQuality, SavedBatterySaver);
            ApplyFps(SavedFps, SavedBatterySaver);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            Apply(SavedQuality, SavedBatterySaver);
            ApplyFps(SavedFps, SavedBatterySaver);
        }

        // ---------------------------------------------------------------- presets
        public static void Apply(int level, bool batterySaver)
        {
            level = Mathf.Clamp(level, 0, 3);
            //                  scale  msaa hdr   shDist casc shRes soft   far    grassDist grassShadow bloom
            switch (level)
            {
                case 0: Set(0.62f, 1, false, 18f, 1, 512, false, 300f, 30f, false, false); break;  // Low
                case 1: Set(0.78f, 1, false, 30f, 1, 1024, false, 500f, 50f, false, false); break; // Medium
                case 2: Set(0.95f, 2, false, 50f, 2, 1024, true, 900f, 80f, false, true); break;   // High
                default: Set(1.0f, 4, true, 100f, 4, 2048, true, 900f, 150f, true, true); break;  // Realistic
            }

            QualitySettings.anisotropicFiltering = level <= 1 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
            QualitySettings.globalTextureMipmapLimit = level == 0 ? 1 : 0;
            QualitySettings.lodBias = level == 0 ? 0.6f : (level == 1 ? 0.85f : 1.2f);
            QualitySettings.pixelLightCount = level <= 1 ? 0 : 2;
            QualitySettings.skinWeights = level <= 1 ? SkinWeights.TwoBones : SkinWeights.FourBones;
            QualitySettings.realtimeReflectionProbes = level >= 3;

            if (batterySaver)
            {
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null) urp.renderScale = Mathf.Min(urp.renderScale, 0.6f);
            }
        }

        public static void ApplyFps(int fps, bool batterySaver)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = batterySaver ? 30 : (fps <= 30 ? 30 : 60);
        }

// ---------------------------------------------------------------- render distance
        public const string PREF_DISTANCE = "JBEL_RENDER_DISTANCE"; // -1 = auto (from quality preset), else index into DistanceOptions
        public static readonly float[] DistanceOptions = { 150f, 300f, 500f, 900f };

        /// <summary>Distance option the given quality preset uses when the player has not picked one.</summary>
        public static int AutoDistanceIndex(int quality)
        {
            return quality <= 0 ? 1 : (quality == 1 ? 2 : 3);
        }

        /// <summary>Saved player choice, or -1 when following the quality preset.</summary>
        public static int SavedDistanceIndex
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(PREF_DISTANCE, -1), -1, DistanceOptions.Length - 1); }
        }

        public static void SaveDistanceIndex(int index)
        {
            if (index < 0) PlayerPrefs.DeleteKey(PREF_DISTANCE);
            else PlayerPrefs.SetInt(PREF_DISTANCE, Mathf.Clamp(index, 0, DistanceOptions.Length - 1));
            PlayerPrefs.Save();
        }

        private static float ResolveFarClip(float presetFar)
        {
            int idx = SavedDistanceIndex;
            return idx < 0 ? presetFar : DistanceOptions[idx];
        }


        private static void Set(float scale, int msaa, bool hdr, float shadowDist, int cascades, int shadowRes,
                                bool softShadows, float farClip, float grassDist, bool grassShadows, bool bloom)
        {
            farClip = ResolveFarClip(farClip);
            grassDist = Mathf.Min(grassDist, farClip);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = scale;
                urp.msaaSampleCount = msaa;
                urp.supportsHDR = hdr;
                urp.shadowDistance = shadowDist;
                urp.shadowCascadeCount = cascades;
                SetProp(urp, "mainLightShadowmapResolution", shadowRes);
                SetProp(urp, "supportsSoftShadows", softShadows);
                SetProp(urp, "supportsAdditionalLightShadows", false);
            }
            QualitySettings.shadowDistance = shadowDist;
            QualitySettings.shadowCascades = cascades;
            QualitySettings.antiAliasing = msaa > 1 ? msaa : 0;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = farClip;
                var cd = cam.GetComponent<UniversalAdditionalCameraData>();
                if (cd != null) cd.antialiasing = AntialiasingMode.None;
            }

            // Cinemachine drives the camera lens every frame, so set far clip on the virtual cameras too
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().FullName != "Unity.Cinemachine.CinemachineCamera") continue;
                var lf = mb.GetType().GetField("Lens");
                if (lf == null) continue;
                object lens = lf.GetValue(mb);
                var ff = lens.GetType().GetField("FarClipPlane");
                if (ff == null) continue;
                ff.SetValue(lens, farClip);
                lf.SetValue(mb, lens);
            }

            var grass = Object.FindAnyObjectByType<LandscapeGrassManager>(FindObjectsInactive.Include);
            if (grass != null)
            {
                grass.maxDrawDistance = grassDist;
                grass.castShadows = grassShadows;
                grass.mobileCastShadows = grassShadows;
                grass.dynamicRadius = Mathf.Min(grass.dynamicRadius, grassDist);
            }

            foreach (var vol in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!vol.isGlobal || vol.sharedProfile == null) continue;
                if (vol.sharedProfile.TryGet(out Bloom b)) b.active = bloom;
                if (vol.sharedProfile.TryGet(out MotionBlur mb)) mb.active = false;
            }

            ConfigureSecondaryCameras();
        }

        /// <summary>Map/minimap cameras: no shadows, no post, no AA.</summary>
        public static void ConfigureSecondaryCameras()
        {
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c == Camera.main || c.targetTexture == null) continue;
                var cd = c.GetComponent<UniversalAdditionalCameraData>();
                if (cd == null) cd = c.gameObject.AddComponent<UniversalAdditionalCameraData>();
                cd.renderShadows = false;
                cd.renderPostProcessing = false;
                cd.antialiasing = AntialiasingMode.None;
                cd.requiresDepthTexture = false;
                cd.requiresColorTexture = false;
                c.useOcclusionCulling = false;
                c.allowHDR = false;
                c.allowMSAA = false;
            }
        }

        private static void SetProp(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name);
            if (p != null && p.CanWrite)
            {
                try { p.SetValue(target, value); } catch { }
            }
        }
    }
}
