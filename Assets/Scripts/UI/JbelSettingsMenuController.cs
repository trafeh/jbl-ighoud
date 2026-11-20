using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master Controller for the authentic Jbel Ighoud Settings Menu Screen:
    /// - 3-Column, 2-Row card layout matching reference art (Sound, Graphics, Control, Langue, Accessibility, Reset to Defaults).
    /// - Uses authentic dark brown gradient card background (Assets/UI/settings/bg.png) with 9-slice corners.
    /// - Circular back button (Assets/UI/settings/arrow.png) at top-left.
    /// - Authentic typography using IM_FELL_English_SC SDF with bottom-left titles/subtitles and centered reset banner.
    /// - Smooth unscaled modal fade and scale transitions connecting seamlessly with Pause Menu.
    /// - Interactive card click actions with audio/haptics feedback and non-destructive singleton management.
    /// </summary>
    public class JbelSettingsMenuController : MonoBehaviour
    {
        private static JbelSettingsMenuController _instance;
        public static JbelSettingsMenuController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<JbelSettingsMenuController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [Header("========================================")]
        [Header("--- MODAL PANELS & CANVAS GROUPS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup settingsModalCG;
        [SerializeField] private Image dimBackdropImage;
        [SerializeField] private RectTransform contentContainerRT;
        [SerializeField] private Button backButton;
        [SerializeField] private JbelSoundSettingsModal soundSettingsModal;
        [SerializeField] private JbelGraphicsSettingsModal graphicsSettingsModal;

        [Header("========================================")]
        [Header("--- SETTINGS CARD BUTTONS ---")]
        [Header("========================================")]
        [SerializeField] private Button soundButton;
        [SerializeField] private Button graphicsButton;
        [SerializeField] private Button controlButton;
        [SerializeField] private Button languageButton;
        [SerializeField] private Button accessibilityButton;
        [SerializeField] private Button resetDefaultsButton;

        [Header("========================================")]
        [Header("--- CARD TEXTS ---")]
        [Header("========================================")]
        [SerializeField] private TextMeshProUGUI soundTitleText;
        [SerializeField] private TextMeshProUGUI soundSubtitleText;
        [SerializeField] private TextMeshProUGUI graphicsTitleText;
        [SerializeField] private TextMeshProUGUI graphicsSubtitleText;
        [SerializeField] private TextMeshProUGUI controlTitleText;
        [SerializeField] private TextMeshProUGUI controlSubtitleText;
        [SerializeField] private TextMeshProUGUI languageTitleText;
        [SerializeField] private TextMeshProUGUI languageSubtitleText;
        [SerializeField] private TextMeshProUGUI accessibilityTitleText;
        [SerializeField] private TextMeshProUGUI accessibilitySubtitleText;
        [SerializeField] private TextMeshProUGUI resetDefaultsTitleText;
        [SerializeField] private TextMeshProUGUI resetDefaultsSubtitleText;

        [Header("========================================")]
        [Header("--- FEEDBACK TOAST ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup toastCG;
        [SerializeField] private TextMeshProUGUI toastText;

        [Header("========================================")]
        [Header("--- TIMING & ANIMATION ---")]
        [Header("========================================")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float dimBackdropAlpha = 0.75f;
        [SerializeField] private float modalAnimationDuration = 0.22f;

        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private bool wasOpenedFromPause = false;
        private Coroutine modalAnimCoroutine;
        private Coroutine toastCoroutine;

        private void Awake()
        {
            _instance = this;
            isOpen = false;

            AutoWireReferences();

            // Wire button click listeners
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(CloseSettings);
                backButton.onClick.AddListener(CloseSettings);
            }
            if (soundButton != null)
            {
                soundButton.onClick.RemoveListener(OnSoundClicked);
                soundButton.onClick.AddListener(OnSoundClicked);
            }
            if (graphicsButton != null)
            {
                graphicsButton.onClick.RemoveListener(OnGraphicsClicked);
                graphicsButton.onClick.AddListener(OnGraphicsClicked);
            }
            if (controlButton != null)
            {
                controlButton.onClick.RemoveListener(OnControlClicked);
                controlButton.onClick.AddListener(OnControlClicked);
            }
            if (languageButton != null)
            {
                languageButton.onClick.RemoveListener(OnLanguageClicked);
                languageButton.onClick.AddListener(OnLanguageClicked);
            }
            if (accessibilityButton != null)
            {
                accessibilityButton.onClick.RemoveListener(OnAccessibilityClicked);
                accessibilityButton.onClick.AddListener(OnAccessibilityClicked);
            }
            if (resetDefaultsButton != null)
            {
                resetDefaultsButton.onClick.RemoveListener(OnResetDefaultsClicked);
                resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            AutoWireReferences();

            // Initialize hidden
            if (settingsModalCG != null)
            {
                settingsModalCG.alpha = 0f;
                settingsModalCG.blocksRaycasts = false;
                settingsModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = false;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one * 0.95f;
            }
            if (toastCG != null)
            {
                toastCG.alpha = 0f;
                toastCG.blocksRaycasts = false;
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // Keyboard shortcut to close settings (Escape or P)
            bool closeRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                closeRequested = Keyboard.current.escapeKey.wasPressedThisFrame ||
                                 Keyboard.current.pKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                closeRequested = true;
            }
#endif

            if (closeRequested)
            {
                if (soundSettingsModal != null && soundSettingsModal.IsOpen)
                {
                    soundSettingsModal.CloseSoundModal();
                }
                else if (graphicsSettingsModal != null && graphicsSettingsModal.IsOpen)
                {
                    graphicsSettingsModal.CloseGraphicsModal();
                }
                else
                {
                    CloseSettings();
                }
            }
        }

        // =========================================================
        // --- MODAL CONTROLS ---
        // =========================================================

        public void OpenSettings(bool fromPause = false)
        {
            if (isOpen) return;
            isOpen = true;
            wasOpenedFromPause = fromPause;

            // Always open on the main cards view (sub-modals may have left grid/back hidden)
            if (soundSettingsModal != null && soundSettingsModal.IsOpen) soundSettingsModal.HideImmediate();
            if (graphicsSettingsModal != null && graphicsSettingsModal.IsOpen) graphicsSettingsModal.HideImmediate();
            if (contentContainerRT != null)
            {
                var grid = contentContainerRT.Find("CardsGrid");
                if (grid != null) grid.gameObject.SetActive(true);
            }
            if (backButton != null) backButton.gameObject.SetActive(true);

            // Hide gameplay HUD & joystick during Settings view for total focus
            SetHUDVisible(false);

            // If opened from pause, hide pause menu card
            if (wasOpenedFromPause && JbelPauseMenuController.Instance != null)
            {
                var pauseCG = JbelPauseMenuController.Instance.GetComponent<CanvasGroup>();
                if (pauseCG != null)
                {
                    pauseCG.alpha = 0f;
                    pauseCG.blocksRaycasts = false;
                    pauseCG.interactable = false;
                }
            }
            else
            {
                // Freeze game time if opened independently during gameplay
                Time.timeScale = 0f;
            }

            // Animate open
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(true));

            Debug.Log("<color=gold>[JbelSettings] Settings Menu Opened.</color>");
        }

        public void CloseSettings()
        {
            if (!isOpen) return;
            isOpen = false;

            if (soundSettingsModal != null && soundSettingsModal.IsOpen)
            {
                soundSettingsModal.HideImmediate();
            }
            if (graphicsSettingsModal != null && graphicsSettingsModal.IsOpen)
            {
                graphicsSettingsModal.HideImmediate();
            }

            if (contentContainerRT != null)
            {
                var grid = contentContainerRT.Find("CardsGrid");
                if (grid != null) grid.gameObject.SetActive(true);
                if (backButton != null) backButton.gameObject.SetActive(true);
            }

            // Animate close
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(false));

            if (wasOpenedFromPause && JbelPauseMenuController.Instance != null)
            {
                // Return to Pause Menu smoothly
                SetHUDVisible(true); // restore HUD under the pause menu
                JbelPauseMenuController.Instance.ShowFromSubmenu();
            }
            else
            {
                // Restore gameplay HUD & joystick
                SetHUDVisible(true);

                // Resume gameplay if opened independently
                Time.timeScale = 1f;

                var touchCtrl = Object.FindAnyObjectByType<MobileFreeLookTouchController>();
                if (touchCtrl != null) touchCtrl.isInputDisabled = false;

                var player = Object.FindAnyObjectByType<TopDownPlayerController>();
                if (player != null) player.SetControlsLocked(false);
            }

            Debug.Log("<color=green>[JbelSettings] Settings Menu Closed.</color>");
        }

        private float _savedSubtitleAlpha = 0f;

        private void SetHUDVisible(bool visible)
        {
            if (transform.parent == null) return;

            var hudPanel = transform.parent.Find("JbelHUD_Panel");
            if (hudPanel != null)
            {
                var cg = hudPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = hudPanel.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = visible ? 1f : 0f;
                cg.blocksRaycasts = visible;
                cg.interactable = visible;
            }

            var joystick = transform.parent.Find("JoystickZone");
            if (joystick != null)
            {
                joystick.gameObject.SetActive(visible);
            }

            var subtitlePanel = transform.parent.Find("SubtitleHUD_Panel");
            if (subtitlePanel != null)
            {
                var cg = subtitlePanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    if (!visible) { if (cg.alpha > 0f) _savedSubtitleAlpha = cg.alpha; cg.alpha = 0f; }
                    else cg.alpha = _savedSubtitleAlpha; 
                }
            }
        }

        public void ToggleSettings()
        {
            if (isOpen) CloseSettings();
            else OpenSettings(fromPause: false);
        }

        // =========================================================
        // --- CARD CLICK HANDLERS ---
        // =========================================================

        public void OnSoundClicked()
        {
            Debug.Log("[JbelSettings] Sound settings clicked.");
            if (soundSettingsModal == null)
            {
                soundSettingsModal = Object.FindAnyObjectByType<JbelSoundSettingsModal>(FindObjectsInactive.Include);
            }

            if (soundSettingsModal != null)
            {
                if (contentContainerRT != null)
                {
                    var grid = contentContainerRT.Find("CardsGrid");
                    if (grid != null) grid.gameObject.SetActive(false);
                    if (backButton != null) backButton.gameObject.SetActive(false);
                }

                soundSettingsModal.OpenSoundModal(onClose: () =>
                {
                    if (contentContainerRT != null)
                    {
                        var grid = contentContainerRT.Find("CardsGrid");
                        if (grid != null) grid.gameObject.SetActive(true);
                        if (backButton != null) backButton.gameObject.SetActive(true);
                    }
                });
            }
            else
            {
                ShowToast("SOUND SETTINGS");
            }
        }

        public void OnGraphicsClicked()
        {
            Debug.Log("[JbelSettings] Graphics settings clicked.");
            if (graphicsSettingsModal == null)
            {
                graphicsSettingsModal = Object.FindAnyObjectByType<JbelGraphicsSettingsModal>(FindObjectsInactive.Include);
            }

            if (graphicsSettingsModal != null)
            {
                if (contentContainerRT != null)
                {
                    var grid = contentContainerRT.Find("CardsGrid");
                    if (grid != null) grid.gameObject.SetActive(false);
                    if (backButton != null) backButton.gameObject.SetActive(false);
                }

                graphicsSettingsModal.OpenGraphicsModal(onClose: () =>
                {
                    if (contentContainerRT != null)
                    {
                        var grid = contentContainerRT.Find("CardsGrid");
                        if (grid != null) grid.gameObject.SetActive(true);
                        if (backButton != null) backButton.gameObject.SetActive(true);
                    }
                });
            }
            else
            {
                ShowToast("GRAPHICS SETTINGS");
            }
        }

        public void OnControlClicked()
        {
            Debug.Log("[JbelSettings] Control settings clicked.");
            ShowToast("CONTROL SETTINGS");
        }

        public void OnLanguageClicked()
        {
            Debug.Log("[JbelSettings] Language settings clicked.");
            ShowToast("LANGUE: FRANÇAIS / ENGLISH");
        }

        public void OnAccessibilityClicked()
        {
            Debug.Log("[JbelSettings] Accessibility settings clicked.");
            ShowToast("ACCESSIBILITY SETTINGS");
        }

        public void OnResetDefaultsClicked()
        {
            Debug.Log("[JbelSettings] Reset to defaults clicked.");
            // Reset audio, quality, controls to defaults
            AudioListener.volume = 1f;
            QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
            ShowToast("SETTINGS RESET TO DEFAULTS");
        }

        public void ShowToast(string message)
        {
            if (toastCG == null || toastText == null) return;
            if (toastCoroutine != null) StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastCG.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.8f);

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                toastCG.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
                yield return null;
            }
            toastCG.alpha = 0f;
        }

        // =========================================================
        // --- ANIMATION ROUTINE ---
        // =========================================================

        private IEnumerator AnimateModalRoutine(bool opening)
        {
            if (settingsModalCG == null) yield break;

            if (opening)
            {
                settingsModalCG.blocksRaycasts = true;
                settingsModalCG.interactable = true;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = true;
            }

            float t = 0f;
            float startAlpha = settingsModalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = (contentContainerRT != null) ? contentContainerRT.localScale : Vector3.one;
            Vector3 targetScale = opening ? Vector3.one : Vector3.one * 0.95f;

            while (t < modalAnimationDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / modalAnimationDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                settingsModalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);

                if (dimBackdropImage != null)
                {
                    Color c = dimBackdropImage.color;
                    c.a = Mathf.Lerp(0f, dimBackdropAlpha, opening ? smoothP : (1f - smoothP));
                    dimBackdropImage.color = c;
                }

                if (contentContainerRT != null)
                {
                    contentContainerRT.localScale = Vector3.Lerp(startScale, targetScale, smoothP);
                }

                yield return null;
            }

            settingsModalCG.alpha = targetAlpha;

            if (!opening)
            {
                settingsModalCG.blocksRaycasts = false;
                settingsModalCG.interactable = false;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = false;
            }
        }

        // =========================================================
        // --- AUTO WIRING & PREVIEWS ---
        // =========================================================

        public void AutoWireReferences()
        {
            if (settingsModalCG == null) settingsModalCG = GetComponent<CanvasGroup>();
            if (dimBackdropImage == null)
            {
                var d = transform.Find("DimBackdrop");
                if (d != null) dimBackdropImage = d.GetComponent<Image>();
            }
            if (soundSettingsModal == null)
            {
                soundSettingsModal = GetComponentInChildren<JbelSoundSettingsModal>(true);
            }
            if (graphicsSettingsModal == null)
            {
                graphicsSettingsModal = GetComponentInChildren<JbelGraphicsSettingsModal>(true);
            }
            if (contentContainerRT == null)
            {
                var c = transform.Find("SettingsContainer");
                if (c != null) contentContainerRT = c.GetComponent<RectTransform>();
            }
            if (backButton == null && contentContainerRT != null)
            {
                var b = contentContainerRT.Find("BackButton");
                if (b != null) backButton = b.GetComponent<Button>();
            }

            Transform grid = (contentContainerRT != null) ? contentContainerRT.Find("CardsGrid") : null;
            if (grid != null)
            {
                if (soundButton == null)
                {
                    var c = grid.Find("Card_Sound");
                    if (c != null)
                    {
                        soundButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) soundTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) soundSubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
                if (graphicsButton == null)
                {
                    var c = grid.Find("Card_Graphics");
                    if (c != null)
                    {
                        graphicsButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) graphicsTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) graphicsSubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
                if (controlButton == null)
                {
                    var c = grid.Find("Card_Control");
                    if (c != null)
                    {
                        controlButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) controlTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) controlSubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
                if (languageButton == null)
                {
                    var c = grid.Find("Card_Language");
                    if (c != null)
                    {
                        languageButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) languageTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) languageSubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
                if (accessibilityButton == null)
                {
                    var c = grid.Find("Card_Accessibility");
                    if (c != null)
                    {
                        accessibilityButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) accessibilityTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) accessibilitySubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
                if (resetDefaultsButton == null)
                {
                    var c = grid.Find("Card_ResetDefaults");
                    if (c != null)
                    {
                        resetDefaultsButton = c.GetComponent<Button>();
                        var t1 = c.Find("TitleText"); if (t1 != null) resetDefaultsTitleText = t1.GetComponent<TextMeshProUGUI>();
                        var t2 = c.Find("SubtitleText"); if (t2 != null) resetDefaultsSubtitleText = t2.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            if (toastCG == null && contentContainerRT != null)
            {
                var t = contentContainerRT.Find("ToastNotification");
                if (t != null)
                {
                    toastCG = t.GetComponent<CanvasGroup>();
                    var txt = t.Find("ToastText");
                    if (txt != null) toastText = txt.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        // --- Editor Preview Helpers ---
        public void ApplyPreviewOpenState()
        {
            AutoWireReferences();
            if (settingsModalCG != null)
            {
                settingsModalCG.alpha = 1f;
                settingsModalCG.blocksRaycasts = true;
                settingsModalCG.interactable = true;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = dimBackdropAlpha;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = true;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one;
            }
        }

        public void ApplyPreviewClosedState()
        {
            AutoWireReferences();
            if (settingsModalCG != null)
            {
                settingsModalCG.alpha = 0f;
                settingsModalCG.blocksRaycasts = false;
                settingsModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = false;
            }
            if (contentContainerRT != null)
            {
                contentContainerRT.localScale = Vector3.one * 0.95f;
            }
        }
    }
}
