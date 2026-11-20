using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Audio;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master Controller for the authentic Jbel Ighoud Graphics Settings Modal:
    /// - Matches reference art design with warm dark brown gradient card background.
    /// - Top-left badge tab "GRAPHICS" (Assets/UI/graphics/graphics.png).
    /// - Top-right circular close button with 'X'.
    /// - Row 1: Quality presets (Low, Medium, High, Realistic) with real Unity & URP settings.
    /// - Row 2: FPS Limiter (30 FPS, 60 FPS) with Application.targetFrameRate & vSyncCount control.
    /// - Row 3: Water Effects toggle with HTML-style circular radio button (inner circle dot when active).
    /// - Complete persistence via PlayerPrefs and real-time visual synchronization.
    /// </summary>
    public class JbelGraphicsSettingsModal : MonoBehaviour
    {
        private static JbelGraphicsSettingsModal _instance;
        public static JbelGraphicsSettingsModal Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<JbelGraphicsSettingsModal>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        public const string PREF_QUALITY = "JBEL_GRAPHICS_QUALITY"; // 0=Low, 1=Medium, 2=High, 3=Realistic
        public const string PREF_FPS = "JBEL_GRAPHICS_FPS"; // 30, 60
        public const string PREF_WATER = "JBEL_GRAPHICS_WATER"; // 1=On, 0=Off

        [Header("========================================")]
        [Header("--- MODAL CONTAINERS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup modalCG;
        [SerializeField] private RectTransform dialogCardRT;
        [SerializeField] private Button closeButton;

        [Header("========================================")]
        [Header("--- ROW 1: QUALITY PRESET BUTTONS ---")]
        [Header("========================================")]
        [SerializeField] private Button btnLow;
        [SerializeField] private Button btnMedium;
        [SerializeField] private Button btnHigh;
        [SerializeField] private Button btnRealistic;

        [SerializeField] private Image imgLow;
        [SerializeField] private Image imgMedium;
        [SerializeField] private Image imgHigh;
        [SerializeField] private Image imgRealistic;

        [SerializeField] private TextMeshProUGUI txtLow;
        [SerializeField] private TextMeshProUGUI txtMedium;
        [SerializeField] private TextMeshProUGUI txtHigh;
        [SerializeField] private TextMeshProUGUI txtRealistic;

        [Header("========================================")]
        [Header("--- ROW 2: FPS LIMITER BUTTONS ---")]
        [Header("========================================")]
        [SerializeField] private Button btn30FPS;
        [SerializeField] private Button btn60FPS;

        [SerializeField] private Image img30FPS;
        [SerializeField] private Image img60FPS;

        [SerializeField] private TextMeshProUGUI txt30FPS;
        [SerializeField] private TextMeshProUGUI txt60FPS;

        [Header("========================================")]
        [Header("--- ROW 3: WATER EFFECTS TOGGLE ---")]
        [Header("========================================")]
        [SerializeField] private Button waterToggleButton;
        [SerializeField] private GameObject waterRadioDot;
        [SerializeField] private Image waterToggleRingImg;
        [SerializeField] private Button batteryToggleButton;
        [SerializeField] private GameObject batteryRadioDot;
        [SerializeField] private Image batteryRingImg;
        private bool isBatterySaver = false;
        private const string PREF_BATTERY = "Jbel_BatterySaver";
        public bool IsBatterySaver => isBatterySaver;

        [Header("========================================")]
        [Header("--- ANIMATION ---")]
        [Header("========================================")]
        [SerializeField] private float animDuration = 0.2f;

        // Visual Palette for Buttons
        private readonly Color colorSelectedBg = new Color(1.22f, 1.06f, 0.72f, 1.0f); // Warm glowing gold highlight
        private readonly Color colorNormalBg = new Color(0.92f, 0.92f, 0.92f, 0.96f);   // Natural authentic pill frame
        private readonly Color colorSelectedText = new Color(1.0f, 0.98f, 0.90f, 1.0f); // Bright ivory
        private readonly Color colorNormalText = new Color(0.85f, 0.77f, 0.67f, 0.95f); // Warm parchment text

        private Coroutine animRoutine;
        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private Action onCloseCallback;

        // Current Applied States
        private int currentQuality = 2; // Default High (2)
        private int currentFPS = 60;     // Default 60 FPS
        private bool isWaterEffectsEnabled = true; // Default ON

        public int CurrentQuality => currentQuality;
        public int CurrentFPS => currentFPS;
        public bool IsWaterEffectsEnabled => isWaterEffectsEnabled;

        private void Awake()
        {
            _instance = this;
            AutoWire();
            WireListeners();
            LoadAndApplySettings();
        }

        private void Start()
        {
            AutoWire();
            WireListeners();
            LoadAndApplySettings();

            if (!isOpen)
            {
                HideImmediate();
            }
        }

        public void AutoWire()
        {
            if (modalCG == null) modalCG = GetComponent<CanvasGroup>();
            if (dialogCardRT == null)
            {
                var card = transform.Find("DialogCard");
                dialogCardRT = card != null ? card.GetComponent<RectTransform>() : GetComponent<RectTransform>();
            }

            if (dialogCardRT != null)
            {
                if (closeButton == null)
                {
                    var cb = dialogCardRT.Find("CloseButton");
                    if (cb != null) closeButton = cb.GetComponent<Button>();
                }

                // Row 1: Quality
                var r1 = dialogCardRT.Find("Row_Quality");
                if (r1 != null)
                {
                    WireQualityButtonRef(r1.Find("Btn_Low"), ref btnLow, ref imgLow, ref txtLow);
                    WireQualityButtonRef(r1.Find("Btn_Medium"), ref btnMedium, ref imgMedium, ref txtMedium);
                    WireQualityButtonRef(r1.Find("Btn_High"), ref btnHigh, ref imgHigh, ref txtHigh);
                    WireQualityButtonRef(r1.Find("Btn_Realistic"), ref btnRealistic, ref imgRealistic, ref txtRealistic);
                }

                // Row 2: FPS
                var r2 = dialogCardRT.Find("Row_FPS");
                if (r2 != null)
                {
                    WireQualityButtonRef(r2.Find("Btn_30FPS"), ref btn30FPS, ref img30FPS, ref txt30FPS);
                    WireQualityButtonRef(r2.Find("Btn_60FPS"), ref btn60FPS, ref img60FPS, ref txt60FPS);
                }

                var rbat = dialogCardRT.Find("Row_Water/BatteryRadioBtn");
                if (rbat != null)
                {
                    batteryToggleButton = rbat.GetComponent<Button>();
                    batteryRingImg = rbat.GetComponent<Image>();
                    var bdot = rbat.Find("RadioDot");
                    if (bdot != null) batteryRadioDot = bdot.gameObject;
                }

                // Row: Render distance
                var rd = dialogCardRT.Find("Row_Distance");
                if (rd != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var bt = rd.Find("Btn_Dist" + i);
                        if (bt == null) continue;
                        btnDist[i] = bt.GetComponent<Button>();
                        imgDist[i] = bt.GetComponent<Image>();
                        var lbl = bt.Find("Text");
                        if (lbl != null) txtDist[i] = lbl.GetComponent<TextMeshProUGUI>();
                    }
                }

                // Row 3: Water Effects
                var r3 = dialogCardRT.Find("Row_Water");
                if (r3 != null)
                {
                    var btnT = r3.Find("WaterRadioBtn");
                    if (btnT != null)
                    {
                        waterToggleButton = btnT.GetComponent<Button>();
                        waterToggleRingImg = btnT.GetComponent<Image>();
                        var dot = btnT.Find("RadioDot");
                        if (dot != null) waterRadioDot = dot.gameObject;
                    }
                }
            }
        }

        private void WireQualityButtonRef(Transform t, ref Button btn, ref Image img, ref TextMeshProUGUI txt)
        {
            if (t == null) return;
            if (btn == null) btn = t.GetComponent<Button>();
            if (img == null) img = t.GetComponent<Image>();
            if (txt == null)
            {
                var label = t.Find("Text");
                if (label != null) txt = label.GetComponent<TextMeshProUGUI>();
            }
        }

        private void WireListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseGraphicsModal);
                closeButton.onClick.AddListener(CloseGraphicsModal);
            }

            if (btnLow != null)
            {
                btnLow.onClick.RemoveListener(OnLowQualityClicked);
                btnLow.onClick.AddListener(OnLowQualityClicked);
            }
            if (btnMedium != null)
            {
                btnMedium.onClick.RemoveListener(OnMediumQualityClicked);
                btnMedium.onClick.AddListener(OnMediumQualityClicked);
            }
            if (btnHigh != null)
            {
                btnHigh.onClick.RemoveListener(OnHighQualityClicked);
                btnHigh.onClick.AddListener(OnHighQualityClicked);
            }
            if (btnRealistic != null)
            {
                btnRealistic.onClick.RemoveListener(OnRealisticQualityClicked);
                btnRealistic.onClick.AddListener(OnRealisticQualityClicked);
            }

            if (btn30FPS != null)
            {
                btn30FPS.onClick.RemoveListener(On30FPSClicked);
                btn30FPS.onClick.AddListener(On30FPSClicked);
            }
            if (btn60FPS != null)
            {
                btn60FPS.onClick.RemoveListener(On60FPSClicked);
                btn60FPS.onClick.AddListener(On60FPSClicked);
            }

            for (int i = 0; i < 4; i++)
            {
                if (btnDist[i] == null) continue;
                int idx = i;
                btnDist[i].onClick.RemoveAllListeners();
                btnDist[i].onClick.AddListener(() => OnDistanceClicked(idx));
            }

            if (waterToggleButton != null)
            {
                waterToggleButton.onClick.RemoveListener(OnWaterToggleClicked);
                waterToggleButton.onClick.AddListener(OnWaterToggleClicked);
            }
            if (batteryToggleButton != null)
            {
                batteryToggleButton.onClick.RemoveListener(ToggleBatterySaver);
                batteryToggleButton.onClick.AddListener(ToggleBatterySaver);
            }
        }

        // =========================================================
        // --- SETTINGS LOGIC & APPLICATION ---
        // =========================================================

        public void LoadAndApplySettings()
        {
            currentQuality = PlayerPrefs.GetInt(PREF_QUALITY, JbelGraphicsPresets.DetectDefaultQuality()); // auto by device
            currentFPS = PlayerPrefs.GetInt(PREF_FPS, JbelGraphicsPresets.DetectDefaultFps(currentQuality));
            isWaterEffectsEnabled = PlayerPrefs.GetInt(PREF_WATER, 1) == 1; // Default ON

            isBatterySaver = PlayerPrefs.GetInt(PREF_BATTERY, 0) == 1;
            ApplyQualitySettings(currentQuality, save: false);
            ApplyFPSLimit(currentFPS, save: false);
            ApplyWaterEffects(isWaterEffectsEnabled, save: false);
            UpdateBatteryUI();
        }

        public void SetQualityLevel(int level)
        {
            ApplyQualitySettings(level, save: true);
        }

        public void SetFPSLimit(int fps)
        {
            ApplyFPSLimit(fps, save: true);
        }

        public void SetWaterEffects(bool enabled)
        {
            ApplyWaterEffects(enabled, save: true);
        }

        public void ToggleWaterEffects()
        {
            ApplyWaterEffects(!isWaterEffectsEnabled, save: true);
        }

        private void OnLowQualityClicked() => ApplyQualitySettings(0, save: true);
        private void OnMediumQualityClicked() => ApplyQualitySettings(1, save: true);
        private void OnHighQualityClicked() => ApplyQualitySettings(2, save: true);
        private void OnRealisticQualityClicked() => ApplyQualitySettings(3, save: true);

        private void On30FPSClicked() => ApplyFPSLimit(30, save: true);
        private void On60FPSClicked() => ApplyFPSLimit(60, save: true);

        private void OnWaterToggleClicked() => ToggleWaterEffects();

        private void ApplyQualitySettings(int level, bool save)
        {
            currentQuality = Mathf.Clamp(level, 0, 3);
            if (save)
            {
                PlayerPrefs.SetInt(PREF_QUALITY, currentQuality);
                PlayerPrefs.Save();
            }

            if (save) JbelGraphicsPresets.SaveDistanceIndex(-1); // a new preset resets render distance to its default
            JbelGraphicsPresets.Apply(currentQuality, isBatterySaver);

            UpdateQualityUI();
            UpdateDistanceUI();
        }

        private void ApplyURPSettings(float renderScale, float shadowDist)
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = isBatterySaver ? Mathf.Min(renderScale, 0.75f) : renderScale;
                urp.shadowDistance = shadowDist;
            }
        }

        private void ApplyGrassSettings(float radius, int budget, bool shadows)
        {
            var grass = UnityEngine.Object.FindAnyObjectByType<LandscapeGrassManager>(FindObjectsInactive.Include);
            if (grass != null)
            {
                grass.dynamicRadius = radius;
                grass.maxTriangleBudget = budget;
                grass.mobileCastShadows = shadows;
            }
        }

        private void ApplyFPSLimit(int fps, bool save)
        {
            currentFPS = (fps <= 30) ? 30 : 60;
            if (save)
            {
                PlayerPrefs.SetInt(PREF_FPS, currentFPS);
                PlayerPrefs.Save();
            }

            QualitySettings.vSyncCount = 0; // Essential for targetFrameRate to take effect
            Application.targetFrameRate = isBatterySaver ? 30 : currentFPS;

            UpdateFPSUI();
        }

        private void ApplyWaterEffects(bool enabled, bool save)
        {
            isWaterEffectsEnabled = enabled;
            if (save)
            {
                PlayerPrefs.SetInt(PREF_WATER, isWaterEffectsEnabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            var lake = UnityEngine.Object.FindAnyObjectByType<LakeWaterManager>(FindObjectsInactive.Include);
            if (lake != null)
            {
                lake.UseSuimonoWater = isWaterEffectsEnabled;
            }

            var suimonoVFX = UnityEngine.Object.FindAnyObjectByType<SuimonoWaterVFX>(FindObjectsInactive.Include);
            if (suimonoVFX != null)
            {
                suimonoVFX.enabled = isWaterEffectsEnabled;
            }

            UpdateWaterUI();
        }

        // =========================================================
        // --- UI VISUAL REFRESH ---
        // =========================================================

        private void UpdateQualityUI()
        {
            SetButtonVisual(imgLow, txtLow, currentQuality == 0);
            SetButtonVisual(imgMedium, txtMedium, currentQuality == 1);
            SetButtonVisual(imgHigh, txtHigh, currentQuality == 2);
            SetButtonVisual(imgRealistic, txtRealistic, currentQuality == 3);
        }

        private void UpdateFPSUI()
        {
            SetButtonVisual(img30FPS, txt30FPS, currentFPS == 30);
            SetButtonVisual(img60FPS, txt60FPS, currentFPS == 60);
        }

// --- Render distance row (150 m / 300 m / 500 m / 900 m) ---
        private readonly Button[] btnDist = new Button[4];
        private readonly Image[] imgDist = new Image[4];
        private readonly TextMeshProUGUI[] txtDist = new TextMeshProUGUI[4];

        private void OnDistanceClicked(int index)
        {
            JbelGraphicsPresets.SaveDistanceIndex(index);
            JbelGraphicsPresets.Apply(currentQuality, isBatterySaver);
            UpdateDistanceUI();
        }

        private void UpdateDistanceUI()
        {
            int sel = JbelGraphicsPresets.SavedDistanceIndex;
            if (sel < 0) sel = JbelGraphicsPresets.AutoDistanceIndex(currentQuality);
            for (int i = 0; i < 4; i++) SetButtonVisual(imgDist[i], txtDist[i], i == sel);
        }


        private void UpdateWaterUI()
        {
            if (waterRadioDot != null)
            {
                waterRadioDot.SetActive(isWaterEffectsEnabled);
            }
            if (waterToggleRingImg != null)
            {
                waterToggleRingImg.color = Color.white;
            }
        }

        public void ToggleBatterySaver()
        {
            isBatterySaver = !isBatterySaver;
            PlayerPrefs.SetInt(PREF_BATTERY, isBatterySaver ? 1 : 0);
            PlayerPrefs.Save();
            ApplyQualitySettings(currentQuality, save: false);
            ApplyFPSLimit(currentFPS, save: false);
            UpdateBatteryUI();
        }

        private void UpdateBatteryUI()
        {
            if (batteryRadioDot != null) batteryRadioDot.SetActive(isBatterySaver);
            if (batteryRingImg != null) batteryRingImg.color = Color.white;
        }

        private void SetButtonVisual(Image img, TextMeshProUGUI txt, bool isSelected)
        {
            if (img != null)
            {
                img.color = isSelected ? colorSelectedBg : colorNormalBg;
            }
            if (txt != null)
            {
                txt.color = isSelected ? colorSelectedText : colorNormalText;
                txt.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // =========================================================
        // --- MODAL ANIMATION & LIFECYCLE ---
        // =========================================================

        public void OpenGraphicsModal(Action onClose = null)
        {
            isOpen = true;
            onCloseCallback = onClose;

            gameObject.SetActive(true);
            if (modalCG != null)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
            }

            AutoWire();
            WireListeners();
            LoadAndApplySettings();

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(true));
        }

        public void CloseGraphicsModal()
        {
            if (!isOpen) return;
            isOpen = false;

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(false));
        }

        private IEnumerator AnimateModal(bool opening)
        {
            if (modalCG == null) yield break;

            if (opening)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
            }

            float t = 0f;
            float startAlpha = opening ? 0f : modalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = dialogCardRT != null ? (opening ? Vector3.one * 0.94f : dialogCardRT.localScale) : Vector3.one;
            Vector3 targetScale = opening ? Vector3.one : Vector3.one * 0.94f;

            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / animDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                modalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);
                if (dialogCardRT != null)
                {
                    dialogCardRT.localScale = Vector3.Lerp(startScale, targetScale, smoothP);
                }
                yield return null;
            }

            modalCG.alpha = targetAlpha;

            if (!opening)
            {
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
                gameObject.SetActive(false);
                onCloseCallback?.Invoke();
                onCloseCallback = null;
            }

            animRoutine = null;
        }

        public void HideImmediate()
        {
            if (animRoutine != null) { StopCoroutine(animRoutine); animRoutine = null; }
            if (modalCG != null)
            {
                modalCG.alpha = 0f;
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
            }
            if (dialogCardRT != null)
            {
                dialogCardRT.localScale = Vector3.one * 0.94f;
            }
            gameObject.SetActive(false);
        }
    }
}
